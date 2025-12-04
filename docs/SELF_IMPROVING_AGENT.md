# Self-Improving Agent Architecture

## Overview

Ouroboros implements a **self-improving agent architecture** based on Meta-AI v2 principles. The system automatically learns from successful executions, extracts reusable skills, manages memory with consolidation and forgetting, and routes tasks based on confidence levels.

## Architecture Components

### 1. Skill Extraction System

The skill extraction system automatically identifies and extracts reusable patterns from high-quality executions.

#### ISkillExtractor Interface

```csharp
public interface ISkillExtractor
{
    Task<Result<Skill, string>> ExtractSkillAsync(
        ExecutionResult execution,
        VerificationResult verification,
        SkillExtractionConfig? config = null,
        CancellationToken ct = default);

    Task<bool> ShouldExtractSkillAsync(
        VerificationResult verification,
        SkillExtractionConfig? config = null);

    Task<string> GenerateSkillNameAsync(
        ExecutionResult execution,
        CancellationToken ct = default);

    Task<string> GenerateSkillDescriptionAsync(
        ExecutionResult execution,
        CancellationToken ct = default);
}
```

#### Key Features

- **Automatic Pattern Recognition**: Analyzes successful execution patterns to identify reusable skills
- **LLM-Powered Naming**: Uses the language model to generate descriptive skill names
- **Quality Thresholding**: Only extracts skills from executions with quality scores above threshold (default: 0.8)
- **Parameter Extraction**: Automatically parameterizes steps to make skills more reusable
- **Skill Evolution**: Updates existing skills with new execution data

#### Configuration

```csharp
var config = new SkillExtractionConfig(
    MinQualityThreshold: 0.8,      // Minimum quality score to extract
    MinStepsForExtraction: 2,       // Minimum number of steps required
    MaxStepsPerSkill: 10,           // Maximum complexity per skill
    EnableAutoParameterization: true,
    EnableSkillVersioning: true
);

var extractor = new SkillExtractor(llm, skillRegistry);
var result = await extractor.ExtractSkillAsync(execution, verification, config);
```

#### Example

```csharp
// After a successful execution with quality > 0.8, the orchestrator automatically:
// 1. Checks if skill should be extracted
// 2. Generates skill name using LLM (e.g., "calculate_arithmetic_sum")
// 3. Generates skill description
// 4. Extracts and parameterizes steps
// 5. Registers skill in the registry

orchestrator.LearnFromExecution(verification);
// ✓ Extracted skill: calculate_arithmetic_sum (Quality: 95%)
```

### 2. Persistent Memory Store

Enhanced memory system with short-term/long-term separation, consolidation, and intelligent forgetting.

#### Memory Types

- **Episodic Memory**: Recent, specific execution instances (short-term)
- **Semantic Memory**: Consolidated, generalized patterns (long-term)

#### Key Features

- **Importance Scoring**: Automatically calculates memory importance based on:
  - Quality score (50% weight)
  - Recency (30% weight)
  - Success/failure (20% weight)

- **Memory Consolidation**: Periodically transfers high-importance episodic memories to semantic storage

- **Intelligent Forgetting**: Removes low-importance memories when capacity is reached

- **Vector Search**: Semantic similarity search when embedding model is available

#### Configuration

```csharp
var config = new PersistentMemoryConfig(
    ShortTermCapacity: 100,              // Max episodic memories
    LongTermCapacity: 1000,              // Max semantic memories
    ConsolidationThreshold: 0.7,         // Min importance to consolidate
    ConsolidationInterval: TimeSpan.FromHours(1),
    EnableForgetting: true,
    ForgettingThreshold: 0.3             // Min importance to retain
);

var memory = new PersistentMemoryStore(embedding, vectorStore, config);
```

#### Memory Lifecycle

```
New Experience
     │
     ├──> Store as Episodic (short-term)
     │         │
     │         ├──> Calculate Importance
     │         │
     │         ├──> Time/Capacity Check
     │         │         │
     │         │         ├──> Consolidate (high importance → Semantic)
     │         │         │
     │         │         └──> Forget (low importance → removed)
     │         │
     │         └──> Vector Store (if available)
     │
     └──> Retrievable via Similarity Search
```

