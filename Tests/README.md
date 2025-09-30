# End-to-End Integration Tests for Ollama Cloud

## Overview

This test suite provides comprehensive end-to-end integration testing for the Ollama Cloud endpoint support feature. The tests validate both remote Ollama Cloud functionality and ensure that local Ollama integration remains intact (regression testing).

## Test Coverage

### 1. Local Ollama Regression Tests
- **TestLocalOllamaChatModel**: Validates local Ollama chat adapter with fallback behavior
- **TestLocalOllamaEmbeddingModel**: Validates local Ollama embedding adapter

### 2. Configuration Tests  
- **TestChatConfigAutoDetection**: Validates URL-based auto-detection
  - `api.ollama.com` → OllamaCloud
  - `ollama.cloud` → OllamaCloud  
  - `api.openai.com` → OpenAiCompatible
- **TestChatConfigManualOverride**: Validates explicit endpoint type specification
- **TestChatConfigEnvironmentVariables**: Validates environment variable resolution and CLI override precedence

### 3. Chat Model Adapter Tests
- **TestOllamaCloudChatModelFallback**: Validates fallback behavior for unreachable Ollama Cloud endpoints
- **TestHttpOpenAiCompatibleChatModelFallback**: Validates fallback for OpenAI-compatible endpoints

### 4. Embedding Model Adapter Tests
- **TestOllamaCloudEmbeddingModelFallback**: Validates deterministic embedding fallback
  - Tests consistent dimensions
  - Tests deterministic values (same input = same output)

### 5. Model Selection Logic Tests
- **TestCreateRemoteChatModelSelection**: Validates correct model type instantiation
- **TestCreateEmbeddingModelSelection**: Validates embedding model selection

### 6. End-to-End Scenario Tests
- **TestEndToEndLocalOllamaScenario**: Complete workflow with local Ollama
  - Chat generation
  - Embedding creation
  - Vector store integration
- **TestEndToEndRemoteOllamaCloudScenario**: Complete workflow with Ollama Cloud
  - Remote chat with fallback
  - Remote embeddings with deterministic fallback
  - Vector store integration

## Running the Tests

### Using CLI Command (when build is working)
```bash
dotnet run -- test --integration
```

### Run All Tests
```bash
dotnet run -- test --all
```

### Manual Execution
If you encounter build issues, you can manually compile and run:

```csharp
using LangChainPipeline.Tests;

await OllamaCloudIntegrationTests.RunAllTests();
```

## Expected Output

```
=== Running Ollama Cloud Integration Tests ===
Testing local Ollama chat model...
  ✓ Local Ollama chat model fallback works correctly
Testing local Ollama embedding model...
  ✓ Local Ollama embedding model works (returned 32 dimensions)
Testing ChatConfig auto-detection...
  ✓ Auto-detection works correctly for all URL patterns
Testing ChatConfig manual override...
  ✓ Manual override works correctly
Testing ChatConfig environment variable handling...
  ✓ Environment variable handling works correctly
Testing OllamaCloudChatModel fallback behavior...
  ✓ OllamaCloudChatModel fallback works correctly
Testing HttpOpenAiCompatibleChatModel fallback behavior...
  ✓ HttpOpenAiCompatibleChatModel fallback works correctly
Testing OllamaCloudEmbeddingModel fallback behavior...
  ✓ OllamaCloudEmbeddingModel fallback works (32 dimensions)
Testing CreateRemoteChatModel selection logic...
  ✓ Remote chat model selection works correctly
Testing CreateEmbeddingModel selection logic...
  ✓ Embedding model selection works correctly
Testing end-to-end local Ollama scenario...
  ✓ End-to-end local Ollama scenario works correctly
Testing end-to-end remote Ollama Cloud scenario...
  ✓ End-to-end remote Ollama Cloud scenario works correctly
✓ All Ollama Cloud integration tests passed!
```

## Test Assertions

All tests use explicit exception throwing for failures:
- Configuration tests validate correct endpoint type detection
- Adapter tests validate fallback messages contain expected strings
- End-to-end tests validate complete workflows including vector storage

## Notes

### Fallback Behavior
Tests are designed to work in environments without running Ollama daemons or real remote endpoints:
- Chat models fallback to descriptive error messages including the original prompt
- Embedding models fallback to deterministic hash-based vectors

### Environment Independence  
Tests save and restore environment variables to ensure no side effects between test runs.

### Backward Compatibility
Local Ollama tests specifically validate that existing functionality is not broken by the new remote endpoint features.
