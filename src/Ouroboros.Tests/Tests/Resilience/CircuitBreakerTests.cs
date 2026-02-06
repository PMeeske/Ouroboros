// <copyright file="CircuitBreakerTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Resilience;

using FluentAssertions;
using Ouroboros.Core.Resilience;
using Xunit;

/// <summary>
/// Comprehensive tests for CircuitBreaker implementation.
/// Validates state transitions, thread safety, and timing behavior.
/// </summary>
[Trait("Category", "Unit")]
public class CircuitBreakerTests
{
    [Fact]
    public void Constructor_WithDefaultValues_CreatesClosedCircuit()
    {
        // Act
        var breaker = new CircuitBreaker();

        // Assert
        breaker.State.Should().Be(CircuitState.Closed);
        breaker.IsClosed.Should().BeTrue();
        breaker.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void Constructor_WithCustomValues_UsesProvidedThreshold()
    {
        // Arrange & Act
        var breaker = new CircuitBreaker(failureThreshold: 5, openDuration: TimeSpan.FromSeconds(30));

        // Assert
        breaker.State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void Constructor_WithInvalidThreshold_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircuitBreaker(failureThreshold: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircuitBreaker(failureThreshold: -1));
    }

    [Fact]
    public void RecordFailure_BelowThreshold_StaysInClosedState()
    {
        // Arrange
        var breaker = new CircuitBreaker(failureThreshold: 3);

        // Act
        breaker.RecordFailure();
        breaker.RecordFailure();

        // Assert
        breaker.State.Should().Be(CircuitState.Closed);
        breaker.IsClosed.Should().BeTrue();
        breaker.ShouldAttempt().Should().BeTrue();
    }

    [Fact]
    public void RecordFailure_ReachingThreshold_TransitionsToOpen()
    {
        // Arrange
        var breaker = new CircuitBreaker(failureThreshold: 3);

        // Act
        breaker.RecordFailure();
        breaker.RecordFailure();
        breaker.RecordFailure();

        // Assert
        breaker.State.Should().Be(CircuitState.Open);
        breaker.IsOpen.Should().BeTrue();
        breaker.IsClosed.Should().BeFalse();
    }

    [Fact]
    public void ShouldAttempt_WhenOpen_ReturnsFalse()
    {
        // Arrange
        var breaker = new CircuitBreaker(failureThreshold: 2, openDuration: TimeSpan.FromHours(1));
        breaker.RecordFailure();
        breaker.RecordFailure();

        // Assert
        breaker.State.Should().Be(CircuitState.Open);
        breaker.ShouldAttempt().Should().BeFalse();
    }

    [Fact]
    public void ShouldAttempt_AfterOpenDuration_TransitionsToHalfOpen()
    {
        // Arrange
        var breaker = new CircuitBreaker(failureThreshold: 2, openDuration: TimeSpan.FromMilliseconds(50));
        breaker.RecordFailure();
        breaker.RecordFailure();
        breaker.State.Should().Be(CircuitState.Open);

        // Act - wait for open duration to elapse
        Thread.Sleep(100);
        var shouldAttempt = breaker.ShouldAttempt();

        // Assert
        shouldAttempt.Should().BeTrue();
        breaker.State.Should().Be(CircuitState.HalfOpen);
    }

    [Fact]
    public void RecordSuccess_InHalfOpenState_TransitionsToClosed()
    {
        // Arrange
        var breaker = new CircuitBreaker(failureThreshold: 2, openDuration: TimeSpan.FromMilliseconds(50));
        
        // Open the circuit
        breaker.RecordFailure();
        breaker.RecordFailure();
        breaker.State.Should().Be(CircuitState.Open);

        // Wait and transition to HalfOpen
        Thread.Sleep(100);
        breaker.ShouldAttempt();
        breaker.State.Should().Be(CircuitState.HalfOpen);

        // Act - successful operation in HalfOpen
        breaker.RecordSuccess();

        // Assert
        breaker.State.Should().Be(CircuitState.Closed);
        breaker.IsClosed.Should().BeTrue();
    }

    [Fact]
    public void RecordFailure_InHalfOpenState_TransitionsBackToOpen()
    {
        // Arrange
        var breaker = new CircuitBreaker(failureThreshold: 2, openDuration: TimeSpan.FromMilliseconds(50));
        
        // Open the circuit
        breaker.RecordFailure();
        breaker.RecordFailure();

        // Wait and transition to HalfOpen
        Thread.Sleep(100);
        breaker.ShouldAttempt();
        breaker.State.Should().Be(CircuitState.HalfOpen);

        // Act - failed operation in HalfOpen
        breaker.RecordFailure();

        // Assert
        breaker.State.Should().Be(CircuitState.Open);
        breaker.IsOpen.Should().BeTrue();
    }

    [Fact]
    public void RecordSuccess_InClosedState_ResetsFailureCount()
    {
        // Arrange
        var breaker = new CircuitBreaker(failureThreshold: 3);
        breaker.RecordFailure();
        breaker.RecordFailure();
        breaker.State.Should().Be(CircuitState.Closed);

        // Act
        breaker.RecordSuccess();

        // More failures shouldn't immediately open the circuit since count was reset
        breaker.RecordFailure();
        breaker.RecordFailure();

        // Assert - should still be closed (only 2 failures after reset)
        breaker.State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void ShouldAttempt_InClosedState_ReturnsTrue()
    {
        // Arrange
        var breaker = new CircuitBreaker();

        // Act & Assert
        breaker.ShouldAttempt().Should().BeTrue();
    }

    [Fact]
    public void ShouldAttempt_InHalfOpenState_ReturnsTrue()
    {
        // Arrange
        var breaker = new CircuitBreaker(failureThreshold: 1, openDuration: TimeSpan.FromMilliseconds(50));
        breaker.RecordFailure();
        Thread.Sleep(100);
        breaker.ShouldAttempt(); // Transition to HalfOpen

        // Act & Assert
        breaker.State.Should().Be(CircuitState.HalfOpen);
        breaker.ShouldAttempt().Should().BeTrue();
    }

    [Fact]
    public void ThreadSafety_ConcurrentRecordFailure_MaintainsCorrectState()
    {
        // Arrange
        var breaker = new CircuitBreaker(failureThreshold: 10);
        var tasks = new List<Task>();

        // Act - concurrent failures
        for (int i = 0; i < 20; i++)
        {
            tasks.Add(Task.Run(() => breaker.RecordFailure()));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert - should be open after threshold exceeded
        breaker.State.Should().Be(CircuitState.Open);
    }

    [Fact]
    public void ThreadSafety_ConcurrentOperations_DoesNotCorruptState()
    {
        // Arrange
        var breaker = new CircuitBreaker(failureThreshold: 5, openDuration: TimeSpan.FromMilliseconds(100));
        var random = new Random();
        var tasks = new List<Task>();

        // Act - concurrent mixed operations
        for (int i = 0; i < 50; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                var operation = random.Next(3);
                switch (operation)
                {
                    case 0:
                        breaker.RecordFailure();
                        break;
                    case 1:
                        breaker.RecordSuccess();
                        break;
                    case 2:
                        _ = breaker.ShouldAttempt();
                        break;
                }
            }));
        }

        Task.WaitAll(tasks.ToArray());

        // Assert - state should be valid (not corrupted)
        var state = breaker.State;
        state.Should().BeOneOf(CircuitState.Closed, CircuitState.Open, CircuitState.HalfOpen);
    }

