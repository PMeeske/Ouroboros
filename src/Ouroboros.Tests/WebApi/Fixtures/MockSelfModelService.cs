// <copyright file="MockSelfModelService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.WebApi.Models;
using Ouroboros.WebApi.Services;

namespace Ouroboros.Tests.WebApi.Fixtures;

/// <summary>
/// Mock implementation of ISelfModelService for testing.
/// </summary>
public class MockSelfModelService : ISelfModelService
{
    /// <inheritdoc/>
    public Task<SelfStateResponse> GetStateAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new SelfStateResponse
        {
            AgentId = Guid.NewGuid(),
            Name = "MockAgent",
            CapabilityCount = 10,
            Resources = new Dictionary<string, object>
            {
                { "CPU", new { Type = "Compute", Available = 80.0, Total = 100.0, Unit = "%" } },
                { "Memory", new { Type = "Storage", Available = 4096.0, Total = 8192.0, Unit = "MB" } }
            },
            Commitments = new List<CommitmentDto>
            {
                new CommitmentDto
                {
                    Id = Guid.NewGuid(),
                    Description = "Test commitment",
                    Deadline = DateTime.UtcNow.AddDays(7),
                    Priority = 5,
                    Status = "InProgress",
                    ProgressPercent = 50.0
                }
            },
            Performance = new PerformanceDto
            {
                OverallSuccessRate = 0.95,
                AverageResponseTime = 250.0,
                TotalTasks = 100,
                SuccessfulTasks = 95,
                FailedTasks = 5
            },
            StateTimestamp = DateTime.UtcNow
        });
    }

    /// <inheritdoc/>
    public Task<SelfForecastResponse> GetForecastsAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new SelfForecastResponse
        {
            PendingForecasts = new List<ForecastDto>
            {
                new ForecastDto
                {
                    Id = Guid.NewGuid(),
                    Description = "Load prediction",
                    MetricName = "RequestRate",
                    PredictedValue = 150.0,
                    Confidence = 0.85,
                    TargetTime = DateTime.UtcNow.AddHours(1),
                    Status = "Pending"
                }
            },
            Calibration = new CalibrationDto
            {
                TotalForecasts = 50,
                AverageConfidence = 0.80,
                AverageAccuracy = 0.75,
                BrierScore = 0.15,
                CalibrationError = 0.05
            },
            RecentAnomalies = new List<AnomalyDto>()
        });
    }

    /// <inheritdoc/>
    public Task<List<CommitmentDto>> GetCommitmentsAsync(CancellationToken ct = default)
    {
        return Task.FromResult(new List<CommitmentDto>
        {
            new CommitmentDto
            {
                Id = Guid.NewGuid(),
                Description = "Active test commitment",
                Deadline = DateTime.UtcNow.AddDays(3),
                Priority = 8,
                Status = "InProgress",
                ProgressPercent = 65.0
            }
        });
    }

    /// <inheritdoc/>
    public Task<SelfExplainResponse> ExplainAsync(SelfExplainRequest request, CancellationToken ct = default)
    {
        return Task.FromResult(new SelfExplainResponse
        {
            Narrative = "This is a mock self-explanation narrative.",
            DagSummary = "Mock DAG summary with execution details.",
            KeyEvents = new List<string> { "Event 1", "Event 2", "Event 3" }
        });
    }
}
