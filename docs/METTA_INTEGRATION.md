# MeTTa Symbolic Reasoning Integration

This document provides detailed information about the MeTTa (meta-type-talk) symbolic reasoning integration in MonadicPipeline.

## Overview

MeTTa is a powerful symbolic reasoning system that enables formal logic, rule-based inference, and symbolic computation. MonadicPipeline's integration allows you to combine neural LLM reasoning with symbolic AI for hybrid neural-symbolic capabilities.

## Architecture

### Components

1. **IMeTTaEngine Interface**: Core abstraction for MeTTa engines
2. **SubprocessMeTTaEngine**: Default implementation using subprocess communication
3. **HttpMeTTaEngine**: HTTP client for Python Hyperon services
4. **MeTTa Tools**: Integration with the tool system
5. **Memory Bridge**: Sync orchestrator experiences to MeTTa facts

### Engine Implementations

#### Subprocess Engine (Default)

The subprocess engine communicates with the `metta` executable through standard input/output:

```csharp
using var engine = new SubprocessMeTTaEngine();
// or with custom path
using var engine = new SubprocessMeTTaEngine("/usr/local/bin/metta");
```

**Pros:**
- Direct integration with metta-stdlib
- No additional services required
- Full MeTTa language support

**Cons:**
- Requires metta executable installation
- Process overhead for each operation

#### HTTP Engine

The HTTP engine connects to a Python-based MeTTa/Hyperon service:

```csharp
using var engine = new HttpMeTTaEngine("http://localhost:8000", apiKey: "optional");
```

**Pros:**
- No local installation required
- Can use remote services
- Scalable architecture

**Cons:**
- Requires running service
- Network latency
- Additional deployment complexity

## Core Operations

### Adding Facts

```csharp
// Add symbolic facts to knowledge base
await engine.AddFactAsync("(human Socrates)");
await engine.AddFactAsync("(human Plato)");
```

### Executing Queries

```csharp
// Query the knowledge base
var result = await engine.ExecuteQueryAsync("!(match &self (human $x) $result)");
result.Match(
    success => Console.WriteLine($"Found: {success}"),
    error => Console.WriteLine($"Error: {error}")
);
```

### Applying Rules

```csharp
// Define and apply inference rules
await engine.ApplyRuleAsync("(= (mortal $x) (human $x))");

// Query using the rule
var result = await engine.ExecuteQueryAsync("!(match &self (mortal Socrates) $result)");
```

### Plan Verification

```csharp
// Verify a plan symbolically
var planValid = await engine.VerifyPlanAsync("(plan (step1) (step2))");
planValid.Match(
    isValid => Console.WriteLine($"Plan is {(isValid ? "valid" : "invalid")}"),
    error => Console.WriteLine($"Verification failed: {error}")
);
```

## Tool Integration

### Registering MeTTa Tools

```csharp
// Default: subprocess-based engine
var tools = ToolRegistry.CreateDefault()
    .WithMeTTaTools();

// Custom engine
var engine = new SubprocessMeTTaEngine("/path/to/metta");
var tools = ToolRegistry.CreateDefault()
    .WithMeTTaTools(engine);

// HTTP-based engine
var tools = ToolRegistry.CreateDefault()
    .WithMeTTaHttpTools("http://metta-service:8000", "api-key");
```

### Available Tools

| Tool Name | Input | Output | Description |
|-----------|-------|--------|-------------|
| `metta_query` | `{"query": "..."}` | Query result | Execute symbolic query |
| `metta_rule` | `{"rule": "..."}` | Application result | Apply inference rule |
| `metta_verify_plan` | `{"plan": "..."}` | "Plan is valid/invalid" | Verify plan |
| `metta_add_fact` | `{"fact": "..."}` | "Fact added successfully" | Add fact |

### Using Tools with LLM

```csharp
var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
var tools = ToolRegistry.CreateDefault().WithMeTTaTools();
var llm = new ToolAwareChatModel(chatModel, tools);

// LLM can now invoke MeTTa tools
var (response, toolCalls) = await llm.GenerateWithToolsAsync(
    "Check if Socrates is mortal using symbolic reasoning"
);
```

## Memory Bridge

### Syncing Experiences to MeTTa

```csharp
var memory = new MemoryStore(embedModel);
var engine = new SubprocessMeTTaEngine();

// Create bridge
var bridge = memory.CreateMeTTaBridge(engine);

// Sync all experiences
var syncResult = await bridge.SyncAllExperiencesAsync();
syncResult.Match(
    count => Console.WriteLine($"Synced {count} facts"),
    error => Console.WriteLine($"Error: {error}")
);
```

### Adding Individual Experiences

```csharp
// Add a single experience as MeTTa facts
var experience = new Experience(...);
var result = await bridge.AddExperienceAsync(experience);
```

### Querying Synced Data

```csharp
// Query experiences using symbolic reasoning
var queryResult = await bridge.QueryExperiencesAsync(
    "!(match &self (experience-quality $id $score) (> $score 0.8))"
);

queryResult.Match(
    results => Console.WriteLine($"High-quality experiences: {results}"),
    error => Console.WriteLine($"Query failed: {error}")
);
```

## Meta-AI Orchestrator Integration

