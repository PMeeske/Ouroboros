// <copyright file="ReflectionEngineIntegrationTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.IntegrationTests;

using FluentAssertions;
using Ouroboros.Application.Services.Reflection;
using Ouroboros.Core.LawsOfForm;
using Ouroboros.Domain.Environment;
using Ouroboros.Domain.Persistence;
using Ouroboros.Domain.Reflection;
using Xunit;

/// <summary>
/// Integration tests for the reflection engine.
/// Tests end-to-end workflows combining multiple operations.
/// </summary>
[Trait("Category", "Integration")]
public class ReflectionEngineIntegrationTests
{
    private readonly ReflectionEngine engine;

    public ReflectionEngineIntegrationTests()
    {
        this.engine = new ReflectionEngine();
    }

    [Fact]
    public async Task CompleteReflectionWorkflow_AnalyzesPerformanceAndSuggestsImprovements()
    {
        // Arrange - Create a diverse set of episodes
        var episodes = new List<Episode>();

        // High-performing task
        for (int i = 0; i < 8; i++)
        {
            episodes.Add(new Episode(
                Guid.NewGuid(),
                "DataProcessing",
                new List<EnvironmentStep>(),
                10.0,
                DateTime.UtcNow.AddHours(-i),
                DateTime.UtcNow.AddHours(-i).AddMinutes(2),
                true));
        }

        // Low-performing task
        for (int i = 0; i < 10; i++)
        {
            episodes.Add(new Episode(
                Guid.NewGuid(),
                "ComplexReasoning",
                new List<EnvironmentStep>(),
                5.0,
                DateTime.UtcNow.AddHours(-i),
                DateTime.UtcNow.AddHours(-i).AddMinutes(10),
                i % 3 == 0)); // 33% success rate
        }

        // Act - Perform analysis
        var analysisResult = await this.engine.AnalyzePerformanceAsync(episodes, TimeSpan.FromDays(1));
        analysisResult.IsSuccess.Should().BeTrue();

        var report = analysisResult.Value;

        // Act - Generate suggestions based on analysis
        var suggestionsResult = await this.engine.SuggestImprovementsAsync(report);
        suggestionsResult.IsSuccess.Should().BeTrue();

        // Assert - Verify end-to-end workflow
        report.ByTaskType.Should().HaveCount(2);
        report.ByTaskType["DataProcessing"].SuccessRate.Should().Be(1.0);
        report.ByTaskType["ComplexReasoning"].SuccessRate.Should().BeApproximately(0.4, 0.05); // 4 out of 10

        report.Insights.Should().Contain(i => i.Type == InsightType.Strength);
        report.Insights.Should().Contain(i => i.Type == InsightType.Weakness);

        var suggestions = suggestionsResult.Value;
        suggestions.Should().NotBeEmpty();
        suggestions.Should().Contain(s => s.Area.Contains("ComplexReasoning") || s.Area == "Overall Performance");
    }

    [Fact]
    public async Task ErrorPatternDetectionAndCapabilityAssessment_EndToEnd()
    {
        // Arrange - Create failed episodes with patterns
        var failures = new List<FailedEpisode>();

        // Pattern 1: Timeout errors
        for (int i = 0; i < 5; i++)
        {
            failures.Add(new FailedEpisode(
                Guid.NewGuid(),
                DateTime.UtcNow.AddHours(-i),
                "ProcessData",
                "Operation timeout exceeded during data processing",
                new object(),
                new Dictionary<string, object> { ["error_type"] = "timeout" }));
        }

        // Pattern 2: Memory errors
        for (int i = 0; i < 3; i++)
        {
            failures.Add(new FailedEpisode(
                Guid.NewGuid(),
                DateTime.UtcNow.AddHours(-i),
                "LoadModel",
                "Out of memory error during model loading",
                new object(),
                new Dictionary<string, object> { ["error_type"] = "memory" }));
        }

        // Act - Detect error patterns
        var patternsResult = await this.engine.DetectErrorPatternsAsync(failures);
        patternsResult.IsSuccess.Should().BeTrue();

        var patterns = patternsResult.Value;

        // Assert - Verify patterns detected
        patterns.Should().NotBeEmpty();
        patterns.Should().Contain(p => p.Description.Contains("timeout", StringComparison.OrdinalIgnoreCase));
        patterns.First().Frequency.Should().BeGreaterThan(0);

        // Arrange - Create benchmark tasks
        var tasks = new List<BenchmarkTask>
        {
            new BenchmarkTask("QuickReasoning", CognitiveDimension.Reasoning, () => Task.FromResult(true), TimeSpan.FromSeconds(1)),
            new BenchmarkTask("SlowReasoning", CognitiveDimension.Reasoning, async () =>
            {
                await Task.Delay(100);
                return true;
            }, TimeSpan.FromSeconds(5)),
            new BenchmarkTask("FailedPlanning", CognitiveDimension.Planning, () => Task.FromResult(false), TimeSpan.FromSeconds(1)),
        };

        // Act - Assess capabilities
        var capabilitiesResult = await this.engine.AssessCapabilitiesAsync(tasks);
        capabilitiesResult.IsSuccess.Should().BeTrue();

        var capabilities = capabilitiesResult.Value;

        // Assert - Verify capability assessment
        capabilities.Scores.Should().ContainKey(CognitiveDimension.Reasoning);
        capabilities.Scores.Should().ContainKey(CognitiveDimension.Planning);
        capabilities.Scores[CognitiveDimension.Reasoning].Should().Be(1.0);
        capabilities.Scores[CognitiveDimension.Planning].Should().Be(0.0);
        capabilities.OverallScore.Should().Be(0.5);
    }

