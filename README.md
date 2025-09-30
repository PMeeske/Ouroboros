# MonadicPipeline

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)](https://github.com/PMeeske/MonadicPipeline)
[![.NET Version](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![LangChain](https://img.shields.io/badge/LangChain-0.17.0-purple)](https://www.nuget.org/packages/LangChain/)

A **sophisticated functional programming-based AI pipeline system** built on LangChain, implementing category theory principles, monadic composition, and functional programming patterns to create type-safe, composable AI workflows.

## 🚀 Key Features

- **🧮 Monadic Composition**: Type-safe pipeline operations using `Result<T>` and `Option<T>` monads
- **🔗 Kleisli Arrows**: Mathematical composition of computations in monadic contexts  
- **🤖 LangChain Integration**: Native integration with LangChain providers and tools
- **⚡ LangChain Pipe Operators**: Familiar `Set | Retrieve | Template | LLM` syntax with monadic safety
- **📊 Vector Database Support**: Built-in vector storage and retrieval capabilities
- **🔄 Event Sourcing**: Complete audit trail with replay functionality
- **🛠️ Extensible Tool System**: Plugin architecture for custom tools and functions
- **💾 Memory Management**: Multiple conversation memory strategies
- **🎯 Type Safety**: Leverages C# type system for compile-time guarantees

## 🏗️ Architecture

MonadicPipeline follows a **Functional Pipeline Architecture** with monadic composition as its central organizing principle:

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Core Layer    │    │  Domain Layer   │    │ Pipeline Layer  │
│                 │    │                 │    │                 │
│ • Monads        │───▶│ • Events        │───▶│ • Branches      │
│ • Kleisli       │    │ • States        │    │ • Reasoning     │
│ • Steps         │    │ • Vectors       │    │ • Ingestion     │
└─────────────────┘    └─────────────────┘    └─────────────────┘
         │                       │                       │
         └───────────────────────┼───────────────────────┘
                                 ▼
                    ┌─────────────────┐
                    │Integration Layer│
                    │                 │
                    │ • Tools         │
                    │ • Providers     │
                    │ • Memory        │
                    └─────────────────┘
```

## 🔧 Getting Started

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- [Ollama](https://ollama.ai/) (for local LLM providers)

### Installation

1. **Clone the repository:**
   ```bash
   git clone https://github.com/PMeeske/MonadicPipeline.git
   cd MonadicPipeline
   ```

2. **Restore dependencies:**
   ```bash
   dotnet restore
   ```

3. **Build the project:**
   ```bash
   dotnet build
   ```

4. **Run the demonstrations:**
   ```bash
   dotnet run
   ```

### Quick Start Example

```csharp
// Create a simple monadic pipeline
var pipeline = Step.Pure<string>()
    .Bind(ValidateInput)
    .Map(ProcessData)
    .Bind(ExecuteReasoning)
    .Map(FormatOutput);

// Execute with error handling
var result = await pipeline("Hello, MonadicPipeline!");
result.Match(
    success => Console.WriteLine($"Result: {success}"),
    error => Console.WriteLine($"Error: {error}")
);
```

## 🧠 Core Concepts

### Monads
MonadicPipeline uses monads for safe, composable operations:
- **`Result<T>`**: Represents operations that can succeed or fail
- **`Option<T>`**: Represents potentially null values safely

### Kleisli Arrows
Mathematical composition of monadic computations:
```csharp
public static Step<TInput, TOutput> CreateStep<TInput, TOutput>(
    Func<TInput, Task<Result<TOutput>>> operation) => 
    async input => await operation(input);
```

### Pipeline Composition
Chain operations using monadic bind:
```csharp
var enhancedPipeline = Step.Pure<string>()
    .Bind(LoadContext)
    .Bind(GenerateDraft)
    .Bind(CritiqueDraft)
    .Map(FinalizeResponse);
```

## 📁 Project Structure

```
├── Core/                    # Monadic abstractions and core functionality
│   ├── Conversation/        # Conversational pipeline builders
│   ├── Kleisli/            # Category theory implementation
│   ├── Memory/             # Memory management for conversations
│   ├── Monads/             # Option and Result monad implementations
│   └── Steps/              # Pipeline step abstractions
├── Domain/                 # Domain models and business logic
│   ├── Events/             # Event sourcing patterns
│   ├── States/             # State management
│   └── Vectors/            # Vector database abstractions
├── Pipeline/               # Pipeline implementation layers
│   ├── Branches/           # Branch management and persistence
│   ├── Ingestion/          # Data ingestion pipelines
│   ├── Reasoning/          # AI reasoning workflows
│   └── Replay/             # Execution replay functionality
├── Tools/                  # Extensible tool system
├── Providers/              # External service providers
└── Examples/               # Comprehensive examples and demonstrations
```

## 🎯 Examples

The `Examples/` directory contains comprehensive demonstrations:

- **`MonadicExamples.cs`**: Core monadic operations
- **`ConversationalKleisliExamples.cs`**: Memory-integrated conversations
- **`HybridStepExamples.cs`**: Sync/async step combinations
- **`FunctionalReasoningExamples.cs`**: AI reasoning workflows
- **`LangChainPipeOperatorsExample.cs`**: **NEW!** LangChain-style pipe operators

Run all examples:
```bash
dotnet run
```

## 🔗 LangChain Pipe Operators

MonadicPipeline now supports **LangChain's familiar pipe operator syntax** while maintaining functional programming guarantees. This provides the best of both worlds: LangChain's convenience with MonadicPipeline's safety.

### Quick Example

```bash
# CLI DSL usage - RAG pipeline
dotnet run -- pipeline --dsl "SetQuery('What is AI?') | LangChainRetrieve('amount=5') | LangChainCombine() | LangChainTemplate('Context: {context}...') | LangChainLLM()"

# Or use the complete RAG operator
dotnet run -- pipeline --dsl "LangChainRAG('question=What is AI?|k=5')"
```

### Code-Based Composition

```csharp
using static LangChainPipeline.Core.Interop.Pipe;

var pipeline = Set("Who was drinking unicorn blood?", "query")
    .Bind(RetrieveSimilarDocuments(5))
    .Bind(CombineDocuments())
    .Bind(Template("Use context: {context}\nQuestion: {question}\nAnswer:"))
    .Bind(LLM());
```

### Available Operators

- **`Set`** / `LangChainSet` - Sets values in pipeline state
- **`RetrieveSimilarDocuments`** / `LangChainRetrieve` - Semantic document search
- **`CombineDocuments`** / `LangChainCombine` - Combines retrieved documents
- **`Template`** / `LangChainTemplate` - Applies prompt templates
- **`LLM`** / `LangChainLLM` - Sends to language model
- **`LangChainRAG`** - Complete RAG pipeline

See [`docs/LANGCHAIN_OPERATORS.md`](docs/LANGCHAIN_OPERATORS.md) for comprehensive documentation.

## 🌐 Remote Endpoint Configuration

MonadicPipeline supports multiple remote AI endpoints including **Ollama Cloud**, **OpenAI**, and other OpenAI-compatible services.

### Quick Setup

#### Using Ollama Cloud
```bash
# Set environment variables (recommended)
export CHAT_ENDPOINT="https://api.ollama.com"
export CHAT_API_KEY="your-ollama-cloud-api-key"

# Or use CLI flags
dotnet run -- ask -q "What is functional programming?" --endpoint "https://api.ollama.com" --api-key "your-key"
```

#### Using OpenAI or Compatible Services
```bash
# OpenAI
export CHAT_ENDPOINT="https://api.openai.com"
export CHAT_API_KEY="your-openai-api-key"

# Other OpenAI-compatible services
export CHAT_ENDPOINT="https://your-custom-endpoint.com"
export CHAT_API_KEY="your-api-key"
export CHAT_ENDPOINT_TYPE="openai"  # Force OpenAI-compatible format
```

### Configuration Methods

#### 1. Environment Variables (Recommended)
```bash
export CHAT_ENDPOINT="https://api.ollama.com"           # Required: API endpoint URL
export CHAT_API_KEY="your-api-key"                      # Required: Your API key
export CHAT_ENDPOINT_TYPE="ollama-cloud"                # Optional: Force endpoint type
```

#### 2. CLI Arguments
```bash
dotnet run -- ask \
  -q "Your question here" \
  --endpoint "https://api.ollama.com" \
  --api-key "your-api-key" \
  --endpoint-type "ollama-cloud"
```

### Endpoint Types

| Type | Description | Auto-Detection |
|------|-------------|----------------|
| `auto` | Automatically detect endpoint type from URL (default) | ✅ |
| `ollama-cloud` | Ollama Cloud native format (`/api/generate`) | URLs containing `api.ollama.com` or `ollama.cloud` |
| `openai` | OpenAI-compatible format (`/v1/responses`) | All other URLs |

### Usage Examples

#### Basic Chat with Ollama Cloud
```bash
# Using environment variables
export CHAT_ENDPOINT="https://api.ollama.com"
export CHAT_API_KEY="your-ollama-cloud-key"
dotnet run -- ask -q "Explain monads in functional programming"

# Using CLI flags
dotnet run -- ask -q "Hello world" \
  --endpoint "https://api.ollama.com" \
  --api-key "your-key" \
  --model "llama3.2"
```

#### RAG with Remote Models
```bash
# Enable RAG with Ollama Cloud
dotnet run -- ask -q "What does the code do?" --rag \
  --endpoint "https://api.ollama.com" \
  --api-key "your-key"
```

#### Agent Mode with Tools
```bash
# Use agent with Ollama Cloud backend
dotnet run -- ask -q "Analyze this repository structure" --agent \
  --endpoint "https://api.ollama.com" \
  --api-key "your-key"
```

#### AI Reasoning Pipeline with Refinement Loop
```bash
# Run complete refinement workflow: Draft -> Critique -> Improve
dotnet run -- pipeline -d "SetTopic('microservices architecture') | UseRefinementLoop('2')"

# The refinement loop automatically:
# 1. Creates an initial draft (if none exists)
# 2. Critiques the draft to identify gaps and issues
# 3. Improves the draft based on critique
# 4. Repeats the critique-improve cycle for the specified iterations

# You can also run individual steps:
dotnet run -- pipeline -d "SetTopic('AI Safety') | UseDraft | UseCritique | UseImprove"
```

## 📚 Documentation

- **[Architecture Summary](ARCHITECTURE_SUMMARY.md)**: High-level architectural overview
- **[Architectural Review](ARCHITECTURAL_REVIEW.md)**: Detailed technical analysis
- **[Memory Integration](MEMORY_INTEGRATION.md)**: Conversation memory strategies
- **[Development Roadmap](ROADMAP.md)**: Project roadmap and milestones
- **[Work Items](WORK_ITEMS.md)**: Development tasks and backlog

## 🛠️ Development

### Tool System
Create custom tools by implementing `ITool`:

```csharp
public class CustomTool : ITool
{
    public string Name => "custom_tool";
    public string Description => "Performs custom analysis";
    
    public async Task<ToolExecution> ExecuteAsync(ToolArgs args)
    {
        // Implementation
        return new ToolExecution(Name, args, result);
    }
}
```

### Testing
The project includes comprehensive tests that run as part of the main demonstration:
```bash
dotnet run
```
Custom test framework with tests integrated into the main program execution.

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/amazing-feature`
3. Follow the functional programming patterns established in the codebase
4. Use monadic error handling consistently
5. Add comprehensive tests for new functionality
6. Submit a pull request

### Coding Standards
- **Functional First**: Prefer pure functions and immutable data structures
- **Monadic Composition**: Use `Result<T>` and `Option<T>` for error handling
- **Type Safety**: Leverage the C# type system fully
- **Documentation**: Include XML documentation for all public APIs

## 📋 Requirements

- **Runtime**: .NET 8.0+
- **LangChain**: 0.17.0
- **System.Reactive**: 6.0.2

## 🔮 Roadmap

See [ROADMAP.md](ROADMAP.md) for detailed development plans including:
- **Phase 1**: Production Readiness (Persistence, Testing, Configuration)
- **Phase 2**: Operations & Scale (Observability, Performance, Security)  
- **Phase 3**: Strategic Capabilities (Advanced Features, Developer Tooling)

## ⚖️ License

This project is open source. Please check the repository for license details.

## 🙏 Acknowledgments

- Built on [LangChain](https://github.com/tryAGI/LangChain) for AI/LLM integration
- Inspired by category theory and functional programming principles
- Special thanks to the functional programming community for mathematical foundations

---

**MonadicPipeline**: Where Category Theory Meets AI Pipeline Engineering 🚀