// <copyright file="OuroborosBuilder.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Builder implementation for configuring Ouroboros system.
/// Provides fluent API for enabling and configuring features.
/// </summary>
public sealed class OuroborosBuilder : IOuroborosBuilder
{
    private EpisodicMemoryOptions? _episodicMemoryOptions;
    private ConsciousnessOptions? _consciousnessOptions;
    private CognitiveLoopOptions? _cognitiveLoopOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="OuroborosBuilder"/> class.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    public OuroborosBuilder(IServiceCollection services)
    {
        Services = services ?? throw new ArgumentNullException(nameof(services));
    }

    /// <inheritdoc/>
    public IServiceCollection Services { get; }

    /// <inheritdoc/>
    public IOuroborosBuilder WithEpisodicMemory(Action<EpisodicMemoryOptions>? configure = null)
    {
        _episodicMemoryOptions = new EpisodicMemoryOptions();
        configure?.Invoke(_episodicMemoryOptions);
        return this;
    }

    /// <inheritdoc/>
    public IOuroborosBuilder WithConsciousness(Action<ConsciousnessOptions>? configure = null)
    {
        _consciousnessOptions = ConsciousnessOptions.Default;
        configure?.Invoke(_consciousnessOptions);
        return this;
    }

    /// <inheritdoc/>
    public IOuroborosBuilder WithCognitiveLoop(Action<CognitiveLoopOptions>? configure = null)
    {
        _cognitiveLoopOptions = CognitiveLoopOptions.Default;
        configure?.Invoke(_cognitiveLoopOptions);
        return this;
    }

    /// <inheritdoc/>
    public void Build()
    {
        // Register configured options
        if (_episodicMemoryOptions != null)
        {
            Services.AddSingleton(_episodicMemoryOptions);
        }

        if (_consciousnessOptions != null)
        {
            Services.AddSingleton(_consciousnessOptions);
        }

        if (_cognitiveLoopOptions != null)
        {
            Services.AddSingleton(_cognitiveLoopOptions);
        }
    }

    /// <summary>
    /// Gets the configured episodic memory options if available.
    /// </summary>
    /// <returns>The episodic memory options or null if not configured.</returns>
    internal EpisodicMemoryOptions? GetEpisodicMemoryOptions() => _episodicMemoryOptions;

    /// <summary>
    /// Gets the configured consciousness options if available.
    /// </summary>
    /// <returns>The consciousness options or null if not configured.</returns>
    internal ConsciousnessOptions? GetConsciousnessOptions() => _consciousnessOptions;

    /// <summary>
    /// Gets the configured cognitive loop options if available.
    /// </summary>
    /// <returns>The cognitive loop options or null if not configured.</returns>
    internal CognitiveLoopOptions? GetCognitiveLoopOptions() => _cognitiveLoopOptions;
}
