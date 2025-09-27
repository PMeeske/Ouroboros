using LangChain.DocumentLoaders;
using LangChain.Extensions;
using LangChain.Providers;
using LangChain.Providers.Ollama;
using LangChainPipeline.Domain.Vectors;
using LangChainPipeline.Tools;

namespace LangChainPipeline.Tools;

/// <summary>
/// A tool for performing semantic search over ingested documents.
/// </summary>
public sealed class RetrievalTool(IVectorStore store, IEmbeddingModel embed) : ITool
{
    /// <inheritdoc />
    public string Name => "search";
    
    /// <inheritdoc />
    public string Description => "Semantic search over ingested documents. Args: { q: string, k?: number }";
    
    /// <inheritdoc />
    public string JsonSchema => SchemaGenerator.GenerateSchema(typeof(RetrievalArgs));

    /// <inheritdoc />
    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            RetrievalArgs args = ToolJson.Deserialize<RetrievalArgs>(input);
            IReadOnlyCollection<Document> docs = await store.GetSimilarDocuments(embed, args.Q, amount: args.K, cancellationToken: ct);
            
            if (docs.Count == 0) 
                return Result<string, string>.Success("No relevant documents found.");

            var result = string.Join("\n---\n", docs.Select(d =>
            {
                object? name = d.Metadata.TryGetValue("name", out object? val) ? val?.ToString() : d.Metadata?["name"];
                string snippet = d.PageContent;
                if (snippet.Length > 240) 
                    snippet = snippet[..240] + "...";
                return $"[{name}] {snippet}";
            }));
            
            return Result<string, string>.Success(result);
        }
        catch (Exception ex) 
        { 
            return Result<string, string>.Failure($"Search failed: {ex.Message}"); 
        }
    }
}