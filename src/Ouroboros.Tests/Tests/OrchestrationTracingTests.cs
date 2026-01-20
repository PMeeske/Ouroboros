// ==========================================================
// Orchestration Tracing Tests
// Tests for the observability layer
// ==========================================================

using System.Diagnostics;
using FluentAssertions;
using Ouroboros.Agent;
using Ouroboros.Agent.MetaAI;
using Xunit;

namespace Ouroboros.Tests;

[Trait("Category", "Unit")]
public sealed class OrchestrationTracingTests : IDisposable
{
    private readonly List<Activity> _startedActivities = new();
    private readonly List<Activity> _stoppedActivities = new();
    private ActivityListener? _listener;

    public OrchestrationTracingTests()
    {
        // Setup listener to capture activities
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Ouroboros.Orchestration",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity => _startedActivities.Add(activity),
            ActivityStopped = activity => _stoppedActivities.Add(activity),
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener?.Dispose();
    }

    [Fact]
    public void StartModelSelection_ShouldCreateActivity()
    {
        // Act
        using var activity = OrchestrationTracing.StartModelSelection("test prompt", "context");

        // Assert
        activity.Should().NotBeNull();
        activity!.OperationName.Should().Be("orchestrator.select_model");
        activity.Tags.Should().Contain(t => t.Key == "orchestrator.operation" && t.Value == "model_selection");
        activity.Tags.Should().Contain(t => t.Key == "orchestrator.prompt_length");
    }

    [Fact]
    public void CompleteModelSelection_ShouldSetTagsAndStatus()
    {
        // Arrange
        using var activity = OrchestrationTracing.StartModelSelection("test prompt");

        // Act
        OrchestrationTracing.CompleteModelSelection(
            activity,
            "gpt-4",
            UseCaseType.CodeGeneration,
            0.85,
            TimeSpan.FromMilliseconds(100));

        // Assert
        activity.Should().NotBeNull();
        activity!.Tags.Should().Contain(t => t.Key == "orchestrator.selected_model" && t.Value == "gpt-4");
        activity.Tags.Should().Contain(t => t.Key == "orchestrator.use_case" && t.Value == "CodeGeneration");
        activity.Status.Should().Be(ActivityStatusCode.Ok);
    }

    [Fact]
    public void StartRouting_ShouldCreateRoutingActivity()
    {
        // Act
        using var activity = OrchestrationTracing.StartRouting("analyze this code");

        // Assert
        activity.Should().NotBeNull();
        activity!.OperationName.Should().Be("orchestrator.route");
        activity.Tags.Should().Contain(t => t.Key == "orchestrator.operation" && t.Value == "routing");
    }

    [Fact]
    public void CompleteRouting_WithFallback_ShouldRecordFallbackInfo()
    {
        // Arrange
        using var activity = OrchestrationTracing.StartRouting("test task");

        // Act
        OrchestrationTracing.CompleteRouting(
            activity,
            "fallback",
            0.5,
            usedFallback: true,
            TimeSpan.FromMilliseconds(50));

        // Assert
        activity.Should().NotBeNull();
        activity!.Tags.Should().Contain(t => t.Key == "orchestrator.used_fallback" && t.Value == "True");
        activity.Tags.Should().Contain(t => t.Key == "orchestrator.route_type" && t.Value == "fallback");
    }

    [Fact]
    public void StartPlanCreation_ShouldIncludeGoalAndDepth()
    {
        // Act
        using var activity = OrchestrationTracing.StartPlanCreation("build a web app", 3);

        // Assert
        activity.Should().NotBeNull();
        activity!.OperationName.Should().Be("orchestrator.create_plan");
        activity.Tags.Should().Contain(t => t.Key == "orchestrator.max_depth" && t.Value == "3");
    }

    [Fact]
    public void CompletePlanCreation_ShouldRecordStepCount()
    {
        // Arrange
        using var activity = OrchestrationTracing.StartPlanCreation("test goal", 2);

        // Act
        OrchestrationTracing.CompletePlanCreation(activity, stepCount: 5, depth: 2, TimeSpan.FromMilliseconds(200));

        // Assert
        activity.Should().NotBeNull();
        activity!.Tags.Should().Contain(t => t.Key == "orchestrator.step_count" && t.Value == "5");
        activity.Tags.Should().Contain(t => t.Key == "orchestrator.depth" && t.Value == "2");
        activity.Status.Should().Be(ActivityStatusCode.Ok);
    }

    [Fact]
    public void StartPlanExecution_ShouldIncludePlanId()
    {
        // Arrange
        var planId = Guid.NewGuid();

        // Act
        using var activity = OrchestrationTracing.StartPlanExecution(planId, 10);

        // Assert
        activity.Should().NotBeNull();
        activity!.OperationName.Should().Be("orchestrator.execute_plan");
        activity.Tags.Should().Contain(t => t.Key == "orchestrator.plan_id" && t.Value == planId.ToString());
        activity.Tags.Should().Contain(t => t.Key == "orchestrator.step_count" && t.Value == "10");
    }

