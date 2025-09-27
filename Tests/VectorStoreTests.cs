using LangChain.Databases;
using LangChainPipeline.Domain.Vectors;
using Xunit;

namespace LangChainPipeline.Tests;

/// <summary>
/// Tests for the IVectorStore interface and implementations.
/// </summary>
public class VectorStoreTests
{
    /// <summary>
    /// Tests basic vector store operations.
    /// </summary>
    [Fact]
    public async Task TestTrackedVectorStoreBasicOperations()
    {
        var vectorStore = new TrackedVectorStore();
        
        // Create some test vectors
        var vectors = new[]
        {
            new Vector { Embedding = [1.0f, 2.0f, 3.0f], Text = "Test vector 1" },
            new Vector { Embedding = [4.0f, 5.0f, 6.0f], Text = "Test vector 2" }
        };
        
        // Add vectors
        await vectorStore.AddAsync(vectors);
        
        // Verify they were added
        var allVectors = vectorStore.GetAll();
        Assert.Equal(2, allVectors.Count());
        
        // Verify the vectors are correct
        var vectorList = allVectors.ToList();
        Assert.NotNull(vectorList[0].Embedding);
        Assert.NotNull(vectorList[1].Embedding);
        Assert.True(vectorList[0].Embedding!.SequenceEqual([1.0f, 2.0f, 3.0f]));
        Assert.True(vectorList[1].Embedding!.SequenceEqual([4.0f, 5.0f, 6.0f]));
    }

    /// <summary>
    /// Tests that TrackedVectorStore implements IVectorStore correctly.
    /// </summary>
    [Fact]
    public void TestTrackedVectorStoreImplementsInterface()
    {
        IVectorStore vectorStore = new TrackedVectorStore();
        Assert.NotNull(vectorStore);
        
        // Verify it's the correct implementation
        Assert.IsType<TrackedVectorStore>(vectorStore);
    }
}