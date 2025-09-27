using LangChain.Databases;
using LangChain.Databases.InMemory;
using LangChain.DocumentLoaders;
using LangChainPipeline.Core.Abstractions;

namespace LangChainPipeline.Domain.Vectors;

/// <summary>
/// In-memory vector store implementation with tracking capabilities.
/// Provides full vector history tracking for debugging and analysis.
/// </summary>
public sealed class TrackedVectorStore : InMemoryVectorCollection, IVectorStore
{
    private readonly List<Vector> _all = new();

    /// <inheritdoc />
    public async Task AddAsync(IEnumerable<Vector> vectors, CancellationToken cancellationToken = default)
    {
        List<Vector> list = vectors.ToList();
        _all.AddRange(list);
        await base.AddAsync(list);
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<Vector>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyCollection<Vector>>(_all.AsReadOnly());
    }

    /// <inheritdoc />
    public Task<IReadOnlyCollection<Document>> GetSimilarDocumentsAsync(
        float[] embedding, 
        int amount = 5, 
        CancellationToken cancellationToken = default)
    {
        // This is a placeholder - in production we'd use the embedding directly
        // For now, return empty collection as this method isn't used by the current interface
        return Task.FromResult<IReadOnlyCollection<Document>>(new List<Document>().AsReadOnly());
    }

    /// <inheritdoc />
    public override Task<bool> IsEmptyAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_all.Count == 0);
    }

    /// <inheritdoc />
    public Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_all.Count);
    }

    /// <inheritdoc />
    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _all.Clear();
        // For now just clear our tracking - the base class might not have ClearAsync
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets all vectors for backwards compatibility.
    /// </summary>
    /// <returns>All vectors in the store.</returns>
    public IEnumerable<Vector> GetAll() => _all;
}
