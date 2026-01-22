// <copyright file="IOuroborosBuilder.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Builder interface for configuring Ouroboros system with fluent API.
/// Provides methods for enabling and configuring various features.
/// </summary>
public interface IOuroborosBuilder
{
    /// <summary>
    /// Gets the service collection being configured.
    /// </summary>
    IServiceCollection Services { get; }

    /// <summary>
    /// Enables episodic memory with optional configuration.
    /// </summary>
    /// <param name="configure">Optional configuration action for episodic memory.</param>
    /// <returns>This builder for method chaining.</returns>
    IOuroborosBuilder WithEpisodicMemory(Action<EpisodicMemoryOptions>? configure = null);

    /// <summary>
    /// Enables consciousness scaffold with optional configuration.
    /// </summary>
    /// <param name="configure">Optional configuration action for consciousness.</param>
    /// <returns>This builder for method chaining.</returns>
    IOuroborosBuilder WithConsciousness(Action<ConsciousnessOptions>? configure = null);

    /// <summary>
    /// Enables cognitive loop with optional configuration.
    /// </summary>
    /// <param name="configure">Optional configuration action for cognitive loop.</param>
    /// <returns>This builder for method chaining.</returns>
    IOuroborosBuilder WithCognitiveLoop(Action<CognitiveLoopOptions>? configure = null);

    /// <summary>
    /// Finalizes the builder configuration and registers all configured services.
    /// </summary>
    void Build();
}
