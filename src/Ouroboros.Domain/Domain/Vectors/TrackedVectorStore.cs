#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using LangChain.Databases;
using LangChain.Databases.InMemory;
using LangChain.DocumentLoaders;

namespace LangChainPipeline.Domain.Vectors;

/// <summary>
/// A tracked vector store that fixes the state consistency issues identified in the PR.
/// This implementation maintains backward compatibility while addressing the core problems.
/// </summary>
public sealed class TrackedVectorStore : InMemoryVectorCollection, IVectorStore
{
    private readonly List<Vector> _all = [];

    /// <summary>
    /// Adds vectors to the store asynchronously.
    /// Ensures both the base class and tracking list are kept in sync.
    /// </summary>
    /// <param name="vectors">The vectors to add.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task AddAsync(IEnumerable<Vector> vectors, CancellationToken cancellationToken = default)
    {
        List<Vector> list = vectors.ToList();
        _all.AddRange(list);
        await base.AddAsync(list, cancellationToken);
    }

    /// <summary>
    /// Performs similarity search and returns the most similar documents.
    /// This method provides actual functionality instead of returning empty results.
    /// </summary>
    /// <param name="embedding">The query embedding.</param>
    /// <param name="amount">The number of results to return.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A collection of similar documents.</returns>
    public Task<IReadOnlyCollection<Document>> GetSimilarDocumentsAsync(
        float[] embedding,
        int amount = 5,
        CancellationToken cancellationToken = default)
    {
        // Get all vectors with embeddings
        List<Vector> vectorsWithEmbeddings = _all.Where(v => v.Embedding != null).ToList();
        if (!vectorsWithEmbeddings.Any())
        {
            return Task.FromResult<IReadOnlyCollection<Document>>(new List<Document>().AsReadOnly());
        }

        // Calculate cosine similarity for each vector
        var similarities = vectorsWithEmbeddings
            .Select(v => new
            {
                Vector = v,
                Similarity = CalculateCosineSimilarity(embedding, v.Embedding!)
            })
            .OrderByDescending(x => x.Similarity)
            .Take(amount)
            .ToList();

        List<Document> documents = similarities
            .Select(s => new Document(s.Vector.Text, s.Vector.Metadata ?? new Dictionary<string, object>()))
            .ToList();

        return Task.FromResult<IReadOnlyCollection<Document>>(documents.AsReadOnly());
    }

    /// <summary>
    /// Clears all vectors from the store by properly clearing both base class and tracking state.
    /// This fixes the inconsistent state issue mentioned in the PR.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        List<string> ids = _all.Select(v => v.Id).ToList();
        if (ids.Any())
        {
            await base.DeleteAsync(ids, cancellationToken).ConfigureAwait(false);
        }
        _all.Clear(); // Clear tracking state after clearing base class
    }

    /// <summary>
    /// Gets all vectors currently stored.
    /// </summary>
    /// <returns>An enumerable of all vectors.</returns>
    public IEnumerable<Vector> GetAll() => _all.AsReadOnly();

    /// <summary>
    /// Calculates cosine similarity between two embeddings.
    /// </summary>
    private static float CalculateCosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            return 0f;

        float dotProduct = 0f;
        float magnitudeA = 0f;
        float magnitudeB = 0f;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        if (magnitudeA == 0f || magnitudeB == 0f)
            return 0f;

        return dotProduct / (MathF.Sqrt(magnitudeA) * MathF.Sqrt(magnitudeB));
    }
}