### Basic Integration

```csharp
var engine = new SubprocessMeTTaEngine();
var tools = ToolRegistry.CreateDefault()
    .WithMeTTaTools(engine);

var orchestrator = MetaAIBuilder.CreateDefault()
    .WithLLM(chatModel)
    .WithTools(tools)
    .WithEmbedding(embedModel)
    .Build();
```

### Domain Knowledge Setup

```csharp
// Add domain-specific rules to MeTTa
await engine.AddFactAsync("(prerequisite teaching-fp basic-programming)");
await engine.AddFactAsync("(requires teaching-fp higher-order-functions)");
await engine.AddFactAsync("(requires teaching-fp immutability)");

// Orchestrator can use symbolic reasoning during planning
var planResult = await orchestrator.PlanAsync(
    "Create a curriculum for teaching functional programming"
);
```

### Hybrid Neural-Symbolic Reasoning

The orchestrator can combine:
- **Neural**: Pattern recognition, natural language understanding
- **Symbolic**: Formal verification, rule-based inference, constraint checking

```csharp
// Planning phase: Use LLM for generation
var plan = await orchestrator.PlanAsync(goal);

// Verification phase: Use MeTTa for symbolic verification
if (plan.IsSuccess)
{
    var mettaPlan = ConvertToMeTTaFormat(plan.Value);
    var verification = await engine.VerifyPlanAsync(mettaPlan);
    
    verification.Match(
        isValid => Console.WriteLine($"Plan verified: {isValid}"),
        error => Console.WriteLine($"Verification error: {error}")
    );
}
```

## Error Handling

All MeTTa operations use the `Result<T, TError>` monad for safe error handling:

```csharp
var result = await engine.ExecuteQueryAsync(query);

// Pattern matching
result.Match(
    success => HandleSuccess(success),
    error => HandleError(error)
);

// Or functional composition
var transformed = result
    .Map(ParseResult)
    .Bind(ValidateResult)
    .Map(FormatOutput);
```

## Installation

### Subprocess Engine Requirements

1. Install metta-stdlib:
   ```bash
   # Follow MeTTa installation instructions
   git clone https://github.com/trueagi-io/metta-stdlib
   cd metta-stdlib
   # Build and install
   ```

2. Ensure `metta` is in PATH or provide explicit path:
   ```csharp
   var engine = new SubprocessMeTTaEngine("/usr/local/bin/metta");
   ```

### HTTP Engine Requirements

1. Start a Python Hyperon service:
   ```python
   # Example Python service (simplified)
   from flask import Flask, request, jsonify
   from hyperon import MeTTa
   
   app = Flask(__name__)
   metta = MeTTa()
   
   @app.route('/query', methods=['POST'])
   def query():
       data = request.json
       result = metta.run(data['query'])
       return jsonify({'result': str(result)})
   
   app.run(port=8000)
   ```

2. Configure client:
   ```csharp
   var engine = new HttpMeTTaEngine("http://localhost:8000");
   ```

## Examples

See [`Examples/MeTTaIntegrationExample.cs`](../Examples/MeTTaIntegrationExample.cs) for complete examples:

1. **Basic MeTTa Operations**: Queries, rules, facts
2. **Tool Integration**: Using MeTTa tools with ToolRegistry
3. **HTTP Client**: Connecting to Python service
4. **Orchestrator Integration**: Hybrid neural-symbolic AI
5. **Memory Bridge**: Syncing experiences to MeTTa

## Best Practices

1. **Dispose Engines**: Always dispose MeTTa engines to free resources
   ```csharp
   using var engine = new SubprocessMeTTaEngine();
   ```

2. **Error Handling**: Use Result monad pattern matching
   ```csharp
   result.Match(success => ..., error => ...);
   ```

3. **Tool Registration**: Register MeTTa tools early in pipeline setup
   ```csharp
   var tools = ToolRegistry.CreateDefault().WithMeTTaTools();
   ```

4. **Domain Knowledge**: Add domain-specific facts before reasoning
   ```csharp
   await engine.AddFactAsync("(domain-specific-rule)");
   ```

5. **Verification**: Use symbolic verification for critical operations
   ```csharp
   var verified = await engine.VerifyPlanAsync(plan);
   ```

## Limitations

1. **Subprocess Engine**: 
   - Requires metta executable installation
   - Process startup overhead
   - Limited to local execution

2. **HTTP Engine**:
   - Requires running service
   - Network latency
   - Service availability dependency

3. **MeTTa Language**: 
   - Learning curve for symbolic syntax
   - Not all neural patterns translate to symbolic

## Future Enhancements

- [ ] Automatic neural-to-symbolic translation
- [ ] Cached symbolic queries for performance
- [ ] Distributed MeTTa engine support
- [ ] Enhanced memory bridge with incremental sync
- [ ] Visual symbolic reasoning debugger

## Resources

- [MeTTa Documentation](https://github.com/trueagi-io/metta-stdlib)
- [Hyperon Project](https://github.com/trueagi-io/hyperon-experimental)
- [MonadicPipeline Examples](../Examples/MeTTaIntegrationExample.cs)
- [Test Suite](../Tests/MeTTaTests.cs)