### 3. Uncertainty Router

Routes tasks based on confidence analysis with fallback strategies.

#### Routing Strategies

The router analyzes confidence and selects appropriate strategies:

| Confidence | Strategy | Action |
|-----------|----------|--------|
| > 0.7 | Direct | Execute with selected model |
| 0.5 - 0.7 | Ensemble | Use multiple models for consensus |
| 0.3 - 0.5 | Decompose or Ensemble | Break down task or use ensemble |
| < 0.3 | Clarification or Context | Request more information |

#### Features

- **Historical Learning**: Tracks routing outcomes to improve confidence estimates
- **Bayesian-Inspired**: Adjusts confidence based on task complexity and context
- **Fallback Cascading**: Automatically escalates when confidence is low

#### Example

```csharp
var router = new UncertaintyRouter(orchestrator, minConfidenceThreshold: 0.7);

var decision = await router.RouteAsync("Complex analytical task");

decision.Match(
    routing => {
        // routing.Route: Selected model/strategy
        // routing.Confidence: 0.0 - 1.0
        // routing.Reason: Explanation
    },
    error => { /* Handle error */ }
);

// Record outcome for learning
router.RecordRoutingOutcome(decision, wasSuccessful: true);
```

## Integration Example

### Complete Self-Improving Agent Setup

```csharp
using LangChain.Providers.Ollama;
using LangChainPipeline.Agent;
using LangChainPipeline.Agent.MetaAI;

// 1. Configure components
var provider = new OllamaProvider();
var llm = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
var tools = ToolRegistry.CreateDefault();

// 2. Create enhanced memory
var memoryConfig = new PersistentMemoryConfig(
    ShortTermCapacity: 50,
    LongTermCapacity: 500,
    ConsolidationThreshold: 0.8,
    EnableForgetting: true
);
var memory = new PersistentMemoryStore(config: memoryConfig);

// 3. Create skill registry and extractor
var skillRegistry = new SkillRegistry();
var skillExtractor = new SkillExtractor(llm, skillRegistry);

// 4. Build orchestrator
var orchestrator = MetaAIBuilder.CreateDefault()
    .WithLLM(llm)
    .WithTools(tools)
    .WithMemoryStore(memory)
    .WithSkillRegistry(skillRegistry)
    .WithSkillExtractor(skillExtractor)
    .WithConfidenceThreshold(0.7)
    .Build();

// 5. Execute and learn
var planResult = await orchestrator.PlanAsync("Calculate sum of 42 and 58");
var execResult = await orchestrator.ExecuteAsync(planResult.Value);
var verifyResult = await orchestrator.VerifyAsync(execResult.Value);

// 6. Automatic learning
orchestrator.LearnFromExecution(verifyResult.Value);
// ✓ Experience stored in memory
// ✓ Skill extracted if quality > 0.8
// ✓ Memory consolidation triggered if needed
```

## Learning Cycle

The complete learning cycle operates as follows:

```
┌──────────────────────────────────────────────────────────┐
│                    LEARNING CYCLE                         │
└──────────────────────────────────────────────────────────┘

1. PLAN
   ├─> Query past experiences (semantic search)
   ├─> Find matching skills
   └─> Generate execution plan

2. EXECUTE
   ├─> Execute steps sequentially
   ├─> Monitor performance
   └─> Collect execution data

3. VERIFY
   ├─> Assess quality (0.0 - 1.0)
   ├─> Identify issues
   └─> Suggest improvements

4. LEARN
   ├─> Store experience in memory
   │   ├─> Calculate importance
   │   ├─> Store as episodic
   │   └─> Vector embedding
   │
   ├─> Extract skill (if quality > 0.8)
   │   ├─> Generate name/description
   │   ├─> Parameterize steps
   │   └─> Register in registry
   │
   └─> Consolidate memory (periodic)
       ├─> Episodic → Semantic
       └─> Forget low-importance
```

## Performance Metrics

The orchestrator tracks performance across multiple dimensions:

```csharp
var metrics = orchestrator.GetMetrics();

foreach (var (component, metric) in metrics)
{
    Console.WriteLine($"{component}:");
    Console.WriteLine($"  Executions: {metric.ExecutionCount}");
    Console.WriteLine($"  Avg Latency: {metric.AverageLatencyMs}ms");
    Console.WriteLine($"  Success Rate: {metric.SuccessRate:P0}");
    Console.WriteLine($"  Last Used: {metric.LastUsed}");
}
```

