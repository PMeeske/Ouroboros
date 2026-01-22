// <copyright file="ICognitiveLoop.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

/// <summary>
/// Interface for cognitive loop.
/// Manages perception-reasoning-action cycle.
/// </summary>
public interface ICognitiveLoop
{
    /// <summary>
    /// Initializes the cognitive loop.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InitializeAsync();

    /// <summary>
    /// Starts the cognitive loop processing.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StartAsync();

    /// <summary>
    /// Stops the cognitive loop processing.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StopAsync();

    /// <summary>
    /// Gets a value indicating whether the loop is running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Executes a single cognitive cycle.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ExecuteCycleAsync();
}
