// <copyright file="PipelineEventTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests.Domain;

using System.Text.Json;
using FluentAssertions;
using Ouroboros.Domain;
using Ouroboros.Domain.Events;
using Ouroboros.Domain.States;
using Xunit;

/// <summary>
/// Tests for PipelineEvent and its derived types (ReasoningStep, ToolExecution, etc.)
/// </summary>
[Trait("Category", "Unit")]
public class PipelineEventTests
{
    #region ToolExecution Tests

    [Fact]
    public void ToolExecution_Constructor_SetsAllProperties()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        // Act
        var execution = new ToolExecution(
            ToolName: "search",
            Arguments: "{\"query\": \"test\"}",
            Output: "Found 3 results",
            Timestamp: timestamp);

        // Assert
        execution.ToolName.Should().Be("search");
        execution.Arguments.Should().Be("{\"query\": \"test\"}");
        execution.Output.Should().Be("Found 3 results");
        execution.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void ToolExecution_Equality_SameValues_AreEqual()
    {
        // Arrange
        var timestamp = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var exec1 = new ToolExecution("math", "2+2", "4", timestamp);
        var exec2 = new ToolExecution("math", "2+2", "4", timestamp);

        // Assert
        exec1.Should().Be(exec2);
    }

    [Fact]
    public void ToolExecution_Equality_DifferentToolName_AreNotEqual()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var exec1 = new ToolExecution("math", "2+2", "4", timestamp);
        var exec2 = new ToolExecution("calc", "2+2", "4", timestamp);

