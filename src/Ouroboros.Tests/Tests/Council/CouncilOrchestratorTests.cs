// <copyright file="CouncilOrchestratorTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Pipeline.Council;
using Ouroboros.Pipeline.Council.Agents;

namespace Ouroboros.Tests.Council;

/// <summary>
/// Tests for CouncilOrchestrator functionality.
/// </summary>
[Trait("Category", "Unit")]
public class CouncilOrchestratorTests
{
    /// <summary>
    /// Simple mock chat model for testing.
    /// </summary>
    private class MockChatCompletionModel : IChatCompletionModel
    {
        private readonly Func<string, CancellationToken, Task<string>> _generateFunc;

        public MockChatCompletionModel(Func<string, CancellationToken, Task<string>>? generateFunc = null)
        {
            _generateFunc = generateFunc ?? ((prompt, ct) => Task.FromResult("POSITION: APPROVE\nRATIONALE: This is a good proposal."));
        }

        public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
            => _generateFunc(prompt, ct);
    }

    private static ToolAwareChatModel CreateMockLlm(Func<string, CancellationToken, Task<string>>? generateFunc = null)
    {
        return new ToolAwareChatModel(new MockChatCompletionModel(generateFunc), new ToolRegistry());
    }

    [Fact]
    public void Constructor_WithLlm_ShouldCreateEmptyCouncil()
    {
        // Arrange & Act
        var orchestrator = new CouncilOrchestrator(CreateMockLlm());

        // Assert
        orchestrator.Agents.Should().BeEmpty();
    }

    [Fact]
    public void CreateWithDefaultAgents_ShouldAddFiveAgents()
    {
        // Act
        var orchestrator = CouncilOrchestrator.CreateWithDefaultAgents(CreateMockLlm());

        // Assert
        orchestrator.Agents.Should().HaveCount(5);
        orchestrator.Agents.Select(a => a.Name).Should().Contain("Optimist");
        orchestrator.Agents.Select(a => a.Name).Should().Contain("SecurityCynic");
        orchestrator.Agents.Select(a => a.Name).Should().Contain("Pragmatist");
        orchestrator.Agents.Select(a => a.Name).Should().Contain("Theorist");
        orchestrator.Agents.Select(a => a.Name).Should().Contain("UserAdvocate");
    }

    [Fact]
    public void AddAgent_ShouldAddToAgentsList()
    {
        // Arrange
        var orchestrator = new CouncilOrchestrator(CreateMockLlm());
        var agent = new OptimistAgent();

        // Act
        orchestrator.AddAgent(agent);

        // Assert
        orchestrator.Agents.Should().HaveCount(1);
        orchestrator.Agents.First().Name.Should().Be("Optimist");
    }

    [Fact]
    public void AddAgent_DuplicateName_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var orchestrator = new CouncilOrchestrator(CreateMockLlm());
        orchestrator.AddAgent(new OptimistAgent());

        // Act
        Action act = () => orchestrator.AddAgent(new OptimistAgent());

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Optimist*already exists*");
    }

    [Fact]
    public void AddAgent_Null_ShouldThrowArgumentNullException()
    {
        // Arrange
        var orchestrator = new CouncilOrchestrator(CreateMockLlm());

        // Act
        Action act = () => orchestrator.AddAgent(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RemoveAgent_ExistingAgent_ShouldReturnTrueAndRemove()
    {
        // Arrange
        var orchestrator = CouncilOrchestrator.CreateWithDefaultAgents(CreateMockLlm());
        var initialCount = orchestrator.Agents.Count;

        // Act
        var result = orchestrator.RemoveAgent("Optimist");

        // Assert
        result.Should().BeTrue();
        orchestrator.Agents.Should().HaveCount(initialCount - 1);
        orchestrator.Agents.Select(a => a.Name).Should().NotContain("Optimist");
    }

    [Fact]
    public void RemoveAgent_NonExistentAgent_ShouldReturnFalse()
    {
        // Arrange
        var orchestrator = new CouncilOrchestrator(CreateMockLlm());

        // Act
        var result = orchestrator.RemoveAgent("NonExistent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ConveneCouncilAsync_NoAgents_ShouldReturnFailure()
    {
        // Arrange
        var orchestrator = new CouncilOrchestrator(CreateMockLlm());
        var topic = CouncilTopic.Simple("Test question");

        // Act
        var result = await orchestrator.ConveneCouncilAsync(topic);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No agents");
    }

    [Fact]
    public async Task ConveneCouncilAsync_WithCancellation_ShouldReturnFailure()
    {
        // Arrange
        var orchestrator = CouncilOrchestrator.CreateWithDefaultAgents(CreateMockLlm());
        var topic = CouncilTopic.Simple("Test question");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await orchestrator.ConveneCouncilAsync(topic, cts.Token);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cancelled");
    }

    [Fact]
    public async Task ConveneCouncilAsync_WithDefaultConfig_ShouldWork()
    {
        // Arrange
        var orchestrator = CouncilOrchestrator.CreateWithDefaultAgents(CreateMockLlm());
        var topic = CouncilTopic.Simple("Should we implement feature X?");

        // Act
        var result = await orchestrator.ConveneCouncilAsync(topic);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Transcript.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ConveneCouncilAsync_ShouldReturnDecisionWithVotes()
    {
        // Arrange
        var orchestrator = CouncilOrchestrator.CreateWithDefaultAgents(CreateMockLlm());
        var topic = CouncilTopic.WithBackground(
            "Should we refactor the authentication module?",
            "The current module is complex and has security concerns.");

        // Act
        var result = await orchestrator.ConveneCouncilAsync(topic);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Votes.Should().NotBeEmpty();
        result.Value.Confidence.Should().BeGreaterThan(0);
    }
}

