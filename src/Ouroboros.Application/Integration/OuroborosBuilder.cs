// <copyright file="OuroborosBuilder.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Builder implementation for fluent configuration of the Ouroboros system.
/// Implements the builder pattern with method chaining for composable configuration.
/// Thread-safe singleton instance per service collection.
/// </summary>
public sealed class OuroborosBuilder : IOuroborosBuilder
{
    private readonly Dictionary<Type, object> _options = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="OuroborosBuilder"/> class.
    /// </summary>
    /// <param name="services">The service collection for dependency injection.</param>
    public OuroborosBuilder(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <inheritdoc/>
    public IServiceCollection Services { get; }

    /// <inheritdoc/>
    public IOuroborosBuilder WithEpisodicMemory(Action<EpisodicMemoryOptions>? configure = null)
    {
        var options = new EpisodicMemoryOptions();
        configure?.Invoke(options);
        _options[typeof(EpisodicMemoryOptions)] = options;
        return this;
    }

    /// <inheritdoc/>
    public IOuroborosBuilder WithAdapterLearning(Action<AdapterLearningOptions>? configure = null)
    {
        var options = new AdapterLearningOptions();
        configure?.Invoke(options);
        _options[typeof(AdapterLearningOptions)] = options;
        return this;
    }

    /// <inheritdoc/>
    public IOuroborosBuilder WithMeTTaReasoning(Action<MeTTaReasoningOptions>? configure = null)
    {
        var options = new MeTTaReasoningOptions();
        configure?.Invoke(options);
        _options[typeof(MeTTaReasoningOptions)] = options;
        return this;
    }

    /// <inheritdoc/>
    public IOuroborosBuilder WithHierarchicalPlanning(Action<HierarchicalPlanningOptions>? configure = null)
    {
        var options = new HierarchicalPlanningOptions();
        configure?.Invoke(options);
        _options[typeof(HierarchicalPlanningOptions)] = options;
        return this;
    }

    /// <inheritdoc/>
    public IOuroborosBuilder WithReflection(Action<ReflectionOptions>? configure = null)
    {
        var options = new ReflectionOptions();
        configure?.Invoke(options);
        _options[typeof(ReflectionOptions)] = options;
        return this;
    }

    /// <inheritdoc/>
    public IOuroborosBuilder WithProgramSynthesis(Action<ProgramSynthesisOptions>? configure = null)
    {
        var options = new ProgramSynthesisOptions();
        configure?.Invoke(options);
        _options[typeof(ProgramSynthesisOptions)] = options;
        return this;
    }

    /// <inheritdoc/>
    public IOuroborosBuilder WithWorldModel(Action<WorldModelOptions>? configure = null)
    {
        var options = new WorldModelOptions();
        configure?.Invoke(options);
        _options[typeof(WorldModelOptions)] = options;
        return this;
    }

    /// <inheritdoc/>
    public IOuroborosBuilder WithMultiAgent(Action<MultiAgentOptions>? configure = null)
    {
        var options = new MultiAgentOptions();
        configure?.Invoke(options);
        _options[typeof(MultiAgentOptions)] = options;
        return this;
    }

    /// <inheritdoc/>
    public IOuroborosBuilder WithCausalReasoning(Action<CausalReasoningOptions>? configure = null)
    {
        var options = new CausalReasoningOptions();
        configure?.Invoke(options);
        _options[typeof(CausalReasoningOptions)] = options;
        return this;
    }

    /// <inheritdoc/>
    public IOuroborosBuilder WithMetaLearning(Action<MetaLearningOptions>? configure = null)
    {
        var options = new MetaLearningOptions();
        configure?.Invoke(options);
        _options[typeof(MetaLearningOptions)] = options;
        return this;
    }

    /// <inheritdoc/>
    public IOuroborosBuilder WithEmbodiedAgent(Action<EmbodiedAgentOptions>? configure = null)
    {
        var options = new EmbodiedAgentOptions();
        configure?.Invoke(options);
        _options[typeof(EmbodiedAgentOptions)] = options;
        return this;
    }

    /// <inheritdoc/>
    public IOuroborosBuilder WithConsciousness(Action<ConsciousnessOptions>? configure = null)
    {
        var options = ConsciousnessOptions.Default;
        configure?.Invoke(options);
        _options[typeof(ConsciousnessOptions)] = options;
        return this;
    }

    /// <inheritdoc/>
    public IOuroborosBuilder WithCognitiveLoop(Action<CognitiveLoopOptions>? configure = null)
    {
        var options = CognitiveLoopOptions.Default;
        configure?.Invoke(options);
        _options[typeof(CognitiveLoopOptions)] = options;
        return this;
    }

    /// <inheritdoc/>
    public IServiceCollection Build()
    {
        // Register all configured options as singletons
        foreach (var kvp in _options)
        {
            Services.AddSingleton(kvp.Key, kvp.Value);
        }

        return Services;
    }

    /// <summary>
    /// Gets the configured options for a specific type.
    /// </summary>
    /// <typeparam name="TOptions">The options type.</typeparam>
    /// <returns>The configured options or default instance.</returns>
    internal TOptions GetOptions<TOptions>() where TOptions : class, new()
    {
        if (_options.TryGetValue(typeof(TOptions), out var options))
        {
            return (TOptions)options;
        }

        return new TOptions();
    }
}