        // Assert
        exec1.Should().NotBe(exec2);
    }

    [Fact]
    public void ToolExecution_Immutable_WithExpression()
    {
        // Arrange
        var original = new ToolExecution("tool", "args", "output", DateTime.UtcNow);

        // Act
        var modified = original with { Output = "new output" };

        // Assert
        original.Output.Should().Be("output");
        modified.Output.Should().Be("new output");
    }

    [Fact]
    public void ToolExecution_SerializesToJson()
    {
        // Arrange
        var execution = new ToolExecution("search", "query", "results", DateTime.UtcNow);

        // Act
        var json = JsonSerializer.Serialize(execution);

        // Assert
        json.Should().Contain("search");
        json.Should().Contain("query");
        json.Should().Contain("results");
    }

    [Fact]
    public void ToolExecution_RoundTripsViaJson()
    {
        // Arrange
        var timestamp = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        var original = new ToolExecution("math", "5*5", "25", timestamp);

        // Act
        var json = JsonSerializer.Serialize(original);
        var deserialized = JsonSerializer.Deserialize<ToolExecution>(json);

        // Assert
        deserialized.Should().Be(original);
    }

    #endregion

    #region ReasoningStep Tests

    [Fact]
    public void ReasoningStep_Constructor_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var state = new Draft("draft content");
        var timestamp = DateTime.UtcNow;
        var toolCalls = new List<ToolExecution>
        {
            new("search", "query", "result", timestamp)
        };

        // Act
        var step = new ReasoningStep(
            Id: id,
            StepKind: "Draft",
            State: state,
            Timestamp: timestamp,
            Prompt: "Generate a draft",
            ToolCalls: toolCalls);

        // Assert
        step.Id.Should().Be(id);
        step.StepKind.Should().Be("Draft");
        step.State.Should().Be(state);
        step.Timestamp.Should().Be(timestamp);
        step.Prompt.Should().Be("Generate a draft");
        step.ToolCalls.Should().HaveCount(1);
    }

    [Fact]
    public void ReasoningStep_WithNullToolCalls_SetsNull()
    {
        // Arrange
        var step = new ReasoningStep(
            Guid.NewGuid(),
            "Draft",
            new Draft("text"),
            DateTime.UtcNow,
            "prompt",
            null);

        // Assert
        step.ToolCalls.Should().BeNull();
    }

    [Fact]
    public void ReasoningStep_InheritsFromPipelineEvent()
    {
        // Arrange
        var id = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var step = new ReasoningStep(id, "Draft", new Draft("text"), timestamp, "prompt");

        // Assert - Check base class properties
        PipelineEvent baseEvent = step;
        baseEvent.Id.Should().Be(id);
        baseEvent.Kind.Should().Be("Reasoning");
        baseEvent.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void ReasoningStep_WithDraftState_HasCorrectStepKind()
    {
        // Arrange
        var state = new Draft("draft text");

        // Act
        var step = new ReasoningStep(
            Guid.NewGuid(),
            state.Kind,
            state,
            DateTime.UtcNow,
            "prompt");

        // Assert
        step.StepKind.Should().Be("Draft");
        step.State.Should().BeOfType<Draft>();
    }

    [Fact]
    public void ReasoningStep_WithCritiqueState_HasCorrectStepKind()
    {
        // Arrange
        var state = new Critique("critique text");

        // Act
        var step = new ReasoningStep(
            Guid.NewGuid(),
            state.Kind,
            state,
            DateTime.UtcNow,
            "prompt");

        // Assert
        step.StepKind.Should().Be("Critique");
        step.State.Should().BeOfType<Critique>();
    }

    [Fact]
    public void ReasoningStep_WithFinalSpecState_HasCorrectStepKind()
    {
        // Arrange
        var state = new FinalSpec("final text");

        // Act
        var step = new ReasoningStep(
            Guid.NewGuid(),
            state.Kind,
            state,
            DateTime.UtcNow,
            "prompt");

        // Assert
        step.StepKind.Should().Be("Final");
        step.State.Should().BeOfType<FinalSpec>();
    }

    [Fact]
    public void ReasoningStep_Immutable_WithExpression()
    {
        // Arrange
        var original = new ReasoningStep(
            Guid.NewGuid(),
            "Draft",
            new Draft("original"),
            DateTime.UtcNow,
            "prompt");

        // Act
        var modified = original with { Prompt = "new prompt" };

        // Assert
        original.Prompt.Should().Be("prompt");
        modified.Prompt.Should().Be("new prompt");
    }

    [Fact]
    public void ReasoningStep_Equality_SameValues_AreEqual()
    {
        // Arrange
        var id = Guid.NewGuid();
        var timestamp = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var state = new Draft("text");

        var step1 = new ReasoningStep(id, "Draft", state, timestamp, "prompt");
        var step2 = new ReasoningStep(id, "Draft", state, timestamp, "prompt");

        // Assert
        step1.Should().Be(step2);
    }

    [Fact]
    public void ReasoningStep_Equality_DifferentId_AreNotEqual()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;
        var state = new Draft("text");

        var step1 = new ReasoningStep(Guid.NewGuid(), "Draft", state, timestamp, "prompt");
        var step2 = new ReasoningStep(Guid.NewGuid(), "Draft", state, timestamp, "prompt");

        // Assert
        step1.Should().NotBe(step2);
    }

    #endregion

    #region PipelineEvent Polymorphism Tests

    [Fact]
    public void PipelineEvent_CanStoreReasoningStep()
    {
        // Arrange
        var step = new ReasoningStep(
            Guid.NewGuid(),
            "Draft",
            new Draft("text"),
            DateTime.UtcNow,
            "prompt");

        // Act
        PipelineEvent evt = step;

        // Assert
        evt.Should().BeOfType<ReasoningStep>();
        evt.Kind.Should().Be("Reasoning");
    }

    [Fact]
    public void PipelineEvent_KindProperty_DistinguishesTypes()
    {
        // Arrange
        var reasoningStep = new ReasoningStep(
            Guid.NewGuid(),
            "Draft",
            new Draft("text"),
            DateTime.UtcNow,
            "prompt");

        // Assert
        PipelineEvent evt = reasoningStep;
        evt.Kind.Should().Be("Reasoning");
    }

    #endregion

    #region ReasoningStep Collection Tests

    [Fact]
    public void ReasoningStep_CanBeUsedInCollection()
    {
        // Arrange
        var steps = new List<ReasoningStep>
        {
            new(Guid.NewGuid(), "Draft", new Draft("draft"), DateTime.UtcNow, "draft prompt"),
            new(Guid.NewGuid(), "Critique", new Critique("critique"), DateTime.UtcNow, "critique prompt"),
            new(Guid.NewGuid(), "Final", new FinalSpec("final"), DateTime.UtcNow, "final prompt")
        };

        // Assert
        steps.Should().HaveCount(3);
        steps.Select(s => s.StepKind).Should().BeEquivalentTo(["Draft", "Critique", "Final"]);
    }

    [Fact]
    public void ReasoningStep_FilterByStepKind()
    {
        // Arrange
        var steps = new List<ReasoningStep>
        {
            new(Guid.NewGuid(), "Draft", new Draft("draft1"), DateTime.UtcNow, "p1"),
            new(Guid.NewGuid(), "Critique", new Critique("critique1"), DateTime.UtcNow, "p2"),
            new(Guid.NewGuid(), "Draft", new Draft("draft2"), DateTime.UtcNow, "p3"),
            new(Guid.NewGuid(), "Final", new FinalSpec("final"), DateTime.UtcNow, "p4")
        };

        // Act
        var drafts = steps.Where(s => s.StepKind == "Draft").ToList();

        // Assert
        drafts.Should().HaveCount(2);
        drafts.All(s => s.State is Draft).Should().BeTrue();
    }

    [Fact]
    public void ReasoningStep_GetLatestByKind()
    {
        // Arrange
        var steps = new List<ReasoningStep>
        {
            new(Guid.NewGuid(), "Draft", new Draft("old draft"), new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc), "p1"),
            new(Guid.NewGuid(), "Draft", new Draft("new draft"), new DateTime(2024, 1, 1, 11, 0, 0, DateTimeKind.Utc), "p2")
        };

        // Act
        var latest = steps
            .Where(s => s.StepKind == "Draft")
            .OrderByDescending(s => s.Timestamp)
            .FirstOrDefault();

        // Assert
        latest.Should().NotBeNull();
        ((Draft)latest!.State).DraftText.Should().Be("new draft");
    }

    #endregion
}
