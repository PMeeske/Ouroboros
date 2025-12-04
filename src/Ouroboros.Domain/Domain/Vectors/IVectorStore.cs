#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using LangChain.Databases;
using LangChain.DocumentLoaders;

namespace LangChainPipeline.Domain.Vectors;

/// <summary>
/// Defines the contract for a vector store that can store and retrieve vectors with similarity search capabilities.
/// </summary>
public interface IVectorStore
{
    /// <summary>
    /// Adds vectors to the store asynchronously.
    /// </summary>
    /// <param name="vectors">The vectors to add.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddAsync(IEnumerable<Vector> vectors, CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs similarity search and returns the most similar documents.
    /// </summary>
    /// <param name="embedding">The query embedding.</param>
    /// <param name="amount">The number of results to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of similar documents.</returns>
    Task<IReadOnlyCollection<Document>> GetSimilarDocumentsAsync(
        float[] embedding,
        int amount = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears all vectors from the store asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all vectors currently stored in the vector store.
    /// </summary>
    /// <returns>An enumerable of all vectors.</returns>
    IEnumerable<Vector> GetAll();
}
