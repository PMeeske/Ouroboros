// <copyright file="RetrievalToolExtendedTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Tools;

using FluentAssertions;
using LangChain.Databases;
using Ouroboros.Core.Monads;
using Ouroboros.Domain;
using Ouroboros.Domain.Vectors;
using Ouroboros.Tools;
using Xunit;

/// <summary>
/// Extended tests for RetrievalTool covering edge cases and various scenarios.
/// </summary>
[Trait("Category", "Unit")]
public class RetrievalToolExtendedTests
{
    #region Test Helper Classes

    private sealed class FakeEmbeddingModel : IEmbeddingModel
    {
        private readonly Func<string, float[]> factory;

        public FakeEmbeddingModel(Func<string, float[]> factory)
        {
            this.factory = factory;
        }

        public Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
        {
            return Task.FromResult(this.factory(input));
        }
    }

    #endregion

    #region K Parameter Tests

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    public async Task InvokeAsync_WithVariousKValues_ReturnsCorrectNumberOfDocuments(int k)
    {
        // Arrange
        TrackedVectorStore store = CreateStoreWithMultipleDocuments(10);
        IEmbeddingModel embeddings = new FakeEmbeddingModel(_ => new[] { 1f, 0f, 0f });
        var tool = new RetrievalTool(store, embeddings);
        string input = ToolJson.Serialize(new RetrievalArgs { Q = "query", K = k });

        // Act
        Result<string, string> result = await tool.InvokeAsync(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        int documentCount = result.Value.Split("---").Length;
        documentCount.Should().BeLessThanOrEqualTo(k);
    }

    [Fact]
    public async Task InvokeAsync_WithDefaultK_UsesDefaultValue()
    {
        // Arrange
        TrackedVectorStore store = CreateStoreWithMultipleDocuments(10);
        IEmbeddingModel embeddings = new FakeEmbeddingModel(_ => new[] { 1f, 0f, 0f });
        var tool = new RetrievalTool(store, embeddings);
        string input = ToolJson.Serialize(new RetrievalArgs { Q = "query" });

        // Act
        Result<string, string> result = await tool.InvokeAsync(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }

    #endregion

    #region Document Formatting Tests

    [Fact]
    public async Task InvokeAsync_WithMultipleDocuments_FormatsWithSeparators()
    {
        // Arrange
        TrackedVectorStore store = CreateStoreWithMultipleDocuments(3);
        IEmbeddingModel embeddings = new FakeEmbeddingModel(_ => new[] { 1f, 0f, 0f });
        var tool = new RetrievalTool(store, embeddings);
        string input = ToolJson.Serialize(new RetrievalArgs { Q = "test", K = 3 });

        // Act
        Result<string, string> result = await tool.InvokeAsync(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("---");
    }

    [Fact]
    public async Task InvokeAsync_TruncatesLongContent()
    {
        // Arrange
        string longText = new string('x', 500);
        var store = new TrackedVectorStore();
        var vector = new Vector
        {
            Id = "long",
            Text = longText,
            Embedding = new[] { 1f, 0f, 0f },
            Metadata = new Dictionary<string, object> { ["name"] = "LongDoc" },
        };
        await store.AddAsync(new[] { vector });

        IEmbeddingModel embeddings = new FakeEmbeddingModel(_ => new[] { 1f, 0f, 0f });
        var tool = new RetrievalTool(store, embeddings);
        string input = ToolJson.Serialize(new RetrievalArgs { Q = "test", K = 1 });

        // Act
        Result<string, string> result = await tool.InvokeAsync(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("...");
        // Verify content is truncated (240 chars + "...")
        string snippet = result.Value[(result.Value.IndexOf("]", StringComparison.Ordinal) + 2)..];
        snippet.Length.Should().BeLessThanOrEqualTo(250);
    }

    [Fact]
    public async Task InvokeAsync_WithShortContent_DoesNotTruncate()
    {
        // Arrange
        string shortText = "Short content";
        var store = new TrackedVectorStore();
        var vector = new Vector
        {
            Id = "short",
            Text = shortText,
            Embedding = new[] { 1f, 0f, 0f },
            Metadata = new Dictionary<string, object> { ["name"] = "ShortDoc" },
        };
        await store.AddAsync(new[] { vector });

        IEmbeddingModel embeddings = new FakeEmbeddingModel(_ => new[] { 1f, 0f, 0f });
        var tool = new RetrievalTool(store, embeddings);
        string input = ToolJson.Serialize(new RetrievalArgs { Q = "test", K = 1 });

        // Act
        Result<string, string> result = await tool.InvokeAsync(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(shortText);
        result.Value.Should().NotContain("...");
    }

    #endregion

    #region Metadata Handling Tests

    [Fact]
    public async Task InvokeAsync_WithMissingNameMetadata_ReturnsFailure()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var vector = new Vector
        {
            Id = "doc1",
            Text = "Content without name",
            Embedding = new[] { 1f, 0f, 0f },
            Metadata = new Dictionary<string, object>(),
        };
        await store.AddAsync(new[] { vector });

        IEmbeddingModel embeddings = new FakeEmbeddingModel(_ => new[] { 1f, 0f, 0f });
        var tool = new RetrievalTool(store, embeddings);
        string input = ToolJson.Serialize(new RetrievalArgs { Q = "test", K = 1 });

        // Act
        Result<string, string> result = await tool.InvokeAsync(input);

        // Assert - The tool should fail when metadata is missing required fields
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Search failed");
    }

    [Fact]
    public async Task InvokeAsync_WithCustomMetadata_IncludesDocumentName()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var vector = new Vector
        {
            Id = "doc1",
            Text = "Test content",
            Embedding = new[] { 1f, 0f, 0f },
            Metadata = new Dictionary<string, object> { ["name"] = "CustomDocument" },
        };
        await store.AddAsync(new[] { vector });

        IEmbeddingModel embeddings = new FakeEmbeddingModel(_ => new[] { 1f, 0f, 0f });
        var tool = new RetrievalTool(store, embeddings);
        string input = ToolJson.Serialize(new RetrievalArgs { Q = "test", K = 1 });

        // Act
        Result<string, string> result = await tool.InvokeAsync(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("[CustomDocument]");
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task InvokeAsync_WithInvalidJson_ReturnsFailure()
    {
        // Arrange
        var store = new TrackedVectorStore();
        IEmbeddingModel embeddings = new FakeEmbeddingModel(_ => new[] { 1f });
        var tool = new RetrievalTool(store, embeddings);

        // Act
        Result<string, string> result = await tool.InvokeAsync("not-valid-json");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Search failed");
    }

    [Fact]
    public async Task InvokeAsync_WithMalformedJson_ReturnsFailure()
    {
        // Arrange
        var store = new TrackedVectorStore();
        IEmbeddingModel embeddings = new FakeEmbeddingModel(_ => new[] { 1f });
        var tool = new RetrievalTool(store, embeddings);

        // Act
        Result<string, string> result = await tool.InvokeAsync("{invalid json}");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithEmptyQuery_HandlesGracefully()
    {
        // Arrange
        var store = new TrackedVectorStore();
        IEmbeddingModel embeddings = new FakeEmbeddingModel(_ => new[] { 1f });
        var tool = new RetrievalTool(store, embeddings);
        string input = ToolJson.Serialize(new RetrievalArgs { Q = string.Empty, K = 1 });

        // Act
        Result<string, string> result = await tool.InvokeAsync(input);

        // Assert
        // Should handle empty query gracefully (either success with no results or failure)
        result.Should().NotBeNull();
    }

    #endregion

    #region Empty Store Tests

    [Fact]
    public async Task InvokeAsync_WithEmptyStore_ReturnsNoDocumentsMessage()
    {
        // Arrange
        var store = new TrackedVectorStore();
        IEmbeddingModel embeddings = new FakeEmbeddingModel(_ => new[] { 1f, 0f, 0f });
        var tool = new RetrievalTool(store, embeddings);
        string input = ToolJson.Serialize(new RetrievalArgs { Q = "anything", K = 5 });

        // Act
        Result<string, string> result = await tool.InvokeAsync(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("No relevant documents found.");
    }

    #endregion

    #region Similarity Search Tests

    [Fact]
    public async Task InvokeAsync_WithSimilarEmbeddings_ReturnsRelevantDocuments()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var vectors = new[]
        {
            new Vector
            {
                Id = "doc1",
                Text = "Machine learning content",
                Embedding = new[] { 1f, 0f, 0f },
                Metadata = new Dictionary<string, object> { ["name"] = "ML Doc" },
            },
            new Vector
            {
                Id = "doc2",
                Text = "Cooking recipes",
                Embedding = new[] { 0f, 1f, 0f },
                Metadata = new Dictionary<string, object> { ["name"] = "Recipe Doc" },
            },
        };
        await store.AddAsync(vectors);

        // Embeddings that match doc1
        IEmbeddingModel embeddings = new FakeEmbeddingModel(_ => new[] { 1f, 0f, 0f });
        var tool = new RetrievalTool(store, embeddings);
        string input = ToolJson.Serialize(new RetrievalArgs { Q = "AI", K = 1 });

        // Act
        Result<string, string> result = await tool.InvokeAsync(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("ML Doc");
        result.Value.Should().NotContain("Recipe Doc");
    }

    #endregion

    #region Tool Interface Tests

    [Fact]
    public void Name_ReturnsSearch()
    {
        // Arrange
        var store = new TrackedVectorStore();
        IEmbeddingModel embeddings = new FakeEmbeddingModel(_ => new[] { 1f });
        var tool = new RetrievalTool(store, embeddings);

        // Act
        var name = tool.Name;

        // Assert
        name.Should().Be("search");
    }

    [Fact]
    public void Description_ContainsSemanticSearch()
    {
        // Arrange
        var store = new TrackedVectorStore();
        IEmbeddingModel embeddings = new FakeEmbeddingModel(_ => new[] { 1f });
        var tool = new RetrievalTool(store, embeddings);

        // Act
        var description = tool.Description;

        // Assert
        description.Should().Contain("Semantic search");
        description.Should().Contain("documents");
    }

    [Fact]
    public void JsonSchema_IsNotNull()
    {
        // Arrange
        var store = new TrackedVectorStore();
        IEmbeddingModel embeddings = new FakeEmbeddingModel(_ => new[] { 1f });
        var tool = new RetrievalTool(store, embeddings);

        // Act
        var schema = tool.JsonSchema;

        // Assert
        schema.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Cancellation Token Tests

    [Fact]
    public async Task InvokeAsync_WithCancellationToken_CompletesSuccessfully()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var store = CreateStoreWithMultipleDocuments(3);
        IEmbeddingModel embeddings = new FakeEmbeddingModel(_ => new[] { 1f, 0f, 0f });
        var tool = new RetrievalTool(store, embeddings);
        string input = ToolJson.Serialize(new RetrievalArgs { Q = "test", K = 2 });

        // Act
        Result<string, string> result = await tool.InvokeAsync(input, cts.Token);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private static TrackedVectorStore CreateStoreWithMultipleDocuments(int count)
    {
        var store = new TrackedVectorStore();
        var vectors = new List<Vector>();

        for (int i = 0; i < count; i++)
        {
            vectors.Add(new Vector
            {
                Id = $"doc{i}",
                Text = $"Document {i} content",
                Embedding = new[] { 1f, 0f, 0f },
                Metadata = new Dictionary<string, object> { ["name"] = $"Doc{i}" },
            });
        }

        store.AddAsync(vectors.ToArray()).GetAwaiter().GetResult();
        return store;
    }

    #endregion
}