## Advanced Features

### Skill Composition (Implemented)

Combine multiple skills into higher-order skills:

```csharp
var composer = new SkillComposer(skillRegistry, memory);

var result = await composer.ComposeSkillsAsync(
    compositeName: "data_analysis_pipeline",
    description: "Complete data analysis workflow",
    componentSkillNames: new List<string> 
    { 
        "load_data", 
        "clean_data", 
        "analyze_data", 
        "generate_report" 
    }
);
```

### Memory Retrieval Strategies

```csharp
// Similarity-based retrieval
var query = new MemoryQuery(
    Goal: "mathematical calculations",
    Context: new Dictionary<string, object> { ["domain"] = "arithmetic" },
    MaxResults: 10,
    MinSimilarity: 0.7
);

var relevantExperiences = await memory.RetrieveRelevantExperiencesAsync(query);
```

## Best Practices

### 1. Quality Thresholds

Set extraction thresholds based on your use case:
- **High-stakes domains**: 0.9+ (only extract highly verified skills)
- **Exploratory learning**: 0.7+ (more aggressive extraction)
- **Production systems**: 0.85+ (balanced approach)

### 2. Memory Management

Configure memory limits based on available resources:
```csharp
// For resource-constrained environments
var config = new PersistentMemoryConfig(
    ShortTermCapacity: 20,
    LongTermCapacity: 100,
    EnableForgetting: true
);

// For servers with ample memory
var config = new PersistentMemoryConfig(
    ShortTermCapacity: 500,
    LongTermCapacity: 5000,
    EnableForgetting: false  // Keep everything
);
```

### 3. Skill Versioning

Track skill evolution over time:
```csharp
var skill = skillRegistry.GetSkill("calculate_sum");
Console.WriteLine($"Success rate: {skill.SuccessRate:P0}");
Console.WriteLine($"Usage count: {skill.UsageCount}");
Console.WriteLine($"Created: {skill.CreatedAt}");
Console.WriteLine($"Last used: {skill.LastUsed}");
```

### 4. Monitoring and Debugging

Monitor learning behavior:
```csharp
// Memory statistics
var stats = await memory.GetStatisticsAsync();
Console.WriteLine($"Total experiences: {stats.TotalExperiences}");
Console.WriteLine($"Avg quality: {stats.AverageQualityScore:P0}");

// Skill statistics
var skills = skillRegistry.GetAllSkills();
Console.WriteLine($"Total skills: {skills.Count}");
foreach (var skill in skills.OrderByDescending(s => s.SuccessRate))
{
    Console.WriteLine($"  {skill.Name}: {skill.SuccessRate:P0} ({skill.UsageCount} uses)");
}
```

## Phase 2: Self-Model & Metacognition (Implemented ✓)

### 4. Capability Registry

The agent maintains a self-model of its own capabilities, success rates, and limitations.

#### ICapabilityRegistry Interface

```csharp
public interface ICapabilityRegistry
{
    Task<List<AgentCapability>> GetCapabilitiesAsync(CancellationToken ct = default);
    Task<bool> CanHandleAsync(string task, Dictionary<string, object>? context = null, CancellationToken ct = default);
    AgentCapability? GetCapability(string name);
    Task UpdateCapabilityAsync(string name, ExecutionResult result, CancellationToken ct = default);
    void RegisterCapability(AgentCapability capability);
    Task<List<string>> IdentifyCapabilityGapsAsync(string task, CancellationToken ct = default);
    Task<List<string>> SuggestAlternativesAsync(string task, CancellationToken ct = default);
}
```

#### Key Features

- **Self-Awareness**: Agent knows what it can and cannot do
- **Success Tracking**: Monitors success rates per capability over time
- **Gap Analysis**: Identifies missing capabilities for given tasks
- **Alternative Suggestions**: Proposes alternatives when unable to handle a task
- **Dynamic Updates**: Capability metrics update with each execution

#### Example

