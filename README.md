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
- **🧠 Meta-AI Layer**: Pipeline steps exposed as tools - the LLM can invoke pipeline operations to think about its own thinking!
- **🎯 AI Orchestrator**: Performance-aware model selection based on use case classification and metrics tracking
- **🚀 Meta-AI Layer v2**: Planner/Executor/Verifier orchestrator with continual learning, skill acquisition, and self-improvement **NEW!**
- **🔮 MeTTa Symbolic Reasoning**: Hybrid neural-symbolic AI with MeTTa integration for formal logic, rule-based inference, and plan verification **NEW!**
- **🚀 Orchestrator v3.0**: MeTTa-first representation layer with symbolic next-node selection and neuro-symbolic execution **NEW!**
- **📊 Vector Database Support**: Built-in vector storage and retrieval capabilities
- **🔄 Event Sourcing**: Complete audit trail with replay functionality
- **🛠️ Extensible Tool System**: Plugin architecture for custom tools and functions with advanced composition patterns
- **💾 Memory Management**: Multiple conversation memory strategies
- **🎯 Type Safety**: Leverages C# type system for compile-time guarantees
- **🔒 Safety & Permissions**: Multi-level security framework with sandboxed execution **NEW!**

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
│   └── MeTTa/              # **NEW!** MeTTa symbolic reasoning integration
├── Providers/              # External service providers
└── Examples/               # Comprehensive examples and demonstrations
```

## 🎯 Examples

The `Examples/` directory contains comprehensive demonstrations:

- **`MonadicExamples.cs`**: Core monadic operations
- **`ConversationalKleisliExamples.cs`**: Memory-integrated conversations
- **`HybridStepExamples.cs`**: Sync/async step combinations
- **`FunctionalReasoningExamples.cs`**: AI reasoning workflows
- **`LangChainPipeOperatorsExample.cs`**: LangChain-style pipe operators
- **`OrchestratorExample.cs`**: AI orchestrator with intelligent model selection
- **`MeTTaIntegrationExample.cs`**: MeTTa symbolic reasoning and hybrid neural-symbolic AI
- **`OrchestratorV3Example.cs`**: **NEW!** Orchestrator v3.0 with MeTTa-first representation layer

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

## 🧠 Meta-AI Layer - Self-Reflective Pipelines

MonadicPipeline features a groundbreaking **meta-AI layer** where the pipeline can think about its own thinking. Pipeline steps are automatically registered as tools that the LLM can invoke, creating a self-improving AI system.

### How It Works

All CLI pipeline steps (like `UseDraft`, `UseCritique`, `UseImprove`) are exposed as tools:

```csharp
// Automatic registration happens in DSL pipelines
tools = tools.WithPipelineSteps(state);
var llm = new ToolAwareChatModel(chatModel, tools);

// LLM can now invoke pipeline steps as tools
var (response, toolCalls) = await llm.GenerateWithToolsAsync(prompt);
```

### Self-Reflective Execution

The LLM can invoke pipeline operations during generation:

```bash
# The LLM can use pipeline tools to improve its own output
dotnet run -- pipeline --dsl "SetPrompt('Explain functional programming') | LLM"
```

During execution, the LLM might emit:
```
[TOOL:run_usedraft]        # Generate initial draft
[TOOL:run_usecritique]     # Critique the draft
[TOOL:run_useimprove]      # Improve based on critique
```

### Available Pipeline Tools

| Tool | Description |
|------|-------------|
| `run_usedraft` | Generate initial draft response |
| `run_usecritique` | Critique current draft |
| `run_useimprove` | Improve draft based on critique |
| `run_setprompt` | Set new prompt |
| `run_retrieve` | Semantic search over documents |
| `run_llm` | Execute LLM generation |
| ... | All CLI steps available |

### Programmatic Usage

```csharp
// Register all pipeline steps as tools
tools = tools.WithPipelineSteps(state);

// Or register specific steps
tools = tools.WithPipelineSteps(state, "UseDraft", "UseCritique", "UseImprove");

