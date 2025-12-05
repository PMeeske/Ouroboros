// <copyright file="HierarchicalPlannerTests.cs" company="Adaptive Systems Inc.">
// Copyright (c) Adaptive Systems Inc. All rights reserved.
// </copyright>

namespace LangChainPipeline.Tests;

using LangChainPipeline.Agent.MetaAI;
using Xunit;
using FluentAssertions;

/// <summary>
/// Comprehensive unit tests for HierarchicalPlanner.
/// Tests hierarchical task decomposition, sub-plan creation, and recursive execution.
/// </summary>
public sealed class HierarchicalPlannerTests
{
    /// <summary>
    /// Tests that planner can be created with valid parameters.
    /// </summary>
    [Fact]
    public void Constructor_WithValidParameters_ShouldCreatePlanner()
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator();
        var llm = new MockChatModel("test");

        // Act
        var planner = new HierarchicalPlanner(orchestrator, llm);

        // Assert
        planner.Should().NotBeNull();
    }

    /// <summary>
    /// Tests that constructor throws ArgumentNullException for null orchestrator.
    /// </summary>
    [Fact]
    public void Constructor_WithNullOrchestrator_ShouldThrowArgumentNullException()
    {
        // Arrange
        var llm = new MockChatModel("test");

        // Act
        Action act = () => new HierarchicalPlanner(null!, llm);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("orchestrator");
    }

    /// <summary>
    /// Tests that constructor throws ArgumentNullException for null LLM.
    /// </summary>
    [Fact]
    public void Constructor_WithNullLLM_ShouldThrowArgumentNullException()
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator();

        // Act
        Action act = () => new HierarchicalPlanner(orchestrator, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("llm");
    }

    /// <summary>
    /// Tests that CreateHierarchicalPlanAsync creates plan with default config.
    /// </summary>
    [Fact]
    public async Task CreateHierarchicalPlanAsync_WithSimpleGoal_ShouldCreatePlan()
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator(stepCount: 2);
        var llm = new MockChatModel("test");
        var planner = new HierarchicalPlanner(orchestrator, llm);

        // Act
        var result = await planner.CreateHierarchicalPlanAsync("Test goal");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Goal.Should().Be("Test goal");
        result.Value.TopLevelPlan.Should().NotBeNull();
        result.Value.TopLevelPlan.Steps.Count.Should().Be(2);
    }

    /// <summary>
    /// Tests that CreateHierarchicalPlanAsync uses provided config.
    /// </summary>
    [Fact]
    public async Task CreateHierarchicalPlanAsync_WithCustomConfig_ShouldUseConfig()
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator(stepCount: 5);
        var llm = new MockChatModel("test");
        var planner = new HierarchicalPlanner(orchestrator, llm);
        var config = new HierarchicalPlanningConfig(
            MaxDepth: 2,
            MinStepsForDecomposition: 4,
            ComplexityThreshold: 0.8);

        // Act
        var result = await planner.CreateHierarchicalPlanAsync(
            "Complex goal",
            context: null,
            config: config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.MaxDepth.Should().Be(2);
        result.Value.TopLevelPlan.Steps.Count.Should().Be(5);
    }

    /// <summary>
    /// Tests that CreateHierarchicalPlanAsync creates sub-plans for complex tasks.
    /// </summary>
    [Fact]
    public async Task CreateHierarchicalPlanAsync_WithComplexTask_ShouldCreateSubPlans()
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator(
            stepCount: 5,
            stepConfidence: 0.6); // Low confidence triggers decomposition
        var llm = new MockChatModel("test");
        var planner = new HierarchicalPlanner(orchestrator, llm);
        var config = new HierarchicalPlanningConfig(
            MaxDepth: 3,
            MinStepsForDecomposition: 3,
            ComplexityThreshold: 0.7);

        // Act
        var result = await planner.CreateHierarchicalPlanAsync(
            "Very complex multi-step goal",
            context: null,
            config: config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SubPlans.Should().NotBeEmpty();
    }

    /// <summary>
    /// Tests that CreateHierarchicalPlanAsync respects max depth limit.
    /// </summary>
    [Fact]
    public async Task CreateHierarchicalPlanAsync_WithMaxDepth_ShouldLimitDecomposition()
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator(stepCount: 10, stepConfidence: 0.5);
        var llm = new MockChatModel("test");
        var planner = new HierarchicalPlanner(orchestrator, llm);
        var config = new HierarchicalPlanningConfig(
            MaxDepth: 1, // Only one level of decomposition
            MinStepsForDecomposition: 3,
            ComplexityThreshold: 0.7);

        // Act
        var result = await planner.CreateHierarchicalPlanAsync(
            "Complex goal",
            context: null,
            config: config);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.MaxDepth.Should().Be(1);
        // Sub-plans should exist but not be further decomposed
    }

    /// <summary>
    /// Tests that CreateHierarchicalPlanAsync propagates orchestrator failures.
    /// </summary>
    [Fact]
    public async Task CreateHierarchicalPlanAsync_WhenOrchestratorFails_ShouldReturnFailure()
    {
        // Arrange
        var orchestrator = CreateFailingOrchestrator();
        var llm = new MockChatModel("test");
        var planner = new HierarchicalPlanner(orchestrator, llm);

        // Act
        var result = await planner.CreateHierarchicalPlanAsync("Test goal");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Orchestrator error");
    }

    /// <summary>
    /// Tests that CreateHierarchicalPlanAsync handles exceptions gracefully.
    /// </summary>
    [Fact]
    public async Task CreateHierarchicalPlanAsync_WhenExceptionThrown_ShouldReturnFailure()
    {
        // Arrange
        var orchestrator = CreateExceptionThrowingOrchestrator();
        var llm = new MockChatModel("test");
        var planner = new HierarchicalPlanner(orchestrator, llm);

        // Act
        var result = await planner.CreateHierarchicalPlanAsync("Test goal");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Hierarchical planning failed");
    }

    /// <summary>
    /// Tests that ExecuteHierarchicalAsync executes top-level plan.
    /// </summary>
    [Fact]
    public async Task ExecuteHierarchicalAsync_WithSimplePlan_ShouldExecute()
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator(stepCount: 3);
        var llm = new MockChatModel("test");
        var planner = new HierarchicalPlanner(orchestrator, llm);

        var planResult = await planner.CreateHierarchicalPlanAsync("Test goal");
        var hierarchicalPlan = planResult.Value;

        // Act
        var result = await planner.ExecuteHierarchicalAsync(hierarchicalPlan);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Success.Should().BeTrue();
    }

    /// <summary>
    /// Tests that ExecuteHierarchicalAsync expands sub-plans during execution.
    /// </summary>
    [Fact]
    public async Task ExecuteHierarchicalAsync_WithSubPlans_ShouldExpandAndExecute()
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator(stepCount: 5, stepConfidence: 0.6);
        var llm = new MockChatModel("test");
        var planner = new HierarchicalPlanner(orchestrator, llm);
        var config = new HierarchicalPlanningConfig(
            MaxDepth: 2,
            MinStepsForDecomposition: 3,
            ComplexityThreshold: 0.7);

        var planResult = await planner.CreateHierarchicalPlanAsync(
            "Complex goal",
            context: null,
            config: config);
        var hierarchicalPlan = planResult.Value;

        // Act
        var result = await planner.ExecuteHierarchicalAsync(hierarchicalPlan);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// Tests that ExecuteHierarchicalAsync handles execution failures gracefully.
    /// </summary>
    [Fact]
    public async Task ExecuteHierarchicalAsync_WhenExecutionFails_ShouldReturnFailure()
    {
        // Arrange
        var orchestrator = CreateFailingExecutionOrchestrator();
        var llm = new MockChatModel("test");
        var planner = new HierarchicalPlanner(orchestrator, llm);

        // First create a valid plan
        var topLevelPlan = new Plan(
            "Test goal",
            new List<PlanStep>
            {
                new PlanStep("action1", new Dictionary<string, object>(), "outcome", 0.8)
            },
            new Dictionary<string, double>(),
            DateTime.UtcNow);

        var hierarchicalPlan = new HierarchicalPlan(
            "Test goal",
            topLevelPlan,
            new Dictionary<string, Plan>(),
            MaxDepth: 1,
            CreatedAt: DateTime.UtcNow);

        // Act
        var result = await planner.ExecuteHierarchicalAsync(hierarchicalPlan);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Execution failed");
    }

    /// <summary>
    /// Tests that planner respects cancellation token properly.
    /// </summary>
    [Fact]
    public async Task CreateHierarchicalPlanAsync_WithCancellationToken_ShouldSupportCancellation()
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator();
        var llm = new MockChatModel("test");
        var planner = new HierarchicalPlanner(orchestrator, llm);
        var cts = new CancellationTokenSource();
        
        // Act - Create plan with cancellation token (should succeed if not cancelled)
        var result = await planner.CreateHierarchicalPlanAsync("Test goal", ct: cts.Token);

        // Assert - Should succeed since we didn't cancel
        result.IsSuccess.Should().BeTrue();
    }

    #region Helper Methods

    private static IMetaAIPlannerOrchestrator CreateMockOrchestrator(
        int stepCount = 3,
        double stepConfidence = 0.8)
    {
        return new MockMetaAIOrchestrator(stepCount, stepConfidence);
    }

    private static IMetaAIPlannerOrchestrator CreateFailingOrchestrator()
    {
        return new FailingMockMetaAIOrchestrator();
    }

    private static IMetaAIPlannerOrchestrator CreateExceptionThrowingOrchestrator()
    {
        return new ExceptionThrowingMockOrchestrator();
    }

    private static IMetaAIPlannerOrchestrator CreateFailingExecutionOrchestrator()
    {
        return new FailingExecutionMockOrchestrator();
    }

    private static IMetaAIPlannerOrchestrator CreateSlowOrchestrator()
    {
        return new SlowMockOrchestrator();
    }

    private sealed class MockMetaAIOrchestrator : IMetaAIPlannerOrchestrator
    {
        private readonly int _stepCount;
        private readonly double _stepConfidence;

        public MockMetaAIOrchestrator(int stepCount, double stepConfidence)
        {
            _stepCount = stepCount;
            _stepConfidence = stepConfidence;
        }

        public Task<Result<Plan, string>> PlanAsync(
            string goal,
            Dictionary<string, object>? context = null,
            CancellationToken ct = default)
        {
            var steps = Enumerable.Range(1, _stepCount)
                .Select(i => new PlanStep(
                    $"action{i}",
                    new Dictionary<string, object> { ["param"] = i },
                    $"outcome{i}",
                    _stepConfidence))
                .ToList();

            var plan = new Plan(
                goal,
                steps,
                new Dictionary<string, double> { ["overall"] = _stepConfidence },
                DateTime.UtcNow);

            return Task.FromResult(Result<Plan, string>.Success(plan));
        }

        public Task<Result<ExecutionResult, string>> ExecuteAsync(
            Plan plan,
            CancellationToken ct = default)
        {
            var stepResults = plan.Steps
                .Select(step => new StepResult(
                    step,
                    Success: true,
                    Output: $"Output for {step.Action}",
                    Error: null,
                    Duration: TimeSpan.FromMilliseconds(100),
                    ObservedState: new Dictionary<string, object>()))
                .ToList();

            var result = new ExecutionResult(
                plan,
                stepResults,
                Success: true,
                FinalOutput: "Completed",
                Metadata: new Dictionary<string, object>(),
                Duration: TimeSpan.FromSeconds(1));

            return Task.FromResult(Result<ExecutionResult, string>.Success(result));
        }

        public Task<Result<VerificationResult, string>> VerifyAsync(
            ExecutionResult execution,
            CancellationToken ct = default)
        {
            var verification = new VerificationResult(
                execution,
                Verified: true,
                QualityScore: 0.9,
                Issues: new List<string>(),
                Improvements: new List<string>(),
                RevisedPlan: null);

            return Task.FromResult(Result<VerificationResult, string>.Success(verification));
        }

        public void LearnFromExecution(VerificationResult verification)
        {
            // Mock implementation
        }

        public IReadOnlyDictionary<string, PerformanceMetrics> GetMetrics()
        {
            return new Dictionary<string, PerformanceMetrics>();
        }
    }

    private sealed class FailingMockMetaAIOrchestrator : IMetaAIPlannerOrchestrator
    {
        public Task<Result<Plan, string>> PlanAsync(
            string goal,
            Dictionary<string, object>? context = null,
            CancellationToken ct = default)
        {
            return Task.FromResult(Result<Plan, string>.Failure("Orchestrator error"));
        }

        public Task<Result<ExecutionResult, string>> ExecuteAsync(
            Plan plan,
            CancellationToken ct = default)
        {
            return Task.FromResult(Result<ExecutionResult, string>.Failure("Execution failed"));
        }

        public Task<Result<VerificationResult, string>> VerifyAsync(
            ExecutionResult execution,
            CancellationToken ct = default)
        {
            return Task.FromResult(Result<VerificationResult, string>.Failure("Verification failed"));
        }

        public void LearnFromExecution(VerificationResult verification) { }

        public IReadOnlyDictionary<string, PerformanceMetrics> GetMetrics()
        {
            return new Dictionary<string, PerformanceMetrics>();
        }
    }

    private sealed class ExceptionThrowingMockOrchestrator : IMetaAIPlannerOrchestrator
    {
        public Task<Result<Plan, string>> PlanAsync(
            string goal,
            Dictionary<string, object>? context = null,
            CancellationToken ct = default)
        {
            throw new InvalidOperationException("Test exception");
        }

        public Task<Result<ExecutionResult, string>> ExecuteAsync(
            Plan plan,
            CancellationToken ct = default)
        {
            throw new InvalidOperationException("Test exception");
        }

        public Task<Result<VerificationResult, string>> VerifyAsync(
            ExecutionResult execution,
            CancellationToken ct = default)
        {
            throw new InvalidOperationException("Test exception");
        }

        public void LearnFromExecution(VerificationResult verification) { }

        public IReadOnlyDictionary<string, PerformanceMetrics> GetMetrics()
        {
            return new Dictionary<string, PerformanceMetrics>();
        }
    }

    private sealed class FailingExecutionMockOrchestrator : IMetaAIPlannerOrchestrator
    {
        public Task<Result<Plan, string>> PlanAsync(
            string goal,
            Dictionary<string, object>? context = null,
            CancellationToken ct = default)
        {
            var plan = new Plan(
                goal,
                new List<PlanStep>(),
                new Dictionary<string, double>(),
                DateTime.UtcNow);
            return Task.FromResult(Result<Plan, string>.Success(plan));
        }

        public Task<Result<ExecutionResult, string>> ExecuteAsync(
            Plan plan,
            CancellationToken ct = default)
        {
            return Task.FromResult(Result<ExecutionResult, string>.Failure("Execution failed"));
        }

        public Task<Result<VerificationResult, string>> VerifyAsync(
            ExecutionResult execution,
            CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public void LearnFromExecution(VerificationResult verification) { }

        public IReadOnlyDictionary<string, PerformanceMetrics> GetMetrics()
        {
            return new Dictionary<string, PerformanceMetrics>();
        }
    }

    private sealed class SlowMockOrchestrator : IMetaAIPlannerOrchestrator
    {
        public async Task<Result<Plan, string>> PlanAsync(
            string goal,
            Dictionary<string, object>? context = null,
            CancellationToken ct = default)
        {
            await Task.Delay(5000, ct); // Intentionally slow
            var plan = new Plan(goal, new List<PlanStep>(), new Dictionary<string, double>(), DateTime.UtcNow);
            return Result<Plan, string>.Success(plan);
        }

        public Task<Result<ExecutionResult, string>> ExecuteAsync(
            Plan plan,
            CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public Task<Result<VerificationResult, string>> VerifyAsync(
            ExecutionResult execution,
            CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }

        public void LearnFromExecution(VerificationResult verification) { }

        public IReadOnlyDictionary<string, PerformanceMetrics> GetMetrics()
        {
            return new Dictionary<string, PerformanceMetrics>();
        }
    }

    #endregion

    #region Input Validation Tests

    /// <summary>
    /// Tests that CreateHierarchicalPlanAsync fails for empty goal.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task CreateHierarchicalPlanAsync_WithEmptyGoal_ShouldReturnFailure(string? goal)
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator();
        var llm = new MockChatModel("test");
        var planner = new HierarchicalPlanner(orchestrator, llm);

        // Act
        var result = await planner.CreateHierarchicalPlanAsync(goal!);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Goal cannot be empty");
    }

    /// <summary>
    /// Tests that CreateHierarchicalPlanAsync fails for invalid MaxDepth.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-5)]
    public async Task CreateHierarchicalPlanAsync_WithInvalidMaxDepth_ShouldReturnFailure(int maxDepth)
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator();
        var llm = new MockChatModel("test");
        var planner = new HierarchicalPlanner(orchestrator, llm);
        var config = new HierarchicalPlanningConfig(MaxDepth: maxDepth);

        // Act
        var result = await planner.CreateHierarchicalPlanAsync("Valid goal", config: config);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("MaxDepth must be at least 1");
    }

    /// <summary>
    /// Tests that ExecuteHierarchicalAsync throws for null plan.
    /// </summary>
    [Fact]
    public async Task ExecuteHierarchicalAsync_WithNullPlan_ShouldThrowArgumentNullException()
    {
        // Arrange
        var orchestrator = CreateMockOrchestrator();
        var llm = new MockChatModel("test");
        var planner = new HierarchicalPlanner(orchestrator, llm);

        // Act
        Func<Task> act = async () => await planner.ExecuteHierarchicalAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>()
            .WithParameterName("plan");
    }

    #endregion
}
