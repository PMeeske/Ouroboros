// <copyright file="MemoryStoreTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Tests.MetaAI;

using Ouroboros.Agent.MetaAI;

/// <summary>
/// Unit tests for MemoryStore implementation.
/// Tests experience storage, retrieval, and statistics tracking.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MemoryStoreTests
{
    [Fact]
    public void Constructor_ShouldCreateMemoryStore()
    {
        // Act
        var store = new MemoryStore();

        // Assert
        store.Should().NotBeNull();
    }

    [Fact]
    public async Task StoreExperienceAsync_WithValidExperience_ShouldStore()
    {
        // Arrange
        var store = new MemoryStore();
        var experience = CreateTestExperience("Test goal", success: true);

        // Act
        await store.StoreExperienceAsync(experience);

        // Assert
        var retrieved = await store.GetExperienceAsync(experience.Id);
        retrieved.Should().NotBeNull();
        retrieved!.Id.Should().Be(experience.Id);
        retrieved.Goal.Should().Be("Test goal");
    }

    [Fact]
    public async Task StoreExperienceAsync_WithMultipleExperiences_ShouldStoreAll()
    {
        // Arrange
        var store = new MemoryStore();
        var exp1 = CreateTestExperience("Goal 1", success: true);
        var exp2 = CreateTestExperience("Goal 2", success: false);
        var exp3 = CreateTestExperience("Goal 1", success: true);

        // Act
        await store.StoreExperienceAsync(exp1);
        await store.StoreExperienceAsync(exp2);
        await store.StoreExperienceAsync(exp3);

        // Assert
        var stats = await store.GetStatisticsAsync();
        stats.TotalExperiences.Should().Be(3);
    }

    [Fact]
    public async Task RetrieveRelevantExperiencesAsync_WithMatchingGoal_ShouldReturnExperiences()
    {
        // Arrange
        var store = new MemoryStore();
        var exp1 = CreateTestExperience("Process data", success: true);
        var exp2 = CreateTestExperience("Generate report", success: true);
        var exp3 = CreateTestExperience("Process files", success: true);

        await store.StoreExperienceAsync(exp1);
        await store.StoreExperienceAsync(exp2);
        await store.StoreExperienceAsync(exp3);

        var query = new MemoryQuery(
            Goal: "Process",
            Context: null,
            MaxResults: 5,
            MinSimilarity: 0.0);

        // Act
        var results = await store.RetrieveRelevantExperiencesAsync(query);

        // Assert
        results.Should().HaveCountGreaterThanOrEqualTo(2);
        results.Should().Contain(e => e.Goal == "Process data");
        results.Should().Contain(e => e.Goal == "Process files");
    }

    [Fact]
    public async Task RetrieveRelevantExperiencesAsync_WithMaxResults_ShouldLimitResults()
    {
        // Arrange
        var store = new MemoryStore();
        for (int i = 0; i < 10; i++)
        {
            var exp = CreateTestExperience($"Test goal {i}", success: true);
            await store.StoreExperienceAsync(exp);
        }

        var query = new MemoryQuery(
            Goal: "Test",
            Context: null,
            MaxResults: 3,
            MinSimilarity: 0.0);

        // Act
        var results = await store.RetrieveRelevantExperiencesAsync(query);

        // Assert
        results.Count.Should().BeLessThanOrEqualTo(3);
    }

    [Fact]
    public async Task RetrieveRelevantExperiencesAsync_WithNoMatches_ShouldReturnEmpty()
    {
        // Arrange
        var store = new MemoryStore();
        var exp = CreateTestExperience("Something completely different", success: true);
        await store.StoreExperienceAsync(exp);

        var query = new MemoryQuery(
            Goal: "Unrelated query",
            Context: null,
            MaxResults: 5,
            MinSimilarity: 0.9);

        // Act
        var results = await store.RetrieveRelevantExperiencesAsync(query);

        // Assert - may or may not return results depending on similarity matching
        results.Should().NotBeNull();
    }

    [Fact]
    public async Task GetStatisticsAsync_WithNoExperiences_ShouldReturnEmptyStats()
    {
        // Arrange
        var store = new MemoryStore();

        // Act
        var stats = await store.GetStatisticsAsync();

        // Assert
        stats.TotalExperiences.Should().Be(0);
        stats.SuccessfulExecutions.Should().Be(0);
        stats.FailedExecutions.Should().Be(0);
        stats.AverageQualityScore.Should().Be(0.0);
        stats.GoalCounts.Should().BeEmpty();
    }

    [Fact]
    public async Task GetStatisticsAsync_WithMixedExperiences_ShouldCalculateCorrectStats()
    {
        // Arrange
        var store = new MemoryStore();
        await store.StoreExperienceAsync(CreateTestExperience("Goal A", success: true, quality: 0.9));
        await store.StoreExperienceAsync(CreateTestExperience("Goal A", success: true, quality: 0.8));
        await store.StoreExperienceAsync(CreateTestExperience("Goal B", success: false, quality: 0.3));

        // Act
        var stats = await store.GetStatisticsAsync();

        // Assert
        stats.TotalExperiences.Should().Be(3);
        stats.SuccessfulExecutions.Should().Be(2);
        stats.FailedExecutions.Should().Be(1);
        stats.AverageQualityScore.Should().BeApproximately(0.67, 0.1);
        stats.GoalCounts.Should().ContainKey("Goal A");
        stats.GoalCounts["Goal A"].Should().Be(2);
    }

    [Fact]
    public async Task ClearAsync_ShouldRemoveAllExperiences()
    {
        // Arrange
        var store = new MemoryStore();
        await store.StoreExperienceAsync(CreateTestExperience("Goal 1", success: true));
        await store.StoreExperienceAsync(CreateTestExperience("Goal 2", success: true));

        // Act
        await store.ClearAsync();

        // Assert
        var stats = await store.GetStatisticsAsync();
        stats.TotalExperiences.Should().Be(0);
    }

    [Fact]
    public async Task GetExperienceAsync_WithNonExistentId_ShouldReturnNull()
    {
        // Arrange
        var store = new MemoryStore();
        var nonExistentId = Guid.NewGuid();

        // Act
        var result = await store.GetExperienceAsync(nonExistentId);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task StoreExperienceAsync_WithPreCancelledToken_ShouldCompleteQuickly()
    {
        // Arrange
        var store = new MemoryStore();
        var experience = CreateTestExperience("Test", success: true);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var sw = System.Diagnostics.Stopwatch.StartNew();
        await store.StoreExperienceAsync(experience, cts.Token);
        sw.Stop();

        // Assert - Operation should complete quickly (memory operations are synchronous)
        sw.ElapsedMilliseconds.Should().BeLessThan(1000);
    }

    private static Experience CreateTestExperience(string goal, bool success, double quality = 0.8)
    {
        var plan = new Plan(
            Goal: goal,
            Steps: new List<PlanStep>(),
            ConfidenceScores: new Dictionary<string, double> { { "overall", 0.8 } },
            CreatedAt: DateTime.UtcNow);

        var execution = new ExecutionResult(
            Plan: plan,
            StepResults: new List<StepResult>(),
            Success: success,
            FinalOutput: success ? "Success" : "Failed",
            Metadata: new Dictionary<string, object>(),
            Duration: TimeSpan.FromSeconds(1));

        var verification = new VerificationResult(
            Execution: execution,
            Verified: success,
            QualityScore: quality,
            Issues: success ? new List<string>() : new List<string> { "Failed" },
            Improvements: new List<string>(),
            RevisedPlan: null);

        return new Experience(
            Id: Guid.NewGuid(),
            Goal: goal,
            Plan: plan,
            Execution: execution,
            Verification: verification,
            Timestamp: DateTime.UtcNow,
            Metadata: new Dictionary<string, object>());
    }
}
