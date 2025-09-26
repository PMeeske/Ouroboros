using LangChain.Databases;
using LangChain.DocumentLoaders;
using LangChain.Providers;
using LangChain.Providers.Ollama;
using LangChain.Splitters.Text;

namespace LangChainPipeline.Pipeline.Ingestion;

public static class InMemoryIngestion
{
    public static async Task<List<Vector>> LoadToMemory<TLoader>(
        TrackedVectorStore store,
        OllamaEmbeddingModel embedding,
        DataSource source,
        ITextSplitter splitter,
        CancellationToken ct = default)
        where TLoader : IDocumentLoader, new()
    {
        TLoader loader = new TLoader();
        List<Vector> vectors = new List<Vector>();

        foreach (Document doc in await loader.LoadAsync(source, cancellationToken: ct))
        {
            string text = doc.PageContent;
            if (string.IsNullOrWhiteSpace(text)) continue;

            IReadOnlyList<string> chunks = splitter.SplitText(text);
            int i = 0;
            foreach (string chunk in chunks)
            {
                EmbeddingResponse resp = await embedding.CreateEmbeddingsAsync(chunk, cancellationToken: ct);
                Vector vec = new Vector()
                {
                    Id = $"{(doc.Metadata != null && doc.Metadata.TryGetValue("path", out object? p) ? p?.ToString() : "doc")}#{i}",
                    Text = chunk,
                    Metadata = new Dictionary<string, object?>(doc.Metadata!)
                    {
                        ["chunkIndex"] = i,
                        ["name"] = doc.Metadata != null && doc.Metadata.TryGetValue("name", out object? n) ? n : null
                    }!,
                    Embedding = resp
                };
                vectors.Add(vec);
                i++;
            }
        }

        await store.AddAsync(vectors);
        return vectors;
    }
}
