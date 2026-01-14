# Test Classification Guide

## Overview

This guide explains how tests are classified in the Ouroboros project and provides guidance for developers on how to properly categorize new tests.

## Test Categories

The Ouroboros test suite uses xUnit traits to classify tests into two primary categories:

### Unit Tests

**Trait:** `[Trait("Category", "Unit")]`

**Characteristics:**
- Fast execution (typically milliseconds to seconds)
- No external dependencies (databases, APIs, services)
- Uses mocks, fakes, or in-memory implementations
- Tests a single component or small group of related components in isolation
- Can run in any environment without configuration
- Deterministic and repeatable

**Examples:**
- `MeTTaOrchestratorTests.cs` - Uses `MockMeTTaEngine` instead of real MeTTa runtime
- `MeTTaTests.cs` - Tests MeTTa tools with mock engine
- `OrchestratorTests.cs` - Tests orchestration logic with mock models
- `MetaAIv2Tests.cs` - Tests Meta-AI orchestrator with mock LLMs

**When to use Unit classification:**
```csharp
/// <summary>
/// Unit tests for the XYZ component.
/// Uses mock dependencies for isolation - no external services required.
/// </summary>
[Trait("Category", "Unit")]
public class XyzComponentTests
{
    [Fact]
    public void Component_Operation_ShouldSucceed()
    {
        // Arrange with mocks
        var mockDependency = new MockDependency();
        var component = new XyzComponent(mockDependency);
        
        // Act
        var result = component.DoSomething();
        
        // Assert
        result.Should().BeTrue();
    }
}
```

### Integration Tests

**Trait:** `[Trait("Category", "Integration")]`

**Characteristics:**
- Slower execution (seconds to minutes)
- Requires external services or resources
- Tests interaction between multiple components
- May require environment configuration (API keys, service endpoints)
- May have non-deterministic behavior (network issues, rate limits)
- Tests real implementations, not mocks

**Examples:**
- `GitHubModelsIntegrationTests.cs` - Requires GitHub API token, tests real API calls
- `AdvancedMeTTaIntegrationTests.cs` - Requires MeTTa engine runtime
- `DagMeTTaIntegrationTests.cs` - Tests MeTTa integration with DAG structures
- `OllamaCloudIntegrationTests.cs` - Tests Ollama service integration
- `UnifiedOrchestrationIntegrationTests.cs` - Tests end-to-end orchestration workflows

**When to use Integration classification:**
```csharp
/// <summary>
/// Integration tests for GitHub API client.
/// Requires GITHUB_TOKEN environment variable to be set for live API tests.
/// Falls back to mock responses when token is not available.
/// </summary>
[Trait("Category", "Integration")]
public class GitHubApiClientTests
{
    [Fact]
    public async Task ApiClient_GetRepository_ShouldReturnData()
    {
        // Skip if no token available
        var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        if (string.IsNullOrEmpty(token))
        {
            // Use fallback or skip
            return;
        }
        
        // Test with real API
        var client = new GitHubApiClient(token);
        var repo = await client.GetRepositoryAsync("owner", "repo");
        
        repo.Should().NotBeNull();
    }
}
```

## Running Tests by Category

### Run All Tests
```bash
dotnet test
```

### Run Only Unit Tests
```bash
dotnet test --filter "Category=Unit"
```

### Run Only Integration Tests
```bash
dotnet test --filter "Category=Integration"
```

### Run Tests from Specific Test File
```bash
dotnet test --filter "FullyQualifiedName~MeTTaOrchestratorTests"
```

### Run Specific Test Method
```bash
dotnet test --filter "FullyQualifiedName~MeTTaOrchestratorTests.MeTTaRepresentation_TranslatePlan_ShouldSucceed"
```

### Combine Filters
```bash
# Run all unit tests except those in a specific class
dotnet test --filter "Category=Unit&FullyQualifiedName!~SomeTestClass"
```

## Environment Requirements for Integration Tests

### GitHub Models Integration Tests
- **Required:** `MODEL_TOKEN`, `GITHUB_TOKEN`, or `GITHUB_MODELS_TOKEN` environment variable
- **Purpose:** Authenticate with GitHub Models API
- **Setup:**
  ```bash
  export GITHUB_TOKEN="your_token_here"
  dotnet test --filter "FullyQualifiedName~GitHubModelsIntegrationTests"
  ```

### Ollama Integration Tests
- **Required:** Ollama service running locally or remotely
- **Default Endpoint:** `http://localhost:11434`
- **Optional:** `OLLAMA_ENDPOINT` environment variable for custom endpoints
- **Setup:**
  ```bash
  # Start Ollama service (if local)
  ollama serve
  
  # Run tests
  dotnet test --filter "FullyQualifiedName~OllamaCloudIntegrationTests"
  ```

### MeTTa Integration Tests
- **Required:** MeTTa runtime installed and accessible
- **Alternative:** Tests fall back to mock engine when runtime is unavailable
- **Setup:**
  ```bash
  # Install MeTTa (if not already installed)
  # See MeTTa documentation for installation instructions
  
  # Run tests
  dotnet test --filter "FullyQualifiedName~MeTTaIntegrationTests"
  ```

## Guidelines for Writing New Tests

### 1. Choose the Correct Category

**Ask yourself:**
- Does this test require external services? → **Integration**
- Does this test use only mocks and fakes? → **Unit**
- Does this test need environment configuration? → **Integration**
- Can this test run in any environment without setup? → **Unit**

