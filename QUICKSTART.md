# Ouroboros Quick Start Guide

Get up and running with Ouroboros in 5 minutes!

## Prerequisites

1. **.NET 10.0 SDK** - [Download here](https://dotnet.microsoft.com/download/dotnet/10.0)
2. **Ollama** (optional, for local LLM) - [Download here](https://ollama.ai/)

## Quick Setup

### Option 1: With Ollama (Local LLM)

1. **Install Ollama and pull models:**
   ```bash
   # Install from https://ollama.ai/
   ollama pull llama3
   ollama pull nomic-embed-text
   ```

2. **Build and run:**
   ```bash
   cd Ouroboros
   dotnet build
   cd src/Ouroboros.CLI
   dotnet run -- ask -q "What is functional programming?"
   ```

### Option 2: Without Ollama (Mock/Test Mode)

The tests include mock implementations, so you can explore the codebase without Ollama:

```bash
cd Ouroboros
dotnet test --filter "FullyQualifiedName!~Features"
```

## Common Commands

### CLI Commands

```bash
cd src/Ouroboros.CLI

# Ask a question
dotnet run -- ask -q "Explain monads"

# Run a pipeline
dotnet run -- pipeline -d "SetTopic('AI') | UseDraft | UseCritique"

# List available operations
dotnet run -- list

# Run examples
cd ../Ouroboros.Examples
dotnet run
```

### Web API

```bash
cd src/Ouroboros.WebApi
dotnet run

# API available at http://localhost:8080
# Swagger UI at http://localhost:8080/
```

### Docker

```bash
# Start all services (Ollama, Qdrant, WebAPI)
docker-compose up -d

# Check status
docker-compose ps

# View logs
docker-compose logs -f monadic-pipeline-webapi
```

## Configuration

All configuration is in `.env` file (already created with defaults).

Key settings:
- **LLM Endpoint**: `PIPELINE__LlmProvider__OllamaEndpoint`
- **Default Model**: `PIPELINE__LlmProvider__DefaultChatModel`
- **Vector Store**: `PIPELINE__VectorStore__Type` (InMemory, Qdrant, Pinecone)

## What's Working Out of the Box

✅ **Core Monadic Operations**
- Result<T> and Option<T> monads
- Kleisli arrow composition
- Type-safe error handling

✅ **Pipeline System**
- Draft → Critique → Improve workflow
- Event sourcing with replay
- Branch management

✅ **Tools & Extensions**
- Math tool
- GitHub scope lock tool
- Retrieval (RAG) tool
- Custom tool creation via ToolBuilder

✅ **AI Orchestration**
- Automatic model selection
- Use case classification
- Performance tracking

✅ **Testing**
- 300+ unit tests
- BDD-style feature tests
- Comprehensive coverage

## Troubleshooting

### "Ollama not responding"
- Ensure Ollama is running: `ollama list`
- Check endpoint: `curl http://localhost:11434/api/tags`

### "Model not found"
- Pull the model: `ollama pull llama3`
- Check available models: `ollama list`

### Build errors
- Ensure .NET 10.0 SDK is installed: `dotnet --version`
- Clean and rebuild: `dotnet clean; dotnet build`

## Next Steps

1. **Read the Architecture Docs**: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)
2. **Explore Examples**: [src/Ouroboros.Examples](src/Ouroboros.Examples)
3. **Try the Web API**: [src/Ouroboros.WebApi/README.md](src/Ouroboros.WebApi/README.md)
4. **Deploy to K8s**: [DEPLOYMENT.md](DEPLOYMENT.md)

## Support

- 📖 **Full Documentation**: [README.md](README.md)
- 🐛 **Issues**: [GitHub Issues](https://github.com/PMeeske/Ouroboros/issues)
- 💬 **Discussions**: [GitHub Discussions](https://github.com/PMeeske/Ouroboros/discussions)