    [Fact]
    public void FullCycle_ClosedToOpenToHalfOpenToClosed()
    {
        // Arrange
        var breaker = new CircuitBreaker(failureThreshold: 2, openDuration: TimeSpan.FromMilliseconds(100));

        // Act & Assert - Closed
        breaker.State.Should().Be(CircuitState.Closed);

        // Closed → Open
        breaker.RecordFailure();
        breaker.RecordFailure();
        breaker.State.Should().Be(CircuitState.Open);
        breaker.ShouldAttempt().Should().BeFalse();

        // Open → HalfOpen (after duration)
        Thread.Sleep(150);
        breaker.ShouldAttempt().Should().BeTrue();
        breaker.State.Should().Be(CircuitState.HalfOpen);

        // HalfOpen → Closed (on success)
        breaker.RecordSuccess();
        breaker.State.Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void MultipleOpenCycles_WorksCorrectly()
    {
        // Arrange
        var breaker = new CircuitBreaker(failureThreshold: 2, openDuration: TimeSpan.FromMilliseconds(50));

        // First cycle: Closed → Open → HalfOpen → Closed
        breaker.RecordFailure();
        breaker.RecordFailure();
        breaker.State.Should().Be(CircuitState.Open);
        
        Thread.Sleep(100);
        breaker.ShouldAttempt();
        breaker.State.Should().Be(CircuitState.HalfOpen);
        
        breaker.RecordSuccess();
        breaker.State.Should().Be(CircuitState.Closed);

        // Second cycle: Closed → Open → HalfOpen → Open (failure in half-open)
        breaker.RecordFailure();
        breaker.RecordFailure();
        breaker.State.Should().Be(CircuitState.Open);
        
        Thread.Sleep(100);
        breaker.ShouldAttempt();
        breaker.State.Should().Be(CircuitState.HalfOpen);
        
        breaker.RecordFailure();
        breaker.State.Should().Be(CircuitState.Open);
    }
}
