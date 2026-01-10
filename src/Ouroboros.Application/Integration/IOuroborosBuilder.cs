// <copyright file="IOuroborosBuilder.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Builder interface for fluent configuration of the Ouroboros system.
/// Follows the builder pattern with method chaining for composable configuration.
/// </summary>
public interface IOuroborosBuilder
{
    /// <summary>Gets the service collection for dependency injection.</summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Configures episodic memory with the specified options.
    /// </summary>
    /// <param name="configure">Action to configure episodic memory options.</param>
    /// <returns>The builder for method chaining.</returns>
    IOuroborosBuilder WithEpisodicMemory(Action<EpisodicMemoryOptions>? configure = null);

    /// <summary>
    /// Configures adapter learning with the specified options.
    /// </summary>
    /// <param name="configure">Action to configure adapter learning options.</param>
    /// <returns>The builder for method chaining.</returns>
    IOuroborosBuilder WithAdapterLearning(Action<AdapterLearningOptions>? configure = null);

    /// <summary>
    /// Configures MeTTa reasoning with the specified options.
    /// </summary>
    /// <param name="configure">Action to configure MeTTa reasoning options.</param>
    /// <returns>The builder for method chaining.</returns>
    IOuroborosBuilder WithMeTTaReasoning(Action<MeTTaReasoningOptions>? configure = null);

    /// <summary>
    /// Configures hierarchical planning with the specified options.
    /// </summary>
    /// <param name="configure">Action to configure hierarchical planning options.</param>
    /// <returns>The builder for method chaining.</returns>
    IOuroborosBuilder WithHierarchicalPlanning(Action<HierarchicalPlanningOptions>? configure = null);

    /// <summary>
    /// Configures reflection with the specified options.
    /// </summary>
    /// <param name="configure">Action to configure reflection options.</param>
    /// <returns>The builder for method chaining.</returns>
    IOuroborosBuilder WithReflection(Action<ReflectionOptions>? configure = null);

    /// <summary>
    /// Configures program synthesis with the specified options.
    /// </summary>
    /// <param name="configure">Action to configure program synthesis options.</param>
    /// <returns>The builder for method chaining.</returns>
    IOuroborosBuilder WithProgramSynthesis(Action<ProgramSynthesisOptions>? configure = null);

    /// <summary>
    /// Configures world model with the specified options.
    /// </summary>
    /// <param name="configure">Action to configure world model options.</param>
    /// <returns>The builder for method chaining.</returns>
    IOuroborosBuilder WithWorldModel(Action<WorldModelOptions>? configure = null);

    /// <summary>
    /// Configures multi-agent coordination with the specified options.
    /// </summary>
    /// <param name="configure">Action to configure multi-agent options.</param>
    /// <returns>The builder for method chaining.</returns>
    IOuroborosBuilder WithMultiAgent(Action<MultiAgentOptions>? configure = null);

    /// <summary>
    /// Configures causal reasoning with the specified options.
    /// </summary>
    /// <param name="configure">Action to configure causal reasoning options.</param>
    /// <returns>The builder for method chaining.</returns>
    IOuroborosBuilder WithCausalReasoning(Action<CausalReasoningOptions>? configure = null);

    /// <summary>
    /// Configures meta-learning with the specified options.
    /// </summary>
    /// <param name="configure">Action to configure meta-learning options.</param>
    /// <returns>The builder for method chaining.</returns>
    IOuroborosBuilder WithMetaLearning(Action<MetaLearningOptions>? configure = null);

    /// <summary>
    /// Configures embodied agent with the specified options.
    /// </summary>
    /// <param name="configure">Action to configure embodied agent options.</param>
    /// <returns>The builder for method chaining.</returns>
    IOuroborosBuilder WithEmbodiedAgent(Action<EmbodiedAgentOptions>? configure = null);

    /// <summary>
    /// Configures consciousness scaffold with the specified options.
    /// </summary>
    /// <param name="configure">Action to configure consciousness options.</param>
    /// <returns>The builder for method chaining.</returns>
    IOuroborosBuilder WithConsciousness(Action<ConsciousnessOptions>? configure = null);

