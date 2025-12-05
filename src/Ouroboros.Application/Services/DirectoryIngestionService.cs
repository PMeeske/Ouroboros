using LangChain.Databases;
using LangChain.DocumentLoaders;
using LangChain.Splitters.Text;
using Ouroboros.Application.Configuration;
using IEmbeddingModel = LangChainPipeline.Domain.IEmbeddingModel;

namespace Ouroboros.Application.Services;

/// <summary>
/// Service for ingesting documents from a directory into the vector store.
/// </summary>
public static class DirectoryIngestionService
{
    /// <summary>
    /// Ingests documents from a directory according to the provided configuration.
    /// </summary>
    /// <param name="config">The ingestion configuration.</param>
    /// <param name="store">The vector store to add documents to.</param>
    /// <param name="embedModel">The embedding model to use.</param>
    /// <returns>A Result containing the ingestion result.</returns>
    public static async Task<Result<DirectoryIngestionResult>> IngestAsync(
        DirectoryIngestionConfig config,
        IVectorStore store,
        IEmbeddingModel embedModel)
    {
        try
        {
            DirectoryIngestionOptions options = CreateIngestionOptions(config);
            DirectoryDocumentLoader<FileLoader> loader = new DirectoryDocumentLoader<FileLoader>(options);
            DirectoryIngestionStats stats = new DirectoryIngestionStats();
            loader.AttachStats(stats);

            RecursiveCharacterTextSplitter splitter = new RecursiveCharacterTextSplitter(
                chunkSize: config.ChunkSize,
                chunkOverlap: config.ChunkOverlap);

            IReadOnlyCollection<Document> docs = await loader.LoadAsync(DataSource.FromPath(config.Root));
            List<Vector> vectors = await CreateVectorsAsync(docs, splitter, embedModel, config.Root);

            if (config.BatchSize > 0)
            {
                await AddVectorsBatchedAsync(store, vectors, config.BatchSize);
            }
            else
            {
                await store.AddAsync(vectors);
            }

            stats.VectorsProduced += vectors.Count;

            return Result<DirectoryIngestionResult>.Success(new DirectoryIngestionResult
            {
                VectorIds = vectors.Select(v => v.Id).ToList(),
                Stats = stats
            });
        }
        catch (Exception ex)
        {
            return Result<DirectoryIngestionResult>.Failure(
                $"Directory ingestion failed: {ex.Message}");
        }
    }

    private static DirectoryIngestionOptions CreateIngestionOptions(DirectoryIngestionConfig config)
        => new()
        {
            Recursive = config.Recursive,
            Extensions = config.Extensions.Count == 0 ? null : config.Extensions.ToArray(),
            ExcludeDirectories = config.ExcludeDirectories.Count == 0 ? null : config.ExcludeDirectories.ToArray(),
            Patterns = config.Patterns.ToArray(),
            MaxFileBytes = config.MaxFileBytes,
            ChunkSize = config.ChunkSize,
            ChunkOverlap = config.ChunkOverlap
        };

    private static async Task<List<Vector>> CreateVectorsAsync(
        IEnumerable<Document> docs,
        RecursiveCharacterTextSplitter splitter,
        IEmbeddingModel embedModel,
        string root)
    {
        List<Vector> vectors = new List<Vector>();
        int fileIndex = 0;

        foreach (Document doc in docs)
        {
            if (string.IsNullOrWhiteSpace(doc.PageContent))
            {
                fileIndex++;
                continue;
            }

            IReadOnlyList<string> chunks = splitter.SplitText(doc.PageContent);
            Dictionary<string, object> baseMetadata = BuildDocumentMetadata(doc, root, fileIndex);

            for (int chunkIdx = 0; chunkIdx < chunks.Count; chunkIdx++)
            {
                string chunk = chunks[chunkIdx];
                string vectorId = $"dir:{fileIndex}:{chunkIdx}";
                Dictionary<string, object> chunkMetadata = BuildChunkMetadata(baseMetadata, chunkIdx, chunks.Count, vectorId);

                try
                {
                    float[] embedding = await embedModel.CreateEmbeddingsAsync(chunk);
                    vectors.Add(new Vector
                    {
                        Id = vectorId,
                        Text = chunk,
                        Embedding = embedding,
                        Metadata = chunkMetadata
                    });
                }
                catch
                {
                    chunkMetadata["embedding"] = "fallback";
                    vectors.Add(new Vector
                    {
                        Id = $"{vectorId}:fallback",
                        Text = chunk,
                        Embedding = new float[8],
                        Metadata = chunkMetadata
                    });
                }
            }

            fileIndex++;
        }

        return vectors;
    }

    private static async Task AddVectorsBatchedAsync(
        IVectorStore store,
        List<Vector> vectors,
        int batchSize)
    {
        for (int i = 0; i < vectors.Count; i += batchSize)
        {
            List<Vector> batch = vectors.Skip(i).Take(batchSize).ToList();
            await store.AddAsync(batch);
        }
    }

    private static Dictionary<string, object> BuildDocumentMetadata(Document doc, string root, int fileIndex)
    {
        Dictionary<string, object> metadata = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (doc.Metadata is not null)
        {
            foreach (KeyValuePair<string, object> kvp in doc.Metadata)
            {
                metadata[kvp.Key] = kvp.Value ?? string.Empty;
            }
        }

        string? sourcePath = null;
        if (metadata.TryGetValue("source", out object? sourceObj) && sourceObj is string sourceStr)
        {
            sourcePath = sourceStr;
        }
        else if (metadata.TryGetValue("path", out object? pathObj) && pathObj is string pathStr)
        {
            sourcePath = pathStr;
        }

        if (!string.IsNullOrWhiteSpace(sourcePath))
        {
            try
            {
                sourcePath = Path.GetFullPath(sourcePath);
            }
            catch
            {
                // ignore invalid paths
            }
        }

        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            sourcePath = Path.Combine(root, $"document_{fileIndex}.md");
        }

        metadata["source"] = sourcePath;
        metadata["relative"] = TryGetRelativePath(root, sourcePath);
        return metadata;
    }

    private static Dictionary<string, object> BuildChunkMetadata(
        Dictionary<string, object> baseMetadata,
        int chunkIndex,
        int chunkCount,
        string vectorId)
    {
        Dictionary<string, object> metadata = new Dictionary<string, object>(baseMetadata, StringComparer.OrdinalIgnoreCase)
        {
            ["chunkIndex"] = chunkIndex,
            ["chunkCount"] = chunkCount,
            ["vectorId"] = vectorId
        };
        return metadata;
    }

    private static string TryGetRelativePath(string root, string path)
    {
        try
        {
            return Path.GetRelativePath(root, path);
        }
        catch
        {
            return path;
        }
    }
}

public record DirectoryIngestionResult
{
    public required IReadOnlyList<string> VectorIds { get; init; }
    public required DirectoryIngestionStats Stats { get; init; }
}

