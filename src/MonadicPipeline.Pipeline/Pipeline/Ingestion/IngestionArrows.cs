#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using LangChain.Databases;
using LangChain.DocumentLoaders;
using LangChain.Splitters.Text;

namespace LangChainPipeline.Pipeline.Ingestion;

/// <summary>
/// Provides arrow functions for data ingestion operations.
/// </summary>
public static class IngestionArrows
{
    /// <summary>
    /// Creates an ingestion arrow that loads documents into a pipeline branch.
    /// </summary>
    /// <typeparam name="TLoader">The type of document loader to use.</typeparam>
    /// <param name="embed">The embedding model for vectorizing text.</param>
    /// <param name="splitter">Optional text splitter for chunking documents.</param>
    /// <param name="tag">Optional tag for the ingestion event.</param>
    /// <returns>A step that ingests documents into the branch.</returns>
    public static Step<PipelineBranch, PipelineBranch> IngestArrow<TLoader>(
        IEmbeddingModel embed,
        ITextSplitter? splitter = null,
        string tag = "")
        where TLoader : IDocumentLoader, new()
        => async branch =>
        {
            splitter ??= new RecursiveCharacterTextSplitter(chunkSize: 2000, chunkOverlap: 200);

            List<Vector> batch = await InMemoryIngestion.LoadToMemory<TLoader>(branch.Store, embed, branch.Source, splitter);

            return branch.WithIngestEvent(string.IsNullOrEmpty(tag) ? typeof(TLoader).Name : tag, batch.Select(v => v.Id));
        };
}
