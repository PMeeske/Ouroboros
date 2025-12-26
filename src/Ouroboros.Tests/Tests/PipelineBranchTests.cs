using LangChain.DocumentLoaders;

namespace Ouroboros.Tests;

/// <summary>
/// Comprehensive tests for PipelineBranch following functional programming principles.
/// Tests focus on immutability, pure functions, and monadic composition.
/// </summary>
public class PipelineBranchTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_ShouldCreateBranch()
    {
        // Arrange
        var name = "test-branch";
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");

        // Act
        var branch = new PipelineBranch(name, store, source);

        // Assert
        branch.Name.Should().Be(name);
        branch.Store.Should().BeSameAs(store);
        branch.Source.Should().BeSameAs(source);
        branch.Events.Should().BeEmpty();
    }

    [Fact]
    public void Constructor_WithNullName_ShouldThrowArgumentNullException()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");

        // Act
        Action act = () => new PipelineBranch(null!, store, source);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("name");
    }

    [Fact]
    public void Constructor_WithNullStore_ShouldThrowArgumentNullException()
    {
        // Arrange
        var source = DataSource.FromPath("/test/path");

        // Act
        Action act = () => new PipelineBranch("test", null!, source);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("store");
    }

    [Fact]
    public void Constructor_WithNullSource_ShouldThrowArgumentNullException()
    {
        // Arrange
        var store = new TrackedVectorStore();

        // Act
        Action act = () => new PipelineBranch("test", store, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("source");
    }

    [Fact]
    public void Constructor_WithEmptyName_ShouldCreateBranch()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");

        // Act
        var branch = new PipelineBranch(string.Empty, store, source);

        // Assert
        branch.Name.Should().BeEmpty();
        branch.Events.Should().BeEmpty();
    }

    #endregion

    #region WithEvents Factory Method Tests

    [Fact]
    public void WithEvents_WithValidParameters_ShouldCreateBranchWithEvents()
    {
        // Arrange
        var name = "test-branch";
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var events = new List<PipelineEvent>
        {
            new ReasoningStep(
                Guid.NewGuid(),
                "Draft",
                new Draft("Initial draft"),
                DateTime.UtcNow,
                "Create a draft"),
            new IngestBatch(
                Guid.NewGuid(),
                "test-source",
                new List<string> { "doc1", "doc2" },
                DateTime.UtcNow)
        };

        // Act
        var branch = PipelineBranch.WithEvents(name, store, source, events);

        // Assert
        branch.Name.Should().Be(name);
        branch.Store.Should().BeSameAs(store);
        branch.Source.Should().BeSameAs(source);
        branch.Events.Should().HaveCount(2);
        branch.Events.Should().ContainItemsAssignableTo<PipelineEvent>();
    }

    [Fact]
    public void WithEvents_WithEmptyEvents_ShouldCreateBranchWithNoEvents()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");

        // Act
        var branch = PipelineBranch.WithEvents("test", store, source, new List<PipelineEvent>());

        // Assert
        branch.Events.Should().BeEmpty();
    }

    [Fact]
    public void WithEvents_EventsAreImmutable_CannotBeModifiedAfterCreation()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var events = new List<PipelineEvent>
        {
            new IngestBatch(Guid.NewGuid(), "source", new List<string> { "id1" }, DateTime.UtcNow)
        };

        // Act
        var branch = PipelineBranch.WithEvents("test", store, source, events);
        events.Add(new IngestBatch(Guid.NewGuid(), "source2", new List<string> { "id2" }, DateTime.UtcNow));

        // Assert - Original branch events should not be affected
        branch.Events.Should().HaveCount(1);
    }

    #endregion

    #region WithReasoning Tests

    [Fact]
    public void WithReasoning_WithDraftState_ShouldReturnNewBranchWithEvent()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var originalBranch = new PipelineBranch("test", store, source);
        var draftState = new Draft("This is a draft response");
        var prompt = "Generate a draft";

        // Act
        var newBranch = originalBranch.WithReasoning(draftState, prompt);

        // Assert
        newBranch.Should().NotBeSameAs(originalBranch);
        newBranch.Events.Should().HaveCount(1);
        newBranch.Name.Should().Be(originalBranch.Name);
        newBranch.Store.Should().BeSameAs(originalBranch.Store);
        newBranch.Source.Should().BeSameAs(originalBranch.Source);
        
        var reasoningEvent = newBranch.Events.First().Should().BeOfType<ReasoningStep>().Subject;
        reasoningEvent.State.Should().BeSameAs(draftState);
        reasoningEvent.Prompt.Should().Be(prompt);
        reasoningEvent.StepKind.Should().Be("Draft");
        reasoningEvent.ToolCalls.Should().BeNull();
    }

    [Fact]
    public void WithReasoning_WithCritiqueState_ShouldReturnNewBranchWithEvent()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var originalBranch = new PipelineBranch("test", store, source);
        var critiqueState = new Critique("This needs improvement");
        var prompt = "Critique the draft";

        // Act
        var newBranch = originalBranch.WithReasoning(critiqueState, prompt);

        // Assert
        newBranch.Events.Should().HaveCount(1);
        var reasoningEvent = newBranch.Events.First().Should().BeOfType<ReasoningStep>().Subject;
        reasoningEvent.State.Should().BeSameAs(critiqueState);
        reasoningEvent.StepKind.Should().Be("Critique");
    }

    [Fact]
    public void WithReasoning_WithFinalSpecState_ShouldReturnNewBranchWithEvent()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var originalBranch = new PipelineBranch("test", store, source);
        var finalState = new FinalSpec("Final specification text");
        var prompt = "Finalize the spec";

        // Act
        var newBranch = originalBranch.WithReasoning(finalState, prompt);

        // Assert
        newBranch.Events.Should().HaveCount(1);
        var reasoningEvent = newBranch.Events.First().Should().BeOfType<ReasoningStep>().Subject;
        reasoningEvent.State.Should().BeSameAs(finalState);
        reasoningEvent.StepKind.Should().Be("Final");
    }

    [Fact]
    public void WithReasoning_WithToolExecutions_ShouldIncludeToolsInEvent()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var originalBranch = new PipelineBranch("test", store, source);
        var draftState = new Draft("Draft with tools");
        var prompt = "Generate with tools";
        var tools = new List<ToolExecution>
        {
            new ToolExecution("calculator", "add(1, 2)", "3", DateTime.UtcNow),
            new ToolExecution("search", "query('test')", "results", DateTime.UtcNow)
        };

        // Act
        var newBranch = originalBranch.WithReasoning(draftState, prompt, tools);

        // Assert
        var reasoningEvent = newBranch.Events.First().Should().BeOfType<ReasoningStep>().Subject;
        reasoningEvent.ToolCalls.Should().NotBeNull();
        reasoningEvent.ToolCalls.Should().HaveCount(2);
        reasoningEvent.ToolCalls![0].ToolName.Should().Be("calculator");
        reasoningEvent.ToolCalls[1].ToolName.Should().Be("search");
    }

    [Fact]
    public void WithReasoning_WithNullState_ShouldThrowArgumentNullException()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test", store, source);

        // Act
        Action act = () => branch.WithReasoning(null!, "prompt");

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("state");
    }

    [Fact]
    public void WithReasoning_WithNullPrompt_ShouldThrowArgumentNullException()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test", store, source);
        var state = new Draft("test");

        // Act
        Action act = () => branch.WithReasoning(state, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("prompt");
    }

    [Fact]
    public void WithReasoning_CalledMultipleTimes_ShouldCreateEventChain()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test", store, source);

        // Act - Functional composition: each operation returns a new immutable instance
        var branch1 = branch.WithReasoning(new Draft("Draft 1"), "Prompt 1");
        var branch2 = branch1.WithReasoning(new Critique("Critique 1"), "Prompt 2");
        var branch3 = branch2.WithReasoning(new Draft("Draft 2"), "Prompt 3");
        var branch4 = branch3.WithReasoning(new FinalSpec("Final"), "Prompt 4");

        // Assert - Immutability: original branch unchanged
        branch.Events.Should().BeEmpty();
        branch1.Events.Should().HaveCount(1);
        branch2.Events.Should().HaveCount(2);
        branch3.Events.Should().HaveCount(3);
        branch4.Events.Should().HaveCount(4);

        // Assert - Event order preserved
        branch4.Events[0].Should().BeOfType<ReasoningStep>()
            .Which.State.Should().BeOfType<Draft>().Which.DraftText.Should().Be("Draft 1");
        branch4.Events[1].Should().BeOfType<ReasoningStep>()
            .Which.State.Should().BeOfType<Critique>();
        branch4.Events[2].Should().BeOfType<ReasoningStep>()
            .Which.State.Should().BeOfType<Draft>().Which.DraftText.Should().Be("Draft 2");
        branch4.Events[3].Should().BeOfType<ReasoningStep>()
            .Which.State.Should().BeOfType<FinalSpec>();
    }

    [Fact]
    public void WithReasoning_Immutability_OriginalBranchShouldNotChange()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var originalBranch = new PipelineBranch("test", store, source);

        // Act
        var newBranch = originalBranch.WithReasoning(new Draft("test"), "prompt");

        // Assert - Demonstrates immutability
        originalBranch.Events.Should().BeEmpty();
        newBranch.Events.Should().HaveCount(1);
    }

    [Fact]
    public void WithReasoning_GeneratesUniqueIds_ForEachEvent()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test", store, source);

        // Act
        var branch1 = branch.WithReasoning(new Draft("Draft 1"), "Prompt 1");
        var branch2 = branch1.WithReasoning(new Draft("Draft 2"), "Prompt 2");

        // Assert
        var id1 = branch2.Events[0].Id;
        var id2 = branch2.Events[1].Id;
        id1.Should().NotBe(id2);
        id1.Should().NotBe(Guid.Empty);
        id2.Should().NotBe(Guid.Empty);
    }

    #endregion

    #region WithIngestEvent Tests

    [Fact]
    public void WithIngestEvent_WithValidParameters_ShouldReturnNewBranchWithEvent()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var originalBranch = new PipelineBranch("test", store, source);
        var sourceString = "test-source";
        var ids = new List<string> { "doc1", "doc2", "doc3" };

        // Act
        var newBranch = originalBranch.WithIngestEvent(sourceString, ids);

        // Assert
        newBranch.Should().NotBeSameAs(originalBranch);
        newBranch.Events.Should().HaveCount(1);
        
        var ingestEvent = newBranch.Events.First().Should().BeOfType<IngestBatch>().Subject;
        ingestEvent.Source.Should().Be(sourceString);
        ingestEvent.Ids.Should().HaveCount(3);
        ingestEvent.Ids.Should().Contain("doc1");
        ingestEvent.Ids.Should().Contain("doc2");
        ingestEvent.Ids.Should().Contain("doc3");
    }

    [Fact]
    public void WithIngestEvent_WithEmptyIds_ShouldCreateEventWithEmptyList()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test", store, source);

        // Act
        var newBranch = branch.WithIngestEvent("source", new List<string>());

        // Assert
        var ingestEvent = newBranch.Events.First().Should().BeOfType<IngestBatch>().Subject;
        ingestEvent.Ids.Should().BeEmpty();
    }

    [Fact]
    public void WithIngestEvent_WithNullSource_ShouldThrowArgumentNullException()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test", store, source);

        // Act
        Action act = () => branch.WithIngestEvent(null!, new List<string> { "id1" });

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("sourceString");
    }

    [Fact]
    public void WithIngestEvent_WithNullIds_ShouldThrowArgumentNullException()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test", store, source);

        // Act
        Action act = () => branch.WithIngestEvent("source", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("ids");
    }

    [Fact]
    public void WithIngestEvent_CalledMultipleTimes_ShouldAccumulateEvents()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test", store, source);

        // Act
        var branch1 = branch.WithIngestEvent("source1", new List<string> { "id1", "id2" });
        var branch2 = branch1.WithIngestEvent("source2", new List<string> { "id3" });
        var branch3 = branch2.WithIngestEvent("source3", new List<string> { "id4", "id5", "id6" });

        // Assert
        branch.Events.Should().BeEmpty();
        branch1.Events.Should().HaveCount(1);
        branch2.Events.Should().HaveCount(2);
        branch3.Events.Should().HaveCount(3);

        var event1 = branch3.Events[0].Should().BeOfType<IngestBatch>().Subject;
        event1.Source.Should().Be("source1");
        event1.Ids.Should().HaveCount(2);

        var event2 = branch3.Events[1].Should().BeOfType<IngestBatch>().Subject;
        event2.Source.Should().Be("source2");
        event2.Ids.Should().HaveCount(1);

        var event3 = branch3.Events[2].Should().BeOfType<IngestBatch>().Subject;
        event3.Source.Should().Be("source3");
        event3.Ids.Should().HaveCount(3);
    }

    [Fact]
    public void WithIngestEvent_IdsListIsImmutable_ModifyingOriginalDoesNotAffectEvent()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test", store, source);
        var ids = new List<string> { "id1", "id2" };

        // Act
        var newBranch = branch.WithIngestEvent("source", ids);
        ids.Add("id3"); // Modify original list

        // Assert - Event should not be affected by modification
        var ingestEvent = newBranch.Events.First().Should().BeOfType<IngestBatch>().Subject;
        ingestEvent.Ids.Should().HaveCount(2);
        ingestEvent.Ids.Should().NotContain("id3");
    }

    #endregion

    #region WithSource Tests

    [Fact]
    public void WithSource_WithValidSource_ShouldReturnNewBranchWithUpdatedSource()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var originalSource = DataSource.FromPath("/original/path");
        var newSource = DataSource.FromPath("/new/path");
        var originalBranch = new PipelineBranch("test", store, originalSource);

        // Act
        var newBranch = originalBranch.WithSource(newSource);

        // Assert
        newBranch.Should().NotBeSameAs(originalBranch);
        newBranch.Source.Should().BeSameAs(newSource);
        newBranch.Name.Should().Be(originalBranch.Name);
        newBranch.Store.Should().BeSameAs(originalBranch.Store);
        newBranch.Events.Should().BeSameAs(originalBranch.Events);
    }

    [Fact]
    public void WithSource_WithNullSource_ShouldThrowArgumentNullException()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test", store, source);

        // Act
        Action act = () => branch.WithSource(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("source");
    }

    [Fact]
    public void WithSource_PreservesExistingEvents_WhenChangingSource()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var originalSource = DataSource.FromPath("/original/path");
        var newSource = DataSource.FromPath("/new/path");
        var branch = new PipelineBranch("test", store, originalSource);
        var branchWithEvents = branch
            .WithReasoning(new Draft("Draft"), "Prompt")
            .WithIngestEvent("source", new List<string> { "id1" });

        // Act
        var branchWithNewSource = branchWithEvents.WithSource(newSource);

        // Assert
        branchWithNewSource.Events.Should().HaveCount(2);
        branchWithNewSource.Source.Should().BeSameAs(newSource);
    }

    [Fact]
    public void WithSource_Immutability_OriginalBranchUnchanged()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var originalSource = DataSource.FromPath("/original/path");
        var newSource = DataSource.FromPath("/new/path");
        var originalBranch = new PipelineBranch("test", store, originalSource);

        // Act
        var newBranch = originalBranch.WithSource(newSource);

        // Assert
        originalBranch.Source.Should().BeSameAs(originalSource);
        newBranch.Source.Should().BeSameAs(newSource);
    }

    #endregion

    #region Fork Tests

    [Fact]
    public void Fork_WithValidParameters_ShouldCreateNewBranchWithCopiedEvents()
    {
        // Arrange
        var originalStore = new TrackedVectorStore();
        var newStore = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var originalBranch = new PipelineBranch("original", originalStore, source)
            .WithReasoning(new Draft("Draft"), "Prompt")
            .WithIngestEvent("source", new List<string> { "id1", "id2" });

        // Act
        var forkedBranch = originalBranch.Fork("forked", newStore);

        // Assert
        forkedBranch.Should().NotBeSameAs(originalBranch);
        forkedBranch.Name.Should().Be("forked");
        forkedBranch.Store.Should().BeSameAs(newStore);
        forkedBranch.Source.Should().BeSameAs(source); // Same source
        forkedBranch.Events.Should().HaveCount(2); // Events copied
        forkedBranch.Events.Should().Equal(originalBranch.Events);
    }

    [Fact]
    public void Fork_WithNullName_ShouldThrowArgumentNullException()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var newStore = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test", store, source);

        // Act
        Action act = () => branch.Fork(null!, newStore);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("newName");
    }

    [Fact]
    public void Fork_WithNullStore_ShouldThrowArgumentNullException()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test", store, source);

        // Act
        Action act = () => branch.Fork("forked", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("newStore");
    }

    [Fact]
    public void Fork_EventsAreSharedReference_ModifyingForkedBranchDoesNotAffectOriginal()
    {
        // Arrange
        var originalStore = new TrackedVectorStore();
        var newStore = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var originalBranch = new PipelineBranch("original", originalStore, source)
            .WithReasoning(new Draft("Draft"), "Prompt");

        // Act
        var forkedBranch = originalBranch.Fork("forked", newStore);
        var modifiedForkedBranch = forkedBranch.WithReasoning(new Critique("Critique"), "Prompt2");

        // Assert
        originalBranch.Events.Should().HaveCount(1);
        forkedBranch.Events.Should().HaveCount(1);
        modifiedForkedBranch.Events.Should().HaveCount(2);
    }

    [Fact]
    public void Fork_WithEmptyEvents_ShouldCreateEmptyForkedBranch()
    {
        // Arrange
        var originalStore = new TrackedVectorStore();
        var newStore = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var originalBranch = new PipelineBranch("original", originalStore, source);

        // Act
        var forkedBranch = originalBranch.Fork("forked", newStore);

        // Assert
        forkedBranch.Events.Should().BeEmpty();
    }

    [Fact]
    public void Fork_CanBeUsedForParallelProcessing_WithDifferentStores()
    {
        // Arrange
        var mainStore = new TrackedVectorStore();
        var experimentalStore1 = new TrackedVectorStore();
        var experimentalStore2 = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var mainBranch = new PipelineBranch("main", mainStore, source)
            .WithReasoning(new Draft("Initial draft"), "Start");

        // Act - Fork for parallel experiments
        var experiment1 = mainBranch.Fork("experiment1", experimentalStore1);
        var experiment2 = mainBranch.Fork("experiment2", experimentalStore2);

        // Further processing on experiments
        var exp1Result = experiment1.WithReasoning(new FinalSpec("Result 1"), "Finalize 1");
        var exp2Result = experiment2.WithReasoning(new FinalSpec("Result 2"), "Finalize 2");

        // Assert
        mainBranch.Events.Should().HaveCount(1);
        exp1Result.Events.Should().HaveCount(2);
        exp2Result.Events.Should().HaveCount(2);
        exp1Result.Name.Should().Be("experiment1");
        exp2Result.Name.Should().Be("experiment2");
        exp1Result.Store.Should().BeSameAs(experimentalStore1);
        exp2Result.Store.Should().BeSameAs(experimentalStore2);
    }

    #endregion

    #region Mixed Operation Tests

    [Fact]
    public void MixedOperations_CombiningReasoningAndIngest_ShouldMaintainEventOrder()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test", store, source);

        // Act - Mixed operations demonstrating functional composition
        var result = branch
            .WithIngestEvent("source1", new List<string> { "doc1" })
            .WithReasoning(new Draft("Draft 1"), "Prompt 1")
            .WithIngestEvent("source2", new List<string> { "doc2", "doc3" })
            .WithReasoning(new Critique("Critique"), "Prompt 2")
            .WithReasoning(new FinalSpec("Final"), "Prompt 3");

        // Assert
        result.Events.Should().HaveCount(5);
        result.Events[0].Should().BeOfType<IngestBatch>();
        result.Events[1].Should().BeOfType<ReasoningStep>();
        result.Events[2].Should().BeOfType<IngestBatch>();
        result.Events[3].Should().BeOfType<ReasoningStep>();
        result.Events[4].Should().BeOfType<ReasoningStep>();
    }

    [Fact]
    public void ComplexWorkflow_DemonstratesFunctionalPipelineComposition()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/project");

        // Act - Demonstrating a complete reasoning pipeline
        var pipeline = new PipelineBranch("reasoning-pipeline", store, source)
            // Phase 1: Ingestion
            .WithIngestEvent("codebase", new List<string> { "file1.cs", "file2.cs" })
            // Phase 2: Draft generation
            .WithReasoning(
                new Draft("Initial analysis of the codebase shows..."),
                "Analyze the codebase structure")
            // Phase 3: Self-critique
            .WithReasoning(
                new Critique("The analysis lacks depth in error handling..."),
                "Critique the draft analysis")
            // Phase 4: Improvement
            .WithReasoning(
                new Draft("Improved analysis addressing error handling..."),
                "Improve based on critique")
            // Phase 5: Additional context
            .WithIngestEvent("documentation", new List<string> { "readme.md", "spec.md" })
            // Phase 6: Final specification
            .WithReasoning(
                new FinalSpec("Final comprehensive analysis..."),
                "Finalize with documentation context");

        // Assert
        pipeline.Events.Should().HaveCount(6);
        pipeline.Events.OfType<IngestBatch>().Should().HaveCount(2);
        pipeline.Events.OfType<ReasoningStep>().Should().HaveCount(4);
        
        // Verify reasoning progression
        var reasoningSteps = pipeline.Events.OfType<ReasoningStep>().ToList();
        reasoningSteps[0].State.Should().BeOfType<Draft>();
        reasoningSteps[1].State.Should().BeOfType<Critique>();
        reasoningSteps[2].State.Should().BeOfType<Draft>();
        reasoningSteps[3].State.Should().BeOfType<FinalSpec>();
    }

    [Fact]
    public void RecordEquality_BranchesWithSameValues_ShouldBeEqual()
    {
        // Arrange - Records with same store reference, name, source, and events should be equal
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var timestamp = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var events = new List<PipelineEvent>
        {
            new IngestBatch(Guid.Parse("00000000-0000-0000-0000-000000000001"), "source", new List<string> { "id1" }, timestamp)
        };

        // Act - Create same branch twice
        var branch1 = PipelineBranch.WithEvents("test", store, source, events);
        var branch2 = PipelineBranch.WithEvents("test", store, source, events);

        // Assert - Properties should match (note: store ID is generated so full equality won't work)
        branch1.Name.Should().Be(branch2.Name);
        branch1.Source.Should().BeSameAs(branch2.Source);
        branch1.Store.Should().BeSameAs(branch2.Store);
        branch1.Events.Should().Equal(branch2.Events);
    }

    [Fact]
    public void RecordEquality_BranchesWithDifferentNames_ShouldNotBeEqual()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");

        // Act
        var branch1 = new PipelineBranch("branch1", store, source);
        var branch2 = new PipelineBranch("branch2", store, source);

        // Assert
        branch1.Should().NotBe(branch2);
    }

    #endregion

    #region Edge Cases and Boundary Tests

    [Fact]
    public void Events_Property_ShouldBeReadOnly()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test", store, source)
            .WithIngestEvent("source", new List<string> { "id1" });

        // Act & Assert - Events should be IReadOnlyList
        branch.Events.Should().BeAssignableTo<IReadOnlyList<PipelineEvent>>();
        
        // This should not compile (testing at runtime is not possible, but the type system ensures it):
        // branch.Events.Add(...) // Would not compile
    }

    [Fact]
    public void WithReasoning_WithVeryLongPrompt_ShouldHandleCorrectly()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test", store, source);
        var longPrompt = new string('X', 10000);

        // Act
        var newBranch = branch.WithReasoning(new Draft("draft"), longPrompt);

        // Assert
        var reasoningEvent = newBranch.Events.First().Should().BeOfType<ReasoningStep>().Subject;
        reasoningEvent.Prompt.Should().HaveLength(10000);
    }

    [Fact]
    public void WithIngestEvent_WithVeryLargeIdList_ShouldHandleCorrectly()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test", store, source);
        var largeIdList = Enumerable.Range(1, 10000).Select(i => $"id{i}").ToList();

        // Act
        var newBranch = branch.WithIngestEvent("source", largeIdList);

        // Assert
        var ingestEvent = newBranch.Events.First().Should().BeOfType<IngestBatch>().Subject;
        ingestEvent.Ids.Should().HaveCount(10000);
    }

    [Fact]
    public void WithReasoning_WithEmptyPrompt_ShouldCreateEvent()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test", store, source);

        // Act
        var newBranch = branch.WithReasoning(new Draft("draft"), string.Empty);

        // Assert
        var reasoningEvent = newBranch.Events.First().Should().BeOfType<ReasoningStep>().Subject;
        reasoningEvent.Prompt.Should().BeEmpty();
    }

    [Fact]
    public void WithIngestEvent_WithEmptySource_ShouldCreateEvent()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test", store, source);

        // Act
        var newBranch = branch.WithIngestEvent(string.Empty, new List<string> { "id1" });

        // Assert
        var ingestEvent = newBranch.Events.First().Should().BeOfType<IngestBatch>().Subject;
        ingestEvent.Source.Should().BeEmpty();
    }

    [Fact]
    public void MultipleBranches_IndependentEvolution_ShouldNotAffectEachOther()
    {
        // Arrange
        var store1 = new TrackedVectorStore();
        var store2 = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");

        // Act
        var branch1 = new PipelineBranch("branch1", store1, source);
        var branch2 = new PipelineBranch("branch2", store2, source);

        var branch1_v2 = branch1.WithReasoning(new Draft("Draft 1"), "Prompt 1");
        var branch2_v2 = branch2.WithReasoning(new Draft("Draft 2"), "Prompt 2");
        var branch1_v3 = branch1_v2.WithReasoning(new Critique("Critique 1"), "Prompt 3");

        // Assert
        branch1.Events.Should().BeEmpty();
        branch2.Events.Should().BeEmpty();
        branch1_v2.Events.Should().HaveCount(1);
        branch1_v3.Events.Should().HaveCount(2);
        branch2_v2.Events.Should().HaveCount(1);
    }

    #endregion

    #region Functional Programming Law Tests

    [Fact]
    public void Immutability_Law_OperationsReturnNewInstances()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test", store, source);

        // Act
        var branch2 = branch.WithReasoning(new Draft("draft"), "prompt");
        var branch3 = branch.WithIngestEvent("source", new List<string> { "id" });
        var branch4 = branch.WithSource(DataSource.FromPath("/new/path"));

        // Assert - All operations return new instances
        branch.Should().NotBeSameAs(branch2);
        branch.Should().NotBeSameAs(branch3);
        branch.Should().NotBeSameAs(branch4);
        branch2.Should().NotBeSameAs(branch3);
        branch2.Should().NotBeSameAs(branch4);
        branch3.Should().NotBeSameAs(branch4);
    }

    [Fact]
    public void Referential_Transparency_SameInputsProduceSameOutputs()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test", store, source);
        var state = new Draft("draft");
        var prompt = "prompt";

        // Act - Note: This test demonstrates the concept but GUIDs make exact equality impossible
        var branch1 = branch.WithReasoning(state, prompt);
        var branch2 = branch.WithReasoning(state, prompt);

        // Assert - Same inputs, same structure (though IDs differ due to Guid.NewGuid())
        branch1.Events.Should().HaveCount(1);
        branch2.Events.Should().HaveCount(1);
        branch1.Events[0].Should().BeOfType<ReasoningStep>();
        branch2.Events[0].Should().BeOfType<ReasoningStep>();
        
        var step1 = (ReasoningStep)branch1.Events[0];
        var step2 = (ReasoningStep)branch2.Events[0];
        step1.State.Should().BeSameAs(step2.State);
        step1.Prompt.Should().Be(step2.Prompt);
    }

    #endregion

    #region Integration with Other Components

    [Fact]
    public void WithReasoning_WithDocumentRevisionState_ShouldWork()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test", store, source);
        var revisionState = new DocumentRevision("/path/to/doc.md", "revised content", 1, "improve clarity");

        // Act
        var newBranch = branch.WithReasoning(revisionState, "Revise the document");

        // Assert
        var reasoningEvent = newBranch.Events.First().Should().BeOfType<ReasoningStep>().Subject;
        reasoningEvent.State.Should().BeSameAs(revisionState);
        reasoningEvent.StepKind.Should().Be("DocumentRevision");
    }

    [Fact]
    public void PipelineBranch_CanBeUsedInFunctionalChaining_WithLinq()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var initialBranch = new PipelineBranch("test", store, source);

        // Act - Functional composition using LINQ-style chaining
        var finalBranch = new[] { "prompt1", "prompt2", "prompt3" }
            .Aggregate(
                initialBranch,
                (branch, prompt) => branch.WithReasoning(new Draft($"Draft for {prompt}"), prompt));

        // Assert
        finalBranch.Events.Should().HaveCount(3);
        var prompts = finalBranch.Events.OfType<ReasoningStep>().Select(e => e.Prompt).ToList();
        prompts.Should().Equal("prompt1", "prompt2", "prompt3");
    }

    #endregion
}
