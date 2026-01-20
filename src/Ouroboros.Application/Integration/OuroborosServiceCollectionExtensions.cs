// <copyright file="OuroborosServiceCollectionExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
// using Microsoft.Extensions.Diagnostics.HealthChecks;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Agent.MetaAI.SelfModel;
// using Ouroboros.Agent.MetaAI.WorldModel; // TODO: Namespace missing after merge conflicts
using Ouroboros.Core.Learning;
// using Ouroboros.Core.Reasoning; // TODO: Namespace missing after merge conflicts
// using Ouroboros.Core.Synthesis; // TODO: Namespace missing after merge conflicts
// using Ouroboros.Domain.Benchmarks; // TODO: Namespace missing after merge conflicts
// using Ouroboros.Domain.Embodied; // TODO: Namespace missing after merge conflicts
using Ouroboros.Domain.MetaLearning;
// using Ouroboros.Domain.MultiAgent; // TODO: Namespace missing after merge conflicts
// using Ouroboros.Domain.Reflection; // TODO: Namespace missing after merge conflicts
using Ouroboros.Pipeline.Memory;
using Ouroboros.Tools.MeTTa;

/// <summary>
/// Extension methods for registering Ouroboros services with dependency injection.
/// Provides both basic and full registration with builder pattern support.
/// </summary>
public static class OuroborosServiceCollectionExtensions
{
    /// <summary>
    /// Adds basic Ouroboros services with default configuration.
    /// Registers core services without advanced features.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOuroboros(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register event bus (singleton for cross-cutting communication)
        services.TryAddSingleton<IEventBus, EventBus>();

        // Note: Actual engine implementations would need to be registered
        // This is a template showing the registration pattern
        // Each engine interface needs its concrete implementation

        // Example pattern (would need actual implementations):
        // services.TryAddSingleton<IEpisodicMemoryEngine, EpisodicMemoryEngine>();
        // services.TryAddSingleton<IAdapterLearningEngine, AdapterLearningEngine>();
        // etc.

        return services;
    }

    /// <summary>
    /// Adds full Ouroboros system with builder-based configuration.
    /// Provides fluent API for configuring all features.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure the Ouroboros builder.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOuroborosFull(
        this IServiceCollection services,
        Action<IOuroborosBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Create builder
        var builder = new OuroborosBuilder(services);

        // Apply configuration
        configure?.Invoke(builder);

        // Register event bus
        services.TryAddSingleton<IEventBus, EventBus>();

        // Register global workspace (required for consciousness scaffold)
        RegisterGlobalWorkspace(services, builder);

        // Register consciousness scaffold
        RegisterConsciousnessScaffold(services, builder);

        // Register cognitive loop
        RegisterCognitiveLoop(services, builder);

        // Register all engine interfaces (Note: implementations must be provided separately)
        RegisterEngineInterfaces(services);

        // Register OuroborosCore as the unified interface
        services.TryAddSingleton<IOuroborosCore, OuroborosCore>();

        // Build to register all options
        builder.Build();

        return services;
    }

    /// <summary>
    /// Adds Ouroboros with custom engine implementations.
    /// Allows injection of mock or custom implementations for testing.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="registerEngines">Action to register engine implementations.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOuroborosWithEngines(
        this IServiceCollection services,
        Action<IServiceCollection> registerEngines)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(registerEngines);

        // Register event bus
        services.TryAddSingleton<IEventBus, EventBus>();

        // Register custom engines
        registerEngines(services);

        // Register infrastructure components
        services.TryAddSingleton<IConsciousnessScaffold, ConsciousnessScaffold>();
        services.TryAddSingleton<ICognitiveLoop, CognitiveLoop>();

        // Register OuroborosCore
        services.TryAddSingleton<IOuroborosCore, OuroborosCore>();

        return services;
    }

    private static void RegisterGlobalWorkspace(
        IServiceCollection services,
        OuroborosBuilder builder)
    {
        // Note: Actual GlobalWorkspace implementation would be registered here
        // For now, we simply skip using the generic GetOptions method
        // services.TryAddSingleton<IGlobalWorkspace>(sp =>
        // {
        //     var policy = new AttentionPolicy(...);
        //     return new GlobalWorkspace(policy);
        // });
    }

    private static void RegisterConsciousnessScaffold(
        IServiceCollection services,
        OuroborosBuilder builder)
    {
        services.TryAddSingleton<IConsciousnessScaffold, ConsciousnessScaffold>();
    }

    private static void RegisterCognitiveLoop(
        IServiceCollection services,
        OuroborosBuilder builder)
    {
        services.TryAddSingleton<ICognitiveLoop, CognitiveLoop>();
    }

    private static void RegisterEngineInterfaces(IServiceCollection services)
    {
        // Note: These registrations require actual implementations
        // This demonstrates the pattern for production usage

        // Tier 1 engines
        // services.TryAddSingleton<IEpisodicMemoryEngine, EpisodicMemoryEngine>();
        // services.TryAddSingleton<IAdapterLearningEngine, AdapterLearningEngine>();
        // services.TryAddSingleton<IAdvancedMeTTaEngine, AdvancedMeTTaEngine>();
        // services.TryAddSingleton<IHierarchicalPlanner, HierarchicalPlanner>();
        // services.TryAddSingleton<IReflectionEngine, ReflectionEngine>();
        // services.TryAddSingleton<IBenchmarkSuite, BenchmarkSuite>();

        // Tier 2 engines
        // services.TryAddSingleton<IProgramSynthesisEngine, ProgramSynthesisEngine>();
        // services.TryAddSingleton<IWorldModelEngine, WorldModelEngine>();
        // services.TryAddSingleton<IMultiAgentCoordinator, MultiAgentCoordinator>();
        // services.TryAddSingleton<ICausalReasoningEngine, CausalReasoningEngine>();

        // Tier 3 engines
        // services.TryAddSingleton<IMetaLearningEngine, MetaLearningEngine>();
        // services.TryAddSingleton<IEmbodiedAgent, EmbodiedAgent>();
    }
}

