using LangChain.Databases;
using LangChain.Databases.InMemory;
using LangChain.DocumentLoaders;
using LangChain.Providers.Ollama;

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
    
    public IEnumerable<Vector> GetAll() => _all;

    public async Task<IReadOnlyCollection<Document>> GetSimilarDocuments(
        OllamaEmbeddingModel embed, 
        string query, 
        int amount = 4)
    {
        return await GetSimilarDocuments(embed, query, amount);
    }
}
