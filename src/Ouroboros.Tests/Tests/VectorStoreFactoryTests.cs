// <copyright file="VectorStoreFactoryTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests;

using FluentAssertions;
using Ouroboros.Core.Configuration;
using Ouroboros.Domain.Vectors;
using Xunit;

/// <summary>
/// Tests for vector store factory functionality.
/// </summary>
public class VectorStoreFactoryTests
{
    [Fact]
    public void Create_InMemoryType_ReturnsTrackedVectorStore()
    {
        // Arrange
        var config = new VectorStoreConfiguration
        {
            Type = "InMemory",
        };
        var factory = new VectorStoreFactory(config);

        // Act
        var store = factory.Create();

        // Assert
        store.Should().NotBeNull();
        store.Should().BeOfType<TrackedVectorStore>();
    }

    [Fact]
    public void Create_InMemoryTypeLowerCase_ReturnsTrackedVectorStore()
    {
        // Arrange
        var config = new VectorStoreConfiguration
        {
            Type = "inmemory",
        };
        var factory = new VectorStoreFactory(config);

        // Act
        var store = factory.Create();

        // Assert
        store.Should().NotBeNull();
        store.Should().BeOfType<TrackedVectorStore>();
    }

    [Fact]
    public void Create_QdrantTypeWithoutConnectionString_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new VectorStoreConfiguration
        {
            Type = "Qdrant",
            ConnectionString = null,
        };
        var factory = new VectorStoreFactory(config);

        // Act
        Action act = () => factory.Create();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Connection string is required*");
    }

    [Fact]
    public void Create_PineconeTypeWithoutConnectionString_ThrowsInvalidOperationException()
    {
        // Arrange
        var config = new VectorStoreConfiguration
        {
            Type = "Pinecone",
            ConnectionString = null,
        };
        var factory = new VectorStoreFactory(config);

        // Act
        Action act = () => factory.Create();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Connection string is required*");
    }

    [Fact]
    public void Create_QdrantTypeWithConnectionString_ReturnsQdrantVectorStore()
    {
        // Arrange
        var config = new VectorStoreConfiguration
        {
            Type = "Qdrant",
            ConnectionString = "http://localhost:6333",
        };
        var factory = new VectorStoreFactory(config);

        // Act
        var store = factory.Create();

        // Assert
        store.Should().NotBeNull();
        store.Should().BeOfType<QdrantVectorStore>();
    }

    [Fact]
    public void Create_PineconeTypeWithConnectionString_ThrowsNotImplementedException()
    {
        // Arrange
        var config = new VectorStoreConfiguration
        {
            Type = "Pinecone",
            ConnectionString = "https://pinecone-api.io",
        };
        var factory = new VectorStoreFactory(config);

        // Act
        Action act = () => factory.Create();

        // Assert
        act.Should().Throw<NotImplementedException>()
            .WithMessage("*Pinecone vector store implementation*");
    }

    [Fact]
    public void Create_UnsupportedType_ThrowsNotSupportedException()
    {
        // Arrange
        var config = new VectorStoreConfiguration
        {
            Type = "UnsupportedType",
        };
        var factory = new VectorStoreFactory(config);

        // Act
        Action act = () => factory.Create();

        // Assert
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*UnsupportedType*is not supported*");
    }

    [Fact]
    public void CreateVectorStoreFactory_FromPipelineConfiguration_CreatesFactory()
    {
        // Arrange
        var pipelineConfig = new PipelineConfiguration
        {
            VectorStore = new VectorStoreConfiguration
            {
                Type = "InMemory",
                BatchSize = 200
            },
        };

        // Act
        var factory = pipelineConfig.CreateVectorStoreFactory();

        // Assert
        factory.Should().NotBeNull();
        var store = factory.Create();
        store.Should().BeOfType<TrackedVectorStore>();
    }

    [Fact]
    public void Constructor_NullConfig_ThrowsArgumentNullException()
    {
        // Arrange, Act & Assert
        Action act = () => new VectorStoreFactory(null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("config");
    }
}