```csharp
var registry = new CapabilityRegistry(llm, tools);

// Check if agent can handle a task
var canHandle = await registry.CanHandleAsync("Build a machine learning model");
if (!canHandle)
{
    // Get suggestions for alternatives
    var alternatives = await registry.SuggestAlternativesAsync("Build a machine learning model");
    // Result: ["Use pre-trained model", "Simplify to statistical analysis", ...]
}

// Get capability gaps
var gaps = await registry.IdentifyCapabilityGapsAsync("Quantum computing simulation");
// Result: ["Missing tools: quantum_simulator", "No experience with quantum algorithms"]
```

### 5. Goal Hierarchy

Hierarchical goal decomposition with value alignment and conflict detection.

#### IGoalHierarchy Interface

```csharp
public interface IGoalHierarchy
{
    void AddGoal(Goal goal);
    Task<Result<Goal, string>> DecomposeGoalAsync(Goal goal, int maxDepth = 3, CancellationToken ct = default);
    Task<List<GoalConflict>> DetectConflictsAsync(CancellationToken ct = default);
    Task<Result<bool, string>> CheckValueAlignmentAsync(Goal goal, CancellationToken ct = default);
    void CompleteGoal(Guid id, string reason);
    Task<List<Goal>> PrioritizeGoalsAsync(CancellationToken ct = default);
}
```

#### Goal Types

- **Primary**: Main objectives
- **Secondary**: Supporting objectives
- **Instrumental**: Means to achieve other goals
- **Safety**: Constraint/boundary conditions (cannot be overridden)

#### Key Features

- **Automatic Decomposition**: LLM-powered goal breakdown into subgoals
- **Conflict Detection**: Identifies contradictory goals
- **Value Alignment**: Ensures goals respect safety constraints
- **Priority Management**: Dependency-aware goal ordering
- **Safety Enforcement**: Safety goals are immutable

#### Example

```csharp
var hierarchy = new GoalHierarchy(llm, safety);

var mainGoal = new Goal("Build a recommendation system", GoalType.Primary, 0.9);

// Decompose into subgoals
var decomposed = await hierarchy.DecomposeGoalAsync(mainGoal);
// Result: subgoals like "collect user data", "train model", "implement API", etc.

// Check for conflicts
var conflicts = await hierarchy.DetectConflictsAsync();
// Identifies conflicting goals and suggests resolutions

// Check value alignment
var aligned = await hierarchy.CheckValueAlignmentAsync(mainGoal);
// Ensures goal respects safety constraints
```

### 6. Self-Evaluator

Metacognitive monitoring and autonomous performance assessment.

#### ISelfEvaluator Interface

```csharp
public interface ISelfEvaluator
{
    Task<Result<SelfAssessment, string>> EvaluatePerformanceAsync(CancellationToken ct = default);
    Task<List<Insight>> GenerateInsightsAsync(CancellationToken ct = default);
    Task<Result<ImprovementPlan, string>> SuggestImprovementsAsync(CancellationToken ct = default);
    Task<double> GetConfidenceCalibrationAsync(CancellationToken ct = default);
    void RecordPrediction(double predictedConfidence, bool actualSuccess);
    Task<List<(DateTime Time, double Value)>> GetPerformanceTrendAsync(string metric, TimeSpan timeWindow, CancellationToken ct = default);
}
```

#### Key Features

- **Performance Tracking**: Monitors success rates, calibration, skill acquisition
- **Confidence Calibration**: Measures and improves prediction accuracy using Brier score
- **Insight Generation**: Identifies patterns in successes and failures
- **Improvement Planning**: Suggests actionable steps to enhance performance
- **Trend Analysis**: Tracks performance evolution over time

#### Example

```csharp
var evaluator = new SelfEvaluator(llm, capabilities, skills, memory, orchestrator);

// Self-assessment
var assessment = await evaluator.EvaluatePerformanceAsync();
// Result: Overall performance, calibration, strengths, weaknesses, summary

// Generate insights
var insights = await evaluator.GenerateInsightsAsync();
// Result: ["Success Pattern: High performance on structured data tasks", ...]

// Create improvement plan
var plan = await evaluator.SuggestImprovementsAsync();
// Result: Goal, actions, expected improvements, duration

// Check calibration
var calibration = await evaluator.GetConfidenceCalibrationAsync();
// Result: 0.85 (well-calibrated: 1.0 = perfect, 0.0 = worst)
```