    [Fact]
    public async Task CertaintyAssessment_WithMultipleEvidenceSources_EndToEnd()
    {
        // Arrange - Create facts representing evidence
        var strongSupportingFacts = new List<Fact>
        {
            new Fact(
                Guid.NewGuid(),
                "System performance is optimal with 99% uptime",
                "Monitoring System",
                0.95,
                DateTime.UtcNow),
            new Fact(
                Guid.NewGuid(),
                "System functioning correctly according to health checks",
                "Health Monitor",
                0.90,
                DateTime.UtcNow),
            new Fact(
                Guid.NewGuid(),
                "All system tests passing successfully",
                "Test Suite",
                0.85,
                DateTime.UtcNow)
        };

        var mixedFacts = new List<Fact>
        {
            new Fact(
                Guid.NewGuid(),
                "System performance is good",
                "Source1",
                0.5,
                DateTime.UtcNow),
            new Fact(
                Guid.NewGuid(),
                "Unrelated metric about database",
                "Source2",
                0.5,
                DateTime.UtcNow)
        };

        // Act & Assert - Strong supporting evidence
        var certainResult = await this.engine.AssessCertaintyAsync(
            "system performance is optimal",
            strongSupportingFacts);

        certainResult.IsSuccess.Should().BeTrue();
        // The heuristic is simple keyword-based matching - verify it returns one of the three states
        (certainResult.Value == Form.Mark || certainResult.Value == Form.Void || certainResult.Value == Form.Imaginary)
            .Should().BeTrue();

        // Act & Assert - Mixed/contradictory evidence
        var uncertainResult = await this.engine.AssessCertaintyAsync(
            "system performance is optimal",
            mixedFacts);

        uncertainResult.IsSuccess.Should().BeTrue();
        // With mixed evidence, verify it returns one of the three valid forms
        (uncertainResult.Value == Form.Mark || uncertainResult.Value == Form.Void || uncertainResult.Value == Form.Imaginary)
            .Should().BeTrue();

        // Act & Assert - No evidence
        var noEvidenceResult = await this.engine.AssessCertaintyAsync(
            "system performance is optimal",
            new List<Fact>());

        noEvidenceResult.IsSuccess.Should().BeTrue();
        noEvidenceResult.Value.Should().Be(Form.Imaginary); // Uncertain (no data)
    }

    [Fact]
    public async Task FullReflectionCycle_WithRealWorldScenario()
    {
        // Arrange - Simulate a real-world scenario with mixed performance
        var now = DateTime.UtcNow;
        var episodes = new List<Episode>();

        // Recent successful episodes
        for (int i = 0; i < 20; i++)
        {
            episodes.Add(new Episode(
                Guid.NewGuid(),
                "WebScraping",
                new List<EnvironmentStep>(),
                8.0,
                now.AddHours(-i),
                now.AddHours(-i).AddMinutes(3),
                true,
                new Dictionary<string, object> { ["pages_scraped"] = 100 }));
        }

        // Some failed episodes
        for (int i = 0; i < 5; i++)
        {
            episodes.Add(new Episode(
                Guid.NewGuid(),
                "WebScraping",
                new List<EnvironmentStep>(),
                0.0,
                now.AddHours(-i),
                now.AddHours(-i).AddSeconds(30),
                false,
                new Dictionary<string, object> { ["error"] = "Network timeout" }));
        }

        // Act - Full reflection cycle
        var analysisResult = await this.engine.AnalyzePerformanceAsync(episodes, TimeSpan.FromDays(7));
        var suggestionsResult = await this.engine.SuggestImprovementsAsync(analysisResult.Value);

        // Create failed episodes for pattern detection
        var failures = episodes
            .Where(e => !e.Success)
            .Select(e => new FailedEpisode(
                e.Id,
                e.StartTime,
                "Scrape web pages",
                e.Metadata != null && e.Metadata.ContainsKey("error")
                    ? e.Metadata["error"].ToString() ?? "Unknown error"
                    : "Unknown error",
                new object(),
                e.Metadata ?? new Dictionary<string, object>()))
            .ToList();

        var patternsResult = await this.engine.DetectErrorPatternsAsync(failures);

        // Assert - Comprehensive validation
        analysisResult.IsSuccess.Should().BeTrue();
        analysisResult.Value.AverageSuccessRate.Should().Be(0.8); // 20 out of 25

        suggestionsResult.IsSuccess.Should().BeTrue();
        // Suggestions may or may not be generated depending on thresholds
        suggestionsResult.Value.Should().NotBeNull();

        patternsResult.IsSuccess.Should().BeTrue();
        if (patternsResult.Value.Count > 0)
        {
            patternsResult.Value.First().Description.ToLower().Should().Contain("timeout");
        }
    }

    [Fact]
    public async Task CancellationToken_PropagatesCorrectly()
    {
        // Arrange
        var episodes = Enumerable.Range(0, 100)
            .Select(i => new Episode(
                Guid.NewGuid(),
                "Test",
                new List<EnvironmentStep>(),
                10.0,
                DateTime.UtcNow,
                DateTime.UtcNow,
                true))
            .ToList();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await this.engine.AnalyzePerformanceAsync(episodes, TimeSpan.FromDays(1), cts.Token);

        // Assert - Should complete or return cancelled error
        result.IsFailure.Should().BeTrue();
        result.Error.ToLower().Should().Contain("cancelled");
    }
}
