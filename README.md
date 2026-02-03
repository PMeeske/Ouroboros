<p align="center">
  <img src="assets/ouroboros-icon.svg" alt="Ouroboros Logo" width="180" height="180">
</p>

<h1 align="center">Ouroboros</h1>

<p align="center">
  <a href="https://github.com/PMeeske/Ouroboros/actions/workflows/dotnet-test-grid.yml"><img src="https://github.com/PMeeske/Ouroboros/actions/workflows/dotnet-test-grid.yml/badge.svg" alt="Test Grid"></a>
  <a href="https://github.com/PMeeske/Ouroboros/actions/workflows/mutation-testing.yml"><img src="https://github.com/PMeeske/Ouroboros/actions/workflows/mutation-testing.yml/badge.svg" alt="Mutation Testing"></a>
  <img src="https://img.shields.io/badge/tests-2432%20passing%2C%2039%20failing-red" alt="Tests">
  <img src="https://img.shields.io/badge/coverage-8.9%25-red" alt="Coverage">
  <a href="https://dotnet.microsoft.com/download/dotnet/10.0"><img src="https://img.shields.io/badge/.NET-10.0-blue" alt=".NET Version"></a>
  <a href="https://www.nuget.org/packages/LangChain/"><img src="https://img.shields.io/badge/LangChain-0.17.0-purple" alt="LangChain"></a>
</p>

A **sophisticated functional programming-based AI pipeline system** (YET EXPERIMENTAL) built on LangChain, implementing category theory principles, monadic composition, and functional programming patterns to create robust, self-improving AI agents.

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
- **🌐 Phase 2 — Integrated Self-Model (NEW)**: Persistent self-model with global workspace and predictive monitoring
  - **Identity Graph**: Tracks capabilities, resources, commitments, and performance metrics
  - **Global Workspace**: Shared working memory with attention-based priority management
  - **Cognitive Processing**: Global Workspace Theory integration with Pavlovian consciousness ([docs](docs/COGNITIVE_ARCHITECTURE.md))
  - **Predictive Monitoring**: Forecast tracking, calibration metrics, and anomaly detection
  - **Self-Explanation**: Generate narratives from execution DAG for transparency
  - **API Endpoints**: `/api/self/state`, `/api/self/forecast`, `/api/self/commitments`, `/api/self/explain`
  - **CLI Commands**: `self state`, `self forecast`, `self commitments`, `self explain`
- **🎯 Epic Branch Orchestration (NEW)**: Automated epic management with agent assignment and dedicated branches
  - **Auto Agent Assignment**: Each sub-issue gets its own dedicated agent
  - **Dedicated Branches**: Isolated work tracking with immutable pipeline branches
  - **Parallel Execution**: Concurrent sub-issue processing with Result monads
- **🔄 GitHub Copilot Development Loop (NEW)**: Automated development workflows powered by AI
  - **Automated Code Review**: AI-assisted PR reviews with functional programming pattern checks
  - **Issue Analysis**: Automatic issue classification and implementation guidance
  - **Continuous Improvement**: Weekly code quality analysis and optimization suggestions
- **🌟 Phase 0 — Evolution Foundations (NEW)**: Infrastructure for evolutionary metacognitive control
  - **Feature Flags**: Modular enablement of `embodiment`, `self_model`, and `affect` capabilities
  - **DAG Maintenance**: Snapshot integrity with SHA-256 hashing and retention policies
  - **Global Projection Service**: System-wide state observation with epoch snapshots and metrics
  - **CLI Commands**: `dag snapshot`, `dag show`, `dag replay`, `dag validate`, `dag retention`
