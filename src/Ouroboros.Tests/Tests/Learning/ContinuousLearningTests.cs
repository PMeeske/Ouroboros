// <copyright file="ContinuousLearningTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Learning;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Ouroboros.Core.Monads;
using Ouroboros.Pipeline.Learning;
using Xunit;

/// <summary>
/// Comprehensive unit tests for the Continuous Learning and Adaptation feature.
/// Tests Experience Replay, Meta-Learning, Online Learning, and Adaptive Agent components.
/// </summary>
[Trait("Category", "Unit")]
public class ContinuousLearningTests
{
    #region Experience Tests

    [Fact]
    public void Experience_Create_ShouldReturnValidExperience()
    {
        // Arrange & Act
        var experience = Experience.Create("state1", "action1", 1.0, "state2");

        // Assert
        experience.State.Should().Be("state1");
        experience.Action.Should().Be("action1");
        experience.Reward.Should().Be(1.0);
        experience.NextState.Should().Be("state2");
        experience.Id.Should().NotBeEmpty();
        experience.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Experience_Create_WithDefaultPriority_ShouldBeOne()
    {
        // Act
        var experience = Experience.Create("state", "action", 0.5, "next");

        // Assert
        experience.Priority.Should().Be(1.0);
    }

    [Fact]
    public void Experience_Create_WithCustomPriority_ShouldSetPriority()
    {
        // Act
        var experience = Experience.Create("state", "action", 0.5, "next", priority: 2.5);

        // Assert
        experience.Priority.Should().Be(2.5);
    }

    [Fact]
    public void Experience_Create_WithMetadata_ShouldPreserveValues()
    {
        // Arrange
        var metadata = ImmutableDictionary<string, object>.Empty
            .Add("key1", "value1")
            .Add("key2", 42);

        // Act
        var experience = Experience.Create("state", "action", 0.5, "next", metadata: metadata);

        // Assert
        experience.Metadata.Should().ContainKey("key1");
        experience.Metadata["key1"].Should().Be("value1");
        experience.Metadata["key2"].Should().Be(42);
    }

    [Fact]
    public void Experience_WithTDErrorPriority_ShouldUpdatePriority()
    {
        // Arrange
        var experience = Experience.Create("state", "action", 0.5, "next");
        var tdError = 0.3;

        // Act
        var updated = experience.WithTDErrorPriority(tdError);

        // Assert
        updated.Priority.Should().BeApproximately(0.31, 0.001); // |0.3| + 0.01 epsilon
        updated.State.Should().Be(experience.State); // Other fields unchanged
    }

    [Fact]
    public void Experience_WithTDErrorPriority_WithNegativeError_ShouldUseAbsoluteValue()
    {
        // Arrange
        var experience = Experience.Create("state", "action", 0.5, "next");

        // Act
        var updated = experience.WithTDErrorPriority(-0.5);

        // Assert
        updated.Priority.Should().BeApproximately(0.51, 0.001);
    }

    [Fact]
    public void Experience_WithMetadata_ShouldAddNewEntry()
    {
        // Arrange
        var experience = Experience.Create("state", "action", 0.5, "next");

        // Act
        var updated = experience.WithMetadata("newKey", "newValue");

        // Assert
        updated.Metadata.Should().ContainKey("newKey");
        updated.Metadata["newKey"].Should().Be("newValue");
        experience.Metadata.Should().NotContainKey("newKey"); // Original unchanged
    }

    [Fact]
    public void Experience_WithMetadata_ShouldOverwriteExistingEntry()
    {
        // Arrange
        var experience = Experience.Create("state", "action", 0.5, "next")
            .WithMetadata("key", "oldValue");

        // Act
        var updated = experience.WithMetadata("key", "newValue");

        // Assert
        updated.Metadata["key"].Should().Be("newValue");
    }

    [Fact]
    public void Experience_Record_ShouldBeImmutable()
    {
        // Arrange
        var experience = Experience.Create("state", "action", 0.5, "next");

        // Act
        var modified = experience with { State = "modified" };

        // Assert
        experience.State.Should().Be("state");
        modified.State.Should().Be("modified");
    }

    [Fact]
    public void Experience_Create_WithNegativeReward_ShouldBeAllowed()
    {
        // Act
        var experience = Experience.Create("state", "action", -0.8, "next");

        // Assert
        experience.Reward.Should().Be(-0.8);
    }

    [Fact]
    public void Experience_Create_WithZeroReward_ShouldBeAllowed()
    {
        // Act
        var experience = Experience.Create("state", "action", 0.0, "next");

        // Assert
        experience.Reward.Should().Be(0.0);
    }

    [Fact]
    public void Experience_WithTDErrorPriority_WithCustomEpsilon_ShouldUseCustomValue()
    {
        // Arrange
        var experience = Experience.Create("state", "action", 0.5, "next");

        // Act
        var updated = experience.WithTDErrorPriority(0.2, epsilon: 0.05);

        // Assert
        updated.Priority.Should().BeApproximately(0.25, 0.001);
    }

    [Fact]
    public void Experience_UniqueIds_ShouldBeDifferent()
    {
        // Act
        var exp1 = Experience.Create("state", "action", 0.5, "next");
        var exp2 = Experience.Create("state", "action", 0.5, "next");

        // Assert
        exp1.Id.Should().NotBe(exp2.Id);
    }

    #endregion

    #region ExperienceBuffer Tests

    [Fact]
    public void ExperienceBuffer_Constructor_WithValidCapacity_ShouldInitialize()
    {
        // Act
        var buffer = new ExperienceBuffer(capacity: 100);

        // Assert
        buffer.Capacity.Should().Be(100);
        buffer.Count.Should().Be(0);
    }

    [Fact]
    public void ExperienceBuffer_Constructor_WithInvalidCapacity_ShouldThrow()
    {
        // Act & Assert
        var act = () => new ExperienceBuffer(capacity: 0);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ExperienceBuffer_Constructor_WithNegativeCapacity_ShouldThrow()
    {
        // Act & Assert
        var act = () => new ExperienceBuffer(capacity: -1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void ExperienceBuffer_Add_ShouldIncreaseCount()
    {
        // Arrange
        var buffer = new ExperienceBuffer(capacity: 10);
        var experience = Experience.Create("state", "action", 0.5, "next");

        // Act
        buffer.Add(experience);

        // Assert
        buffer.Count.Should().Be(1);
    }

    [Fact]
    public void ExperienceBuffer_Add_MultipleExperiences_ShouldTrackCount()
    {
        // Arrange
        var buffer = new ExperienceBuffer(capacity: 100);

        // Act
        for (int i = 0; i < 5; i++)
        {
            buffer.Add(Experience.Create($"state{i}", $"action{i}", 0.1 * i, $"next{i}"));
        }

        // Assert
        buffer.Count.Should().Be(5);
    }

    [Fact]
    public void ExperienceBuffer_Add_WhenAtCapacity_ShouldEvictOldest()
    {
        // Arrange
        var buffer = new ExperienceBuffer(capacity: 3);
        var exp1 = Experience.Create("state1", "action1", 0.1, "next1");
        var exp2 = Experience.Create("state2", "action2", 0.2, "next2");
        var exp3 = Experience.Create("state3", "action3", 0.3, "next3");
        var exp4 = Experience.Create("state4", "action4", 0.4, "next4");

        // Act
        buffer.Add(exp1);
        buffer.Add(exp2);
        buffer.Add(exp3);
        buffer.Add(exp4); // Should evict exp1

        // Assert
        buffer.Count.Should().Be(3);
        var all = buffer.GetAll();
        all.Should().NotContain(e => e.State == "state1");
        all.Should().Contain(e => e.State == "state4");
    }

    [Fact]
    public void ExperienceBuffer_Add_NullExperience_ShouldThrow()
    {
        // Arrange
        var buffer = new ExperienceBuffer(capacity: 10);

        // Act & Assert
        var act = () => buffer.Add(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ExperienceBuffer_Sample_ShouldReturnRequestedCount()
    {
        // Arrange
        var buffer = new ExperienceBuffer(capacity: 100, seed: 42);
        for (int i = 0; i < 10; i++)
        {
            buffer.Add(Experience.Create($"state{i}", $"action{i}", 0.1 * i, $"next{i}"));
        }

        // Act
        var samples = buffer.Sample(5);

        // Assert
        samples.Should().HaveCount(5);
    }

    [Fact]
    public void ExperienceBuffer_Sample_WithEmptyBuffer_ShouldReturnEmpty()
    {
        // Arrange
        var buffer = new ExperienceBuffer(capacity: 10);

        // Act
        var samples = buffer.Sample(5);

        // Assert
        samples.Should().BeEmpty();
    }

    [Fact]
    public void ExperienceBuffer_Sample_WithZeroBatchSize_ShouldReturnEmpty()
    {
        // Arrange
        var buffer = new ExperienceBuffer(capacity: 10);
        buffer.Add(Experience.Create("state", "action", 0.5, "next"));

        // Act
        var samples = buffer.Sample(0);

        // Assert
        samples.Should().BeEmpty();
    }

    [Fact]
    public void ExperienceBuffer_Sample_WithNegativeBatchSize_ShouldReturnEmpty()
    {
        // Arrange
        var buffer = new ExperienceBuffer(capacity: 10);
        buffer.Add(Experience.Create("state", "action", 0.5, "next"));

        // Act
        var samples = buffer.Sample(-1);

        // Assert
        samples.Should().BeEmpty();
    }

    [Fact]
    public void ExperienceBuffer_Sample_WhenBatchSizeExceedsCount_ShouldReturnAvailable()
    {
        // Arrange
        var buffer = new ExperienceBuffer(capacity: 100);
        for (int i = 0; i < 3; i++)
        {
            buffer.Add(Experience.Create($"state{i}", $"action{i}", 0.1 * i, $"next{i}"));
        }

        // Act
        var samples = buffer.Sample(10);

        // Assert
        samples.Should().HaveCount(3);
    }

    [Fact]
    public void ExperienceBuffer_Sample_ShouldReturnUniqueExperiences()
    {
        // Arrange
        var buffer = new ExperienceBuffer(capacity: 100, seed: 42);
        for (int i = 0; i < 10; i++)
        {
            buffer.Add(Experience.Create($"state{i}", $"action{i}", 0.1 * i, $"next{i}"));
        }

        // Act
        var samples = buffer.Sample(5);

        // Assert
        samples.Select(e => e.Id).Distinct().Should().HaveCount(5);
    }

    [Fact]
    public void ExperienceBuffer_SamplePrioritized_ShouldReturnRequestedCount()
    {
        // Arrange
        var buffer = new ExperienceBuffer(capacity: 100, seed: 42);
        for (int i = 0; i < 10; i++)
        {
            buffer.Add(Experience.Create($"state{i}", $"action{i}", 0.1 * i, $"next{i}", priority: i + 1));
        }

        // Act
        var samples = buffer.SamplePrioritized(5, alpha: 0.6);

        // Assert
        samples.Should().HaveCount(5);
    }

    [Fact]
    public void ExperienceBuffer_SamplePrioritized_WithEmptyBuffer_ShouldReturnEmpty()
    {
        // Arrange
        var buffer = new ExperienceBuffer(capacity: 10);

        // Act
        var samples = buffer.SamplePrioritized(5);

        // Assert
        samples.Should().BeEmpty();
    }

    [Fact]
    public void ExperienceBuffer_SamplePrioritized_WithHighAlpha_ShouldFavorHighPriority()
    {
        // Arrange
        var buffer = new ExperienceBuffer(capacity: 100, seed: 42);
        buffer.Add(Experience.Create("low", "action", 0.1, "next", priority: 0.1));
        buffer.Add(Experience.Create("high", "action", 0.9, "next", priority: 10.0));

        // Act - sample many times to verify statistical bias
        var highPriorityCount = 0;
        for (int i = 0; i < 100; i++)
        {
            var sample = buffer.SamplePrioritized(1, alpha: 1.0);
            if (sample[0].State == "high")
            {
                highPriorityCount++;
            }
        }

        // Assert - high priority should be sampled more often
        highPriorityCount.Should().BeGreaterThan(50);
    }

    [Fact]
    public void ExperienceBuffer_Clear_ShouldRemoveAllExperiences()
    {
        // Arrange
        var buffer = new ExperienceBuffer(capacity: 10);
        for (int i = 0; i < 5; i++)
        {
            buffer.Add(Experience.Create($"state{i}", $"action{i}", 0.1 * i, $"next{i}"));
        }

        // Act
        buffer.Clear();

        // Assert
        buffer.Count.Should().Be(0);
    }

    [Fact]
    public void ExperienceBuffer_UpdatePriority_ShouldUpdateExistingExperience()
    {
        // Arrange
        var buffer = new ExperienceBuffer(capacity: 10);
        var experience = Experience.Create("state", "action", 0.5, "next", priority: 1.0);
        buffer.Add(experience);

        // Act
        var result = buffer.UpdatePriority(experience.Id, 5.0);

        // Assert
        result.Should().BeTrue();
        buffer.GetAll().First().Priority.Should().Be(5.0);
    }

    [Fact]
    public void ExperienceBuffer_UpdatePriority_WithNonExistentId_ShouldReturnFalse()
    {
        // Arrange
        var buffer = new ExperienceBuffer(capacity: 10);
        buffer.Add(Experience.Create("state", "action", 0.5, "next"));

        // Act
        var result = buffer.UpdatePriority(Guid.NewGuid(), 5.0);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ExperienceBuffer_GetAll_ShouldReturnAllExperiences()
    {
        // Arrange
        var buffer = new ExperienceBuffer(capacity: 10);
        for (int i = 0; i < 5; i++)
        {
            buffer.Add(Experience.Create($"state{i}", $"action{i}", 0.1 * i, $"next{i}"));
        }

        // Act
        var all = buffer.GetAll();

        // Assert
        all.Should().HaveCount(5);
    }

    [Fact]
    public void ExperienceBuffer_ThreadSafety_ShouldHandleConcurrentAccess()
    {
        // Arrange
        var buffer = new ExperienceBuffer(capacity: 1000);
        var tasks = new List<Task>();

        // Act - concurrent adds
        for (int i = 0; i < 100; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                buffer.Add(Experience.Create($"state{index}", $"action{index}", 0.01 * index, $"next{index}"));
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        buffer.Count.Should().Be(100);
    }

    #endregion

    #region LearningStrategy Tests

    [Fact]
    public void LearningStrategy_Default_ShouldHaveValidValues()
    {
        // Act
        var strategy = LearningStrategy.Default;

        // Assert
        strategy.LearningRate.Should().BeApproximately(0.001, 0.0001);
        strategy.ExplorationRate.Should().BeApproximately(0.1, 0.01);
        strategy.DiscountFactor.Should().BeApproximately(0.99, 0.001);
        strategy.BatchSize.Should().Be(32);
        strategy.Name.Should().Be("Default");
    }

    [Fact]
    public void LearningStrategy_Exploratory_ShouldHaveHighExplorationRate()
    {
        // Act
        var strategy = LearningStrategy.Exploratory();

        // Assert
        strategy.ExplorationRate.Should().Be(0.5);
        strategy.LearningRate.Should().Be(0.01);
        strategy.Parameters.Should().ContainKey("temperature");
        strategy.Parameters["temperature"].Should().Be(1.5);
    }

    [Fact]
    public void LearningStrategy_Exploitative_ShouldHaveLowExplorationRate()
    {
        // Act
        var strategy = LearningStrategy.Exploitative();

        // Assert
        strategy.ExplorationRate.Should().Be(0.01);
        strategy.LearningRate.Should().Be(0.0001);
        strategy.BatchSize.Should().Be(128);
    }

    [Fact]
    public void LearningStrategy_Validate_WithValidStrategy_ShouldSucceed()
    {
        // Arrange
        var strategy = LearningStrategy.Default;

        // Act
        var result = strategy.Validate();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void LearningStrategy_Validate_WithEmptyName_ShouldFail()
    {
        // Arrange
        var strategy = LearningStrategy.Default with { Name = string.Empty };

        // Act
        var result = strategy.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("name");
    }

    [Fact]
    public void LearningStrategy_Validate_WithInvalidLearningRate_ShouldFail()
    {
        // Arrange
        var strategy = LearningStrategy.Default with { LearningRate = 0 };

        // Act
        var result = strategy.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Learning rate");
    }

    [Fact]
    public void LearningStrategy_Validate_WithNegativeExplorationRate_ShouldFail()
    {
        // Arrange
        var strategy = LearningStrategy.Default with { ExplorationRate = -0.1 };

        // Act
        var result = strategy.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void LearningStrategy_Validate_WithInvalidBatchSize_ShouldFail()
    {
        // Arrange
        var strategy = LearningStrategy.Default with { BatchSize = 0 };

        // Act
        var result = strategy.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void LearningStrategy_WithLearningRate_ShouldReturnNewInstance()
    {
        // Arrange
        var original = LearningStrategy.Default;

        // Act
        var modified = original.WithLearningRate(0.05);

        // Assert
        modified.LearningRate.Should().Be(0.05);
        original.LearningRate.Should().Be(0.001); // Unchanged
    }

    [Fact]
    public void LearningStrategy_WithLearningRate_ShouldClampValue()
    {
        // Arrange
        var strategy = LearningStrategy.Default;

        // Act
        var clamped = strategy.WithLearningRate(5.0);

        // Assert
        clamped.LearningRate.Should().Be(1.0); // Clamped to max
    }

    [Fact]
    public void LearningStrategy_WithExplorationRate_ShouldClampToValidRange()
    {
        // Arrange
        var strategy = LearningStrategy.Default;

        // Act
        var clampedHigh = strategy.WithExplorationRate(1.5);
        var clampedLow = strategy.WithExplorationRate(-0.5);

        // Assert
        clampedHigh.ExplorationRate.Should().Be(1.0);
        clampedLow.ExplorationRate.Should().Be(0.0);
    }

    [Fact]
    public void LearningStrategy_WithParameter_ShouldAddParameter()
    {
        // Arrange
        var strategy = LearningStrategy.Default;

        // Act
        var modified = strategy.WithParameter("customParam", 42.0);

        // Assert
        modified.Parameters.Should().ContainKey("customParam");
        modified.Parameters["customParam"].Should().Be(42.0);
    }

    #endregion

    #region LearningMetrics Tests

    [Fact]
    public void LearningMetrics_Empty_ShouldHaveZeroValues()
    {
        // Act
        var metrics = LearningMetrics.Empty;

        // Assert
        metrics.TotalEpisodes.Should().Be(0);
        metrics.AverageReward.Should().Be(0.0);
        metrics.RewardVariance.Should().Be(0.0);
    }

    [Fact]
    public void LearningMetrics_FromRewards_ShouldComputeCorrectAverage()
    {
        // Arrange
        var rewards = new[] { 1.0, 2.0, 3.0, 4.0, 5.0 };

        // Act
        var metrics = LearningMetrics.FromRewards(rewards);

        // Assert
        metrics.TotalEpisodes.Should().Be(5);
        metrics.AverageReward.Should().Be(3.0);
    }

    [Fact]
    public void LearningMetrics_FromRewards_ShouldComputeCorrectVariance()
    {
        // Arrange
        var rewards = new[] { 2.0, 4.0, 6.0 };

        // Act
        var metrics = LearningMetrics.FromRewards(rewards);

        // Assert - variance of [2,4,6] with mean 4 is ((4+0+4)/3) = 8/3 ≈ 2.67
        metrics.RewardVariance.Should().BeApproximately(2.67, 0.01);
    }

    [Fact]
    public void LearningMetrics_FromRewards_WithEmptyList_ShouldReturnEmpty()
    {
        // Act
        var metrics = LearningMetrics.FromRewards(Array.Empty<double>());

        // Assert
        metrics.TotalEpisodes.Should().Be(0);
        metrics.AverageReward.Should().Be(0.0);
    }

    [Fact]
    public void LearningMetrics_WithNewReward_ShouldUpdateIncrementally()
    {
        // Arrange
        var metrics = LearningMetrics.FromRewards(new[] { 1.0, 2.0, 3.0 });

        // Act
        var updated = metrics.WithNewReward(4.0);

        // Assert
        updated.TotalEpisodes.Should().Be(4);
        updated.AverageReward.Should().Be(2.5);
    }

    [Fact]
    public void LearningMetrics_WithNewReward_OnEmpty_ShouldInitialize()
    {
        // Arrange
        var metrics = LearningMetrics.Empty;

        // Act
        var updated = metrics.WithNewReward(1.0);

        // Assert
        updated.TotalEpisodes.Should().Be(1);
        updated.AverageReward.Should().Be(1.0);
    }

    [Fact]
    public void LearningMetrics_ComputePerformanceScore_ShouldNormalize()
    {
        // Arrange
        var metrics = LearningMetrics.FromRewards(new[] { 0.5, 0.6, 0.7, 0.8, 0.9 });

        // Act
        var score = metrics.ComputePerformanceScore();

        // Assert
        score.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public void LearningMetrics_ComputePerformanceScore_WithZeroEpisodes_ShouldReturnZero()
    {
        // Arrange
        var metrics = LearningMetrics.Empty;

        // Act
        var score = metrics.ComputePerformanceScore();

        // Assert
        score.Should().Be(0.0);
    }

    [Fact]
    public void LearningMetrics_ComputePerformanceScore_HighAverageReward_ShouldHaveHighScore()
    {
        // Arrange
        var highRewardMetrics = LearningMetrics.FromRewards(Enumerable.Repeat(0.9, 20));
        var lowRewardMetrics = LearningMetrics.FromRewards(Enumerable.Repeat(0.1, 20));

        // Act
        var highScore = highRewardMetrics.ComputePerformanceScore();
        var lowScore = lowRewardMetrics.ComputePerformanceScore();

        // Assert
        highScore.Should().BeGreaterThan(lowScore);
    }

    [Fact]
    public void LearningMetrics_Timestamps_ShouldTrackUpdateHistory()
    {
        // Arrange
        var metrics = LearningMetrics.Empty;

        // Act
        var updated1 = metrics.WithNewReward(1.0);
        var updated2 = updated1.WithNewReward(2.0);

        // Assert
        updated2.Timestamps.Count.Should().Be(2);
    }

    #endregion

    #region MetaLearner Tests

    [Fact]
    public void AdaptiveMetaLearner_EvaluateStrategy_ShouldReturnScore()
    {
        // Arrange
        var learner = new AdaptiveMetaLearner(seed: 42);
        var strategy = LearningStrategy.Default;
        var metrics = LearningMetrics.FromRewards(new[] { 0.5, 0.6, 0.7 });

        // Act
        var score = learner.EvaluateStrategy(strategy, metrics);

        // Assert
        score.Should().NotBe(double.NaN);
    }

    [Fact]
    public void AdaptiveMetaLearner_EvaluateStrategy_ValidStrategy_ShouldHaveHigherScore()
    {
        // Arrange
        var learner = new AdaptiveMetaLearner(seed: 42);
        var validStrategy = LearningStrategy.Default;
        var invalidStrategy = LearningStrategy.Default with { Name = string.Empty };
        var metrics = LearningMetrics.FromRewards(new[] { 0.5, 0.6, 0.7 });

        // Act
        var validScore = learner.EvaluateStrategy(validStrategy, metrics);
        var invalidScore = learner.EvaluateStrategy(invalidStrategy, metrics);

        // Assert
        validScore.Should().BeGreaterThan(invalidScore);
    }

    [Fact]
    public void AdaptiveMetaLearner_AdaptStrategy_ShouldReturnModifiedStrategy()
    {
        // Arrange
        var learner = new AdaptiveMetaLearner(seed: 42);
        var strategy = LearningStrategy.Default;
        var metrics = LearningMetrics.FromRewards(Enumerable.Repeat(0.5, 20));

        // Act
        var result = learner.AdaptStrategy(strategy, metrics);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // At least one parameter should change due to adaptation
        var adapted = result.Value;
        (adapted.LearningRate != strategy.LearningRate ||
         adapted.ExplorationRate != strategy.ExplorationRate ||
         adapted.DiscountFactor != strategy.DiscountFactor).Should().BeTrue();
    }

    [Fact]
    public void AdaptiveMetaLearner_SelectBestStrategy_ShouldReturnHighestScoring()
    {
        // Arrange
        var learner = new AdaptiveMetaLearner(explorationWeight: 0.0, seed: 42);
        var strategies = new[]
        {
            LearningStrategy.Default,
            LearningStrategy.Exploratory(),
            LearningStrategy.Exploitative(),
        };
        var metrics = LearningMetrics.FromRewards(Enumerable.Repeat(0.8, 50));

        // Act
        var result = learner.SelectBestStrategy(strategies, metrics);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void AdaptiveMetaLearner_SelectBestStrategy_WithEmptyList_ShouldFail()
    {
        // Arrange
        var learner = new AdaptiveMetaLearner();
        var metrics = LearningMetrics.FromRewards(new[] { 0.5 });

        // Act
        var result = learner.SelectBestStrategy(Array.Empty<LearningStrategy>(), metrics);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void AdaptiveMetaLearner_GetHistory_ForNewStrategy_ShouldReturnNone()
    {
        // Arrange
        var learner = new AdaptiveMetaLearner();

        // Act
        var history = learner.GetHistory(Guid.NewGuid());

        // Assert
        history.HasValue.Should().BeFalse();
    }

    [Fact]
    public void AdaptiveMetaLearner_GetHistory_AfterAdaptation_ShouldReturnHistory()
    {
        // Arrange
        var learner = new AdaptiveMetaLearner();
        var strategy = LearningStrategy.Default;
        var metrics = LearningMetrics.FromRewards(Enumerable.Repeat(0.5, 20));
        learner.AdaptStrategy(strategy, metrics);

        // Act
        var history = learner.GetHistory(strategy.Id);

        // Assert
        history.HasValue.Should().BeTrue();
    }

    #endregion

    #region Feedback Tests

    [Fact]
    public void Feedback_Explicit_ShouldCreateCorrectType()
    {
        // Act
        var feedback = Feedback.Explicit("source1", "input", "output", 0.8, "tag1", "tag2");

        // Assert
        feedback.Type.Should().Be(FeedbackType.Explicit);
        feedback.SourceId.Should().Be("source1");
        feedback.Score.Should().Be(0.8);
        feedback.Tags.Should().Contain("tag1");
        feedback.Tags.Should().Contain("tag2");
    }

    [Fact]
    public void Feedback_Implicit_ShouldCreateCorrectType()
    {
        // Act
        var feedback = Feedback.Implicit("source1", "input", "output", 0.5);

        // Assert
        feedback.Type.Should().Be(FeedbackType.Implicit);
    }

    [Fact]
    public void Feedback_Corrective_ShouldCreateWithNegativeScore()
    {
        // Act
        var feedback = Feedback.Corrective("source1", "input", "actualOutput", "preferredOutput");

        // Assert
        feedback.Type.Should().Be(FeedbackType.Corrective);
        feedback.Score.Should().Be(-0.5);
        feedback.Tags.Should().Contain(t => t.StartsWith("preferred:"));
    }

    [Fact]
    public void Feedback_Comparative_ShouldCreateCorrectType()
    {
        // Act
        var feedback = Feedback.Comparative("source1", "input", "chosen", "rejected", 0.7);

        // Assert
        feedback.Type.Should().Be(FeedbackType.Comparative);
        feedback.Score.Should().Be(0.7);
        feedback.Tags.Should().Contain(t => t.StartsWith("rejected:"));
    }

    [Fact]
    public void Feedback_Score_ShouldBeClamped()
    {
        // Act
        var highFeedback = Feedback.Explicit("source", "input", "output", 1.5);
        var lowFeedback = Feedback.Explicit("source", "input", "output", -1.5);

        // Assert
        highFeedback.Score.Should().Be(1.0);
        lowFeedback.Score.Should().Be(-1.0);
    }

    [Fact]
    public void Feedback_Validate_WithValidFeedback_ShouldSucceed()
    {
        // Arrange
        var feedback = Feedback.Explicit("source", "input", "output", 0.5);

        // Act
        var result = feedback.Validate();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Feedback_Validate_WithEmptySourceId_ShouldFail()
    {
        // Arrange
        var feedback = Feedback.Explicit(string.Empty, "input", "output", 0.5);

        // Act
        var result = feedback.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Feedback_WithTags_ShouldAddTags()
    {
        // Arrange
        var feedback = Feedback.Explicit("source", "input", "output", 0.5);

        // Act
        var withTags = feedback.WithTags("newTag1", "newTag2");

        // Assert
        withTags.Tags.Should().Contain("newTag1");
        withTags.Tags.Should().Contain("newTag2");
    }

    [Fact]
    public void Feedback_Tags_ShouldBeImmutable()
    {
        // Arrange
        var feedback = Feedback.Explicit("source", "input", "output", 0.5, "original");

        // Act
        var modified = feedback.WithTags("added");

        // Assert
        feedback.Tags.Should().NotContain("added");
        modified.Tags.Should().Contain("original");
        modified.Tags.Should().Contain("added");
    }

    #endregion

    #region LearningUpdate Tests

    [Fact]
    public void LearningUpdate_FromGradient_ShouldComputeCorrectly()
    {
        // Arrange
        var paramName = "weight1";
        var currentValue = 0.5;
        var gradient = 0.1;
        var learningRate = 0.01;

        // Act
        var update = LearningUpdate.FromGradient(paramName, currentValue, gradient, learningRate);

        // Assert
        update.ParameterName.Should().Be(paramName);
        update.OldValue.Should().Be(currentValue);
        update.NewValue.Should().BeApproximately(0.499, 0.001); // 0.5 - (0.01 * 0.1)
        update.Gradient.Should().Be(gradient);
    }

    [Fact]
    public void LearningUpdate_Magnitude_ShouldBeAbsoluteDifference()
    {
        // Arrange
        var update = LearningUpdate.FromGradient("param", 1.0, 0.5, 0.1);

        // Act
        var magnitude = update.Magnitude;

        // Assert
        magnitude.Should().BeApproximately(0.05, 0.001);
    }

    [Fact]
    public void LearningUpdate_Scale_ShouldMultiplyValues()
    {
        // Arrange
        var update = LearningUpdate.FromGradient("param", 1.0, 1.0, 0.1);

        // Act
        var scaled = update.Scale(0.5);

        // Assert
        scaled.Magnitude.Should().BeApproximately(update.Magnitude * 0.5, 0.001);
    }

    [Fact]
    public void LearningUpdate_MergeWith_ShouldAverageByConfidence()
    {
        // Arrange
        var update1 = new LearningUpdate("param", 1.0, 1.1, 0.1, 0.8);
        var update2 = new LearningUpdate("param", 1.0, 1.2, 0.2, 0.2);

        // Act
        var merged = update1.MergeWith(update2);

        // Assert
        merged.ParameterName.Should().Be("param");
        // Weighted average: (1.1*0.8 + 1.2*0.2) / 1.0 = 1.12
        merged.NewValue.Should().BeApproximately(1.12, 0.001);
    }

    [Fact]
    public void LearningUpdate_MergeWith_DifferentParameters_ShouldThrow()
    {
        // Arrange
        var update1 = new LearningUpdate("param1", 1.0, 1.1, 0.1, 1.0);
        var update2 = new LearningUpdate("param2", 1.0, 1.2, 0.2, 1.0);

        // Act & Assert
        var act = () => update1.MergeWith(update2);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void LearningUpdate_Confidence_ShouldBeClamped()
    {
        // Act
        var update = LearningUpdate.FromGradient("param", 1.0, 0.1, 0.01, confidence: 1.5);

        // Assert
        update.Confidence.Should().Be(1.0);
    }

    [Fact]
    public void LearningUpdate_FromGradient_NegativeGradient_ShouldIncreaseValue()
    {
        // Act
        var update = LearningUpdate.FromGradient("param", 1.0, -0.1, 0.1);

        // Assert
        update.NewValue.Should().BeGreaterThan(update.OldValue);
    }

    [Fact]
    public void LearningUpdate_Scale_WithZero_ShouldResetToOldValue()
    {
        // Arrange
        var update = LearningUpdate.FromGradient("param", 1.0, 0.5, 0.1);

        // Act
        var scaled = update.Scale(0.0);

        // Assert
        scaled.NewValue.Should().Be(scaled.OldValue);
    }

    #endregion

    #region OnlineLearner Tests

    [Fact]
    public void GradientBasedLearner_ProcessFeedback_ShouldUpdateMetrics()
    {
        // Arrange
        var learner = new GradientBasedLearner();
        var feedback = Feedback.Explicit("source", "input", "output", 0.8);

        // Act
        var result = learner.ProcessFeedback(feedback);

        // Assert
        result.IsSuccess.Should().BeTrue();
        learner.Metrics.ProcessedCount.Should().Be(1);
    }

    [Fact]
    public void GradientBasedLearner_ProcessFeedback_WithInvalidFeedback_ShouldFail()
    {
        // Arrange
        var learner = new GradientBasedLearner();
        var invalidFeedback = Feedback.Explicit(string.Empty, "input", "output", 0.5);

        // Act
        var result = learner.ProcessFeedback(invalidFeedback);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void GradientBasedLearner_ProcessBatch_ShouldHandleMultiple()
    {
        // Arrange
        var learner = new GradientBasedLearner();
        var batch = new[]
        {
            Feedback.Explicit("source", "input1", "output1", 0.8),
            Feedback.Explicit("source", "input2", "output2", 0.6),
            Feedback.Explicit("source", "input3", "output3", 0.9),
        };

        // Act
        var result = learner.ProcessBatch(batch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        learner.Metrics.ProcessedCount.Should().Be(3);
    }

    [Fact]
    public void GradientBasedLearner_ProcessBatch_WithEmptyBatch_ShouldReturnEmpty()
    {
        // Arrange
        var learner = new GradientBasedLearner();

        // Act
        var result = learner.ProcessBatch(Array.Empty<Feedback>());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public void GradientBasedLearner_GetPendingUpdates_ShouldReturnAccumulated()
    {
        // Arrange
        var config = GradientLearnerConfig.Default with { BatchAccumulationSize = 10 };
        var learner = new GradientBasedLearner(config);
        learner.ProcessFeedback(Feedback.Explicit("source", "input", "output", 0.8));

        // Act
        var pending = learner.GetPendingUpdates();

        // Assert
        pending.Should().NotBeEmpty();
    }

    [Fact]
    public void GradientBasedLearner_ApplyUpdates_ShouldClearPending()
    {
        // Arrange
        var config = GradientLearnerConfig.Default with { BatchAccumulationSize = 10 };
        var learner = new GradientBasedLearner(config);
        learner.ProcessFeedback(Feedback.Explicit("source", "input", "output", 0.8));

        // Act
        var result = learner.ApplyUpdates();

        // Assert
        result.IsSuccess.Should().BeTrue();
        learner.GetPendingUpdates().Should().BeEmpty();
    }

    [Fact]
    public void GradientBasedLearner_GetParameter_ExistingParameter_ShouldReturnSome()
    {
        // Arrange
        var initialParams = new Dictionary<string, double> { { "weight", 0.5 } };
        var learner = new GradientBasedLearner(initialParameters: initialParams);

        // Act
        var result = learner.GetParameter("weight");

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value.Should().Be(0.5);
    }

    [Fact]
    public void GradientBasedLearner_GetParameter_NonExistentParameter_ShouldReturnNone()
    {
        // Arrange
        var learner = new GradientBasedLearner();

        // Act
        var result = learner.GetParameter("nonexistent");

        // Assert
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void GradientBasedLearner_SetParameter_ShouldUpdateValue()
    {
        // Arrange
        var learner = new GradientBasedLearner();

        // Act
        learner.SetParameter("newParam", 1.5);

        // Assert
        learner.GetParameter("newParam").Value.Should().Be(1.5);
    }

    [Fact]
    public void GradientBasedLearner_ResetState_ShouldClearUpdatesButPreserveParams()
    {
        // Arrange
        var config = GradientLearnerConfig.Default with { BatchAccumulationSize = 10 };
        var initialParams = new Dictionary<string, double> { { "weight", 0.5 } };
        var learner = new GradientBasedLearner(config, initialParams);
        learner.ProcessFeedback(Feedback.Explicit("source", "input", "output", 0.8));

        // Act
        learner.ResetState();

        // Assert
        learner.GetPendingUpdates().Should().BeEmpty();
        learner.GetParameter("weight").Value.Should().Be(0.5);
        learner.Metrics.ProcessedCount.Should().Be(0);
    }

    #endregion

    #region AgentPerformance Tests

    [Fact]
    public void AgentPerformance_Initial_ShouldCreateValidState()
    {
        // Arrange
        var agentId = Guid.NewGuid();

        // Act
        var performance = AgentPerformance.Initial(agentId);

        // Assert
        performance.AgentId.Should().Be(agentId);
        performance.TotalInteractions.Should().Be(0);
        performance.SuccessRate.Should().Be(0.0);
        performance.AverageResponseQuality.Should().Be(0.0);
        performance.LearningCurve.Should().BeEmpty();
    }

    [Fact]
    public void AgentPerformance_CalculateTrend_WithFewPoints_ShouldReturnZero()
    {
        // Arrange
        var performance = AgentPerformance.Initial(Guid.NewGuid());

        // Act
        var trend = performance.CalculateTrend();

        // Assert
        trend.Should().Be(0.0);
    }

    [Fact]
    public void AgentPerformance_CalculateTrend_WithPositiveTrend_ShouldBePositive()
    {
        // Arrange
        var performance = AgentPerformance.Initial(Guid.NewGuid());
        for (int i = 1; i <= 10; i++)
        {
            performance = performance.WithLearningCurveEntry(i * 0.1);
        }

        // Act
        var trend = performance.CalculateTrend();

        // Assert
        trend.Should().BeGreaterThan(0);
    }

    [Fact]
    public void AgentPerformance_CalculateTrend_WithNegativeTrend_ShouldBeNegative()
    {
        // Arrange
        var performance = AgentPerformance.Initial(Guid.NewGuid());
        for (int i = 10; i >= 1; i--)
        {
            performance = performance.WithLearningCurveEntry(i * 0.1);
        }

        // Act
        var trend = performance.CalculateTrend();

        // Assert
        trend.Should().BeLessThan(0);
    }

    [Fact]
    public void AgentPerformance_IsStagnating_WithLowVariance_ShouldReturnTrue()
    {
        // Arrange
        var performance = AgentPerformance.Initial(Guid.NewGuid());
        for (int i = 0; i < 15; i++)
        {
            performance = performance.WithLearningCurveEntry(0.5 + (i * 0.0001));
        }

        // Act
        var isStagnating = performance.IsStagnating(windowSize: 10, varianceThreshold: 0.001);

        // Assert
        isStagnating.Should().BeTrue();
    }

    [Fact]
    public void AgentPerformance_IsStagnating_WithHighVariance_ShouldReturnFalse()
    {
        // Arrange
        var performance = AgentPerformance.Initial(Guid.NewGuid());
        for (int i = 0; i < 15; i++)
        {
            performance = performance.WithLearningCurveEntry(i % 2 == 0 ? 0.2 : 0.8);
        }

        // Act
        var isStagnating = performance.IsStagnating(windowSize: 10);

        // Assert
        isStagnating.Should().BeFalse();
    }

    [Fact]
    public void AgentPerformance_IsStagnating_WithInsufficientData_ShouldReturnFalse()
    {
        // Arrange
        var performance = AgentPerformance.Initial(Guid.NewGuid())
            .WithLearningCurveEntry(0.5);

        // Act
        var isStagnating = performance.IsStagnating(windowSize: 10);

        // Assert
        isStagnating.Should().BeFalse();
    }

    [Fact]
    public void AgentPerformance_WithLearningCurveEntry_ShouldAddEntry()
    {
        // Arrange
        var performance = AgentPerformance.Initial(Guid.NewGuid());

        // Act
        var updated = performance.WithLearningCurveEntry(0.75);

        // Assert
        updated.LearningCurve.Should().HaveCount(1);
        updated.LearningCurve[0].Should().Be(0.75);
    }

    [Fact]
    public void AgentPerformance_WithLearningCurveEntry_ShouldTrimExcess()
    {
        // Arrange
        var performance = AgentPerformance.Initial(Guid.NewGuid());
        for (int i = 0; i < 105; i++)
        {
            performance = performance.WithLearningCurveEntry(i * 0.01, maxCurveLength: 100);
        }

        // Assert
        performance.LearningCurve.Should().HaveCount(100);
    }

    #endregion

    #region AdaptiveAgent Tests

    [Fact]
    public void ContinuouslyLearningAgent_RecordInteraction_ShouldUpdatePerformance()
    {
        // Arrange
        var agent = new ContinuouslyLearningAgent();

        // Act
        var result = agent.RecordInteraction("input", "output", 0.8);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalInteractions.Should().Be(1);
    }

    [Fact]
    public void ContinuouslyLearningAgent_RecordInteraction_WithEmptyInput_ShouldFail()
    {
        // Arrange
        var agent = new ContinuouslyLearningAgent();

        // Act
        var result = agent.RecordInteraction(string.Empty, "output", 0.8);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ContinuouslyLearningAgent_RecordInteraction_WithEmptyOutput_ShouldFail()
    {
        // Arrange
        var agent = new ContinuouslyLearningAgent();

        // Act
        var result = agent.RecordInteraction("input", string.Empty, 0.8);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ContinuouslyLearningAgent_ShouldAdapt_BeforeMinInteractions_ShouldReturnFalse()
    {
        // Arrange
        var config = AdaptiveAgentConfig.Default with { MinInteractionsBeforeAdaptation = 100 };
        var agent = new ContinuouslyLearningAgent(config: config);

        for (int i = 0; i < 10; i++)
        {
            agent.RecordInteraction($"input{i}", $"output{i}", 0.5);
        }

        // Act
        var shouldAdapt = agent.ShouldAdapt();

        // Assert
        shouldAdapt.Should().BeFalse();
    }

    [Fact]
    public void ContinuouslyLearningAgent_ShouldAdapt_WithDecline_ShouldReturnTrue()
    {
        // Arrange
        var config = AdaptiveAgentConfig.Default with { MinInteractionsBeforeAdaptation = 10 };
        var agent = new ContinuouslyLearningAgent(config: config);

        // Start with high quality, then decline
        for (int i = 0; i < 20; i++)
        {
            var quality = i < 10 ? 0.8 : 0.3; // Decline after 10 interactions
            agent.RecordInteraction($"input{i}", $"output{i}", quality);
        }

        // Act
        var shouldAdapt = agent.ShouldAdapt();

        // Assert
        shouldAdapt.Should().BeTrue();
    }

    [Fact]
    public void ContinuouslyLearningAgent_Adapt_ShouldCreateEvent()
    {
        // Arrange
        var config = AdaptiveAgentConfig.Default with { MinInteractionsBeforeAdaptation = 10 };
        var agent = new ContinuouslyLearningAgent(config: config);

        for (int i = 0; i < 15; i++)
        {
            agent.RecordInteraction($"input{i}", $"output{i}", 0.5);
        }

        // Act
        var result = agent.Adapt();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.EventType.Should().NotBe(AdaptationEventType.Rollback);
    }

    [Fact]
    public void ContinuouslyLearningAgent_Adapt_BeforeMinInteractions_ShouldFail()
    {
        // Arrange
        var config = AdaptiveAgentConfig.Default with { MinInteractionsBeforeAdaptation = 100 };
        var agent = new ContinuouslyLearningAgent(config: config);

        // Act
        var result = agent.Adapt();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ContinuouslyLearningAgent_GetPerformance_ShouldReturnCurrentState()
    {
        // Arrange
        var agent = new ContinuouslyLearningAgent();
        agent.RecordInteraction("input", "output", 0.8);

        // Act
        var performance = agent.GetPerformance();

        // Assert
        performance.TotalInteractions.Should().Be(1);
    }

    [Fact]
    public void ContinuouslyLearningAgent_GetAdaptationHistory_ShouldTrackChanges()
    {
        // Arrange
        var config = AdaptiveAgentConfig.Default with { MinInteractionsBeforeAdaptation = 5 };
        var agent = new ContinuouslyLearningAgent(config: config);

        for (int i = 0; i < 10; i++)
        {
            agent.RecordInteraction($"input{i}", $"output{i}", 0.5);
        }

        agent.Adapt();

        // Act
        var history = agent.GetAdaptationHistory();

        // Assert
        history.Should().HaveCount(1);
    }

    [Fact]
    public void ContinuouslyLearningAgent_Rollback_ShouldRestorePreviousState()
    {
        // Arrange
        var config = AdaptiveAgentConfig.Default with { MinInteractionsBeforeAdaptation = 5 };
        var agent = new ContinuouslyLearningAgent(config: config);

        for (int i = 0; i < 10; i++)
        {
            agent.RecordInteraction($"input{i}", $"output{i}", 0.5);
        }

        var adaptResult = agent.Adapt();
        var adaptationId = adaptResult.Value.Id;

        // Act
        var rollbackResult = agent.Rollback(adaptationId);

        // Assert
        rollbackResult.IsSuccess.Should().BeTrue();
        rollbackResult.Value.EventType.Should().Be(AdaptationEventType.Rollback);
    }

    [Fact]
    public void ContinuouslyLearningAgent_Rollback_WithInvalidId_ShouldFail()
    {
        // Arrange
        var config = AdaptiveAgentConfig.Default with { MinInteractionsBeforeAdaptation = 5 };
        var agent = new ContinuouslyLearningAgent(config: config);

        for (int i = 0; i < 10; i++)
        {
            agent.RecordInteraction($"input{i}", $"output{i}", 0.5);
        }

        agent.Adapt();

        // Act
        var result = agent.Rollback(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void ContinuouslyLearningAgent_GetCurrentStrategy_ShouldReturnStrategy()
    {
        // Arrange
        var agent = new ContinuouslyLearningAgent();

        // Act
        var strategy = agent.GetCurrentStrategy();

        // Assert
        strategy.Should().NotBeNull();
        strategy.Validate().IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void ContinuouslyLearningAgent_GetExperienceCount_ShouldTrackExperiences()
    {
        // Arrange
        var agent = new ContinuouslyLearningAgent();

        for (int i = 0; i < 5; i++)
        {
            agent.RecordInteraction($"input{i}", $"output{i}", 0.5);
        }

        // Act
        var count = agent.GetExperienceCount();

        // Assert
        count.Should().Be(5);
    }

    [Fact]
    public void ContinuouslyLearningAgent_AdaptationHistory_ShouldLimitSize()
    {
        // Arrange
        var config = new AdaptiveAgentConfig(
            AdaptationThreshold: 0.01,
            MinInteractionsBeforeAdaptation: 1,
            MaxAdaptationHistory: 5);
        var agent = new ContinuouslyLearningAgent(config: config);

        // Act - Perform many adaptations
        for (int i = 0; i < 10; i++)
        {
            agent.RecordInteraction($"input{i}", $"output{i}", 0.3);
            agent.Adapt();
        }

        // Assert
        agent.GetAdaptationHistory().Count.Should().BeLessThanOrEqualTo(5);
    }

    #endregion

    #region Arrow Tests

    [Fact]
    public async Task ExperienceReplayArrows_AddExperienceArrow_ShouldAddToBuffer()
    {
        // Arrange
        var buffer = new ExperienceBuffer(capacity: 10);
        var arrow = ExperienceReplayArrows.AddExperienceArrow(buffer);
        var experience = Experience.Create("state", "action", 0.5, "next");

        // Act
        var result = await arrow(experience);

        // Assert
        result.IsSuccess.Should().BeTrue();
        buffer.Count.Should().Be(1);
    }

    [Fact]
    public async Task ExperienceReplayArrows_SampleExperiencesArrow_ShouldReturnSamples()
    {
        // Arrange
        var buffer = new ExperienceBuffer(capacity: 100, seed: 42);
        for (int i = 0; i < 10; i++)
        {
            buffer.Add(Experience.Create($"state{i}", $"action{i}", 0.1 * i, $"next{i}"));
        }

        var arrow = ExperienceReplayArrows.SampleExperiencesArrow(buffer);

        // Act
        var result = await arrow(5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(5);
    }

    [Fact]
    public async Task ExperienceReplayArrows_RecordExperienceArrow_ShouldCreateAndStore()
    {
        // Arrange
        var buffer = new ExperienceBuffer(capacity: 10);
        var arrow = ExperienceReplayArrows.RecordExperienceArrow(buffer);

        // Act
        var result = await arrow(("state", "action", 0.5, "next"));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.State.Should().Be("state");
        buffer.Count.Should().Be(1);
    }

    [Fact]
    public async Task MetaLearningArrow_EvaluateArrow_ShouldReturnScore()
    {
        // Arrange
        var metaLearner = new AdaptiveMetaLearner(seed: 42);
        var arrow = MetaLearningArrow.EvaluateArrow(metaLearner);
        var strategy = LearningStrategy.Default;
        var metrics = LearningMetrics.FromRewards(new[] { 0.5, 0.6, 0.7 });

        // Act
        var score = await arrow((strategy, metrics));

        // Assert
        score.Should().NotBe(double.NaN);
    }

    [Fact]
    public async Task MetaLearningArrow_AdaptArrow_ShouldReturnAdaptedStrategy()
    {
        // Arrange
        var metaLearner = new AdaptiveMetaLearner(seed: 42);
        var arrow = MetaLearningArrow.AdaptArrow(metaLearner);
        var strategy = LearningStrategy.Default;
        var metrics = LearningMetrics.FromRewards(Enumerable.Repeat(0.5, 20));

        // Act
        var result = await arrow((strategy, metrics));

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task OnlineLearningArrow_ProcessFeedbackStep_ShouldProcessFeedback()
    {
        // Arrange
        var learner = new GradientBasedLearner();
        var arrow = OnlineLearningArrow.ProcessFeedbackStep(learner);
        var feedback = Feedback.Explicit("source", "input", "output", 0.8);

        // Act
        var result = await arrow(feedback);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task AdaptiveAgentArrow_RecordInteractionStep_ShouldRecordInteraction()
    {
        // Arrange
        var agent = new ContinuouslyLearningAgent();
        var arrow = AdaptiveAgentArrow.RecordInteractionStep(agent);

        // Act
        var result = await arrow(("input", "output", 0.8));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalInteractions.Should().Be(1);
    }

    [Fact]
    public async Task AdaptiveAgentArrow_TryAdaptStep_WhenShouldNotAdapt_ShouldReturnNone()
    {
        // Arrange
        var config = AdaptiveAgentConfig.Default with { MinInteractionsBeforeAdaptation = 100 };
        var agent = new ContinuouslyLearningAgent(config: config);
        var arrow = AdaptiveAgentArrow.TryAdaptStep(agent);

        // Act
        var result = await arrow(Unit.Value);

        // Assert
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public async Task AdaptiveAgentArrow_FullLearningPipeline_ShouldComposeCorrectly()
    {
        // Arrange
        var agent = new ContinuouslyLearningAgent();
        var arrow = AdaptiveAgentArrow.FullLearningPipeline(agent);

        // Act
        var result = await arrow(("input", "output", 0.8));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Performance.TotalInteractions.Should().Be(1);
    }

    [Fact]
    public async Task AdaptiveAgentArrow_ProcessBatchStep_ShouldProcessMultiple()
    {
        // Arrange
        var agent = new ContinuouslyLearningAgent();
        var arrow = AdaptiveAgentArrow.ProcessBatchStep(agent);
        var interactions = new[]
        {
            ("input1", "output1", 0.8),
            ("input2", "output2", 0.7),
            ("input3", "output3", 0.9),
        };

        // Act
        var result = await arrow(interactions);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.TotalInteractions.Should().Be(3);
    }

    [Fact]
    public async Task OnlineLearningArrow_FullLearningPipeline_ShouldProcessAndApply()
    {
        // Arrange
        var learner = new GradientBasedLearner(GradientLearnerConfig.Default with { BatchAccumulationSize = 1 });
        var arrow = OnlineLearningArrow.FullLearningPipeline(learner, "source");

        // Act
        var result = await arrow(("input", "output", 0.8));

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region AdaptationEvent Tests

    [Fact]
    public void AdaptationEvent_Create_ShouldSetProperties()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var performance = AgentPerformance.Initial(agentId);

        // Act
        var evt = AdaptationEvent.Create(
            agentId,
            AdaptationEventType.StrategyChange,
            "Test adaptation",
            performance);

        // Assert
        evt.AgentId.Should().Be(agentId);
        evt.EventType.Should().Be(AdaptationEventType.StrategyChange);
        evt.Description.Should().Be("Test adaptation");
        evt.BeforeMetrics.Should().Be(performance);
        evt.AfterMetrics.Should().BeNull();
    }

    [Fact]
    public void AdaptationEvent_WithAfterMetrics_ShouldSetAfterMetrics()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var beforePerformance = AgentPerformance.Initial(agentId);
        var afterPerformance = beforePerformance with { AverageResponseQuality = 0.8 };
        var evt = AdaptationEvent.Create(
            agentId,
            AdaptationEventType.ParameterTune,
            "Test",
            beforePerformance);

        // Act
        var updated = evt.WithAfterMetrics(afterPerformance);

        // Assert
        updated.AfterMetrics.Should().Be(afterPerformance);
        evt.AfterMetrics.Should().BeNull(); // Original unchanged
    }

    [Fact]
    public void AdaptationEvent_PerformanceDelta_WithAfterMetrics_ShouldCalculate()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var beforePerformance = AgentPerformance.Initial(agentId) with { AverageResponseQuality = 0.5 };
        var afterPerformance = beforePerformance with { AverageResponseQuality = 0.8 };
        var evt = AdaptationEvent.Create(agentId, AdaptationEventType.ModelUpdate, "Test", beforePerformance)
            .WithAfterMetrics(afterPerformance);

        // Act
        var delta = evt.PerformanceDelta;

        // Assert
        delta.Should().BeApproximately(0.3, 0.001);
    }

    [Fact]
    public void AdaptationEvent_PerformanceDelta_WithoutAfterMetrics_ShouldBeNull()
    {
        // Arrange
        var evt = AdaptationEvent.Create(
            Guid.NewGuid(),
            AdaptationEventType.SkillAcquisition,
            "Test",
            AgentPerformance.Initial(Guid.NewGuid()));

        // Act
        var delta = evt.PerformanceDelta;

        // Assert
        delta.Should().BeNull();
    }

    [Fact]
    public void AdaptationEvent_WasBeneficial_WithImprovement_ShouldBeTrue()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var beforePerformance = AgentPerformance.Initial(agentId) with { AverageResponseQuality = 0.5 };
        var afterPerformance = beforePerformance with { AverageResponseQuality = 0.8 };
        var evt = AdaptationEvent.Create(agentId, AdaptationEventType.ModelUpdate, "Test", beforePerformance)
            .WithAfterMetrics(afterPerformance);

        // Act
        var wasBeneficial = evt.WasBeneficial;

        // Assert
        wasBeneficial.Should().BeTrue();
    }

    [Fact]
    public void AdaptationEvent_WasBeneficial_WithDecline_ShouldBeFalse()
    {
        // Arrange
        var agentId = Guid.NewGuid();
        var beforePerformance = AgentPerformance.Initial(agentId) with { AverageResponseQuality = 0.8 };
        var afterPerformance = beforePerformance with { AverageResponseQuality = 0.5 };
        var evt = AdaptationEvent.Create(agentId, AdaptationEventType.ModelUpdate, "Test", beforePerformance)
            .WithAfterMetrics(afterPerformance);

        // Act
        var wasBeneficial = evt.WasBeneficial;

        // Assert
        wasBeneficial.Should().BeFalse();
    }

    #endregion

    #region GradientLearnerConfig Tests

    [Fact]
    public void GradientLearnerConfig_Default_ShouldHaveValidValues()
    {
        // Act
        var config = GradientLearnerConfig.Default;

        // Assert
        config.LearningRate.Should().Be(0.01);
        config.Momentum.Should().Be(0.9);
        config.AdaptiveLearningRate.Should().BeTrue();
    }

    [Fact]
    public void GradientLearnerConfig_Conservative_ShouldHaveLowerLearningRate()
    {
        // Act
        var config = GradientLearnerConfig.Conservative;

        // Assert
        config.LearningRate.Should().BeLessThan(GradientLearnerConfig.Default.LearningRate);
    }

    [Fact]
    public void GradientLearnerConfig_Validate_WithValidConfig_ShouldSucceed()
    {
        // Arrange
        var config = GradientLearnerConfig.Default;

        // Act
        var result = config.Validate();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void GradientLearnerConfig_Validate_WithInvalidLearningRate_ShouldFail()
    {
        // Arrange
        var config = GradientLearnerConfig.Default with { LearningRate = 0 };

        // Act
        var result = config.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region AdaptiveAgentConfig Tests

    [Fact]
    public void AdaptiveAgentConfig_Default_ShouldHaveValidValues()
    {
        // Act
        var config = AdaptiveAgentConfig.Default;

        // Assert
        config.AdaptationThreshold.Should().Be(0.1);
        config.MinInteractionsBeforeAdaptation.Should().Be(50);
    }

    [Fact]
    public void AdaptiveAgentConfig_Aggressive_ShouldHaveLowerThresholds()
    {
        // Act
        var config = AdaptiveAgentConfig.Aggressive;

        // Assert
        config.AdaptationThreshold.Should().BeLessThan(AdaptiveAgentConfig.Default.AdaptationThreshold);
        config.MinInteractionsBeforeAdaptation.Should().BeLessThan(AdaptiveAgentConfig.Default.MinInteractionsBeforeAdaptation);
    }

    [Fact]
    public void AdaptiveAgentConfig_Conservative_ShouldHaveHigherThresholds()
    {
        // Act
        var config = AdaptiveAgentConfig.Conservative;

        // Assert
        config.AdaptationThreshold.Should().BeGreaterThan(AdaptiveAgentConfig.Default.AdaptationThreshold);
        config.MinInteractionsBeforeAdaptation.Should().BeGreaterThan(AdaptiveAgentConfig.Default.MinInteractionsBeforeAdaptation);
    }

    #endregion

    #region OnlineLearningMetrics Tests

    [Fact]
    public void OnlineLearningMetrics_Empty_ShouldHaveZeroValues()
    {
        // Act
        var metrics = OnlineLearningMetrics.Empty;

        // Assert
        metrics.ProcessedCount.Should().Be(0);
        metrics.AverageScore.Should().Be(0.0);
        metrics.UpdateCount.Should().Be(0);
    }

    [Fact]
    public void OnlineLearningMetrics_WithNewScore_ShouldUpdateAverage()
    {
        // Arrange
        var metrics = OnlineLearningMetrics.Empty;

        // Act
        var updated = metrics
            .WithNewScore(0.8)
            .WithNewScore(0.6);

        // Assert
        updated.ProcessedCount.Should().Be(2);
        updated.AverageScore.Should().BeApproximately(0.7, 0.01);
    }

    [Fact]
    public void OnlineLearningMetrics_ComputePerformanceScore_ShouldNormalize()
    {
        // Arrange
        var metrics = OnlineLearningMetrics.Empty
            .WithNewScore(0.8)
            .WithNewScore(0.9);

        // Act
        var score = metrics.ComputePerformanceScore();

        // Assert
        score.Should().BeInRange(0.0, 1.0);
    }

    #endregion
}
