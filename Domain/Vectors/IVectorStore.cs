using LangChain.Databases;
using LangChain.DocumentLoaders;
using LangChain.Providers;

namespace LangChainPipeline.Domain.Vectors;

/// <summary>
/// Defines the contract for vector storage operations in the pipeline.
/// Uses generic IEmbeddingModel to avoid provider lock-in.
/// </summary>
public interface IVectorStore
{
    /// <summary>
    /// Adds vectors to the store asynchronously.
    /// </summary>
    /// <param name="vectors">The vectors to add to the store.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddAsync(IEnumerable<Vector> vectors, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves similar documents based on a query using semantic search.
    /// </summary>
    /// <param name="embed">The embedding model to use for query vectorization.</param>
    /// <param name="query">The query text to search for.</param>
    /// <param name="amount">The number of similar documents to retrieve.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A collection of similar documents.</returns>
    Task<IReadOnlyCollection<Document>> GetSimilarDocuments(
        IEmbeddingModel embed, 
        string query, 
        int amount = 4, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all vectors in the store.
    /// </summary>
    /// <returns>All vectors in the store.</returns>
    IEnumerable<Vector> GetAll();
}