# CLI End-to-End Tests

This directory contains comprehensive end-to-end tests for all CLI commands and variations.

## Test Coverage

### Running Tests

```bash
# Run all test suites
dotnet run -- test -s all

# Run only CLI tests
dotnet run -- test -s cli

# Run specific test suites
dotnet run -- test -s vector
dotnet run -- test -s memory
dotnet run -- test -s conversation
```

### CLI Test Suite (CliEndToEndTests.cs)

The CLI test suite includes **21 comprehensive test methods** covering all command variations:

#### Ask Command Tests
1. **Basic Ask Command** - Tests simple question parsing with defaults
2. **Ask with RAG** - Tests RAG mode with custom K value and model
3. **Ask with Agent Mode** - Tests all agent modes (simple, lc, react) with configurable max steps
4. **Ask with Router** - Tests multi-model routing with specialized models (coder, summarize, reason)
5. **Ask with Temperature/Tokens** - Tests temperature, max-tokens, and timeout configuration
6. **Ask with Debug/Stream** - Tests debug logging and streaming options
7. **Ask with Strict Model** - Tests strict model validation mode
8. **Ask with JSON Tools** - Tests JSON tool call format option

#### Pipeline Command Tests
9. **Basic Pipeline** - Tests simple DSL parsing
10. **Pipeline with Trace** - Tests trace output with custom model, source, and K value
11. **Pipeline with Debug** - Tests debug logging option
12. **Pipeline with Custom Source** - Tests custom source path and embed model
13. **DSL Variations** - Tests multiple DSL patterns:
    - Single-step DSL
    - Parameterized DSL with arguments
    - DSL with trace control tokens
    - DSL with retrieval steps

#### Other Command Tests
14. **List Command** - Tests token listing
15. **Explain Command** - Tests DSL explanation
16. **Test Command** - Tests the test runner itself with different suite options

#### Error Handling Tests
17. **Invalid Command** - Tests handling of unrecognized commands
18. **Missing Required Parameters** - Tests validation for:
    - Ask without question
    - Pipeline without DSL
    - Explain without DSL

#### Execution Tests (Non-LLM)
19. **List Command Execution** - Verifies actual token list output
20. **Explain Command Execution** - Verifies basic DSL explanation output
21. **Complex DSL Explanation** - Verifies multi-step DSL explanation

### Test Philosophy

- **No LLM Dependencies**: Tests focus on CLI parsing and structure without requiring actual LLM/embedding models
- **Comprehensive Coverage**: All CLI options and combinations are tested
- **Error Validation**: Both success and error scenarios are covered
- **Real Output Verification**: Where possible, actual command execution is tested (list, explain)

### Adding New Tests

When adding new CLI options or commands:

1. Add parsing tests for the new options
2. Test default values and overrides
3. Add error handling tests for invalid inputs
4. Update this README with the new test coverage

### Integration with Other Test Suites

The test runner (`dotnet run -- test`) integrates with:
- **TrackedVectorStoreTests** - Vector database operations
- **MemoryContextTests** - Conversation memory management
- **LangChainConversationTests** - LangChain conversation integration

All test suites follow the same pattern:
- Static test methods
- Console output for visibility
- Exception throwing for failures
- `RunAllTests()` entry point
