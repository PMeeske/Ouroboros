using LangChain.DocumentLoaders;

namespace LangChainPipeline.Domain.Vectors;

public static class VectorStoreExtensions
{
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

        var embedding = await embeddingModel.CreateEmbeddingsAsync(query, cancellationToken).ConfigureAwait(false);
        return await store.GetSimilarDocumentsAsync(embedding, amount, cancellationToken).ConfigureAwait(false);
    }
}
