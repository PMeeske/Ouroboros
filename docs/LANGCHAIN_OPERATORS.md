# LangChain Pipe Operators Integration

This document describes the integration of LangChain's native pipe operators into the MonadicPipeline system.

## Overview

The MonadicPipeline now supports LangChain's familiar pipe operator syntax while maintaining functional programming guarantees. This integration provides the best of both worlds:

- **LangChain's operator convenience**: Familiar `Set | Retrieve | Template | LLM` syntax
- **MonadicPipeline's safety guarantees**: Kleisli composition, monadic laws, type safety
- **Flexible usage**: Both CLI DSL and code-based composition with static imports

## Available Operators

### CLI Tokens

The following CLI pipeline tokens are now available:

| Token | Aliases | Description |
|-------|---------|-------------|
| `LangChainSet` | `ChainSet` | Sets a value in the pipeline context |
| `LangChainRetrieve` | `ChainRetrieve` | Retrieves similar documents from vector store |
| `LangChainCombine` | `ChainCombine` | Combines retrieved documents into context |
| `LangChainTemplate` | `ChainTemplate` | Applies a prompt template with placeholders |
| `LangChainLLM` | `ChainLLM` | Sends prompt to the language model |
| `LangChainRAG` | `ChainRAG` | Complete RAG pipeline (all steps combined) |

### Static Helper Functions

When using code-based composition, you can import static helpers:

```csharp
using static LangChainPipeline.Core.Interop.Pipe;
```

Available functions:
- `Set(string value, string? key = null)` - Sets a value
- `RetrieveSimilarDocuments(int amount = 5)` - Retrieves documents
- `CombineDocuments(string? separator = null)` - Combines documents
- `Template(string template)` - Applies template
- `LLM()` - Calls language model

## Usage Examples

### CLI DSL Usage

```bash
# Basic RAG pipeline using individual operators
dotnet run -- pipeline --dsl "SetQuery('What is AI?') | LangChainRetrieve('amount=5') | LangChainCombine() | LangChainTemplate('Use context: {context}...') | LangChainLLM()"

# Using the complete RAG operator
dotnet run -- pipeline --dsl "LangChainRAG('question=What is AI?|k=5')"
```

### Code-Based Usage

```csharp
using static LangChainPipeline.Core.Interop.Pipe;
using LangChainPipeline.CLI;
using LangChainPipeline.Core.Steps;

// Create a RAG pipeline using static helpers
var ragPipeline = Set("Who was drinking unicorn blood?", "query")
    .Bind(RetrieveSimilarDocuments(5))
    .Bind(CombineDocuments())
    .Bind(Template(@"
        Use the following context to answer the question.
        Context: {context}
        Question: {question}
        Answer:"))
    .Bind(LLM());

// Execute the pipeline
var state = new CliPipelineState { /* initialize */ };
var result = await ragPipeline(state);
```

## Comparison with Original LangChain

### Original LangChain Syntax

```csharp
using static LangChain.Chains.Chain;

var chain =
    Set("Who was drinking a unicorn blood?")
    | RetrieveSimilarDocuments(vectorCollection, embeddingModel, amount: 5)
    | CombineDocuments(outputKey: "context")
    | Template(promptTemplate)
    | LLM(llm);

var answer = await chain.RunAsync("text");
```

### MonadicPipeline Equivalent

```csharp
using static LangChainPipeline.Core.Interop.Pipe;

var pipeline = Set("Who was drinking a unicorn blood?", "query")
    .Bind(RetrieveSimilarDocuments(5))
    .Bind(CombineDocuments())
    .Bind(Template(promptTemplate))
    .Bind(LLM());

var result = await pipeline(state);
```

## Operator Details

### LangChainSet / ChainSet

Sets a value in the pipeline state.

**CLI Usage:**
```bash
LangChainSet('value|key')
```

**Code Usage:**
```csharp
Set("My question", "query")
```

### LangChainRetrieve / ChainRetrieve

Retrieves similar documents from the vector store using semantic search.

**CLI Usage:**
```bash
LangChainRetrieve('amount=5')
```

**Code Usage:**
```csharp
RetrieveSimilarDocuments(5)
```

**Parameters:**
- `amount`: Number of documents to retrieve (default: 5)

### LangChainCombine / ChainCombine

Combines retrieved documents into a single context string.

**CLI Usage:**
```bash
LangChainCombine('separator=\\n\\n')
```

**Code Usage:**
```csharp
CombineDocuments("\n\n")
```

**Parameters:**
- `separator`: Optional separator between documents

### LangChainTemplate / ChainTemplate

Applies a prompt template with variable substitution.

**CLI Usage:**
```bash
LangChainTemplate('Use context: {context}\nQuestion: {question}\nAnswer:')
```

**Code Usage:**
```csharp
Template("Use context: {context}\nQuestion: {question}\nAnswer:")
```

**Supported placeholders:**
- `{context}` - Combined documents
- `{question}` or `{text}` - User query
- `{prompt}` - Current prompt
- `{topic}` - Topic
- `{query}` - Query text

### LangChainLLM / ChainLLM

Sends the formatted prompt to the language model.

**CLI Usage:**
```bash
LangChainLLM()
```

**Code Usage:**
```csharp
LLM()
```

### LangChainRAG / ChainRAG

Complete RAG (Retrieval-Augmented Generation) pipeline combining all operators.

**CLI Usage:**
```bash
LangChainRAG('question=What is AI?|template=...|k=5')
```

**Parameters:**
- `question`: The question to answer
- `template`: Optional custom template
- `k`: Number of documents to retrieve (default: 5)

## Architectural Benefits

### Type Safety
- All operators are type-checked at compile time
- `Step<CliPipelineState, CliPipelineState>` ensures proper composition

### Error Handling
- Graceful fallbacks when operations fail
- Error messages logged with trace flag
- No exceptions thrown in normal operation

### Immutability
- Pipeline state is immutable
- Each operator returns a new state
- Event sourcing tracks all transformations

### Testability
- Each operator is a pure function
- Easy to unit test individual components
- Composable and mockable

## Integration with Existing Features

The LangChain operators work seamlessly with existing MonadicPipeline features:

- **Vector Stores**: Uses `IVectorCollection` from the branch store
- **Embeddings**: Leverages `IEmbeddingModel` adapters
- **Language Models**: Integrates with `ToolAwareChatModel`
- **Event Sourcing**: All operations are recorded as events
- **Reasoning**: Compatible with Draft/Critique/Improve workflow

## Migration Guide

If you're familiar with LangChain and want to use MonadicPipeline:

1. **Install MonadicPipeline** (already done)
2. **Import static helpers**:
   ```csharp
   using static LangChainPipeline.Core.Interop.Pipe;
   ```
3. **Replace LangChain chains with MonadicPipeline steps**:
   - `Set()` → `Set(value, key)`
   - `RetrieveSimilarDocuments()` → `RetrieveSimilarDocuments(amount)`
   - `CombineDocuments()` → `CombineDocuments()`
   - `Template()` → `Template(template)`
   - `LLM()` → `LLM()`
4. **Use `.Bind()` for composition** instead of `|`
5. **Initialize `CliPipelineState`** before executing

## Examples

See `Examples/LangChainPipeOperatorsExample.cs` for complete usage examples and demonstrations.

## Future Enhancements

Potential additions:
- `LoadMemory` / `UpdateMemory` operators for conversation
- `SaveToFile` operator
- `ReActAgent` operator
- Custom chain registration
