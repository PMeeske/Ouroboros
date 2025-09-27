using LangChain.Databases;
using LangChain.DocumentLoaders;
using LangChain.Providers.Ollama;

namespace LangChainPipeline.Domain.Vectors;

/// <summary>
/// Abstraction for vector storage operations, enabling different persistence strategies.
/// </summary>
public interface IVectorStore
{
    /// <summary>
    /// Adds vectors to the store asynchronously.
    /// </summary>
    /// <param name="vectors">The vectors to add to the store.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task AddAsync(IEnumerable<Vector> vectors);

    /// <summary>
    /// Gets all vectors from the store.
    /// </summary>
    /// <returns>A read-only collection of all vectors in the store.</returns>
    IEnumerable<Vector> GetAll();

    /// <summary>
    /// Gets documents similar to the provided query using the embedding model.
    /// </summary>
    /// <param name="embed">The embedding model to use for similarity search.</param>
    /// <param name="query">The query string for similarity matching.</param>
    /// <param name="amount">The maximum number of documents to return.</param>
    /// <returns>A collection of similar documents.</returns>
    Task<IReadOnlyCollection<Document>> GetSimilarDocuments(
        OllamaEmbeddingModel embed, 
        string query, 
        int amount = 4);
}