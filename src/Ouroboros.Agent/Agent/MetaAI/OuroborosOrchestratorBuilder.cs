// <copyright file="OuroborosOrchestratorBuilder.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

// ==========================================================
// Ouroboros Orchestrator Builder
// Fluent builder for creating OuroborosOrchestrator instances
// ==========================================================

using Ouroboros.Agent;

namespace LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Fluent builder for creating OuroborosOrchestrator instances.
/// Provides a convenient way to configure all required components.
/// </summary>
public sealed class OuroborosOrchestratorBuilder
{
    private IChatCompletionModel? _llm;
    private ToolRegistry? _tools;
    private IMemoryStore? _memory;
    private ISafetyGuard? _safety;
    private IMeTTaEngine? _mettaEngine;
    private OuroborosAtom? _atom;
    private OrchestratorConfig? _config;

    /// <summary>
    /// Sets the language model for the orchestrator.
    /// </summary>
    /// <param name="llm">The chat completion model.</param>
    /// <returns>This builder for chaining.</returns>
    public OuroborosOrchestratorBuilder WithLLM(IChatCompletionModel llm)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        return this;
    }

    /// <summary>
    /// Sets the tool registry for the orchestrator.
    /// </summary>
    /// <param name="tools">The tool registry.</param>
    /// <returns>This builder for chaining.</returns>
    public OuroborosOrchestratorBuilder WithTools(ToolRegistry tools)
    {
        _tools = tools ?? throw new ArgumentNullException(nameof(tools));
        return this;
    }

    /// <summary>
    /// Sets the memory store for the orchestrator.
    /// </summary>
    /// <param name="memory">The memory store.</param>
    /// <returns>This builder for chaining.</returns>
    public OuroborosOrchestratorBuilder WithMemory(IMemoryStore memory)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        return this;
    }

    /// <summary>
    /// Sets the safety guard for the orchestrator.
    /// </summary>
    /// <param name="safety">The safety guard.</param>
    /// <returns>This builder for chaining.</returns>
    public OuroborosOrchestratorBuilder WithSafety(ISafetyGuard safety)
    {
        _safety = safety ?? throw new ArgumentNullException(nameof(safety));
        return this;
    }

    /// <summary>
    /// Sets the MeTTa engine for symbolic reasoning.
    /// </summary>
    /// <param name="engine">The MeTTa engine.</param>
    /// <returns>This builder for chaining.</returns>
    public OuroborosOrchestratorBuilder WithMeTTaEngine(IMeTTaEngine engine)
    {
        _mettaEngine = engine ?? throw new ArgumentNullException(nameof(engine));
        return this;
    }

    /// <summary>
    /// Sets a pre-configured OuroborosAtom for the orchestrator.
    /// If not provided, a default atom will be created.
    /// </summary>
    /// <param name="atom">The Ouroboros atom.</param>
    /// <returns>This builder for chaining.</returns>
    public OuroborosOrchestratorBuilder WithAtom(OuroborosAtom atom)
    {
        _atom = atom ?? throw new ArgumentNullException(nameof(atom));
        return this;
    }

    /// <summary>
    /// Sets the orchestrator configuration.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <returns>This builder for chaining.</returns>
    public OuroborosOrchestratorBuilder WithConfiguration(OrchestratorConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        return this;
    }

    /// <summary>
    /// Builds the OuroborosOrchestrator instance.
    /// </summary>
    /// <returns>Configured OuroborosOrchestrator instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown when required components are missing.</exception>
    public OuroborosOrchestrator Build()
    {
        if (_llm == null)
        {
            throw new InvalidOperationException("LLM is required. Use WithLLM() to set it.");
        }

        if (_memory == null)
        {
            throw new InvalidOperationException("Memory is required. Use WithMemory() to set it.");
        }

        if (_safety == null)
        {
            throw new InvalidOperationException("Safety is required. Use WithSafety() to set it.");
        }

        // Initialize with defaults if not provided
        IMeTTaEngine mettaEngine = _mettaEngine ?? new SubprocessMeTTaEngine();
        ToolRegistry tools = _tools ?? ToolRegistry.CreateDefault();

        // Ensure tools include MeTTa tools
        bool hasMeTTaTools = tools.All.Any(t => t.Name.StartsWith("metta_") || t.Name == "next_node");
        if (!hasMeTTaTools)
        {
            tools = tools.WithMeTTaTools(mettaEngine);
        }

        return new OuroborosOrchestrator(
            _llm,
            tools,
            _memory,
            _safety,
            mettaEngine,
            _atom,
            _config);
    }

    /// <summary>
    /// Creates a default builder with standard components.
    /// You must still call WithLLM() before Build().
    /// </summary>
    /// <param name="embedModel">The embedding model for memory operations.</param>
    /// <returns>Configured builder with default components (except LLM).</returns>
    public static OuroborosOrchestratorBuilder CreateDefault(IEmbeddingModel embedModel)
    {
        ArgumentNullException.ThrowIfNull(embedModel);

        MemoryStore memory = new MemoryStore(embedModel);
        SafetyGuard safety = new SafetyGuard();
        SubprocessMeTTaEngine mettaEngine = new SubprocessMeTTaEngine();
        ToolRegistry tools = ToolRegistry.CreateDefault().WithMeTTaTools(mettaEngine);

        return new OuroborosOrchestratorBuilder()
            .WithMemory(memory)
            .WithSafety(safety)
            .WithMeTTaEngine(mettaEngine)
            .WithTools(tools);
    }

    /// <summary>
    /// Creates a minimal builder for testing scenarios.
    /// You must call WithLLM() and WithMemory() before Build().
    /// </summary>
    /// <returns>A minimal builder instance.</returns>
    public static OuroborosOrchestratorBuilder CreateMinimal()
    {
        return new OuroborosOrchestratorBuilder()
            .WithSafety(new SafetyGuard());
    }

    /// <summary>
    /// Creates a builder configured for development/testing with a mock MeTTa engine.
    /// </summary>
    /// <param name="embedModel">The embedding model for memory operations.</param>
    /// <param name="mockMeTTaEngine">A mock MeTTa engine for testing.</param>
    /// <returns>Configured builder for testing.</returns>
    public static OuroborosOrchestratorBuilder CreateForTesting(
        IEmbeddingModel embedModel,
        IMeTTaEngine mockMeTTaEngine)
    {
        ArgumentNullException.ThrowIfNull(embedModel);
        ArgumentNullException.ThrowIfNull(mockMeTTaEngine);

        MemoryStore memory = new MemoryStore(embedModel);
        SafetyGuard safety = new SafetyGuard();
        ToolRegistry tools = ToolRegistry.CreateDefault().WithMeTTaTools(mockMeTTaEngine);

        return new OuroborosOrchestratorBuilder()
            .WithMemory(memory)
            .WithSafety(safety)
            .WithMeTTaEngine(mockMeTTaEngine)
            .WithTools(tools);
    }
}