// Create meta-AI enabled LLM
var llm = new ToolAwareChatModel(chatModel, tools);
state.Llm = llm;
```

### Benefits

- **Self-Improvement**: Pipeline can refine its own outputs
- **Dynamic Workflows**: LLM chooses which steps to execute
- **Emergent Behavior**: Complex reasoning patterns emerge naturally
- **Transparency**: Full tool execution history in event log

See [`docs/META_AI_LAYER.md`](docs/META_AI_LAYER.md) for complete documentation and examples.

## 🔮 MeTTa Symbolic Reasoning Integration

MonadicPipeline integrates with **MeTTa** (meta-type-talk), a powerful symbolic reasoning system, to enable hybrid neural-symbolic AI capabilities. This allows combining LLM reasoning with formal logic, rule-based inference, and symbolic plan verification.

### Key Features

- **Symbolic Querying**: Execute MeTTa queries for logical inference
- **Rule Application**: Apply symbolic rules to derive new knowledge
- **Plan Verification**: Formally verify AI plans using symbolic reasoning
- **Memory Bridge**: Sync orchestrator experiences to MeTTa facts
- **Multiple Backends**: Subprocess-based (default) or HTTP client for Python Hyperon service

### Quick Start

```csharp
// Create MeTTa engine (subprocess-based by default)
using var engine = new SubprocessMeTTaEngine();

// Add symbolic facts
await engine.AddFactAsync("(human Socrates)");
await engine.AddFactAsync("(mortal $x) :- (human $x)");

// Execute symbolic query
var result = await engine.ExecuteQueryAsync("!(match &self (mortal Socrates) $result)");
result.Match(
    success => Console.WriteLine($"Result: {success}"),
    error => Console.WriteLine($"Error: {error}")
);
```

### Tool Integration

Register MeTTa tools with the tool registry:

```csharp
// Add MeTTa tools to tool registry
var tools = ToolRegistry.CreateDefault()
    .WithMeTTaTools();  // Subprocess-based engine

// Or use HTTP client for Python Hyperon service
var tools = ToolRegistry.CreateDefault()
    .WithMeTTaHttpTools("http://localhost:8000", apiKey: "your-key");

// MeTTa tools are now available to the LLM
var llm = new ToolAwareChatModel(chatModel, tools);
```

### Available MeTTa Tools

| Tool | Description |
|------|-------------|
| `metta_query` | Execute symbolic queries against knowledge base |
| `metta_rule` | Apply inference rules to derive new knowledge |
| `metta_verify_plan` | Verify plans using symbolic reasoning |
| `metta_add_fact` | Add facts to the symbolic knowledge base |

### Memory Bridge

Sync orchestrator memory to MeTTa for symbolic reasoning:

```csharp
var memory = new MemoryStore(embedModel);
var engine = new SubprocessMeTTaEngine();

// Create bridge between memory and MeTTa
var bridge = memory.CreateMeTTaBridge(engine);

// Sync experiences as symbolic facts
var result = await bridge.SyncAllExperiencesAsync();
result.Match(
    count => Console.WriteLine($"Synced {count} facts to MeTTa"),
    error => Console.WriteLine($"Error: {error}")
);

// Query experiences using symbolic reasoning
var queryResult = await bridge.QueryExperiencesAsync(
    "!(match &self (experience-quality $id $score) $result)"
);
```

### Backend Options

**Subprocess Engine (Default):**
```csharp
// Requires 'metta' executable in PATH
var engine = new SubprocessMeTTaEngine();

// Or specify custom path
var engine = new SubprocessMeTTaEngine("/path/to/metta");
```

**HTTP Client (Python Hyperon Service):**
```csharp
// Connect to Python-based MeTTa/Hyperon service
var engine = new HttpMeTTaEngine("http://localhost:8000", apiKey: "optional-key");
```

### Meta-AI Orchestrator Integration

Combine MeTTa with the Meta-AI orchestrator for hybrid neural-symbolic reasoning:

```csharp
var engine = new SubprocessMeTTaEngine();
var tools = ToolRegistry.CreateDefault()
    .WithMeTTaTools(engine);

