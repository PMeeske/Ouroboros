// <copyright file="RetrievalToolTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using LangChain.Databases;
using Ouroboros.Domain;
using Ouroboros.Domain.Vectors;
using Ouroboros.Core.Monads;
using Ouroboros.Tools;
using Xunit;

/// <summary>
/// Tests for the RetrievalTool to ensure semantic search responses are formatted correctly.
/// </summary>
public class RetrievalToolTests
{
    [Fact]
    public async Task InvokeAsync_WithMatchingDocuments_ReturnsFormattedResult()
    {
        // Arrange
        TrackedVectorStore store = CreateStoreWithDocuments();
        IEmbeddingModel embeddings = new FakeEmbeddingModel(_ => new[] { 1f, 0f, 0f });
        var tool = new RetrievalTool(store, embeddings);
        string input = ToolJson.Serialize(new RetrievalArgs { Q = "AI", K = 1 });

        // Act
        Result<string, string> result = await tool.InvokeAsync(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("[Doc1]");
        result.Value.Should().Contain("Machine learning");
        result.Value.Should().NotContain("Doc2");
    }

    [Fact]
    public async Task InvokeAsync_WhenNoDocumentsFound_ReturnsFriendlyMessage()
    {
        // Arrange
        var store = new TrackedVectorStore();
        IEmbeddingModel embeddings = new FakeEmbeddingModel(_ => new[] { 0f, 1f, 0f });
        var tool = new RetrievalTool(store, embeddings);
        string input = ToolJson.Serialize(new RetrievalArgs { Q = "nothing", K = 2 });

        // Act
        Result<string, string> result = await tool.InvokeAsync(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("No relevant documents found.");
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidJson_ReturnsFailure()
    {
        // Arrange
        var store = new TrackedVectorStore();
        IEmbeddingModel embeddings = new FakeEmbeddingModel(_ => new[] { 1f });
        var tool = new RetrievalTool(store, embeddings);

        // Act
        Result<string, string> result = await tool.InvokeAsync("not-json");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Search failed");
    }

    [Fact]
    public async Task InvokeAsync_TruncatesLongSnippets()
    {
        // Arrange
        string longText = new string('a', 300);
        TrackedVectorStore store = new TrackedVectorStore();
        Vector vector = new Vector
        {
            Id = "long",
            Text = longText,
            Embedding = new[] { 1f, 0f, 0f },
            Metadata = new Dictionary<string, object> { ["name"] = "LongDoc" },
        };
        await store.AddAsync(new[] { vector });

        IEmbeddingModel embeddings = new FakeEmbeddingModel(_ => new[] { 1f, 0f, 0f });
        var tool = new RetrievalTool(store, embeddings);
        string input = ToolJson.Serialize(new RetrievalArgs { Q = "long", K = 1 });

        // Act
        Result<string, string> result = await tool.InvokeAsync(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("[LongDoc]");
        result.Value.Should().Contain("...");
        string snippet = result.Value[(result.Value.IndexOf("]", StringComparison.Ordinal) + 2)..];
        snippet.Length.Should().BeLessThanOrEqualTo(243);
        snippet.Should().Contain(longText[..240]);
    }

    private static TrackedVectorStore CreateStoreWithDocuments()
    {
        var store = new TrackedVectorStore();
        Vector[] vectors =
        [
            new Vector
            {
                Id = "doc1",
                Text = "Machine learning is fascinating",
                Embedding = new[] { 1f, 0f, 0f },
                Metadata = new Dictionary<string, object> { ["name"] = "Doc1" },
            },
            new Vector
            {
                Id = "doc2",
                Text = "Baking cakes requires patience",
                Embedding = new[] { 0f, 1f, 0f },
                Metadata = new Dictionary<string, object> { ["name"] = "Doc2" },
            },
        ];

        store.AddAsync(vectors).GetAwaiter().GetResult();
        return store;
    }

    private sealed class FakeEmbeddingModel : IEmbeddingModel
    {
        private readonly Func<string, float[]> factory;

        public FakeEmbeddingModel(Func<string, float[]> factory)
        {
            this.factory = factory;
        }

        public Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
        {
            _ = input;
            _ = ct;
            return Task.FromResult(this.factory(input));
        }
    }
}