## Phase 3: Emergent Intelligence (Implemented ✓)

### 7. Transfer Learning

Cross-domain skill adaptation using analogical reasoning.

#### ITransferLearner Interface

```csharp
public interface ITransferLearner
{
    Task<Result<TransferResult, string>> AdaptSkillToDomainAsync(
        Skill sourceSkill, string targetDomain, TransferLearningConfig? config = null, CancellationToken ct = default);
    
    Task<double> EstimateTransferabilityAsync(Skill skill, string targetDomain, CancellationToken ct = default);
    
    Task<List<(string source, string target, double confidence)>> FindAnalogiesAsync(
        string sourceDomain, string targetDomain, CancellationToken ct = default);
    
    List<TransferResult> GetTransferHistory(string skillName);
    
    void RecordTransferValidation(TransferResult transferResult, bool success);
}
```

#### Key Features

- **Domain Adaptation**: LLM-powered skill transformation for new domains
- **Analogical Reasoning**: Finds conceptual mappings between domains
- **Transferability Scoring**: Estimates transfer success probability
- **Transfer History**: Tracks all adaptation attempts
- **Validation Recording**: Updates transferability based on results

#### Example

```csharp
var transferLearner = new TransferLearner(llm, skills, memory);

// Estimate transferability
var score = await transferLearner.EstimateTransferabilityAsync(
    debuggingSkill,
    "troubleshooting mechanical systems");
// Result: 0.72 (reasonably transferable)

// Find analogies
var analogies = await transferLearner.FindAnalogiesAsync(
    "software debugging",
    "mechanical troubleshooting");
// Result: [("error message", "symptom", 0.85), ("code location", "faulty component", 0.78), ...]

// Adapt skill
var result = await transferLearner.AdaptSkillToDomainAsync(debuggingSkill, "mechanical systems");
// Result: Adapted skill with transformed steps
```

### 8. Hypothesis Engine

Scientific reasoning with hypothesis generation and experimental testing.

#### IHypothesisEngine Interface

```csharp
public interface IHypothesisEngine
{
    Task<Result<Hypothesis, string>> GenerateHypothesisAsync(
        string observation, Dictionary<string, object>? context = null, CancellationToken ct = default);
    
    Task<Result<Experiment, string>> DesignExperimentAsync(Hypothesis hypothesis, CancellationToken ct = default);
    
    Task<Result<HypothesisTestResult, string>> TestHypothesisAsync(
        Hypothesis hypothesis, Experiment experiment, CancellationToken ct = default);
    
    Task<Result<Hypothesis, string>> AbductiveReasoningAsync(
        List<string> observations, CancellationToken ct = default);
    
    void UpdateHypothesis(Guid hypothesisId, string evidence, bool supports);
}
```

#### Key Features

- **Hypothesis Generation**: Forms testable hypotheses from observations
- **Experiment Design**: Creates concrete tests for hypotheses
- **Hypothesis Testing**: Executes experiments and evaluates results
- **Abductive Reasoning**: Infers best explanation for multiple observations
- **Confidence Tracking**: Adjusts hypothesis confidence based on evidence

#### Example

```csharp
var hypothesisEngine = new HypothesisEngine(llm, orchestrator, memory);

// Generate hypothesis
var hypothesis = await hypothesisEngine.GenerateHypothesisAsync(
    "Tasks with structured steps succeed more often");
// Result: Hypothesis with confidence 0.7

// Design experiment
var experiment = await hypothesisEngine.DesignExperimentAsync(hypothesis.Value);
// Result: Experiment with test steps and expected outcomes

// Test hypothesis
var testResult = await hypothesisEngine.TestHypothesisAsync(
    hypothesis.Value, 
    experiment.Value);
// Result: Updated hypothesis with adjusted confidence

// Abductive reasoning
var best = await hypothesisEngine.AbductiveReasoningAsync(
    new[] { "Observation 1", "Observation 2", "Observation 3" });
// Result: Best explanation that accounts for all observations
```

### 9. Curiosity Engine

Intrinsic motivation and autonomous exploration.

#### ICuriosityEngine Interface

