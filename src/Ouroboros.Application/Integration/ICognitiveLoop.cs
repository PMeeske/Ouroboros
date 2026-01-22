// <copyright file="ICognitiveLoop.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

using Ouroboros.Core.Monads;
using Unit = Ouroboros.Core.Learning.Unit;

/// <summary>
/// Interface for the autonomous cognitive loop.
/// Implements continuous perception-reason-act cycles for autonomous operation.
/// </summary>
public interface ICognitiveLoop
{
    /// <summary>
    /// Gets a value indicating whether the cognitive loop is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Starts the autonomous cognitive loop with the given configuration.
    /// Runs continuously until stopped or cancelled.
    /// </summary>
    /// <param name="config">Configuration for the cognitive loop.</param>
    /// <param name="ct">Cancellation token to stop the loop.</param>
    /// <returns>Task representing the running loop.</returns>
    Task RunAsync(CognitiveLoopConfig config, CancellationToken ct = default);

    /// <summary>
    /// Stops the cognitive loop gracefully.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or error message.</returns>
    Task<Result<Unit, string>> StopAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets the current state of the cognitive loop.
    /// </summary>
    /// <returns>Current cognitive state.</returns>
    CognitiveState GetCurrentState();

    /// <summary>
    /// Manually triggers a single cognitive cycle (perception → reasoning → action).
    /// Useful for debugging or controlled execution.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing cycle outcome or error message.</returns>
    Task<Result<CycleOutcome, string>> ExecuteSingleCycleAsync(CancellationToken ct = default);
}

/// <summary>
/// Configuration for the cognitive loop.
/// </summary>
public sealed record CognitiveLoopConfig(
    TimeSpan CycleInterval = default,
    bool EnablePerception = true,
    bool EnableReasoning = true,
    bool EnableAction = true,
    int MaxCyclesPerRun = -1,
    double AttentionThreshold = 0.5)
{
    /// <summary>Gets the default cognitive loop configuration.</summary>
    public static CognitiveLoopConfig Default => new(TimeSpan.FromSeconds(1));
}

/// <summary>
/// Represents the current state of the cognitive loop.
/// </summary>
public sealed record CognitiveState(
    bool IsRunning,
    int CyclesCompleted,
    DateTime LastCycleTime,
    string CurrentPhase,
    List<string> RecentActions);

/// <summary>
/// Represents the outcome of a single cognitive cycle.
/// </summary>
public sealed record CycleOutcome(
    bool Success,
    string Phase,
    TimeSpan Duration,
    List<string> ActionsPerformed,
    Dictionary<string, object> Metrics);
