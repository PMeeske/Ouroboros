// <copyright file="OuroborosHealthChecks.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

using System.Diagnostics;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Ouroboros.Core.Monads;

/// <summary>
/// Health check for the Ouroboros integrated system.
/// Checks availability and health of all engines and subsystems.
/// </summary>
public sealed class OuroborosHealthCheck : IHealthCheck
{
    private readonly IOuroborosCore _core;

    /// <summary>
    /// Initializes a new instance of the <see cref="OuroborosHealthCheck"/> class.
    /// </summary>
    public OuroborosHealthCheck(IOuroborosCore core)
    {
        _core = core ?? throw new ArgumentNullException(nameof(core));
    }

    /// <summary>
    /// Performs health check on the Ouroboros system.
    /// </summary>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var data = new Dictionary<string, object>();
        var unhealthyEngines = new List<string>();

        try
        {
            // Check each engine availability
            CheckEngine("EpisodicMemory", _core.EpisodicMemory, data, unhealthyEngines);
            CheckEngine("AdapterLearning", _core.AdapterLearning, data, unhealthyEngines);
            CheckEngine("MeTTaReasoning", _core.MeTTaReasoning, data, unhealthyEngines);
            CheckEngine("HierarchicalPlanner", _core.HierarchicalPlanner, data, unhealthyEngines);
            CheckEngine("Reflection", _core.Reflection, data, unhealthyEngines);
            CheckEngine("ProgramSynthesis", _core.ProgramSynthesis, data, unhealthyEngines);
            CheckEngine("WorldModel", _core.WorldModel, data, unhealthyEngines);
            CheckEngine("MultiAgent", _core.MultiAgent, data, unhealthyEngines);
            CheckEngine("CausalReasoning", _core.CausalReasoning, data, unhealthyEngines);
            CheckEngine("MetaLearning", _core.MetaLearning, data, unhealthyEngines);
            CheckEngine("EmbodiedAgent", _core.EmbodiedAgent, data, unhealthyEngines);
            CheckEngine("Consciousness", _core.Consciousness, data, unhealthyEngines);
            CheckEngine("Benchmarks", _core.Benchmarks, data, unhealthyEngines);

            if (unhealthyEngines.Any())
            {
                return HealthCheckResult.Degraded(
                    $"Some engines unavailable: {string.Join(", ", unhealthyEngines)}",
                    data: data);
            }

            data["status"] = "All engines operational";
            return HealthCheckResult.Healthy("Ouroboros system operational", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Ouroboros system health check failed",
                ex,
                data);
        }
    }

    private static void CheckEngine(string name, object? engine, Dictionary<string, object> data, List<string> unhealthy)
    {
        if (engine == null)
        {
            data[$"{name}.Status"] = "unavailable";
            unhealthy.Add(name);
        }
        else
        {
            data[$"{name}.Status"] = "available";
            data[$"{name}.Type"] = engine.GetType().Name;
        }
    }
}

/// <summary>
/// Health check for the episodic memory engine.
/// </summary>
public sealed class EpisodicMemoryHealthCheck : IHealthCheck
{
    private readonly IOuroborosCore _core;

    /// <summary>
    /// Initializes a new instance of the <see cref="EpisodicMemoryHealthCheck"/> class.
    /// </summary>
    public EpisodicMemoryHealthCheck(IOuroborosCore core)
    {
        _core = core ?? throw new ArgumentNullException(nameof(core));
    }

    /// <summary>
    /// Checks health of episodic memory system.
    /// </summary>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_core.EpisodicMemory == null)
            {
                return HealthCheckResult.Unhealthy("Episodic memory engine not initialized");
            }

            // Try a basic retrieval operation with timeout
            var stopwatch = Stopwatch.StartNew();
            var result = await _core.EpisodicMemory.RetrieveSimilarEpisodesAsync(
                "health_check",
                topK: 1,
                minSimilarity: 0.0,
                ct: cancellationToken);
            stopwatch.Stop();

            return result.Match(
                success => HealthCheckResult.Healthy(
                    "Episodic memory operational",
                    new Dictionary<string, object>
                    {
                        ["response_time_ms"] = stopwatch.ElapsedMilliseconds,
                        ["episodes_retrieved"] = success.Count
                    }),
                error => HealthCheckResult.Degraded(
                    $"Episodic memory degraded: {error}",
                    data: new Dictionary<string, object> { ["error"] = error }));
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Episodic memory health check failed", ex);
        }
    }
}

/// <summary>
/// Health check for the consciousness scaffold.
/// </summary>
public sealed class ConsciousnessHealthCheck : IHealthCheck
{
    private readonly IOuroborosCore _core;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsciousnessHealthCheck"/> class.
    /// </summary>
    public ConsciousnessHealthCheck(IOuroborosCore core)
    {
        _core = core ?? throw new ArgumentNullException(nameof(core));
    }

    /// <summary>
    /// Checks health of consciousness scaffold.
    /// </summary>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_core.Consciousness == null)
            {
                return HealthCheckResult.Unhealthy("Consciousness scaffold not initialized");
            }

            // Check workspace availability
            var workspace = _core.Consciousness.GlobalWorkspace;
            if (workspace == null)
            {
                return HealthCheckResult.Degraded("Global workspace not available");
            }

            var items = workspace.GetItems();
            var highPriority = workspace.GetHighPriorityItems();

            return HealthCheckResult.Healthy(
                "Consciousness scaffold operational",
                new Dictionary<string, object>
                {
                    ["workspace_items"] = items.Count,
                    ["high_priority_items"] = highPriority.Count
                });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Consciousness health check failed", ex);
        }
    }
}

/// <summary>
/// Health check for the cognitive loop.
/// </summary>
public sealed class CognitiveLoopHealthCheck : IHealthCheck
{
    private readonly ICognitiveLoop? _cognitiveLoop;

    /// <summary>
    /// Initializes a new instance of the <see cref="CognitiveLoopHealthCheck"/> class.
    /// </summary>
    public CognitiveLoopHealthCheck(IServiceProvider serviceProvider)
    {
        // Cognitive loop may not be registered
        _cognitiveLoop = serviceProvider.GetService(typeof(ICognitiveLoop)) as ICognitiveLoop;
    }

    /// <summary>
    /// Checks health of cognitive loop if enabled.
    /// </summary>
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (_cognitiveLoop == null)
            {
                return HealthCheckResult.Healthy(
                    "Cognitive loop not enabled",
                    new Dictionary<string, object> { ["enabled"] = false });
            }

            var state = _cognitiveLoop.GetCurrentState();

            return state.IsRunning
                ? HealthCheckResult.Healthy(
                    "Cognitive loop running",
                    new Dictionary<string, object>
                    {
                        ["enabled"] = true,
                        ["is_running"] = state.IsRunning,
                        ["cycles_completed"] = state.CyclesCompleted,
                        ["last_cycle_time"] = state.LastCycleTime,
                        ["current_phase"] = state.CurrentPhase,
                        ["recent_actions"] = state.RecentActions.Count
                    })
                : HealthCheckResult.Healthy(
                    "Cognitive loop stopped",
                    new Dictionary<string, object>
                    {
                        ["enabled"] = true,
                        ["is_running"] = false,
                        ["cycles_completed"] = state.CyclesCompleted
                    });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Cognitive loop health check failed", ex);
        }
    }
}
