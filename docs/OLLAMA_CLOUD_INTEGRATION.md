# Ollama Cloud Integration Guide

This guide explains how to use Ouroboros with Ollama Cloud, a remote hosted Ollama service that provides API access to various LLM models with client authentication.

## Overview

Ouroboros supports three types of remote LLM endpoints:

1. **Ollama Cloud** - Native Ollama API format (recommended for Ollama Cloud)
2. **OpenAI-Compatible** - Standard OpenAI API format
3. **Auto-Detection** - Automatically detects endpoint type based on URL

## Quick Start

### 1. Using Environment Variables

The simplest way to configure Ollama Cloud is through environment variables:

```bash
# Set up Ollama Cloud credentials
export CHAT_ENDPOINT="https://api.ollama.com"
export CHAT_API_KEY="your-ollama-cloud-api-key"
export CHAT_ENDPOINT_TYPE="ollama-cloud"  # or "auto" for auto-detection

# Now run any CLI command
cd src/Ouroboros.CLI

# Ask a question
dotnet run -- ask -q "What is functional programming?"

# Run the orchestrator
dotnet run -- orchestrator --goal "Explain monads"

# Run MeTTa orchestrator
dotnet run -- metta --goal "Plan a web scraping task"

# Run a pipeline
dotnet run -- pipeline -d "SetTopic('AI') | UseDraft | UseCritique"
```

### 2. Using CLI Flags

You can also provide credentials directly via CLI flags (overrides environment variables):

```bash
cd src/Ouroboros.CLI

# Ask command
dotnet run -- ask -q "Hello from Ollama Cloud" \
  --endpoint "https://api.ollama.com" \
  --api-key "your-api-key" \
  --endpoint-type "ollama-cloud"

# Orchestrator command
dotnet run -- orchestrator \
  --goal "Generate a Python function" \
  --endpoint "https://api.ollama.com" \
  --api-key "your-api-key" \
  --endpoint-type "ollama-cloud"

# MeTTa orchestrator command
dotnet run -- metta \
  --goal "Design a recommendation system" \
  --endpoint "https://api.ollama.com" \
  --api-key "your-api-key" \
  --endpoint-type "ollama-cloud"

# Pipeline command
dotnet run -- pipeline \
  -d "SetTopic('AI') | UseDraft" \
  --endpoint "https://api.ollama.com" \
  --api-key "your-api-key" \
  --endpoint-type "ollama-cloud"
```

## Configuration Options

### Endpoint Types

The `--endpoint-type` (or `CHAT_ENDPOINT_TYPE`) parameter supports three values:

1. **`auto`** (default) - Automatically detects based on URL:
   - URLs containing `api.ollama.com` or `ollama.cloud` → Ollama Cloud format
   - Other URLs → OpenAI-compatible format

2. **`ollama-cloud`** - Forces Ollama's native API format:
   - Uses `/api/generate` endpoint for chat
   - Uses `/api/embeddings` endpoint for embeddings
   - Includes `Authorization: Bearer <api-key>` header

3. **`openai`** - Forces OpenAI-compatible API format:
   - Uses `/v1/responses` endpoint
   - Standard OpenAI API structure

### Using .env File

For persistent configuration, copy `.env.example` to `.env` and configure:

```bash
# Copy example file
cp .env.example .env

# Edit .env and uncomment/set these lines:
CHAT_ENDPOINT=https://api.ollama.com
CHAT_API_KEY=your-ollama-cloud-api-key
CHAT_ENDPOINT_TYPE=ollama-cloud
```

## Supported Commands

All CLI commands support Ollama Cloud authentication:

### Ask Command
```bash
dotnet run -- ask -q "Your question" \
  --endpoint "https://api.ollama.com" \
  --api-key "your-key"
```

Options: `--rag`, `--agent`, `--router`, `--model`, `--temperature`, etc.

### Pipeline Command
```bash
dotnet run -- pipeline \
  -d "Your DSL pipeline" \
  --endpoint "https://api.ollama.com" \
  --api-key "your-key"
```

Options: `--model`, `--embed`, `--source`, `--trace`, `--debug`, etc.

### Orchestrator Command
```bash
dotnet run -- orchestrator \
  --goal "Your goal" \
  --endpoint "https://api.ollama.com" \
  --api-key "your-key"
```

Options: `--model`, `--coder-model`, `--reason-model`, `--metrics`, etc.

### MeTTa Command
```bash
dotnet run -- metta \
  --goal "Your goal" \
  --endpoint "https://api.ollama.com" \
  --api-key "your-key"
```

Options: `--model`, `--embed`, `--plan-only`, `--metrics`, etc.

## Advanced Usage

### Multi-Model Routing with Ollama Cloud

You can use different remote models for different tasks:

```bash
dotnet run -- ask -q "Write Python code" \
  --router "auto" \
  --general-model "llama3" \
  --coder-model "codellama" \
  --reason-model "deepseek-r1" \
  --endpoint "https://api.ollama.com" \
  --api-key "your-key"
```

### RAG with Ollama Cloud Embeddings

