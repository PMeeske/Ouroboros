// <copyright file="TheoryOfMindTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.TheoryOfMind;

using FluentAssertions;
using Ouroboros.Agent.TheoryOfMind;
using Ouroboros.Core.Learning;
using Ouroboros.Core.Monads;
using Ouroboros.Domain.Embodied;
using Ouroboros.Providers;
using Ouroboros.Tests.Mocks;
using Xunit;

/// <summary>
/// Comprehensive unit tests for Theory of Mind implementation.
/// Tests belief inference, intention prediction, action prediction, and model management.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TheoryOfMindTests
{
    private ITheoryOfMind CreateTheoryOfMind(string defaultResponse = "{}")
    {
        MockChatModel mockLlm = new MockChatModel(defaultResponse);
        return new global::Ouroboros.Agent.TheoryOfMind.TheoryOfMind(mockLlm);
    }

    [Fact]
    public async Task InferBeliefsAsync_WithValidObservations_ShouldReturnBeliefs()
    {
        // Arrange
        string agentId = "agent_1";
        List<AgentObservation> observations = new()
        {
            AgentObservation.Action(agentId, "Moved towards the goal"),
            AgentObservation.Statement(agentId, "I need to reach the destination"),
            AgentObservation.Action(agentId, "Avoided obstacle")
        };

        string mockResponse = @"{
            ""beliefs"": [
                {
                    ""key"": ""knows_goal_location"",
                    ""proposition"": ""Agent knows where the goal is"",
                    ""probability"": 0.9,
                    ""source"": ""inference""
                }
            ],
            ""overall_confidence"": 0.85
        }";

        ITheoryOfMind theoryOfMind = CreateTheoryOfMind(mockResponse);

        // Act
        Result<BeliefState, string> result = await theoryOfMind.InferBeliefsAsync(agentId, observations);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AgentId.Should().Be(agentId);
        result.Value.Beliefs.Should().ContainKey("knows_goal_location");
        result.Value.Confidence.Should().BeApproximately(0.85, 0.01);
    }

    [Fact]
    public async Task InferBeliefsAsync_WithEmptyAgentId_ShouldReturnFailure()
    {
        // Arrange
        ITheoryOfMind theoryOfMind = CreateTheoryOfMind();
        List<AgentObservation> observations = new()
        {
            AgentObservation.Action("agent_1", "test")
        };

        // Act
        Result<BeliefState, string> result = await theoryOfMind.InferBeliefsAsync("", observations);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Agent ID cannot be empty");
    }

    [Fact]
    public async Task InferBeliefsAsync_WithNoObservations_ShouldReturnEmptyBeliefs()
    {
        // Arrange
        string agentId = "agent_1";
        ITheoryOfMind theoryOfMind = CreateTheoryOfMind();
        List<AgentObservation> observations = new();

        // Act
        Result<BeliefState, string> result = await theoryOfMind.InferBeliefsAsync(agentId, observations);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Beliefs.Should().BeEmpty();
        result.Value.Confidence.Should().Be(0.0);
    }

    [Fact]
    public async Task PredictIntentionAsync_WithValidBeliefs_ShouldReturnPrediction()
    {
        // Arrange
        string agentId = "agent_1";
        string mockResponse = @"{
            ""predicted_goal"": ""Complete the assigned task"",
            ""confidence"": 0.75,
            ""supporting_evidence"": [""Stated goal explicitly"", ""Actions align with goal""],
            ""alternative_goals"": [""Explore environment""]
        }";

        ITheoryOfMind theoryOfMind = CreateTheoryOfMind(mockResponse);

        // First, add some observations to build context
        await theoryOfMind.UpdateAgentModelAsync(
            agentId,
            AgentObservation.Action(agentId, "Moving towards goal"));
        await theoryOfMind.UpdateAgentModelAsync(
            agentId,
            AgentObservation.Statement(agentId, "I want to complete the task"));

        BeliefState beliefs = BeliefState.Empty(agentId)
            .WithBelief("goal_oriented", BeliefValue.FromInference("Wants to complete task", 0.8));

        // Act
        Result<IntentionPrediction, string> result = await theoryOfMind.PredictIntentionAsync(agentId, beliefs);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PredictedGoal.Should().Contain("Complete");
        result.Value.Confidence.Should().BeApproximately(0.75, 0.01);
        result.Value.SupportingEvidence.Should().NotBeEmpty();
    }

    [Fact]
    public async Task PredictIntentionAsync_WithInsufficientData_ShouldReturnUnknown()
    {
        // Arrange
        string agentId = "agent_new";
        ITheoryOfMind theoryOfMind = CreateTheoryOfMind();
        BeliefState beliefs = BeliefState.Empty(agentId);

        // Act
        Result<IntentionPrediction, string> result = await theoryOfMind.PredictIntentionAsync(agentId, beliefs);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PredictedGoal.Should().Contain("Unknown");
        result.Value.Confidence.Should().Be(0.0);
    }

    [Fact]
    public async Task PredictNextActionAsync_WithAvailableActions_ShouldReturnPrediction()
    {
        // Arrange
        string agentId = "agent_1";
        string mockResponse = @"{
            ""action_index"": 0,
            ""action_name"": ""MoveForward"",
            ""confidence"": 0.70,
            ""reasoning"": ""Agent has been moving forward consistently""
        }";

        ITheoryOfMind theoryOfMind = CreateTheoryOfMind(mockResponse);

        BeliefState beliefs = BeliefState.Empty(agentId)
            .WithBelief("at_location", BeliefValue.FromObservation("Agent is at starting position", 1.0));

        // Add observations first
        await theoryOfMind.UpdateAgentModelAsync(
            agentId,
            AgentObservation.Action(agentId, "Moved forward"));

        List<EmbodiedAction> availableActions = new()
        {
            EmbodiedAction.Move(new Vector3(1, 0, 0), "MoveForward"),
            EmbodiedAction.Move(new Vector3(0, 0, 1), "MoveRight"),
            EmbodiedAction.NoOp()
        };

        // Act
        Result<ActionPrediction, string> result = await theoryOfMind.PredictNextActionAsync(
            agentId,
            beliefs,
            availableActions);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Confidence.Should().BeApproximately(0.70, 0.01);
        result.Value.PredictedAction.Should().NotBeNull();
        result.Value.Reasoning.Should().NotBeEmpty();
    }

    [Fact]
    public async Task PredictNextActionAsync_WithNoObservationHistory_ShouldReturnNoOp()
    {
        // Arrange
        string agentId = "agent_new";
        ITheoryOfMind theoryOfMind = CreateTheoryOfMind();
        BeliefState beliefs = BeliefState.Empty(agentId);
        List<EmbodiedAction> availableActions = new() { EmbodiedAction.NoOp() };

        // Act
        Result<ActionPrediction, string> result = await theoryOfMind.PredictNextActionAsync(
            agentId,
            beliefs,
            availableActions);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PredictedAction.ActionName.Should().Be("NoOp");
        result.Value.Reasoning.Should().Contain("observation history");
    }

    [Fact]
    public async Task UpdateAgentModelAsync_WithValidObservation_ShouldSucceed()
    {
        // Arrange
        string agentId = "agent_1";
        string mockResponse = @"{
            ""beliefs"": [{
                ""key"": ""test"",
                ""proposition"": ""test proposition"",
                ""probability"": 0.8,
                ""source"": ""inference""
            }],
            ""overall_confidence"": 0.7
        }";

        ITheoryOfMind theoryOfMind = CreateTheoryOfMind(mockResponse);
        AgentObservation observation = AgentObservation.Action(agentId, "Performed action A");

        // Act
        Result<Unit, string> result = await theoryOfMind.UpdateAgentModelAsync(agentId, observation);

        // Assert
        result.IsSuccess.Should().BeTrue();

        AgentModel? model = theoryOfMind.GetAgentModel(agentId);
        model.Should().NotBeNull();
        model!.ObservationHistory.Should().Contain(observation);
    }

    [Fact]
    public async Task UpdateAgentModelAsync_WithNullObservation_ShouldReturnFailure()
    {
        // Arrange
        string agentId = "agent_1";
        ITheoryOfMind theoryOfMind = CreateTheoryOfMind();

        // Act
        Result<Unit, string> result = await theoryOfMind.UpdateAgentModelAsync(agentId, null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Observation cannot be null");
    }

    [Fact]
    public async Task GetAgentModel_WithExistingAgent_ShouldReturnModel()
    {
        // Arrange
        string agentId = "agent_1";
        ITheoryOfMind theoryOfMind = CreateTheoryOfMind(@"{""beliefs"": [], ""overall_confidence"": 0.5}");
        AgentObservation observation = AgentObservation.Action(agentId, "test");

        await theoryOfMind.UpdateAgentModelAsync(agentId, observation);

        // Act
        AgentModel? model = theoryOfMind.GetAgentModel(agentId);

        // Assert
        model.Should().NotBeNull();
        model!.AgentId.Should().Be(agentId);
    }

    [Fact]
    public void GetAgentModel_WithNonExistentAgent_ShouldReturnNull()
    {
        // Arrange
        ITheoryOfMind theoryOfMind = CreateTheoryOfMind();

        // Act
        AgentModel? model = theoryOfMind.GetAgentModel("non_existent");

        // Assert
        model.Should().BeNull();
    }

    [Fact]
    public async Task GetModelConfidenceAsync_WithNewAgent_ShouldReturnLowConfidence()
    {
        // Arrange
        string agentId = "agent_new";
        ITheoryOfMind theoryOfMind = CreateTheoryOfMind();

        // Act
        double confidence = await theoryOfMind.GetModelConfidenceAsync(agentId);

        // Assert
        confidence.Should().Be(0.0);
    }

    [Fact]
    public async Task GetModelConfidenceAsync_WithEstablishedModel_ShouldReturnHigherConfidence()
    {
        // Arrange
        string agentId = "agent_1";
        ITheoryOfMind theoryOfMind = CreateTheoryOfMind(@"{""beliefs"": [], ""overall_confidence"": 0.8}");

        // Add multiple observations to build model confidence
        for (int i = 0; i < 10; i++)
        {
            await theoryOfMind.UpdateAgentModelAsync(
                agentId,
                AgentObservation.Action(agentId, $"Action {i}"));
        }

        // Act
        double confidence = await theoryOfMind.GetModelConfidenceAsync(agentId);

        // Assert
        confidence.Should().BeGreaterThan(0.3);
    }

    [Fact]
    public async Task MultiAgent_Scenario_ShouldMaintainSeparateModels()
    {
        // Arrange
        string agent1 = "agent_1";
        string agent2 = "agent_2";
        ITheoryOfMind theoryOfMind = CreateTheoryOfMind(@"{""beliefs"": [], ""overall_confidence"": 0.5}");

        // Act
        await theoryOfMind.UpdateAgentModelAsync(agent1, AgentObservation.Action(agent1, "Agent 1 action"));
        await theoryOfMind.UpdateAgentModelAsync(agent2, AgentObservation.Action(agent2, "Agent 2 action"));

        // Assert
        AgentModel? model1 = theoryOfMind.GetAgentModel(agent1);
        AgentModel? model2 = theoryOfMind.GetAgentModel(agent2);

        model1.Should().NotBeNull();
        model2.Should().NotBeNull();
        model1!.AgentId.Should().Be(agent1);
        model2!.AgentId.Should().Be(agent2);
        model1.ObservationHistory.Should().HaveCount(1);
        model2.ObservationHistory.Should().HaveCount(1);
    }

    [Fact]
    public void BeliefState_WithBelief_ShouldUpdateCorrectly()
    {
        // Arrange
        BeliefState state = BeliefState.Empty("agent_1");
        BeliefValue belief = BeliefValue.FromInference("test proposition", 0.9);

        // Act
        BeliefState updated = state.WithBelief("test_key", belief);

        // Assert
        updated.Beliefs.Should().ContainKey("test_key");
        updated.Beliefs["test_key"].Probability.Should().Be(0.9);
        updated.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void AgentModel_WithObservation_ShouldAddToHistory()
    {
        // Arrange
        AgentModel model = AgentModel.Create("agent_1");
        AgentObservation observation = AgentObservation.Action("agent_1", "test action");

        // Act
        AgentModel updated = model.WithObservation(observation);

        // Assert
        updated.ObservationHistory.Should().Contain(observation);
        updated.LastInteraction.Should().Be(observation.ObservedAt);
    }

    [Fact]
    public void AgentObservation_FactoryMethods_ShouldCreateCorrectTypes()
    {
        // Arrange & Act
        AgentObservation action = AgentObservation.Action("agent_1", "test action");
        AgentObservation statement = AgentObservation.Statement("agent_1", "test statement");
        AgentObservation stateChange = AgentObservation.StateChange("agent_1", "test state");

        // Assert
        action.ObservationType.Should().Be("action");
        statement.ObservationType.Should().Be("statement");
        stateChange.ObservationType.Should().Be("state_change");
    }

    [Fact]
    public void PersonalityTraits_WithTrait_ShouldUpdateCustomTraits()
    {
        // Arrange
        PersonalityTraits traits = PersonalityTraits.Default();

        // Act
        PersonalityTraits updated = traits.WithTrait("curiosity", 0.8);

        // Assert
        updated.CustomTraits.Should().ContainKey("curiosity");
        updated.CustomTraits["curiosity"].Should().Be(0.8);
    }

    [Fact]
    public void PersonalityTraits_WithTrait_ShouldClampValues()
    {
        // Arrange
        PersonalityTraits traits = PersonalityTraits.Default();

        // Act
        PersonalityTraits tooHigh = traits.WithTrait("test", 1.5);
        PersonalityTraits tooLow = traits.WithTrait("test2", -0.5);

        // Assert
        tooHigh.CustomTraits["test"].Should().Be(1.0);
        tooLow.CustomTraits["test2"].Should().Be(0.0);
    }
}
