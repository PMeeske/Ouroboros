# Ouroboros Examples

This directory contains practical examples demonstrating various features of Ouroboros.

## Ollama Cloud Integration

### [ollama-cloud-usage.sh](./ollama-cloud-usage.sh)

Comprehensive examples showing how to use Ollama Cloud authentication with all CLI commands:
- Basic question answering
- RAG (Retrieval Augmented Generation)
- Agent mode with tool execution
- Pipeline DSL execution
- Smart model orchestration
- MeTTa symbolic reasoning
- Multi-model routing
- Debug mode

**Setup:**
```bash
# Make the script executable
chmod +x examples/ollama-cloud-usage.sh

# Edit the script and set your API key
nano examples/ollama-cloud-usage.sh
# Change: export CHAT_API_KEY="your-ollama-cloud-api-key-here"

# Run all examples
cd src/Ouroboros.CLI
../../examples/ollama-cloud-usage.sh
```

**Individual Examples:**

You can also run individual commands directly:

```bash
# Navigate to CLI directory
cd src/Ouroboros.CLI

# Set up authentication
export CHAT_ENDPOINT="https://api.ollama.com"
export CHAT_API_KEY="your-api-key"
export CHAT_ENDPOINT_TYPE="ollama-cloud"

# Run a single example
dotnet run -- ask -q "What is functional programming?"
```

## Additional Resources

- [Main README](../README.md) - Project overview and quick start
- [Ollama Cloud Integration Guide](../docs/OLLAMA_CLOUD_INTEGRATION.md) - Comprehensive authentication guide
- [Architecture Documentation](../docs/ARCHITECTURE.md) - System design and patterns

## Contributing Examples

To add new examples:

1. Create a new script or directory for your example
2. Document the purpose and usage in this README
3. Include comments explaining key concepts
4. Test the example thoroughly
5. Submit a pull request

## Example Categories

### Completed
- ✅ Ollama Cloud authentication and usage

### Planned
- ⏳ Advanced pipeline patterns
- ⏳ Custom tool creation
- ⏳ Multi-step reasoning workflows
- ⏳ Vector database integration
- ⏳ Production deployment scenarios
