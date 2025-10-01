# Meta-AI Layer Documentation

## Overview

The MonadicPipeline now includes **meta-AI capabilities** where the pipeline can reason about and modify its own execution. This is achieved by exposing CLI pipeline steps as tools that the LLM can invoke, creating a self-reflective AI system that thinks about its own thinking.

## What is Meta-AI?

Meta-AI refers to an AI system that has awareness of and control over its own reasoning process. In MonadicPipeline:

- **Pipeline steps** (like `UseDraft`, `UseCritique`, `UseImprove`) are registered as **tools**
- The **LLM** can invoke these tools during generation
- This creates a **feedback loop** where the AI can improve its own responses
- The pipeline becomes **self-modifying** and **self-improving**

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    ToolAwareChatModel                    │
│  ┌────────────────────────────────────────────────────┐ │
│  │  LLM generates text with [TOOL:name args] syntax   │ │
│  └────────────────────────────────────────────────────┘ │
│                           │                              │
│                           ▼                              │
│  ┌────────────────────────────────────────────────────┐ │
│  │         ToolRegistry (includes pipeline steps)      │ │
│  │  • Math tools                                       │ │
│  │  • Search tools                                     │ │
│  │  • Pipeline step tools ← NEW!                       │ │
│  │    - run_usedraft                                   │ │
│  │    - run_usecritique                                │ │
│  │    - run_useimprove                                 │ │
│  │    - run_setprompt                                  │ │
│  │    - run_llm                                        │ │
│  │    - ... all CLI steps                              │ │
│  └────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
```

## Key Components

### 1. PipelineStepTool

A new `ITool` implementation that wraps CLI pipeline steps:

```csharp
var tool = PipelineStepTool.FromStepName("UseDraft", "Generate initial draft");
tool.SetPipelineState(state);
```

Features:
- Wraps any CLI step as a tool
- Maintains reference to pipeline state
- Executes steps when invoked by LLM
- Returns results back to the LLM

### 2. PipelineToolExtensions

Extension methods for easy tool registration:

```csharp
// Register ALL pipeline steps as tools
tools = tools.WithPipelineSteps(state);

// Register SPECIFIC pipeline steps
tools = tools.WithPipelineSteps(state, "UseDraft", "UseCritique", "UseImprove");
```

### 3. Automatic Registration

Pipeline steps are **automatically registered as tools** when running DSL commands:

```csharp
// In Program.cs - happens automatically
tools = tools.WithPipelineSteps(state);
var llm = new ToolAwareChatModel(chatModel, tools);
```

## Usage Examples

### Basic Meta-AI Pipeline

```bash
# The LLM can now invoke pipeline steps as tools
dotnet run -- pipeline --dsl "SetPrompt('Explain meta-AI') | LLM"
```

When the LLM generates a response, it can emit tool calls like:
```
[TOOL:run_usedraft {"args": ""}]
[TOOL:run_usecritique {"args": ""}]
[TOOL:run_useimprove {"args": ""}]
```

### Self-Improving Pipeline

```bash
# Pipeline that can improve its own output
dotnet run -- pipeline --dsl "SetTopic('functional programming') | UseRefinementLoop('3')"
```

The LLM inside each step can invoke other steps to enhance reasoning!

### Programmatic Usage

```csharp
using LangChainPipeline.CLI;
using LangChainPipeline.Tools;

// Setup
var tools = new ToolRegistry();
var state = new CliPipelineState { /* ... */ };

// Enable meta-AI by registering pipeline steps
tools = tools.WithPipelineSteps(state);

// Create tool-aware LLM
var llm = new ToolAwareChatModel(chatModel, tools);
state.Llm = llm;

// LLM can now invoke pipeline steps during generation
var (response, toolCalls) = await llm.GenerateWithToolsAsync(prompt);
```

## Benefits

### 1. Self-Reflection
The pipeline can analyze and critique its own outputs:
```
LLM: "Let me draft an answer..."
LLM: [TOOL:run_usedraft]
LLM: "Now let me critique this draft..."
LLM: [TOOL:run_usecritique]
LLM: "Based on the critique, I'll improve it..."
LLM: [TOOL:run_useimprove]
```

### 2. Dynamic Workflow Construction
The LLM can decide which pipeline steps to invoke based on context:
```csharp
// The LLM chooses its own workflow
if (needs_more_context)
    [TOOL:run_retrieve]
