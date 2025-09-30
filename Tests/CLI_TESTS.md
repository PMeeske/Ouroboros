# CLI End-to-End Tests

## Overview

Comprehensive end-to-end tests for all CLI commands and their variations. These tests validate command execution, option handling, and integration with cloud implementations (Ollama Cloud, OpenAI-compatible endpoints).

## Running the Tests

### Using CLI Command
```bash
# Run only CLI end-to-end tests
dotnet run -- test --cli

# Run all tests including CLI tests
dotnet run -- test --all

# Run only integration tests (excludes CLI tests)
dotnet run -- test --integration
```

### Test Coverage

#### 1. Ask Command Tests
- **TestAskCommandBasic**: Validates basic ask without RAG
  - Tests adapter pattern and fallback behavior
  - Handles Ollama unavailability gracefully

- **TestAskCommandWithRag**: Validates RAG setup and document retrieval
  - Tests vector store seeding
  - Tests embedding model integration
  - Validates document count

- **TestAskCommandWithAgent**: Validates agent mode functionality
  - Tests tool registry setup
  - Tests agent factory creation
  - Validates agent mode configuration

- **TestAskCommandWithAgentModes**: Validates all agent implementations
  - Tests `simple` agent mode
  - Tests `lc` (LangChain) agent mode
  - Tests `react` agent mode

- **TestAskCommandWithRouter**: Validates multi-model router
  - Tests router creation with model map
  - Tests fallback to general model
  - Validates routing logic

- **TestAskCommandWithDebug**: Validates debug mode
  - Tests MONADIC_DEBUG environment variable
  - Validates debug mode activation

- **TestAskCommandWithRemoteEndpoints**: Validates remote endpoint handling
  - Tests Ollama Cloud endpoint with fallback
  - Tests OpenAI-compatible endpoint with fallback
  - Tests endpoint type auto-detection
  - Tests manual endpoint type override

- **TestAskCommandWithJsonTools**: Validates JSON tool format
  - Tests tool registration with JSON output
  - Validates tool retrieval

- **TestAskCommandOptionCombinations**: Validates option precedence
  - Tests ChatRuntimeSettings (temperature, max-tokens)
  - Tests CLI override precedence over environment variables
  - Validates endpoint type resolution

#### 2. Pipeline Command Tests
- **TestPipelineCommandBasic**: Validates basic DSL parsing
  - Tests tokenization of simple DSL
  - Validates token count

- **TestPipelineCommandWithIngestion**: Validates ingestion steps
  - Tests UseDir and UseIngest DSL tokens
  - Tests directory ingestion step
  - Validates branch preservation

- **TestPipelineCommandWithReasoning**: Validates reasoning pipeline
  - Tests SetTopic, UseDraft, UseCritique, UseImprove tokens
  - Validates DSL parsing for reasoning steps

- **TestCompleteRefinementLoop**: Validates complete refinement loop workflow
  - Tests automatic draft creation when none exists
  - Tests Draft -> Critique -> Improve cycle execution
  - Validates that existing drafts are reused
  - Tests multiple refinement iterations
  - Handles Ollama unavailability gracefully

- **TestPipelineCommandWithTrace**: Validates trace control
  - Tests TraceOn step
  - Tests TraceOff step
  - Validates trace state changes

- **TestPipelineCommandWithDebug**: Validates debug mode
  - Tests MONADIC_DEBUG environment variable
  - Validates debug mode in pipeline context

#### 3. List Command Tests
- **TestListCommand**: Validates token enumeration
  - Tests StepRegistry.GetTokenGroups()
  - Validates essential tokens are registered
  - Tests token groups structure

#### 4. Explain Command Tests
- **TestExplainCommand**: Validates basic DSL explanation
  - Tests PipelineDsl.Explain()
  - Validates output format
  - Ensures headers are present

- **TestExplainCommandComplexDsl**: Validates complex DSL explanation
  - Tests multi-step DSL explanation
  - Validates all tokens are explained
  - Tests reasoning pipeline explanation

