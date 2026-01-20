// <copyright file="DomainTypesTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Reflection;

using FluentAssertions;
using Ouroboros.Domain.Environment;
using Ouroboros.Domain.Persistence;
using Ouroboros.Domain.Reflection;
using Xunit;

/// <summary>
/// Tests for reflection domain types.
/// Validates record properties and computed values.
/// </summary>
[Trait("Category", "Unit")]
public class DomainTypesTests
{
    [Fact]
    public void TaskPerformance_CalculatesSuccessRate_Correctly()
    {
        // Arrange & Act
        var perf = new TaskPerformance("Test", 10, 7, 5.0, Array.Empty<string>());

        // Assert
        perf.SuccessRate.Should().Be(0.7);
        perf.Failures.Should().Be(3);
    }

    [Fact]
    public void TaskPerformance_WithZeroAttempts_ReturnsZeroSuccessRate()
    {
        // Arrange & Act
        var perf = new TaskPerformance("Test", 0, 0, 0.0, Array.Empty<string>());

        // Assert
        perf.SuccessRate.Should().Be(0.0);
        perf.Failures.Should().Be(0);
    }

    [Fact]
    public void PerformanceReport_CalculatesTotalTasks_Correctly()
    {
        // Arrange
        var byTask = new Dictionary<string, TaskPerformance>
        {
            ["Task1"] = new TaskPerformance("Task1", 10, 5, 3.0, Array.Empty<string>()),
            ["Task2"] = new TaskPerformance("Task2", 20, 15, 2.0, Array.Empty<string>())
        };

        // Act
        var report = new PerformanceReport(0.7, TimeSpan.FromSeconds(2.5), byTask, Array.Empty<Insight>(), DateTime.UtcNow);

        // Assert
        report.TotalTasks.Should().Be(2);
    }

    [Fact]
    public void PerformanceReport_BestPerformingTasks_SortedBySuccessRate()
    {
        // Arrange
        var byTask = new Dictionary<string, TaskPerformance>
        {
            ["Low"] = new TaskPerformance("Low", 10, 3, 3.0, Array.Empty<string>()),
            ["High"] = new TaskPerformance("High", 10, 9, 2.0, Array.Empty<string>()),
            ["Med"] = new TaskPerformance("Med", 10, 5, 2.5, Array.Empty<string>())
        };

        // Act
        var report = new PerformanceReport(0.6, TimeSpan.FromSeconds(2.5), byTask, Array.Empty<Insight>(), DateTime.UtcNow);
        var best = report.BestPerformingTasks.ToList();

        // Assert
        best[0].TaskType.Should().Be("High");
        best[1].TaskType.Should().Be("Med");
        best[2].TaskType.Should().Be("Low");
    }

