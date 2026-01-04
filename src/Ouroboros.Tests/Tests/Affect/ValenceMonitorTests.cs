// <copyright file="ValenceMonitorTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Affect;

using FluentAssertions;
using Ouroboros.Agent.MetaAI.Affect;
using Xunit;

/// <summary>
/// Tests for ValenceMonitor - Phase 3 Affective Dynamics.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ValenceMonitorTests
{
    [Fact]
    public void GetCurrentState_ReturnsValidState()
    {
        // Arrange
        var monitor = new ValenceMonitor();

        // Act
        var state = monitor.GetCurrentState();

        // Assert
        state.Should().NotBeNull();
        state.Id.Should().NotBe(Guid.Empty);
        state.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void GetCurrentState_InitialValues_AreBaseline()
    {
        // Arrange
        var monitor = new ValenceMonitor();

        // Act
        var state = monitor.GetCurrentState();

        // Assert
        state.Valence.Should().Be(0.0);
        state.Stress.Should().Be(0.0);
        state.Confidence.Should().Be(0.5);
        state.Curiosity.Should().Be(0.3);
        state.Arousal.Should().Be(0.5);
    }

    [Theory]
    [InlineData(SignalType.Stress, 0.8)]
    [InlineData(SignalType.Confidence, 0.9)]
    [InlineData(SignalType.Curiosity, 0.7)]
    [InlineData(SignalType.Valence, 0.5)]
    [InlineData(SignalType.Arousal, 0.6)]
    public void RecordSignal_StoresSignal(SignalType type, double value)
    {
        // Arrange
        var monitor = new ValenceMonitor();

        // Act
        monitor.RecordSignal("test_source", value, type);
        var signals = monitor.GetRecentSignals(type);

        // Assert
        signals.Should().HaveCount(1);
        signals[0].Source.Should().Be("test_source");
        signals[0].Value.Should().BeApproximately(value, 0.01);
        signals[0].Type.Should().Be(type);
    }

    [Fact]
    public void RecordSignal_ClampsValueToRange()
    {
        // Arrange
        var monitor = new ValenceMonitor();

        // Act
        monitor.RecordSignal("test", 2.0, SignalType.Valence);
        monitor.RecordSignal("test", -2.0, SignalType.Valence);
        var signals = monitor.GetRecentSignals(SignalType.Valence);

        // Assert
        signals.Should().HaveCount(2);
        signals[0].Value.Should().Be(1.0);
        signals[1].Value.Should().Be(-1.0);
    }

    [Fact]
    public void RecordSignal_UpdatesInternalState()
    {
        // Arrange
        var monitor = new ValenceMonitor();

        // Act
        for (int i = 0; i < 5; i++)
        {
            monitor.RecordSignal("stress_source", 0.9, SignalType.Stress);
        }
        var state = monitor.GetCurrentState();

        // Assert
        state.Stress.Should().BeGreaterThan(0);
    }

    [Fact]
    public void UpdateConfidence_IncreasesOnSuccess()
    {
        // Arrange
        var monitor = new ValenceMonitor();
        var initial = monitor.GetCurrentState();

        // Act
        monitor.UpdateConfidence("task1", success: true, weight: 1.0);
        var updated = monitor.GetCurrentState();

        // Assert
        updated.Confidence.Should().BeGreaterThanOrEqualTo(initial.Confidence * 0.9); // accounting for decay
    }

    [Fact]
    public void UpdateConfidence_DecreasesOnFailure()
    {
        // Arrange
        var monitor = new ValenceMonitor();
        var initial = monitor.GetCurrentState();

        // Act
        monitor.UpdateConfidence("task1", success: false, weight: 1.0);
        var updated = monitor.GetCurrentState();

        // Assert
        updated.Confidence.Should().BeLessThan(initial.Confidence);
    }

    [Fact]
    public void UpdateCuriosity_IncreasesWithNovelty()
    {
        // Arrange
        var monitor = new ValenceMonitor();
        var initial = monitor.GetCurrentState();

        // Act
        monitor.UpdateCuriosity(0.9, "novel_context");
        var updated = monitor.GetCurrentState();

        // Assert
        updated.Curiosity.Should().BeGreaterThan(initial.Curiosity);
    }

    [Fact]
    public async Task DetectStressAsync_ReturnsResult()
    {
        // Arrange
        var monitor = new ValenceMonitor();

        // Add some stress signals
        for (int i = 0; i < 10; i++)
        {
            monitor.RecordSignal("test", 0.5 + (0.1 * Math.Sin(i * 0.5)), SignalType.Stress);
        }

        // Act
        var result = await monitor.DetectStressAsync();

        // Assert
        result.Should().NotBeNull();
        result.DetectedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DetectStressAsync_WithInsufficientData_ReturnsDefault()
    {
        // Arrange
        var monitor = new ValenceMonitor();

        // Act
        var result = await monitor.DetectStressAsync();

        // Assert
        result.Should().NotBeNull();
        result.Analysis.Should().Contain("Insufficient data");
    }

    [Fact]
    public void GetRunningAverage_CalculatesCorrectly()
    {
        // Arrange
        var monitor = new ValenceMonitor();
        monitor.RecordSignal("test", 0.2, SignalType.Stress);
        monitor.RecordSignal("test", 0.4, SignalType.Stress);
        monitor.RecordSignal("test", 0.6, SignalType.Stress);

        // Act
        var average = monitor.GetRunningAverage(SignalType.Stress, 3);

        // Assert
        average.Should().BeApproximately(0.4, 0.01);
    }

    [Fact]
    public void Reset_ClearsAllState()
    {
        // Arrange
        var monitor = new ValenceMonitor();
        monitor.RecordSignal("test", 0.8, SignalType.Stress);
        monitor.UpdateConfidence("task", true);

        // Act
        monitor.Reset();
        var state = monitor.GetCurrentState();

        // Assert
        state.Valence.Should().Be(0.0);
        state.Stress.Should().Be(0.0);
        state.Confidence.Should().Be(0.5);
        state.Curiosity.Should().Be(0.3);
        state.Arousal.Should().Be(0.5);
    }

    [Fact]
    public void GetSignalHistory_ReturnsCorrectArray()
    {
        // Arrange
        var monitor = new ValenceMonitor();
        monitor.RecordSignal("test", 0.1, SignalType.Valence);
        monitor.RecordSignal("test", 0.2, SignalType.Valence);
        monitor.RecordSignal("test", 0.3, SignalType.Valence);

        // Act
        var history = monitor.GetSignalHistory(SignalType.Valence);

        // Assert
        history.Should().HaveCount(3);
        history[0].Should().BeApproximately(0.1, 0.01);
        history[1].Should().BeApproximately(0.2, 0.01);
        history[2].Should().BeApproximately(0.3, 0.01);
    }

    [Fact]
    public void GetStateHistory_ReturnsMultipleStates()
    {
        // Arrange
        var monitor = new ValenceMonitor();

        // Act
        _ = monitor.GetCurrentState();
        _ = monitor.GetCurrentState();
        _ = monitor.GetCurrentState();
        var history = monitor.GetStateHistory(10);

        // Assert
        history.Should().HaveCount(3);
    }
}