- **✨ Convenience Layer**: Simplified one-liner methods for quick orchestrator setup
- **🔮 MeTTa Symbolic Reasoning**: Hybrid neural-symbolic AI with MeTTa integration
- **📊 Vector Database Support**: Built-in vector storage and retrieval capabilities
- **🔄 Event Sourcing**: Complete audit trail with replay functionality
- **🛠️ Extensible Tool System**: Plugin architecture for custom tools and functions
- **💾 Memory Management**: Multiple conversation memory strategies
- **📄 RecursiveChunkProcessor**: Process large contexts (100+ pages) with adaptive chunking and map-reduce
- **🎯 Type Safety**: Leverages C# type system for compile-time guarantees
- **☁️ IONOS Cloud Ready**: Optimized deployment for IONOS Cloud Kubernetes infrastructure
- **🤖 Multi-Provider Support**: Native support for Anthropic Claude, OpenAI, Ollama, GitHub Models, and more
- **💰 Cost Tracking**: Built-in LLM usage cost tracking with session summaries

## 🏗️ Architecture

Ouroboros follows a **Functional Pipeline Architecture** with monadic composition as its central organizing principle:

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Core Layer    │    │  Domain Layer   │    │ Pipeline Layer  │
│                 │    │                 │    │                 │
│ • Monads        │───▶│ • Events        │───▶│ • Branches      │
│ • Kleisli       │    │ • States        │    │ • Vectors       │
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

