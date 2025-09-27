using Xunit;
using LangChain.Databases;

namespace LangChainPipeline.Tests;

/// <summary>
/// Tests for vector store interface and implementation.
/// </summary>
public class VectorStoreTests
{
    [Fact]
    public async Task TestTrackedVectorStoreInterface()
    {
        var store = new TrackedVectorStore();

        // Test empty store
        Assert.True(await store.IsEmptyAsync());
        Assert.Equal(0, await store.CountAsync());

        // Add some vectors
        var vectors = new List<Vector>
        {
            new() { Id = "1", Text = "Test document 1", Embedding = new float[] { 0.1f, 0.2f } },
            new() { Id = "2", Text = "Test document 2", Embedding = new float[] { 0.3f, 0.4f } }
        };

        await store.AddAsync(vectors);

        // Test non-empty store
        Assert.False(await store.IsEmptyAsync());
        Assert.Equal(2, await store.CountAsync());

        // Test GetAllAsync
        var allVectors = await store.GetAllAsync();
        Assert.Equal(2, allVectors.Count);

        // Test backwards compatibility
        var allVectorsCompat = store.GetAll();
        Assert.Equal(2, allVectorsCompat.Count());

        // Test clear
        await store.ClearAsync();
        Assert.True(await store.IsEmptyAsync());
    }

    [Fact]
    public async Task TestVectorStoreInterfaceCompliance()
    {
        IVectorStore store = new TrackedVectorStore();

        // Test interface methods work
        Assert.True(await store.IsEmptyAsync());

        var vectors = new List<Vector>
        {
            new() { Id = "test", Text = "Test", Embedding = new float[] { 1.0f } }
        };

        await store.AddAsync(vectors);
        Assert.False(await store.IsEmptyAsync());
        Assert.Equal(1, await store.CountAsync());

        // Test GetSimilarDocumentsAsync (placeholder implementation)
        var similar = await store.GetSimilarDocumentsAsync(new float[] { 1.0f, 2.0f });
        Assert.NotNull(similar);
    }
}