using LangChain.Databases;
using Xunit;
using FluentAssertions;

namespace LangChainPipeline.Tests;

/// <summary>
/// Tests for the TrackedVectorStore fix to verify the PR issues are resolved.
/// </summary>
public class TrackedVectorStoreTests
{
    [Fact]
    public async Task AddAsync_ShouldAddVectorsToStore()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var vectors = new List<Vector>
        {
            new()
            {
                Id = "test1",
                Text = "This is a test document",
                Embedding = [1f, 0f, 0f],
                Metadata = new Dictionary<string, object> { ["type"] = "test" }
            },
            new()
            {
                Id = "test2", 
                Text = "Another test document",
                Embedding = [0f, 1f, 0f],
                Metadata = new Dictionary<string, object> { ["type"] = "test" }
            }
        };

        // Act
        await store.AddAsync(vectors);
        var allVectors = store.GetAll().ToList();
        
        // Assert
        allVectors.Should().HaveCount(2);
        allVectors.Should().Contain(v => v.Id == "test1");
        allVectors.Should().Contain(v => v.Id == "test2");
    }

    [Fact]
    public async Task GetSimilarDocumentsAsync_ShouldReturnMostSimilarDocument()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var vectors = new List<Vector>
        {
            new()
            {
                Id = "doc1",
                Text = "Machine learning is fascinating",
                Embedding = [1f, 0.5f, 0f],
                Metadata = new Dictionary<string, object> { ["category"] = "ai" }
            },
            new()
            {
                Id = "doc2",
                Text = "Cooking recipes are useful",
                Embedding = [0f, 0f, 1f],
                Metadata = new Dictionary<string, object> { ["category"] = "cooking" }
            }
        };

        await store.AddAsync(vectors);
        
        // Act
        var queryEmbedding = new[] { 0.9f, 0.4f, 0.1f };
        var results = await store.GetSimilarDocumentsAsync(queryEmbedding, amount: 1);
        
        // Assert
        results.Should().HaveCount(1);
        results.First().PageContent.Should().Contain("Machine learning");
    }

    [Fact]
    public async Task ClearAsync_ShouldRemoveAllVectors()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var vector = new Vector
        {
            Id = "clear-test",
            Text = "Document to be cleared",
            Embedding = [1f, 1f, 1f]
        };

        await store.AddAsync(new[] { vector });
        
        // Verify it was added
        var beforeClear = store.GetAll().ToList();
        beforeClear.Should().HaveCount(1);
        
        // Act
        await store.ClearAsync();
        
        // Assert
        var afterClear = store.GetAll().ToList();
        afterClear.Should().BeEmpty();
    }

    [Fact]
    public void GetAll_ShouldReturnEmptyCollectionInitially()
    {
        // Arrange
        var store = new TrackedVectorStore();
        
        // Act
        var initialVectors = store.GetAll().ToList();
        
        // Assert
        initialVectors.Should().BeEmpty();
    }
}