### 2. Add Appropriate Trait

Always add the trait at the **class level**:

```csharp
[Trait("Category", "Unit")]  // or "Integration"
public class YourTestClass
{
    // Tests here
}
```

### 3. Add XML Documentation

Provide clear documentation explaining:
- What is being tested
- Whether it's a unit or integration test
- Any external dependencies or requirements

```csharp
/// <summary>
/// Unit tests for the calculation engine.
/// Uses in-memory data - no external database required.
/// </summary>
[Trait("Category", "Unit")]
public class CalculationEngineTests
{
    // ...
}
```

### 4. Follow Naming Conventions

**Test Class Naming:**
- Unit tests: `ComponentNameTests.cs`
- Integration tests: `ComponentNameIntegrationTests.cs`

**Test Method Naming:**
Use the pattern: `MethodUnderTest_Scenario_ExpectedBehavior`

Examples:
- `Calculate_ValidInput_ReturnsCorrectResult`
- `Process_InvalidData_ThrowsException`
- `ApiClient_NetworkError_RetriesAutomatically`

### 5. Handle Missing Dependencies Gracefully

For integration tests that may not have required services available:

```csharp
[Fact]
public async Task Component_WithExternalService_ShouldWork()
{
    try
    {
        // Attempt to use external service
        var result = await ExternalService.CallAsync();
        result.Should().NotBeNull();
    }
    catch (Exception ex) when (ex.Message.Contains("Connection refused"))
    {
        // Expected when service is not available
        Console.WriteLine("⚠ Service not available - test skipped");
    }
}
```

## Test Organization Best Practices

### File Structure
```
src/Ouroboros.Tests/Tests/
├── Unit Tests (majority of tests)
│   ├── MeTTaOrchestratorTests.cs
│   ├── ToolRegistryTests.cs
│   └── ...
└── Integration Tests
    ├── GitHubModelsIntegrationTests.cs
    ├── OllamaCloudIntegrationTests.cs
    └── ...
```

### Class Structure
```csharp
/// <summary>
/// [Description of what's being tested]
/// [Dependency information]
/// </summary>
[Trait("Category", "Unit|Integration")]
public class ComponentTests
{
    // Test methods using [Fact] or [Theory]
    
    [Fact]
    public void TestMethod_Scenario_Expected()
    {
        // Arrange
        // Act
        // Assert
    }
    
    // Optional: Keep legacy RunAllTests() for backward compatibility
    public static async Task RunAllTests()
    {
        var instance = new ComponentTests();
        instance.TestMethod_Scenario_Expected();
        // ...
    }
}
```

## Continuous Integration (CI) Considerations

### Fast CI Pipeline
Run only unit tests for quick feedback:
```bash
dotnet test --filter "Category=Unit"
```

### Full CI Pipeline
Run all tests including integration tests:
```bash
dotnet test
```

### Conditional Integration Tests
Run integration tests only when required secrets/tokens are available:
```yaml
# Example GitHub Actions workflow
- name: Run Unit Tests
  run: dotnet test --filter "Category=Unit"

- name: Run Integration Tests
  if: env.GITHUB_TOKEN != ''
  run: dotnet test --filter "Category=Integration"
  env:
    GITHUB_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

## Migration Notes

### Converting Legacy Tests

Old pattern (static methods):
```csharp
public static class LegacyTests
{
    public static async Task RunAllTests()
    {
        await TestSomething();
        TestAnotherThing();
    }
    
    public static async Task TestSomething() { }
    public static void TestAnotherThing() { }
}
```

New pattern (instance methods with xUnit):
```csharp
[Trait("Category", "Unit")]
public class ModernTests
{
    [Fact]
    public async Task Something_Scenario_Expected() { }
    
    [Fact]
    public void AnotherThing_Scenario_Expected() { }
    
    // Optional: Keep for backward compatibility
    public static async Task RunAllTests()
    {
        var instance = new ModernTests();
        await instance.Something_Scenario_Expected();
        instance.AnotherThing_Scenario_Expected();
    }
}
```

## Troubleshooting

### Tests Not Running
- Verify trait syntax: `[Trait("Category", "Unit")]` (not "category" or "UNIT")
- Check filter syntax: `dotnet test --filter "Category=Unit"` (case-sensitive)

### Integration Tests Failing
- Check environment variables are set
- Verify external services are running
- Check network connectivity
- Review service-specific requirements in test documentation

### Inconsistent Test Results
- Unit tests should always be deterministic - if not, they may need refactoring
- Integration tests may have non-deterministic behavior - add retry logic or timeouts

## Additional Resources

- [xUnit Documentation](https://xunit.net/)
- [.NET Testing Best Practices](https://docs.microsoft.com/en-us/dotnet/core/testing/)
- [Ouroboros Contributing Guide](../CONTRIBUTING.md)
- [Ouroboros Test Coverage Report](../TEST_COVERAGE_REPORT.md)

## Summary

- Use `[Trait("Category", "Unit")]` for fast, isolated tests with no external dependencies
- Use `[Trait("Category", "Integration")]` for tests requiring external services
- Always add XML documentation explaining test purpose and requirements
- Follow naming conventions: `MethodUnderTest_Scenario_ExpectedBehavior`
- Run unit tests frequently during development
- Run integration tests before merging to ensure nothing breaks
- Handle missing external dependencies gracefully in integration tests