var orchestrator = MetaAIBuilder.CreateDefault()
    .WithLLM(chatModel)
    .WithTools(tools)  // Includes MeTTa tools
    .WithEmbedding(embedModel)
    .Build();

// Add domain knowledge to MeTTa
await engine.AddFactAsync("(requires teaching-fp basic-programming)");
await engine.AddFactAsync("(requires teaching-fp higher-order-functions)");

// Orchestrator can now use symbolic reasoning during planning
var planResult = await orchestrator.PlanAsync(
    "Create a curriculum for teaching functional programming"
);
```

### Use Cases

- **Plan Verification**: Formally verify that AI-generated plans are logically sound
- **Knowledge Representation**: Store domain knowledge as symbolic facts and rules
- **Hybrid Reasoning**: Combine neural pattern recognition with symbolic logic
- **Constraint Checking**: Verify outputs satisfy formal constraints
- **Explainable AI**: Trace reasoning through symbolic proof chains

### Example Programs

See [`Examples/MeTTaIntegrationExample.cs`](Examples/MeTTaIntegrationExample.cs) for complete examples including:
- Basic symbolic reasoning
- Tool integration
- HTTP client usage
- Orchestrator integration
- Memory bridging

See [`Examples/OrchestratorV3Example.cs`](Examples/OrchestratorV3Example.cs) for **Orchestrator v3.0** demonstrations.

### Installation Notes

**For subprocess engine:**
- Install `metta-stdlib` and ensure `metta` executable is in PATH
- Or provide custom path: `new SubprocessMeTTaEngine("/path/to/metta")`

**For HTTP client:**
- Requires a running Python Hyperon service with HTTP API
- See the Hyperon documentation for service setup

---

## 🚀 Orchestrator v3.0 — MeTTa-First Representation Layer

**NEW in v3.0**: A groundbreaking neuro-symbolic orchestration system that represents all orchestrator concepts (plans, steps, tools, state, memory) as MeTTa symbolic atoms, enabling symbolic reasoning over execution flow.

### Core Innovation

Orchestrator v3.0 lifts the entire orchestration layer into a MeTTa-first representation:

```
Neural Layer (LLM)  ←→  Symbolic Layer (MeTTa)
     ↓                        ↓
 Plan Generation  →   MeTTa Atom Representation
 Step Execution   →   Symbolic State Updates
 Next Node Query  ←   Constraint-Based Reasoning
```

### Key Features

- **🎯 MeTTa Representation Layer**: Translates plans, steps, tools, and state to MeTTa atoms
- **🔍 NextNode Tool**: Symbolic next-step enumeration using constraint-based reasoning
- **🧩 Constraint System**: Add domain rules to guide execution flow
- **🔗 Hybrid Reasoning**: Combines neural planning with symbolic verification
- **📊 Telemetry Integration**: Updates MeTTa knowledge base with execution results

### Quick Start

```csharp
// Initialize v3.0 orchestrator with MeTTa
var mettaEngine = new SubprocessMeTTaEngine();
var tools = ToolRegistry.CreateDefault()
    .WithMeTTaTools(mettaEngine);  // Includes NextNode tool

// Create MeTTa representation layer
var representation = new MeTTaRepresentation(mettaEngine);

// Define a plan
var plan = new Plan(
    Goal: "Research and summarize functional programming",
    Steps: new List<PlanStep>
    {
        new("search_docs", params, "Find relevant documents", 0.9),
        new("analyze", params, "Extract key concepts", 0.8),
        new("synthesize", params, "Create summary", 0.85)
    },
    ConfidenceScores: new Dictionary<string, double> { ["overall"] = 0.85 },
    CreatedAt: DateTime.UtcNow
);

// Translate plan to MeTTa symbolic representation
await representation.TranslatePlanAsync(plan);
await representation.TranslateToolsAsync(tools);

