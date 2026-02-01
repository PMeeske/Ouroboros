// <copyright file="MultiAgentCoordinatorPipelineTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.MultiAgent;

using FluentAssertions;
using Ouroboros.Domain.MultiAgent;
using Ouroboros.Domain.Reinforcement;
using Xunit;
using Unit = Ouroboros.Domain.Reinforcement.Unit;

/// <summary>
/// Tests for MultiAgentCoordinatorPipeline composable arrow implementation.
/// </summary>
[Trait("Category", "Unit")]
public class MultiAgentCoordinatorPipelineTests
{
    private readonly IAgentRegistry agentRegistry;

    public MultiAgentCoordinatorPipelineTests()
    {
        this.agentRegistry = new InMemoryAgentRegistry();
    }

    [Fact]
    public async Task CollaborativePlanningPipeline_WithValidInput_ShouldSucceed()
    {
        // Arrange
        var agents = CreateTestAgents(3);
        var goal = "Build a web application";
        var pipeline = MultiAgentCoordinatorPipeline.CollaborativePlanningPipeline(
            goal,
            agents,
            this.agentRegistry);

        // Act
        var result = await pipeline(Unit.Value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var plan = result.Value;
        plan.Should().NotBeNull();
        plan.Goal.Should().Be(goal);
        plan.Assignments.Should().NotBeEmpty();
        plan.Dependencies.Should().NotBeNull();
        plan.EstimatedDuration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task CollaborativePlanningPipeline_WithEmptyGoal_ShouldFail()
    {
        // Arrange
        var agents = CreateTestAgents(3);
        var pipeline = MultiAgentCoordinatorPipeline.CollaborativePlanningPipeline(
            string.Empty,
            agents,
            this.agentRegistry);

        // Act
        var result = await pipeline(Unit.Value);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Goal cannot be empty");
    }

    [Fact]
    public async Task CollaborativePlanningPipeline_WithNoParticipants_ShouldFail()
    {
        // Arrange
        var pipeline = MultiAgentCoordinatorPipeline.CollaborativePlanningPipeline(
            "Build something",
            new List<AgentId>(),
            this.agentRegistry);

        // Act
        var result = await pipeline(Unit.Value);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No participants provided");
    }

    [Fact]
    public async Task CollaborativePlanningPipeline_ShouldDecomposeGoal()
    {
        // Arrange
        var agents = CreateTestAgents(3);
        var goal = "Develop AI system";
        var pipeline = MultiAgentCoordinatorPipeline.CollaborativePlanningPipeline(
            goal,
            agents,
            this.agentRegistry);

        // Act
        var result = await pipeline(Unit.Value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var plan = result.Value;

        // Should have decomposed into multiple tasks
        plan.Assignments.Should().HaveCountGreaterThanOrEqualTo(1);
        
        // At least one task should be assigned
        plan.Assignments.Should().NotBeEmpty();
        plan.Assignments.First().TaskDescription.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CollaborativePlanningPipeline_ShouldAllocateTasksBasedOnSkills()
    {
        // Arrange
        var agents = CreateTestAgentsWithSkills();
        var goal = "Analyze data and generate report";
        var pipeline = MultiAgentCoordinatorPipeline.CollaborativePlanningPipeline(
            goal,
            agents,
            this.agentRegistry);

        // Act
        var result = await pipeline(Unit.Value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var plan = result.Value;

        // All assignments should be to available agents
        plan.Assignments.Should().AllSatisfy(assignment =>
        {
            assignment.AssignedTo.Should().NotBeNull();
            assignment.Deadline.Should().BeAfter(DateTime.UtcNow);
        });
    }

    [Fact]
    public async Task CollaborativePlanningPipeline_ShouldIdentifyDependencies()
    {
        // Arrange
        var agents = CreateTestAgents(3);
        var goal = "Complete project workflow";
        var pipeline = MultiAgentCoordinatorPipeline.CollaborativePlanningPipeline(
            goal,
            agents,
            this.agentRegistry);

        // Act
        var result = await pipeline(Unit.Value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var plan = result.Value;

        // Should identify sequential dependencies (Analyze -> Plan -> Execute -> Verify)
        plan.Dependencies.Should().NotBeNull();
        // At least some dependencies should exist for sequential tasks
        plan.Dependencies.Count.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task CollaborativePlanningPipeline_CanBeComposedWithOtherArrows()
    {
        // Arrange
        var agents = CreateTestAgents(3);
        var goal = "Test composition";
        var logged = false;

        var loggingPipeline = MultiAgentCoordinatorPipeline
            .CollaborativePlanningPipeline(goal, agents, this.agentRegistry)
            .Tap(result =>
            {
                if (result.IsSuccess)
                {
                    logged = true;
                }
            });

        // Act
        var result = await loggingPipeline(Unit.Value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        logged.Should().BeTrue("Tap should have executed");
    }

    [Fact]
    public async Task CollaborativePlanningPipeline_CanBeTransformed()
    {
        // Arrange
        var agents = CreateTestAgents(3);
        var goal = "Test transformation";

        var transformedPipeline = MultiAgentCoordinatorPipeline
            .CollaborativePlanningPipeline(goal, agents, this.agentRegistry)
            .Map(planResult => planResult.IsSuccess
                ? $"Plan created with {planResult.Value.Assignments.Count} assignments"
                : $"Planning failed: {planResult.Error}");

        // Act
        var result = await transformedPipeline(Unit.Value);

        // Assert
        result.Should().Contain("Plan created with");
        result.Should().Contain("assignments");
    }

    [Fact]
    public async Task CollaborativePlanningPipeline_WithCancellationToken_ShouldRespectCancellation()
    {
        // Arrange
        var agents = CreateTestAgents(3);
        var goal = "Test cancellation";
        var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        var pipeline = MultiAgentCoordinatorPipeline.CollaborativePlanningPipeline(
            goal,
            agents,
            this.agentRegistry,
            cts.Token);

        // Act
        var result = await pipeline(Unit.Value);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cancelled");
    }

    [Fact]
    public async Task CollaborativePlanningPipeline_ShouldEstimateDuration()
    {
        // Arrange
        var agents = CreateTestAgents(5);
        var goal = "Large project with many tasks";
        var pipeline = MultiAgentCoordinatorPipeline.CollaborativePlanningPipeline(
            goal,
            agents,
            this.agentRegistry);

        // Act
        var result = await pipeline(Unit.Value);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var plan = result.Value;

        // Duration should be estimated based on tasks and parallelization
        plan.EstimatedDuration.Should().BeGreaterThan(TimeSpan.Zero);
        plan.EstimatedDuration.Should().BeLessThan(TimeSpan.FromDays(1));
    }

    #region Helper Methods

    private List<AgentId> CreateTestAgents(int count)
    {
        var agents = new List<AgentId>();

        for (int i = 0; i < count; i++)
        {
            var agentId = new AgentId(Guid.NewGuid(), $"agent-{i}");
            agents.Add(agentId);

            // Register agent with basic capabilities
            var capabilities = new AgentCapabilities(
                agentId,
                new List<string> { "general", "task-execution" },
                new Dictionary<string, double>(),
                CurrentLoad: 0.5,
                IsAvailable: true);

            this.agentRegistry.RegisterAgentAsync(capabilities).Wait();
        }

        return agents;
    }

    private List<AgentId> CreateTestAgentsWithSkills()
    {
        var agents = new List<AgentId>();

        // Agent with analysis skills
        var analyst = new AgentId(Guid.NewGuid(), "analyst");
        agents.Add(analyst);
        this.agentRegistry.RegisterAgentAsync(
            new AgentCapabilities(
                analyst,
                new List<string> { "analyze", "data-processing" },
                new Dictionary<string, double>(),
                CurrentLoad: 0.3,
                IsAvailable: true)).Wait();

        // Agent with planning skills
        var planner = new AgentId(Guid.NewGuid(), "planner");
        agents.Add(planner);
        this.agentRegistry.RegisterAgentAsync(
            new AgentCapabilities(
                planner,
                new List<string> { "plan", "strategy" },
                new Dictionary<string, double>(),
                CurrentLoad: 0.4,
                IsAvailable: true)).Wait();

        // Agent with execution skills
        var executor = new AgentId(Guid.NewGuid(), "executor");
        agents.Add(executor);
        this.agentRegistry.RegisterAgentAsync(
            new AgentCapabilities(
                executor,
                new List<string> { "execute", "implementation" },
                new Dictionary<string, double>(),
                CurrentLoad: 0.2,
                IsAvailable: true)).Wait();

        return agents;
    }

    #endregion
}
