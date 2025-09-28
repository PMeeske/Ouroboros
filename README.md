# MonadicPipeline

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)](https://github.com/PMeeske/MonadicPipeline)
[![.NET Version](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![LangChain](https://img.shields.io/badge/LangChain-0.17.0-purple)](https://www.nuget.org/packages/LangChain/)

A **sophisticated functional programming-based AI pipeline system** built on LangChain, implementing category theory principles, monadic composition, and functional programming patterns to create type-safe, composable AI workflows.

## 🚀 Key Features

- **🧮 Monadic Composition**: Type-safe pipeline operations using `Result<T>` and `Option<T>` monads
- **🔗 Kleisli Arrows**: Mathematical composition of computations in monadic contexts  
- **🤖 LangChain Integration**: Native integration with LangChain providers and tools
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

Run all examples:
```bash
dotnet run
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