    [Fact]
    public void ErrorPattern_CalculatesSeverityScore_BasedOnFrequencyAndRecency()
    {
        // Arrange
        var recentFailure = new FailedEpisode(
            Guid.NewGuid(),
            DateTime.UtcNow,
            "Goal",
            "Error",
            new object(),
            new Dictionary<string, object>());

        // Act
        var pattern = new ErrorPattern("Test error", 10, new[] { recentFailure }, null);

        // Assert
        pattern.SeverityScore.Should().BeGreaterThan(0.0);
        pattern.SeverityScore.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public void CapabilityMap_CalculatesOverallScore_AsAverage()
    {
        // Arrange
        var scores = new Dictionary<CognitiveDimension, double>
        {
            [CognitiveDimension.Reasoning] = 0.8,
            [CognitiveDimension.Planning] = 0.6,
            [CognitiveDimension.Learning] = 0.7
        };

        // Act
        var map = new CapabilityMap(scores, Array.Empty<string>(), Array.Empty<string>());

        // Assert
        map.OverallScore.Should().BeApproximately(0.7, 0.01);
    }

    [Fact]
    public void CapabilityMap_IdentifiesStrongestDimension()
    {
        // Arrange
        var scores = new Dictionary<CognitiveDimension, double>
        {
            [CognitiveDimension.Reasoning] = 0.8,
            [CognitiveDimension.Planning] = 0.6,
            [CognitiveDimension.Learning] = 0.9
        };

        // Act
        var map = new CapabilityMap(scores, Array.Empty<string>(), Array.Empty<string>());

        // Assert
        map.StrongestDimension.Should().Be(CognitiveDimension.Learning);
    }

    [Fact]
    public void CapabilityMap_IdentifiesWeakestDimension()
    {
        // Arrange
        var scores = new Dictionary<CognitiveDimension, double>
        {
            [CognitiveDimension.Reasoning] = 0.8,
            [CognitiveDimension.Planning] = 0.4,
            [CognitiveDimension.Learning] = 0.9
        };

        // Act
        var map = new CapabilityMap(scores, Array.Empty<string>(), Array.Empty<string>());

        // Assert
        map.WeakestDimension.Should().Be(CognitiveDimension.Planning);
    }

    [Fact]
    public void ImprovementSuggestion_CalculatesPriority_Correctly()
    {
        // Arrange & Act
        var high = new ImprovementSuggestion("Area", "Suggestion", 0.8, "Impl");
        var medium = new ImprovementSuggestion("Area", "Suggestion", 0.5, "Impl");
        var low = new ImprovementSuggestion("Area", "Suggestion", 0.2, "Impl");

        // Assert
        high.Priority.Should().Be("High");
        medium.Priority.Should().Be("Medium");
        low.Priority.Should().Be("Low");
    }

    [Fact]
    public async Task BenchmarkTask_ExecutesWithTimeout_ReturnsTrue_WhenSuccessful()
    {
        // Arrange
        var task = new BenchmarkTask(
            "Test",
            CognitiveDimension.Reasoning,
            () => Task.FromResult(true),
            TimeSpan.FromSeconds(5));

        // Act
        var result = await task.ExecuteWithTimeoutAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task BenchmarkTask_ExecutesWithTimeout_ReturnsFalse_WhenFailed()
    {
        // Arrange
        var task = new BenchmarkTask(
            "Test",
            CognitiveDimension.Reasoning,
            () => Task.FromResult(false),
            TimeSpan.FromSeconds(5));

        // Act
        var result = await task.ExecuteWithTimeoutAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task BenchmarkTask_ExecutesWithTimeout_ReturnsFalse_OnTimeout()
    {
        // Arrange
        var task = new BenchmarkTask(
            "Test",
            CognitiveDimension.Reasoning,
            async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(10));
                return true;
            },
            TimeSpan.FromMilliseconds(100));

        // Act
        var result = await task.ExecuteWithTimeoutAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task BenchmarkTask_ExecutesWithTimeout_ReturnsFalse_OnException()
    {
        // Arrange
        var task = new BenchmarkTask(
            "Test",
            CognitiveDimension.Reasoning,
            () => throw new InvalidOperationException("Test error"),
            TimeSpan.FromSeconds(5));

        // Act
        var result = await task.ExecuteWithTimeoutAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Fact_IsHighConfidence_WhenConfidenceAbove80Percent()
    {
        // Arrange & Act
        var highConfidence = new Fact(Guid.NewGuid(), "Content", "Source", 0.85, DateTime.UtcNow);
        var lowConfidence = new Fact(Guid.NewGuid(), "Content", "Source", 0.5, DateTime.UtcNow);

        // Assert
        highConfidence.IsHighConfidence.Should().BeTrue();
        lowConfidence.IsHighConfidence.Should().BeFalse();
    }

    [Fact]
    public void Fact_IsRecent_WhenWithinLast30Days()
    {
        // Arrange & Act
        var recent = new Fact(Guid.NewGuid(), "Content", "Source", 0.9, DateTime.UtcNow.AddDays(-15));
        var old = new Fact(Guid.NewGuid(), "Content", "Source", 0.9, DateTime.UtcNow.AddDays(-60));

        // Assert
        recent.IsRecent.Should().BeTrue();
        old.IsRecent.Should().BeFalse();
    }

    [Fact]
    public void Insight_SupportsAllInsightTypes()
    {
        // Arrange
        var supportingEpisodes = new List<Episode>
        {
            new Episode(Guid.NewGuid(), "Test", new List<EnvironmentStep>(), 10.0, DateTime.UtcNow, DateTime.UtcNow, true)
        };

        // Act & Assert - Test all insight types
        foreach (InsightType type in Enum.GetValues(typeof(InsightType)))
        {
            var insight = new Insight(type, "Description", 0.8, supportingEpisodes);
            insight.Type.Should().Be(type);
        }
    }

    [Fact]
    public void CognitiveDimension_HasAllExpectedValues()
    {
        // Arrange & Act
        var dimensions = Enum.GetValues(typeof(CognitiveDimension)).Cast<CognitiveDimension>().ToList();

        // Assert
        dimensions.Should().Contain(CognitiveDimension.Reasoning);
        dimensions.Should().Contain(CognitiveDimension.Planning);
        dimensions.Should().Contain(CognitiveDimension.Learning);
        dimensions.Should().Contain(CognitiveDimension.Memory);
        dimensions.Should().Contain(CognitiveDimension.Generalization);
        dimensions.Should().Contain(CognitiveDimension.Creativity);
        dimensions.Should().Contain(CognitiveDimension.SocialIntelligence);
    }
}