📘 **[View Detailed Architectural Layer Diagram](docs/ARCHITECTURAL_LAYERS.md)** - Comprehensive system architecture documentation including component responsibilities, data flow patterns, deployment topology, and cross-cutting concerns.

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

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) or later
- [Ollama](https://ollama.ai/) (for local LLM providers) or remote API access (optional)

### Installation

1. **Clone the repository:**
   ```bash
   git clone https://github.com/PMeeske/Ouroboros.git
   cd Ouroboros
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
   cd src/Ouroboros.CLI
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
   cd src/Ouroboros.Examples
   dotnet run
   ```

### Quick Start

#### Command Line Interface

The CLI provides several commands for interacting with the pipeline system. All commands should be run from the `src/Ouroboros.CLI` directory:

```bash
# Navigate to CLI directory
cd src/Ouroboros.CLI

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

# DAG Operations (Phase 0 — Evolution Foundations)
# Create snapshot of pipeline branches
dotnet run -- dag --command snapshot --branch main

# Show metrics and latest epoch
dotnet run -- dag --command show

# Replay snapshot from file
dotnet run -- dag --command replay --input snapshot.json

# Validate snapshot integrity
dotnet run -- dag --command validate

# Evaluate retention policy (dry run)
dotnet run -- dag --command retention --max-age-days 30 --dry-run
```

#### Orchestrating Complex Tasks with Small Models

Ouroboros supports **intelligent model orchestration** that allows you to efficiently handle complex tasks by combining multiple small, specialized models. This approach is more cost-effective and often faster than using a single massive model.

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
cd src/Ouroboros.WebApi

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

See [Web API Documentation](src/Ouroboros.WebApi/README.md) for more details.

#### Android App (Mobile CLI Interface)

Ouroboros is now available as an Android app with a terminal-style CLI interface and integrated Ollama support.

**Get the APK:**
- **Download:** APK is automatically built by CI/CD - download from [GitHub Actions artifacts](../../actions/workflows/android-build.yml)
- **Build locally:** Requires MAUI workload (see below)

```bash
# To build locally (requires: dotnet workload install maui-android)
cd src/Ouroboros.Android
dotnet build -c Release -f net10.0-android

# Install on connected device
dotnet build -c Release -f net10.0-android -t:Install
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
6. See [Android App Documentation](src/Ouroboros.Android/README.md) for complete instructions.

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
using Ouroboros.Core.Processing;
using Ouroboros.Core.Monads;

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


#### Using Remote Endpoints (Ollama Cloud, OpenAI, Anthropic, GitHub Models, LiteLLM)

Configure remote AI endpoints via environment variables or CLI flags. All CLI commands (ask, pipeline, orchestrator, metta) support remote endpoints with authentication:

```bash
# Navigate to CLI directory
cd src/Ouroboros.CLI

# Set environment variables (recommended)
export CHAT_ENDPOINT="https://api.ollama.com"
export CHAT_API_KEY="your-api-key"
export CHAT_ENDPOINT_TYPE="ollama-cloud"  # or "auto", "openai", "anthropic", "github-models", "litellm"

# Anthropic Claude example
export ANTHROPIC_API_KEY="sk-ant-api03-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
export CHAT_ENDPOINT="https://api.anthropic.com"
export CHAT_ENDPOINT_TYPE="anthropic"

# GitHub Models example
export GITHUB_TOKEN="ghp_xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx"
export CHAT_ENDPOINT="https://models.inference.ai.azure.com"
export CHAT_ENDPOINT_TYPE="github-models"

# Now run any command
dotnet run -- ask -q "Hello"
dotnet run -- orchestrator --goal "Explain monads"
dotnet run -- metta --goal "Plan a research task"

# Or use CLI flags (overrides environment variables)
dotnet run -- ask -q "Hello" \
  --endpoint "https://api.ollama.com" \
  --api-key "your-key" \
  --endpoint-type "ollama-cloud"

# Anthropic Claude with cost tracking
dotnet run -- ask -q "Explain monads" \
  --endpoint "https://api.anthropic.com" \
  --endpoint-type anthropic \
  --model claude-sonnet-4-20250514 \
  --show-costs
  
# GitHub Models with specific model
dotnet run -- ask -q "Explain functional programming" \
  --endpoint-type github-models \
  --model gpt-4o
```

#### Cost Tracking

Ouroboros includes built-in LLM cost tracking across all providers. Enable cost visibility with CLI flags:

```bash
# Show cost after each response
dotnet run -- ask -q "What is a monad?" --show-costs

# Enable cost-aware prompts (model considers token efficiency)
dotnet run -- ask -q "Explain functional programming" --cost-aware

# Display session cost summary on exit
dotnet run -- ask -q "Deep analysis" --cost-summary

# Combine all cost options
dotnet run -- orchestrator --goal "Complex task" \
  --show-costs --cost-aware --cost-summary
```

**Supported Providers for Cost Tracking:**
- **Anthropic**: Claude 3/4/5 family (Opus, Sonnet, Haiku)
- **OpenAI**: GPT-4, GPT-4o, GPT-3.5 family
- **DeepSeek**: DeepSeek-Coder, DeepSeek-V2
- **Google**: Gemini Pro, Gemini Flash
- **Mistral**: Mistral Large, Medium, Small
- **Local**: Ollama/local models (tracked as free)

📚 **Provider Documentation:**

- **[Anthropic Claude Integration Guide](docs/ANTHROPIC_INTEGRATION.md)** - Setting up Anthropic Claude API with secure key management
- **[Ollama Cloud Integration Guide](docs/OLLAMA_CLOUD_INTEGRATION.md)** - Detailed guide for connecting to cloud-hosted Ollama instances
- **[GitHub Models Guide](docs/GITHUB_MODELS_INTEGRATION.md)** - Using GitHub Models (GPT-4o, Llama 3) with Ouroboros

#### Programmatic Usage - Convenience Layer

The `Ouroboros` convenience class provides simplified access to the system's capabilities:

```csharp
// 1. Initialize with specific models
var ai = Ouroboros.Create(
    chatModelName: "llama3", 
    embeddingModelName: "nomic-embed-text"
);

// 2. Or initialize with an existing specialized orchestrator
var ai = Ouroboros.Create(orchestrator);

// 3. Ask a simple question
var answer = await ai.AskAsync("What is a monad?");

// 4. Ask with RAG (automatically ingests/retrieves from vector store)
var ragAnswer = await ai.AskWithRagAsync("How does the current project handle errors?");

// 5. Execute a complex goal using the smart planner
var result = await ai.ExecuteGoalAsync("Analyze the project structure and suggest improvements");

// 6. Execute a specific pipeline DSL
var dslResult = await ai.ExecutePipelineAsync("SetTopic('FP') | UseDraft | UseCritique");
```

#### Programmatic Usage - Core Pipeline Composition

For advanced scenarios, you can compose pipelines directly:

```csharp
// Initialize core components
var llm = new ToolAwareChatModel(new OllamaChatConfig { ModelId = "llama3" });
var tools = new ToolRegistry();
var embed = new OllamaEmbeddingModel();

// Create reasoning arrows (Kleisli arrows)
var draft = ReasoningArrows.DraftArrow(llm, tools, embed, "Explain monads", "draft");
var critique = ReasoningArrows.CritiqueArrow(llm, tools, embed, "draft", "critique");
var improve = ReasoningArrows.ImproveArrow(llm, tools, embed, "critique", "final");

// Compose the pipeline
// Draft -> Critique -> Improve
var pipeline = draft.ComposeWith(critique).ComposeWith(improve);

// Execute
var result = await pipeline(ReasoningState.Initial);
```

## 🧠 Core Concepts

### Monads

The project uses `Result<T>` and `Option<T>` to handle side effects and failures gracefully, ensuring that pipeline steps can be composed without exception handling boilerplate.

### Kleisli Arrows

Pipeline steps are modeled as Kleisli arrows `A -> M<B>`, where `M` is the `Result` monad. This allows for mathematical composition of steps: `(A -> M<B>) >=> (B -> M<C>)` yields `A -> M<C>`.

### Pipeline Composition

Pipelines are constructed by composing these arrows. If any step fails, the failure propagates through the monad, short-circuiting subsequent steps safely.

## 📁 Project Structure

- **src/Ouroboros.Core**: Core abstractions, monads, and interfaces
- **src/Ouroboros.Domain**: Domain entities, events, and value objects
- **src/Ouroboros.Pipeline**: Pipeline implementation, steps, and arrows
- **src/Ouroboros.Providers**: LLM and service provider implementations (Ollama, OpenAI)
- **src/Ouroboros.Agent**: AI Agent implementations and orchestrators
- **src/Ouroboros.CLI**: Command-line interface and entry point
- **src/Ouroboros.WebApi**: Web API for remote access
- **src/Ouroboros.Android**: Android mobile application
- **src/Ouroboros.Tests**: Unit and integration tests

## 🎯 Examples

The `src/Ouroboros.Examples` directory contains runnable examples demonstrating various capabilities.

### Epic Branch Orchestration ⭐ NEW

Demonstrates the **Phase 0** capability of automated epic management:

```bash
# Run the Epic Branch Orchestration example
dotnet run -- project src/Ouroboros.Examples/Ouroboros.Examples.csproj -- example epic-orchestration
```

This example shows:
1. **Epic Analysis**: Decomposing a high-level requirement into sub-tasks
2. **Agent Assignment**: Creating dedicated agents for each sub-task
3. **Branch Management**: Creating isolated git branches for each task
4. **Parallel Execution**: Running tasks concurrently
5. **Result Aggregation**: Combining results back into the main epic

### Phase 2: Integrated Self-Model ⭐ NEW

Demonstrates the **Phase 2** capability of an agent maintaining a persistent self-model:

```bash
# Run the Self-Model example
dotnet run -- project src/Ouroboros.Examples/Ouroboros.Examples.csproj -- example self-model
```

This example shows:
1. **Identity Initialization**: Agent loads its capability profile and constraints
2. **Global Workspace**: Information moves in and out of the agent's attention
3. **Predictive Monitoring**: Agent forecasts expected outcomes of its actions
4. **Anomaly Detection**: Agent notices when results deviate from predictions
5. **Self-Explanation**: Agent generates a narrative explaining *why* it made specific decisions

## 🔗 Key Features Details

### LangChain Pipe Operators

We've implemented custom operators to support LangChain's pipe syntax in C#:

```csharp
var chain = 
    Set("topic", "AI") 
    | Retrieve(vectorStore) 
    | Template(promptTemplate) 
    | LLM(chatModel);

var result = await chain.RunAsync();
```

### Meta-AI Layer - Self-Reflective Pipelines

The system exposes its own pipeline steps as tools to the LLM, allowing the AI to:
1. **Plan** a sequence of operations
2. **Execute** pipeline steps (e.g., "Draft", "Critique")
3. **Observe** the results
4. **Iterate** based on the output

### AI Reasoning Pipeline with Refinement Loop

The default reasoning pipeline implements a `Draft -> Critique -> Improve` loop:
1. **Draft**: Generates an initial response or solution
2. **Critique**: Analyzes the draft for errors, gaps, or improvements
3. **Improve**: Generates a refined version based on the critique

### MeTTa Symbolic Reasoning

Integration with MeTTa allows for:
- **Symbolic Planning**: Using logic programming to derive plans
- **Constraint Satisfaction**: Verifying plans against logical constraints
- **Neuro-symbolic execution**: Combining LLM generation with symbolic validation

### AI Orchestrator

The smart orchestrator:
1. **Analyzes** the incoming prompt
2. **Classifies** the intent (Code, Reasoning, Creative, General)
3. **Selects** the best model and parameters for the task
4. **Routes** the request to the appropriate sub-system

## 🛠️ Development

### Tool System

The system features a robust tool registry that supports:
- **Automatic Schema Generation**: Generates JSON schemas from C# interfaces
- **Dependency Injection**: Tools can request services via DI
- **Middleware**: Intercept tool execution for logging or validation

#### Built-in Tools
- `FileSystemTool`: Safe file operations
- `SearchTool`: Web search capabilities
- `VectorStoreTool`: RAG operations
- `PipelineStepTool`: Exposes pipeline steps as tools

### Testing

The project maintains high test coverage using xUnit and Moq.

#### Run Tests
```bash
dotnet test
```

#### Integration Tests
Integration tests require a running Ollama instance:
```bash
dotnet test --filter "Category=Integration"
```

#### Code Coverage
Generate code coverage reports:
```bash
# Install report generator
dotnet tool install -g dotnet-reportgenerator-globaltool

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Generate report
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coveragereport
```

#### Mutation Testing
We use Stryker for mutation testing to ensure test quality:
```bash
dotnet tool install -g dotnet-stryker
cd src/Ouroboros.Tests
dotnet stryker
```

## 🔄 GitHub Copilot Development Loop

This project uses a fully automated **GitHub Copilot Development Loop** to accelerate development and ensure quality.

### 🤖 Automated Development Cycle ⭐ **NEW**
The repository is equipped with a self-reinforcing development cycle that runs on a schedule or trigger:

1. **Code Analysis**: Copilot analyzes the codebase for improvement opportunities
2. **Issue Creation**: An issue is automatically created with a detailed implementation plan
3. **Branch Creation**: A dedicated branch (e.g., `copilot/refactor-auth`) is created
4. **Implementation**: Copilot Workspace/Agents implement the changes
5. **PR & Review**: A Pull Request is opened and automatically reviewed by a separate AI agent
6. **Merge**: Upon approval, the code is merged, completing the cycle

### Automated Code Review
Every Pull Request is automatically analyzed for:
- **Functional Patterns**: Ensuring proper use of Monads and Kleisli arrows
- **Test Coverage**: Verifying that new code is adequately tested
- **Performance**: Checking for potential bottlenecks

### Issue Analysis Assistant
When a human opens an issue, the system:
1. Analyzes the requirements
2. Identifies affected files
3. Suggests an implementation plan
4. Generates a "Start Here" prompt for Copilot

### Continuous Improvement
Weekly jobs run to:
- Identify technical debt
- Suggest refactoring candidates
- Update documentation
- Verify architectural compliance

### Documentation
See [GitHub Copilot Development Loop](docs/COPILOT_DEVELOPMENT_LOOP.md) for detailed configuration and workflows.

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch
3. Commit your changes
4. Push to the branch
5. Open a Pull Request

### Coding Standards
- Use **functional style** where possible (immutability, pure functions)
- Use **Result monad** for all failure prone operations
- Follow **C# standard conventions**

## 📋 Requirements

- .NET 10.0 SDK
- Ollama (for local LLM execution)
- Docker (optional, for containerized execution)

## 🚀 Deployment

### Docker Deployment

```bash
docker build -t ouroboros .
docker run -p 8080:8080 ouroboros
```

### Kubernetes Deployment

Helm charts are provided in `deploy/charts/ouroboros`.

```bash
helm install ouroboros ./deploy/charts/ouroboros
```

### Local/Systemd Deployment
See [Deployment Guide](DEPLOYMENT.md) for setting up Ouroboros as a systemd service.

## 🏗️ Infrastructure & Dependencies

The system relies on a carefully managed set of infrastructure components and dependencies.

### Infrastructure Documentation
- **[Infrastructure Dependencies](docs/INFRASTRUCTURE_DEPENDENCIES.md)**: Comprehensive mapping of all dependencies across C# projects, Kubernetes resources, and Terraform modules.
- **[Deployment Topology](docs/DEPLOYMENT_TOPOLOGY.md)**: Visual and descriptive overview of how components are deployed and interact.
- **[Environment Infrastructure Mapping](docs/ENVIRONMENT_INFRASTRUCTURE_MAPPING.md)**: Environment-specific configurations for Development, Staging, and Production.

### Infrastructure Validation
To validate your infrastructure configuration:

```bash
# Validate dependencies
dotnet run -- project src/Ouroboros.Tools/Ouroboros.Tools.csproj -- validate-dependencies

# Check environment configuration
dotnet run -- project src/Ouroboros.Tools/Ouroboros.Tools.csproj -- validate-env --environment production
```

### Key Infrastructure Dependencies
- **Core Runtime**: .NET 10.0, ASP.NET Core
- **AI/ML**: Ollama, LangChain
- **Data**: Qdrant (Vector DB), Redis (Caching)
- **Observability**: OpenTelemetry, Prometheus, Grafana
- **Cloud**: IONOS Cloud Kubernetes (DCD)

### Infrastructure Summary
For a quick overview of the current infrastructure state, see the [Infrastructure Summary Report](docs/INFRASTRUCTURE_SUMMARY.md).

## 🔧 Troubleshooting

### Kubernetes Image Pull Errors
If you see `ImagePullBackOff` errors when deploying to Kubernetes:

1. Check if the image exists in the registry
2. Verify image pull secrets are correctly configured
3. Ensure the tag matches the deployed version

See [Troubleshooting Guide](TROUBLESHOOTING.md) for more solutions.

## 📚 Documentation

### Essential Guides
- **[Quick Start Guide](QUICKSTART.md)** - Get up and running in 5 minutes
- **[Deployment Guide](DEPLOYMENT.md)** - Production deployment instructions
- **[Configuration & Security Guide](CONFIGURATION_AND_SECURITY.md)** - Configuration and security options
- **[Contributing Guide](CONTRIBUTING.md)** - Guidelines for contributors

### Technical Documentation
- **[Architecture Overview](docs/ARCHITECTURE.md)** - High-level system design
- **[Architectural Layers](docs/ARCHITECTURAL_LAYERS.md)** - Detailed layer breakdown
- **[Recursive Chunking](docs/RECURSIVE_CHUNKING.md)** - Large context processing
- **[Self-Improving Agent](docs/SELF_IMPROVING_AGENT.md)** - Agent capabilities
- **[Iterative Refinement](docs/ITERATIVE_REFINEMENT_ARCHITECTURE.md)** - Reasoning loops

### Developer Resources
- **[Test Coverage Report](TEST_COVERAGE_REPORT.md)** - Current test metrics
- **[Test Coverage Quick Reference](TEST_COVERAGE_QUICKREF.md)** - Testing commands
- **[Infrastructure Dependencies](docs/INFRASTRUCTURE_DEPENDENCIES.md)** - System dependencies

## ⚖️ License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- [LangChain](https://github.com/langchain-ai/langchain) for the inspiration
- [Ollama](https://ollama.ai/) for making local LLMs accessible
- [MeTTa](https://github.com/trueagi-io/hyperon-experimental) for symbolic reasoning capabilities
