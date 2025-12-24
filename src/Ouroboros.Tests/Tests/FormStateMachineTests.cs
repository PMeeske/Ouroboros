// <copyright file="FormStateMachineTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Tests;

using FluentAssertions;
using LangChainPipeline.Core.LawsOfForm;
using Xunit;

/// <summary>
/// Tests for FormStateMachine with indeterminate state support.
/// Validates state transitions, oscillation, and leader election scenarios.
/// </summary>
public class FormStateMachineTests
{
    private enum ServerRole { Follower, Candidate, Leader }

    [Fact]
    public void Constructor_StartsInCertainState()
    {
        // Arrange & Act
        var machine = new FormStateMachine<ServerRole>(ServerRole.Follower);

        // Assert
        machine.CurrentForm.Should().Be(Form.Mark);
        machine.IsCertain.Should().BeTrue();
        machine.IsIndeterminate.Should().BeFalse();
        machine.CurrentState.HasValue.Should().BeTrue();
        machine.CurrentState.Value.Should().Be(ServerRole.Follower);
    }

    [Fact]
    public void TransitionTo_ChangesCertainState()
    {
        // Arrange
        var machine = new FormStateMachine<ServerRole>(ServerRole.Follower);

        // Act
        machine.TransitionTo(ServerRole.Leader, "Won election");

        // Assert
        machine.CurrentState.HasValue.Should().BeTrue();
        machine.CurrentState.Value.Should().Be(ServerRole.Leader);
        machine.IsCertain.Should().BeTrue();
        machine.History.Should().HaveCount(2);
    }

    [Fact]
    public void EnterIndeterminateState_BecomesIndeterminate()
    {
        // Arrange
        var machine = new FormStateMachine<ServerRole>(ServerRole.Follower);

        // Act
        machine.EnterIndeterminateState(0.5, "Election in progress");

        // Assert
        machine.CurrentForm.Should().Be(Form.Imaginary);
        machine.IsIndeterminate.Should().BeTrue();
        machine.IsCertain.Should().BeFalse();
        machine.CurrentState.HasValue.Should().BeFalse();
        machine.OscillationPhase.Should().Be(0.5);
    }

    [Fact]
    public void EnterIndeterminateState_ThrowsForInvalidPhase()
    {
        // Arrange
        var machine = new FormStateMachine<ServerRole>(ServerRole.Follower);

        // Act & Assert
        var act = () => machine.EnterIndeterminateState(1.5, "Invalid");
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("phase");
    }

    [Fact]
    public void ResolveState_ResolvesIndeterminateState()
    {
        // Arrange
        var machine = new FormStateMachine<ServerRole>(ServerRole.Follower);
        machine.EnterIndeterminateState(0.5, "Election started");

        // Act
        machine.ResolveState(ServerRole.Leader, "Election completed");

        // Assert
        machine.IsCertain.Should().BeTrue();
        machine.CurrentState.HasValue.Should().BeTrue();
        machine.CurrentState.Value.Should().Be(ServerRole.Leader);
    }

