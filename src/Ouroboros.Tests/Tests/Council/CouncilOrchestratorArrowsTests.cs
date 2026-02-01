// <copyright file="CouncilOrchestratorArrowsTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Pipeline.Council;
using Ouroboros.Pipeline.Council.Agents;

namespace Ouroboros.Tests.Council;

/// <summary>
/// Tests for CouncilOrchestratorArrows arrow parameterization pattern.
/// </summary>
[Trait("Category", "Unit")]
public class CouncilOrchestratorArrowsTests
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

    private static PipelineBranch CreateTestBranch()
    {
        var store = new TrackedVectorStore();
        var dataSource = LangChain.DocumentLoaders.DataSource.FromPath(Environment.CurrentDirectory);
        return new PipelineBranch("test", store, dataSource);
    }

    [Fact]
    public async Task ConveneCouncilArrow_WithAgents_ShouldExecuteDebate()
    {
        // Arrange
        var llm = CreateMockLlm();
        var agents = new List<IAgentPersona>
        {
            new OptimistAgent(),
            new SecurityCynicAgent(),
            new PragmatistAgent()
        };
        var topic = CouncilTopic.Simple("Should we implement feature X?");
        var branch = CreateTestBranch();

        // Act
        var arrow = CouncilOrchestratorArrows.ConveneCouncilArrow(llm, agents, topic);
        var result = await arrow(branch);

        // Assert
        result.Should().NotBeNull();
        result.Events.Should().NotBeEmpty();
        var councilEvent = result.Events.OfType<CouncilDecisionEvent>().FirstOrDefault();
        councilEvent.Should().NotBeNull();
    }

    [Fact]
    public async Task ConveneCouncilArrow_WithNoAgents_ShouldReturnFailure()
    {
        // Arrange
        var llm = CreateMockLlm();
        var agents = new List<IAgentPersona>();
        var topic = CouncilTopic.Simple("Test question");
        var branch = CreateTestBranch();

        // Act
        var arrow = CouncilOrchestratorArrows.ConveneCouncilArrow(llm, agents, topic);
        var result = await arrow(branch);

        // Assert
        result.Should().NotBeNull();
        var councilEvent = result.Events.OfType<CouncilDecisionEvent>().FirstOrDefault();
        councilEvent.Should().NotBeNull();
        councilEvent!.Decision.Conclusion.Should().Contain("No agents");
    }

    [Fact]
    public async Task ConveneCouncilWithDefaultAgentsArrow_ShouldUseDefaultAgents()
    {
        // Arrange
        var llm = CreateMockLlm();
        var topic = CouncilTopic.Simple("Should we refactor the authentication?");
        var branch = CreateTestBranch();

        // Act
        var arrow = CouncilOrchestratorArrows.ConveneCouncilWithDefaultAgentsArrow(llm, topic);
        var result = await arrow(branch);

        // Assert
        result.Should().NotBeNull();
        result.Events.Should().NotBeEmpty();
        var councilEvent = result.Events.OfType<CouncilDecisionEvent>().FirstOrDefault();
        councilEvent.Should().NotBeNull();
    }

    [Fact]
    public async Task SafeConveneCouncilArrow_WithAgents_ShouldReturnSuccess()
    {
        // Arrange
        var llm = CreateMockLlm();
        var agents = new List<IAgentPersona>
        {
            new OptimistAgent(),
            new PragmatistAgent()
        };
        var topic = CouncilTopic.Simple("Test question");
        var branch = CreateTestBranch();

        // Act
        var arrow = CouncilOrchestratorArrows.SafeConveneCouncilArrow(llm, agents, topic);
        var result = await arrow(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Events.Should().NotBeEmpty();
    }

    [Fact]
    public async Task SafeConveneCouncilArrow_WithNoAgents_ShouldReturnFailure()
    {
        // Arrange
        var llm = CreateMockLlm();
        var agents = new List<IAgentPersona>();
        var topic = CouncilTopic.Simple("Test question");
        var branch = CreateTestBranch();

        // Act
        var arrow = CouncilOrchestratorArrows.SafeConveneCouncilArrow(llm, agents, topic);
        var result = await arrow(branch);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No agents");
    }

    [Fact]
    public async Task DynamicConveneCouncilArrow_ShouldBuildTopicFromBranch()
    {
        // Arrange
        var llm = CreateMockLlm();
        var agents = new List<IAgentPersona>
        {
            new OptimistAgent(),
            new PragmatistAgent()
        };
        var branch = CreateTestBranch();

        // Dynamic topic builder that uses branch state
        Func<PipelineBranch, CouncilTopic> topicBuilder = b =>
            CouncilTopic.Simple($"Should we proceed with branch {b.Name}?");

        // Act
        var arrow = CouncilOrchestratorArrows.DynamicConveneCouncilArrow(llm, agents, topicBuilder);
        var result = await arrow(branch);

        // Assert
        result.Should().NotBeNull();
        var councilEvent = result.Events.OfType<CouncilDecisionEvent>().FirstOrDefault();
        councilEvent.Should().NotBeNull();
        councilEvent!.Topic.Question.Should().Contain("branch test");
    }

    [Fact]
    public void CreateConfiguredCouncil_ShouldReturnFactory()
    {
        // Arrange
        var llm = CreateMockLlm();
        var config = CouncilConfig.Default;

        // Act
        var factory = CouncilOrchestratorArrows.CreateConfiguredCouncil(llm, config);

        // Assert
        factory.Should().NotBeNull();

        // Test that the factory can create arrows
        var topic = CouncilTopic.Simple("Test");
        var arrow = factory(topic);
        arrow.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateConfiguredCouncil_CreatedArrow_ShouldWork()
    {
        // Arrange
        var llm = CreateMockLlm();
        var factory = CouncilOrchestratorArrows.CreateConfiguredCouncil(llm);
        var branch = CreateTestBranch();
        var topic = CouncilTopic.Simple("Should we deploy to production?");

        // Act
        var arrow = factory(topic);
        var result = await arrow(branch);

        // Assert
        result.Should().NotBeNull();
        result.Events.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ConveneCouncilArrow_WithCustomConfig_ShouldUseConfig()
    {
        // Arrange
        var llm = CreateMockLlm();
        var agents = new List<IAgentPersona> { new OptimistAgent() };
        var topic = CouncilTopic.Simple("Test");
        var branch = CreateTestBranch();
        var customConfig = new CouncilConfig(
            ConsensusThreshold: 0.9,
            EnableMinorityReport: true,
            MaxRoundsPerPhase: 5);

        // Act
        var arrow = CouncilOrchestratorArrows.ConveneCouncilArrow(llm, agents, topic, customConfig);
        var result = await arrow(branch);

        // Assert
        result.Should().NotBeNull();
        result.Events.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ArrowComposition_MultipleCouncilArrows_ShouldCompose()
    {
        // Arrange
        var llm = CreateMockLlm();
        var agents = new List<IAgentPersona> { new OptimistAgent(), new PragmatistAgent() };
        var branch = CreateTestBranch();

        var topic1 = CouncilTopic.Simple("First decision");
        var topic2 = CouncilTopic.Simple("Second decision");

        // Act - Compose two council arrows
        var arrow1 = CouncilOrchestratorArrows.ConveneCouncilArrow(llm, agents, topic1);
        var arrow2 = CouncilOrchestratorArrows.ConveneCouncilArrow(llm, agents, topic2);

        var result1 = await arrow1(branch);
        var result2 = await arrow2(result1);

        // Assert
        result2.Events.OfType<CouncilDecisionEvent>().Should().HaveCount(2);
    }
}
