// <copyright file="QdrantVectorStoreTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests;

using FluentAssertions;
using Ouroboros.Domain.Vectors;
using Microsoft.Extensions.Logging;
using Xunit;

/// <summary>
/// Unit tests for QdrantVectorStore.
/// Note: These tests verify construction and basic error handling.
/// Integration tests with a real Qdrant instance would require docker-compose setup.
/// </summary>
[Trait("Category", "Unit")]
public class QdrantVectorStoreTests
{
    [Fact]
    public void Constructor_WithValidConnectionString_ShouldCreateInstance()
    {
        // Arrange
        var connectionString = "http://localhost:6333";
        var collectionName = "test_collection";

        // Act
        var store = new QdrantVectorStore(connectionString, collectionName);

        // Assert
        store.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullConnectionString_ShouldThrowArgumentException()
    {
        // Arrange
        string? connectionString = null;

        // Act
        Action act = () => new QdrantVectorStore(connectionString!, "test");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Connection string cannot be null or empty*");
    }

    [Fact]
    public void Constructor_WithEmptyConnectionString_ShouldThrowArgumentException()
    {
        // Arrange
        var connectionString = string.Empty;

        // Act
        Action act = () => new QdrantVectorStore(connectionString, "test");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Connection string cannot be null or empty*");
    }

    [Fact]
    public void Constructor_WithNullCollectionName_ShouldThrowArgumentException()
    {
        // Arrange
        var connectionString = "http://localhost:6333";
        string? collectionName = null;

        // Act
        Action act = () => new QdrantVectorStore(connectionString, collectionName!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Collection name cannot be null or empty*");
    }

    [Fact]
    public void Constructor_WithEmptyCollectionName_ShouldThrowArgumentException()
    {
        // Arrange
        var connectionString = "http://localhost:6333";
        var collectionName = string.Empty;

        // Act
        Action act = () => new QdrantVectorStore(connectionString, collectionName);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Collection name cannot be null or empty*");
    }

    [Fact]
    public void Constructor_WithHttpsConnectionString_ShouldCreateInstance()
    {
        // Arrange
        var connectionString = "https://cloud.qdrant.io:6333";
        var collectionName = "secure_collection";

        // Act
        var store = new QdrantVectorStore(connectionString, collectionName);

        // Assert
        store.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithCustomPort_ShouldCreateInstance()
    {
        // Arrange
        var connectionString = "http://localhost:6334"; // gRPC port
        var collectionName = "custom_port_collection";

        // Act
        var store = new QdrantVectorStore(connectionString, collectionName);

        // Assert
        store.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithLogger_ShouldCreateInstance()
    {
        // Arrange
        var connectionString = "http://localhost:6333";
        var collectionName = "logged_collection";
        var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        var logger = loggerFactory.CreateLogger<QdrantVectorStore>();

        // Act
        var store = new QdrantVectorStore(connectionString, collectionName, logger);

        // Assert
        store.Should().NotBeNull();
    }

    [Fact]
    public void GetAll_ShouldReturnEmptyForQdrantStore()
    {
        // Arrange
        var connectionString = "http://localhost:6333";
        var store = new QdrantVectorStore(connectionString, "test_collection");

        // Act
        var result = store.GetAll();

        // Assert
        // Qdrant GetAll returns empty as it's not a recommended operation
        result.Should().NotBeNull();
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task DisposeAsync_ShouldNotThrow()
    {
        // Arrange
        var connectionString = "http://localhost:6333";
        var store = new QdrantVectorStore(connectionString, "test_collection");

        // Act
        Func<Task> act = async () => await store.DisposeAsync();

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Theory]
    [InlineData("http://localhost:6333")]
    [InlineData("https://qdrant.example.com:6333")]
    [InlineData("http://192.168.1.100:6334")]
    public void Constructor_WithVariousValidConnectionStrings_ShouldCreateInstance(string connectionString)
    {
        // Act
        var store = new QdrantVectorStore(connectionString, "test");

        // Assert
        store.Should().NotBeNull();
    }
}
