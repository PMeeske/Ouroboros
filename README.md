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
- **🧠 Meta-AI Layer**: Pipeline steps exposed as tools - the LLM can invoke pipeline operations
- **🎯 AI Orchestrator**: Performance-aware model selection based on use case classification
- **🚀 Meta-AI Layer v2**: Planner/Executor/Verifier orchestrator with continual learning
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

## 🔧 Getting Started

### Prerequisites

- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- [Ollama](https://ollama.ai/) (for local LLM providers) or remote API access

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

4. **Run examples:**
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

Run all examples:
```bash
cd src/MonadicPipeline.Examples
dotnet run
```

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

```bash
# Navigate to CLI directory
cd src/MonadicPipeline.CLI

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

### Testing

Run the comprehensive test suite:

```bash
# Navigate to CLI directory
cd src/MonadicPipeline.CLI

# Run all tests
dotnet run -- test --all

# Run only integration tests
dotnet run -- test --integration

# Run only CLI tests
dotnet run -- test --cli
```

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
- [**ImagePullBackOff Quick Fix**](IMAGEPULLBACKOFF-FIX.md) - Solve Kubernetes image issues
- [**ImagePullBackOff Incident Response**](INCIDENT-RESPONSE-IMAGEPULLBACKOFF.md) - Real incident analysis and resolution
- [**Troubleshooting Guide**](TROUBLESHOOTING.md) - Common issues and solutions
- [**Scripts README**](scripts/README.md) - Deployment scripts documentation

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

**Quick Fix Guide**: See [IMAGEPULLBACKOFF-FIX.md](IMAGEPULLBACKOFF-FIX.md) for step-by-step solutions.

**Incident Response**: See [INCIDENT-RESPONSE-IMAGEPULLBACKOFF.md](INCIDENT-RESPONSE-IMAGEPULLBACKOFF.md) for real incident analysis.

**Detailed Troubleshooting**: See [TROUBLESHOOTING.md](TROUBLESHOOTING.md) for comprehensive troubleshooting.

## 📚 Additional Documentation

- [**Deployment Guide**](DEPLOYMENT.md) - **Comprehensive deployment instructions for all environments**
- [**IONOS Cloud Deployment Guide**](docs/IONOS_DEPLOYMENT_GUIDE.md) - **Detailed IONOS Cloud deployment instructions**
- [**RecursiveChunking Guide**](docs/RECURSIVE_CHUNKING.md) - **Large context processing with adaptive chunking**
- [Configuration and Security](CONFIGURATION_AND_SECURITY.md) - Security best practices and configuration guide
- [Implementation Guide](IMPLEMENTATION_GUIDE.md) - Detailed implementation guidance
- [Sprint Summary](SPRINT_3_4_SUMMARY.md) - Recent development progress
- [GitHub Copilot Instructions](.github/copilot-instructions.md) - Development guidelines for contributors

## ⚖️ License

This project is open source. Please check the repository for license details.

## 🙏 Acknowledgments

- Built on [LangChain](https://github.com/tryAGI/LangChain) for AI/LLM integration
- Inspired by category theory and functional programming principles
- Special thanks to the functional programming community for mathematical foundations
- Developed by **Adaptive Systems Inc.** for enterprise AI pipeline solutions

---

**MonadicPipeline by Adaptive Systems Inc.**: Where Category Theory Meets AI Pipeline Engineering 🚀
