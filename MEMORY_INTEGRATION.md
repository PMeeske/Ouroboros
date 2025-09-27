# LangChain Memory Integration with Kleisli Pipelines

This integration demonstrates how to merge LangChain's memory concepts with the existing Kleisli pipeline system, providing a monadic approach to conversational AI.

## Overview

The integration provides:

1. **Memory-Aware Pipeline Context** - `MemoryContext<T>` that carries both data and conversation memory
2. **LangChain-Style Chain Building** - Fluent API that mirrors LangChain's approach but uses Kleisli arrows
3. **Multiple Memory Strategies** - Buffer, window, summary, and summary buffer memory implementations
4. **Seamless Integration** - Works with existing Kleisli pipeline components

## Key Components

### ConversationMemory
Manages conversation history with configurable limits and formatting options.

```csharp
var memory = new ConversationMemory(maxTurns: 10);
memory.AddTurn("Hello!", "Hi there!");
var history = memory.GetFormattedHistory();
```

### MemoryArrows
Provides Kleisli arrows for memory-aware operations:
- `LoadMemory<T>()` - Loads conversation history into context
- `UpdateMemory<T>()` - Updates memory with new conversation turn
- `Template()` - Processes conversation templates
- `MockLLM()` - Mock language model for demonstrations

### ConversationChainBuilder
Fluent builder that mirrors LangChain's chain syntax:

```csharp
var chain = input
    .StartConversation(memory)
    .LoadMemory(outputKey: "history")
    .Template(template)
    .LLM("AI:")
    .UpdateMemory(inputKey: "input", responseKey: "text");

var response = await chain.RunAsync<string>("text");
```

## LangChain Equivalence

The integration provides direct equivalence to LangChain's chain syntax:

**LangChain:**
```csharp
var chain =
    LoadMemory(memory, outputKey: "history")
    | Template(template)
    | LLM(model)
    | UpdateMemory(memory, requestKey: "input", responseKey: "text");
```

**Kleisli Pipeline:**
```csharp
var chain = input
    .StartConversation(memory)
    .LoadMemory(outputKey: "history")
    .Template(template)
    .LLM("AI:")
    .UpdateMemory(inputKey: "input", responseKey: "text");
```

## Memory Strategies

The system supports multiple memory strategies similar to LangChain:

1. **ConversationBufferMemory** - Keeps all conversation history
2. **ConversationWindowBufferMemory** - Keeps only recent turns
3. **ConversationSummaryMemory** - Could summarize old conversations (simplified)
4. **ConversationSummaryBufferMemory** - Combines token limits with summarization (simplified)

## Examples

See `Examples/LangChainStyleExample.cs` for a complete demonstration that mirrors the original LangChain conversation example but uses our Kleisli pipeline system.

## Benefits

1. **Type Safety** - Full compile-time type checking with monadic operations
2. **Composability** - Memory-aware operations compose naturally with existing Kleisli arrows
3. **Functional Approach** - Immutable contexts and pure functional transformations
4. **Error Handling** - Integrated Result and Option monad support
5. **Testability** - Easy to mock and test individual pipeline components