using LangChain.DocumentLoaders;

namespace LangChainPipeline.Domain.Vectors;

/// <summary>
/// Extension methods for vector store operations.
/// Provides convenient wrappers for common retrieval patterns.
/// </summary>
public static class VectorStoreExtensions
{
    /// <summary>
    /// Retrieves similar documents from the vector store based on a text query.
    /// Automatically generates embeddings for the query text.
    /// </summary>
    /// <param name="store">The vector store to search</param>
    /// <param name="embeddingModel">Model to generate query embeddings</param>
    /// <param name="query">The text query to search for</param>
    /// <param name="amount">Number of similar documents to retrieve (default: 5)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of similar documents</returns>
    public static async Task<IReadOnlyCollection<Document>> GetSimilarDocuments(
        this IVectorStore store,
        IEmbeddingModel embeddingModel,
        string query,
        int amount = 5,
        CancellationToken cancellationToken = default)
    {
        if (store is null) throw new ArgumentNullException(nameof(store));
        if (embeddingModel is null) throw new ArgumentNullException(nameof(embeddingModel));
        if (query is null) query = string.Empty;

        float[] embedding = await embeddingModel.CreateEmbeddingsAsync(query, cancellationToken).ConfigureAwait(false);
        return await store.GetSimilarDocumentsAsync(embedding, amount, cancellationToken).ConfigureAwait(false);
    }
}
