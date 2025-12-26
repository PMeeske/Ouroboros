// <copyright file="SelfModelService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Agent.MetaAI;
using Ouroboros.Agent.MetaAI.SelfModel;
using Ouroboros.WebApi.Models;

namespace Ouroboros.WebApi.Services;

/// <summary>
/// Service for self-model operations.
/// </summary>
public interface ISelfModelService
{
    /// <summary>
    /// Gets the current agent state.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Agent state response</returns>
    Task<SelfStateResponse> GetStateAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets forecast information.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Forecast response</returns>
    Task<SelfForecastResponse> GetForecastsAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets active commitments.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of commitments</returns>
    Task<List<CommitmentDto>> GetCommitmentsAsync(CancellationToken ct = default);

    /// <summary>
    /// Generates a self-explanation from execution DAG.
    /// </summary>
    /// <param name="request">Explanation request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Explanation response</returns>
    Task<SelfExplainResponse> ExplainAsync(SelfExplainRequest request, CancellationToken ct = default);
}

/// <summary>
/// Implementation of self-model service.
/// </summary>
public sealed class SelfModelService : ISelfModelService
{
    private readonly IIdentityGraph _identityGraph;
    private readonly IPredictiveMonitor _predictiveMonitor;
    private readonly IGlobalWorkspace _globalWorkspace;
    private readonly IChatCompletionModel? _llm;

    public SelfModelService(
        IIdentityGraph identityGraph,
        IPredictiveMonitor predictiveMonitor,
        IGlobalWorkspace globalWorkspace,
        IChatCompletionModel? llm = null)
    {
        _identityGraph = identityGraph ?? throw new ArgumentNullException(nameof(identityGraph));
        _predictiveMonitor = predictiveMonitor ?? throw new ArgumentNullException(nameof(predictiveMonitor));
        _globalWorkspace = globalWorkspace ?? throw new ArgumentNullException(nameof(globalWorkspace));
        _llm = llm;
    }

    public async Task<SelfStateResponse> GetStateAsync(CancellationToken ct = default)
    {
        AgentIdentityState state = await _identityGraph.GetStateAsync(ct);

        List<CommitmentDto> commitments = state.Commitments
            .Select(c => new CommitmentDto
            {
                Id = c.Id,
                Description = c.Description,
                Deadline = c.Deadline,
                Priority = c.Priority,
                Status = c.Status.ToString(),
                ProgressPercent = c.ProgressPercent
            })
            .ToList();

        Dictionary<string, object> resources = state.Resources
            .ToDictionary(
                r => r.Name,
                r => (object)new
                {
                    r.Type,
                    r.Available,
                    r.Total,
                    r.Unit,
                    Utilization = r.Total > 0 ? (r.Total - r.Available) / r.Total : 0.0
                });

        PerformanceDto performance = new PerformanceDto
        {
            OverallSuccessRate = state.Performance.OverallSuccessRate,
            AverageResponseTime = state.Performance.AverageResponseTime,
            TotalTasks = state.Performance.TotalTasks,
            SuccessfulTasks = state.Performance.SuccessfulTasks,
            FailedTasks = state.Performance.FailedTasks
        };

        return new SelfStateResponse
        {
            AgentId = state.AgentId,
            Name = state.Name,
            CapabilityCount = state.Capabilities.Count,
            Resources = resources,
            Commitments = commitments,
            Performance = performance,
            StateTimestamp = state.StateTimestamp
        };
    }

    public async Task<SelfForecastResponse> GetForecastsAsync(CancellationToken ct = default)
    {
        await Task.CompletedTask;

        List<Forecast> pending = _predictiveMonitor.GetPendingForecasts();
        ForecastCalibration calibration = _predictiveMonitor.GetCalibration(TimeSpan.FromDays(30));
        List<AnomalyDetection> anomalies = _predictiveMonitor.GetRecentAnomalies(10);

        List<ForecastDto> pendingDtos = pending.Select(f => new ForecastDto
        {
            Id = f.Id,
            Description = f.Description,
            MetricName = f.MetricName,
            PredictedValue = f.PredictedValue,
            Confidence = f.Confidence,
            TargetTime = f.TargetTime,
            Status = f.Status.ToString()
        }).ToList();

        CalibrationDto calibrationDto = new CalibrationDto
        {
            TotalForecasts = calibration.TotalForecasts,
            AverageConfidence = calibration.AverageConfidence,
            AverageAccuracy = calibration.AverageAccuracy,
            BrierScore = calibration.BrierScore,
            CalibrationError = calibration.CalibrationError
        };

        List<AnomalyDto> anomalyDtos = anomalies.Select(a => new AnomalyDto
        {
            MetricName = a.MetricName,
            ObservedValue = a.ObservedValue,
            ExpectedValue = a.ExpectedValue,
            Deviation = a.Deviation,
            Severity = a.Severity,
            DetectedAt = a.DetectedAt
        }).ToList();

        return new SelfForecastResponse
        {
            PendingForecasts = pendingDtos,
            Calibration = calibrationDto,
            RecentAnomalies = anomalyDtos
        };
    }

