# End-to-End Integration Tests

## Overview

This directory contains comprehensive end-to-end testing suites for MonadicPipeline, including integration tests for Ollama Cloud endpoint support and CLI command end-to-end tests.

## Test Suites

### 1. Ollama Cloud Integration Tests
End-to-end integration testing for Ollama Cloud endpoint support. Validates both remote Ollama Cloud functionality and ensures that local Ollama integration remains intact (regression testing).

**See**: [OllamaCloudIntegrationTests.cs](./OllamaCloudIntegrationTests.cs)

### 2. CLI End-to-End Tests
Comprehensive end-to-end tests for all CLI commands and their variations. Tests command execution, option handling, and integration with cloud implementations.

**See**: [CLI_TESTS.md](./CLI_TESTS.md) for detailed documentation

### 3. TrackedVectorStore Tests
Tests for the in-memory vector store implementation.

**See**: [TrackedVectorStoreTests.cs](./TrackedVectorStoreTests.cs)

### 4. Memory Context Tests
Tests for conversation memory and context management.

**See**: [MemoryContextTests.cs](./MemoryContextTests.cs)

### 5. LangChain Conversation Tests
Tests for LangChain conversation integration.

**See**: [LangChainConversationTests.cs](./LangChainConversationTests.cs)

## Running the Tests

### Run Only Integration Tests (Ollama Cloud)
```bash
dotnet run -- test --integration
```

### Run Only CLI End-to-End Tests
```bash
dotnet run -- test --cli
```

### Run All Tests
```bash
dotnet run -- test --all
```

## Test Coverage

### Ollama Cloud Integration Tests

### Ollama Cloud Integration Tests

#### 1. Local Ollama Regression Tests
- **TestLocalOllamaChatModel**: Validates local Ollama chat adapter with fallback behavior
- **TestLocalOllamaEmbeddingModel**: Validates local Ollama embedding adapter

#### 2. Configuration Tests  
- **TestChatConfigAutoDetection**: Validates URL-based auto-detection
  - `api.ollama.com` → OllamaCloud
  - `ollama.cloud` → OllamaCloud  
  - `api.openai.com` → OpenAiCompatible
- **TestChatConfigManualOverride**: Validates explicit endpoint type specification
- **TestChatConfigEnvironmentVariables**: Validates environment variable resolution and CLI override precedence

#### 3. Chat Model Adapter Tests
- **TestOllamaCloudChatModelFallback**: Validates fallback behavior for unreachable Ollama Cloud endpoints
- **TestHttpOpenAiCompatibleChatModelFallback**: Validates fallback for OpenAI-compatible endpoints

#### 4. Embedding Model Adapter Tests
- **TestOllamaCloudEmbeddingModelFallback**: Validates deterministic embedding fallback
  - Tests consistent dimensions
  - Tests deterministic values (same input = same output)

#### 5. Model Selection Logic Tests
- **TestCreateRemoteChatModelSelection**: Validates correct model type instantiation
- **TestCreateEmbeddingModelSelection**: Validates embedding model selection

#### 6. End-to-End Scenario Tests
- **TestEndToEndLocalOllamaScenario**: Complete workflow with local Ollama
  - Chat generation
  - Embedding creation
  - Vector store integration
- **TestEndToEndRemoteOllamaCloudScenario**: Complete workflow with Ollama Cloud
  - Remote chat with fallback
  - Remote embeddings with deterministic fallback
  - Vector store integration

## Running Ollama Cloud Tests

### Using CLI Command
```bash
dotnet run -- test --integration
```

### Run All Tests (including CLI tests)
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
