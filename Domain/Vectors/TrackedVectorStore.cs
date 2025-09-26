using LangChain.Databases;
using LangChain.Databases.InMemory;

namespace LangChainPipeline.Domain.Vectors;

public sealed class TrackedVectorStore : InMemoryVectorCollection
{
    private readonly List<Vector> _all = new();
    public async Task AddAsync(IEnumerable<Vector> vectors)
    {
        List<Vector> list = vectors.ToList();
        _all.AddRange(list);
        await base.AddAsync(list);
    }
    public IEnumerable<Vector> GetAll() => _all;
}
