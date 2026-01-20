// <copyright file="ReplayEngineTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Pipeline;

using FluentAssertions;
using LangChain.DocumentLoaders;
using Ouroboros.Pipeline.Branches;
using Ouroboros.Pipeline.Reasoning;
using Ouroboros.Pipeline.Retrieval;
using Ouroboros.Providers;
using Ouroboros.Tests.Mocks;
using Ouroboros.Tools;
using Xunit;

/// <summary>
/// Tests for the ReplayEngine implementation.
/// Note: These tests use mocks and don't require external services.
/// </summary>
[Trait("Category", "Unit")]
public class ReplayEngineTests
{
    #region Basic Branch Tests

    [Fact]
    public void EmptyBranch_HasNoEvents()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");

        // Act
        var branch = new PipelineBranch("test", store, source);

        // Assert
        branch.Events.Should().BeEmpty();
        branch.Name.Should().Be("test");
    }

    [Fact]
    public void Branch_WithReasoning_PreservesEvents()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");
        var branch = new PipelineBranch("test", store, source);

        // Act
        var branch1 = branch.WithReasoning(new Draft("Draft 1"), "Prompt 1");
        var branch2 = branch1.WithReasoning(new Critique("Critique 1"), "Prompt 2");
        var branch3 = branch2.WithReasoning(new FinalSpec("Final"), "Prompt 3");

        // Assert
        branch3.Events.Should().HaveCount(3);
        branch3.Events[0].Should().BeOfType<ReasoningStep>()
            .Which.State.Should().BeOfType<Draft>();
        branch3.Events[1].Should().BeOfType<ReasoningStep>()
            .Which.State.Should().BeOfType<Critique>();
        branch3.Events[2].Should().BeOfType<ReasoningStep>()
            .Which.State.Should().BeOfType<FinalSpec>();
    }

    [Fact]
    public void Branch_WithIngestEvent_PreservesEventOrder()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");
        var branch = new PipelineBranch("test", store, source);

        // Act
        var result = branch
            .WithIngestEvent("source1", new List<string> { "doc1" })
            .WithReasoning(new Draft("Draft"), "Prompt")
            .WithIngestEvent("source2", new List<string> { "doc2" });

        // Assert
        result.Events.Should().HaveCount(3);
        result.Events[0].Should().BeOfType<IngestBatch>();
        result.Events[1].Should().BeOfType<ReasoningStep>();
        result.Events[2].Should().BeOfType<IngestBatch>();
    }

    #endregion

    #region Reasoning State Tests

    [Fact]
    public void Draft_StoresText()
    {
        // Arrange
        var draft = new Draft("This is a draft");

        // Assert
        draft.DraftText.Should().Be("This is a draft");
        draft.Text.Should().Be("This is a draft");
        draft.Kind.Should().Be("Draft");
    }

    [Fact]
    public void Critique_StoresText()
    {
        // Arrange
        var critique = new Critique("This needs improvement");

        // Assert
        critique.CritiqueText.Should().Be("This needs improvement");
        critique.Text.Should().Be("This needs improvement");
        critique.Kind.Should().Be("Critique");
    }

    [Fact]
    public void FinalSpec_StoresText()
    {
        // Arrange
        var finalSpec = new FinalSpec("Final specification");

        // Assert
        finalSpec.FinalText.Should().Be("Final specification");
        finalSpec.Text.Should().Be("Final specification");
        finalSpec.Kind.Should().Be("Final");
    }

    [Fact]
    public void Thinking_StoresText()
    {
        // Arrange
        var thinking = new Thinking("Reasoning process");

        // Assert
        thinking.Text.Should().Be("Reasoning process");
        thinking.Kind.Should().Be("Thinking");
    }

    [Fact]
    public void DocumentRevision_StoresAllProperties()
    {
        // Arrange
        var revision = new DocumentRevision(
            "/path/to/doc.md",
            "Revised content",
            1,
            "Improve clarity");

        // Assert
        revision.FilePath.Should().Be("/path/to/doc.md");
        revision.RevisionText.Should().Be("Revised content");
        revision.Iteration.Should().Be(1);
        revision.Goal.Should().Be("Improve clarity");
        revision.Kind.Should().Be("DocumentRevision");
    }

    #endregion

    #region ReasoningStep Tests

    [Fact]
    public void ReasoningStep_HasCorrectProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var draft = new Draft("Draft text");
        var tools = new List<ToolExecution>
        {
            new ToolExecution("math", "2+2", "4", DateTime.UtcNow)
        };

        // Act
        var step = new ReasoningStep(id, "Draft", draft, timestamp, "Prompt", tools);

        // Assert
        step.Id.Should().Be(id);
        step.StepKind.Should().Be("Draft");
        step.State.Should().BeSameAs(draft);
        step.Timestamp.Should().Be(timestamp);
        step.Prompt.Should().Be("Prompt");
        step.ToolCalls.Should().HaveCount(1);
        step.ToolCalls![0].ToolName.Should().Be("math");
    }

    [Fact]
    public void ReasoningStep_WithNullToolCalls_HasNullToolCalls()
    {
        // Arrange
        var step = new ReasoningStep(
            Guid.NewGuid(),
            "Draft",
            new Draft("Text"),
            DateTime.UtcNow,
            "Prompt",
            null);

        // Assert
        step.ToolCalls.Should().BeNull();
    }

    #endregion

    #region IngestBatch Tests

    [Fact]
    public void IngestBatch_HasCorrectProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var timestamp = DateTime.UtcNow;
        var ids = new List<string> { "id1", "id2", "id3" };

        // Act
        var batch = new IngestBatch(id, "source", ids, timestamp);

        // Assert
        batch.Id.Should().Be(id);
        batch.Source.Should().Be("source");
        batch.Ids.Should().Equal(ids);
        batch.Timestamp.Should().Be(timestamp);
    }

    [Fact]
    public void IngestBatch_WithEmptyIds_HasEmptyList()
    {
        // Arrange
        var batch = new IngestBatch(
            Guid.NewGuid(),
            "source",
            new List<string>(),
            DateTime.UtcNow);

        // Assert
        batch.Ids.Should().BeEmpty();
    }

    #endregion

    #region ToolExecution Tests

    [Fact]
    public void ToolExecution_HasCorrectProperties()
    {
        // Arrange
        var timestamp = DateTime.UtcNow;

        // Act
        var execution = new ToolExecution("math", "2+2", "4", timestamp);

        // Assert
        execution.ToolName.Should().Be("math");
        execution.Arguments.Should().Be("2+2");
        execution.Output.Should().Be("4");
        execution.Timestamp.Should().Be(timestamp);
    }

    #endregion

    #region Branch Fork Tests

    [Fact]
    public void Fork_CreatesIndependentBranch()
    {
        // Arrange
        var store1 = new TrackedVectorStore();
        var store2 = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");
        var original = new PipelineBranch("original", store1, source)
            .WithReasoning(new Draft("Original draft"), "Prompt");

        // Act
        var forked = original.Fork("forked", store2);

        // Assert
        forked.Name.Should().Be("forked");
        forked.Store.Should().BeSameAs(store2);
        forked.Events.Should().HaveCount(1);
    }

    [Fact]
    public void Fork_PreservesEventHistory()
    {
        // Arrange
        var store1 = new TrackedVectorStore();
        var store2 = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");
        var original = new PipelineBranch("original", store1, source)
            .WithReasoning(new Draft("Draft 1"), "Prompt 1")
            .WithReasoning(new Critique("Critique"), "Prompt 2")
            .WithIngestEvent("source", new List<string> { "doc1" });

        // Act
        var forked = original.Fork("forked", store2);

        // Assert
        forked.Events.Should().HaveCount(3);
        forked.Events.Should().Equal(original.Events);
    }

    [Fact]
    public void Fork_ModificationsDoNotAffectOriginal()
    {
        // Arrange
        var store1 = new TrackedVectorStore();
        var store2 = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");
        var original = new PipelineBranch("original", store1, source)
            .WithReasoning(new Draft("Draft"), "Prompt");

        // Act
        var forked = original.Fork("forked", store2);
        var modified = forked.WithReasoning(new FinalSpec("Final"), "Prompt 2");

        // Assert
        original.Events.Should().HaveCount(1);
        forked.Events.Should().HaveCount(1);
        modified.Events.Should().HaveCount(2);
    }

    #endregion

    #region Immutability Tests

    [Fact]
    public void WithReasoning_ReturnsNewInstance()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");
        var branch = new PipelineBranch("test", store, source);

        // Act
        var newBranch = branch.WithReasoning(new Draft("Draft"), "Prompt");

        // Assert
        newBranch.Should().NotBeSameAs(branch);
        branch.Events.Should().BeEmpty();
        newBranch.Events.Should().HaveCount(1);
    }

    [Fact]
    public void WithIngestEvent_ReturnsNewInstance()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");
        var branch = new PipelineBranch("test", store, source);

        // Act
        var newBranch = branch.WithIngestEvent("source", new List<string> { "id1" });

        // Assert
        newBranch.Should().NotBeSameAs(branch);
        branch.Events.Should().BeEmpty();
        newBranch.Events.Should().HaveCount(1);
    }

    [Fact]
    public void WithSource_ReturnsNewInstance()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source1 = DataSource.FromPath("/path1");
        var source2 = DataSource.FromPath("/path2");
        var branch = new PipelineBranch("test", store, source1);

        // Act
        var newBranch = branch.WithSource(source2);

        // Assert
        newBranch.Should().NotBeSameAs(branch);
        branch.Source.Should().BeSameAs(source1);
        newBranch.Source.Should().BeSameAs(source2);
    }

    #endregion

    #region Event Ordering Tests

    [Fact]
    public void Events_MaintainInsertionOrder()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");
        var branch = new PipelineBranch("test", store, source);

        // Act
        var result = branch
            .WithReasoning(new Draft("1"), "P1")
            .WithReasoning(new Draft("2"), "P2")
            .WithReasoning(new Draft("3"), "P3")
            .WithReasoning(new Draft("4"), "P4")
            .WithReasoning(new Draft("5"), "P5");

        // Assert
        var steps = result.Events.OfType<ReasoningStep>().ToList();
        steps[0].State.Text.Should().Be("1");
        steps[1].State.Text.Should().Be("2");
        steps[2].State.Text.Should().Be("3");
        steps[3].State.Text.Should().Be("4");
        steps[4].State.Text.Should().Be("5");
    }

    [Fact]
    public void Events_GenerateUniqueIds()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");
        var branch = new PipelineBranch("test", store, source);

        // Act
        var result = branch
            .WithReasoning(new Draft("1"), "P1")
            .WithReasoning(new Draft("2"), "P2")
            .WithReasoning(new Draft("3"), "P3");

        // Assert
        var ids = result.Events.Select(e => e.Id).ToList();
        ids.Should().OnlyHaveUniqueItems();
        ids.Should().NotContain(Guid.Empty);
    }

    #endregion
}
