#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using Ouroboros.Core.Configuration;
using Microsoft.Extensions.Logging;

namespace Ouroboros.Domain.Vectors;

/// <summary>
/// Factory for creating vector store instances based on configuration.
/// </summary>
public class VectorStoreFactory
{
    private readonly VectorStoreConfiguration _config;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initializes a new vector store factory.
    /// </summary>
    public VectorStoreFactory(VectorStoreConfiguration config, ILogger? logger = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger;
    }

    /// <summary>
    /// Creates a vector store based on the configured type.
    /// </summary>
    public IVectorStore Create()
    {
        _logger?.LogInformation("Creating vector store of type: {Type}", _config.Type);

        return _config.Type.ToLowerInvariant() switch
        {
            "inmemory" => CreateInMemoryStore(),
            "qdrant" => CreateQdrantStore(),
            "pinecone" => CreatePineconeStore(),
            _ => throw new NotSupportedException($"Vector store type '{_config.Type}' is not supported")
        };
    }

    private IVectorStore CreateInMemoryStore()
    {
        _logger?.LogInformation("Creating in-memory vector store");
        return new TrackedVectorStore();
    }

    private IVectorStore CreateQdrantStore()
    {
        if (string.IsNullOrEmpty(_config.ConnectionString))
        {
            throw new InvalidOperationException("Connection string is required for Qdrant vector store");
        }

        _logger?.LogInformation("Creating Qdrant vector store with connection: {Connection}",
            MaskConnectionString(_config.ConnectionString));

        return new QdrantVectorStore(_config.ConnectionString, _config.DefaultCollection, _logger);
    }

    private IVectorStore CreatePineconeStore()
    {
        if (string.IsNullOrEmpty(_config.ConnectionString))
        {
            throw new InvalidOperationException("Connection string is required for Pinecone vector store");
        }

        _logger?.LogInformation("Creating Pinecone vector store with connection: {Connection}",
            MaskConnectionString(_config.ConnectionString));

        // FUTURE: Implement PineconeVectorStore when Pinecone package is added
        // Steps to implement:
        // 1. Add NuGet package: dotnet add package Pinecone.Client
        // 2. Create PineconeVectorStore class implementing IVectorStore
        // 3. Replace this exception with: return new PineconeVectorStore(_config.ConnectionString, _logger);
        throw new NotImplementedException(
            "Pinecone vector store implementation requires Pinecone SDK package. " +
            "Add the package and implement PineconeVectorStore class. " +
            "See docs/VECTOR_STORES.md for implementation guide.");
    }

    private static string MaskConnectionString(string connectionString)
    {
        // Mask sensitive parts of connection string for logging
        if (connectionString.Length <= 10)
            return "***";

        return $"{connectionString[..5]}***{connectionString[^3..]}";
    }
}

/// <summary>
/// Extension methods for vector store factory.
/// </summary>
public static class VectorStoreFactoryExtensions
{
    /// <summary>
    /// Creates a vector store factory from pipeline configuration.
    /// </summary>
    public static VectorStoreFactory CreateVectorStoreFactory(
        this PipelineConfiguration config,
        ILogger? logger = null)
    {
        return new VectorStoreFactory(config.VectorStore, logger);
    }
}
