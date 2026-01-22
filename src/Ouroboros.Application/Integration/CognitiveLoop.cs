// <copyright file="CognitiveLoop.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

using System.Diagnostics;
using Ouroboros.Core.Monads;
using Unit = Ouroboros.Core.Learning.Unit;

/// <summary>
/// Implementation of the autonomous cognitive loop.
/// Executes continuous perception-reason-act cycles for autonomous operation.
/// Thread-safe implementation using semaphores for state management.
/// </summary>
public sealed class CognitiveLoop : ICognitiveLoop
{
    private readonly IOuroborosCore _core;
    private readonly IEventBus _eventBus;
    private readonly SemaphoreSlim _stateLock = new(1, 1);

    private volatile bool _isRunning;
    private int _cyclesCompleted;
    private DateTime _lastCycleTime;
    private string _currentPhase = "Idle";
    private readonly List<string> _recentActions = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CognitiveLoop"/> class.
    /// </summary>
    /// <param name="core">The Ouroboros core system.</param>
    /// <param name="eventBus">The event bus for publishing events.</param>
    public CognitiveLoop(IOuroborosCore core, IEventBus eventBus)
    {
        _core = core ?? throw new ArgumentNullException(nameof(core));
        _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
    }

    /// <inheritdoc/>
    public bool IsRunning => _isRunning;

    /// <inheritdoc/>
    public async Task RunAsync(CognitiveLoopConfig config, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        await _stateLock.WaitAsync(ct);
        try
        {
            if (_isRunning)
            {
                throw new InvalidOperationException("Cognitive loop is already running");
            }

            _isRunning = true;
            _cyclesCompleted = 0;
        }
        finally
        {
            _stateLock.Release();
        }

        try
        {
            var cycleInterval = config.CycleInterval == default
                ? TimeSpan.FromSeconds(1)
                : config.CycleInterval;

            while (!ct.IsCancellationRequested)
            {
                // Check max cycles limit
                if (config.MaxCyclesPerRun > 0 && _cyclesCompleted >= config.MaxCyclesPerRun)
                {
                    break;
                }

                // Execute a single cognitive cycle
                var cycleResult = await ExecuteSingleCycleAsync(ct);

                if (cycleResult.IsSuccess)
                {
                    _cyclesCompleted++;
                    _lastCycleTime = DateTime.UtcNow;
                }

                // Wait for next cycle
                await Task.Delay(cycleInterval, ct);
            }
        }
        finally
        {
            await _stateLock.WaitAsync(ct);
            try
            {
                _isRunning = false;
                _currentPhase = "Stopped";
            }
            finally
            {
                _stateLock.Release();
            }
        }
    }

    /// <inheritdoc/>
    public async Task<Result<Unit, string>> StopAsync(CancellationToken ct = default)
    {
        await _stateLock.WaitAsync(ct);
        try
        {
            if (!_isRunning)
            {
                return Result<Unit, string>.Failure("Cognitive loop is not running");
            }

            _isRunning = false;
            _currentPhase = "Stopping";

            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Failed to stop loop: {ex.Message}");
        }
        finally
        {
            _stateLock.Release();
        }
    }

    /// <inheritdoc/>
    public CognitiveState GetCurrentState()
    {
        return new CognitiveState(
            _isRunning,
            _cyclesCompleted,
            _lastCycleTime,
            _currentPhase,
            new List<string>(_recentActions));
    }

    /// <inheritdoc/>
    public async Task<Result<CycleOutcome, string>> ExecuteSingleCycleAsync(CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var actions = new List<string>();
        var metrics = new Dictionary<string, object>();

        try
        {
            // Phase 1: Perception - Get attentional focus from consciousness
            _currentPhase = "Perception";
            var focusResult = await _core.Consciousness.GetAttentionalFocusAsync(5, ct);

            if (!focusResult.IsSuccess)
            {
                return Result<CycleOutcome, string>.Failure($"Perception failed: {focusResult.Error}");
            }

            var focusItems = focusResult.Value;
            actions.Add($"Perceived {focusItems.Count} items in focus");
            metrics["focus_items"] = focusItems.Count;

            // Phase 2: Reasoning - Perform metacognitive monitoring
            _currentPhase = "Reasoning";
            var insightsResult = await _core.Consciousness.MonitorMetacognitionAsync(ct);

            if (!insightsResult.IsSuccess)
            {
                return Result<CycleOutcome, string>.Failure($"Reasoning failed: {insightsResult.Error}");
            }

            var insights = insightsResult.Value;
            actions.Add($"Detected {insights.DetectedConflicts.Count} conflicts");
            actions.Add($"Identified {insights.IdentifiedPatterns.Count} patterns");
            metrics["coherence"] = insights.OverallCoherence;

            // Phase 3: Action - Integrate insights and apply attention policies
            _currentPhase = "Action";

            // Broadcast important patterns to consciousness
            foreach (var pattern in insights.IdentifiedPatterns.Take(2))
            {
                var broadcastResult = await _core.Consciousness.BroadcastToConsciousnessAsync(
                    pattern,
                    "CognitiveLoop",
                    new List<string> { "pattern", "insight" },
                    ct);

                if (broadcastResult.IsSuccess)
                {
                    actions.Add($"Broadcasted pattern: {pattern}");
                }
            }

            // Update recent actions
            await _stateLock.WaitAsync(ct);
            try
            {
                _recentActions.Clear();
                _recentActions.AddRange(actions.Take(10));
            }
            finally
            {
                _stateLock.Release();
            }

            stopwatch.Stop();

            var outcome = new CycleOutcome(
                true,
                "Complete",
                stopwatch.Elapsed,
                actions,
                metrics);

            return Result<CycleOutcome, string>.Success(outcome);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            var outcome = new CycleOutcome(
                false,
                _currentPhase,
                stopwatch.Elapsed,
                actions,
                metrics);

            return Result<CycleOutcome, string>.Failure($"Cycle failed in {_currentPhase}: {ex.Message}");
        }
        finally
        {
            _currentPhase = "Idle";
        }
    }
}
