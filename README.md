# MonadicPipeline

[![Build Status](https://img.shields.io/badge/build-passing-brightgreen)](https://github.com/PMeeske/MonadicPipeline)
[![.NET Version](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![LangChain](https://img.shields.io/badge/LangChain-0.17.0-purple)](https://www.nuget.org/packages/LangChain/)
[![Coverage](https://img.shields.io/badge/coverage-8.4%25-yellow)](TEST_COVERAGE_REPORT.md)
[![Tests](https://img.shields.io/badge/tests-304%20passing-brightgreen)](src/MonadicPipeline.Tests)

A **sophisticated functional programming-based AI pipeline system** built on LangChain, implementing category theory principles, monadic composition, and functional programming patterns to create type-safe, composable AI workflows.

## 🚀 Key Features

- **🧮 Monadic Composition**: Type-safe pipeline operations using `Result<T>` and `Option<T>` monads
- **🔗 Kleisli Arrows**: Mathematical composition of computations in monadic contexts
- **🤖 LangChain Integration**: Native integration with LangChain providers and tools
- **⚡ LangChain Pipe Operators**: Familiar `Set | Retrieve | Template | LLM` syntax with monadic safety
- **🧠 Meta-AI Layer**: Pipeline steps exposed as tools - the LLM can invoke pipeline operations
- **🎯 AI Orchestrator**: Performance-aware model selection based on use case classification
- **🚀 Meta-AI Layer v2**: Planner/Executor/Verifier orchestrator with continual learning
- **🎓 Self-Improving Agents**: Automatic skill extraction and learning from successful executions
- **🧠 Enhanced Memory**: Persistent memory with consolidation and intelligent forgetting
- **📈 Uncertainty Routing**: Confidence-aware task routing with fallback strategies
- **🤖 Phase 2 Metacognition (NEW)**: Agent self-model, goal hierarchy, and autonomous self-evaluation
  - **Capability Registry**: Agent understands its own capabilities and limitations
  - **Goal Hierarchy**: Hierarchical goal decomposition with value alignment
  - **Self-Evaluator**: Autonomous performance assessment and improvement planning
- **🎯 Epic Branch Orchestration (NEW)**: Automated epic management with agent assignment and dedicated branches
  - **Auto Agent Assignment**: Each sub-issue gets its own dedicated agent
  - **Dedicated Branches**: Isolated work tracking with immutable pipeline branches
  - **Parallel Execution**: Concurrent sub-issue processing with Result monads
- **🔄 GitHub Copilot Development Loop (NEW)**: Automated development workflows powered by AI
  - **Automated Code Review**: AI-assisted PR reviews with functional programming pattern checks
  - **Issue Analysis**: Automatic issue classification and implementation guidance
  - **Continuous Improvement**: Weekly code quality analysis and optimization suggestions
- **✨ Convenience Layer**: Simplified one-liner methods for quick orchestrator setup
- **🔮 MeTTa Symbolic Reasoning**: Hybrid neural-symbolic AI with MeTTa integration
- **📊 Vector Database Support**: Built-in vector storage and retrieval capabilities
- **🔄 Event Sourcing**: Complete audit trail with replay functionality
- **🛠️ Extensible Tool System**: Plugin architecture for custom tools and functions
- **💾 Memory Management**: Multiple conversation memory strategies
- **📄 RecursiveChunkProcessor**: Process large contexts (100+ pages) with adaptive chunking and map-reduce
- **🎯 Type Safety**: Leverages C# type system for compile-time guarantees
- **☁️ IONOS Cloud Ready**: Optimized deployment for IONOS Cloud Kubernetes infrastructure

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

### Iterative Refinement Architecture

The reasoning pipeline implements a **sophisticated iterative refinement architecture** that enables true progressive enhancement across multiple critique-improve cycles:

```
Iteration 0:  Draft ──────────────────────────────────────┐
                │                                          │
Iteration 1:  Critique(Draft) → Improve → FinalSpec₁     │
                                              │            │
Iteration 2:  Critique(FinalSpec₁) → Improve → FinalSpec₂│
                                              │            │
Iteration N:  Critique(FinalSpec_{N-1}) → Improve → FinalSpec_N
```

**Key Architectural Features:**
- **State Chaining**: Each iteration uses `GetMostRecentReasoningState()` to build upon the previous improvement
- **Polymorphic States**: Both `Draft` and `FinalSpec` are `ReasoningState` instances that can be processed uniformly
- **Event Sourcing**: Complete immutable audit trail enables replay and analysis of the entire reasoning process
- **Monadic Composition**: `CritiqueArrow` and `ImproveArrow` compose as pure Kleisli arrows for type-safe pipelines

## 🔧 Getting Started

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- [Ollama](https://ollama.ai/) (for local LLM providers) or remote API access (optional)

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

4. **Run the guided setup (recommended for first-time users):**
   ```bash
   cd src/MonadicPipeline.CLI
   dotnet run -- setup --all
   ```

   The guided setup wizard will help you:
   - Install and configure Ollama for local LLM execution
   - Setup authentication for external providers (OpenAI, Ollama Cloud)
   - Install MeTTa symbolic reasoning engine (optional)
   - Configure local vector database (Qdrant)

   You can also run individual setup steps:
   ```bash
   dotnet run -- setup --ollama           # Install Ollama only
   dotnet run -- setup --auth             # Configure authentication
   dotnet run -- setup --metta            # Install MeTTa
   dotnet run -- setup --vector-store     # Setup vector database
   ```

5. **Try the examples:**
   ```bash
   cd src/MonadicPipeline.Examples
   dotnet run
   ```

### Quick Start

#### Command Line Interface

The CLI provides several commands for interacting with the pipeline system. All commands should be run from the `src/MonadicPipeline.CLI` directory:

```bash
# Navigate to CLI directory
cd src/MonadicPipeline.CLI

# Ask a question
dotnet run -- ask -q "What is functional programming?"

# Ask with RAG (retrieval augmented generation)
dotnet run -- ask -q "What does the code do?" --rag

# Run a pipeline with DSL
dotnet run -- pipeline -d "SetTopic('AI') | UseDraft | UseCritique | UseImprove"

# List available pipeline tokens
dotnet run -- list

# Explain a pipeline DSL
dotnet run -- explain -d "SetTopic('test') | UseDraft"

# Run tests
dotnet run -- test --all

# Run smart model orchestrator
dotnet run -- orchestrator --goal "Explain functional programming"

# Run orchestrator with specific models
dotnet run -- orchestrator \
  --goal "Write a Python function for sorting" \
  --coder-model "codellama" \
  --reason-model "llama3"

# Run MeTTa orchestrator with symbolic reasoning
dotnet run -- metta --goal "Analyze data patterns and find insights"

# Run MeTTa orchestrator in plan-only mode
dotnet run -- metta --goal "Create a research plan" --plan-only
```

#### Orchestrating Complex Tasks with Small Models

MonadicPipeline supports **intelligent model orchestration** that allows you to efficiently handle complex tasks by combining multiple small, specialized models. This approach is more cost-effective and often faster than using a single large model.

**Key Features:**
- **Automatic Model Selection**: The `--router auto` flag intelligently routes sub-tasks to specialized models
- **Multi-Model Composition**: Combine general, coding, reasoning, and summarization models
- **Performance Tracking**: Use `--show-metrics` to monitor model usage and optimize selection

**Example: Complex Code Review with Small Models**

```bash
# Use multiple small models for different aspects of code review
dotnet run -- pipeline \
  -d "SetTopic('Code Review Best Practices') | UseDraft | UseCritique | UseImprove" \
  --router auto \
  --general-model phi3:mini \          # Fast general responses (2.3GB)
  --coder-model deepseek-coder:1.3b \  # Specialized code analysis (800MB)
  --reason-model qwen2.5:3b \          # Deep reasoning (2GB)
  --trace                              # See which model handles each step
```

**Example: Efficient Question Answering**

```bash
# The orchestrator selects the best small model for your task
dotnet run -- orchestrator \
  --goal "Explain monadic composition in functional programming" \
  --model phi3:mini \
  --show-metrics
```

**Recommended Small Model Combinations:**

1. **Balanced Setup** (5GB total):
   - `phi3:mini` - General purpose (2.3GB)
   - `qwen2.5:3b` - Complex reasoning (2GB)
   - `deepseek-coder:1.3b` - Code tasks (800MB)

2. **Ultra-Light Setup** (2.5GB total):
   - `tinyllama` - Quick responses (637MB)
   - `phi3:mini` - General tasks (2.3GB)

3. **Specialized Setup** (8GB total):
   - `llama3:8b` - Advanced reasoning (4.7GB)
   - `deepseek-coder:6.7b` - Professional coding (3.8GB)

Install recommended models:
```bash
ollama pull phi3:mini
ollama pull qwen2.5:3b
ollama pull deepseek-coder:1.3b
```

#### Web API (Kubernetes-Friendly Remoting)

The Web API provides REST endpoints for the same pipeline functionality, ideal for containerized and cloud-native deployments:

```bash
# Navigate to Web API directory
cd src/MonadicPipeline.WebApi

# Run locally
dotnet run

# Or use Docker
docker-compose up -d monadic-pipeline-webapi
```

Access the API at `http://localhost:8080` with Swagger UI at the root `/`.

**API Examples:**

```bash
# Ask a question
curl -X POST http://localhost:8080/api/ask \
  -H "Content-Type: application/json" \
  -d '{
    "question": "What is functional programming?",
    "useRag": false,
    "model": "llama3"
  }'

# Execute a pipeline
curl -X POST http://localhost:8080/api/pipeline \
  -H "Content-Type: application/json" \
  -d '{
    "dsl": "SetTopic(\"AI\") | UseDraft | UseCritique",
    "model": "llama3"
  }'

# Health check (for Kubernetes)
curl http://localhost:8080/health
```

See [Web API Documentation](src/MonadicPipeline.WebApi/README.md) for more details.

#### Android App (Mobile CLI Interface)

MonadicPipeline is now available as an Android app with a terminal-style CLI interface and integrated Ollama support.

**Get the APK:**
- **Download:** APK is automatically built by CI/CD - download from [GitHub Actions artifacts](../../actions/workflows/android-build.yml)
- **Build locally:** Requires MAUI workload (see below)

```bash
# To build locally (requires: dotnet workload install maui-android)
cd src/MonadicPipeline.Android
dotnet build -c Release -f net8.0-android

# Install on connected device
dotnet build -c Release -f net8.0-android -t:Install
```

**Features:**
- ✅ **Terminal-Style UI**: Green-on-black terminal interface for mobile
- ✅ **Ollama Integration**: Connect to local or remote Ollama servers
- ✅ **Automatic Model Management**: Models auto-unload after 5 minutes of inactivity
- ✅ **Small Model Optimization**: Recommended models (tinyllama, phi, qwen, gemma)
- ✅ **Efficiency Hints**: Built-in guidance for battery, network, and memory usage
- ✅ **Standalone Operation**: Download models as needed from Ollama

**Quick Start on Android:**
1. Download and install the APK from GitHub Actions artifacts
2. Launch the app
3. Configure Ollama endpoint: `config http://YOUR_SERVER_IP:11434`
4. Pull a small model on your server: `ollama pull tinyllama`
5. Ask questions: `ask What is functional programming?`

See [Android App Documentation](src/MonadicPipeline.Android/README.md) for complete instructions.


#### Smart Model Orchestrator

The orchestrator command provides intelligent model selection based on the task type:

```bash
# Basic orchestrator usage
dotnet run -- orchestrator --goal "Your task here"

# Configure models for different use cases
dotnet run -- orchestrator \
  --goal "Write and debug Python code" \
  --model "llama3" \                    # General model
  --coder-model "codellama" \           # For code tasks
  --reason-model "llama3" \             # For reasoning
  --metrics                             # Show performance metrics

# Available options:
# --goal           : Task to accomplish (required)
# --model          : Primary model (default: llama3)
# --coder-model    : Model for code tasks (default: codellama)
# --reason-model   : Model for reasoning tasks
# --metrics        : Display performance metrics (default: true)
# --debug          : Enable verbose logging
```

The orchestrator:
- Automatically classifies prompts by use case (code, reasoning, general)
- Selects optimal models based on task requirements
- Tracks performance metrics for continuous improvement
- Falls back gracefully when models are unavailable

#### MeTTa Symbolic Reasoning

The metta command uses hybrid neural-symbolic AI with MeTTa integration:

```bash
# Basic MeTTa orchestrator usage
dotnet run -- metta --goal "Your goal here"

# Plan without execution
dotnet run -- metta --goal "Research task" --plan-only

# With custom configuration
dotnet run -- metta \
  --goal "Analyze and synthesize information" \
  --model "llama3" \
  --metrics \
  --plan-only

# Available options:
# --goal           : Goal to plan and execute (required)
# --model          : LLM model to use (default: llama3)
# --embed          : Embedding model (default: nomic-embed-text)
# --plan-only      : Generate plan without execution
# --metrics        : Display performance metrics (default: true)
# --debug          : Enable verbose logging
```

The MeTTa orchestrator:
- Creates symbolic representations of plans and tools
- Uses symbolic reasoning for next-step selection
- Provides explainable AI through MeTTa symbolic queries
- Requires `metta` executable in PATH (install from [hyperon-experimental](https://github.com/trueagi-io/hyperon-experimental))

#### RecursiveChunkProcessor (Large Context Processing)

Process documents and contexts that exceed model limits using adaptive chunking:

```csharp
using LangChainPipeline.Core.Processing;
using LangChainPipeline.Core.Monads;

// Define how to process each chunk
Func<string, Task<Result<string>>> processChunk = async chunk =>
{
    var summary = await llm.SummarizeAsync(chunk);
    return Result<string>.Success(summary);
};

// Define how to combine results
Func<IEnumerable<string>, Task<Result<string>>> combineResults = async summaries =>
{
    var final = await llm.CombineSummariesAsync(summaries.ToList());
    return Result<string>.Success(final);
};

// Create processor
var processor = new RecursiveChunkProcessor(processChunk, combineResults);

// Process large document (100+ pages)
var result = await processor.ProcessLargeContextAsync<string, string>(
    largeDocument,
    maxChunkSize: 512,              // Adaptive chunk sizing
    strategy: ChunkingStrategy.Adaptive  // Learns optimal size
);
```

**Features:**
- **Adaptive Chunking**: Learns optimal chunk sizes over time
- **Map-Reduce Pattern**: Parallel processing for performance
- **Token-Aware Splitting**: Respects semantic boundaries
- **Hierarchical Joining**: Combines results intelligently

**Use Cases:**
- 📄 Long document summarization (100+ pages)
- 💻 Large codebase analysis
- 📚 Multi-document Q&A
- 🔍 Research paper synthesis

See [RecursiveChunking Documentation](docs/RECURSIVE_CHUNKING.md) for detailed guide and examples.


#### Using Remote Endpoints (Ollama Cloud, OpenAI)

Configure remote AI endpoints via environment variables or CLI flags:

```bash
# Navigate to CLI directory
cd src/MonadicPipeline.CLI

# Set environment variables
export CHAT_ENDPOINT="https://api.ollama.com"
export CHAT_API_KEY="your-api-key"

# Or use CLI flags
dotnet run -- ask -q "Hello" \
  --endpoint "https://api.ollama.com" \
  --api-key "your-key"
```

#### Programmatic Usage - Convenience Layer

```csharp
using LangChain.Providers.Ollama;
using LangChainPipeline.Agent.MetaAI;

// Create a chat assistant in one line
var provider = new OllamaProvider();
var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
var orchestrator = MetaAIConvenience.CreateChatAssistant(chatModel).Value;

// Ask a question and get an answer
var result = await orchestrator.AskQuestion("What is functional programming?");

result.Match(
    answer => Console.WriteLine($"Answer: {answer}"),
    error => Console.WriteLine($"Error: {error}"));
```

#### Programmatic Usage - Core Pipeline Composition

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
src/
├── MonadicPipeline.Core/        # Monadic abstractions and core functionality
│   ├── Conversation/            # Conversational pipeline builders
│   ├── Kleisli/                 # Category theory implementation
│   ├── Memory/                  # Memory management for conversations
│   ├── Monads/                  # Option and Result monad implementations
│   └── Steps/                   # Pipeline step abstractions
├── MonadicPipeline.Domain/      # Domain models and business logic
│   ├── Events/                  # Event sourcing patterns
│   ├── States/                  # State management
│   └── Vectors/                 # Vector database abstractions
├── MonadicPipeline.Pipeline/    # Pipeline implementation layers
│   ├── Branches/                # Branch management and persistence
│   ├── Ingestion/               # Data ingestion pipelines
│   ├── Reasoning/               # AI reasoning workflows
│   └── Replay/                  # Execution replay functionality
├── MonadicPipeline.Tools/       # Extensible tool system
│   └── MeTTa/                   # MeTTa symbolic reasoning integration
├── MonadicPipeline.Providers/   # External service providers
├── MonadicPipeline.Agent/       # AI orchestration and meta-AI
├── MonadicPipeline.CLI/         # Command-line interface
├── MonadicPipeline.WebApi/      # REST API for containerized deployments
├── MonadicPipeline.Android/     # Android app with terminal CLI interface
├── MonadicPipeline.Examples/    # Comprehensive examples
└── MonadicPipeline.Tests/       # Test suite
```

## 🎯 Examples

The `src/MonadicPipeline.Examples/Examples/` directory contains comprehensive demonstrations:

- **`MonadicExamples.cs`**: Core monadic operations
- **`ConversationalKleisliExamples.cs`**: Memory-integrated conversations
- **`HybridStepExamples.cs`**: Sync/async step combinations
- **`FunctionalReasoningExamples.cs`**: AI reasoning workflows
- **`LangChainPipeOperatorsExample.cs`**: LangChain-style pipe operators
- **`OrchestratorExample.cs`**: AI orchestrator with intelligent model selection
- **`MeTTaIntegrationExample.cs`**: MeTTa symbolic reasoning
- **`MetaAIv2Example.cs`**: Meta-AI v2 with planning and learning
- **`ConvenienceLayerExamples.cs`**: Simplified convenience layer usage
- **`Epic120Example.cs`**: Epic workflow orchestration with automated agent assignment ⭐ **NEW**

Run all examples:
```bash
cd src/MonadicPipeline.Examples
dotnet run
```

### Epic Branch Orchestration ⭐ NEW

The **Epic Branch Orchestration** system enables automated management of GitHub epics with dedicated agent assignment and branch creation for each sub-issue. Perfect for coordinating large initiatives like Epic #120 (Production-ready Release v1.0).

**Key Features:**
- 🤖 **Automatic Agent Assignment**: Each sub-issue gets its own dedicated agent
- 🌿 **Dedicated Branches**: Isolated `PipelineBranch` instances for work tracking
- 📊 **Status Tracking**: Monitor progress through well-defined states
- ⚡ **Parallel Execution**: Work on multiple sub-issues concurrently
- 🛡️ **Robust Error Handling**: Result monads throughout for type-safe errors

**Quick Start:**
```csharp
var orchestrator = new EpicBranchOrchestrator(distributor, config);
await orchestrator.RegisterEpicAsync(120, title, description, subIssues);
await orchestrator.ExecuteSubIssueAsync(120, 121, workFunc);
```

**Documentation:**
- 📘 [API Reference](docs/EpicBranchOrchestration.md) - Complete API documentation
- 📗 [Integration Guide](docs/Epic120Integration.md) - Practical usage patterns
- 📙 [Implementation Summary](docs/ImplementationSummary.md) - Architecture overview
- 💻 [Example Code](src/MonadicPipeline.Examples/Examples/Epic120Example.cs) - Working example

## 🔗 Key Features Details

### LangChain Pipe Operators

MonadicPipeline supports **LangChain's familiar pipe operator syntax** while maintaining functional programming guarantees:

```bash
# Navigate to CLI directory
cd src/MonadicPipeline.CLI

# CLI DSL usage - RAG pipeline
dotnet run -- pipeline --dsl "SetQuery('What is AI?') | Retrieve | Template | LLM"
```

Code-based composition:

```csharp
using static LangChainPipeline.Core.Interop.Pipe;

var pipeline = Set("Who was drinking unicorn blood?", "query")
    .Bind(RetrieveSimilarDocuments(5))
    .Bind(CombineDocuments())
    .Bind(Template("Use context: {context}\nQuestion: {question}\nAnswer:"))
    .Bind(LLM());
```

### Meta-AI Layer - Self-Reflective Pipelines

Pipeline steps are automatically registered as tools that the LLM can invoke:

```bash
# Navigate to CLI directory
cd src/MonadicPipeline.CLI

# The LLM can use pipeline tools to improve its own output
dotnet run -- pipeline --dsl "SetPrompt('Explain functional programming') | UseDraft | UseCritique | UseImprove"
```

Available pipeline tools:
- `run_usedraft` - Generate initial draft response
- `run_usecritique` - Critique current draft
- `run_useimprove` - Improve draft based on critique
- `run_retrieve` - Semantic search over documents
- Many more...

### AI Reasoning Pipeline with Refinement Loop

The refinement loop implements an **iterative refinement architecture** where each iteration builds upon the previous improvement, creating a true progressive enhancement cycle.

```bash
# Navigate to CLI directory
cd src/MonadicPipeline.CLI

# Run complete refinement workflow: Draft -> Critique -> Improve
dotnet run -- pipeline -d "SetTopic('microservices architecture') | UseRefinementLoop('2')"

# The refinement loop architecture works as follows:
# 1. Creates an initial draft (if none exists)
# 2. First iteration: Critiques the draft → produces first improvement
# 3. Second iteration: Critiques the first improvement → produces second improvement
# 4. Each iteration uses the most recent reasoning state (Draft or FinalSpec) as the baseline
#    This creates true iterative refinement where each cycle builds on previous improvements

# You can also run individual steps:
dotnet run -- pipeline -d "SetTopic('AI Safety') | UseDraft | UseCritique | UseImprove"
```

**Architecture Highlights:**
- **Iterative State Chaining**: Each critique-improve cycle uses the most recent state as input
- **Progressive Refinement**: Improvements compound across iterations rather than restarting from the original draft
- **Monadic Error Handling**: Safe arrows (SafeCritiqueArrow, SafeImproveArrow) provide comprehensive error handling
- **Event Sourcing**: Complete audit trail of all reasoning steps for replay and analysis

### MeTTa Symbolic Reasoning

Combine neural LLM reasoning with symbolic logic:

```csharp
// Create MeTTa engine (subprocess-based by default)
using var engine = new SubprocessMeTTaEngine();

// Add symbolic facts
await engine.AddFactAsync("(human Socrates)");
await engine.AddFactAsync("(mortal $x) :- (human $x)");

// Execute symbolic query
var result = await engine.ExecuteQueryAsync("!(match &self (mortal Socrates) $result)");
```

Register MeTTa tools with the orchestrator:

```csharp
var tools = ToolRegistry.CreateDefault()
    .WithMeTTaTools();  // Subprocess-based engine

// MeTTa tools are now available to the LLM
var llm = new ToolAwareChatModel(chatModel, tools);
```

### AI Orchestrator

Intelligent model selection based on use case classification:

```csharp
// Build orchestrator with multiple specialized models
var orchestrator = new OrchestratorBuilder(tools, "general")
    .WithModel("general", generalModel, ModelType.General,
        new[] { "conversation", "general-purpose" })
    .WithModel("coder", codeModel, ModelType.Code,
        new[] { "code", "programming", "debugging" })
    .WithModel("reasoner", reasoningModel, ModelType.Reasoning,
        new[] { "reasoning", "analysis", "logic" })
    .WithMetricTracking(true)
    .Build();

// Automatically routes to best model
var response = await orchestrator.GenerateTextAsync(
    "Write a function to calculate factorial");
// → Automatically selects 'coder' model
```

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

#### Built-in Tools

MonadicPipeline includes several built-in tools:

- **MathTool**: Arithmetic expression evaluation
- **RetrievalTool**: Semantic search over ingested documents
- **GitHubScopeLockTool**: Formal scope locking mechanism to prevent scope creep
  - Adds `scope-locked` label to GitHub issues
  - Posts confirmation comments
  - Updates milestones
  - Integrates with Epic Branch Orchestrator for release planning

See [SCOPE_LOCK_GUIDE.md](docs/SCOPE_LOCK_GUIDE.md) for detailed documentation on scope locking.

### Testing

MonadicPipeline has a comprehensive test suite with 224 passing tests covering core functionality, domain models, security, and performance.

#### Run Tests

```bash
# Run all unit tests (mocked, without Ollama)
dotnet test

# Run specific test class
dotnet test --filter "FullyQualifiedName~InputValidatorTests"

# Run with detailed output
dotnet test --verbosity detailed

# Run CLI integration tests
cd src/MonadicPipeline.CLI
dotnet run -- test --all
```

#### Integration Tests

End-to-end integration tests with Ollama are automated via GitHub Actions (`.github/workflows/ollama-integration-test.yml`):

- ✅ **Basic Ollama connectivity** - Validates LLM model communication
- ✅ **Pipeline DSL execution** - Tests composable pipeline steps
- ✅ **Reverse engineering workflow** - Memory-efficient configuration testing
- ✅ **RAG with embeddings** - Vector search and retrieval testing

**Run locally** (requires Ollama installed):
```bash
# Ensure Ollama is running and models are pulled
ollama pull llama3:8b
ollama pull nomic-embed-text

# Run integration tests manually
cd src/MonadicPipeline.CLI
dotnet run -- ask -q "Test question" --model "llama3:8b"
dotnet run -- pipeline --dsl "SetPrompt('test') | UseDraft" --model "llama3:8b"
```

#### Code Coverage

[![Line Coverage](https://img.shields.io/badge/coverage-8.4%25-yellow)](TEST_COVERAGE_REPORT.md)
[![Branch Coverage](https://img.shields.io/badge/branch--coverage-6.2%25-red)](TEST_COVERAGE_REPORT.md)
[![Tests](https://img.shields.io/badge/tests-224%20passing-brightgreen)](src/MonadicPipeline.Tests)

**Current Coverage Summary:**
- **Line Coverage:** 8.4% (1,134 of 13,465 lines)
- **Branch Coverage:** 6.2% (219 of 3,490 branches)
- **Test Execution:** ~480ms, all passing

**Well-Tested Components (>80% coverage):**
- ✅ Domain Model: 80.1% (Event Store, Vector Store, Reasoning States)
- ✅ Security: 100% (Input Validation, Sanitization)
- ✅ Performance: 96-100% (Object Pooling, Performance Utilities)
- ✅ Diagnostics: 99%+ (Metrics, Distributed Tracing)

```bash
# Generate coverage report
./scripts/run-coverage.sh

# Or manually
dotnet test --collect:"XPlat Code Coverage"
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"TestCoverageReport" -reporttypes:"Html"
```

**Coverage Documentation:**
- 📊 [Full Coverage Report](TEST_COVERAGE_REPORT.md) - Detailed analysis and recommendations
- 📋 [Quick Reference](TEST_COVERAGE_QUICKREF.md) - Commands and current metrics
- 🔄 CI/CD: Automated coverage reporting via GitHub Actions

#### Mutation Testing

Mutation testing is powered by [Stryker.NET](https://stryker-mutator.io). A local dotnet tool manifest (`.config/dotnet-tools.json`) pins the `dotnet-stryker` version and a repository-wide configuration (`stryker-config.json`) defines reporters, thresholds, and mutation filters.

```powershell
# PowerShell
./scripts/run-mutation-tests.ps1 -OpenReport

# Bash / WSL / macOS
./scripts/run-mutation-tests.sh
```

Both helpers restore local tools and execute `dotnet stryker --config-file stryker-config.json`. The PowerShell variant can optionally open the latest HTML report (`StrykerOutput/<timestamp>/reports/mutation-report.html`).

Additional details and CI guidance are available in [TEST_MUTATION_GUIDE.md](TEST_MUTATION_GUIDE.md).

## 🔄 GitHub Copilot Development Loop

MonadicPipeline features an **automatic development loop** powered by GitHub Copilot that provides:

### 🤖 Automated Development Cycle ⭐ **NEW**

Fully automated code improvement workflow:
- 🔄 Runs twice daily (9 AM and 5 PM UTC)
- 📊 Maintains max 5 open copilot PRs
- 🔍 Analyzes codebase for improvements
- 📝 Auto-generates prioritized improvement tasks
- 🎭 Uses Playwright to assign @copilot via GitHub UI
- 🖼️ Captures screenshots for debugging
- 🔄 Falls back to API if UI automation fails
- 🚀 Triggers new cycle when PRs are merged

**Features**:
- TODO/FIXME resolution
- Documentation gap filling
- Test coverage improvement
- Error handling modernization (Result<T> monads)
- Async/await pattern fixes

### Automated Code Review

Every pull request automatically receives AI-assisted code review:
- ✅ Functional programming pattern checks
- ✅ Monadic error handling validation
- ✅ Documentation completeness review
- ✅ Async/await pattern analysis
- ✅ Architectural convention verification

### Issue Analysis Assistant

When you create an issue, Copilot automatically:
- 🔍 Classifies the issue type (bug, feature, test, docs, refactor)
- 📁 Finds relevant files in the codebase
- 💡 Suggests implementation approaches
- 📋 Provides step-by-step guidance
- 👤 Auto-assigns @copilot for analysis ⭐ **NEW**

**Usage**: Add the `copilot-assist` label or mention `@copilot` in comments

### Continuous Improvement

Weekly automated analysis provides:
- 📊 Code quality metrics and trends
- 🧪 Test coverage analysis
- 🔒 Security pattern review
- 🏗️ Architectural recommendations
- 📋 Actionable improvement tasks
- 👤 Auto-assigns @copilot to quality reports ⭐ **NEW**

**Schedule**: Runs every Monday at 9 AM UTC

### Documentation

See these guides for complete documentation:
- [GitHub Copilot Development Loop Guide](docs/COPILOT_DEVELOPMENT_LOOP.md)
- [Automated Development Cycle](docs/AUTOMATED_DEVELOPMENT_CYCLE.md)
- [Playwright Copilot Assignment](docs/PLAYWRIGHT_COPILOT_ASSIGNMENT.md) 🎭 **NEW**

---

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

See [`.github/copilot-instructions.md`](.github/copilot-instructions.md) for detailed coding guidelines.

**💡 Tip**: GitHub Copilot will automatically review your PRs and provide suggestions!

## 📋 Requirements

- **Runtime**: .NET 8.0+
- **LangChain**: 0.17.0
- **Ollama** (optional): For local LLM providers

## 🚀 Deployment

MonadicPipeline supports multiple deployment options for various environments:

### Docker Deployment

```bash
# Build Docker image
docker build -t monadic-pipeline:latest .

# Run with Docker Compose (includes Ollama, Qdrant, Jaeger)
docker-compose up -d

# Or use the automated script
./scripts/deploy-docker.sh production
```

### Kubernetes Deployment

**Local Kubernetes** (Docker Desktop, minikube, kind):
```bash
# Automated deployment - builds and loads images automatically
./scripts/deploy-k8s.sh monadic-pipeline
```

**Azure AKS** with Azure Container Registry:
```bash
# Automated deployment - builds, pushes to ACR, and deploys
./scripts/deploy-aks.sh myregistry monadic-pipeline
```

**AWS EKS, GCP GKE, or Docker Hub**:
```bash
# AWS EKS with ECR
./scripts/deploy-cloud.sh 123456789.dkr.ecr.us-east-1.amazonaws.com

# GCP GKE with GCR
./scripts/deploy-cloud.sh gcr.io/my-project

# Docker Hub
./scripts/deploy-cloud.sh docker.io/myusername
```

**IONOS Cloud** (recommended by Adaptive Systems Inc.):

**Infrastructure as Code (NEW)**:
```bash
# Provision infrastructure with Terraform
./scripts/manage-infrastructure.sh apply production

# Get kubeconfig
./scripts/manage-infrastructure.sh kubeconfig production

# Deploy application
./scripts/deploy-ionos.sh monadic-pipeline
```

**Quick deployment** (assumes infrastructure exists):
```bash
# Automated deployment to IONOS Cloud Kubernetes
# Includes registry authentication, image push, and deployment
./scripts/deploy-ionos.sh monadic-pipeline

# Or with environment variables
export IONOS_USERNAME="your-username"
export IONOS_PASSWORD="your-password"
./scripts/deploy-ionos.sh

# See docs/IONOS_DEPLOYMENT_GUIDE.md for detailed instructions
```

**Infrastructure Management**:
- **Quick Start**: [IONOS IaC Quick Start](docs/IONOS_IAC_QUICKSTART.md)
- **Full Guide**: [IONOS IaC Guide](docs/IONOS_IAC_GUIDE.md)
- **Terraform Docs**: [terraform/README.md](terraform/README.md)
- **Incident Runbook**: [Infrastructure Runbook](docs/INFRASTRUCTURE_RUNBOOK.md) ⚡ NEW

**Key Features**:
- ✅ **Automated infrastructure provisioning** via Terraform
- ✅ **Multi-environment support** (dev/staging/production)
- ✅ **Infrastructure as Code** - version controlled and reproducible
- ✅ **Cost optimization** - right-sized resources per environment
- ✅ **Disaster recovery** - infrastructure can be recreated anytime

**IONOS Cloud with GitHub Actions** (CI/CD):

Automated deployment via GitHub Actions is configured in `.github/workflows/ionos-deploy.yml`. Every push to `main` automatically:
1. Runs tests
2. Builds and pushes Docker images to IONOS Container Registry
3. Deploys to IONOS Kubernetes cluster

Setup required secrets in GitHub repository settings:
- `IONOS_REGISTRY_USERNAME`: IONOS Container Registry username
- `IONOS_REGISTRY_PASSWORD`: IONOS Container Registry password
- `IONOS_KUBECONFIG`: Base64-encoded kubeconfig file

See [IONOS Deployment Guide](docs/IONOS_DEPLOYMENT_GUIDE.md#cicd-with-github-actions) for detailed setup.

**Manual deployment**:
```bash
# Or manually
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/secrets.yaml
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/ollama.yaml
kubectl apply -f k8s/qdrant.yaml
kubectl apply -f k8s/deployment.yaml
kubectl apply -f k8s/webapi-deployment.yaml
```

### Local/Systemd Deployment

```bash
# Publish application
./scripts/deploy-local.sh /opt/monadic-pipeline

# Install as systemd service (Linux)
sudo cp scripts/monadic-pipeline.service /etc/systemd/system/
sudo systemctl enable monadic-pipeline
sudo systemctl start monadic-pipeline
```

For comprehensive deployment instructions including configuration management, monitoring, security, and troubleshooting, see:
- [**Deployment Guide**](DEPLOYMENT.md) - Complete deployment instructions
- [**Troubleshooting Guide**](TROUBLESHOOTING.md) - Common issues and solutions (including ImagePullBackOff fixes)
- [**Scripts README**](scripts/README.md) - Deployment scripts documentation

## 🏗️ Infrastructure & Dependencies

MonadicPipeline has comprehensive infrastructure documentation covering the complete stack from C# application to Terraform provisioning:

### Infrastructure Documentation

- [**Infrastructure Dependencies**](docs/INFRASTRUCTURE_DEPENDENCIES.md) - Complete mapping of dependencies across C#, Kubernetes, and Terraform
- [**Terraform-Kubernetes Integration**](docs/TERRAFORM_K8S_INTEGRATION.md) - Integration patterns, workflows, and automation
- [**Environment Infrastructure Mapping**](docs/ENVIRONMENT_INFRASTRUCTURE_MAPPING.md) - Environment-specific configurations (dev, staging, production)
- [**Deployment Topology**](docs/DEPLOYMENT_TOPOLOGY.md) - Visual topological representations of the complete infrastructure
- [**Infrastructure Migration Guide**](docs/INFRASTRUCTURE_MIGRATION_GUIDE.md) - Safe migration and change management procedures

### Infrastructure Validation

Before deploying or making infrastructure changes, validate your setup:

```bash
# Comprehensive infrastructure validation
./scripts/validate-infrastructure-dependencies.sh

# Checks:
# ✓ Terraform configuration
# ✓ Kubernetes manifests
# ✓ C# application configuration
# ✓ Configuration consistency
# ✓ Docker files
# ✓ Resource requirements
# ✓ Storage configuration
# ✓ Network configuration
# ✓ Security configuration
# ✓ CI/CD workflows
```

### Key Infrastructure Dependencies

The system has well-documented dependencies across layers:

```
C# Application (appsettings.json)
    ↓ Configuration
Kubernetes (ConfigMaps, Deployments, Services)
    ↓ Orchestration
Terraform (IONOS Cloud Infrastructure)
    ↓ Provisioning
IONOS Cloud (Data Center, K8s, Registry, Storage)
```

**Critical dependencies**:
- C# → Ollama service (LLM inference): `http://ollama-service:11434`
- C# → Qdrant service (vector storage): `http://qdrant-service:6333`
- K8s → Container Registry: `adaptive-systems.cr.de-fra.ionos.com`
- Terraform → K8s Cluster: Node sizing, storage volumes, networking

See [Infrastructure Dependencies](docs/INFRASTRUCTURE_DEPENDENCIES.md) for complete mapping.

### Infrastructure Summary

For a high-level overview of all infrastructure work:
- [**Infrastructure Refinement Summary**](docs/archive/INFRASTRUCTURE_REFINEMENT_SUMMARY.md) - Complete summary of infrastructure documentation and tools

## 🔧 Troubleshooting

### Kubernetes Image Pull Errors

If you encounter errors like:
```
Failed to pull image "monadic-pipeline-webapi:latest": failed to pull and unpack image
"docker.io/library/monadic-pipeline-webapi:latest": failed to resolve reference
"docker.io/library/monadic-pipeline-webapi:latest": pull access denied, repository does
not exist or may require authorization
```

Or `ImagePullBackOff` errors in your pods:
```bash
kubectl get events -n monadic-pipeline
# Error: ImagePullBackOff
```

**Solution**: The image doesn't exist in Docker Hub or your container registry. Use our automated deployment scripts:

**First, validate your setup:**
```bash
./scripts/validate-deployment.sh
```
This script checks your cluster type and provides specific guidance.

1. **For local clusters** (Docker Desktop, minikube, kind):
   ```bash
   ./scripts/deploy-k8s.sh
   ```
   The script automatically builds and loads images into your cluster.

2. **For Azure AKS with ACR**:
   ```bash
   ./scripts/deploy-aks.sh myregistry monadic-pipeline
   ```
   Automatically builds, pushes to ACR, and deploys to AKS.

3. **For IONOS Cloud**:
   ```bash
   ./scripts/deploy-ionos.sh monadic-pipeline

   # Check deployment status
   ./scripts/check-ionos-deployment.sh monadic-pipeline
   ```
   Automatically builds, pushes to IONOS registry, and deploys to IONOS Cloud.

4. **For AWS EKS, GCP GKE, or Docker Hub**:
   ```bash
   # AWS EKS
   ./scripts/deploy-cloud.sh 123456789.dkr.ecr.us-east-1.amazonaws.com

   # GCP GKE
   ./scripts/deploy-cloud.sh gcr.io/my-project

   # Docker Hub
   ./scripts/deploy-cloud.sh docker.io/myusername
   ```

**Detailed Troubleshooting**: See [TROUBLESHOOTING.md](TROUBLESHOOTING.md) for comprehensive troubleshooting, including ImagePullBackOff solutions.

**Historical Incident Reports**: See [docs/archive/](docs/archive/) for past incident post-mortems and resolutions.

## 📚 Documentation

### Essential Guides
- [**Deployment Guide**](DEPLOYMENT.md) - Comprehensive deployment instructions for all environments
- [**Deployment Quick Reference**](DEPLOYMENT-QUICK-REFERENCE.md) - Common deployment commands
- [**Troubleshooting Guide**](TROUBLESHOOTING.md) - Common issues and solutions
- [**Configuration and Security**](CONFIGURATION_AND_SECURITY.md) - Security best practices and configuration
- [**Test Coverage Report**](TEST_COVERAGE_REPORT.md) - Test coverage analysis

### Technical Documentation
- [**Documentation Index**](docs/README.md) - Complete documentation catalog
- [**Self-Improving Agent Architecture**](docs/SELF_IMPROVING_AGENT.md) - Agent capabilities and architecture
- [**IONOS Cloud Deployment**](docs/IONOS_DEPLOYMENT_GUIDE.md) - IONOS-specific deployment guide
- [**Infrastructure Dependencies**](docs/INFRASTRUCTURE_DEPENDENCIES.md) - Complete infrastructure dependency mapping
- [**RecursiveChunking Guide**](docs/RECURSIVE_CHUNKING.md) - Large context processing

### Developer Resources
- [**GitHub Copilot Instructions**](.github/copilot-instructions.md) - Development guidelines for contributors
- [**docs/ Directory**](docs/) - All technical documentation

## ⚖️ License

This project is open source. Please check the repository for license details.

## 🙏 Acknowledgments

- Built on [LangChain](https://github.com/tryAGI/LangChain) for AI/LLM integration
- Inspired by category theory and functional programming principles
- Special thanks to the functional programming community for mathematical foundations
- Developed by **Adaptive Systems Inc.** for enterprise AI pipeline solutions

---

**MonadicPipeline by Adaptive Systems Inc.**: Where Category Theory Meets AI Pipeline Engineering 🚀
