// <copyright file="OuroborosCore.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

/// <summary>
/// Core unified implementation for Ouroboros system.
/// Orchestrates all major subsystems.
/// </summary>
public sealed class OuroborosCore : IOuroborosCore
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OuroborosCore"/> class.
    /// </summary>
    /// <param name="eventBus">The event bus.</param>
    /// <param name="consciousnessScaffold">The consciousness scaffold.</param>
    /// <param name="cognitiveLoop">The cognitive loop.</param>
    public OuroborosCore(
        IEventBus eventBus,
        IConsciousnessScaffold consciousnessScaffold,
        ICognitiveLoop cognitiveLoop)
    {
        EventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        ConsciousnessScaffold = consciousnessScaffold ?? throw new ArgumentNullException(nameof(consciousnessScaffold));
        CognitiveLoop = cognitiveLoop ?? throw new ArgumentNullException(nameof(cognitiveLoop));
    }

    /// <inheritdoc/>
    public IEventBus EventBus { get; }

    /// <inheritdoc/>
    public IConsciousnessScaffold ConsciousnessScaffold { get; }

    /// <inheritdoc/>
    public ICognitiveLoop CognitiveLoop { get; }

    /// <inheritdoc/>
    public bool IsRunning =>
        ConsciousnessScaffold.IsRunning && CognitiveLoop.IsRunning;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        await ConsciousnessScaffold.InitializeAsync();
        await CognitiveLoop.InitializeAsync();
    }

    /// <inheritdoc/>
    public async Task StartAsync()
    {
        await ConsciousnessScaffold.StartAsync();
        await CognitiveLoop.StartAsync();
    }

    /// <inheritdoc/>
    public async Task StopAsync()
    {
        await CognitiveLoop.StopAsync();
        await ConsciousnessScaffold.StopAsync();
    }
}
