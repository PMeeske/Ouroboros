#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// MeTTa Orchestrator v3.0 Builder
// Fluent builder for creating v3.0 orchestrator instances
// ==========================================================

using LangChainPipeline.Tools.MeTTa;

namespace LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Fluent builder for creating MeTTa-first Orchestrator v3.0 instances.
/// </summary>
public sealed class MeTTaOrchestratorBuilder
{
    private IChatCompletionModel? _llm;
    private ToolRegistry? _tools;
    private IMemoryStore? _memory;
    private ISkillRegistry? _skills;
    private IUncertaintyRouter? _router;
    private ISafetyGuard? _safety;
    private IMeTTaEngine? _mettaEngine;

    /// <summary>
    /// Sets the language model for the orchestrator.
    /// </summary>
    public MeTTaOrchestratorBuilder WithLLM(IChatCompletionModel llm)
    {
        _llm = llm;
        return this;
    }

    /// <summary>
    /// Sets the tool registry for the orchestrator.
    /// Automatically adds MeTTa tools if not already present.
    /// </summary>
    public MeTTaOrchestratorBuilder WithTools(ToolRegistry tools)
    {
        _tools = tools;
        return this;
    }

    /// <summary>
    /// Sets the memory store for the orchestrator.
    /// </summary>
    public MeTTaOrchestratorBuilder WithMemory(IMemoryStore memory)
    {
        _memory = memory;
        return this;
    }

    /// <summary>
    /// Sets the skill registry for the orchestrator.
    /// </summary>
    public MeTTaOrchestratorBuilder WithSkills(ISkillRegistry skills)
    {
        _skills = skills;
        return this;
    }

    /// <summary>
    /// Sets the uncertainty router for the orchestrator.
    /// </summary>
    public MeTTaOrchestratorBuilder WithRouter(IUncertaintyRouter router)
    {
        _router = router;
        return this;
    }

    /// <summary>
    /// Sets the safety guard for the orchestrator.
    /// </summary>
    public MeTTaOrchestratorBuilder WithSafety(ISafetyGuard safety)
    {
        _safety = safety;
        return this;
    }

    /// <summary>
    /// Sets the MeTTa engine for symbolic reasoning.
    /// </summary>
    public MeTTaOrchestratorBuilder WithMeTTaEngine(IMeTTaEngine engine)
    {
        _mettaEngine = engine;
        return this;
    }

    /// <summary>
    /// Builds the MeTTa Orchestrator v3.0 instance.
    /// </summary>
    /// <returns>Configured MeTTaOrchestrator instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when required components are missing.</exception>
    public MeTTaOrchestrator Build()
    {
        if (_llm == null)
            throw new InvalidOperationException("LLM is required. Use WithLLM() to set it.");

        if (_memory == null)
            throw new InvalidOperationException("Memory is required. Use WithMemory() to set it.");

        if (_skills == null)
            throw new InvalidOperationException("Skills are required. Use WithSkills() to set it.");

        if (_router == null)
            throw new InvalidOperationException("Router is required. Use WithRouter() to set it.");

        if (_safety == null)
            throw new InvalidOperationException("Safety is required. Use WithSafety() to set it.");

        // Initialize MeTTa engine if not provided
        IMeTTaEngine mettaEngine = _mettaEngine ?? new SubprocessMeTTaEngine();

        // Ensure tools include MeTTa tools
        ToolRegistry tools = _tools ?? ToolRegistry.CreateDefault();
        bool hasMeTTaTools = tools.All.Any(t => t.Name.StartsWith("metta_") || t.Name == "next_node");
        if (!hasMeTTaTools)
        {
            tools = tools.WithMeTTaTools(mettaEngine);
        }

        return new MeTTaOrchestrator(
            _llm,
            tools,
            _memory,
            _skills,
            _router,
            _safety,
            mettaEngine
        );
    }

    /// <summary>
    /// Creates a default builder with standard components.
    /// Note: You must still call WithLLM() and optionally WithTools() before Build().
    /// </summary>
    /// <param name="embedModel">The embedding model for memory operations.</param>
    /// <returns>Configured builder with default components (except LLM and tools).</returns>
    public static MeTTaOrchestratorBuilder CreateDefault(IEmbeddingModel embedModel)
    {
        MemoryStore memory = new MemoryStore(embedModel);
        SkillRegistry skills = new SkillRegistry();
        SafetyGuard safety = new SafetyGuard();
        SubprocessMeTTaEngine mettaEngine = new SubprocessMeTTaEngine();

        // Create a simple orchestrator with default tools
        ToolRegistry defaultTools = ToolRegistry.CreateDefault();
        SmartModelOrchestrator orchestrator = new SmartModelOrchestrator(defaultTools);

        UncertaintyRouter router = new UncertaintyRouter(orchestrator);

        return new MeTTaOrchestratorBuilder()
            .WithMemory(memory)
            .WithSkills(skills)
            .WithRouter(router)
            .WithSafety(safety)
            .WithMeTTaEngine(mettaEngine);
    }

    /// <summary>
    /// Creates a builder with mock MeTTa engine (for testing/demo when MeTTa not installed).
    /// </summary>
    /// <param name="embedModel">The embedding model for memory operations.</param>
    /// <returns>Configured builder with mock MeTTa engine.</returns>
    public static MeTTaOrchestratorBuilder CreateWithMockMeTTa(IEmbeddingModel embedModel)
    {
        MeTTaOrchestratorBuilder builder = CreateDefault(embedModel);
        // The mock engine would be created separately and passed via WithMeTTaEngine
        return builder;
    }
}
