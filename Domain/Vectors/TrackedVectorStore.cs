using LangChain.Databases;
using LangChain.Databases.InMemory;
using LangChain.DocumentLoaders;
using LangChain.Extensions;
using LangChain.Providers;

namespace LangChainPipeline.Domain.Vectors;

public sealed class TrackedVectorStore : InMemoryVectorCollection, IVectorStore
{
    private readonly List<Vector> _all = new();
    
    public async Task AddAsync(IEnumerable<Vector> vectors)
    {
        List<Vector> list = vectors.ToList();
        _all.AddRange(list);
        await base.AddAsync(list);
    }
    
    /// <inheritdoc />
    async Task IVectorStore.AddAsync(IEnumerable<Vector> vectors, CancellationToken cancellationToken)
    {
        List<Vector> list = vectors.ToList();
        _all.AddRange(list);
        await base.AddAsync(list, cancellationToken);
    }
    
    /// <inheritdoc />
    /// <remarks>
    /// This implementation prevents the infinite recursion bug mentioned in the issue.
    /// The faulty version would have been:
    /// 
    /// public async Task&lt;IReadOnlyCollection&lt;Document&gt;&gt; GetSimilarDocuments(...)
    /// {
    ///     return await GetSimilarDocuments(embed, query, amount); // ❌ Infinite recursion!
    /// }
    /// 
    /// This correct version explicitly implements the interface to avoid name conflicts
    /// and delegates to the extension method with the correct signature.
    /// </remarks>
    async Task<IReadOnlyCollection<Document>> IVectorStore.GetSimilarDocuments(
        IEmbeddingModel embed, 
        string query, 
        int amount, 
        CancellationToken cancellationToken)
    {
        // Use the extension method on the base class with the correct signature
        // This avoids infinite recursion by calling the extension method, not itself
        return await this.GetSimilarDocuments(embed, query, amount);
    }
    
    public IEnumerable<Vector> GetAll() => _all;
}