```csharp
public interface ICuriosityEngine
{
    Task<double> ComputeNoveltyAsync(Plan plan, CancellationToken ct = default);
    
    Task<Result<Plan, string>> GenerateExploratoryPlanAsync(CancellationToken ct = default);
    
    Task<bool> ShouldExploreAsync(string? currentGoal = null, CancellationToken ct = default);
    
    Task<List<ExplorationOpportunity>> IdentifyExplorationOpportunitiesAsync(
        int maxOpportunities = 5, CancellationToken ct = default);
    
    Task<double> EstimateInformationGainAsync(string explorationDescription, CancellationToken ct = default);
    
    void RecordExploration(Plan plan, ExecutionResult execution, double actualNovelty);
}
```

#### Key Features

- **Novelty Detection**: Computes how different a plan is from past experiences
- **Exploration Planning**: Generates plans for learning new areas
- **Exploration/Exploitation Balance**: Decides when to explore vs exploit
- **Opportunity Identification**: Finds unexplored areas with high learning potential
- **Information Gain Estimation**: Predicts learning value of exploration

#### Example

```csharp
var curiosityEngine = new CuriosityEngine(llm, memory, skills, safety);

// Check novelty
var novelty = await curiosityEngine.ComputeNoveltyAsync(plan);
// Result: 0.85 (highly novel)

// Decide whether to explore
var shouldExplore = await curiosityEngine.ShouldExploreAsync();
if (shouldExplore)
{
    // Generate exploratory plan
    var expPlan = await curiosityEngine.GenerateExploratoryPlanAsync();
    // Result: Safe, structured exploration plan
}

// Identify opportunities
var opportunities = await curiosityEngine.IdentifyExplorationOpportunitiesAsync();
// Result: List of areas with high novelty and information gain

// Estimate information gain
var gain = await curiosityEngine.EstimateInformationGainAsync("quantum computing");
// Result: 0.9 (high potential learning)
```

## Future Enhancements

The following capabilities are planned for future releases:

### Phase 4: Advanced Capabilities
- **Meta-Learning**: Learn how to learn more effectively
- **Few-Shot Adaptation**: Quickly adapt to new tasks with minimal examples
- **Causal Reasoning**: Understand cause-and-effect relationships
- **Counterfactual Thinking**: Reason about "what if" scenarios

## Safety Considerations

### Skill Extraction Boundaries

The skill extraction system operates within defined safety boundaries:

1. **Quality Gating**: Only high-quality executions (>0.8) are extracted
2. **Capacity Limits**: Maximum skill complexity and registry size
3. **Human Oversight**: All extracted skills can be reviewed and removed
4. **Versioning**: Skill evolution is tracked for auditing

### Memory Management

Memory operations are bounded:

1. **Capacity Limits**: Prevent unbounded memory growth
2. **Importance Thresholds**: Maintain memory quality through forgetting
3. **Consolidation Intervals**: Prevent excessive processing overhead

## References

- **Documentation Index**: See [README.md](README.md) for complete documentation catalog
- **Phase Implementations**: See [archive/](archive/) for detailed implementation summaries
- **Phase 1 Example**: `src/Ouroboros.Examples/Examples/SelfImprovingAgentExample.cs`
- **Phase 2 Example**: `src/Ouroboros.Examples/Examples/Phase2MetacognitionExample.cs`
- **Phase 3 Example**: `src/Ouroboros.Examples/Examples/Phase3EmergentIntelligenceExample.cs`
- **Phase 1 Tests**: `src/Ouroboros.Tests/Tests/SkillExtractionTests.cs`
- **Phase 1 Tests**: `src/Ouroboros.Tests/Tests/PersistentMemoryStoreTests.cs`
- **Phase 2 Tests**: `src/Ouroboros.Tests/Tests/Phase2MetacognitionTests.cs`
- **Phase 3 Tests**: `src/Ouroboros.Tests/Tests/Phase3EmergentIntelligenceTests.cs`

## Contributing

When extending the self-improvement capabilities:

1. Follow monadic error handling patterns (`Result<T, E>`)
2. Maintain immutability in data structures
3. Add comprehensive tests for new learning behaviors
4. Document safety boundaries and limitations
5. Use functional composition for skill operations
