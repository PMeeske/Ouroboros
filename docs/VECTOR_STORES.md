# Vector Store Configuration Guide

This guide explains how to configure and use different vector store implementations in Ouroboros.

## Overview

Ouroboros supports multiple vector store backends for storing and retrieving document embeddings:

- **InMemory** - Fast in-memory storage for development and testing
- **Qdrant** - Production-ready vector database with persistence
- **Pinecone** - Cloud-based vector database (planned)

## Configuration

Vector stores are configured via the `Pipeline:VectorStore` section in `appsettings.json` or through environment variables.

### In-Memory Vector Store (Default)

Best for development and testing. No external dependencies required.

**Configuration:**
```json
{
  "Pipeline": {
    "VectorStore": {
      "Type": "InMemory"
    }
  }
}
```

**Environment Variables:**
```bash
PIPELINE__VectorStore__Type=InMemory
```

**Characteristics:**
- ✅ No setup required
- ✅ Fast performance
- ❌ Data lost on restart
- ❌ Limited to single instance

## Qdrant Vector Store

Production-ready persistent vector database. Recommended for production deployments.

### Local Development with Docker Compose

**1. Start Qdrant with Docker Compose:**

```bash
docker-compose up -d qdrant
```

This starts Qdrant on:
- HTTP REST API: `http://localhost:6333`
- gRPC API: `http://localhost:6334`

**2. Configure Ouroboros:**

Update your `.env` file or `appsettings.json`:

```bash
# .env
PIPELINE__VectorStore__Type=Qdrant
PIPELINE__VectorStore__ConnectionString=http://localhost:6333
PIPELINE__VectorStore__DefaultCollection=pipeline_vectors
```

Or in `appsettings.json`:
```json
{
  "Pipeline": {
    "VectorStore": {
      "Type": "Qdrant",
      "ConnectionString": "http://localhost:6333",
      "DefaultCollection": "pipeline_vectors",
      "BatchSize": 100
    }
  }
}
```

**3. Run your application:**

```bash
dotnet run --project src/Ouroboros.CLI/Ouroboros.CLI.csproj
```

### Production Deployment

For production, use a persistent Qdrant instance:

**Option 1: Qdrant Cloud**
```bash
PIPELINE__VectorStore__Type=Qdrant
PIPELINE__VectorStore__ConnectionString=https://your-instance.qdrant.io:6333
PIPELINE__VectorStore__DefaultCollection=production_vectors
```

**Option 2: Self-Hosted Qdrant**
```bash
PIPELINE__VectorStore__Type=Qdrant
PIPELINE__VectorStore__ConnectionString=http://qdrant.your-domain.com:6333
PIPELINE__VectorStore__DefaultCollection=production_vectors
```

### Qdrant Features

- ✅ Persistent storage
- ✅ High performance similarity search
- ✅ HNSW indexing
- ✅ Metadata filtering
- ✅ Horizontal scaling
- ✅ REST and gRPC APIs
- ✅ Docker support

### Qdrant Operations

The `QdrantVectorStore` implementation provides:

- **AddAsync** - Batch insert vectors with metadata
- **GetSimilarDocumentsAsync** - Similarity search with cosine distance
- **ClearAsync** - Delete all vectors in collection
- **Automatic Collection Creation** - Collections are created on first use with proper dimensions

**Example Usage:**

```csharp
using LangChainPipeline.Domain.Vectors;
using LangChain.Databases;

// Create Qdrant store
var store = new QdrantVectorStore(
    connectionString: "http://localhost:6333",
    collectionName: "my_documents",
    logger: logger);

// Add vectors
var vectors = new List<Vector>
{
    new()
    {
        Id = "doc1",
        Text = "Machine learning documentation",
        Embedding = new[] { 0.1f, 0.2f, 0.3f },
        Metadata = new Dictionary<string, object>
        {
            ["category"] = "ml",
            ["author"] = "John Doe"
        }
    }
};

await store.AddAsync(vectors);

// Search for similar documents
var queryEmbedding = new[] { 0.15f, 0.25f, 0.35f };
var results = await store.GetSimilarDocumentsAsync(
    queryEmbedding, 
    amount: 5);

foreach (var doc in results)
{
    Console.WriteLine($"Document: {doc.PageContent}");
    Console.WriteLine($"Score: {doc.Metadata["score"]}");
}

// Cleanup
await store.DisposeAsync();
```

## Docker Compose Configuration

The included `docker-compose.yml` already configures Qdrant:

```yaml
services:
  qdrant:
    image: qdrant/qdrant:latest
    container_name: qdrant
    ports:
      - "6333:6333"  # HTTP REST API
      - "6334:6334"  # gRPC API
    volumes:
      - qdrant-data:/qdrant/storage
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:6333/health"]
      interval: 30s
      timeout: 10s
      retries: 5
```

To start the full stack with Qdrant:
```bash
docker-compose up -d
```

This starts:
- Ollama (LLM)
- Qdrant (Vector Store)
- Jaeger (Tracing)
- Redis (Caching)

## Monitoring Qdrant

Qdrant provides a web UI at `http://localhost:6333/dashboard` when running locally.

You can also check the health endpoint:
```bash
curl http://localhost:6333/health
```

List collections:
```bash
curl http://localhost:6333/collections
```

## Troubleshooting

### Connection Refused

**Problem:** Cannot connect to Qdrant
```
Failed to connect to Qdrant: Connection refused
```

**Solutions:**
1. Ensure Qdrant is running: `docker ps | grep qdrant`
2. Start Qdrant: `docker-compose up -d qdrant`
3. Check Qdrant logs: `docker logs qdrant`

### Collection Not Found

**Problem:** Collection doesn't exist
```
Collection 'pipeline_vectors' not found
```

**Solution:** The collection is created automatically on first `AddAsync` call. Ensure your vectors have embeddings.

### Dimension Mismatch

**Problem:** Vector dimensions don't match
```
Vector dimension mismatch: expected 384, got 768
```

**Solution:** Ensure all vectors use the same embedding model with consistent dimensions.

## Performance Tips

1. **Batch Operations**: Insert vectors in batches of 100-1000 for optimal performance
2. **Connection Pooling**: Reuse QdrantVectorStore instances instead of creating new ones
3. **Indexing**: Qdrant automatically uses HNSW indexing for fast similarity search
4. **Metadata**: Use metadata for filtering to reduce search scope

## Migration Guide

### From InMemory to Qdrant

1. Start Qdrant: `docker-compose up -d qdrant`
2. Update configuration to use Qdrant
3. Re-index your documents (InMemory data won't be migrated)

```bash
# Update .env
PIPELINE__VectorStore__Type=Qdrant
PIPELINE__VectorStore__ConnectionString=http://localhost:6333
```

### Testing with Qdrant

For integration tests, use Testcontainers or docker-compose:

```csharp
// Example with Docker Compose
public class QdrantIntegrationTests : IAsyncLifetime
{
    private QdrantVectorStore _store;

    public async Task InitializeAsync()
    {
        // Assumes docker-compose up -d qdrant is running
        _store = new QdrantVectorStore(
            "http://localhost:6333",
            $"test_{Guid.NewGuid()}");
    }

    public async Task DisposeAsync()
    {
        await _store.ClearAsync();
        await _store.DisposeAsync();
    }

    [Fact]
    public async Task Can_Store_And_Retrieve_Vectors()
    {
        // Test implementation
    }
}
```

## Related Documentation

- [Qdrant Documentation](https://qdrant.tech/documentation/)
- [Docker Compose Guide](../DEPLOYMENT.md)
- [Configuration Reference](CONFIGURATION_AND_SECURITY.md)
