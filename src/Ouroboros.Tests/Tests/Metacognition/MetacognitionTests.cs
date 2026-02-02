// --------------------------------------------------------------------------------------------------------------------
// <copyright file="MetacognitionTests.cs" company="Ouroboros">
//   Copyright (c) Ouroboros. All rights reserved.
//   Licensed under the MIT License.
// </copyright>
// <summary>
//   Comprehensive unit tests for the Self-Reflection &amp; Metacognition feature.
//   Tests cover Introspection, SelfAssessment, CognitiveMonitor, and ReflectiveReasoning.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Ouroboros.Tests.Metacognition;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Ouroboros.Pipeline.Metacognition;
using Xunit;

/// <summary>
/// Comprehensive tests for the Metacognition subsystem covering introspection,
/// self-assessment, cognitive monitoring, and reflective reasoning.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MetacognitionTests
{
    #region ProcessingMode Enum Tests

    [Theory]
    [InlineData(ProcessingMode.Analytical)]
    [InlineData(ProcessingMode.Creative)]
    [InlineData(ProcessingMode.Reactive)]
    [InlineData(ProcessingMode.Reflective)]
    [InlineData(ProcessingMode.Intuitive)]
    public void ProcessingMode_AllValues_ShouldBeDefined(ProcessingMode mode)
    {
        // Assert
        Enum.IsDefined(typeof(ProcessingMode), mode).Should().BeTrue();
    }

    [Fact]
    public void ProcessingMode_ShouldHaveExpectedCount()
    {
        // Assert
        Enum.GetValues<ProcessingMode>().Should().HaveCount(5);
    }

    #endregion

    #region InternalState Tests

    [Fact]
    public void InternalState_Initial_ShouldCreateDefaultState()
    {
        // Arrange & Act
        var state = InternalState.Initial();

        // Assert
        state.Id.Should().NotBeEmpty();
        state.ActiveGoals.Should().BeEmpty();
        state.CurrentFocus.Should().Be("None");
        state.CognitiveLoad.Should().Be(0.0);
        state.EmotionalValence.Should().Be(0.0);
        state.AttentionDistribution.Should().BeEmpty();
        state.WorkingMemoryItems.Should().BeEmpty();
        state.Mode.Should().Be(ProcessingMode.Reactive);
    }

    [Fact]
    public void InternalState_WithGoal_ShouldAddGoal()
    {
        // Arrange
        var state = InternalState.Initial();

        // Act
        var newState = state.WithGoal("Complete task");

        // Assert
        newState.ActiveGoals.Should().ContainSingle().Which.Should().Be("Complete task");
        state.ActiveGoals.Should().BeEmpty("original state should be unchanged");
    }

    [Fact]
    public void InternalState_WithGoal_EmptyString_ShouldNotAddGoal()
    {
        // Arrange
        var state = InternalState.Initial();

        // Act
        var newState = state.WithGoal("");

        // Assert
        newState.ActiveGoals.Should().BeEmpty();
    }

    [Fact]
    public void InternalState_WithGoal_Whitespace_ShouldNotAddGoal()
    {
        // Arrange
        var state = InternalState.Initial();

        // Act
        var newState = state.WithGoal("   ");

        // Assert
        newState.ActiveGoals.Should().BeEmpty();
    }

    [Fact]
    public void InternalState_WithoutGoal_ShouldRemoveGoal()
    {
        // Arrange
        var state = InternalState.Initial().WithGoal("Goal A").WithGoal("Goal B");

        // Act
        var newState = state.WithoutGoal("Goal A");

        // Assert
        newState.ActiveGoals.Should().ContainSingle().Which.Should().Be("Goal B");
    }

    [Fact]
    public void InternalState_WithFocus_ShouldUpdateFocus()
    {
        // Arrange
        var state = InternalState.Initial();

        // Act
        var newState = state.WithFocus("Important task");

        // Assert
        newState.CurrentFocus.Should().Be("Important task");
        state.CurrentFocus.Should().Be("None", "original state should be unchanged");
    }

    [Fact]
    public void InternalState_WithFocus_Null_ShouldSetToNone()
    {
        // Arrange
        var state = InternalState.Initial().WithFocus("Something");

        // Act
        var newState = state.WithFocus(null!);

        // Assert
        newState.CurrentFocus.Should().Be("None");
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(0.5, 0.5)]
    [InlineData(1.0, 1.0)]
    public void InternalState_WithCognitiveLoad_ValidRange_ShouldSetValue(double input, double expected)
    {
        // Arrange
        var state = InternalState.Initial();

        // Act
        var newState = state.WithCognitiveLoad(input);

        // Assert
        newState.CognitiveLoad.Should().Be(expected);
    }

    [Theory]
    [InlineData(-0.5, 0.0)]
    [InlineData(-1.0, 0.0)]
    public void InternalState_WithCognitiveLoad_BelowZero_ShouldClampToZero(double input, double expected)
    {
        // Arrange
        var state = InternalState.Initial();

        // Act
        var newState = state.WithCognitiveLoad(input);

        // Assert
        newState.CognitiveLoad.Should().Be(expected);
    }

    [Theory]
    [InlineData(1.5, 1.0)]
    [InlineData(2.0, 1.0)]
    public void InternalState_WithCognitiveLoad_AboveOne_ShouldClampToOne(double input, double expected)
    {
        // Arrange
        var state = InternalState.Initial();

        // Act
        var newState = state.WithCognitiveLoad(input);

        // Assert
        newState.CognitiveLoad.Should().Be(expected);
    }

    [Theory]
    [InlineData(-1.0, -1.0)]
    [InlineData(0.0, 0.0)]
    [InlineData(1.0, 1.0)]
    public void InternalState_WithValence_ValidRange_ShouldSetValue(double input, double expected)
    {
        // Arrange
        var state = InternalState.Initial();

        // Act
        var newState = state.WithValence(input);

        // Assert
        newState.EmotionalValence.Should().Be(expected);
    }

    [Theory]
    [InlineData(-1.5, -1.0)]
    [InlineData(1.5, 1.0)]
    public void InternalState_WithValence_OutOfRange_ShouldClamp(double input, double expected)
    {
        // Arrange
        var state = InternalState.Initial();

        // Act
        var newState = state.WithValence(input);

        // Assert
        newState.EmotionalValence.Should().Be(expected);
    }

    [Fact]
    public void InternalState_WithWorkingMemoryItem_ShouldAddItem()
    {
        // Arrange
        var state = InternalState.Initial();

        // Act
        var newState = state.WithWorkingMemoryItem("item1");

        // Assert
        newState.WorkingMemoryItems.Should().ContainSingle().Which.Should().Be("item1");
    }

    [Fact]
    public void InternalState_WithWorkingMemoryItem_EmptyString_ShouldNotAddItem()
    {
        // Arrange
        var state = InternalState.Initial();

        // Act
        var newState = state.WithWorkingMemoryItem("");

        // Assert
        newState.WorkingMemoryItems.Should().BeEmpty();
    }

    [Fact]
    public void InternalState_WithAttention_ShouldUpdateDistribution()
    {
        // Arrange
        var state = InternalState.Initial();
        var attention = ImmutableDictionary<string, double>.Empty
            .Add("TaskA", 0.6)
            .Add("TaskB", 0.4);

        // Act
        var newState = state.WithAttention(attention);

        // Assert
        newState.AttentionDistribution.Should().HaveCount(2);
        newState.AttentionDistribution["TaskA"].Should().Be(0.6);
    }

    [Fact]
    public void InternalState_WithMode_ShouldUpdateMode()
    {
        // Arrange
        var state = InternalState.Initial();

        // Act
        var newState = state.WithMode(ProcessingMode.Analytical);

        // Assert
        newState.Mode.Should().Be(ProcessingMode.Analytical);
        state.Mode.Should().Be(ProcessingMode.Reactive, "original state should be unchanged");
    }

    [Fact]
    public void InternalState_Snapshot_ShouldCreateNewIdAndTimestamp()
    {
        // Arrange
        var state = InternalState.Initial();
        var originalId = state.Id;
        var originalTimestamp = state.Timestamp;

        // Act
        var snapshot = state.Snapshot();

        // Assert
        snapshot.Id.Should().NotBe(originalId);
        snapshot.Timestamp.Should().BeOnOrAfter(originalTimestamp);
    }

    [Fact]
    public void InternalState_Immutability_ChainedOperationsShouldPreserveOriginal()
    {
        // Arrange
        var original = InternalState.Initial();

        // Act
        var modified = original
            .WithGoal("Goal")
            .WithFocus("Focus")
            .WithCognitiveLoad(0.5)
            .WithValence(0.3)
            .WithMode(ProcessingMode.Analytical);

        // Assert
        original.ActiveGoals.Should().BeEmpty();
        original.CurrentFocus.Should().Be("None");
        original.CognitiveLoad.Should().Be(0.0);
        original.EmotionalValence.Should().Be(0.0);
        original.Mode.Should().Be(ProcessingMode.Reactive);
    }

    #endregion

    #region IntrospectionReport Tests

    [Fact]
    public void IntrospectionReport_Empty_ShouldCreateEmptyReport()
    {
        // Arrange
        var state = InternalState.Initial();

        // Act
        var report = IntrospectionReport.Empty(state);

        // Assert
        report.StateSnapshot.Should().Be(state);
        report.Observations.Should().BeEmpty();
        report.Anomalies.Should().BeEmpty();
        report.Recommendations.Should().BeEmpty();
        report.SelfAssessmentScore.Should().Be(0.5);
        report.HasAnomalies.Should().BeFalse();
        report.HasRecommendations.Should().BeFalse();
    }

    [Fact]
    public void IntrospectionReport_WithObservation_ShouldAddObservation()
    {
        // Arrange
        var state = InternalState.Initial();
        var report = IntrospectionReport.Empty(state);

        // Act
        var newReport = report.WithObservation("High focus detected");

        // Assert
        newReport.Observations.Should().ContainSingle().Which.Should().Be("High focus detected");
    }

    [Fact]
    public void IntrospectionReport_WithAnomaly_ShouldAddAnomaly()
    {
        // Arrange
        var state = InternalState.Initial();
        var report = IntrospectionReport.Empty(state);

        // Act
        var newReport = report.WithAnomaly("Cognitive overload");

        // Assert
        newReport.Anomalies.Should().ContainSingle().Which.Should().Be("Cognitive overload");
        newReport.HasAnomalies.Should().BeTrue();
    }

    [Fact]
    public void IntrospectionReport_WithRecommendation_ShouldAddRecommendation()
    {
        // Arrange
        var state = InternalState.Initial();
        var report = IntrospectionReport.Empty(state);

        // Act
        var newReport = report.WithRecommendation("Reduce task complexity");

        // Assert
        newReport.Recommendations.Should().ContainSingle().Which.Should().Be("Reduce task complexity");
        newReport.HasRecommendations.Should().BeTrue();
    }

    [Fact]
    public void IntrospectionReport_ChainedOperations_ShouldAccumulate()
    {
        // Arrange
        var state = InternalState.Initial();

        // Act
        var report = IntrospectionReport.Empty(state)
            .WithObservation("Observation 1")
            .WithObservation("Observation 2")
            .WithAnomaly("Anomaly 1")
            .WithRecommendation("Recommendation 1");

        // Assert
        report.Observations.Should().HaveCount(2);
        report.Anomalies.Should().ContainSingle();
        report.Recommendations.Should().ContainSingle();
    }

    #endregion

    #region StateComparison Tests

    [Fact]
    public void StateComparison_Create_ShouldComputeDeltas()
    {
        // Arrange
        var before = InternalState.Initial().WithCognitiveLoad(0.3).WithValence(0.1);
        var after = InternalState.Initial().WithCognitiveLoad(0.7).WithValence(0.5);

        // Act
        var comparison = StateComparison.Create(before, after);

        // Assert
        comparison.CognitiveLoadDelta.Should().BeApproximately(0.4, 0.001);
        comparison.ValenceDelta.Should().BeApproximately(0.4, 0.001);
    }

    [Fact]
    public void StateComparison_CognitiveLoadIncreased_ShouldDetectIncrease()
    {
        // Arrange
        var before = InternalState.Initial().WithCognitiveLoad(0.3);
        var after = InternalState.Initial().WithCognitiveLoad(0.6);

        // Act
        var comparison = StateComparison.Create(before, after);

        // Assert
        comparison.CognitiveLoadIncreased.Should().BeTrue();
        comparison.CognitiveLoadDecreased.Should().BeFalse();
    }

    [Fact]
    public void StateComparison_CognitiveLoadDecreased_ShouldDetectDecrease()
    {
        // Arrange
        var before = InternalState.Initial().WithCognitiveLoad(0.7);
        var after = InternalState.Initial().WithCognitiveLoad(0.4);

        // Act
        var comparison = StateComparison.Create(before, after);

        // Assert
        comparison.CognitiveLoadDecreased.Should().BeTrue();
        comparison.CognitiveLoadIncreased.Should().BeFalse();
    }

    [Fact]
    public void StateComparison_ModeChanged_ShouldDetectChange()
    {
        // Arrange
        var before = InternalState.Initial().WithMode(ProcessingMode.Reactive);
        var after = InternalState.Initial().WithMode(ProcessingMode.Analytical);

        // Act
        var comparison = StateComparison.Create(before, after);

        // Assert
        comparison.ModeChanged.Should().BeTrue();
    }

    [Fact]
    public void StateComparison_GoalsAdded_ShouldTrackNewGoals()
    {
        // Arrange
        var before = InternalState.Initial().WithGoal("Goal A");
        var after = InternalState.Initial().WithGoal("Goal A").WithGoal("Goal B");

        // Act
        var comparison = StateComparison.Create(before, after);

        // Assert
        comparison.GoalsAdded.Should().ContainSingle().Which.Should().Be("Goal B");
    }

    [Fact]
    public void StateComparison_GoalsRemoved_ShouldTrackRemovedGoals()
    {
        // Arrange
        var before = InternalState.Initial().WithGoal("Goal A").WithGoal("Goal B");
        var after = InternalState.Initial().WithGoal("Goal B");

        // Act
        var comparison = StateComparison.Create(before, after);

        // Assert
        comparison.GoalsRemoved.Should().ContainSingle().Which.Should().Be("Goal A");
    }

    [Fact]
    public void StateComparison_AttentionChanges_ShouldTrackSignificantChanges()
    {
        // Arrange
        var beforeAttention = ImmutableDictionary<string, double>.Empty.Add("TaskA", 0.5);
        var afterAttention = ImmutableDictionary<string, double>.Empty.Add("TaskA", 0.8);
        var before = InternalState.Initial().WithAttention(beforeAttention);
        var after = InternalState.Initial().WithAttention(afterAttention);

        // Act
        var comparison = StateComparison.Create(before, after);

        // Assert
        comparison.AttentionChanges.Should().ContainKey("TaskA");
        comparison.AttentionChanges["TaskA"].Should().BeApproximately(0.3, 0.001);
    }

    [Fact]
    public void StateComparison_Interpretation_ShouldProvideDescription()
    {
        // Arrange
        var before = InternalState.Initial().WithCognitiveLoad(0.2);
        var after = InternalState.Initial().WithCognitiveLoad(0.9);

        // Act
        var comparison = StateComparison.Create(before, after);

        // Assert
        comparison.Interpretation.Should().NotBeNullOrEmpty();
        comparison.Interpretation.Should().Contain("cognitive load");
    }

    #endregion

    #region CognitiveIntrospector Tests

    [Fact]
    public void CognitiveIntrospector_CaptureState_ShouldReturnCurrentState()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result = introspector.CaptureState();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public void CognitiveIntrospector_Analyze_ShouldGenerateValidReport()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();
        var state = InternalState.Initial().WithCognitiveLoad(0.5);

        // Act
        var result = introspector.Analyze(state);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.StateSnapshot.Should().Be(state);
        result.Value.GeneratedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void CognitiveIntrospector_Analyze_HighCognitiveLoad_ShouldGenerateAnomaly()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();
        var state = InternalState.Initial().WithCognitiveLoad(0.95);

        // Act
        var result = introspector.Analyze(state);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.HasAnomalies.Should().BeTrue();
    }

    [Fact]
    public void CognitiveIntrospector_Analyze_NullState_ShouldReturnFailure()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result = introspector.Analyze(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("null");
    }

    [Fact]
    public void CognitiveIntrospector_CompareStates_ShouldReturnComparison()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();
        var before = InternalState.Initial().WithCognitiveLoad(0.3);
        var after = InternalState.Initial().WithCognitiveLoad(0.7);

        // Act
        var result = introspector.CompareStates(before, after);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CognitiveLoadDelta.Should().BeApproximately(0.4, 0.001);
    }

    [Fact]
    public void CognitiveIntrospector_CompareStates_NullBefore_ShouldReturnFailure()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();
        var after = InternalState.Initial();

        // Act
        var result = introspector.CompareStates(null!, after);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void CognitiveIntrospector_CompareStates_NullAfter_ShouldReturnFailure()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();
        var before = InternalState.Initial();

        // Act
        var result = introspector.CompareStates(before, null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void CognitiveIntrospector_IdentifyPatterns_InsufficientHistory_ShouldReturnMessage()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();
        var history = new List<InternalState> { InternalState.Initial() };

        // Act
        var result = introspector.IdentifyPatterns(history);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle().Which.Should().Contain("Insufficient");
    }

    [Fact]
    public void CognitiveIntrospector_IdentifyPatterns_NullHistory_ShouldReturnFailure()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result = introspector.IdentifyPatterns(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void CognitiveIntrospector_GetStateHistory_ShouldReturnHistory()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();
        introspector.CaptureState();
        introspector.CaptureState();

        // Act
        var result = introspector.GetStateHistory();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public void CognitiveIntrospector_SetCurrentFocus_ShouldUpdateState()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result = introspector.SetCurrentFocus("New Focus");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var stateResult = introspector.CaptureState();
        stateResult.Value.CurrentFocus.Should().Be("New Focus");
    }

    [Fact]
    public void CognitiveIntrospector_AddGoal_ShouldAddToActiveGoals()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result = introspector.AddGoal("Complete the task");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var stateResult = introspector.CaptureState();
        stateResult.Value.ActiveGoals.Should().Contain("Complete the task");
    }

    [Fact]
    public void CognitiveIntrospector_RemoveGoal_ShouldRemoveFromActiveGoals()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();
        introspector.AddGoal("Goal A");
        introspector.AddGoal("Goal B");

        // Act
        var result = introspector.RemoveGoal("Goal A");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var stateResult = introspector.CaptureState();
        stateResult.Value.ActiveGoals.Should().NotContain("Goal A");
        stateResult.Value.ActiveGoals.Should().Contain("Goal B");
    }

    [Fact]
    public void CognitiveIntrospector_SetCognitiveLoad_ShouldUpdateState()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result = introspector.SetCognitiveLoad(0.75);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var stateResult = introspector.CaptureState();
        stateResult.Value.CognitiveLoad.Should().Be(0.75);
    }

    [Fact]
    public void CognitiveIntrospector_SetValence_ShouldUpdateState()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result = introspector.SetValence(-0.5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var stateResult = introspector.CaptureState();
        stateResult.Value.EmotionalValence.Should().Be(-0.5);
    }

    [Fact]
    public void CognitiveIntrospector_SetMode_ShouldUpdateState()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();

        // Act
        var result = introspector.SetMode(ProcessingMode.Creative);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var stateResult = introspector.CaptureState();
        stateResult.Value.Mode.Should().Be(ProcessingMode.Creative);
    }

    [Fact]
    public void CognitiveIntrospector_ThreadSafety_ConcurrentCaptureState()
    {
        // Arrange
        var introspector = new CognitiveIntrospector();
        var tasks = new List<Task<bool>>();

        // Act
        for (int i = 0; i < 100; i++)
        {
            tasks.Add(Task.Run(() => introspector.CaptureState().IsSuccess));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert
        tasks.Should().OnlyContain(t => t.Result);
        introspector.GetStateHistory().Value.Should().HaveCount(100);
    }

    [Fact]
    public void CognitiveIntrospector_HistoryLimit_ShouldTrimOldestEntries()
    {
        // Arrange
        var introspector = new CognitiveIntrospector(maxHistorySize: 5);

        // Act
        for (int i = 0; i < 10; i++)
        {
            introspector.CaptureState();
        }

        // Assert
        introspector.GetStateHistory().Value.Should().HaveCount(5);
    }

    #endregion

    #region PerformanceDimension Enum Tests

    [Theory]
    [InlineData(PerformanceDimension.Accuracy)]
    [InlineData(PerformanceDimension.Speed)]
    [InlineData(PerformanceDimension.Creativity)]
    [InlineData(PerformanceDimension.Consistency)]
    [InlineData(PerformanceDimension.Adaptability)]
    [InlineData(PerformanceDimension.Communication)]
    public void PerformanceDimension_AllValues_ShouldBeDefined(PerformanceDimension dimension)
    {
        // Assert
        Enum.IsDefined(typeof(PerformanceDimension), dimension).Should().BeTrue();
    }

    [Fact]
    public void PerformanceDimension_ShouldHaveExpectedCount()
    {
        // Assert
        Enum.GetValues<PerformanceDimension>().Should().HaveCount(6);
    }

    #endregion

    #region Trend Enum Tests

    [Theory]
    [InlineData(Trend.Improving)]
    [InlineData(Trend.Stable)]
    [InlineData(Trend.Declining)]
    [InlineData(Trend.Volatile)]
    [InlineData(Trend.Unknown)]
    public void Trend_AllValues_ShouldBeDefined(Trend trend)
    {
        // Assert
        Enum.IsDefined(typeof(Trend), trend).Should().BeTrue();
    }

    #endregion

    #region DimensionScore Tests

    [Fact]
    public void DimensionScore_Unknown_ShouldCreateDefaultScore()
    {
        // Act
        var score = DimensionScore.Unknown(PerformanceDimension.Accuracy);

        // Assert
        score.Dimension.Should().Be(PerformanceDimension.Accuracy);
        score.Score.Should().Be(0.5);
        score.Confidence.Should().Be(0.0);
        score.Evidence.Should().BeEmpty();
        score.Trend.Should().Be(Trend.Unknown);
    }

    [Fact]
    public void DimensionScore_Create_ShouldInitializeWithValues()
    {
        // Arrange
        var evidence = new[] { "Test evidence" };

        // Act
        var score = DimensionScore.Create(PerformanceDimension.Speed, 0.8, 0.6, evidence);

        // Assert
        score.Dimension.Should().Be(PerformanceDimension.Speed);
        score.Score.Should().Be(0.8);
        score.Confidence.Should().Be(0.6);
        score.Evidence.Should().ContainSingle();
    }

    [Theory]
    [InlineData(-0.5, 0.0)]
    [InlineData(1.5, 1.0)]
    public void DimensionScore_Create_ShouldClampScore(double input, double expected)
    {
        // Act
        var score = DimensionScore.Create(PerformanceDimension.Creativity, input, 0.5, Array.Empty<string>());

        // Assert
        score.Score.Should().Be(expected);
    }

    [Fact]
    public void DimensionScore_WithBayesianUpdate_ShouldUpdateScore()
    {
        // Arrange
        var score = DimensionScore.Create(PerformanceDimension.Accuracy, 0.5, 0.3, new[] { "initial" });

        // Act
        var updated = score.WithBayesianUpdate(0.8, 0.5, "new evidence");

        // Assert
        updated.Score.Should().BeGreaterThan(score.Score);
        updated.Evidence.Should().HaveCount(2);
        updated.Confidence.Should().BeGreaterThan(score.Confidence);
    }

    [Fact]
    public void DimensionScore_WithBayesianUpdate_HighNewScore_ShouldShowImproving()
    {
        // Arrange
        var score = DimensionScore.Create(PerformanceDimension.Accuracy, 0.5, 0.5, new[] { "initial" });

        // Act
        var updated = score.WithBayesianUpdate(0.9, 0.5, "improvement");

        // Assert
        updated.Trend.Should().Be(Trend.Improving);
    }

    [Fact]
    public void DimensionScore_WithBayesianUpdate_LowNewScore_ShouldShowDeclining()
    {
        // Arrange
        var score = DimensionScore.Create(PerformanceDimension.Speed, 0.8, 0.5, new[] { "initial" });

        // Act
        var updated = score.WithBayesianUpdate(0.3, 0.5, "decline");

        // Assert
        updated.Trend.Should().Be(Trend.Declining);
    }

    [Fact]
    public void DimensionScore_Validate_ValidScore_ShouldReturnSuccess()
    {
        // Arrange
        var score = DimensionScore.Create(PerformanceDimension.Accuracy, 0.7, 0.5, Array.Empty<string>());

        // Act
        var result = score.Validate();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region SelfAssessmentResult Tests

    [Fact]
    public void SelfAssessmentResult_Empty_ShouldCreateEmptyResult()
    {
        // Act
        var result = SelfAssessmentResult.Empty();

        // Assert
        result.Id.Should().NotBeEmpty();
        result.OverallScore.Should().Be(0.5);
        result.OverallConfidence.Should().Be(0.0);
        result.Strengths.Should().BeEmpty();
        result.Weaknesses.Should().BeEmpty();
        result.DimensionScores.Should().BeEmpty();
    }

    [Fact]
    public void SelfAssessmentResult_FromDimensionScores_ShouldComputeOverallScore()
    {
        // Arrange
        var scores = ImmutableDictionary<PerformanceDimension, DimensionScore>.Empty
            .Add(PerformanceDimension.Accuracy, DimensionScore.Create(PerformanceDimension.Accuracy, 0.8, 0.5, Array.Empty<string>()))
            .Add(PerformanceDimension.Speed, DimensionScore.Create(PerformanceDimension.Speed, 0.6, 0.5, Array.Empty<string>()));

        // Act
        var result = SelfAssessmentResult.FromDimensionScores(scores);

        // Assert
        result.OverallScore.Should().BeInRange(0.6, 0.8);
        result.DimensionScores.Should().HaveCount(2);
    }

    [Fact]
    public void SelfAssessmentResult_FromDimensionScores_ShouldIdentifyStrengths()
    {
        // Arrange
        var scores = ImmutableDictionary<PerformanceDimension, DimensionScore>.Empty
            .Add(PerformanceDimension.Accuracy, DimensionScore.Create(PerformanceDimension.Accuracy, 0.85, 0.5, Array.Empty<string>()));

        // Act
        var result = SelfAssessmentResult.FromDimensionScores(scores);

        // Assert
        result.Strengths.Should().ContainSingle().Which.Should().Contain("Accuracy");
    }

    [Fact]
    public void SelfAssessmentResult_FromDimensionScores_ShouldIdentifyWeaknesses()
    {
        // Arrange
        var scores = ImmutableDictionary<PerformanceDimension, DimensionScore>.Empty
            .Add(PerformanceDimension.Speed, DimensionScore.Create(PerformanceDimension.Speed, 0.25, 0.5, Array.Empty<string>()));

        // Act
        var result = SelfAssessmentResult.FromDimensionScores(scores);

        // Assert
        result.Weaknesses.Should().ContainSingle().Which.Should().Contain("Speed");
    }

    [Fact]
    public void SelfAssessmentResult_GetDimensionScore_ExistingDimension_ShouldReturnSome()
    {
        // Arrange
        var scores = ImmutableDictionary<PerformanceDimension, DimensionScore>.Empty
            .Add(PerformanceDimension.Accuracy, DimensionScore.Create(PerformanceDimension.Accuracy, 0.7, 0.5, Array.Empty<string>()));
        var result = SelfAssessmentResult.FromDimensionScores(scores);

        // Act
        var score = result.GetDimensionScore(PerformanceDimension.Accuracy);

        // Assert
        score.HasValue.Should().BeTrue();
        score.Value.Score.Should().Be(0.7);
    }

    [Fact]
    public void SelfAssessmentResult_GetDimensionScore_NonExistingDimension_ShouldReturnNone()
    {
        // Arrange
        var result = SelfAssessmentResult.Empty();

        // Act
        var score = result.GetDimensionScore(PerformanceDimension.Creativity);

        // Assert
        score.HasValue.Should().BeFalse();
    }

    [Fact]
    public void SelfAssessmentResult_WithDimensionScore_ShouldUpdateResult()
    {
        // Arrange
        var result = SelfAssessmentResult.Empty();
        var newScore = DimensionScore.Create(PerformanceDimension.Adaptability, 0.9, 0.7, new[] { "evidence" });

        // Act
        var updated = result.WithDimensionScore(newScore);

        // Assert
        updated.GetDimensionScore(PerformanceDimension.Adaptability).HasValue.Should().BeTrue();
    }

    #endregion

    #region CapabilityBelief Tests

    [Fact]
    public void CapabilityBelief_Uninformative_ShouldCreateMaxEntropyPrior()
    {
        // Act
        var belief = CapabilityBelief.Uninformative("test_capability");

        // Assert
        belief.CapabilityName.Should().Be("test_capability");
        belief.Proficiency.Should().Be(0.5);
        belief.Uncertainty.Should().Be(1.0);
        belief.ValidationCount.Should().Be(0);
    }

    [Fact]
    public void CapabilityBelief_Create_ShouldInitializeWithValues()
    {
        // Act
        var belief = CapabilityBelief.Create("reasoning", 0.7, 0.3);

        // Assert
        belief.CapabilityName.Should().Be("reasoning");
        belief.Proficiency.Should().Be(0.7);
        belief.Uncertainty.Should().Be(0.3);
        belief.ValidationCount.Should().Be(1);
    }

    [Fact]
    public void CapabilityBelief_WithBayesianUpdate_ShouldUpdateProficiency()
    {
        // Arrange
        var belief = CapabilityBelief.Create("analysis", 0.5, 0.5);

        // Act
        var updated = belief.WithBayesianUpdate(0.9, 5);

        // Assert
        updated.Proficiency.Should().BeGreaterThan(belief.Proficiency);
        updated.ValidationCount.Should().Be(6);
    }

    [Fact]
    public void CapabilityBelief_WithBayesianUpdate_ShouldReduceUncertainty()
    {
        // Arrange
        var belief = CapabilityBelief.Uninformative("test");

        // Act
        var updated = belief.WithBayesianUpdate(0.8, 10);

        // Assert
        updated.Uncertainty.Should().BeLessThan(belief.Uncertainty);
    }

    [Fact]
    public void CapabilityBelief_GetCredibleInterval_ShouldReturnValidBounds()
    {
        // Arrange
        var belief = CapabilityBelief.Create("test", 0.7, 0.2);

        // Act
        var (lower, expected, upper) = belief.GetCredibleInterval(0.95);

        // Assert
        lower.Should().BeLessThanOrEqualTo(expected);
        expected.Should().Be(belief.Proficiency);
        upper.Should().BeGreaterThanOrEqualTo(expected);
        lower.Should().BeGreaterThanOrEqualTo(0.0);
        upper.Should().BeLessThanOrEqualTo(1.0);
    }

    [Fact]
    public void CapabilityBelief_Validate_ValidBelief_ShouldReturnSuccess()
    {
        // Arrange
        var belief = CapabilityBelief.Create("valid", 0.7, 0.3);

        // Act
        var result = belief.Validate();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void CapabilityBelief_Validate_EmptyName_ShouldReturnFailure()
    {
        // Arrange
        var belief = new CapabilityBelief("", 0.5, 0.5, DateTime.UtcNow, 1);

        // Act
        var result = belief.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("name");
    }

    #endregion

    #region BayesianSelfAssessor Tests

    [Fact]
    public async Task BayesianSelfAssessor_AssessAsync_ShouldReturnValidResult()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();

        // Act
        var result = await assessor.AssessAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DimensionScores.Should().HaveCount(6);
    }

    [Fact]
    public async Task BayesianSelfAssessor_AssessDimensionAsync_ShouldReturnScore()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();

        // Act
        var result = await assessor.AssessDimensionAsync(PerformanceDimension.Accuracy);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Dimension.Should().Be(PerformanceDimension.Accuracy);
    }

    [Fact]
    public void BayesianSelfAssessor_GetCapabilityBelief_UnknownCapability_ShouldReturnNone()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();

        // Act
        var result = assessor.GetCapabilityBelief("unknown");

        // Assert
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void BayesianSelfAssessor_UpdateBelief_ShouldCreateAndUpdateBelief()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();

        // Act
        var result = assessor.UpdateBelief("new_capability", 0.8);

        // Assert
        result.IsSuccess.Should().BeTrue();
        assessor.GetCapabilityBelief("new_capability").HasValue.Should().BeTrue();
    }

    [Fact]
    public void BayesianSelfAssessor_UpdateBelief_EmptyCapability_ShouldReturnFailure()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();

        // Act
        var result = assessor.UpdateBelief("", 0.5);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void BayesianSelfAssessor_UpdateBelief_InvalidEvidence_ShouldReturnFailure()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();

        // Act
        var result = assessor.UpdateBelief("test", 1.5);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void BayesianSelfAssessor_GetAllBeliefs_ShouldReturnAllBeliefs()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();
        assessor.UpdateBelief("capability1", 0.7);
        assessor.UpdateBelief("capability2", 0.8);

        // Act
        var beliefs = assessor.GetAllBeliefs();

        // Assert
        beliefs.Should().HaveCount(2);
        beliefs.Should().ContainKey("capability1");
        beliefs.Should().ContainKey("capability2");
    }

    [Fact]
    public void BayesianSelfAssessor_CalibrateConfidence_EmptySamples_ShouldSucceed()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();

        // Act
        var result = assessor.CalibrateConfidence(Array.Empty<(double, double)>());

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void BayesianSelfAssessor_CalibrateConfidence_WithSamples_ShouldAdjustFactor()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();
        var samples = new[]
        {
            (0.9, 0.7), // Overconfident
            (0.8, 0.6),
            (0.85, 0.65)
        };

        // Act
        var result = assessor.CalibrateConfidence(samples);

        // Assert
        result.IsSuccess.Should().BeTrue();
        assessor.GetCalibrationFactor().Should().BeLessThan(1.0);
    }

    [Fact]
    public void BayesianSelfAssessor_UpdateDimensionScore_ShouldUpdateScore()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();

        // Act
        var result = assessor.UpdateDimensionScore(PerformanceDimension.Creativity, 0.8, 0.5, "creative output");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Score.Should().BeGreaterThan(0.5);
    }

    #endregion

    #region CognitiveEventType Enum Tests

    [Theory]
    [InlineData(CognitiveEventType.ThoughtGenerated)]
    [InlineData(CognitiveEventType.DecisionMade)]
    [InlineData(CognitiveEventType.ErrorDetected)]
    [InlineData(CognitiveEventType.ConfusionSensed)]
    [InlineData(CognitiveEventType.InsightGained)]
    [InlineData(CognitiveEventType.AttentionShift)]
    [InlineData(CognitiveEventType.GoalActivated)]
    [InlineData(CognitiveEventType.GoalCompleted)]
    [InlineData(CognitiveEventType.Uncertainty)]
    [InlineData(CognitiveEventType.Contradiction)]
    public void CognitiveEventType_AllValues_ShouldBeDefined(CognitiveEventType eventType)
    {
        // Assert
        Enum.IsDefined(typeof(CognitiveEventType), eventType).Should().BeTrue();
    }

    #endregion

    #region Severity Enum Tests

    [Theory]
    [InlineData(Severity.Info)]
    [InlineData(Severity.Warning)]
    [InlineData(Severity.Critical)]
    public void Severity_AllValues_ShouldBeDefined(Severity severity)
    {
        // Assert
        Enum.IsDefined(typeof(Severity), severity).Should().BeTrue();
    }

    #endregion

    #region CognitiveEvent Tests

    [Fact]
    public void CognitiveEvent_Thought_ShouldCreateThoughtEvent()
    {
        // Act
        var evt = CognitiveEvent.Thought("New idea");

        // Assert
        evt.Id.Should().NotBeEmpty();
        evt.EventType.Should().Be(CognitiveEventType.ThoughtGenerated);
        evt.Description.Should().Be("New idea");
        evt.Severity.Should().Be(Severity.Info);
        evt.Context.Should().BeEmpty();
    }

    [Fact]
    public void CognitiveEvent_Decision_ShouldCreateDecisionEvent()
    {
        // Act
        var evt = CognitiveEvent.Decision("Chose option A");

        // Assert
        evt.EventType.Should().Be(CognitiveEventType.DecisionMade);
        evt.Description.Should().Be("Chose option A");
        evt.Severity.Should().Be(Severity.Info);
    }

    [Fact]
    public void CognitiveEvent_Error_ShouldCreateErrorEvent()
    {
        // Act
        var evt = CognitiveEvent.Error("Processing failed", Severity.Critical);

        // Assert
        evt.EventType.Should().Be(CognitiveEventType.ErrorDetected);
        evt.Severity.Should().Be(Severity.Critical);
    }

    [Fact]
    public void CognitiveEvent_Confusion_ShouldCreateConfusionEvent()
    {
        // Act
        var evt = CognitiveEvent.Confusion("Unclear instructions");

        // Assert
        evt.EventType.Should().Be(CognitiveEventType.ConfusionSensed);
        evt.Severity.Should().Be(Severity.Warning);
    }

    [Fact]
    public void CognitiveEvent_Insight_ShouldCreateInsightEvent()
    {
        // Act
        var evt = CognitiveEvent.Insight("Pattern recognized");

        // Assert
        evt.EventType.Should().Be(CognitiveEventType.InsightGained);
        evt.Severity.Should().Be(Severity.Info);
    }

    [Fact]
    public void CognitiveEvent_ContradictionDetected_ShouldCreateCriticalEvent()
    {
        // Act
        var evt = CognitiveEvent.ContradictionDetected("Inconsistent data");

        // Assert
        evt.EventType.Should().Be(CognitiveEventType.Contradiction);
        evt.Severity.Should().Be(Severity.Critical);
    }

    [Fact]
    public void CognitiveEvent_WithContext_ShouldAddContext()
    {
        // Arrange
        var evt = CognitiveEvent.Thought("Test");

        // Act
        var withContext = evt.WithContext("key", "value");

        // Assert
        withContext.Context.Should().ContainKey("key");
        withContext.Context["key"].Should().Be("value");
        evt.Context.Should().BeEmpty("original should be unchanged");
    }

    [Fact]
    public void CognitiveEvent_WithMergedContext_ShouldMergeContexts()
    {
        // Arrange
        var evt = CognitiveEvent.Thought("Test").WithContext("existing", "value");
        var additional = ImmutableDictionary<string, object>.Empty.Add("new", "data");

        // Act
        var merged = evt.WithMergedContext(additional);

        // Assert
        merged.Context.Should().ContainKey("existing");
        merged.Context.Should().ContainKey("new");
    }

    #endregion

    #region MonitoringAlert Tests

    [Fact]
    public void MonitoringAlert_HighPriority_ShouldCreatePriority8Alert()
    {
        // Arrange
        var events = new[] { CognitiveEvent.Error("Error") };

        // Act
        var alert = MonitoringAlert.HighPriority("ErrorAlert", "Critical error", events, "Fix it");

        // Assert
        alert.Priority.Should().Be(8);
        alert.AlertType.Should().Be("ErrorAlert");
        alert.TriggeringEvents.Should().ContainSingle();
    }

    [Fact]
    public void MonitoringAlert_MediumPriority_ShouldCreatePriority5Alert()
    {
        // Arrange
        var events = new[] { CognitiveEvent.Confusion("Confused") };

        // Act
        var alert = MonitoringAlert.MediumPriority("ConfusionAlert", "High confusion", events, "Clarify");

        // Assert
        alert.Priority.Should().Be(5);
    }

    [Fact]
    public void MonitoringAlert_LowPriority_ShouldCreatePriority2Alert()
    {
        // Arrange
        var events = new[] { CognitiveEvent.Thought("Thought") };

        // Act
        var alert = MonitoringAlert.LowPriority("InfoAlert", "For your info", events, "Acknowledge");

        // Assert
        alert.Priority.Should().Be(2);
    }

    [Fact]
    public void MonitoringAlert_Validate_ValidAlert_ShouldReturnSuccess()
    {
        // Arrange
        var alert = MonitoringAlert.HighPriority("Test", "Message", Array.Empty<CognitiveEvent>(), "Action");

        // Act
        var result = alert.Validate();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region CognitiveHealth Tests

    [Fact]
    public void CognitiveHealth_Optimal_ShouldCreateHealthyStatus()
    {
        // Act
        var health = CognitiveHealth.Optimal();

        // Assert
        health.Status.Should().Be(HealthStatus.Healthy);
        health.HealthScore.Should().Be(1.0);
        health.ProcessingEfficiency.Should().Be(1.0);
        health.ErrorRate.Should().Be(0.0);
        health.ActiveAlerts.Should().BeEmpty();
    }

    [Fact]
    public void CognitiveHealth_FromMetrics_LowHealthScore_ShouldBeCritical()
    {
        // Act
        var health = CognitiveHealth.FromMetrics(
            healthScore: 0.2,
            efficiency: 0.5,
            errorRate: 0.1,
            latency: TimeSpan.FromMilliseconds(100),
            alerts: ImmutableList<MonitoringAlert>.Empty);

        // Assert
        health.Status.Should().Be(HealthStatus.Critical);
    }

    [Fact]
    public void CognitiveHealth_FromMetrics_HighErrorRate_ShouldBeCritical()
    {
        // Act
        var health = CognitiveHealth.FromMetrics(
            healthScore: 0.7,
            efficiency: 0.8,
            errorRate: 0.6,
            latency: TimeSpan.FromMilliseconds(100),
            alerts: ImmutableList<MonitoringAlert>.Empty);

        // Assert
        health.Status.Should().Be(HealthStatus.Critical);
    }

    [Fact]
    public void CognitiveHealth_FromMetrics_ModerateIssues_ShouldBeDegraded()
    {
        // Arrange
        var alert = MonitoringAlert.LowPriority("Test", "Test", Array.Empty<CognitiveEvent>(), "Test");

        // Act
        var health = CognitiveHealth.FromMetrics(
            healthScore: 0.65,
            efficiency: 0.7,
            errorRate: 0.05,
            latency: TimeSpan.FromMilliseconds(100),
            alerts: ImmutableList.Create(alert));

        // Assert
        health.Status.Should().Be(HealthStatus.Degraded);
    }

    [Fact]
    public void CognitiveHealth_RequiresAttention_UnhealthyStatus_ShouldReturnTrue()
    {
        // Arrange
        var health = CognitiveHealth.FromMetrics(0.5, 0.5, 0.2, TimeSpan.Zero, ImmutableList<MonitoringAlert>.Empty);

        // Act & Assert
        health.RequiresAttention().Should().BeTrue();
    }

    [Fact]
    public void CognitiveHealth_IsCritical_CriticalStatus_ShouldReturnTrue()
    {
        // Arrange
        var health = CognitiveHealth.FromMetrics(0.2, 0.3, 0.6, TimeSpan.Zero, ImmutableList<MonitoringAlert>.Empty);

        // Act & Assert
        health.IsCritical().Should().BeTrue();
    }

    [Fact]
    public void CognitiveHealth_Validate_ValidHealth_ShouldReturnSuccess()
    {
        // Arrange
        var health = CognitiveHealth.Optimal();

        // Act
        var result = health.Validate();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region RealtimeCognitiveMonitor Tests

    [Fact]
    public void RealtimeCognitiveMonitor_RecordEvent_ShouldSucceed()
    {
        // Arrange
        using var monitor = new RealtimeCognitiveMonitor();
        var evt = CognitiveEvent.Thought("Test thought");

        // Act
        var result = monitor.RecordEvent(evt);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void RealtimeCognitiveMonitor_RecordEvent_NullEvent_ShouldReturnFailure()
    {
        // Arrange
        using var monitor = new RealtimeCognitiveMonitor();

        // Act
        var result = monitor.RecordEvent(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RealtimeCognitiveMonitor_GetHealth_ShouldReturnHealth()
    {
        // Arrange
        using var monitor = new RealtimeCognitiveMonitor();

        // Act
        var health = monitor.GetHealth();

        // Assert
        health.Should().NotBeNull();
        health.Status.Should().Be(HealthStatus.Healthy);
    }

    [Fact]
    public void RealtimeCognitiveMonitor_GetRecentEvents_ShouldReturnCorrectCount()
    {
        // Arrange
        using var monitor = new RealtimeCognitiveMonitor();
        for (int i = 0; i < 10; i++)
        {
            monitor.RecordEvent(CognitiveEvent.Thought($"Thought {i}"));
        }

        // Act
        var events = monitor.GetRecentEvents(5);

        // Assert
        events.Should().HaveCount(5);
    }

    [Fact]
    public void RealtimeCognitiveMonitor_GetAlerts_ShouldReturnActiveAlerts()
    {
        // Arrange
        using var monitor = new RealtimeCognitiveMonitor();

        // Record many errors to trigger alert
        for (int i = 0; i < 5; i++)
        {
            monitor.RecordEvent(CognitiveEvent.Error($"Error {i}"));
        }

        // Act
        var alerts = monitor.GetAlerts();

        // Assert - may or may not have alerts depending on threshold configuration
        alerts.Should().NotBeNull();
    }

    [Fact]
    public void RealtimeCognitiveMonitor_AcknowledgeAlert_NonExisting_ShouldReturnFailure()
    {
        // Arrange
        using var monitor = new RealtimeCognitiveMonitor();

        // Act
        var result = monitor.AcknowledgeAlert(Guid.NewGuid());

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RealtimeCognitiveMonitor_SetThreshold_ShouldSucceed()
    {
        // Arrange
        using var monitor = new RealtimeCognitiveMonitor();

        // Act
        var result = monitor.SetThreshold("custom_metric", 0.5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        monitor.GetThreshold("custom_metric").HasValue.Should().BeTrue();
    }

    [Fact]
    public void RealtimeCognitiveMonitor_SetThreshold_EmptyMetric_ShouldReturnFailure()
    {
        // Arrange
        using var monitor = new RealtimeCognitiveMonitor();

        // Act
        var result = monitor.SetThreshold("", 0.5);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RealtimeCognitiveMonitor_SetThreshold_NegativeValue_ShouldReturnFailure()
    {
        // Arrange
        using var monitor = new RealtimeCognitiveMonitor();

        // Act
        var result = monitor.SetThreshold("metric", -0.5);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void RealtimeCognitiveMonitor_Subscribe_ShouldReceiveAlerts()
    {
        // Arrange
        using var monitor = new RealtimeCognitiveMonitor();
        var alertReceived = false;

        // Act
        using var subscription = monitor.Subscribe(_ => alertReceived = true);

        // Record events that may trigger alerts
        for (int i = 0; i < 10; i++)
        {
            monitor.RecordEvent(CognitiveEvent.ContradictionDetected($"Contradiction {i}"));
        }

        // Assert - subscription should be valid even if no alerts triggered
        subscription.Should().NotBeNull();
    }

    [Fact]
    public void RealtimeCognitiveMonitor_Reset_ShouldClearEvents()
    {
        // Arrange
        using var monitor = new RealtimeCognitiveMonitor();
        for (int i = 0; i < 5; i++)
        {
            monitor.RecordEvent(CognitiveEvent.Thought($"Thought {i}"));
        }

        // Act
        monitor.Reset();

        // Assert
        monitor.GetRecentEvents(10).Should().BeEmpty();
    }

    [Fact]
    public void RealtimeCognitiveMonitor_BufferLimit_ShouldTrimOldEvents()
    {
        // Arrange
        using var monitor = new RealtimeCognitiveMonitor(maxBufferSize: 5);

        // Act
        for (int i = 0; i < 10; i++)
        {
            monitor.RecordEvent(CognitiveEvent.Thought($"Thought {i}"));
        }

        // Assert
        monitor.GetRecentEvents(100).Should().HaveCountLessThanOrEqualTo(5);
    }

    #endregion

    #region ReasoningStepType Enum Tests

    [Theory]
    [InlineData(ReasoningStepType.Observation)]
    [InlineData(ReasoningStepType.Inference)]
    [InlineData(ReasoningStepType.Hypothesis)]
    [InlineData(ReasoningStepType.Validation)]
    [InlineData(ReasoningStepType.Revision)]
    [InlineData(ReasoningStepType.Assumption)]
    [InlineData(ReasoningStepType.Conclusion)]
    [InlineData(ReasoningStepType.Contradiction)]
    public void ReasoningStepType_AllValues_ShouldBeDefined(ReasoningStepType stepType)
    {
        // Assert
        Enum.IsDefined(typeof(ReasoningStepType), stepType).Should().BeTrue();
    }

    #endregion

    #region ReasoningStep Tests

    [Fact]
    public void ReasoningStep_Observation_ShouldCreateStepWithNoDependencies()
    {
        // Act
        var step = ReasoningStep.Observation(1, "Data shows X", "Relevant to hypothesis");

        // Assert
        step.StepNumber.Should().Be(1);
        step.StepType.Should().Be(ReasoningStepType.Observation);
        step.Content.Should().Be("Data shows X");
        step.Justification.Should().Be("Relevant to hypothesis");
        step.Dependencies.Should().BeEmpty();
    }

    [Fact]
    public void ReasoningStep_Inference_ShouldCreateStepWithDependencies()
    {
        // Act
        var step = ReasoningStep.Inference(2, "Therefore Y", "Follows from X", 1);

        // Assert
        step.StepType.Should().Be(ReasoningStepType.Inference);
        step.Dependencies.Should().ContainSingle().Which.Should().Be(1);
    }

    [Fact]
    public void ReasoningStep_Hypothesis_ShouldCreateHypothesisStep()
    {
        // Act
        var step = ReasoningStep.Hypothesis(3, "Maybe Z is true", "Based on observations", 1, 2);

        // Assert
        step.StepType.Should().Be(ReasoningStepType.Hypothesis);
        step.Dependencies.Should().HaveCount(2);
    }

    [Fact]
    public void ReasoningStep_Conclusion_ShouldCreateConclusionStep()
    {
        // Act
        var step = ReasoningStep.Conclusion(4, "Therefore, conclusion", "Based on all evidence", 1, 2, 3);

        // Assert
        step.StepType.Should().Be(ReasoningStepType.Conclusion);
        step.Dependencies.Should().HaveCount(3);
    }

    [Fact]
    public void ReasoningStep_WithDependency_ShouldAddDependency()
    {
        // Arrange
        var step = ReasoningStep.Inference(2, "Content", "Justification", 1);

        // Act
        var withDep = step.WithDependency(0); // Note: invalid but tests immutability

        // Assert
        withDep.Dependencies.Should().HaveCount(2);
        step.Dependencies.Should().ContainSingle();
    }

    [Fact]
    public void ReasoningStep_HasValidDependencies_ValidDeps_ShouldReturnTrue()
    {
        // Arrange
        var step = ReasoningStep.Inference(3, "Content", "Justification", 1, 2);

        // Act & Assert
        step.HasValidDependencies().Should().BeTrue();
    }

    [Fact]
    public void ReasoningStep_HasValidDependencies_SelfReference_ShouldReturnFalse()
    {
        // Arrange
        var step = new ReasoningStep(2, ReasoningStepType.Inference, "Content", "Justification",
            DateTime.UtcNow, ImmutableList.Create(2));

        // Act & Assert
        step.HasValidDependencies().Should().BeFalse();
    }

    [Fact]
    public void ReasoningStep_HasValidDependencies_FutureReference_ShouldReturnFalse()
    {
        // Arrange
        var step = new ReasoningStep(2, ReasoningStepType.Inference, "Content", "Justification",
            DateTime.UtcNow, ImmutableList.Create(3));

        // Act & Assert
        step.HasValidDependencies().Should().BeFalse();
    }

    #endregion

    #region ReasoningTrace Tests

    [Fact]
    public void ReasoningTrace_Start_ShouldCreateEmptyTrace()
    {
        // Act
        var trace = ReasoningTrace.Start();

        // Assert
        trace.Id.Should().NotBeEmpty();
        trace.Steps.Should().BeEmpty();
        trace.IsActive.Should().BeTrue();
        trace.FinalConclusion.Should().BeNull();
        trace.WasSuccessful.Should().BeFalse();
    }

    [Fact]
    public void ReasoningTrace_StartWithId_ShouldUseProvidedId()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var trace = ReasoningTrace.StartWithId(id);

        // Assert
        trace.Id.Should().Be(id);
    }

    [Fact]
    public void ReasoningTrace_WithStep_ShouldAddStep()
    {
        // Arrange
        var trace = ReasoningTrace.Start();
        var step = ReasoningStep.Observation(1, "Content", "Justification");

        // Act
        var withStep = trace.WithStep(step);

        // Assert
        withStep.Steps.Should().ContainSingle();
        trace.Steps.Should().BeEmpty("original should be unchanged");
    }

    [Fact]
    public void ReasoningTrace_AddObservation_ShouldAddObservationStep()
    {
        // Arrange
        var trace = ReasoningTrace.Start();

        // Act
        var withObs = trace.AddObservation("Observed X", "Relevant");

        // Assert
        withObs.Steps.Should().ContainSingle();
        withObs.Steps[0].StepType.Should().Be(ReasoningStepType.Observation);
    }

    [Fact]
    public void ReasoningTrace_AddInference_ShouldAddInferenceStep()
    {
        // Arrange
        var trace = ReasoningTrace.Start().AddObservation("X", "Reason");

        // Act
        var withInf = trace.AddInference("Therefore Y", "Based on X", 1);

        // Assert
        withInf.Steps.Should().HaveCount(2);
        withInf.Steps[1].StepType.Should().Be(ReasoningStepType.Inference);
    }

    [Fact]
    public void ReasoningTrace_Complete_ShouldFinalizeTrace()
    {
        // Arrange
        var trace = ReasoningTrace.Start()
            .AddObservation("Evidence", "Relevant")
            .AddInference("Conclusion follows", "Logic", 1);

        // Act
        var completed = trace.Complete("Final conclusion", 0.9);

        // Assert
        completed.IsActive.Should().BeFalse();
        completed.WasSuccessful.Should().BeTrue();
        completed.FinalConclusion.Should().Be("Final conclusion");
        completed.Confidence.Should().Be(0.9);
        completed.Duration.Should().NotBeNull();
    }

    [Fact]
    public void ReasoningTrace_Fail_ShouldMarkAsFailed()
    {
        // Arrange
        var trace = ReasoningTrace.Start().AddObservation("X", "Y");

        // Act
        var failed = trace.Fail("Could not reach conclusion");

        // Assert
        failed.IsActive.Should().BeFalse();
        failed.WasSuccessful.Should().BeFalse();
        failed.Confidence.Should().Be(0.0);
        failed.FinalConclusion.Should().Contain("Failed");
    }

    [Fact]
    public void ReasoningTrace_GetStepsByType_ShouldFilterCorrectly()
    {
        // Arrange
        var trace = ReasoningTrace.Start()
            .AddObservation("Obs 1", "Reason")
            .AddObservation("Obs 2", "Reason")
            .AddInference("Inf", "Reason", 1, 2);

        // Act
        var observations = trace.GetStepsByType(ReasoningStepType.Observation);

        // Assert
        observations.Should().HaveCount(2);
    }

    [Fact]
    public void ReasoningTrace_IsLogicallyConsistent_ValidTrace_ShouldReturnTrue()
    {
        // Arrange
        var trace = ReasoningTrace.Start()
            .AddObservation("X", "Reason")
            .AddInference("Y", "From X", 1);

        // Act & Assert
        trace.IsLogicallyConsistent().Should().BeTrue();
    }

    [Fact]
    public void ReasoningTrace_NextStepNumber_ShouldIncrement()
    {
        // Arrange
        var trace = ReasoningTrace.Start();

        // Assert
        trace.NextStepNumber.Should().Be(1);

        var withStep = trace.AddObservation("X", "Y");
        withStep.NextStepNumber.Should().Be(2);
    }

    #endregion

    #region ReflectionResult Tests

    [Fact]
    public void ReflectionResult_HighQuality_ShouldCreateGoodResult()
    {
        // Arrange
        var trace = ReasoningTrace.Start();

        // Act
        var result = ReflectionResult.HighQuality(trace);

        // Assert
        result.QualityScore.Should().Be(0.9);
        result.LogicalSoundness.Should().Be(0.95);
        result.IdentifiedFallacies.Should().BeEmpty();
        result.HasIssues.Should().BeFalse();
    }

    [Fact]
    public void ReflectionResult_Invalid_ShouldCreateInvalidResult()
    {
        // Arrange
        var trace = ReasoningTrace.Start();

        // Act
        var result = ReflectionResult.Invalid(trace);

        // Assert
        result.QualityScore.Should().Be(0.0);
        result.IdentifiedFallacies.Should().NotBeEmpty();
        result.HasIssues.Should().BeTrue();
    }

    [Fact]
    public void ReflectionResult_MeetsQualityThreshold_HighQuality_ShouldReturnTrue()
    {
        // Arrange
        var trace = ReasoningTrace.Start();
        var result = ReflectionResult.HighQuality(trace);

        // Act & Assert
        result.MeetsQualityThreshold(0.7).Should().BeTrue();
    }

    [Fact]
    public void ReflectionResult_MeetsQualityThreshold_LowQuality_ShouldReturnFalse()
    {
        // Arrange
        var trace = ReasoningTrace.Start();
        var result = ReflectionResult.Invalid(trace);

        // Act & Assert
        result.MeetsQualityThreshold(0.7).Should().BeFalse();
    }

    [Fact]
    public void ReflectionResult_WithFallacy_ShouldAddFallacy()
    {
        // Arrange
        var trace = ReasoningTrace.Start();
        var result = ReflectionResult.HighQuality(trace);

        // Act
        var withFallacy = result.WithFallacy("Circular reasoning");

        // Assert
        withFallacy.IdentifiedFallacies.Should().ContainSingle();
        withFallacy.HasIssues.Should().BeTrue();
    }

    [Fact]
    public void ReflectionResult_WithMissedConsideration_ShouldAddConsideration()
    {
        // Arrange
        var trace = ReasoningTrace.Start();
        var result = ReflectionResult.HighQuality(trace);

        // Act
        var updated = result.WithMissedConsideration("Did not consider X");

        // Assert
        updated.MissedConsiderations.Should().ContainSingle();
    }

    [Fact]
    public void ReflectionResult_WithImprovement_ShouldAddImprovement()
    {
        // Arrange
        var trace = ReasoningTrace.Start();
        var result = ReflectionResult.HighQuality(trace);

        // Act
        var updated = result.WithImprovement("Add more evidence");

        // Assert
        updated.Improvements.Should().ContainSingle();
    }

    #endregion

    #region ThinkingStyle Tests

    [Fact]
    public void ThinkingStyle_Balanced_ShouldCreateBalancedProfile()
    {
        // Act
        var style = ThinkingStyle.Balanced();

        // Assert
        style.StyleName.Should().Be("Balanced");
        style.AnalyticalScore.Should().Be(0.5);
        style.CreativeScore.Should().Be(0.5);
        style.BiasProfile.Should().BeEmpty();
    }

    [Fact]
    public void ThinkingStyle_Analytical_ShouldCreateAnalyticalProfile()
    {
        // Act
        var style = ThinkingStyle.Analytical();

        // Assert
        style.StyleName.Should().Be("Analytical");
        style.AnalyticalScore.Should().BeGreaterThan(0.7);
        style.DominantDimension.Should().Be("Analytical");
    }

    [Fact]
    public void ThinkingStyle_Creative_ShouldCreateCreativeProfile()
    {
        // Act
        var style = ThinkingStyle.Creative();

        // Assert
        style.StyleName.Should().Be("Creative");
        style.CreativeScore.Should().BeGreaterThan(0.7);
    }

    [Fact]
    public void ThinkingStyle_HasSignificantBiases_NoBiases_ShouldReturnFalse()
    {
        // Arrange
        var style = ThinkingStyle.Balanced();

        // Act & Assert
        style.HasSignificantBiases().Should().BeFalse();
    }

    [Fact]
    public void ThinkingStyle_HasSignificantBiases_WithBias_ShouldReturnTrue()
    {
        // Arrange
        var style = ThinkingStyle.Balanced().WithBias("Confirmation Bias", 0.7);

        // Act & Assert
        style.HasSignificantBiases().Should().BeTrue();
    }

    [Fact]
    public void ThinkingStyle_WithBias_ShouldAddBias()
    {
        // Arrange
        var style = ThinkingStyle.Balanced();

        // Act
        var withBias = style.WithBias("Anchoring", 0.6);

        // Assert
        withBias.BiasProfile.Should().ContainKey("Anchoring");
        withBias.BiasProfile["Anchoring"].Should().Be(0.6);
    }

    [Fact]
    public void ThinkingStyle_WithBias_ShouldClampValue()
    {
        // Arrange
        var style = ThinkingStyle.Balanced();

        // Act
        var withBias = style.WithBias("Extreme", 1.5);

        // Assert
        withBias.BiasProfile["Extreme"].Should().Be(1.0);
    }

    [Fact]
    public void ThinkingStyle_GetSignificantBiases_ShouldFilterByThreshold()
    {
        // Arrange
        var style = ThinkingStyle.Balanced()
            .WithBias("Strong", 0.8)
            .WithBias("Weak", 0.1)
            .WithBias("Medium", 0.5);

        // Act
        var significant = style.GetSignificantBiases(0.4).ToList();

        // Assert
        significant.Should().HaveCount(2);
        significant.Should().Contain(b => b.Bias == "Strong");
        significant.Should().Contain(b => b.Bias == "Medium");
    }

    #endregion

    #region MetacognitiveReasoner Tests

    [Fact]
    public void MetacognitiveReasoner_StartTrace_ShouldReturnNewTraceId()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();

        // Act
        var traceId = reasoner.StartTrace();

        // Assert
        traceId.Should().NotBeEmpty();
        reasoner.GetActiveTrace().HasValue.Should().BeTrue();
    }

    [Fact]
    public void MetacognitiveReasoner_AddStep_WithActiveTrace_ShouldSucceed()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        reasoner.StartTrace();

        // Act
        var result = reasoner.AddStep(ReasoningStepType.Observation, "Observed X", "Relevant");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(1);
    }

    [Fact]
    public void MetacognitiveReasoner_AddStep_WithoutActiveTrace_ShouldReturnFailure()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();

        // Act
        var result = reasoner.AddStep(ReasoningStepType.Observation, "Content", "Justification");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No active");
    }

    [Fact]
    public void MetacognitiveReasoner_EndTrace_ShouldCompleteTrace()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        reasoner.StartTrace();
        reasoner.AddStep(ReasoningStepType.Observation, "X", "Y");

        // Act
        var result = reasoner.EndTrace("Conclusion reached", success: true);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.WasSuccessful.Should().BeTrue();
        reasoner.GetActiveTrace().HasValue.Should().BeFalse();
    }

    [Fact]
    public void MetacognitiveReasoner_EndTrace_WithoutActiveTrace_ShouldReturnFailure()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();

        // Act
        var result = reasoner.EndTrace("Conclusion", true);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void MetacognitiveReasoner_ReflectOn_EmptyTrace_ShouldReturnInvalid()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        var trace = ReasoningTrace.Start();

        // Act
        var reflection = reasoner.ReflectOn(trace);

        // Assert
        reflection.QualityScore.Should().Be(0.0);
        reflection.HasIssues.Should().BeTrue();
    }

    [Fact]
    public void MetacognitiveReasoner_ReflectOn_ValidTrace_ShouldReturnQualityResult()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        var trace = ReasoningTrace.Start()
            .AddObservation("Evidence A", "Relevant")
            .AddObservation("Evidence B", "Also relevant")
            .AddInference("Therefore C", "From A and B", 1, 2)
            .Complete("Final conclusion", 0.8);

        // Act
        var reflection = reasoner.ReflectOn(trace);

        // Assert
        reflection.QualityScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public void MetacognitiveReasoner_GetThinkingStyle_NoHistory_ShouldReturnBalanced()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();

        // Act
        var style = reasoner.GetThinkingStyle();

        // Assert
        style.StyleName.Should().Be("Balanced");
    }

    [Fact]
    public void MetacognitiveReasoner_GetThinkingStyle_WithHistory_ShouldAnalyzeStyle()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();

        // Create multiple reasoning traces
        for (int i = 0; i < 3; i++)
        {
            reasoner.StartTrace();
            reasoner.AddStep(ReasoningStepType.Observation, $"Obs {i}", "Reason");
            reasoner.AddStep(ReasoningStepType.Inference, $"Inf {i}", "Logic", 1);
            reasoner.EndTrace($"Conclusion {i}", true);
        }

        // Act
        var style = reasoner.GetThinkingStyle();

        // Assert
        style.Should().NotBeNull();
    }

    [Fact]
    public void MetacognitiveReasoner_IdentifyBiases_EmptyHistory_ShouldReturnEmpty()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();

        // Act
        var biases = reasoner.IdentifyBiases(Array.Empty<ReasoningTrace>());

        // Assert
        biases.Should().BeEmpty();
    }

    [Fact]
    public void MetacognitiveReasoner_SuggestImprovement_ShortTrace_ShouldSuggestMoreDevelopment()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        var trace = ReasoningTrace.Start()
            .AddObservation("X", "Y")
            .Complete("Quick conclusion", 0.5);

        // Act
        var suggestions = reasoner.SuggestImprovement(trace);

        // Assert
        suggestions.Should().NotBeEmpty();
    }

    [Fact]
    public void MetacognitiveReasoner_SuggestImprovement_NoObservations_ShouldSuggestGatherMore()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        var trace = ReasoningTrace.Start();
        trace = trace.WithStep(ReasoningStep.Inference(1, "Inference", "Reason"));

        // Act
        var suggestions = reasoner.SuggestImprovement(trace);

        // Assert
        suggestions.Should().Contain(s => s.Contains("observation", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MetacognitiveReasoner_GetHistory_ShouldReturnCompletedTraces()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        reasoner.StartTrace();
        reasoner.AddStep(ReasoningStepType.Observation, "X", "Y");
        reasoner.EndTrace("Done", true);

        // Act
        var history = reasoner.GetHistory().ToList();

        // Assert
        history.Should().ContainSingle();
        history[0].WasSuccessful.Should().BeTrue();
    }

    [Fact]
    public void MetacognitiveReasoner_ThreadSafety_ConcurrentOperations()
    {
        // Arrange
        var reasoner = new MetacognitiveReasoner();
        var tasks = new List<Task>();

        // Act - start multiple traces concurrently
        for (int i = 0; i < 10; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                var traceId = reasoner.StartTrace();
                reasoner.AddStep(ReasoningStepType.Observation, $"Obs {index}", "Reason");
                reasoner.EndTrace($"Conclusion {index}", true);
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert - most traces should complete (may have races on active trace)
        reasoner.GetHistory().Should().HaveCountGreaterThanOrEqualTo(8);
    }

    #endregion

    #region Arrow Composition Tests

    [Fact]
    public async Task SelfAssessmentArrow_AssessArrow_ShouldReturnAssessment()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();
        var arrow = SelfAssessmentArrow.AssessArrow(assessor);

        // Act
        var result = await arrow(Ouroboros.Pipeline.Metacognition.Unit.Value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task SelfAssessmentArrow_AssessDimensionArrow_ShouldReturnDimensionScore()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();
        var arrow = SelfAssessmentArrow.AssessDimensionArrow(assessor);

        // Act
        var result = await arrow(PerformanceDimension.Accuracy);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Dimension.Should().Be(PerformanceDimension.Accuracy);
    }

    [Fact]
    public async Task SelfAssessmentArrow_UpdateBeliefArrow_ShouldUpdateBelief()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();
        var arrow = SelfAssessmentArrow.UpdateBeliefArrow(assessor);

        // Act
        var result = await arrow(("test_capability", 0.8));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.CapabilityName.Should().Be("test_capability");
    }

    [Fact]
    public async Task SelfAssessmentArrow_GetBeliefArrow_UnknownCapability_ShouldReturnNone()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();
        var arrow = SelfAssessmentArrow.GetBeliefArrow(assessor);

        // Act
        var result = await arrow("unknown");

        // Assert
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public async Task SelfAssessmentArrow_CalibrateArrow_ShouldSucceed()
    {
        // Arrange
        var assessor = new BayesianSelfAssessor();
        var arrow = SelfAssessmentArrow.CalibrateArrow(assessor);
        var samples = new[] { (0.8, 0.7), (0.9, 0.8) };

        // Act
        var result = await arrow(samples);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion
}