// Add domain constraints
await representation.AddConstraintAsync("(requires analyze search_docs)");
await representation.AddConstraintAsync("(requires synthesize analyze)");
await representation.AddConstraintAsync("(capability search_docs information-retrieval)");

// Use NextNode tool to query valid next steps
var nextNodeTool = tools.GetTool("next_node");
var nextNodes = await nextNodeTool.Value.InvokeAsync(@"{
    ""current_step_id"": ""step_0"",
    ""plan_goal"": ""Research and summarize functional programming"",
    ""context"": { ""completed"": [""step_0""] }
}");
```

### MeTTa Representation Examples

**Plan as MeTTa Atoms:**
```metta
(goal plan_abc123 "Research functional programming")
(step plan_abc123 step_0 0 "search_docs")
(step plan_abc123 step_1 1 "analyze")
(step plan_abc123 step_2 2 "synthesize")
(before step_0 step_1)
(before step_1 step_2)
(confidence step_0 0.90)
(confidence step_1 0.80)
```

**Tools as MeTTa Atoms:**
```metta
(tool tool_search "search_docs")
(tool-desc tool_search "Search for documents")
(capability tool_search information-retrieval)
(capability tool_analyze content-analysis)
(capability tool_synthesize content-creation)
```

**Constraints:**
```metta
(requires step_2 step_1)
(forbids parallel step_1 step_2)
(min-confidence step_2 0.8)
(requires-capability step_1 nlp-processing)
```

### NextNode Tool

The `next_node` tool uses symbolic reasoning to enumerate valid next execution nodes:

**Input Schema:**
```json
{
  "current_step_id": "step_0",
  "plan_goal": "Goal description",
  "context": { "step_index": 0, "total_steps": 3 },
  "constraints": [
    "(requires step_2 step_1)",
    "(capability step_1 processing)"
  ]
}
```

**Output:**
```json
{
  "nextSteps": [
    {
      "nodeId": "step_1",
      "action": "analyze",
      "confidence": 0.9
    }
  ],
  "recommendedTools": ["tool_analyze"],
  "timestamp": "2024-01-01T12:00:00Z"
}
```

### MeTTaOrchestrator Integration

Use the v3.0 orchestrator for full neuro-symbolic execution:

```csharp
// Create v3.0 orchestrator
var orchestrator = new MeTTaOrchestrator(
    llm: chatModel,
    tools: toolsWithMeTTa,
    memory: memoryStore,
    skills: skillRegistry,
    router: uncertaintyRouter,
    safety: safetyGuard,
    mettaEngine: mettaEngine
);

// Plan with symbolic representation
var planResult = await orchestrator.PlanAsync(
    "Create research summary",
    context: new Dictionary<string, object> { ["domain"] = "AI" }
);

// Execute with MeTTa-guided next-step selection
var executionResult = await orchestrator.ExecuteAsync(planResult.Value);

// Verify with symbolic reasoning
var verification = await orchestrator.VerifyAsync(executionResult.Value);

// Learn and update MeTTa knowledge base
orchestrator.LearnFromExecution(verification.Value);
```

### Advanced Constraint Reasoning

Add sophisticated constraints for complex orchestration:

```csharp
var advancedConstraints = new[]
{
    // Dependencies
    "(depends step_analyze step_fetch)",
    "(depends step_summarize step_analyze)",
    
    // Capability requirements
    "(requires-capability step_fetch network-access)",
    "(requires-capability step_analyze nlp-processing)",
    
    // Resource constraints
    "(max-concurrent step_fetch 3)",
    "(memory-intensive step_analyze)",
    
    // Quality constraints
    "(min-confidence step_summarize 0.8)",
    "(requires-validation step_summarize)"
};

foreach (var constraint in advancedConstraints)
{
    await representation.AddConstraintAsync(constraint);
}

