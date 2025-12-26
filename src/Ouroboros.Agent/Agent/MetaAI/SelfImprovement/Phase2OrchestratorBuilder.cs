#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Phase 2 Enhanced Orchestrator Builder
// Fluent builder for orchestrator with metacognitive components
// ==========================================================

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Enhanced builder for creating orchestrator with Phase 2 metacognitive capabilities.
/// </summary>
public sealed class Phase2OrchestratorBuilder
{
    private IChatCompletionModel? _llm;
    private ToolRegistry? _tools;
    private IMemoryStore? _memory;
    private ISkillRegistry? _skills;
    private IUncertaintyRouter? _router;
    private ISafetyGuard? _safety;
    private ISkillExtractor? _skillExtractor;
    private ICapabilityRegistry? _capabilityRegistry;
    private IGoalHierarchy? _goalHierarchy;
    private ISelfEvaluator? _selfEvaluator;
    private double _confidenceThreshold = 0.7;
    private SkillExtractionConfig? _skillConfig;
    private PersistentMemoryConfig? _memoryConfig;
    private CapabilityRegistryConfig? _capabilityConfig;
    private GoalHierarchyConfig? _goalConfig;
    private SelfEvaluatorConfig? _evaluatorConfig;

    /// <summary>
    /// Sets the language model for the orchestrator.
    /// </summary>
    public Phase2OrchestratorBuilder WithLLM(IChatCompletionModel llm)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        return this;
    }

    /// <summary>
    /// Sets the tool registry.
    /// </summary>
    public Phase2OrchestratorBuilder WithTools(ToolRegistry tools)
    {
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        return this;
    }

    /// <summary>
    /// Sets the memory store (uses PersistentMemoryStore by default).
    /// </summary>
    public Phase2OrchestratorBuilder WithMemory(IMemoryStore memory)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        return this;
    }

    /// <summary>
    /// Configures persistent memory with custom settings.
    /// </summary>
    public Phase2OrchestratorBuilder WithMemoryConfig(PersistentMemoryConfig config)
    {
        _memoryConfig = config ?? throw new ArgumentNullException(nameof(config));
        return this;
    }

    /// <summary>
    /// Sets the skill registry.
    /// </summary>
    public Phase2OrchestratorBuilder WithSkills(ISkillRegistry skills)
    {
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        return this;
    }

    /// <summary>
    /// Sets the skill extraction configuration.
    /// </summary>
    public Phase2OrchestratorBuilder WithSkillExtractionConfig(SkillExtractionConfig config)
    {
        _skillConfig = config ?? throw new ArgumentNullException(nameof(config));
        return this;
    }

    /// <summary>
    /// Sets the capability registry configuration.
    /// </summary>
    public Phase2OrchestratorBuilder WithCapabilityConfig(CapabilityRegistryConfig config)
    {
        _capabilityConfig = config ?? throw new ArgumentNullException(nameof(config));
        return this;
    }

    /// <summary>
    /// Sets the goal hierarchy configuration.
    /// </summary>
    public Phase2OrchestratorBuilder WithGoalConfig(GoalHierarchyConfig config)
    {
        _goalConfig = config ?? throw new ArgumentNullException(nameof(config));
        return this;
    }

    /// <summary>
    /// Sets the self-evaluator configuration.
    /// </summary>
    public Phase2OrchestratorBuilder WithEvaluatorConfig(SelfEvaluatorConfig config)
    {
        _evaluatorConfig = config ?? throw new ArgumentNullException(nameof(config));
        return this;
    }

    /// <summary>
    /// Sets the safety guard.
    /// </summary>
    public Phase2OrchestratorBuilder WithSafety(ISafetyGuard safety)
    {
        _safety = safety ?? throw new ArgumentNullException(nameof(safety));
        return this;
    }

    /// <summary>
    /// Sets the uncertainty router confidence threshold.
    /// </summary>
    public Phase2OrchestratorBuilder WithConfidenceThreshold(double threshold)
    {
        _confidenceThreshold = Math.Clamp(threshold, 0.0, 1.0);
        return this;
    }

    /// <summary>
    /// Builds the orchestrator with all Phase 2 components.
    /// </summary>
    /// <returns>A tuple containing the orchestrator and all Phase 2 components</returns>
    public (
        IMetaAIPlannerOrchestrator Orchestrator,
        ICapabilityRegistry CapabilityRegistry,
        IGoalHierarchy GoalHierarchy,
        ISelfEvaluator SelfEvaluator) Build()
    {
        // Validate required components
        if (_llm == null)
            throw new InvalidOperationException("LLM must be set before building");
        if (_tools == null)
            throw new InvalidOperationException("Tools must be set before building");

        // Initialize defaults if not provided
        _safety ??= new SafetyGuard();
        _skills ??= new SkillRegistry();
        _memory ??= new PersistentMemoryStore(config: _memoryConfig);

        // Create Phase 1 components
        _skillExtractor ??= new SkillExtractor(_llm, _skills);
        _router ??= new UncertaintyRouter(null!, _confidenceThreshold);

        // Create Phase 2 components
        _capabilityRegistry ??= new CapabilityRegistry(_llm, _tools, _capabilityConfig);
        _goalHierarchy ??= new GoalHierarchy(_llm, _safety, _goalConfig);

        // Create orchestrator
        MetaAIPlannerOrchestrator orchestrator = new MetaAIPlannerOrchestrator(
            _llm,
            _tools,
            _memory,
            _skills,
            _router,
            _safety,
            _skillExtractor);

        // Create self-evaluator (requires orchestrator)
        _selfEvaluator ??= new SelfEvaluator(
            _llm,
            _capabilityRegistry,
            _skills,
            _memory,
            orchestrator,
            _evaluatorConfig);

        return (orchestrator, _capabilityRegistry, _goalHierarchy, _selfEvaluator);
    }

    /// <summary>
    /// Creates a default Phase 2 orchestrator setup.
    /// </summary>
    public static (
        IMetaAIPlannerOrchestrator Orchestrator,
        ICapabilityRegistry CapabilityRegistry,
        IGoalHierarchy GoalHierarchy,
        ISelfEvaluator SelfEvaluator) CreateDefault(IChatCompletionModel llm)
    {
        ToolRegistry tools = ToolRegistry.CreateDefault();

        return new Phase2OrchestratorBuilder()
            .WithLLM(llm)
            .WithTools(tools)
            .Build();
    }
}
