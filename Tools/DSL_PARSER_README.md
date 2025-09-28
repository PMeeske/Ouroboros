# DSL Parser for Tool Invocations

## Overview

The **ToolCallParser** provides sophisticated parsing for tool invocations in the MonadicPipeline system. It replaces the basic string-splitting approach with a context-aware parser that handles complex argument formats correctly.

## Problem Solved

### Before: Basic String Splitting
```csharp
// OLD - BROKEN APPROACH
string inside = line.Trim('[', ']')[5..].Trim(); // Remove "[TOOL:" prefix
string[] split = inside.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
string name = split[0];
string args = split.Length > 1 ? split[1] : string.Empty;
```

**Problems:**
- ❌ `[TOOL:search {"q":"tenant cache", "k":3}]` → name=`search`, args=`{"q":"tenant`
- ❌ `[TOOL:math (10 - 5) * 2]` → name=`math`, args=`(10`
- ❌ Complex JSON objects completely broken
- ❌ No validation of argument formats

### After: Sophisticated DSL Parser
```csharp
// NEW - WORKING APPROACH
var toolCalls = ToolCallParser.ParseToolCalls(text);
foreach (var call in toolCalls) {
    // call.Name and call.Arguments correctly parsed
    var result = await tool.InvokeAsync(call.Arguments);
}
```

**Benefits:**
- ✅ `[TOOL:search {"q":"tenant cache", "k":3}]` → name=`search`, args=`{"q":"tenant cache", "k":3}`
- ✅ `[TOOL:math (10 - 5) * 2]` → name=`math`, args=`(10 - 5) * 2`
- ✅ Complex nested JSON objects work perfectly
- ✅ JSON validation and type detection
- ✅ Monadic error handling with Result<T>

## Supported Syntax

### 1. Simple Text Arguments
```
[TOOL:math 2+2*5]
[TOOL:simple hello world]
```

### 2. JSON Objects
```
[TOOL:search {"q":"monadic pipelines", "k":5}]
[TOOL:complex {"config":{"nested":{"value":"test"}}, "array":[1,2,3]}]
```

### 3. Mathematical Expressions
```
[TOOL:math (10 - 5) * 2 + 3]
[TOOL:math ((25 / 5) + 3) * 2]
[TOOL:math 2 * (3 + 4 * (5 - 2))]
```

### 4. Mixed Scenarios
```
Let me help you with calculations and searches.
[TOOL:math (15 + 25) / 2]
Now searching: [TOOL:search {"q":"functional programming", "k":3}]
```

## API Reference

### Core Functions

#### `ParseToolCalls(string text)`
Parses all tool calls from a text block.
```csharp
List<ToolCall> calls = ToolCallParser.ParseToolCalls(responseText);
```

#### `ParseSingleToolCall(string line)`
Parses a single tool call line with monadic error handling.
```csharp
Result<ToolCall, string> result = ToolCallParser.ParseSingleToolCall("[TOOL:math 2+2]");
result.Match(
    success => Console.WriteLine($"Tool: {success.Name}, Args: {success.Arguments}"),
    error => Console.WriteLine($"Parse error: {error}")
);
```

### Validation Functions

#### `ValidateJsonArguments(string json)`
Validates JSON format for tools expecting structured data.
```csharp
var validation = ToolCallParser.ValidateJsonArguments("{\"q\":\"test\"}");
// Returns Result<string, string> with success or validation error
```

#### `IsJsonArguments(string args)` / `IsMathExpression(string args)`
Type detection helpers for argument classification.
```csharp
bool isJson = ToolCallParser.IsJsonArguments("{\"key\":\"value\"}"); // true
bool isMath = ToolCallParser.IsMathExpression("2 + 2 * 5"); // true
```

## Integration

### ToolAwareChatModel Integration
The parser is automatically used in `ToolAwareChatModel.GenerateWithToolsAsync()`:

```csharp
// Automatically parses and executes tool calls from LLM responses
var (text, toolExecutions) = await toolAwareLlm.GenerateWithToolsAsync(prompt);
```

### Manual Usage
For custom scenarios or testing:

```csharp
string llmResponse = "I'll calculate: [TOOL:math 2+2*5]";
var toolCalls = ToolCallParser.ParseToolCalls(llmResponse);

foreach (var call in toolCalls)
{
    var tool = toolRegistry.Get(call.Name);
    if (tool != null)
    {
        var result = await tool.InvokeAsync(call.Arguments);
        // Handle result...
    }
}
```

## Error Handling

The parser uses monadic error handling throughout:

```csharp
// Single tool call parsing
Result<ToolCall, string> parseResult = ToolCallParser.ParseSingleToolCall(line);
parseResult.Match(
    success => ProcessToolCall(success),
    error => LogParseError(error)
);

// JSON validation
Result<string, string> jsonResult = ToolCallParser.ValidateJsonArguments(args);
jsonResult.Match(
    validJson => UseJsonArgs(validJson),
    error => HandleJsonError(error)
);
```

## Examples

See `Examples/DslParserExamples.cs` for comprehensive demonstrations of:
- Basic tool call parsing
- JSON argument handling  
- Mathematical expression parsing
- Complex mixed scenarios
- Error handling patterns

## Testing

Run the comprehensive test suite:
```bash
dotnet run  # Includes DSL parser tests in the demonstration
```

The test suite covers:
- ✅ Basic tool calls
- ✅ JSON arguments with complex nesting
- ✅ Mathematical expressions with parentheses
- ✅ Edge cases and error conditions
- ✅ Multiple tool calls in single text
- ✅ Argument type detection
- ✅ JSON validation

## Technical Details

### Parser Architecture
- **Context-Aware**: Respects JSON brackets `{}`, arrays `[]`, parentheses `()`, and quoted strings `""`
- **Single Pass**: Efficient parsing without backtracking
- **Error Recovery**: Skips invalid tool calls and continues processing
- **Functional**: Uses Result monads for consistent error handling

### Performance
- **Minimal Allocations**: Efficient string processing
- **O(n) Complexity**: Linear parsing performance
- **Memory Efficient**: No intermediate collections for simple cases

This DSL parser brings the tool invocation system up to production quality, enabling complex AI workflows with reliable argument parsing.