    public async Task<List<CommitmentDto>> GetCommitmentsAsync(CancellationToken ct = default)
    {
        await Task.CompletedTask;

        List<AgentCommitment> commitments = _identityGraph.GetActiveCommitments();

        return commitments.Select(c => new CommitmentDto
        {
            Id = c.Id,
            Description = c.Description,
            Deadline = c.Deadline,
            Priority = c.Priority,
            Status = c.Status.ToString(),
            ProgressPercent = c.ProgressPercent
        }).ToList();
    }

    public async Task<SelfExplainResponse> ExplainAsync(
        SelfExplainRequest request,
        CancellationToken ct = default)
    {
        // Get workspace items to understand current context
        List<WorkspaceItem> contextItems = _globalWorkspace.GetItems(WorkspacePriority.Normal);
        
        // Get recent state
        AgentIdentityState state = await _identityGraph.GetStateAsync(ct);

        // Build DAG summary
        string dagSummary = $@"Agent: {state.Name}
Capabilities: {state.Capabilities.Count}
Active Commitments: {state.Commitments.Count(c => c.Status == CommitmentStatus.InProgress)}
Success Rate: {state.Performance.OverallSuccessRate:P1}
Workspace Items: {contextItems.Count}";

        // Generate narrative if LLM is available
        string narrative;
        if (_llm != null)
        {
            string prompt = $@"Generate a concise self-explanation for the agent's current state:

{dagSummary}

Recent High-Priority Items:
{string.Join("\n", contextItems.Take(5).Select(i => $"- {i.Content}"))}

Active Commitments:
{string.Join("\n", state.Commitments.Where(c => c.Status == CommitmentStatus.InProgress).Take(3).Select(c => $"- {c.Description} ({c.ProgressPercent:F0}% complete)"))}

Provide a 2-3 paragraph narrative explaining:
1. What the agent is currently doing
2. What it has accomplished recently
3. What challenges or priorities it faces";

            try
            {
                narrative = await _llm.GenerateTextAsync(prompt, ct);
            }
            catch
            {
                narrative = GenerateFallbackNarrative(state, contextItems);
            }
        }
        else
        {
            narrative = GenerateFallbackNarrative(state, contextItems);
        }

        List<string> keyEvents = contextItems
            .Where(i => i.Priority >= WorkspacePriority.High)
            .OrderByDescending(i => i.GetAttentionWeight())
            .Take(5)
            .Select(i => i.Content)
            .ToList();

        return new SelfExplainResponse
        {
            Narrative = narrative,
            DagSummary = dagSummary,
            KeyEvents = keyEvents
        };
    }

    private static string GenerateFallbackNarrative(
        AgentIdentityState state,
        List<WorkspaceItem> contextItems)
    {
        int activeCommitments = state.Commitments.Count(c => c.Status == CommitmentStatus.InProgress);
        int highPriorityItems = contextItems.Count(i => i.Priority >= WorkspacePriority.High);

        return $@"Agent '{state.Name}' is currently active with {state.Capabilities.Count} capabilities and {activeCommitments} active commitment(s).

Performance metrics show a {state.Performance.OverallSuccessRate:P0} success rate across {state.Performance.TotalTasks} total tasks. The agent's working memory contains {contextItems.Count} items, with {highPriorityItems} requiring high-priority attention.

Current focus areas include: {string.Join(", ", contextItems.Take(3).Select(i => i.Source))}. The agent continues to adapt and learn from each execution.";
    }
}