    /// <summary>
    /// Configures cognitive loop with the specified options.
    /// </summary>
    /// <param name="configure">Action to configure cognitive loop options.</param>
    /// <returns>The builder for method chaining.</returns>
    IOuroborosBuilder WithCognitiveLoop(Action<CognitiveLoopOptions>? configure = null);

    /// <summary>
    /// Builds and registers all configured services.
    /// </summary>
    /// <returns>The service collection with all Ouroboros services registered.</returns>
    IServiceCollection Build();
}

// ===== Configuration Option Records =====

/// <summary>Options for episodic memory configuration.</summary>
public sealed record EpisodicMemoryOptions(
    string VectorStoreType = "InMemory",
    int MaxEpisodes = 10000,
    double SimilarityThreshold = 0.7);

/// <summary>Options for adapter learning configuration.</summary>
public sealed record AdapterLearningOptions(
    int Rank = 8,
    double LearningRate = 0.0001,
    int BatchSize = 32);

/// <summary>Options for MeTTa reasoning configuration.</summary>
public sealed record MeTTaReasoningOptions(
    string HyperonPath = "",
    int MaxInferenceSteps = 100,
    double ConfidenceThreshold = 0.7);

/// <summary>Options for hierarchical planning configuration.</summary>
public sealed record HierarchicalPlanningOptions(
    int MaxDepth = 10,
    int MinStepsForDecomposition = 3,
    double ComplexityThreshold = 0.7);

/// <summary>Options for reflection configuration.</summary>
public sealed record ReflectionOptions(
    bool EnableCodeReflection = true,
    bool EnablePerformanceReflection = true,
    int ReflectionDepth = 3);

/// <summary>Options for program synthesis configuration.</summary>
public sealed record ProgramSynthesisOptions(
    string TargetLanguage = "CSharp",
    int MaxSynthesisAttempts = 5,
    bool EnableVerification = true);

/// <summary>Options for world model configuration.</summary>
public sealed record WorldModelOptions(
    int StateSpaceSize = 128,
    int ActionSpaceSize = 64,
    double DiscountFactor = 0.99);

/// <summary>Options for multi-agent coordination configuration.</summary>
public sealed record MultiAgentOptions(
    int MaxAgents = 10,
    string CoordinationStrategy = "Hierarchical",
    bool EnableCommunication = true);

/// <summary>Options for causal reasoning configuration.</summary>
public sealed record CausalReasoningOptions(
    bool EnableInterventions = true,
    bool EnableCounterfactuals = true,
    int MaxCausalDepth = 5);

/// <summary>Options for meta-learning configuration.</summary>
public sealed record MetaLearningOptions(
    string Algorithm = "MAML",
    int InnerSteps = 5,
    double MetaLearningRate = 0.001);

/// <summary>Options for embodied agent configuration.</summary>
public sealed record EmbodiedAgentOptions(
    string EnvironmentType = "Simulated",
    int SensorDimensions = 64,
    int ActuatorDimensions = 32);

/// <summary>Options for consciousness scaffold configuration.</summary>
public sealed record ConsciousnessOptions(
    int MaxWorkspaceSize = 100,
    TimeSpan DefaultItemLifetime = default,
    double MinAttentionThreshold = 0.5)
{
    /// <summary>Gets default consciousness options.</summary>
    public static ConsciousnessOptions Default => new(
        DefaultItemLifetime: TimeSpan.FromMinutes(5));
}

/// <summary>Options for cognitive loop configuration.</summary>
public sealed record CognitiveLoopOptions(
    TimeSpan CycleInterval = default,
    bool AutoStart = false,
    int MaxCyclesPerRun = -1)
{
    /// <summary>Gets default cognitive loop options.</summary>
    public static CognitiveLoopOptions Default => new(
        CycleInterval: TimeSpan.FromSeconds(1));
}
