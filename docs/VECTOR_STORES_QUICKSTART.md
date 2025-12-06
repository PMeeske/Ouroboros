# Quick Start: Using Qdrant Vector Store Locally

This quick guide shows you how to use the Qdrant vector store in Ouroboros for local development.

## Prerequisites

- Docker and Docker Compose installed
- .NET 10.0 SDK installed

## Steps

### 1. Start Qdrant

```bash
cd /path/to/Ouroboros
docker-compose up -d qdrant
```

Verify Qdrant is running:
```bash
curl http://localhost:6333/health
# Should return: {"status":"ok"}
```

### 2. Configure Ouroboros

Create or update `.env` file:

```bash
# Copy from example
cp .env.example .env

# Update vector store configuration
PIPELINE__VectorStore__Type=Qdrant
PIPELINE__VectorStore__ConnectionString=http://localhost:6333
PIPELINE__VectorStore__DefaultCollection=pipeline_vectors
```

Or update `appsettings.Development.json`:

```json
{
  "Pipeline": {
    "VectorStore": {
      "Type": "Qdrant",
      "ConnectionString": "http://localhost:6333",
      "DefaultCollection": "pipeline_vectors"
    }
  }
}
```

### 3. Run the Application

```bash
dotnet run --project src/Ouroboros.CLI/Ouroboros.CLI.csproj
```

## What's Happening

1. **Automatic Collection Creation**: When you first add vectors, QdrantVectorStore automatically creates a collection with the correct vector dimensions
2. **Persistent Storage**: All vectors are stored in Qdrant's persistent storage (volume mount)
3. **Similarity Search**: Use cosine similarity to find similar documents

## Example Usage in Code

```csharp
using LangChainPipeline.Core.Configuration;
using LangChainPipeline.Domain.Vectors;

// Create factory from configuration
var config = new VectorStoreConfiguration
{
    Type = "Qdrant",
    ConnectionString = "http://localhost:6333",
    DefaultCollection = "my_vectors"
};

var factory = new VectorStoreFactory(config);
var store = factory.Create(); // Returns QdrantVectorStore

// Use the store...
await store.AddAsync(vectors);
var results = await store.GetSimilarDocumentsAsync(queryEmbedding);
```

## Viewing Qdrant Data

Access the Qdrant Web UI at: http://localhost:6333/dashboard

## Stopping Qdrant

```bash
docker-compose stop qdrant
```

To remove data:
```bash
docker-compose down -v  # Removes volumes (data will be lost!)
```

## Troubleshooting

**Problem**: Can't connect to Qdrant
```bash
# Check if Qdrant is running
docker ps | grep qdrant

# View logs
docker logs qdrant

# Restart Qdrant
docker-compose restart qdrant
```

**Problem**: Port 6333 already in use
```bash
# Check what's using the port
lsof -i :6333  # macOS/Linux
netstat -ano | findstr :6333  # Windows

# Option 1: Stop the conflicting service
# Option 2: Change Qdrant port in docker-compose.yml
```

## Next Steps

- See [VECTOR_STORES.md](VECTOR_STORES.md) for detailed documentation
- Learn about [production deployment](../DEPLOYMENT.md)
- Explore [configuration options](../CONFIGURATION_AND_SECURITY.md)
