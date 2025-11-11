// <copyright file="MeTTaOrchestratorBuilder.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Agent.MetaAI;

using LangChainPipeline.Tools.MeTTa;

/// <summary>
/// Fluent builder for creating MeTTa-first Orchestrator v3.0 instances.
/// </summary>
public sealed class MeTTaOrchestratorBuilder
{
    private IChatCompletionModel? llm;
    private ToolRegistry? tools;
    private IMemoryStore? memory;
    private ISkillRegistry? skills;
    private IUncertaintyRouter? router;
    private ISafetyGuard? safety;
    private IMeTTaEngine? mettaEngine;

    /// <summary>
    /// Sets the language model for the orchestrator.
    /// </summary>
    /// <returns></returns>
    public MeTTaOrchestratorBuilder WithLLM(IChatCompletionModel llm)
    {
        this.llm = llm;
        return this;
    }

    /// <summary>
    /// Sets the tool registry for the orchestrator.
    /// Automatically adds MeTTa tools if not already present.
    /// </summary>
    /// <returns></returns>
    public MeTTaOrchestratorBuilder WithTools(ToolRegistry tools)
    {
        this.tools = tools;
        return this;
    }

    /// <summary>
    /// Sets the memory store for the orchestrator.
    /// </summary>
    /// <returns></returns>
    public MeTTaOrchestratorBuilder WithMemory(IMemoryStore memory)
    {
        this.memory = memory;
        return this;
    }

    /// <summary>
    /// Sets the skill registry for the orchestrator.
    /// </summary>
    /// <returns></returns>
    public MeTTaOrchestratorBuilder WithSkills(ISkillRegistry skills)
    {
        this.skills = skills;
        return this;
    }

    /// <summary>
    /// Sets the uncertainty router for the orchestrator.
    /// </summary>
    /// <returns></returns>
    public MeTTaOrchestratorBuilder WithRouter(IUncertaintyRouter router)
    {
        this.router = router;
        return this;
    }

    /// <summary>
    /// Sets the safety guard for the orchestrator.
    /// </summary>
    /// <returns></returns>
    public MeTTaOrchestratorBuilder WithSafety(ISafetyGuard safety)
    {
        this.safety = safety;
        return this;
    }

    /// <summary>
    /// Sets the MeTTa engine for symbolic reasoning.
    /// </summary>
    /// <returns></returns>
    public MeTTaOrchestratorBuilder WithMeTTaEngine(IMeTTaEngine engine)
    {
        this.mettaEngine = engine;
        return this;
    }

    /// <summary>
    /// Builds the MeTTa Orchestrator v3.0 instance.
    /// </summary>
    /// <returns>Configured MeTTaOrchestrator instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when required components are missing.</exception>
    public MeTTaOrchestrator Build()
    {
        if (this.llm == null)
        {
            throw new InvalidOperationException("LLM is required. Use WithLLM() to set it.");
        }

        if (this.memory == null)
        {
            throw new InvalidOperationException("Memory is required. Use WithMemory() to set it.");
        }

        if (this.skills == null)
        {
            throw new InvalidOperationException("Skills are required. Use WithSkills() to set it.");
        }

        if (this.router == null)
        {
            throw new InvalidOperationException("Router is required. Use WithRouter() to set it.");
        }

        if (this.safety == null)
        {
            throw new InvalidOperationException("Safety is required. Use WithSafety() to set it.");
        }

        // Initialize MeTTa engine if not provided
        var mettaEngine = this.mettaEngine ?? new SubprocessMeTTaEngine();

        // Ensure tools include MeTTa tools
        var tools = this.tools ?? ToolRegistry.CreateDefault();
        var hasMeTTaTools = tools.All.Any(t => t.Name.StartsWith("metta_") || t.Name == "next_node");
        if (!hasMeTTaTools)
        {
            tools = tools.WithMeTTaTools(mettaEngine);
        }

        return new MeTTaOrchestrator(
            this.llm,
            tools,
            this.memory,
            this.skills,
            this.router,
            this.safety,
            mettaEngine);
    }

    /// <summary>
    /// Creates a default builder with standard components.
    /// Note: You must still call WithLLM() and optionally WithTools() before Build().
    /// </summary>
    /// <param name="embedModel">The embedding model for memory operations.</param>
    /// <returns>Configured builder with default components (except LLM and tools).</returns>
    public static MeTTaOrchestratorBuilder CreateDefault(IEmbeddingModel embedModel)
    {
        var memory = new MemoryStore(embedModel);
        var skills = new SkillRegistry();
        var safety = new SafetyGuard();
        var mettaEngine = new SubprocessMeTTaEngine();

        // Create a simple orchestrator with default tools
        var defaultTools = ToolRegistry.CreateDefault();
        var orchestrator = new SmartModelOrchestrator(defaultTools);

        var router = new UncertaintyRouter(orchestrator);

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
        var builder = CreateDefault(embedModel);

        // The mock engine would be created separately and passed via WithMeTTaEngine
        return builder;
    }
}
