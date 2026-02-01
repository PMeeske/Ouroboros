// <copyright file="ArrowCompositionTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Agent;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Pipeline.Council;
using Ouroboros.Pipeline.Council.Agents;

namespace Ouroboros.Tests.Core;

/// <summary>
/// Tests for arrow-based composition patterns replacing inheritance.
/// </summary>
[Trait("Category", "Unit")]
public class ArrowCompositionTests
{
    [Fact]
    public void OrchestratorArrows_FromSimpleArrow_ShouldCreateWorkingOrchestrator()
    {
        // Arrange
        Step<string, string> arrow = async input => input.ToUpper();
        var orchestrator = OrchestratorArrows.FromSimpleArrow("TestOrchestrator", arrow);

        // Act & Assert
        orchestrator.Should().NotBeNull();
        orchestrator.Configuration.Should().NotBeNull();
    }

    [Fact]
    public async Task OrchestratorArrows_FromSimpleArrow_ShouldExecuteCorrectly()
    {
        // Arrange
        Step<string, string> arrow = async input => input.ToUpper();
        var orchestrator = OrchestratorArrows.FromSimpleArrow("TestOrchestrator", arrow);

        // Act
        var result = await orchestrator.ExecuteAsync("hello world");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("HELLO WORLD");
    }

    [Fact]
    public async Task OrchestratorArrows_FromArrow_WithContext_ShouldWork()
    {
        // Arrange
        Step<(string input, OrchestratorContext context), string> contextArrow = 
            async tuple => $"{tuple.input} - OperationId: {tuple.context.OperationId}";
        
        var orchestrator = OrchestratorArrows.FromArrow("ContextOrchestrator", contextArrow);

        // Act
        var result = await orchestrator.ExecuteAsync("test");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("test");
        result.Output.Should().Contain("OperationId:");
    }

    [Fact]
    public async Task OrchestratorArrows_Compose_ShouldChainOrchestrators()
    {
        // Arrange
        var first = OrchestratorArrows.FromSimpleArrow<string, string>(
            "First", 
            async s => s.ToUpper());
        
        var second = OrchestratorArrows.FromSimpleArrow<string, int>(
            "Second", 
            async s => s.Length);

        // Act
        var composed = OrchestratorArrows.Compose(first, second);
        var result = await composed.ExecuteAsync("hello");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be(5);
    }

    [Fact]
    public async Task OrchestratorArrows_WithRetry_ShouldRetryOnFailure()
    {
        // Arrange
        var attemptCount = 0;
        Step<int, int> flakyArrow = async input =>
        {
            attemptCount++;
            if (attemptCount < 3)
                throw new InvalidOperationException("Temporary failure");
            return input * 2;
        };

        var retryConfig = new RetryConfig
        {
            MaxRetries = 5,
            InitialDelay = TimeSpan.FromMilliseconds(10),
            MaxDelay = TimeSpan.FromMilliseconds(100),
            BackoffMultiplier = 2.0
        };

        var resilientArrow = OrchestratorArrows.WithRetry(flakyArrow, retryConfig);

        // Act
        var result = await resilientArrow(10);

        // Assert
        result.Should().Be(20);
        attemptCount.Should().Be(3);
    }

    [Fact]
    public void AgentPersonaArrows_BuildDefaultProposalPrompt_ShouldIncludeAllElements()
    {
        // Arrange
        var topic = new CouncilTopic(
            "Test Question?",
            "Background info",
            new List<string> { "Constraint 1", "Constraint 2" });
        var systemPrompt = "You are a test agent";

        // Act
        var prompt = AgentPersonaArrows.BuildDefaultProposalPrompt(topic, systemPrompt);

        // Assert
        prompt.Should().Contain("Test Question?");
        prompt.Should().Contain("Background info");
        prompt.Should().Contain("Constraint 1");
        prompt.Should().Contain("Constraint 2");
        prompt.Should().Contain(systemPrompt);
    }

    [Fact]
    public void AgentPersonaArrows_ParseDefaultVoteResponse_ShouldParseCorrectly()
    {
        // Arrange
        var response = """
            POSITION: APPROVE
            RATIONALE: This is a good proposal because it addresses all concerns.
            """;

        // Act
        var vote = AgentPersonaArrows.ParseDefaultVoteResponse("TestAgent", response, 0.8);

        // Assert
        vote.AgentName.Should().Be("TestAgent");
        vote.Position.Should().Contain("APPROVE");
        vote.Weight.Should().Be(0.8);
        vote.Rationale.Should().Contain("good proposal");
    }

    [Fact]
    public void BaseAgentPersona_WithCustomProposalBuilder_ShouldUseCustomFunction()
    {
        // Arrange
        var agent = new TestAgentWithCustomProposal();
        
        // Act
        var promptBuilder = agent.GetProposalPromptBuilder();
        var topic = new CouncilTopic("Question", "Background", new List<string>());
        var prompt = promptBuilder(topic, "System");

        // Assert
        prompt.Should().Contain("CUSTOM PREFIX");
    }

    [Fact]
    public void AgentPersonaArrows_CreateProposalArrow_ShouldAcceptCancellationToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        var topic = new CouncilTopic("Question", "Background", new List<string>());

        // Act - Just verify the arrow can be created with CancellationToken
        // We can't easily test actual cancellation without a real LLM,
        // but we verify the API accepts the token
        var arrowCreation = () =>
        {
            // This would normally use a real LLM, but for unit test we just verify signature
            // The cancellation token is captured in the closure and passed to GenerateWithToolsAsync
            var arrow = AgentPersonaArrows.CreateProposalArrow(
                "TestAgent",
                "System prompt",
                AgentPersonaArrows.BuildDefaultProposalPrompt,
                null!, // Would be real LLM in actual usage
                cts.Token);
            return arrow;
        };

        // Assert - No exception thrown when creating arrow with cancellation token
        arrowCreation.Should().NotThrow();
    }

    // Helper class for testing custom proposal builder
    private class TestAgentWithCustomProposal : BaseAgentPersona
    {
        public override string Name => "TestAgent";
        public override string Description => "Test description";
        public override string SystemPrompt => "Test prompt";

        protected override Func<CouncilTopic, string, string> ProposalPromptBuilder =>
            (topic, systemPrompt) => $"CUSTOM PREFIX: {topic.Question}";

        // Expose for testing
        public Func<CouncilTopic, string, string> GetProposalPromptBuilder() => ProposalPromptBuilder;
    }
}
