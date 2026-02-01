// <copyright file="WorldModelTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Planning;

using FluentAssertions;
using LangChain.DocumentLoaders;
using Ouroboros.Core.Monads;
using Ouroboros.Pipeline.Branches;
using Ouroboros.Pipeline.Planning;
using Ouroboros.Pipeline.WorldModel;
using Ouroboros.Providers;
using Ouroboros.Tools;
using Xunit;

/// <summary>
/// Comprehensive unit tests for WorldState, ToolCapabilityMatcher, CausalGraph, and SmartToolSelector.
/// Tests functional programming patterns, immutability, and monadic operations.
/// </summary>
[Trait("Category", "Unit")]
public sealed class WorldModelTests
{
    #region Test Helpers

    /// <summary>
    /// Test tool implementation for testing purposes.
    /// </summary>
    private sealed class TestTool : ITool
    {
        public string Name { get; }

        public string Description { get; }

        public string? JsonSchema { get; }

        public TestTool(string name, string description, string? jsonSchema = null)
        {
            this.Name = name;
            this.Description = description;
            this.JsonSchema = jsonSchema;
        }

        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            return Task.FromResult(Result<string, string>.Success($"Result from {this.Name}: {input}"));
        }
    }

    /// <summary>
    /// Mock chat completion model for testing.
    /// </summary>
    private sealed class MockChatCompletionModel : IChatCompletionModel
    {
        private readonly Func<string, CancellationToken, Task<string>> generateFunc;

        public MockChatCompletionModel(Func<string, CancellationToken, Task<string>>? generateFunc = null)
        {
            this.generateFunc = generateFunc ?? ((prompt, ct) => Task.FromResult("Mock response"));
        }

        public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
            => this.generateFunc(prompt, ct);
    }

    private static ToolRegistry CreateTestToolRegistry()
    {
        return new ToolRegistry()
            .WithTool(new TestTool("search", "Search the web for information"))
            .WithTool(new TestTool("calculator", "Perform mathematical calculations"))
            .WithTool(new TestTool("file_reader", "Read contents from files"))
            .WithTool(new TestTool("code_analyzer", "Analyze source code for patterns and issues"))
            .WithTool(new TestTool("data_processor", "Process and transform data"));
    }

    private static ToolAwareChatModel CreateMockLlm()
    {
        return new ToolAwareChatModel(new MockChatCompletionModel(), new ToolRegistry());
    }

    private static PipelineBranch CreateTestBranch()
    {
        var store = new TrackedVectorStore();
        var dataSource = DataSource.FromPath(Environment.CurrentDirectory);
        return new PipelineBranch("test-branch", store, dataSource);
    }

    #endregion

    #region WorldState Tests

    /// <summary>
    /// Tests that Empty creates a world state with no observations, capabilities, or constraints.
    /// </summary>
    [Fact]
    public void WorldState_Empty_ShouldCreateStateWithNoContent()
    {
        // Act
        WorldState state = WorldState.Empty();

        // Assert
        state.Should().NotBeNull();
        state.Observations.Should().BeEmpty();
        state.Capabilities.Should().BeEmpty();
        state.Constraints.Should().BeEmpty();
        state.LastUpdated.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Tests that WithObservation adds an observation with confidence.
    /// </summary>
    [Fact]
    public void WorldState_WithObservation_ShouldAddObservationWithConfidence()
    {
        // Arrange
        WorldState state = WorldState.Empty();

        // Act
        WorldState updated = state.WithObservation("temperature", 25.5, 0.9);

        // Assert
        updated.Observations.Should().HaveCount(1);
        updated.Observations.Should().ContainKey("temperature");
        updated.Observations["temperature"].Value.Should().Be(25.5);
        updated.Observations["temperature"].Confidence.Should().Be(0.9);
    }

    /// <summary>
    /// Tests that WithObservation defaults to full confidence when not specified.
    /// </summary>
    [Fact]
    public void WorldState_WithObservation_WithoutConfidence_ShouldDefaultToFullConfidence()
    {
        // Arrange
        WorldState state = WorldState.Empty();

        // Act
        WorldState updated = state.WithObservation("status", "active");

        // Assert
        updated.Observations["status"].Confidence.Should().Be(1.0);
    }

    /// <summary>
    /// Tests that WithoutObservation removes an existing observation.
    /// </summary>
    [Fact]
    public void WorldState_WithoutObservation_ShouldRemoveObservation()
    {
        // Arrange
        WorldState state = WorldState.Empty()
            .WithObservation("key1", "value1")
            .WithObservation("key2", "value2");

        // Act
        WorldState updated = state.WithoutObservation("key1");

        // Assert
        updated.Observations.Should().HaveCount(1);
        updated.Observations.Should().NotContainKey("key1");
        updated.Observations.Should().ContainKey("key2");
    }

    /// <summary>
    /// Tests that WithCapability adds a capability to the state.
    /// </summary>
    [Fact]
    public void WorldState_WithCapability_ShouldAddCapability()
    {
        // Arrange
        WorldState state = WorldState.Empty();
        Capability capability = Capability.Create("web_search", "Search the web for information");

        // Act
        WorldState updated = state.WithCapability(capability);

        // Assert
        updated.Capabilities.Should().HaveCount(1);
        updated.Capabilities[0].Name.Should().Be("web_search");
        updated.Capabilities[0].Description.Should().Be("Search the web for information");
    }

    /// <summary>
    /// Tests that WithoutCapability removes a capability by name.
    /// </summary>
    [Fact]
    public void WorldState_WithoutCapability_ShouldRemoveCapabilityByName()
    {
        // Arrange
        WorldState state = WorldState.Empty()
            .WithCapability(Capability.Create("cap1", "Capability 1"))
            .WithCapability(Capability.Create("cap2", "Capability 2"));

        // Act
        WorldState updated = state.WithoutCapability("cap1");

        // Assert
        updated.Capabilities.Should().HaveCount(1);
        updated.HasCapability("cap1").Should().BeFalse();
        updated.HasCapability("cap2").Should().BeTrue();
    }

    /// <summary>
    /// Tests that WithConstraint adds a constraint to the state.
    /// </summary>
    [Fact]
    public void WorldState_WithConstraint_ShouldAddConstraint()
    {
        // Arrange
        WorldState state = WorldState.Empty();
        Constraint constraint = Constraint.Create("no_external", "Cannot access external resources", 10);

        // Act
        WorldState updated = state.WithConstraint(constraint);

        // Assert
        updated.Constraints.Should().HaveCount(1);
        updated.Constraints[0].Name.Should().Be("no_external");
        updated.Constraints[0].Priority.Should().Be(10);
    }

    /// <summary>
    /// Tests that WithoutConstraint removes a constraint by name.
    /// </summary>
    [Fact]
    public void WorldState_WithoutConstraint_ShouldRemoveConstraintByName()
    {
        // Arrange
        WorldState state = WorldState.Empty()
            .WithConstraint(Constraint.Create("const1", "Rule 1"))
            .WithConstraint(Constraint.Create("const2", "Rule 2"));

        // Act
        WorldState updated = state.WithoutConstraint("const1");

        // Assert
        updated.Constraints.Should().HaveCount(1);
        updated.HasConstraint("const1").Should().BeFalse();
        updated.HasConstraint("const2").Should().BeTrue();
    }

    /// <summary>
    /// Tests that GetObservation returns Option.Some when observation exists.
    /// </summary>
    [Fact]
    public void WorldState_GetObservation_WhenExists_ShouldReturnSome()
    {
        // Arrange
        WorldState state = WorldState.Empty()
            .WithObservation("test_key", "test_value", 0.8);

        // Act
        Option<Observation> result = state.GetObservation("test_key");

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value!.Value.Should().Be("test_value");
        result.Value!.Confidence.Should().Be(0.8);
    }

    /// <summary>
    /// Tests that GetObservation returns Option.None when observation does not exist.
    /// </summary>
    [Fact]
    public void WorldState_GetObservation_WhenNotExists_ShouldReturnNone()
    {
        // Arrange
        WorldState state = WorldState.Empty();

        // Act
        Option<Observation> result = state.GetObservation("nonexistent");

        // Assert
        result.HasValue.Should().BeFalse();
    }

    /// <summary>
    /// Tests that HasCapability returns true when capability exists.
    /// </summary>
    [Fact]
    public void WorldState_HasCapability_WhenExists_ShouldReturnTrue()
    {
        // Arrange
        WorldState state = WorldState.Empty()
            .WithCapability(Capability.Create("test_cap", "Test capability"));

        // Act & Assert
        state.HasCapability("test_cap").Should().BeTrue();
    }

    /// <summary>
    /// Tests that HasCapability returns false when capability does not exist.
    /// </summary>
    [Fact]
    public void WorldState_HasCapability_WhenNotExists_ShouldReturnFalse()
    {
        // Arrange
        WorldState state = WorldState.Empty();

        // Act & Assert
        state.HasCapability("nonexistent").Should().BeFalse();
    }

    /// <summary>
    /// Tests that WorldState operations are immutable.
    /// </summary>
    [Fact]
    public void WorldState_Operations_ShouldBeImmutable()
    {
        // Arrange
        WorldState original = WorldState.Empty();

        // Act
        WorldState withObs = original.WithObservation("key", "value");
        WorldState withCap = original.WithCapability(Capability.Create("cap", "test"));
        WorldState withConst = original.WithConstraint(Constraint.Create("const", "rule"));

        // Assert
        original.Observations.Should().BeEmpty();
        original.Capabilities.Should().BeEmpty();
        original.Constraints.Should().BeEmpty();

        withObs.Observations.Should().HaveCount(1);
        withCap.Capabilities.Should().HaveCount(1);
        withConst.Constraints.Should().HaveCount(1);
    }

    /// <summary>
    /// Tests that Snapshot creates a copy with updated timestamp.
    /// </summary>
    [Fact]
    public void WorldState_Snapshot_ShouldCreateCopyWithUpdatedTimestamp()
    {
        // Arrange
        WorldState state = WorldState.Empty()
            .WithObservation("key", "value");
        DateTime originalTime = state.LastUpdated;

        // Wait a small amount to ensure different timestamp
        Thread.Sleep(10);

        // Act
        WorldState snapshot = state.Snapshot();

        // Assert
        snapshot.Observations.Should().BeEquivalentTo(state.Observations);
        snapshot.LastUpdated.Should().BeOnOrAfter(originalTime);
    }

    #endregion

    #region ToolCapabilityMatcher Tests

    /// <summary>
    /// Tests matching tools for a simple goal description.
    /// </summary>
    [Fact]
    public void ToolCapabilityMatcher_MatchToolsForGoal_ShouldMatchRelevantTools()
    {
        // Arrange
        ToolRegistry registry = CreateTestToolRegistry();
        ToolCapabilityMatcher matcher = new(registry);
        Goal goal = Goal.Atomic("Search the web for AI research papers");

        // Act
        Result<IReadOnlyList<ToolMatch>, string> result = matcher.MatchToolsForGoal(goal);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        result.Value.Should().Contain(m => m.ToolName == "search");
    }

    /// <summary>
    /// Tests scoring tool relevance for a goal description.
    /// </summary>
    [Fact]
    public void ToolCapabilityMatcher_ScoreToolRelevance_ShouldReturnPositiveScore()
    {
        // Arrange
        ToolRegistry registry = CreateTestToolRegistry();
        ToolCapabilityMatcher matcher = new(registry);
        ITool searchTool = registry.GetTool("search").Value!;

        // Act
        double score = matcher.ScoreToolRelevance(searchTool, "Search for information on the web");

        // Assert
        score.Should().BeGreaterThan(0.0);
        score.Should().BeLessThanOrEqualTo(1.0);
    }

    /// <summary>
    /// Tests extracting required capabilities from a description.
    /// </summary>
    [Fact]
    public void ToolCapabilityMatcher_GetRequiredCapabilities_ShouldExtractKeywords()
    {
        // Arrange
        ToolRegistry registry = CreateTestToolRegistry();
        ToolCapabilityMatcher matcher = new(registry);

        // Act
        IReadOnlyList<string> capabilities = matcher.GetRequiredCapabilities("Analyze code and search for patterns");

        // Assert
        capabilities.Should().NotBeEmpty();
        capabilities.Should().Contain(c => c.Contains("analyze") || c.Contains("code") || c.Contains("search") || c.Contains("patterns"));
    }

    /// <summary>
    /// Tests that no matching tools returns empty list.
    /// </summary>
    [Fact]
    public void ToolCapabilityMatcher_MatchToolsForGoal_WithNoMatches_ShouldReturnEmptyList()
    {
        // Arrange
        ToolRegistry registry = new ToolRegistry()
            .WithTool(new TestTool("specific_tool", "Does something very specific"));
        ToolCapabilityMatcher matcher = new(registry);
        Goal goal = Goal.Atomic("Completely unrelated task xyz abc 123");

        // Act
        Result<IReadOnlyList<ToolMatch>, string> result = matcher.MatchToolsForGoal(goal, minScore: 0.9);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    /// <summary>
    /// Tests minimum score filtering.
    /// </summary>
    [Fact]
    public void ToolCapabilityMatcher_MatchToolsForGoal_WithMinScore_ShouldFilterLowScores()
    {
        // Arrange
        ToolRegistry registry = CreateTestToolRegistry();
        ToolCapabilityMatcher matcher = new(registry);
        Goal goal = Goal.Atomic("Search web");

        // Act
        Result<IReadOnlyList<ToolMatch>, string> resultHigh = matcher.MatchToolsForGoal(goal, minScore: 0.9);
        Result<IReadOnlyList<ToolMatch>, string> resultLow = matcher.MatchToolsForGoal(goal, minScore: 0.0);

        // Assert
        resultHigh.Value.Count.Should().BeLessThanOrEqualTo(resultLow.Value.Count);
    }

    /// <summary>
    /// Tests getting the best match for a goal.
    /// </summary>
    [Fact]
    public void ToolCapabilityMatcher_GetBestMatch_ShouldReturnHighestScoringTool()
    {
        // Arrange
        ToolRegistry registry = CreateTestToolRegistry();
        ToolCapabilityMatcher matcher = new(registry);
        Goal goal = Goal.Atomic("Search the web for information");

        // Act
        Option<ToolMatch> result = matcher.GetBestMatch(goal, minScore: 0.0);

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value!.RelevanceScore.Should().BeGreaterThanOrEqualTo(0.0);
    }

    /// <summary>
    /// Tests handling null goal description.
    /// </summary>
    [Fact]
    public void ToolCapabilityMatcher_MatchToolsForGoalDescription_WithNullDescription_ShouldThrow()
    {
        // Arrange
        ToolRegistry registry = CreateTestToolRegistry();
        ToolCapabilityMatcher matcher = new(registry);

        // Act & Assert
        Action act = () => matcher.MatchToolsForGoalDescription(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Tests handling empty goal description.
    /// </summary>
    [Fact]
    public void ToolCapabilityMatcher_MatchToolsForGoalDescription_WithEmptyDescription_ShouldReturnFailure()
    {
        // Arrange
        ToolRegistry registry = CreateTestToolRegistry();
        ToolCapabilityMatcher matcher = new(registry);

        // Act
        Result<IReadOnlyList<ToolMatch>, string> result = matcher.MatchToolsForGoalDescription("   ");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    #endregion

    #region CausalGraph Tests

    /// <summary>
    /// Tests creating an empty causal graph.
    /// </summary>
    [Fact]
    public void CausalGraph_Empty_ShouldCreateGraphWithNoNodesOrEdges()
    {
        // Act
        CausalGraph graph = CausalGraph.Empty();

        // Assert
        graph.Should().NotBeNull();
        graph.NodeCount.Should().Be(0);
        graph.EdgeCount.Should().Be(0);
        graph.Nodes.Should().BeEmpty();
        graph.Edges.Should().BeEmpty();
    }

    /// <summary>
    /// Tests adding nodes to the graph.
    /// </summary>
    [Fact]
    public void CausalGraph_AddNode_ShouldAddNodeToGraph()
    {
        // Arrange
        CausalGraph graph = CausalGraph.Empty();
        CausalNode node = CausalNode.CreateState("initial_state", "The initial system state");

        // Act
        Result<CausalGraph, string> result = graph.AddNode(node);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.NodeCount.Should().Be(1);
        result.Value.GetNode(node.Id).HasValue.Should().BeTrue();
    }

    /// <summary>
    /// Tests adding edges between nodes.
    /// </summary>
    [Fact]
    public void CausalGraph_AddEdge_ShouldConnectNodes()
    {
        // Arrange
        CausalNode node1 = CausalNode.CreateAction("action1", "First action");
        CausalNode node2 = CausalNode.CreateState("state2", "Resulting state");

        CausalGraph graph = CausalGraph.Empty();
        graph = graph.AddNode(node1).Value;
        graph = graph.AddNode(node2).Value;

        CausalEdge edge = CausalEdge.Create(node1.Id, node2.Id, 0.8);

        // Act
        Result<CausalGraph, string> result = graph.AddEdge(edge);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.EdgeCount.Should().Be(1);
    }

    /// <summary>
    /// Tests getting causes of a node.
    /// </summary>
    [Fact]
    public void CausalGraph_GetCauses_ShouldReturnPredecessorNodes()
    {
        // Arrange
        CausalNode cause1 = CausalNode.CreateAction("cause1", "First cause");
        CausalNode cause2 = CausalNode.CreateAction("cause2", "Second cause");
        CausalNode effect = CausalNode.CreateState("effect", "The effect");

        CausalGraph graph = CausalGraph.Empty()
            .AddNode(cause1).Value
            .AddNode(cause2).Value
            .AddNode(effect).Value
            .AddEdge(CausalEdge.Create(cause1.Id, effect.Id, 0.8)).Value
            .AddEdge(CausalEdge.Create(cause2.Id, effect.Id, 0.6)).Value;

        // Act
        IReadOnlyList<CausalNode> causes = graph.GetCauses(effect.Id);

        // Assert
        causes.Should().HaveCount(2);
        causes.Should().Contain(n => n.Id == cause1.Id);
        causes.Should().Contain(n => n.Id == cause2.Id);
    }

    /// <summary>
    /// Tests getting effects of a node.
    /// </summary>
    [Fact]
    public void CausalGraph_GetEffects_ShouldReturnSuccessorNodes()
    {
        // Arrange
        CausalNode cause = CausalNode.CreateAction("cause", "The cause");
        CausalNode effect1 = CausalNode.CreateState("effect1", "First effect");
        CausalNode effect2 = CausalNode.CreateState("effect2", "Second effect");

        CausalGraph graph = CausalGraph.Empty()
            .AddNode(cause).Value
            .AddNode(effect1).Value
            .AddNode(effect2).Value
            .AddEdge(CausalEdge.Create(cause.Id, effect1.Id, 0.9)).Value
            .AddEdge(CausalEdge.Create(cause.Id, effect2.Id, 0.7)).Value;

        // Act
        IReadOnlyList<CausalNode> effects = graph.GetEffects(cause.Id);

        // Assert
        effects.Should().HaveCount(2);
        effects.Should().Contain(n => n.Id == effect1.Id);
        effects.Should().Contain(n => n.Id == effect2.Id);
    }

    /// <summary>
    /// Tests predicting effects from an action.
    /// </summary>
    [Fact]
    public void CausalGraph_PredictEffects_ShouldReturnPredictedEffectsWithProbabilities()
    {
        // Arrange
        CausalNode action = CausalNode.CreateAction("action", "An action");
        CausalNode directEffect = CausalNode.CreateState("direct", "Direct effect");
        CausalNode indirectEffect = CausalNode.CreateState("indirect", "Indirect effect");

        CausalGraph graph = CausalGraph.Empty()
            .AddNode(action).Value
            .AddNode(directEffect).Value
            .AddNode(indirectEffect).Value
            .AddEdge(CausalEdge.Create(action.Id, directEffect.Id, 0.9)).Value
            .AddEdge(CausalEdge.Create(directEffect.Id, indirectEffect.Id, 0.8)).Value;

        // Act
        Result<IReadOnlyList<PredictedEffect>, string> result = graph.PredictEffects(action.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(2);
        result.Value.Should().Contain(e => e.Node.Id == directEffect.Id && e.Probability == 0.9);
        result.Value.Should().Contain(e => e.Node.Id == indirectEffect.Id);
    }

    /// <summary>
    /// Tests finding a path between two nodes.
    /// </summary>
    [Fact]
    public void CausalGraph_FindPath_ShouldReturnPathBetweenNodes()
    {
        // Arrange
        CausalNode nodeA = CausalNode.CreateState("A", "Node A");
        CausalNode nodeB = CausalNode.CreateState("B", "Node B");
        CausalNode nodeC = CausalNode.CreateState("C", "Node C");

        CausalGraph graph = CausalGraph.Empty()
            .AddNode(nodeA).Value
            .AddNode(nodeB).Value
            .AddNode(nodeC).Value
            .AddEdge(CausalEdge.Create(nodeA.Id, nodeB.Id, 0.9)).Value
            .AddEdge(CausalEdge.Create(nodeB.Id, nodeC.Id, 0.8)).Value;

        // Act
        Option<CausalPath> path = graph.FindPath(nodeA.Id, nodeC.Id);

        // Assert
        path.HasValue.Should().BeTrue();
        path.Value!.Nodes.Should().HaveCount(3);
        path.Value!.Length.Should().Be(2);
    }

    /// <summary>
    /// Tests cycle detection in the graph.
    /// </summary>
    [Fact]
    public void CausalGraph_HasCycle_WithCycle_ShouldReturnTrue()
    {
        // Arrange
        CausalNode nodeA = CausalNode.CreateState("A", "Node A");
        CausalNode nodeB = CausalNode.CreateState("B", "Node B");
        CausalNode nodeC = CausalNode.CreateState("C", "Node C");

        CausalGraph graph = CausalGraph.Empty()
            .AddNode(nodeA).Value
            .AddNode(nodeB).Value
            .AddNode(nodeC).Value
            .AddEdge(CausalEdge.Create(nodeA.Id, nodeB.Id, 0.9)).Value
            .AddEdge(CausalEdge.Create(nodeB.Id, nodeC.Id, 0.8)).Value
            .AddEdge(CausalEdge.Create(nodeC.Id, nodeA.Id, 0.7)).Value; // Creates cycle

        // Act
        bool hasCycle = graph.HasCycle();

        // Assert
        hasCycle.Should().BeTrue();
    }

    /// <summary>
    /// Tests cycle detection in a graph without cycles.
    /// </summary>
    [Fact]
    public void CausalGraph_HasCycle_WithoutCycle_ShouldReturnFalse()
    {
        // Arrange
        CausalNode nodeA = CausalNode.CreateState("A", "Node A");
        CausalNode nodeB = CausalNode.CreateState("B", "Node B");
        CausalNode nodeC = CausalNode.CreateState("C", "Node C");

        CausalGraph graph = CausalGraph.Empty()
            .AddNode(nodeA).Value
            .AddNode(nodeB).Value
            .AddNode(nodeC).Value
            .AddEdge(CausalEdge.Create(nodeA.Id, nodeB.Id, 0.9)).Value
            .AddEdge(CausalEdge.Create(nodeB.Id, nodeC.Id, 0.8)).Value;

        // Act
        bool hasCycle = graph.HasCycle();

        // Assert
        hasCycle.Should().BeFalse();
    }

    /// <summary>
    /// Tests creating a subgraph from a subset of nodes.
    /// </summary>
    [Fact]
    public void CausalGraph_CreateSubgraph_ShouldContainOnlySpecifiedNodes()
    {
        // Arrange
        CausalNode nodeA = CausalNode.CreateState("A", "Node A");
        CausalNode nodeB = CausalNode.CreateState("B", "Node B");
        CausalNode nodeC = CausalNode.CreateState("C", "Node C");
        CausalNode nodeD = CausalNode.CreateState("D", "Node D");

        CausalGraph graph = CausalGraph.Empty()
            .AddNode(nodeA).Value
            .AddNode(nodeB).Value
            .AddNode(nodeC).Value
            .AddNode(nodeD).Value
            .AddEdge(CausalEdge.Create(nodeA.Id, nodeB.Id, 0.9)).Value
            .AddEdge(CausalEdge.Create(nodeB.Id, nodeC.Id, 0.8)).Value
            .AddEdge(CausalEdge.Create(nodeC.Id, nodeD.Id, 0.7)).Value;

        // Act
        Result<CausalGraph, string> result = graph.CreateSubgraph(new[] { nodeA.Id, nodeB.Id });

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.NodeCount.Should().Be(2);
        result.Value.EdgeCount.Should().Be(1); // Only edge between A and B
    }

    /// <summary>
    /// Tests removing a node from the graph.
    /// </summary>
    [Fact]
    public void CausalGraph_RemoveNode_ShouldRemoveNodeAndConnectedEdges()
    {
        // Arrange
        CausalNode nodeA = CausalNode.CreateState("A", "Node A");
        CausalNode nodeB = CausalNode.CreateState("B", "Node B");
        CausalNode nodeC = CausalNode.CreateState("C", "Node C");

        CausalGraph graph = CausalGraph.Empty()
            .AddNode(nodeA).Value
            .AddNode(nodeB).Value
            .AddNode(nodeC).Value
            .AddEdge(CausalEdge.Create(nodeA.Id, nodeB.Id, 0.9)).Value
            .AddEdge(CausalEdge.Create(nodeB.Id, nodeC.Id, 0.8)).Value;

        // Act
        Result<CausalGraph, string> result = graph.RemoveNode(nodeB.Id);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.NodeCount.Should().Be(2);
        result.Value.EdgeCount.Should().Be(0); // Both edges connected to B are removed
        result.Value.GetNode(nodeB.Id).HasValue.Should().BeFalse();
    }

    /// <summary>
    /// Tests empty graph handling for operations.
    /// </summary>
    [Fact]
    public void CausalGraph_Operations_OnEmptyGraph_ShouldHandleGracefully()
    {
        // Arrange
        CausalGraph graph = CausalGraph.Empty();
        Guid nonexistentId = Guid.NewGuid();

        // Act & Assert
        graph.GetCauses(nonexistentId).Should().BeEmpty();
        graph.GetEffects(nonexistentId).Should().BeEmpty();
        graph.GetNode(nonexistentId).HasValue.Should().BeFalse();
        graph.FindPath(nonexistentId, Guid.NewGuid()).HasValue.Should().BeFalse();
        graph.HasCycle().Should().BeFalse();
    }

    #endregion

    #region SmartToolSelector Tests

    /// <summary>
    /// Tests selecting tools for a goal.
    /// </summary>
    [Fact]
    public async Task SmartToolSelector_SelectForGoalAsync_ShouldSelectRelevantTools()
    {
        // Arrange
        ToolRegistry registry = CreateTestToolRegistry();
        WorldState worldState = WorldState.Empty();
        ToolCapabilityMatcher matcher = new(registry);
        SmartToolSelector selector = new(worldState, registry, matcher);
        Goal goal = Goal.Atomic("Search the web for information");

        // Act
        Result<ToolSelection, string> result = await selector.SelectForGoalAsync(goal);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.HasTools.Should().BeTrue();
        result.Value.SelectedTools.Should().Contain(t => t.Name == "search");
        result.Value.ConfidenceScore.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Tests that constraints are applied during selection.
    /// </summary>
    [Fact]
    public async Task SmartToolSelector_SelectForGoalAsync_WithConstraints_ShouldApplyConstraints()
    {
        // Arrange
        ToolRegistry registry = CreateTestToolRegistry();
        WorldState worldState = WorldState.Empty()
            .WithConstraint(Constraint.Create("exclude_search", "exclude:search", 10));
        ToolCapabilityMatcher matcher = new(registry);
        SmartToolSelector selector = new(worldState, registry, matcher);
        Goal goal = Goal.Atomic("Search the web for information");

        // Act
        Result<ToolSelection, string> result = await selector.SelectForGoalAsync(goal);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SelectedTools.Should().NotContain(t => t.Name == "search");
    }

    /// <summary>
    /// Tests evaluating tool fit for a goal.
    /// </summary>
    [Fact]
    public void SmartToolSelector_EvaluateToolFit_ShouldReturnCandidate()
    {
        // Arrange
        ToolRegistry registry = CreateTestToolRegistry();
        WorldState worldState = WorldState.Empty();
        ToolCapabilityMatcher matcher = new(registry);
        SmartToolSelector selector = new(worldState, registry, matcher);
        ITool tool = registry.GetTool("calculator").Value!;
        Goal goal = Goal.Atomic("Calculate the sum");

        // Act
        ToolCandidate candidate = selector.EvaluateToolFit(tool, goal, worldState);

        // Assert
        candidate.Should().NotBeNull();
        candidate.Tool.Should().Be(tool);
        candidate.FitScore.Should().BeGreaterThanOrEqualTo(0);
        candidate.FitScore.Should().BeLessThanOrEqualTo(1);
    }

    /// <summary>
    /// Tests creating a step for a goal.
    /// </summary>
    [Fact]
    public async Task SmartToolSelector_CreateStepForGoal_ShouldTransformBranch()
    {
        // Arrange
        ToolRegistry registry = CreateTestToolRegistry();
        WorldState worldState = WorldState.Empty();
        ToolCapabilityMatcher matcher = new(registry);
        SmartToolSelector selector = new(worldState, registry, matcher);
        Goal goal = Goal.Atomic("Process data");
        PipelineBranch branch = CreateTestBranch();

        // Act
        var step = selector.CreateStepForGoal(goal);
        Result<PipelineBranch, string> result = await step(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Events.Should().NotBeEmpty();
    }

    /// <summary>
    /// Tests handling no matching tools scenario.
    /// </summary>
    [Fact]
    public async Task SmartToolSelector_SelectForGoalAsync_WithNoMatchingTools_ShouldReturnEmptySelection()
    {
        // Arrange
        ToolRegistry registry = new ToolRegistry(); // Empty registry
        WorldState worldState = WorldState.Empty();
        ToolCapabilityMatcher matcher = new(registry);
        SmartToolSelector selector = new(worldState, registry, matcher);
        Goal goal = Goal.Atomic("Do something");

        // Act
        Result<ToolSelection, string> result = await selector.SelectForGoalAsync(goal);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.HasTools.Should().BeFalse();
    }

    /// <summary>
    /// Tests confidence threshold configuration.
    /// </summary>
    [Fact]
    public async Task SmartToolSelector_SelectForGoalAsync_WithHighMinConfidence_ShouldFilterLowConfidenceTools()
    {
        // Arrange
        ToolRegistry registry = CreateTestToolRegistry();
        WorldState worldState = WorldState.Empty();
        ToolCapabilityMatcher matcher = new(registry);
        SelectionConfig config = SelectionConfig.Default.WithMinConfidence(0.9);
        SmartToolSelector selector = new(worldState, registry, matcher, config);
        Goal goal = Goal.Atomic("Vague task");

        // Act
        Result<ToolSelection, string> result = await selector.SelectForGoalAsync(goal);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // All selected tools should have high relevance
        foreach (var candidate in result.Value.AllCandidates)
        {
            if (result.Value.SelectedTools.Any(t => t.Name == candidate.Tool.Name))
            {
                candidate.FitScore.Should().BeGreaterThanOrEqualTo(0.9);
            }
        }
    }

    /// <summary>
    /// Tests different optimization strategies.
    /// </summary>
    [Theory]
    [InlineData(OptimizationStrategy.Cost)]
    [InlineData(OptimizationStrategy.Speed)]
    [InlineData(OptimizationStrategy.Quality)]
    [InlineData(OptimizationStrategy.Balanced)]
    public async Task SmartToolSelector_SelectForGoalAsync_WithDifferentStrategies_ShouldSucceed(OptimizationStrategy strategy)
    {
        // Arrange
        ToolRegistry registry = CreateTestToolRegistry();
        WorldState worldState = WorldState.Empty();
        ToolCapabilityMatcher matcher = new(registry);
        SelectionConfig config = new(OptimizeFor: strategy);
        SmartToolSelector selector = new(worldState, registry, matcher, config);
        Goal goal = Goal.Atomic("Search and analyze code");

        // Act
        Result<ToolSelection, string> result = await selector.SelectForGoalAsync(goal);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Reasoning.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Tests that applying constraints filters candidates.
    /// </summary>
    [Fact]
    public void SmartToolSelector_ApplyConstraints_ShouldFilterCandidates()
    {
        // Arrange
        ToolRegistry registry = CreateTestToolRegistry();
        WorldState worldState = WorldState.Empty();
        ToolCapabilityMatcher matcher = new(registry);
        SmartToolSelector selector = new(worldState, registry, matcher);

        ITool searchTool = registry.GetTool("search").Value!;
        ITool calcTool = registry.GetTool("calculator").Value!;

        List<ToolCandidate> candidates = new()
        {
            new ToolCandidate(searchTool, 0.8, 0.3, 0.7, 0.8, new[] { "search", "web" }),
            new ToolCandidate(calcTool, 0.7, 0.2, 0.9, 0.7, new[] { "math", "calculate" }),
        };

        List<Constraint> constraints = new()
        {
            Constraint.Create("exclude_search", "exclude:search"),
        };

        // Act
        List<ToolCandidate> filtered = selector.ApplyConstraints(candidates, constraints);

        // Assert
        filtered.Should().HaveCount(1);
        filtered[0].Tool.Name.Should().Be("calculator");
    }

    #endregion

    #region Integration Tests

    /// <summary>
    /// Tests the full workflow from world state to tool selection.
    /// </summary>
    [Fact]
    public async Task Integration_WorldStateToToolSelection_ShouldWorkEndToEnd()
    {
        // Arrange
        ToolRegistry registry = CreateTestToolRegistry();

        WorldState worldState = WorldState.Empty()
            .WithObservation("task_type", "code_analysis", 0.9)
            .WithCapability(Capability.Create("code_analysis", "Can analyze source code", "code_analyzer"))
            .WithConstraint(Constraint.Create("no_external", "exclude:search"));

        ToolCapabilityMatcher matcher = new(registry);

        // Use a low minimum confidence to ensure tools match
        SelectionConfig config = new(MinConfidence: 0.0);
        SmartToolSelector selector = new(worldState, registry, matcher, config);

        // Use a goal description that matches tools well
        Goal goal = Goal.Atomic("Search the web for information");

        // Verify constraint is set in world state
        worldState.Constraints.Should().Contain(c => c.Name == "no_external");

        // Act
        Result<ToolSelection, string> result = await selector.SelectForGoalAsync(goal);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Search tool should be excluded due to the constraint
        result.Value.SelectedTools.Should().NotContain(t => t.Name == "search");
        // Constraints from world state are applied to selection
        result.Value.AppliedConstraints.Should().BeEquivalentTo(worldState.Constraints);
    }

    /// <summary>
    /// Tests causal reasoning with world state updates.
    /// </summary>
    [Fact]
    public void Integration_CausalGraphWithWorldState_ShouldTrackCausality()
    {
        // Arrange
        CausalNode userRequest = CausalNode.CreateEvent("user_request", "User makes a search request");
        CausalNode toolSelection = CausalNode.CreateAction("tool_selection", "System selects appropriate tools");
        CausalNode toolExecution = CausalNode.CreateAction("tool_execution", "System executes selected tools");
        CausalNode response = CausalNode.CreateState("response", "System provides response to user");

        CausalGraph graph = CausalGraph.Empty()
            .AddNode(userRequest).Value
            .AddNode(toolSelection).Value
            .AddNode(toolExecution).Value
            .AddNode(response).Value
            .AddEdge(CausalEdge.Deterministic(userRequest.Id, toolSelection.Id)).Value
            .AddEdge(CausalEdge.Create(toolSelection.Id, toolExecution.Id, 0.95)).Value
            .AddEdge(CausalEdge.Create(toolExecution.Id, response.Id, 0.9)).Value;

        // Act
        Result<IReadOnlyList<PredictedEffect>, string> effects = graph.PredictEffects(userRequest.Id);

        // Assert
        effects.IsSuccess.Should().BeTrue();
        effects.Value.Should().HaveCount(3);

        // The direct effect should have highest probability
        effects.Value.First().Node.Name.Should().Be("tool_selection");
        effects.Value.First().Probability.Should().Be(1.0);
    }

    #endregion
}
