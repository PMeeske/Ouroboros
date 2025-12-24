> [!NOTE]
> **⚠️ Personal Learning Project**
> 
> This is an experimental side project built in my spare time to explore functional programming, category theory, and AI orchestration patterns. Code quality varies — some parts are polished, others are rough drafts. Use at your own risk, contributions welcome, here be dragons. 🐉
> 
> *Built with curiosity, caffeine, and Claude on my phone.* 📱

<p align="center">
  <img src="assets/ouroboros-logo.svg" alt="Ouroboros Logo" width="200"/>
</p>

<h1 align="center">Ouroboros</h1>

<p align="center">
  <em>The self-consuming serpent of AI orchestration</em>
</p>

<p align="center">
  <a href="#features">Features</a> •
  <a href="#installation">Installation</a> •
  <a href="#quick-start">Quick Start</a> •
  <a href="#architecture">Architecture</a> •
  <a href="#contributing">Contributing</a>
</p>

---

## Overview

Ouroboros is a sophisticated **functional programming-based AI pipeline system** built on C# and LangChain. The project implements category theory principles, monadic composition, and functional programming patterns to create type-safe, composable AI workflows. Named after the ancient symbol of a serpent eating its own tail, this framework explores how AI systems can introspect, modify, and enhance their own behavior through recursive self-improvement patterns.

**Built with:** C# 14.0+, .NET 10.0, LangChain, Category Theory, Functional Programming

## Features

- 🔄 **Monadic Pipeline Composition** - Type-safe error handling with `Result<T>` and `Option<T>` monads
- 🧮 **Category Theory Abstractions** - Kleisli arrows, functors, and natural transformations
- 🤖 **Multi-Agent Reasoning** - Draft → Critique → Improve workflow with event sourcing
- 🧠 **AI Orchestration** - Smart model selection, use case classification, and performance optimization
- 📊 **Observable Execution** - Full tracing, replay capabilities, and branch management
- 🔌 **Extensible Tool System** - Composable tools with automatic schema generation
- 🎯 **Vector Search Integration** - RAG capabilities with multiple vector store backends
- 🏗️ **Production Ready** - Kubernetes deployment, Docker support, comprehensive testing

## Installation

```bash
# Clone the repository
git clone https://github.com/PMeeske/Ouroboros.git
cd Ouroboros

# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run tests
dotnet test
```

## Quick Start

```csharp
using LangChainPipeline.Pipeline.Branches;
using LangChainPipeline.Pipeline.Reasoning;
using LangChainPipeline.Tools;
using LangChainPipeline.Providers;

// Create a pipeline branch with vector store
var store = new TrackedVectorStore();
var branch = new PipelineBranch("demo", store, DataSource.FromPath("."));

// Initialize LLM and tools
var llm = new OllamaChatModel("llama3");
var tools = new ToolRegistry();
var embed = new OllamaEmbeddingModel();

// Create a reasoning pipeline: Draft → Critique → Improve
var draftArrow = ReasoningArrows.DraftArrow(llm, tools, "What is functional programming?");
var critiqueArrow = ReasoningArrows.CritiqueArrow(llm, tools, embed, "analysis", "critique");
var improveArrow = ReasoningArrows.ImproveArrow(llm, tools);

// Execute the pipeline with monadic composition
var result = await draftArrow(branch);
result = await critiqueArrow(result);
result = await improveArrow(result);

// Access the final reasoning state
var finalSpec = result.GetMostRecentReasoning() as FinalSpec;
Console.WriteLine($"Result: {finalSpec?.Text}");
```

For more examples, see [QUICKSTART.md](QUICKSTART.md) and [examples/](examples/).

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                        Ouroboros                             │
│                                                               │
│  ┌────────────────────────────────────────────────────────┐ │
│  │              AI Orchestration Layer                    │ │
│  │  SmartModelOrchestrator → UseCaseClassifier           │ │
│  │  PerformanceTracking → ConfidenceRouting              │ │
│  └────────────────────────────────────────────────────────┘ │
│                             ↓                                 │
│  ┌────────────────────────────────────────────────────────┐ │
│  │           Reasoning Pipeline (Monadic)                │ │
│  │  Draft → Critique → Improve → Verify                  │ │
│  │  (PipelineBranch + Event Sourcing)                    │ │
│  └────────────────────────────────────────────────────────┘ │
│                             ↓                                 │
│  ┌────────────────────────────────────────────────────────┐ │
│  │        Functional Abstractions (Category Theory)      │ │
│  │  Result<T> → Option<T> → Step<TIn,TOut>              │ │
│  │  Kleisli Arrows → Monadic Composition                 │ │
│  └────────────────────────────────────────────────────────┘ │
│                             ↓                                 │
│  ┌────────────────────────────────────────────────────────┐ │
│  │              Integration Layer                         │ │
│  │  LangChain → Vector Stores → Tool Registry            │ │
│  │  Ollama/OpenAI → Qdrant/Pinecone → Custom Tools       │ │
│  └────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────┘
```

## Contributing

Contributions are welcome! This is a learning project, so expect rough edges. Please see:

- **[Contributing Guide](CONTRIBUTING.md)** - Development setup, coding standards, and PR guidelines
- **[Quick Start Guide](QUICKSTART.md)** - Get running in 5 minutes
- **[Architecture Documentation](docs/ARCHITECTURE.md)** - Detailed system architecture
- **[Deployment Guide](DEPLOYMENT.md)** - Production deployment instructions

Feel free to:
- Open issues for bugs or ideas
- Submit PRs for improvements
- Share feedback on the architecture
- Join discussions on design patterns

## Documentation

- 📖 **[Full Documentation](docs/README.md)** - Complete technical documentation
- 🚀 **[Quick Start](QUICKSTART.md)** - 5-minute getting started guide
- 🏗️ **[Architecture](docs/ARCHITECTURE.md)** - System design and patterns
- 📦 **[Deployment](DEPLOYMENT.md)** - Docker, Kubernetes, and cloud deployment
- 🔧 **[Troubleshooting](TROUBLESHOOTING.md)** - Common issues and solutions
- 🧪 **[Test Coverage](TEST_COVERAGE_REPORT.md)** - Testing strategy and metrics

## License

MIT © 2024 PMeeske