if (needs_initial_response)
    [TOOL:run_usedraft]
if (needs_refinement)
    [TOOL:run_usecritique]
```

### 3. Emergent Capabilities
New behaviors emerge from the combination of:
- LLM reasoning
- Tool invocation
- Pipeline step composition
- Feedback loops

## Available Pipeline Tools

When you register pipeline steps as tools, they become available with the `run_` prefix:

| Step Name | Tool Name | Description |
|-----------|-----------|-------------|
| `UseDraft` | `run_usedraft` | Generate initial draft response |
| `UseCritique` | `run_usecritique` | Critique current draft |
| `UseImprove` | `run_useimprove` | Improve draft based on critique |
| `SetPrompt` | `run_setprompt` | Set new prompt |
| `SetTopic` | `run_settopic` | Set topic |
| `LLM` | `run_llm` | Execute LLM generation |
| `Retrieve` | `run_retrieve` | Semantic search |
| `UseIngest` | `run_useingest` | Ingest documents |
| ... | ... | All CLI steps available |

## Examples

See the following files for complete examples:

1. **Tests/MetaAiTests.cs** - Unit tests demonstrating meta-AI capabilities
2. **Examples/MetaAiPipelineExample.cs** - Runnable examples showing usage
3. **Program.cs** - Integration with DSL pipeline execution

## Implementation Details

### Tool Invocation Flow

1. LLM generates text containing `[TOOL:toolname args]`
2. `ToolAwareChatModel` parses tool invocations
3. `ToolRegistry` looks up the tool
4. `PipelineStepTool.InvokeAsync()` executes the pipeline step
5. Result is appended as `[TOOL-RESULT:toolname] output`
6. LLM sees the result and continues generation

### State Management

Pipeline step tools maintain a reference to `CliPipelineState`:
```csharp
public void SetPipelineState(CliPipelineState state)
{
    _pipelineState = state;
}
```

When invoked, they:
1. Execute the wrapped pipeline step
2. Update the shared pipeline state
3. Return results to the LLM

### Thread Safety

The current implementation assumes single-threaded execution within a pipeline. For concurrent access, consider:
- Immutable state updates
- Lock-based synchronization
- Actor-based concurrency

## Future Enhancements

Potential improvements to the meta-AI layer:

1. **Memory of Tool Usage** - Track which tools were effective
2. **Tool Composition** - Allow tools to invoke other tools
3. **Learned Workflows** - Save successful tool sequences
4. **Conditional Execution** - Tools that execute based on state
5. **Parallel Tool Invocation** - Execute multiple tools simultaneously
6. **Tool Result Caching** - Avoid redundant executions

## Troubleshooting

### Tools Not Being Invoked

If the LLM doesn't invoke tools:
- Ensure model supports tool/function calling
- Check prompt includes tool usage instructions
- Verify tools are registered before LLM creation
- Enable trace mode to see tool schemas

### State Not Updating

If pipeline state doesn't change:
- Verify `SetPipelineState()` was called on tools
- Check tool execution completes successfully
- Enable trace logging to debug

### Tool Not Found Errors

If you see "tool not found":
- List available tools: `tools.All.Select(t => t.Name)`
- Check tool name uses `run_` prefix
- Verify step is registered in `StepRegistry`

## Best Practices

1. **Selective Registration** - Only register tools the LLM should use
2. **Clear Descriptions** - Provide detailed tool descriptions
3. **Error Handling** - Tools should handle errors gracefully
4. **State Validation** - Verify state is valid before execution
5. **Logging** - Enable trace mode during development

## Conclusion

The meta-AI layer transforms MonadicPipeline into a self-aware system where the AI can reason about and improve its own thinking process. This creates powerful emergent behaviors and enables sophisticated multi-step reasoning workflows.

The pipeline can now truly "think about its thinking" - making it a genuine meta-AI system.
