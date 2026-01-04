using Ouroboros.Agent.MetaAI;
using Ouroboros.Agent.MetaAI.SelfModel;
using Xunit;

namespace Ouroboros.Tests.Tests.SelfModel;

/// <summary>
/// Tests for IdentityGraph implementation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class IdentityGraphTests
{
    private static IdentityGraph CreateTestIdentityGraph()
    {
        var mockRegistry = new MockCapabilityRegistry();
        return new IdentityGraph(
            Guid.NewGuid(),
            "TestAgent",
            mockRegistry,
            null); // No persistence for tests
    }

    [Fact]
    public async Task GetStateAsync_Should_ReturnCompleteState()
    {
        // Arrange
        IdentityGraph graph = CreateTestIdentityGraph();

        // Act
        AgentIdentityState state = await graph.GetStateAsync();

        // Assert
        Assert.NotNull(state);
        Assert.Equal("TestAgent", state.Name);
        Assert.NotEmpty(state.Resources); // Should have default resources
        Assert.NotNull(state.Performance);
    }

    [Fact]
    public void RegisterResource_Should_AddResource()
    {
        // Arrange
        IdentityGraph graph = CreateTestIdentityGraph();
        var resource = new AgentResource(
            "TestResource",
            "Custom",
            100.0,
            100.0,
            "units",
            DateTime.UtcNow,
            new Dictionary<string, object>());

        // Act
        graph.RegisterResource(resource);
        AgentResource? retrieved = graph.GetResource("TestResource");

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("TestResource", retrieved.Name);
        Assert.Equal(100.0, retrieved.Total);
    }

    [Fact]
    public void CreateCommitment_Should_AddNewCommitment()
    {
        // Arrange
        IdentityGraph graph = CreateTestIdentityGraph();

        // Act
        AgentCommitment commitment = graph.CreateCommitment(
            "Complete task X",
            DateTime.UtcNow.AddHours(24),
            0.9,
            new List<string> { "dep1", "dep2" });

        // Assert
        Assert.NotNull(commitment);
        Assert.Equal("Complete task X", commitment.Description);
        Assert.Equal(0.9, commitment.Priority);
        Assert.Equal(CommitmentStatus.Planned, commitment.Status);
        Assert.Equal(2, commitment.Dependencies.Count);
    }

    [Fact]
    public void UpdateCommitment_Should_ModifyStatus()
    {
        // Arrange
        IdentityGraph graph = CreateTestIdentityGraph();
        AgentCommitment commitment = graph.CreateCommitment(
            "Test commitment",
            DateTime.UtcNow.AddHours(1),
            0.7);

        // Act
        graph.UpdateCommitment(commitment.Id, CommitmentStatus.InProgress, 50.0);
        List<AgentCommitment> active = graph.GetActiveCommitments();

        // Assert
        AgentCommitment? updated = active.FirstOrDefault(c => c.Id == commitment.Id);
        Assert.NotNull(updated);
        Assert.Equal(CommitmentStatus.InProgress, updated.Status);
        Assert.Equal(50.0, updated.ProgressPercent);
    }

    [Fact]
    public void GetActiveCommitments_Should_FilterByStatus()
    {
        // Arrange
        IdentityGraph graph = CreateTestIdentityGraph();
        AgentCommitment c1 = graph.CreateCommitment("C1", DateTime.UtcNow.AddHours(1), 0.5);
        AgentCommitment c2 = graph.CreateCommitment("C2", DateTime.UtcNow.AddHours(2), 0.8);
        graph.UpdateCommitment(c1.Id, CommitmentStatus.InProgress, 30.0);
        graph.UpdateCommitment(c2.Id, CommitmentStatus.Completed, 100.0);

        // Act
        List<AgentCommitment> active = graph.GetActiveCommitments();

        // Assert
        Assert.Single(active);
        Assert.Equal(c1.Id, active[0].Id);
    }

    [Fact]
    public void GetAtRiskCommitments_Should_IdentifyRisks()
    {
        // Arrange
        IdentityGraph graph = CreateTestIdentityGraph();
        
        // Create commitment with near deadline and low progress
        AgentCommitment atRisk = graph.CreateCommitment(
            "At-risk task",
            DateTime.UtcNow.AddHours(2), // Soon deadline
            0.9);
        graph.UpdateCommitment(atRisk.Id, CommitmentStatus.InProgress, 30.0); // Low progress

        // Create healthy commitment
        AgentCommitment healthy = graph.CreateCommitment(
            "Healthy task",
            DateTime.UtcNow.AddDays(7), // Far deadline
            0.5);
        graph.UpdateCommitment(healthy.Id, CommitmentStatus.InProgress, 90.0); // High progress

        // Act
        List<AgentCommitment> atRiskCommitments = graph.GetAtRiskCommitments();

        // Assert
        Assert.Single(atRiskCommitments);
        Assert.Equal(atRisk.Id, atRiskCommitments[0].Id);
    }

    [Fact]
    public void RecordTaskResult_Should_UpdatePerformance()
    {
        // Arrange
        IdentityGraph graph = CreateTestIdentityGraph();
        var result = new ExecutionResult(
            new Plan("Test goal", new List<PlanStep>(), new Dictionary<string, double>(), DateTime.UtcNow),
            new List<StepResult>(),
            true,
            "Success",
            new Dictionary<string, object>(),
            TimeSpan.FromSeconds(5));

        // Act
        graph.RecordTaskResult(result);
        AgentPerformance performance = graph.GetPerformanceSummary(TimeSpan.FromDays(1));

        // Assert
        Assert.Equal(1, performance.TotalTasks);
        Assert.Equal(1, performance.SuccessfulTasks);
        Assert.Equal(0, performance.FailedTasks);
        Assert.True(performance.OverallSuccessRate > 0);
    }

    [Fact]
    public void GetPerformanceSummary_Should_CalculateMetrics()
    {
        // Arrange
        IdentityGraph graph = CreateTestIdentityGraph();
        
        // Record successful and failed tasks
        for (int i = 0; i < 7; i++)
        {
            var result = new ExecutionResult(
                new Plan($"Goal {i}", new List<PlanStep>(), new Dictionary<string, double>(), DateTime.UtcNow),
                new List<StepResult>(),
                i < 5, // 5 successes, 2 failures
                i < 5 ? "Success" : "Failure",
                new Dictionary<string, object>(),
                TimeSpan.FromSeconds(i + 1));
            graph.RecordTaskResult(result);
        }

        // Act
        AgentPerformance performance = graph.GetPerformanceSummary(TimeSpan.FromDays(1));

        // Assert
        Assert.Equal(7, performance.TotalTasks);
        Assert.Equal(5, performance.SuccessfulTasks);
        Assert.Equal(2, performance.FailedTasks);
        Assert.Equal(5.0 / 7.0, performance.OverallSuccessRate, precision: 2);
    }

    // Mock capability registry for testing
    private sealed class MockCapabilityRegistry : ICapabilityRegistry
    {
        public Task<List<AgentCapability>> GetCapabilitiesAsync(CancellationToken ct = default)
        {
            return Task.FromResult(new List<AgentCapability>());
        }

        public Task<bool> CanHandleAsync(string task, Dictionary<string, object>? context = null, CancellationToken ct = default)
        {
            return Task.FromResult(true);
        }

        public AgentCapability? GetCapability(string name) => null;

        public Task UpdateCapabilityAsync(string name, ExecutionResult result, CancellationToken ct = default)
        {
            return Task.CompletedTask;
        }

        public void RegisterCapability(AgentCapability capability) { }

        public Task<List<string>> IdentifyCapabilityGapsAsync(string task, CancellationToken ct = default)
        {
            return Task.FromResult(new List<string>());
        }

        public Task<List<string>> SuggestAlternativesAsync(string task, CancellationToken ct = default)
        {
            return Task.FromResult(new List<string>());
        }
    }
}