Ollama Cloud supports embeddings for RAG (Retrieval Augmented Generation):

```bash
dotnet run -- ask -q "What does the code do?" \
  --rag \
  --model "llama3" \
  --embed "nomic-embed-text" \
  --endpoint "https://api.ollama.com" \
  --api-key "your-key" \
  --endpoint-type "ollama-cloud"
```

### Agent Mode with Ollama Cloud

Run autonomous agents backed by Ollama Cloud:

```bash
dotnet run -- ask -q "Research functional programming" \
  --agent \
  --agent-mode "react" \
  --agent-max-steps 10 \
  --endpoint "https://api.ollama.com" \
  --api-key "your-key"
```

## Architecture

### Endpoint Detection Flow

```
User Input (--endpoint / CHAT_ENDPOINT)
    ↓
ChatConfig.ResolveWithOverrides()
    ↓
┌─────────────────────────────────┐
│  Endpoint Type Determination    │
├─────────────────────────────────┤
│ • Manual: --endpoint-type       │
│ • Auto: URL pattern matching    │
│   - api.ollama.com → OllamaCloud│
│   - ollama.cloud → OllamaCloud  │
│   - Others → OpenAI-compatible  │
└─────────────────────────────────┘
    ↓
Model Creation (OllamaCloudChatModel or HttpOpenAiCompatibleChatModel)
    ↓
API Request with Authentication
```

### Fallback Behavior

Ouroboros implements graceful fallback:

1. **Remote Endpoint Available** → Uses Ollama Cloud
2. **Remote Endpoint Unavailable** → Returns fallback response with context
3. **No Remote Config** → Uses local Ollama (if available)
4. **Local Ollama Unavailable** → Returns deterministic fallback

This ensures the pipeline always produces output, even in offline scenarios.

## API Format Examples

### Ollama Cloud Chat Request

```json
POST /api/generate
Authorization: Bearer your-api-key
Content-Type: application/json

{
  "model": "llama3",
  "prompt": "What is functional programming?",
  "stream": false,
  "options": {
    "temperature": 0.7,
    "num_predict": 512
  }
}
```

### Ollama Cloud Embeddings Request

```json
POST /api/embeddings
Authorization: Bearer your-api-key
Content-Type: application/json

{
  "model": "nomic-embed-text",
  "prompt": "Text to embed"
}
```

## Troubleshooting

### Connection Issues

If you see connection errors:

1. **Verify API Key**: Ensure your `CHAT_API_KEY` is correct
2. **Check Endpoint URL**: Verify `CHAT_ENDPOINT` is accessible
3. **Test Connectivity**: Use curl to test the endpoint:
   ```bash
   curl -H "Authorization: Bearer your-key" \
        https://api.ollama.com/api/generate \
        -d '{"model":"llama3","prompt":"test"}'
   ```

### Authentication Failures

If authentication fails:

1. Check that your API key is not expired
2. Verify the key has proper permissions
3. Ensure the endpoint type matches your provider:
   - Use `ollama-cloud` for Ollama Cloud
   - Use `openai` for OpenAI-compatible endpoints

### Fallback Responses

If you see `[ollama-cloud-fallback:model]` or `[remote-fallback:model]` in responses:

- This indicates the remote endpoint was unreachable
- The system returned a deterministic fallback to keep the pipeline running
- Check your internet connection and endpoint configuration

## Security Best Practices

1. **Never commit API keys** to source control
2. **Use environment variables** for credentials in production
3. **Rotate API keys** regularly
4. **Use `.env` files** for local development (excluded by `.gitignore`)
5. **Restrict API key permissions** to minimum required access

## Testing

To test your Ollama Cloud integration:

```bash
# Run integration tests
cd src/Ouroboros.Tests
dotnet test --filter "FullyQualifiedName~OllamaCloud"

# Test a simple query
cd ../Ouroboros.CLI
export CHAT_ENDPOINT="https://api.ollama.com"
export CHAT_API_KEY="your-key"
dotnet run -- ask -q "Hello, world!"
```

## Performance Considerations

### Latency
- Ollama Cloud: 1-5 seconds typical response time
- Local Ollama: 0.5-3 seconds (depends on hardware)
- Network latency affects remote endpoints

### Cost Optimization
- Use smaller models for simple tasks
- Leverage multi-model routing to use cheaper models when appropriate
- Cache embeddings when possible

### Concurrency
- Orchestrator supports parallel model execution
- Multiple CLI instances can share the same Ollama Cloud endpoint
- Rate limits depend on your Ollama Cloud plan

## Additional Resources

- [Ouroboros README](../README.md) - Main documentation
- [Architecture Guide](ARCHITECTURE.md) - System architecture
- [API Documentation](../src/Ouroboros.Providers/Providers/Adapters.cs) - Implementation details

## Support

For issues or questions:
1. Check existing [GitHub Issues](https://github.com/PMeeske/Ouroboros/issues)
2. Review integration tests in `src/Ouroboros.Tests/Tests/OllamaCloudIntegrationTests.cs`
3. Open a new issue with detailed error messages and configuration
