// <copyright file="ReflectionEngineTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Reflection;

using FluentAssertions;
using Ouroboros.Application.Services.Reflection;
using Ouroboros.Core.LawsOfForm;
using Ouroboros.Domain.Environment;
using Ouroboros.Domain.Persistence;
using Ouroboros.Domain.Reflection;
using Xunit;

/// <summary>
/// Tests for the ReflectionEngine implementation.
/// Validates all methods of the IReflectionEngine interface.
/// </summary>
[Trait("Category", "Unit")]
public class ReflectionEngineTests
{
    private readonly ReflectionEngine engine;

    public ReflectionEngineTests()
    {
        this.engine = new ReflectionEngine();
    }

    [Fact]
    public async Task AnalyzePerformanceAsync_WithValidEpisodes_ReturnsSuccessResult()
    {
        // Arrange
        var episodes = new List<Episode>
        {
            new Episode(
                Guid.NewGuid(),
                "TestEnv",
                new List<EnvironmentStep>(),
                10.0,
                DateTime.UtcNow.AddMinutes(-10),
                DateTime.UtcNow,
                true),
            new Episode(
                Guid.NewGuid(),
                "TestEnv",
                new List<EnvironmentStep>(),
                5.0,
                DateTime.UtcNow.AddMinutes(-5),
                DateTime.UtcNow,
                false)
        };

        // Act
        var result = await this.engine.AnalyzePerformanceAsync(episodes, TimeSpan.FromHours(1));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AverageSuccessRate.Should().Be(0.5);
        result.Value.ByTaskType.Should().ContainKey("TestEnv");
        result.Value.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task AnalyzePerformanceAsync_WithEmptyList_ReturnsFailure()
    {
        // Arrange
        var episodes = new List<Episode>();

        // Act
        var result = await this.engine.AnalyzePerformanceAsync(episodes, TimeSpan.FromHours(1));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No episodes provided");
    }

    [Fact]
    public async Task AnalyzePerformanceAsync_WithNullList_ReturnsFailure()
    {
        // Act
        var result = await this.engine.AnalyzePerformanceAsync(null!, TimeSpan.FromHours(1));

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task AnalyzePerformanceAsync_WithOldEpisodes_ReturnsFailure()
    {
        // Arrange
        var episodes = new List<Episode>
        {
            new Episode(
                Guid.NewGuid(),
                "TestEnv",
                new List<EnvironmentStep>(),
                10.0,
                DateTime.UtcNow.AddDays(-100),
                DateTime.UtcNow.AddDays(-100),
                true)
        };

        // Act
        var result = await this.engine.AnalyzePerformanceAsync(episodes, TimeSpan.FromHours(1));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No episodes found within the specified period");
    }

    [Fact]
    public async Task AnalyzePerformanceAsync_GroupsByEnvironmentName()
    {
        // Arrange
        var episodes = new List<Episode>
        {
            new Episode(Guid.NewGuid(), "EnvA", new List<EnvironmentStep>(), 10.0, DateTime.UtcNow, DateTime.UtcNow, true),
            new Episode(Guid.NewGuid(), "EnvB", new List<EnvironmentStep>(), 5.0, DateTime.UtcNow, DateTime.UtcNow, false),
            new Episode(Guid.NewGuid(), "EnvA", new List<EnvironmentStep>(), 8.0, DateTime.UtcNow, DateTime.UtcNow, true)
        };

        // Act
        var result = await this.engine.AnalyzePerformanceAsync(episodes, TimeSpan.FromHours(1));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ByTaskType.Should().HaveCount(2);
        result.Value.ByTaskType["EnvA"].TotalAttempts.Should().Be(2);
        result.Value.ByTaskType["EnvA"].Successes.Should().Be(2);
        result.Value.ByTaskType["EnvB"].TotalAttempts.Should().Be(1);
    }

    [Fact]
    public async Task AnalyzePerformanceAsync_GeneratesInsights()
    {
        // Arrange
        var highSuccessEpisodes = Enumerable.Range(0, 10)
            .Select(_ => new Episode(
                Guid.NewGuid(),
                "HighSuccess",
                new List<EnvironmentStep>(),
                10.0,
                DateTime.UtcNow,
                DateTime.UtcNow,
                true))
            .ToList();

        // Act
        var result = await this.engine.AnalyzePerformanceAsync(highSuccessEpisodes, TimeSpan.FromHours(1));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Insights.Should().NotBeEmpty();
        result.Value.Insights.Should().Contain(i => i.Type == InsightType.Strength);
    }

    [Fact]
    public async Task DetectErrorPatternsAsync_WithValidFailures_DetectsPatterns()
    {
        // Arrange
        var failures = new List<FailedEpisode>
        {
            new FailedEpisode(
                Guid.NewGuid(),
                DateTime.UtcNow,
                "Goal1",
                "Timeout error occurred during execution",
                new object(),
                new Dictionary<string, object>()),
            new FailedEpisode(
                Guid.NewGuid(),
                DateTime.UtcNow,
                "Goal2",
                "Timeout error occurred during processing",
                new object(),
                new Dictionary<string, object>()),
            new FailedEpisode(
                Guid.NewGuid(),
                DateTime.UtcNow,
                "Goal3",
                "Network failure during connection",
                new object(),
                new Dictionary<string, object>())
        };

        // Act
        var result = await this.engine.DetectErrorPatternsAsync(failures);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        result.Value.Should().Contain(p => p.Description.Contains("timeout", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DetectErrorPatternsAsync_WithEmptyList_ReturnsEmptyList()
    {
        // Arrange
        var failures = new List<FailedEpisode>();

        // Act
        var result = await this.engine.DetectErrorPatternsAsync(failures);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task DetectErrorPatternsAsync_WithNullList_ReturnsFailure()
    {
        // Act
        var result = await this.engine.DetectErrorPatternsAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task DetectErrorPatternsAsync_SortsByFrequency()
    {
        // Arrange
        var failures = new List<FailedEpisode>();
        for (int i = 0; i < 5; i++)
        {
            failures.Add(new FailedEpisode(
                Guid.NewGuid(),
                DateTime.UtcNow,
                "Goal",
                "Common error pattern repeated",
                new object(),
                new Dictionary<string, object>()));
        }

        failures.Add(new FailedEpisode(
            Guid.NewGuid(),
            DateTime.UtcNow,
            "Goal",
            "Rare error unique",
            new object(),
            new Dictionary<string, object>()));

        // Act
        var result = await this.engine.DetectErrorPatternsAsync(failures);

        // Assert
        result.IsSuccess.Should().BeTrue();
        if (result.Value.Count > 0)
        {
            result.Value.First().Frequency.Should().BeGreaterThanOrEqualTo(result.Value.Last().Frequency);
        }
    }

    [Fact]
    public async Task AssessCapabilitiesAsync_WithValidTasks_ReturnsCapabilityMap()
    {
        // Arrange
        var tasks = new List<BenchmarkTask>
        {
            new BenchmarkTask(
                "Reasoning1",
                CognitiveDimension.Reasoning,
                () => Task.FromResult(true),
                TimeSpan.FromSeconds(5)),
            new BenchmarkTask(
                "Reasoning2",
                CognitiveDimension.Reasoning,
                () => Task.FromResult(true),
                TimeSpan.FromSeconds(5)),
            new BenchmarkTask(
                "Planning1",
                CognitiveDimension.Planning,
                () => Task.FromResult(false),
                TimeSpan.FromSeconds(5))
        };

        // Act
        var result = await this.engine.AssessCapabilitiesAsync(tasks);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Scores.Should().ContainKey(CognitiveDimension.Reasoning);
        result.Value.Scores.Should().ContainKey(CognitiveDimension.Planning);
        result.Value.Scores[CognitiveDimension.Reasoning].Should().Be(1.0);
        result.Value.Scores[CognitiveDimension.Planning].Should().Be(0.0);
    }

    [Fact]
    public async Task AssessCapabilitiesAsync_WithEmptyList_ReturnsFailure()
    {
        // Arrange
        var tasks = new List<BenchmarkTask>();

        // Act
        var result = await this.engine.AssessCapabilitiesAsync(tasks);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No benchmark tasks");
    }

    [Fact]
    public async Task AssessCapabilitiesAsync_WithNullList_ReturnsFailure()
    {
        // Act
        var result = await this.engine.AssessCapabilitiesAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task AssessCapabilitiesAsync_IdentifiesStrengthsAndWeaknesses()
    {
        // Arrange
        var tasks = new List<BenchmarkTask>
        {
            new BenchmarkTask("Strong1", CognitiveDimension.Reasoning, () => Task.FromResult(true), TimeSpan.FromSeconds(5)),
            new BenchmarkTask("Strong2", CognitiveDimension.Reasoning, () => Task.FromResult(true), TimeSpan.FromSeconds(5)),
            new BenchmarkTask("Weak1", CognitiveDimension.Planning, () => Task.FromResult(false), TimeSpan.FromSeconds(5)),
            new BenchmarkTask("Weak2", CognitiveDimension.Planning, () => Task.FromResult(false), TimeSpan.FromSeconds(5))
        };

        // Act
        var result = await this.engine.AssessCapabilitiesAsync(tasks);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Strengths.Should().Contain(s => s.Contains("Reasoning"));
        result.Value.Weaknesses.Should().Contain(w => w.Contains("Planning"));
    }

    [Fact]
    public async Task SuggestImprovementsAsync_WithLowSuccessRate_SuggestsImprovement()
    {
        // Arrange
        var report = new PerformanceReport(
            0.3,
            TimeSpan.FromMinutes(1),
            new Dictionary<string, TaskPerformance>(),
            Array.Empty<Insight>(),
            DateTime.UtcNow);

        // Act
        var result = await this.engine.SuggestImprovementsAsync(report);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(s => s.Area == "Overall Performance");
    }

    [Fact]
    public async Task SuggestImprovementsAsync_WithHighExecutionTime_SuggestsOptimization()
    {
        // Arrange
        var report = new PerformanceReport(
            0.8,
            TimeSpan.FromMinutes(10),
            new Dictionary<string, TaskPerformance>(),
            Array.Empty<Insight>(),
            DateTime.UtcNow);

        // Act
        var result = await this.engine.SuggestImprovementsAsync(report);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain(s => s.Area == "Execution Efficiency");
    }

    [Fact]
    public async Task SuggestImprovementsAsync_WithNullReport_ReturnsFailure()
    {
        // Act
        var result = await this.engine.SuggestImprovementsAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SuggestImprovementsAsync_OrdersByExpectedImpact()
    {
        // Arrange
        var report = new PerformanceReport(
            0.3,
            TimeSpan.FromMinutes(10),
            new Dictionary<string, TaskPerformance>(),
            Array.Empty<Insight>(),
            DateTime.UtcNow);

        // Act
        var result = await this.engine.SuggestImprovementsAsync(report);

        // Assert
        result.IsSuccess.Should().BeTrue();
        if (result.Value.Count > 1)
        {
            result.Value[0].ExpectedImpact.Should().BeGreaterThanOrEqualTo(result.Value[^1].ExpectedImpact);
        }
    }

    [Fact]
    public async Task AssessCertaintyAsync_WithNoEvidence_ReturnsImaginary()
    {
        // Arrange
        var claim = "The system is functioning correctly";
        var evidence = new List<Fact>();

        // Act
        var result = await this.engine.AssessCertaintyAsync(claim, evidence);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(Form.Imaginary);
    }

    [Fact]
    public async Task AssessCertaintyAsync_WithSupportingEvidence_ReturnsMark()
    {
        // Arrange
        var claim = "system functioning correctly";
        var evidence = new List<Fact>
        {
            new Fact(Guid.NewGuid(), "system is functioning correctly", "Source1", 0.9, DateTime.UtcNow),
            new Fact(Guid.NewGuid(), "system functioning as expected", "Source2", 0.8, DateTime.UtcNow)
        };

        // Act
        var result = await this.engine.AssessCertaintyAsync(claim, evidence);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(Form.Mark);
    }

    [Fact]
    public async Task AssessCertaintyAsync_WithContradictingEvidence_ReturnsVoid()
    {
        // Arrange
        var claim = "system functioning correctly";
        var evidence = new List<Fact>
        {
            new Fact(Guid.NewGuid(), "unrelated data point", "Source1", 0.9, DateTime.UtcNow),
            new Fact(Guid.NewGuid(), "different topic entirely", "Source2", 0.8, DateTime.UtcNow)
        };

        // Act
        var result = await this.engine.AssessCertaintyAsync(claim, evidence);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(Form.Void);
    }

    [Fact]
    public async Task AssessCertaintyAsync_WithMixedEvidence_ReturnsImaginary()
    {
        // Arrange
        var claim = "system functioning";
        var evidence = new List<Fact>
        {
            new Fact(Guid.NewGuid(), "system functioning well", "Source1", 0.5, DateTime.UtcNow),
            new Fact(Guid.NewGuid(), "unrelated information", "Source2", 0.5, DateTime.UtcNow)
        };

        // Act
        var result = await this.engine.AssessCertaintyAsync(claim, evidence);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(Form.Imaginary);
    }

    [Fact]
    public async Task AssessCertaintyAsync_WithNullClaim_ReturnsFailure()
    {
        // Arrange
        var evidence = new List<Fact>();

        // Act
        var result = await this.engine.AssessCertaintyAsync(null!, evidence);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task AssessCertaintyAsync_WithNullEvidence_ReturnsFailure()
    {
        // Act
        var result = await this.engine.AssessCertaintyAsync("claim", null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }
}