    [Fact]
    public void CompletePlanExecution_WithFailure_ShouldSetErrorStatus()
    {
        // Arrange
        using var activity = OrchestrationTracing.StartPlanExecution(Guid.NewGuid(), 5);

        // Act
        OrchestrationTracing.CompletePlanExecution(
            activity,
            stepsCompleted: 3,
            stepsFailed: 2,
            TimeSpan.FromMilliseconds(500),
            success: false);

        // Assert
        activity.Should().NotBeNull();
        activity!.Tags.Should().Contain(t => t.Key == "orchestrator.steps_completed" && t.Value == "3");
        activity!.Tags.Should().Contain(t => t.Key == "orchestrator.steps_failed" && t.Value == "2");
        activity.Status.Should().Be(ActivityStatusCode.Error);
    }

    [Fact]
    public void RecordError_ShouldAddExceptionDetails()
    {
        // Arrange
        using var activity = OrchestrationTracing.StartModelSelection("test");
        var exception = new InvalidOperationException("Test error");

        // Act
        OrchestrationTracing.RecordError(activity, "test_op", exception);

        // Assert
        activity.Should().NotBeNull();
        activity!.Status.Should().Be(ActivityStatusCode.Error);
        activity.Tags.Should().Contain(t => t.Key == "error.type" && t.Value == "System.InvalidOperationException");
        activity.Tags.Should().Contain(t => t.Key == "error.message" && t.Value == "Test error");
    }

    [Fact]
    public void RecordEvent_ShouldAddEventToCurrentActivity()
    {
        // Arrange
        using var activity = OrchestrationTracing.StartModelSelection("test");

        // Act
        OrchestrationTracing.RecordEvent("custom_event", new Dictionary<string, object?>
        {
            ["key1"] = "value1",
            ["key2"] = 42
        });

        // Assert
        activity.Should().NotBeNull();
        activity!.Events.Should().Contain(e => e.Name == "custom_event");
    }
}

[Trait("Category", "Unit")]
public sealed class OrchestrationScopeTests
{
    [Fact]
    public void ModelSelectionScope_ShouldTrackElapsedTime()
    {
        // Act
        using var scope = OrchestrationScope.ModelSelection("test prompt");
        Thread.Sleep(10); // Ensure some time passes

        // Assert
        scope.Elapsed.TotalMilliseconds.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ModelSelectionScope_CompleteModelSelection_ShouldSetActivity()
    {
        // Arrange
        using var scope = OrchestrationScope.ModelSelection("test prompt");

        // Act
        scope.CompleteModelSelection("gpt-4", UseCaseType.Reasoning, 0.9);

        // Assert
        scope.Activity?.Tags.Should().Contain(t => t.Key == "orchestrator.selected_model");
    }

    [Fact]
    public void Scope_Fail_ShouldSetErrorStatus()
    {
        // Arrange
        using var scope = OrchestrationScope.ModelSelection("test");

        // Act
        scope.Fail("Something went wrong");

        // Assert
        scope.Activity?.Status.Should().Be(ActivityStatusCode.Error);
    }

    [Fact]
    public void RoutingScope_ShouldCreateRoutingActivity()
    {
        // Act
        using var scope = OrchestrationScope.Routing("analyze task");

        // Assert
        scope.Activity?.OperationName.Should().Be("orchestrator.route");
    }

    [Fact]
    public void PlanCreationScope_ShouldIncludeMaxDepth()
    {
        // Act
        using var scope = OrchestrationScope.PlanCreation("build app", 3);

        // Assert
        scope.Activity?.Tags.Should().Contain(t => t.Key == "orchestrator.max_depth" && t.Value == "3");
    }

    [Fact]
    public void PlanExecutionScope_ShouldIncludeStepCount()
    {
        // Act
        using var scope = OrchestrationScope.PlanExecution(Guid.NewGuid(), 5);

        // Assert
        scope.Activity?.Tags.Should().Contain(t => t.Key == "orchestrator.step_count" && t.Value == "5");
    }
}

[Trait("Category", "Unit")]
public sealed class OrchestrationInstrumentationIntegrationTests
{
    [Fact]
    public async Task SmartModelOrchestrator_SelectModelAsync_ShouldGenerateTraces()
    {
        // Arrange
        var capturedActivities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Ouroboros.Orchestration",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => capturedActivities.Add(activity),
        };
        ActivitySource.AddActivityListener(listener);

        var tools = new ToolRegistry();
        using var orchestrator = new SmartModelOrchestrator(tools);
        orchestrator.RegisterModel(new ModelCapability(
            "test-model",
            new[] { "code", "analysis" },
            4096,
            0.01,
            100,
            ModelType.General));

        // Act
        await orchestrator.SelectModelAsync("Write a function to sort an array");

        // Assert
        capturedActivities.Should().Contain(a => a.OperationName == "orchestrator.select_model");
    }
}
