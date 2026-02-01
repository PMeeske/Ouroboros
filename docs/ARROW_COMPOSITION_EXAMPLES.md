# Arrow-Based Composition Examples

This document demonstrates how to use the arrow-based composition patterns that replace inheritance-based template methods.

## Important Notes

### Cancellation Token Support
All arrow factory functions now support `CancellationToken` for proper cancellation of long-running LLM operations. When using arrow composition directly, pass the cancellation token to the factory functions:

```csharp
// Arrow factories accept CancellationToken
var arrow = AgentPersonaArrows.CreateProposalArrow(
    agentName, 
    systemPrompt, 
    promptBuilder, 
    llm, 
    cancellationToken);
```

When using `BaseAgentPersona` or `IAgentPersona` interface methods, cancellation tokens are automatically propagated to the underlying arrow operations.

## Agent Persona Composition

### Before (Inheritance-Based)
```csharp
public sealed class CustomAgent : BaseAgentPersona
{
    public override string Name => "CustomAgent";
    public override string Description => "Custom agent description";
    public override string SystemPrompt => "Custom system prompt";
    
    // Override template methods if needed
    protected override string BuildProposalPrompt(CouncilTopic topic)
    {
        // Custom implementation
        return base.BuildProposalPrompt(topic) + "\nAdditional context";
    }
}
```

### After (Arrow-Based Composition)
```csharp
// Option 1: Use BaseAgentPersona with custom functions
public sealed class CustomAgent : BaseAgentPersona
{
    public override string Name => "CustomAgent";
    public override string Description => "Custom agent description";
    public override string SystemPrompt => "Custom system prompt";
    
    // Override functional properties instead of methods
    protected override Func<CouncilTopic, string, string> ProposalPromptBuilder =>
        (topic, systemPrompt) => 
            AgentPersonaArrows.BuildDefaultProposalPrompt(topic, systemPrompt) + 
            "\nAdditional context";
}

// Option 2: Direct arrow usage without inheritance
public static class CustomAgentArrows
{
    public static Step<CouncilTopic, Result<AgentContribution, string>> CreateProposal(
        ToolAwareChatModel llm,
        CancellationToken ct = default)
    {
        return AgentPersonaArrows.CreateProposalArrow(
            "CustomAgent",
            "Custom system prompt",
            (topic, systemPrompt) => $"{systemPrompt}\n\n{topic.Question}\n\nAdditional context",
            llm,
            ct);
    }
    
    // Compose multiple arrows with cancellation support
    public static Step<CouncilTopic, Result<List<AgentContribution>, string>> CreateFullDebate(
        ToolAwareChatModel llm,
        CancellationToken ct = default)
    {
        return async topic =>
        {
            var results = new List<AgentContribution>();
            
            // Generate proposal with cancellation support
            var proposalResult = await CreateProposal(llm, ct)(topic);
            if (!proposalResult.IsSuccess)
                return Result<List<AgentContribution>, string>.Failure(proposalResult.Error);
            
            results.Add(proposalResult.Value);
            
            // Can compose more arrows here
            return Result<List<AgentContribution>, string>.Success(results);
        };
    }
}
```

## Orchestrator Composition

### Before (Inheritance-Based)
```csharp
public sealed class CustomOrchestrator : OrchestratorBase<string, string>
{
    public CustomOrchestrator(OrchestratorConfig config)
        : base("CustomOrchestrator", config)
    {
    }
    
    protected override async Task<string> ExecuteCoreAsync(string input, OrchestratorContext context)
    {
        // Custom implementation
        var processed = input.ToUpper();
        await Task.Delay(100);
        return processed;
    }
    
    protected override Result<bool, string> ValidateInput(string input, OrchestratorContext context)
    {
        if (string.IsNullOrWhiteSpace(input))
            return Result<bool, string>.Failure("Input cannot be empty");
        return Result<bool, string>.Success(true);
    }
}
```

