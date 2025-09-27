using LangChain.Databases;
using LangChain.DocumentLoaders;

namespace LangChainPipeline.Core.Abstractions;

/// <summary>
/// Interface for vector store operations supporting both in-memory and persistent implementations.
/// Provides the foundation for production-ready vector storage.
/// </summary>
public interface IVectorStore
{
    /// <summary>
    /// Adds vectors to the store.
    /// </summary>
    /// <param name="vectors">The vectors to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddAsync(IEnumerable<Vector> vectors, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all vectors from the store.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>All vectors in the store.</returns>
    Task<IReadOnlyCollection<Vector>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds similar documents based on an embedding vector.
    /// </summary>
    /// <param name="embedding">The query embedding.</param>
    /// <param name="amount">Number of similar documents to return.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of similar documents.</returns>
    Task<IReadOnlyCollection<Document>> GetSimilarDocumentsAsync(
        float[] embedding, 
        int amount = 5, 
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if the store contains any vectors.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the store has vectors, false otherwise.</returns>
    Task<bool> IsEmptyAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total count of vectors in the store.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Total number of vectors.</returns>
    Task<int> CountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all vectors from the store.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ClearAsync(CancellationToken cancellationToken = default);
}