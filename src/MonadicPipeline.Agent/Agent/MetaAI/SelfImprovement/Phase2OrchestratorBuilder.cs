// <copyright file="Phase2OrchestratorBuilder.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Enhanced builder for creating orchestrator with Phase 2 metacognitive capabilities.
/// </summary>
public sealed class Phase2OrchestratorBuilder
{
    private IChatCompletionModel? llm;
    private ToolRegistry? tools;
    private IMemoryStore? memory;
    private ISkillRegistry? skills;
    private IUncertaintyRouter? router;
    private ISafetyGuard? safety;
    private ISkillExtractor? skillExtractor;
    private ICapabilityRegistry? capabilityRegistry;
    private IGoalHierarchy? goalHierarchy;
    private ISelfEvaluator? selfEvaluator;
    private double confidenceThreshold = 0.7;
    private SkillExtractionConfig? skillConfig;
    private PersistentMemoryConfig? memoryConfig;
    private CapabilityRegistryConfig? capabilityConfig;
    private GoalHierarchyConfig? goalConfig;
    private SelfEvaluatorConfig? evaluatorConfig;

    /// <summary>
    /// Sets the language model for the orchestrator.
    /// </summary>
    /// <returns></returns>
    public Phase2OrchestratorBuilder WithLLM(IChatCompletionModel llm)
    {
        this.llm = llm ?? throw new ArgumentNullException(nameof(llm));
        return this;
    }

    /// <summary>
    /// Sets the tool registry.
    /// </summary>
    /// <returns></returns>
    public Phase2OrchestratorBuilder WithTools(ToolRegistry tools)
    {
        this.tools = tools ?? throw new ArgumentNullException(nameof(tools));
        return this;
    }

    /// <summary>
    /// Sets the memory store (uses PersistentMemoryStore by default).
    /// </summary>
    /// <returns></returns>
    public Phase2OrchestratorBuilder WithMemory(IMemoryStore memory)
    {
        this.memory = memory ?? throw new ArgumentNullException(nameof(memory));
        return this;
    }

    /// <summary>
    /// Configures persistent memory with custom settings.
    /// </summary>
    /// <returns></returns>
    public Phase2OrchestratorBuilder WithMemoryConfig(PersistentMemoryConfig config)
    {
        this.memoryConfig = config ?? throw new ArgumentNullException(nameof(config));
        return this;
    }

    /// <summary>
    /// Sets the skill registry.
    /// </summary>
    /// <returns></returns>
    public Phase2OrchestratorBuilder WithSkills(ISkillRegistry skills)
    {
        this.skills = skills ?? throw new ArgumentNullException(nameof(skills));
        return this;
    }

    /// <summary>
    /// Sets the skill extraction configuration.
    /// </summary>
    /// <returns></returns>
    public Phase2OrchestratorBuilder WithSkillExtractionConfig(SkillExtractionConfig config)
    {
        this.skillConfig = config ?? throw new ArgumentNullException(nameof(config));
        return this;
    }

    /// <summary>
    /// Sets the capability registry configuration.
    /// </summary>
    /// <returns></returns>
    public Phase2OrchestratorBuilder WithCapabilityConfig(CapabilityRegistryConfig config)
    {
        this.capabilityConfig = config ?? throw new ArgumentNullException(nameof(config));
        return this;
    }

    /// <summary>
    /// Sets the goal hierarchy configuration.
    /// </summary>
    /// <returns></returns>
    public Phase2OrchestratorBuilder WithGoalConfig(GoalHierarchyConfig config)
    {
        this.goalConfig = config ?? throw new ArgumentNullException(nameof(config));
        return this;
    }

    /// <summary>
    /// Sets the self-evaluator configuration.
    /// </summary>
    /// <returns></returns>
    public Phase2OrchestratorBuilder WithEvaluatorConfig(SelfEvaluatorConfig config)
    {
        this.evaluatorConfig = config ?? throw new ArgumentNullException(nameof(config));
        return this;
    }

    /// <summary>
    /// Sets the safety guard.
    /// </summary>
    /// <returns></returns>
    public Phase2OrchestratorBuilder WithSafety(ISafetyGuard safety)
    {
        this.safety = safety ?? throw new ArgumentNullException(nameof(safety));
        return this;
    }

    /// <summary>
    /// Sets the uncertainty router confidence threshold.
    /// </summary>
    /// <returns></returns>
    public Phase2OrchestratorBuilder WithConfidenceThreshold(double threshold)
    {
        this.confidenceThreshold = Math.Clamp(threshold, 0.0, 1.0);
        return this;
    }

    /// <summary>
    /// Builds the orchestrator with all Phase 2 components.
    /// </summary>
    /// <returns>A tuple containing the orchestrator and all Phase 2 components.</returns>
    public (
        IMetaAIPlannerOrchestrator Orchestrator,
        ICapabilityRegistry CapabilityRegistry,
        IGoalHierarchy GoalHierarchy,
        ISelfEvaluator SelfEvaluator) Build()
    {
        // Validate required components
        if (this.llm == null)
        {
            throw new InvalidOperationException("LLM must be set before building");
        }

        if (this.tools == null)
        {
            throw new InvalidOperationException("Tools must be set before building");
        }

        // Initialize defaults if not provided
        this.safety ??= new SafetyGuard();
        this.skills ??= new SkillRegistry();
        this.memory ??= new PersistentMemoryStore(config: this.memoryConfig);

        // Create Phase 1 components
        this.skillExtractor ??= new SkillExtractor(this.llm, this.skills);
        this.router ??= new UncertaintyRouter(null!, this.confidenceThreshold);

        // Create Phase 2 components
        this.capabilityRegistry ??= new CapabilityRegistry(this.llm, this.tools, this.capabilityConfig);
        this.goalHierarchy ??= new GoalHierarchy(this.llm, this.safety, this.goalConfig);

        // Create orchestrator
        var orchestrator = new MetaAIPlannerOrchestrator(
            this.llm,
            this.tools,
            this.memory,
            this.skills,
            this.router,
            this.safety,
            this.skillExtractor);

        // Create self-evaluator (requires orchestrator)
        this.selfEvaluator ??= new SelfEvaluator(
            this.llm,
            this.capabilityRegistry,
            this.skills,
            this.memory,
            orchestrator,
            this.evaluatorConfig);

        return (orchestrator, this.capabilityRegistry, this.goalHierarchy, this.selfEvaluator);
    }

    /// <summary>
    /// Creates a default Phase 2 orchestrator setup.
    /// </summary>
    /// <returns></returns>
    public static (
        IMetaAIPlannerOrchestrator Orchestrator,
        ICapabilityRegistry CapabilityRegistry,
        IGoalHierarchy GoalHierarchy,
        ISelfEvaluator SelfEvaluator) CreateDefault(IChatCompletionModel llm)
    {
        var tools = ToolRegistry.CreateDefault();

        return new Phase2OrchestratorBuilder()
            .WithLLM(llm)
            .WithTools(tools)
            .Build();
    }
}
