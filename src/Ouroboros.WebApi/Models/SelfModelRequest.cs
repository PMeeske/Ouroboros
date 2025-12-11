// <copyright file="SelfModelRequest.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.WebApi.Models;

/// <summary>
/// Request for self-model explain endpoint.
/// </summary>
public sealed record SelfExplainRequest
{
    /// <summary>
    /// Gets or sets the event ID or range to explain.
    /// </summary>
    public string? EventId { get; set; }

    /// <summary>
    /// Gets or sets whether to include full DAG context.
    /// </summary>
    public bool IncludeContext { get; set; } = true;

    /// <summary>
    /// Gets or sets the maximum depth for narrative generation.
    /// </summary>
    public int MaxDepth { get; set; } = 5;
}

/// <summary>
/// Response containing agent identity state.
/// </summary>
public sealed record SelfStateResponse
{
    /// <summary>
    /// Gets or sets the agent ID.
    /// </summary>
    public required Guid AgentId { get; set; }

    /// <summary>
    /// Gets or sets the agent name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the number of capabilities.
    /// </summary>
    public int CapabilityCount { get; set; }

    /// <summary>
    /// Gets or sets the available resources.
    /// </summary>
    public Dictionary<string, object>? Resources { get; set; }

    /// <summary>
    /// Gets or sets the active commitments.
    /// </summary>
    public List<CommitmentDto>? Commitments { get; set; }

    /// <summary>
    /// Gets or sets the performance metrics.
    /// </summary>
    public PerformanceDto? Performance { get; set; }

    /// <summary>
    /// Gets or sets the state timestamp.
    /// </summary>
    public DateTime StateTimestamp { get; set; }
}

/// <summary>
/// DTO for agent commitment.
/// </summary>
public sealed record CommitmentDto
{
    /// <summary>
    /// Gets or sets the commitment ID.
    /// </summary>
    public required Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Gets or sets the deadline.
    /// </summary>
    public DateTime Deadline { get; set; }

    /// <summary>
    /// Gets or sets the priority.
    /// </summary>
    public double Priority { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public required string Status { get; set; }

    /// <summary>
    /// Gets or sets the progress percentage.
    /// </summary>
    public double ProgressPercent { get; set; }
}

/// <summary>
/// DTO for performance metrics.
/// </summary>
public sealed record PerformanceDto
{
    /// <summary>
    /// Gets or sets the overall success rate.
    /// </summary>
    public double OverallSuccessRate { get; set; }

    /// <summary>
    /// Gets or sets the average response time in milliseconds.
    /// </summary>
    public double AverageResponseTime { get; set; }

    /// <summary>
    /// Gets or sets the total tasks.
    /// </summary>
    public int TotalTasks { get; set; }

    /// <summary>
    /// Gets or sets the successful tasks.
    /// </summary>
    public int SuccessfulTasks { get; set; }

    /// <summary>
    /// Gets or sets the failed tasks.
    /// </summary>
    public int FailedTasks { get; set; }
}

/// <summary>
/// Response containing forecast information.
/// </summary>
public sealed record SelfForecastResponse
{
    /// <summary>
    /// Gets or sets the pending forecasts.
    /// </summary>
    public List<ForecastDto>? PendingForecasts { get; set; }

    /// <summary>
    /// Gets or sets the forecast calibration.
    /// </summary>
    public CalibrationDto? Calibration { get; set; }

    /// <summary>
    /// Gets or sets the recent anomalies.
    /// </summary>
    public List<AnomalyDto>? RecentAnomalies { get; set; }
}

/// <summary>
/// DTO for forecast.
/// </summary>
public sealed record ForecastDto
{
    /// <summary>
    /// Gets or sets the forecast ID.
    /// </summary>
    public required Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the description.
    /// </summary>
    public required string Description { get; set; }

    /// <summary>
    /// Gets or sets the metric name.
    /// </summary>
    public required string MetricName { get; set; }

    /// <summary>
    /// Gets or sets the predicted value.
    /// </summary>
    public double PredictedValue { get; set; }

    /// <summary>
    /// Gets or sets the confidence.
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Gets or sets the target time.
    /// </summary>
    public DateTime TargetTime { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public required string Status { get; set; }
}

/// <summary>
/// DTO for calibration metrics.
/// </summary>
public sealed record CalibrationDto
{
    /// <summary>
    /// Gets or sets the total forecasts.
    /// </summary>
    public int TotalForecasts { get; set; }

    /// <summary>
    /// Gets or sets the average confidence.
    /// </summary>
    public double AverageConfidence { get; set; }

    /// <summary>
    /// Gets or sets the average accuracy.
    /// </summary>
    public double AverageAccuracy { get; set; }

    /// <summary>
    /// Gets or sets the Brier score.
    /// </summary>
    public double BrierScore { get; set; }

    /// <summary>
    /// Gets or sets the calibration error.
    /// </summary>
    public double CalibrationError { get; set; }
}

/// <summary>
/// DTO for anomaly.
/// </summary>
public sealed record AnomalyDto
{
    /// <summary>
    /// Gets or sets the metric name.
    /// </summary>
    public required string MetricName { get; set; }

    /// <summary>
    /// Gets or sets the observed value.
    /// </summary>
    public double ObservedValue { get; set; }

    /// <summary>
    /// Gets or sets the expected value.
    /// </summary>
    public double ExpectedValue { get; set; }

    /// <summary>
    /// Gets or sets the deviation.
    /// </summary>
    public double Deviation { get; set; }

    /// <summary>
    /// Gets or sets the severity.
    /// </summary>
    public required string Severity { get; set; }

    /// <summary>
    /// Gets or sets the detected timestamp.
    /// </summary>
    public DateTime DetectedAt { get; set; }
}

/// <summary>
/// Response for self-explanation.
/// </summary>
public sealed record SelfExplainResponse
{
    /// <summary>
    /// Gets or sets the narrative explanation.
    /// </summary>
    public required string Narrative { get; set; }

    /// <summary>
    /// Gets or sets the execution DAG summary.
    /// </summary>
    public required string DagSummary { get; set; }

    /// <summary>
    /// Gets or sets the key events.
    /// </summary>
    public List<string>? KeyEvents { get; set; }
}
