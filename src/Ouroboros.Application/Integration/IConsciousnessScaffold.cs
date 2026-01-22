// <copyright file="IConsciousnessScaffold.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

/// <summary>
/// Interface for consciousness scaffold.
/// Manages global workspace and attention mechanisms.
/// </summary>
public interface IConsciousnessScaffold
{
    /// <summary>
    /// Initializes the consciousness scaffold.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InitializeAsync();

    /// <summary>
    /// Starts the consciousness scaffold processing.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StartAsync();

    /// <summary>
    /// Stops the consciousness scaffold processing.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task StopAsync();

    /// <summary>
    /// Gets a value indicating whether the scaffold is running.
    /// </summary>
    bool IsRunning { get; }
}