/// <summary>
/// Extension methods for configuring specific Ouroboros features.
/// </summary>
public static class OuroborosFeatureExtensions
{
    /// <summary>
    /// Adds episodic memory services with configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddEpisodicMemory(
        this IServiceCollection services,
        Action<EpisodicMemoryOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new EpisodicMemoryOptions();
        configure?.Invoke(options);

        services.AddSingleton(options);

        // Register implementation
        // services.TryAddSingleton<IEpisodicMemoryEngine, EpisodicMemoryEngine>();

        return services;
    }

    /// <summary>
    /// Adds consciousness scaffold services with configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddConsciousness(
        this IServiceCollection services,
        Action<ConsciousnessOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = ConsciousnessOptions.Default;
        configure?.Invoke(options);

        services.AddSingleton(options);

        // Register event bus if not already registered
        services.TryAddSingleton<IEventBus, EventBus>();

        // Register global workspace
        // services.TryAddSingleton<IGlobalWorkspace, GlobalWorkspace>();

        // Register consciousness scaffold
        services.TryAddSingleton<IConsciousnessScaffold, ConsciousnessScaffold>();

        return services;
    }

    /// <summary>
    /// Adds cognitive loop services with configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration action.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddCognitiveLoop(
        this IServiceCollection services,
        Action<CognitiveLoopOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = CognitiveLoopOptions.Default;
        configure?.Invoke(options);

        services.AddSingleton(options);

        // Register cognitive loop
        services.TryAddSingleton<ICognitiveLoop, CognitiveLoop>();

        return services;
    }

    /// <summary>
    /// Adds health checks for all Ouroboros subsystems.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOuroborosHealthChecks(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHealthChecks()
            .AddCheck<OuroborosHealthCheck>(
                "ouroboros_system",
                tags: new[] { "ouroboros", "system" })
            .AddCheck<EpisodicMemoryHealthCheck>(
                "ouroboros_episodic_memory",
                tags: new[] { "ouroboros", "memory" })
            .AddCheck<ConsciousnessHealthCheck>(
                "ouroboros_consciousness",
                tags: new[] { "ouroboros", "consciousness" })
            .AddCheck<CognitiveLoopHealthCheck>(
                "ouroboros_cognitive_loop",
                tags: new[] { "ouroboros", "loop" });

        return services;
    }

    /// <summary>
    /// Adds OpenTelemetry instrumentation for Ouroboros operations.
    /// Includes metrics, tracing, and activity tracking.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOuroborosTelemetry(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register telemetry as singleton
        services.TryAddSingleton<OuroborosTelemetry>();

        return services;
    }

    /// <summary>
    /// Adds full Ouroboros system with health checks and telemetry enabled.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureHealthChecks">Optional health check configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOuroborosFullWithMonitoring(
        this IServiceCollection services,
        Action<IHealthChecksBuilder>? configureHealthChecks = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Add full Ouroboros system
        services.AddOuroborosFull();

        // Add health checks
        var healthChecksBuilder = services.AddHealthChecks()
            .AddCheck<OuroborosHealthCheck>(
                "ouroboros_system",
                tags: new[] { "ouroboros", "system", "ready" })
            .AddCheck<EpisodicMemoryHealthCheck>(
                "ouroboros_episodic_memory",
                tags: new[] { "ouroboros", "memory" })
            .AddCheck<ConsciousnessHealthCheck>(
                "ouroboros_consciousness",
                tags: new[] { "ouroboros", "consciousness" })
            .AddCheck<CognitiveLoopHealthCheck>(
                "ouroboros_cognitive_loop",
                tags: new[] { "ouroboros", "loop" });

        // Allow custom health check configuration
        configureHealthChecks?.Invoke(healthChecksBuilder);

        // Add telemetry
        services.TryAddSingleton<OuroborosTelemetry>();

        return services;
    }
}