### After (Arrow-Based Composition)
```csharp
// Option 1: Create orchestrator from arrow
public static class CustomOrchestratorFactory
{
    public static IOrchestrator<string, string> Create(OrchestratorConfig? config = null)
    {
        // Define the execution arrow
        Step<string, string> executionArrow = async input =>
        {
            var processed = input.ToUpper();
            await Task.Delay(100);
            return processed;
        };
        
        return OrchestratorArrows.FromSimpleArrow(
            "CustomOrchestrator",
            executionArrow,
            config);
    }
    
    // Option 2: Compose multiple orchestrators
    public static IOrchestrator<string, int> CreatePipeline(
        IOrchestrator<string, string> first,
        IOrchestrator<string, int> second)
    {
        return OrchestratorArrows.Compose(first, second);
    }
    
    // Option 3: Add retry and timeout to an arrow
    public static Step<string, string> CreateResilientArrow(Step<string, string> baseArrow)
    {
        var withRetry = OrchestratorArrows.WithRetry(baseArrow, RetryConfig.Default());
        return OrchestratorArrows.WithTimeout(withRetry, TimeSpan.FromSeconds(30));
    }
}

// Usage
var orchestrator = CustomOrchestratorFactory.Create();
var result = await orchestrator.ExecuteAsync("hello world");
```

## Fix Chain Composition

### Before (Inheritance-Based)
```csharp
public class CustomFixChain : FixChain
{
    public override string Title => "Custom Fix";
    public override string EquivalenceKey => "Custom.Fix";
    
    protected override Future<FixState> DefinePipeline(Future<FixState> input)
    {
        return input
            | StandardSteps.TryResolve
            | CustomStep
            | StandardSteps.FormatCode;
    }
    
    private async Task<FixState> CustomStep(FixState state)
    {
        // Custom logic
        return state;
    }
}
```

### After (Arrow-Based Composition)
```csharp
// Option 1: Use FixChain with functional property
public class CustomFixChain : FixChain
{
    public override string Title => "Custom Fix";
    public override string EquivalenceKey => "Custom.Fix";
    
    protected override Func<Future<FixState>, Future<FixState>> PipelineBuilder =>
        input => input
            | StandardSteps.TryResolve
            | CustomStep
            | StandardSteps.FormatCode;
    
    private async Task<FixState> CustomStep(FixState state)
    {
        // Custom logic
        return state;
    }
}

// Option 2: Direct arrow usage without inheritance
public static class CustomFixChainFactory
{
    public static Func<CodeFixContext, Task> CreateFix()
    {
        return FixChainArrows.CreateFixChain(
            "Custom Fix",
            "Custom.Fix",
            input => input
                | StandardSteps.TryResolve
                | CustomStep
                | StandardSteps.FormatCode);
    }
    
    private static async Task<FixState> CustomStep(FixState state)
    {
        // Custom logic
        return state;
    }
    
    // Can also get a full configuration tuple
    public static (string Title, string EquivalenceKey, Func<CodeFixContext, Task> Register) CreateConfiguration()
    {
        return FixChainArrows.CreateFixConfiguration(
            "Custom Fix",
            "Custom.Fix",
            input => input | StandardSteps.TryResolve | StandardSteps.FormatCode);
    }
}
```

## Benefits of Arrow Composition

1. **Flexibility**: Mix and match pipeline stages without rigid inheritance
2. **Reusability**: Arrow functions can be composed and reused across different contexts
3. **Testability**: Individual arrows are easier to test in isolation
4. **Type Safety**: Leverages the C# type system for compile-time guarantees
5. **Functional Purity**: Encourages immutable transformations and pure functions
6. **Category Theory**: Aligns with mathematical principles (arrows as morphisms)

## Migration Guide

### For Existing Subclasses

1. **If you don't override template methods**: No changes needed! The base classes still work.

2. **If you override template methods**: 
   - Convert protected virtual methods to protected Func properties
   - Or use the arrow factory functions directly

3. **For new implementations**:
   - Consider using arrow factories directly instead of inheritance
   - Only inherit if you need the infrastructure (metrics, tracing, etc.)

### Example Migration

```csharp
// OLD: Override template method
protected override string BuildProposalPrompt(CouncilTopic topic)
{
    return $"Custom: {topic.Question}";
}

// NEW: Override functional property
protected override Func<CouncilTopic, string, string> ProposalPromptBuilder =>
    (topic, systemPrompt) => $"Custom: {topic.Question}";
```
