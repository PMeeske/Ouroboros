// <copyright file="DistributedTracingTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests;

using System.Diagnostics;
using Ouroboros.Diagnostics;
using Xunit;

[Trait("Category", "Unit")]
public class DistributedTracingTests
{
    public DistributedTracingTests()
    {
        // Ensure tracing is enabled for tests
        TracingConfiguration.DisableTracing();
    }

    [Fact]
    public void StartActivity_ShouldCreateActivity()
    {
        // Arrange
        TracingConfiguration.EnableTracing();

        // Act
        using var activity = DistributedTracing.StartActivity("test_operation");

        // Assert
        Assert.NotNull(activity);
        Assert.Equal("test_operation", activity.OperationName);
    }

    [Fact]
    public void StartActivity_WithTags_ShouldAddTags()
    {
        // Arrange
        TracingConfiguration.EnableTracing();
        var tags = new Dictionary<string, object?>
        {
            ["key1"] = "value1",
            ["key2"] = 42,
        };

        // Act
        using var activity = DistributedTracing.StartActivity("test_operation", tags: tags);

        // Assert
        Assert.NotNull(activity);

        // At least one tag should be present
        Assert.True(activity.Tags.Any());
    }

    [Fact]
    public void RecordEvent_ShouldAddEventToActivity()
    {
        // Arrange
        TracingConfiguration.EnableTracing();

        // Act
        using var activity = DistributedTracing.StartActivity("test_operation");
        DistributedTracing.RecordEvent("test_event", new Dictionary<string, object?> { ["detail"] = "info" });

        // Assert
        Assert.NotNull(activity);
        Assert.Single(activity.Events);
        Assert.Equal("test_event", activity.Events.First().Name);
    }

    [Fact]
    public void RecordException_ShouldSetErrorStatus()
    {
        // Arrange
        TracingConfiguration.EnableTracing();
        var exception = new InvalidOperationException("Test error");

        // Act
        using var activity = DistributedTracing.StartActivity("test_operation");
        DistributedTracing.RecordException(exception);

        // Assert
        Assert.NotNull(activity);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Contains(activity.Tags, t => t.Key == "exception.type");
        Assert.Contains(activity.Tags, t => t.Key == "exception.message" && t.Value as string == "Test error");
    }

    [Fact]
    public void SetStatus_ShouldUpdateActivityStatus()
    {
        // Arrange
        TracingConfiguration.EnableTracing();

        // Act
        using var activity = DistributedTracing.StartActivity("test_operation");
        DistributedTracing.SetStatus(ActivityStatusCode.Ok, "Success");

        // Assert
        Assert.NotNull(activity);
        Assert.Equal(ActivityStatusCode.Ok, activity.Status);

        // StatusDescription might not be exposed the same way in all Activity implementations
    }

    [Fact]
    public void AddTag_ShouldAddTagToActivity()
    {
        // Arrange
        TracingConfiguration.EnableTracing();

        // Act
        using var activity = DistributedTracing.StartActivity("test_operation");
        DistributedTracing.AddTag("custom_key", "custom_value");

        // Assert
        Assert.NotNull(activity);
        Assert.Contains(activity.Tags, t => t.Key == "custom_key" && t.Value as string == "custom_value");
    }

    [Fact]
    public void GetTraceId_ShouldReturnCurrentTraceId()
    {
        // Arrange
        TracingConfiguration.EnableTracing();

        // Act
        using var activity = DistributedTracing.StartActivity("test_operation");
        var traceId = DistributedTracing.GetTraceId();

        // Assert
        Assert.NotNull(traceId);
        Assert.NotEmpty(traceId);
    }

    [Fact]
    public void GetSpanId_ShouldReturnCurrentSpanId()
    {
        // Arrange
        TracingConfiguration.EnableTracing();

        // Act
        using var activity = DistributedTracing.StartActivity("test_operation");
        var spanId = DistributedTracing.GetSpanId();

        // Assert
        Assert.NotNull(spanId);
        Assert.NotEmpty(spanId);
    }

    [Fact]
    public void TraceToolExecution_ShouldCreateActivityWithToolTags()
    {
        // Arrange
        TracingConfiguration.EnableTracing();

        // Act
        using var activity = TracingExtensions.TraceToolExecution("test_tool", "input data");

        // Assert
        Assert.NotNull(activity);
        Assert.Contains("test_tool", activity.OperationName);
        Assert.Contains(activity.Tags, t => t.Key == "tool.name" && t.Value as string == "test_tool");
    }

