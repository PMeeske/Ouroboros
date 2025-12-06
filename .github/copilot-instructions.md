# GitHub Copilot Instructions for Ouroboros

## Project Overview

Ouroboros is a sophisticated **functional programming-based AI pipeline system** built on LangChain. The project implements category theory principles, monadic composition, and functional programming patterns to create type-safe, composable AI workflows.

**Key Technologies**: C# 14.0+, LangChain, Category Theory, Functional Programming, Vector Databases

## Architecture Patterns

### Core Functional Principles
- **Monadic Composition**: Use `Result<T>` and `Option<T>` monads for error handling and null safety
- **Kleisli Arrows**: Implement `Step<TInput, TOutput>` for composable pipeline operations
- **Immutable Data Structures**: Prefer immutable state and pure functions
- **Category Theory**: Follow mathematical laws of composition, identity, and associativity

### Pipeline Architecture
```csharp
// Example monadic pipeline composition
var pipeline = Step.Pure<string>()
    .Bind(ProcessInput)
    .Map(TransformData)
    .Bind(ExecuteReasoning)
    .Map(FormatOutput);
```

## Coding Standards

### Namespace Organization
```csharp
// Follow the existing namespace hierarchy
namespace LangChainPipeline.Pipeline.Reasoning;  // ✅ Good
namespace MyCustom.Pipeline;                     // ❌ Avoid
```

### Monadic Error Handling
```csharp
// Use Result<T> for operations that can fail
public static async Task<Result<Draft>> GenerateDraft(string prompt, ToolRegistry tools)
{
    try 
    {
        var result = await llm.GenerateAsync(prompt);
        return Result<Draft>.Ok(new Draft(result));
    }
    catch (Exception ex)
    {
        return Result<Draft>.Error($"Draft generation failed: {ex.Message}");
    }
}

// Use Option<T> for nullable values
public static Option<ReasoningStep> GetLastStep(PipelineBranch branch) =>
    branch.Events.OfType<ReasoningStep>().LastOrDefault()?.ToOption() ?? Option<ReasoningStep>.None();
```

### Step Composition
```csharp
// Create composable steps using the Step<TInput, TOutput> pattern
public static Step<PipelineBranch, PipelineBranch> DraftArrow(
    ToolAwareChatModel llm, ToolRegistry tools, string topic) =>
    async branch =>
    {
        // Implementation logic
        return branch.WithNewReasoning(draft);
    };
```

### Tool Integration
```csharp
// Tools should implement ITool interface
public class CustomTool : ITool
{
    public string Name => "custom_tool";
    public string Description => "Performs custom analysis";
    
    public async Task<ToolExecution> ExecuteAsync(ToolArgs args)
    {
        // Tool implementation
        return new ToolExecution(Name, args, result);
    }
}
```

## Domain-Specific Guidelines

### Reasoning Components
- **Prompts**: Use `PromptTemplate.Format()` for dynamic prompt generation
- **States**: Implement `ReasoningState` derivatives (`Draft`, `Critique`, `FinalSpec`)
- **Events**: Follow event sourcing patterns with immutable event records

### Vector Operations
- Use `TrackedVectorStore` for development and testing
- Implement `IVectorStore` abstractions for production vector databases
- Handle embeddings through `OllamaEmbeddingModel` or compatible providers

### Branch Management
- `PipelineBranch` represents execution context with immutable event history
- Use `Fork()` for creating parallel execution paths
- Implement replay functionality through `ReplayEngine`

## Code Quality Standards

### Documentation
```csharp
/// <summary>
/// Creates a draft arrow that generates an initial response using the provided LLM and tools.
/// </summary>
/// <param name="llm">The tool-aware language model for generation</param>
/// <param name="tools">Registry of available tools</param>
/// <param name="topic">The topic for draft generation</param>
/// <returns>A step that transforms a pipeline branch by adding a draft reasoning state</returns>
public static Step<PipelineBranch, PipelineBranch> DraftArrow(
    ToolAwareChatModel llm, ToolRegistry tools, string topic)
```

### Testing Patterns
```csharp
// Use existing test patterns from Tests/ directory
[Fact]
public async Task Should_Generate_Draft_With_Valid_Input()
{
    // Arrange
    var branch = new PipelineBranch("test", store, dataSource);
    var llm = CreateMockLLM();
    var tools = CreateTestTools();
    
    // Act
    var result = await DraftArrow(llm, tools, "test topic")(branch);
    
    // Assert
    result.Events.OfType<ReasoningStep>()
        .Should().ContainSingle()
        .Which.State.Should().BeOfType<Draft>();
}
```

### Error Handling Best Practices
- Always use monadic error handling (`Result<T>`, `Option<T>`)
- Avoid throwing exceptions in pipeline operations
- Log errors through structured logging when available
- Provide meaningful error messages with context

## Integration Guidelines

### LangChain Integration
- Use `ToolAwareChatModel` wrapper for LLM operations
- Implement custom providers in `Providers/` namespace
- Follow existing patterns for document loading and processing

### Tool System
- Register tools through `ToolRegistry.RegisterTool<T>()`
- Export tool schemas using `ToolRegistry.ExportSchemas()`
- Handle tool execution errors gracefully with Result patterns

### Memory and Context
- Use conversation memory for stateful interactions
- Implement context retrieval through vector similarity search
- Handle context window limitations appropriately

## Common Patterns to Follow

### Pipeline Composition
```csharp
// Chain operations using monadic bind
var enhancedPipeline = Step.Pure<string>()
    .Bind(ValidateInput)
    .Bind(LoadContext)
    .Bind(GenerateDraft)
    .Bind(CritiqueDraft)
    .Bind(ImproveResponse);
```

### State Management
```csharp
// Immutable state updates
public PipelineBranch AddReasoning(ReasoningState state, string prompt, List<ToolExecution>? tools = null)
{
    var newEvent = new ReasoningStep(Guid.NewGuid(), state.Kind, state, DateTime.UtcNow, prompt, tools);
    return this with { Events = Events.Append(newEvent).ToList() };
}
```

### Resource Management
```csharp
// Proper async/await patterns
public static async Task<Result<T>> ExecuteWithTimeout<T>(
    Func<Task<T>> operation, 
    TimeSpan timeout)
{
    using var cts = new CancellationTokenSource(timeout);
    try
    {
        var result = await operation().ConfigureAwait(false);
        return Result<T>.Ok(result);
    }
    catch (OperationCanceledException)
    {
        return Result<T>.Error("Operation timed out");
    }
    catch (Exception ex)
    {
        return Result<T>.Error($"Operation failed: {ex.Message}");
    }
}
```

## Development Workflow

1. **Feature Development**: Create new features as composable steps
2. **Testing**: Write tests that validate monadic laws and composition
3. **Documentation**: Include XML documentation for all public APIs  
4. **Error Handling**: Use Result/Option monads consistently
5. **Integration**: Follow existing patterns for LangChain integration

## Important Notes

- This codebase prioritizes **functional programming principles** over object-oriented patterns
- **Type safety** is paramount - leverage the C# type system fully
- **Composition over inheritance** - build functionality through step composition
- **Immutability** - prefer immutable data structures and pure functions
- **Category theory** knowledge helps but isn't required - follow existing patterns

When in doubt, examine existing code in the `Examples/` directory for reference implementations.