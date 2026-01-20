// <copyright file="CouncilDecisionTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Pipeline.Council;

namespace Ouroboros.Tests.Council;

/// <summary>
/// Tests for CouncilDecision and related records.
/// </summary>
[Trait("Category", "Unit")]
public class CouncilDecisionTests
{
    [Fact]
    public void IsConsensus_AllVotesSame_ShouldReturnTrue()
    {
        // Arrange
        var votes = new Dictionary<string, AgentVote>
        {
            ["Agent1"] = new AgentVote("Agent1", "APPROVE", 1.0, "Good idea"),
            ["Agent2"] = new AgentVote("Agent2", "APPROVE", 0.8, "Agreed"),
            ["Agent3"] = new AgentVote("Agent3", "APPROVE", 0.9, "Support")
        };

        var decision = new CouncilDecision(
            "We should proceed",
            votes,
            [],
            0.9,
            []);

        // Assert
        decision.IsConsensus.Should().BeTrue();
    }

    [Fact]
    public void IsConsensus_MixedVotes_ShouldReturnFalse()
    {
        // Arrange
        var votes = new Dictionary<string, AgentVote>
        {
            ["Agent1"] = new AgentVote("Agent1", "APPROVE", 1.0, "Good idea"),
            ["Agent2"] = new AgentVote("Agent2", "REJECT", 0.8, "Too risky"),
            ["Agent3"] = new AgentVote("Agent3", "APPROVE", 0.9, "Support")
        };

        var decision = new CouncilDecision(
            "Majority approves",
            votes,
            [],
            0.7,
            []);

        // Assert
        decision.IsConsensus.Should().BeFalse();
    }

    [Fact]
    public void MajorityPosition_ShouldReturnHighestWeightedPosition()
    {
        // Arrange
        var votes = new Dictionary<string, AgentVote>
        {
            ["Agent1"] = new AgentVote("Agent1", "APPROVE", 1.0, "Good idea"),
            ["Agent2"] = new AgentVote("Agent2", "REJECT", 0.3, "Too risky"),
            ["Agent3"] = new AgentVote("Agent3", "APPROVE", 0.9, "Support")
        };

        var decision = new CouncilDecision(
            "Majority approves",
            votes,
            [],
            0.7,
            []);

        // Assert
        decision.MajorityPosition.Should().Be("APPROVE");
    }

    [Fact]
    public void Failed_ShouldCreateFailedDecision()
    {
        // Arrange
        var reason = "No agents available";

        // Act
        var decision = CouncilDecision.Failed(reason);

        // Assert
        decision.Conclusion.Should().Contain(reason);
        decision.Votes.Should().BeEmpty();
        decision.Transcript.Should().BeEmpty();
        decision.Confidence.Should().Be(0.0);
        decision.MinorityOpinions.Should().BeEmpty();
    }

    [Fact]
    public void AgentVote_ShouldStoreAllProperties()
    {
        // Arrange & Act
        var vote = new AgentVote("TestAgent", "APPROVE", 0.85, "Because it's good");

        // Assert
        vote.AgentName.Should().Be("TestAgent");
        vote.Position.Should().Be("APPROVE");
        vote.Weight.Should().Be(0.85);
        vote.Rationale.Should().Be("Because it's good");
    }

    [Fact]
    public void DebateRound_ShouldStorePhaseAndContributions()
    {
        // Arrange
        var contributions = new List<AgentContribution>
        {
            new AgentContribution("Agent1", "My proposal is..."),
            new AgentContribution("Agent2", "I suggest...")
        };

        // Act
        var round = new DebateRound(
            DebatePhase.Proposal,
            1,
            contributions,
            DateTime.UtcNow);

        // Assert
        round.Phase.Should().Be(DebatePhase.Proposal);
        round.RoundNumber.Should().Be(1);
        round.Contributions.Should().HaveCount(2);
    }

    [Fact]
    public void AgentContribution_ShouldGenerateUniqueId()
    {
        // Arrange & Act
        var contrib1 = new AgentContribution("Agent1", "Content 1");
        var contrib2 = new AgentContribution("Agent1", "Content 2");

        // Assert
        contrib1.Id.Should().NotBeEmpty();
        contrib2.Id.Should().NotBeEmpty();
        contrib1.Id.Should().NotBe(contrib2.Id);
    }

    [Fact]
    public void MinorityOpinion_ShouldStoreDissentingView()
    {
        // Arrange & Act
        var opinion = new MinorityOpinion(
            "SecurityCynic",
            "REJECT",
            "Security concerns not addressed",
            ["SQL injection risk", "No encryption"]);

        // Assert
        opinion.AgentName.Should().Be("SecurityCynic");
        opinion.Position.Should().Be("REJECT");
        opinion.Rationale.Should().Contain("Security concerns");
        opinion.Concerns.Should().HaveCount(2);
    }
}