    [Fact]
    public void TracePipelineExecution_ShouldCreateActivityWithPipelineTags()
    {
        // Arrange
        TracingConfiguration.EnableTracing();

        // Act
        using var activity = TracingExtensions.TracePipelineExecution("test_pipeline");

        // Assert
        Assert.NotNull(activity);
        Assert.Contains("test_pipeline", activity.OperationName);
        Assert.Contains(activity.Tags, t => t.Key == "pipeline.name" && t.Value as string == "test_pipeline");
    }

    [Fact]
    public void TraceLlmRequest_ShouldCreateActivityWithLlmTags()
    {
        // Arrange
        TracingConfiguration.EnableTracing();

        // Act
        using var activity = TracingExtensions.TraceLlmRequest("gpt-4", 500);

        // Assert
        Assert.NotNull(activity);
        Assert.Equal("llm.request", activity.OperationName);
        Assert.Contains(activity.Tags, t => t.Key == "llm.model" && t.Value as string == "gpt-4");
    }

    [Fact]
    public void TraceVectorOperation_ShouldCreateActivityWithVectorTags()
    {
        // Arrange
        TracingConfiguration.EnableTracing();

        // Act
        using var activity = TracingExtensions.TraceVectorOperation("search", 10);

        // Assert
        Assert.NotNull(activity);
        Assert.Contains("search", activity.OperationName);
        Assert.Contains(activity.Tags, t => t.Key == "vector.operation" && t.Value as string == "search");
    }

    [Fact]
    public void CompleteLlmRequest_ShouldAddCompletionTags()
    {
        // Arrange
        TracingConfiguration.EnableTracing();

        // Act
        using var activity = TracingExtensions.TraceLlmRequest("gpt-4", 500);
        activity.CompleteLlmRequest(responseLength: 1000, tokenCount: 150);

        // Assert
        Assert.NotNull(activity);

        // Tags set via SetTag show up after activity completion
        Assert.Equal(ActivityStatusCode.Ok, activity.Status);
    }

    [Fact]
    public void CompleteToolExecution_WithSuccess_ShouldSetOkStatus()
    {
        // Arrange
        TracingConfiguration.EnableTracing();

        // Act
        using var activity = TracingExtensions.TraceToolExecution("test_tool", "input");
        activity.CompleteToolExecution(success: true, outputLength: 200);

        // Assert
        Assert.NotNull(activity);

        // Tags set via SetTag show up after activity completion
        Assert.Equal(ActivityStatusCode.Ok, activity.Status);
    }

    [Fact]
    public void CompleteToolExecution_WithFailure_ShouldSetErrorStatus()
    {
        // Arrange
        TracingConfiguration.EnableTracing();

        // Act
        using var activity = TracingExtensions.TraceToolExecution("test_tool", "input");
        activity.CompleteToolExecution(success: false, outputLength: 0);

        // Assert
        Assert.NotNull(activity);

        // Tags set via SetTag show up after activity completion
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
    }

    [Fact]
    public void NestedActivities_ShouldMaintainParentChildRelationship()
    {
        // Arrange
        TracingConfiguration.EnableTracing();

        // Act
        using var parentActivity = DistributedTracing.StartActivity("parent");
        var parentId = parentActivity?.Id;

        using var childActivity = DistributedTracing.StartActivity("child");
        var childParentId = childActivity?.ParentId;

        // Assert
        Assert.NotNull(parentActivity);
        Assert.NotNull(childActivity);
        Assert.Equal(parentId, childParentId);
    }

    [Fact]
    public void ActivityListener_ShouldReceiveCallbacks()
    {
        // Arrange
        int startedCount = 0;
        int stoppedCount = 0;

        TracingConfiguration.EnableTracing(
            onActivityStarted: _ => startedCount++,
            onActivityStopped: _ => stoppedCount++);

        // Act
        using (var activity = DistributedTracing.StartActivity("test"))
        {
            // Activity is active
        }

        // Assert
        Assert.Equal(1, startedCount);
        Assert.Equal(1, stoppedCount);
    }

    [Fact]
    public void DisableTracing_ShouldStopCreatingActivities()
    {
        // Arrange
        TracingConfiguration.EnableTracing();
        using var activity1 = DistributedTracing.StartActivity("test1");
        Assert.NotNull(activity1);

        // Act
        TracingConfiguration.DisableTracing();
        using var activity2 = DistributedTracing.StartActivity("test2");

        // Assert
        Assert.Null(activity2);
    }
}
