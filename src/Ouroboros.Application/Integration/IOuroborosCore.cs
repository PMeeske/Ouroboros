// <copyright file="IOuroborosCore.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

/// <summary>
/// Core unified interface for Ouroboros system.
/// Provides access to all major subsystems.
/// </summary>
public interface IOuroborosCore
{
    /// <summary>
    /// Gets the event bus for cross-component communication.
    /// </summary>
    IEventBus EventBus { get; }

    /// <summary>
    /// Gets the consciousness scaffold.
    /// </summary>
    IConsciousnessScaffold ConsciousnessScaffold { get; }

    /// <summary>
    /// Gets the cognitive loop.
    /// </summary>
    ICognitiveLoop CognitiveLoop { get; }

    /// <summary>
    /// Initializes all core subsystems.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InitializeAsync();

    /// <summary>
    /// Starts all core subsystems.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StartAsync();

    /// <summary>
    /// Stops all core subsystems.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StopAsync();

    /// <summary>
    /// Gets a value indicating whether the system is running.
    /// </summary>
    bool IsRunning { get; }
}