#### 5. Test Command Tests
- **TestTestCommandStructure**: Validates test infrastructure
  - Validates OllamaCloudIntegrationTests exists
  - Validates TrackedVectorStoreTests exists
  - Validates MemoryContextTests exists
  - Validates LangChainConversationTests exists

#### 6. Validation and Error Handling Tests
- **TestCommandValidation**: Validates error handling
  - Tests invalid token parsing
  - Tests no-op handling for unknown tokens
  - Validates build with invalid tokens

- **TestEnvironmentVariableHandling**: Validates environment variable resolution
  - Tests CHAT_ENDPOINT resolution
  - Tests CHAT_API_KEY resolution
  - Tests CHAT_ENDPOINT_TYPE resolution
  - Tests CLI override precedence

## Test Assertions

All tests use exception-based assertions that throw descriptive errors on failure:

```csharp
if (condition)
{
    throw new Exception("Descriptive error message");
}
```

## Integration with Cloud Implementations

The tests respect current cloud implementations:

### Ollama Cloud
- Tests auto-detection via URL patterns (`api.ollama.com`, `ollama.cloud`)
- Tests fallback behavior with `[ollama-cloud-fallback:model]` response
- Tests manual override with `--endpoint-type ollama-cloud`

### OpenAI-Compatible
- Tests auto-detection for `api.openai.com`
- Tests fallback behavior with `[remote-fallback:model]` response
- Tests manual override with `--endpoint-type openai`

### Environment Variables
- `CHAT_ENDPOINT`: Remote endpoint URL
- `CHAT_API_KEY`: API key for authentication
- `CHAT_ENDPOINT_TYPE`: Endpoint type (auto, openai, ollama-cloud)
- `MONADIC_DEBUG`: Enable debug mode
- `MONADIC_ROUTER`: Enable multi-model routing

## Expected Output

When running `dotnet run -- test --cli`, you should see:

```
=== Running CLI End-to-End Tests ===
Testing ask command (basic)...
  ✓ Basic ask command works correctly
Testing ask command with RAG...
  ✓ Ask command with RAG setup works correctly
Testing ask command with agent mode...
  ✓ Ask command with agent mode works correctly
Testing ask command with different agent modes...
  ✓ All agent modes (simple, lc, react) work correctly
Testing ask command with multi-model router...
  ✓ Multi-model router works correctly
Testing ask command with debug mode...
  ✓ Debug mode environment variable works correctly
Testing ask command with remote endpoints...
  ✓ Remote endpoint handling works correctly
Testing ask command with JSON tools...
  ✓ JSON tools registration works correctly
Testing ask command option combinations...
  ✓ Ask command option combinations work correctly
Testing pipeline command (basic)...
  ✓ Basic pipeline DSL parsing works correctly
Testing pipeline command with ingestion...
  ✓ Pipeline ingestion steps work correctly
Testing pipeline command with reasoning steps...
  ✓ Pipeline reasoning steps parsing works correctly
Testing pipeline command with trace...
  ✓ Pipeline trace control works correctly
Testing pipeline command with debug...
  ✓ Pipeline debug mode works correctly
Testing list command...
  ✓ List command token enumeration works correctly
Testing explain command...
  ✓ Explain command works correctly
Testing explain command with complex DSL...
  ✓ Explain command with complex DSL works correctly
Testing test command structure...
  ✓ Test command structure is complete
Testing command validation...
  ✓ Command validation handles invalid tokens correctly
Testing environment variable handling...
  ✓ Environment variable handling works correctly
✓ All CLI end-to-end tests passed!
```

## Notes

- Tests are designed to work without requiring Ollama to be running
- Fallback mechanisms are tested to ensure graceful degradation
- Environment variables are properly cleaned up after tests
- All tests follow functional programming patterns used in the codebase
- Tests validate both success paths and error handling