// Query tools matching constraints
var toolQuery = await representation.QueryToolsForGoalAsync(
    "Analyze large dataset"
);
```

### Benefits of v3.0

1. **Explainable Decisions**: Trace why each next node was selected via symbolic proof
2. **Constraint Satisfaction**: Ensure execution respects formal requirements
3. **Hybrid Intelligence**: Neural creativity + symbolic precision
4. **Formal Verification**: Prove plans are correct before execution
5. **Knowledge Accumulation**: MeTTa facts persist across executions

### Example Programs

See [`Examples/OrchestratorV3Example.cs`](Examples/OrchestratorV3Example.cs) for comprehensive demonstrations:
- Basic MeTTa-first orchestration
- Constraint-based reasoning
- NextNode tool usage
- Advanced symbolic planning

### Testing

Run v3.0 tests:
```bash
dotnet run -- test --all  # Includes MeTTa Orchestrator v3.0 tests
```

---

**For HTTP client:**
- Start a Python Hyperon service (see MeTTa documentation)
- Configure endpoint: `new HttpMeTTaEngine("http://localhost:8000")`

## 🎯 AI Orchestrator - Intelligent Model & Tool Selection

MonadicPipeline features a sophisticated **AI orchestrator** that automatically selects the best models and tools based on prompt analysis and performance metrics. This creates a self-optimizing system that continuously improves.

### How It Works

The orchestrator analyzes each prompt to classify its use case, then selects the optimal model based on:
- **Use Case Matching**: Code generation, reasoning, creative, summarization, etc.
- **Model Capabilities**: Strengths and specializations
- **Performance Metrics**: Historical success rate and latency

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

### Composable Tools

Advanced tool composition with performance tracking:

```csharp
var enhancedTool = tool
    .WithRetry(maxRetries: 3)              // Retry on failure
    .WithPerformanceTracking(callback)     // Track metrics
    .WithCaching(TimeSpan.FromMinutes(5))  // Cache results
    .WithTimeout(TimeSpan.FromSeconds(10)) // Timeout protection
    .WithFallback(fallbackTool);           // Graceful degradation
```

### Use Case Classification

Prompts are automatically classified into use case types:
- **CodeGeneration**: "Write a function to..."
- **Reasoning**: "Explain why..." / "Analyze..."
- **Creative**: "Create a story about..."
- **Summarization**: "Summarize this document..."
- **ToolUse**: "Use the search tool to..."
- **Conversation**: General chat

### Performance Tracking

Every execution is tracked for continuous optimization:

```csharp
var metrics = orchestrator.GetMetrics();
foreach (var (name, metric) in metrics)
{
    Console.WriteLine($"{name}:");
    Console.WriteLine($"  Executions: {metric.ExecutionCount}");
    Console.WriteLine($"  Success Rate: {metric.SuccessRate:P0}");
    Console.WriteLine($"  Avg Latency: {metric.AverageLatencyMs:F0}ms");
}
```

### Advanced Tool Patterns

**Parallel Execution**:
```csharp
var parallelSearch = OrchestratorToolExtensions.Parallel(
    "multi_search",
    "Searches multiple sources",
    results => string.Join("\n", results),
    webSearch, docSearch, codeSearch);
```

**Conditional Routing**:
```csharp
var smartTool = AdvancedToolBuilder.Switch(
    "smart_calc",
    "Routes to appropriate calculator",
    (input => input.Contains("complex"), advancedCalc),
    (input => true, simpleCalc));
```

**Pipeline Composition**:
```csharp
var analysisPipeline = AdvancedToolBuilder.Pipeline(
    "analysis",
    "Complete analysis workflow",
    dataTool, processTool, analyzeTool, reportTool);
```

See [`docs/ORCHESTRATOR.md`](docs/ORCHESTRATOR.md) for comprehensive documentation and examples.

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
- **[Meta-AI Layer v2](docs/META_AI_LAYER_V2.md)**: **NEW!** Planner/Executor/Verifier orchestrator with continual learning
- **[AI Orchestrator](docs/ORCHESTRATOR.md)**: Intelligent model and tool selection guide
- **[Meta-AI Layer](docs/META_AI_LAYER.md)**: Self-reflective pipeline documentation
- **[LangChain Operators](docs/LANGCHAIN_OPERATORS.md)**: LangChain pipe operator reference
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