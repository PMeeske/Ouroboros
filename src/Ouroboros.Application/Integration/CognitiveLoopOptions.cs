// <copyright file="CognitiveLoopOptions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

/// <summary>
/// Configuration options for cognitive loop.
/// Controls perception, reasoning, action cycle behavior.
/// </summary>
public sealed class CognitiveLoopOptions
{
    /// <summary>
    /// Gets the default cognitive loop options.
    /// </summary>
    public static CognitiveLoopOptions Default => new()
    {
        CycleInterval = TimeSpan.FromMilliseconds(100),
        MaxConcurrentCycles = 4,
        EnablePredictiveProcessing = true,
        EnableMetaCognition = true,
        ReasoningTimeout = TimeSpan.FromSeconds(30),
        ActionTimeout = TimeSpan.FromSeconds(10)
    };

    /// <summary>
    /// Gets or sets the interval between cognitive cycles.
    /// Default: 100 milliseconds.
    /// </summary>
    public TimeSpan CycleInterval { get; set; } = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets or sets the maximum number of concurrent cognitive cycles.
    /// Default: 4 cycles.
    /// </summary>
    public int MaxConcurrentCycles { get; set; } = 4;

    /// <summary>
    /// Gets or sets a value indicating whether to enable predictive processing.
    /// Default: true.
    /// </summary>
    public bool EnablePredictiveProcessing { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether to enable meta-cognition.
    /// Meta-cognition allows the system to reason about its own reasoning.
    /// Default: true.
    /// </summary>
    public bool EnableMetaCognition { get; set; } = true;

    /// <summary>
    /// Gets or sets the timeout for reasoning operations.
    /// Default: 30 seconds.
    /// </summary>
    public TimeSpan ReasoningTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gets or sets the timeout for action execution.
    /// Default: 10 seconds.
    /// </summary>
    public TimeSpan ActionTimeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Gets or sets a value indicating whether to enable error correction.
    /// Default: true.
    /// </summary>
    public bool EnableErrorCorrection { get; set; } = true;

    /// <summary>
    /// Gets or sets the minimum confidence threshold for actions.
    /// Range: 0.0 to 1.0. Default: 0.6.
    /// </summary>
    public double MinActionConfidence { get; set; } = 0.6;

    /// <summary>
    /// Gets or sets a value indicating whether to log cognitive states.
    /// Default: false.
    /// </summary>
    public bool LogCognitiveStates { get; set; } = false;
}