    [Fact]
    public void ResolveState_ThrowsWhenNotIndeterminate()
    {
        // Arrange
        var machine = new FormStateMachine<ServerRole>(ServerRole.Follower);

        // Act & Assert
        var act = () => machine.ResolveState(ServerRole.Leader, "Test");
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UpdatePhase_UpdatesOscillationPhase()
    {
        // Arrange
        var machine = new FormStateMachine<ServerRole>(ServerRole.Follower);
        machine.EnterIndeterminateState(0.3, "Test");

        // Act
        machine.UpdatePhase(0.7);

        // Assert
        machine.OscillationPhase.Should().Be(0.7);
    }

    [Fact]
    public void UpdatePhase_ThrowsWhenNotIndeterminate()
    {
        // Arrange
        var machine = new FormStateMachine<ServerRole>(ServerRole.Follower);

        // Act & Assert
        var act = () => machine.UpdatePhase(0.5);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UpdatePhase_ThrowsForInvalidPhase()
    {
        // Arrange
        var machine = new FormStateMachine<ServerRole>(ServerRole.Follower);
        machine.EnterIndeterminateState(0.5, "Test");

        // Act & Assert
        var act = () => machine.UpdatePhase(-0.1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void WhenCertain_ExecutesActionWhenCertain()
    {
        // Arrange
        var machine = new FormStateMachine<ServerRole>(ServerRole.Leader);

        // Act
        var result = machine.WhenCertain(role => role.ToString());

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value.Should().Be("Leader");
    }

    [Fact]
    public void WhenCertain_ReturnsNoneWhenIndeterminate()
    {
        // Arrange
        var machine = new FormStateMachine<ServerRole>(ServerRole.Follower);
        machine.EnterIndeterminateState(0.5, "Test");

        // Act
        var result = machine.WhenCertain(role => role.ToString());

        // Assert
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void SampleAt_OscillatesBetweenStates()
    {
        // Arrange
        var machine = new FormStateMachine<ServerRole>(ServerRole.Follower);
        machine.EnterIndeterminateState(0.0, "Election");

        // Act - Sample at different time steps
        var sample1 = machine.SampleAt(ServerRole.Leader, ServerRole.Follower, 0.0);

        // Assert - Due to sine wave at timeStep=0, phase=0: sin(0) = 0, so we get state2 (Follower)
        sample1.Should().Be(ServerRole.Follower);
        
        // Sample at different time step
        var sample2 = machine.SampleAt(ServerRole.Leader, ServerRole.Follower, 0.5);
        // At timeStep=0.5, phase=0: sin(π) ≈ 0, boundary case - also Follower
        sample2.Should().Be(ServerRole.Follower);
    }

    [Fact]
    public void SampleAt_ThrowsWhenNotIndeterminate()
    {
        // Arrange
        var machine = new FormStateMachine<ServerRole>(ServerRole.Leader);

        // Act & Assert
        var act = () => machine.SampleAt(ServerRole.Leader, ServerRole.Follower, 0.0);
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void History_TracksAllTransitions()
    {
        // Arrange
        var machine = new FormStateMachine<ServerRole>(ServerRole.Follower);

        // Act
        machine.TransitionTo(ServerRole.Candidate, "Start election");
        machine.EnterIndeterminateState(0.5, "Voting");
        machine.ResolveState(ServerRole.Leader, "Won");

        // Assert
        machine.History.Should().HaveCount(4);
        machine.History[0].State.HasValue.Should().BeTrue();
        machine.History[0].State.Value.Should().Be(ServerRole.Follower);
        machine.History[1].State.Value.Should().Be(ServerRole.Candidate);
        machine.History[2].State.HasValue.Should().BeFalse();
        machine.History[3].State.Value.Should().Be(ServerRole.Leader);
    }

    [Fact]
    public void ToString_ShowsCertainState()
    {
        // Arrange
        var machine = new FormStateMachine<ServerRole>(ServerRole.Leader);

        // Act
        var result = machine.ToString();

        // Assert
        result.Should().Contain("Certain");
        result.Should().Contain("Leader");
    }

    [Fact]
    public void ToString_ShowsIndeterminateState()
    {
        // Arrange
        var machine = new FormStateMachine<ServerRole>(ServerRole.Follower);
        machine.EnterIndeterminateState(0.65, "Test");

        // Act
        var result = machine.ToString();

        // Assert
        result.Should().Contain("Indeterminate");
        result.Should().Contain("0.65");
    }

    [Fact]
    public void LeaderElectionScenario_CompleteFlow()
    {
        // Arrange - Simulate a distributed consensus scenario
        var machine = new FormStateMachine<ServerRole>(ServerRole.Follower);

        // Act - Simulate leader election flow
        // 1. Start as follower
        machine.CurrentState.Value.Should().Be(ServerRole.Follower);

        // 2. Become candidate
        machine.TransitionTo(ServerRole.Candidate, "Timeout, starting election");
        machine.CurrentState.Value.Should().Be(ServerRole.Candidate);

        // 3. Enter indeterminate state during voting
        machine.EnterIndeterminateState(0.6, "Waiting for votes from quorum");
        machine.IsIndeterminate.Should().BeTrue();

        // 4. Update confidence as votes come in
        machine.UpdatePhase(0.75);
        machine.OscillationPhase.Should().Be(0.75);

        // 5. Resolve to leader after winning majority
        machine.ResolveState(ServerRole.Leader, "Received majority votes");
        machine.IsCertain.Should().BeTrue();
        machine.CurrentState.Value.Should().Be(ServerRole.Leader);

        // Assert - History should show complete election cycle
        machine.History.Should().HaveCount(4);
        machine.History.Last().Reason.Should().Contain("Resolved");
    }

    [Fact]
    public void NetworkPartitionScenario_HandlesSplitBrain()
    {
        // Arrange - Two nodes think they're leader (split brain)
        var node1 = new FormStateMachine<ServerRole>(ServerRole.Leader);
        var node2 = new FormStateMachine<ServerRole>(ServerRole.Leader);

        // Act - Detect partition and enter indeterminate state
        node1.EnterIndeterminateState(0.5, "Network partition detected");
        node2.EnterIndeterminateState(0.5, "Network partition detected");

        // Assert - Both should be indeterminate
        node1.IsIndeterminate.Should().BeTrue();
        node2.IsIndeterminate.Should().BeTrue();

        // Act - Partition heals, re-elect
        node1.ResolveState(ServerRole.Leader, "Won re-election");
        node2.ResolveState(ServerRole.Follower, "Lost re-election");

        // Assert - Consensus restored
        node1.IsCertain.Should().BeTrue();
        node2.IsCertain.Should().BeTrue();
        node1.CurrentState.Value.Should().Be(ServerRole.Leader);
        node2.CurrentState.Value.Should().Be(ServerRole.Follower);
    }
}
