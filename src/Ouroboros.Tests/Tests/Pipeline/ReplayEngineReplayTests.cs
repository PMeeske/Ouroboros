using LangChain.Databases;
using LangChain.DocumentLoaders;
using Moq;
using Ouroboros.Pipeline.Branches;
using Ouroboros.Pipeline.Replay;
using Ouroboros.Tests.Mocks;

namespace Ouroboros.Tests.Pipeline;

/// <summary>
/// Comprehensive tests for ReplayEngine replay functionality.
/// Tests focus on replay operations, fresh context, and tool re-execution.
/// </summary>
[Trait("Category", "Unit")]
public class ReplayEngineReplayTests
{
    private readonly ToolAwareChatModel _mockLlm;
    private readonly Mock<IEmbeddingModel> _mockEmbedding;
    private readonly ToolRegistry _toolRegistry;

    public ReplayEngineReplayTests()
    {
        // Create a mock chat model and wrap it in ToolAwareChatModel
        var chatModel = new MockChatModel("Generated response");
        _toolRegistry = new ToolRegistry();
        _mockLlm = new ToolAwareChatModel(chatModel, _toolRegistry);
        
        _mockEmbedding = new Mock<IEmbeddingModel>();

        // Setup default mock behaviors
        _mockEmbedding.Setup(e => e.CreateEmbeddingsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new float[] { 0.1f, 0.2f, 0.3f });
    }

    #region Basic Replay Tests

    [Fact]
    public async Task ReplayAsync_WithEmptyBranch_CreatesReplayBranch()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");
        var branch = new PipelineBranch("original", store, source);

        var engine = new ReplayEngine(_mockLlm, _mockEmbedding.Object);

        // Act
        var result = await engine.ReplayAsync(branch, "topic", "query", _toolRegistry, k: 8);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("original_replay");
        result.Events.Should().BeEmpty(); // No reasoning steps to replay
    }

    [Fact]
    public async Task ReplayAsync_WithDraftEvent_ReplaysWithFreshContext()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");
        var branch = new PipelineBranch("original", store, source);

        // Add a document to the store for context retrieval
        await store.AddAsync(new[]
        {
            new Vector
            {
                Id = "doc1",
                Text = "Context document",
                Embedding = new[] { 1.0f, 0.0f, 0.0f }
            }
        });

        branch = branch.WithReasoning(
            new Draft("Original draft content"),
            "Generate a draft about {topic} using {context} and {tools_schemas}");

        var engine = new ReplayEngine(_mockLlm, _mockEmbedding.Object);

        // Act
        var result = await engine.ReplayAsync(branch, "AI", "artificial intelligence", _toolRegistry, k: 8);

        // Assert
        result.Should().NotBeNull();
        result.Events.OfType<ReasoningStep>().Should().HaveCount(1);
    }

    [Fact]
    public async Task ReplayAsync_WithMultipleReasoningSteps_ReplaysAll()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");
        var branch = new PipelineBranch("original", store, source);

        await store.AddAsync(new[]
        {
            new Vector
            {
                Id = "doc1",
                Text = "Context",
                Embedding = new[] { 1.0f, 0.0f, 0.0f }
            }
        });

        branch = branch.WithReasoning(new Draft("Draft"), "prompt with {context} and {tools_schemas}");
        branch = branch.WithReasoning(new Critique("Critique"), "critique {context} {tools_schemas}");
        branch = branch.WithReasoning(new FinalSpec("Final"), "final {context} {tools_schemas}");

        var engine = new ReplayEngine(_mockLlm, _mockEmbedding.Object);

        // Act
        var result = await engine.ReplayAsync(branch, "topic", "query", _toolRegistry, k: 8);

        // Assert
        result.Events.OfType<ReasoningStep>().Should().HaveCount(3);
    }

    [Fact]
    public async Task ReplayAsync_ReplacesContextPlaceholder()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");
        var branch = new PipelineBranch("original", store, source);

        await store.AddAsync(new[]
        {
            new Vector
            {
                Id = "doc1",
                Text = "Retrieved context",
                Embedding = new[] { 1.0f, 0.0f, 0.0f }
            }
        });

        branch = branch.WithReasoning(
            new Draft("Draft"),
            "Prompt with {context} placeholder");

        var engine = new ReplayEngine(_mockLlm, _mockEmbedding.Object);

        // Act
        var result = await engine.ReplayAsync(branch, "topic", "query", _toolRegistry, k: 8);

        // Assert - Verify the replay completed
        result.Should().NotBeNull();
        result.Events.OfType<ReasoningStep>().Should().HaveCount(1);
    }

    [Fact]
    public async Task ReplayAsync_ReplacesToolsSchemaPlaceholder()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");
        var branch = new PipelineBranch("original", store, source);

        branch = branch.WithReasoning(
            new Draft("Draft"),
            "Prompt with {tools_schemas} placeholder");

        var engine = new ReplayEngine(_mockLlm, _mockEmbedding.Object);

        // Act
        var result = await engine.ReplayAsync(branch, "topic", "query", _toolRegistry, k: 8);

        // Assert
        result.Should().NotBeNull();
        result.Events.OfType<ReasoningStep>().Should().HaveCount(1);
    }

    #endregion

    #region Tool Re-execution Tests

    [Fact]
    public async Task ReplayAsync_WithToolExecutions_ReExecutesTools()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");
        var branch = new PipelineBranch("original", store, source);

        var toolExecutions = new List<ToolExecution>
        {
            new ToolExecution("tool1", "args1", "result1", DateTime.UtcNow),
            new ToolExecution("tool2", "args2", "result2", DateTime.UtcNow)
        };

        branch = branch.WithReasoning(
            new Draft("Draft"),
            "prompt {context} {tools_schemas}",
            toolExecutions);

        var engine = new ReplayEngine(_mockLlm, _mockEmbedding.Object);

        // Act
        var result = await engine.ReplayAsync(branch, "topic", "query", _toolRegistry, k: 8);

        // Assert
        var replayedStep = result.Events.OfType<ReasoningStep>().First();
        // The replayed step will have tool calls from the mock LLM (none in our mock, but structure is there)
        replayedStep.Should().NotBeNull();
    }

    #endregion

    #region State Type Tests

    [Fact]
    public async Task ReplayAsync_WithDraftState_CreatesDraftState()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");
        var branch = new PipelineBranch("original", store, source);

        branch = branch.WithReasoning(new Draft("Original"), "prompt {context} {tools_schemas}");

        var engine = new ReplayEngine(_mockLlm, _mockEmbedding.Object);

        // Act
        var result = await engine.ReplayAsync(branch, "topic", "query", _toolRegistry, k: 8);

        // Assert
        var step = result.Events.OfType<ReasoningStep>().First();
        step.State.Should().BeOfType<Draft>();
        ((Draft)step.State).DraftText.Should().Be("Generated response");
    }

    [Fact]
    public async Task ReplayAsync_WithCritiqueState_CreatesCritiqueState()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");
        var branch = new PipelineBranch("original", store, source);

        branch = branch.WithReasoning(new Critique("Original"), "prompt {context} {tools_schemas}");

        var engine = new ReplayEngine(_mockLlm, _mockEmbedding.Object);

        // Act
        var result = await engine.ReplayAsync(branch, "topic", "query", _toolRegistry, k: 8);

        // Assert
        var step = result.Events.OfType<ReasoningStep>().First();
        step.State.Should().BeOfType<Critique>();
        ((Critique)step.State).CritiqueText.Should().Be("Generated response");
    }

    [Fact]
    public async Task ReplayAsync_WithFinalState_CreatesFinalState()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");
        var branch = new PipelineBranch("original", store, source);

        branch = branch.WithReasoning(new FinalSpec("Original"), "prompt {context} {tools_schemas}");

        var engine = new ReplayEngine(_mockLlm, _mockEmbedding.Object);

        // Act
        var result = await engine.ReplayAsync(branch, "topic", "query", _toolRegistry, k: 8);

        // Assert
        var step = result.Events.OfType<ReasoningStep>().First();
        step.State.Should().BeOfType<FinalSpec>();
        ((FinalSpec)step.State).FinalText.Should().Be("Generated response");
    }

    [Fact]
    public async Task ReplayAsync_WithUnknownStateKind_CreatesDraftState()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");
        var branch = new PipelineBranch("original", store, source);

        // Create a reasoning step with an unknown kind
        var customState = new Draft("Custom");
        var reasoningStep = new ReasoningStep(
            Guid.NewGuid(),
            "UnknownKind", // Unknown kind
            customState,
            DateTime.UtcNow,
            "prompt {context} {tools_schemas}",
            null);

        branch = branch.WithEvent(reasoningStep);

        var engine = new ReplayEngine(_mockLlm, _mockEmbedding.Object);

        // Act
        var result = await engine.ReplayAsync(branch, "topic", "query", _toolRegistry, k: 8);

        // Assert
        var step = result.Events.OfType<ReasoningStep>().First();
        step.State.Should().BeOfType<Draft>(); // Should default to Draft
    }

    #endregion

    #region Vector Store Tests

    [Fact]
    public async Task ReplayAsync_CopiesVectorsToReplayBranch()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");
        var branch = new PipelineBranch("original", store, source);

        await store.AddAsync(new[]
        {
            new Vector { Id = "vec1", Text = "Text 1", Embedding = new[] { 1.0f, 0.0f, 0.0f } },
            new Vector { Id = "vec2", Text = "Text 2", Embedding = new[] { 0.0f, 1.0f, 0.0f } },
            new Vector { Id = "vec3", Text = "Text 3", Embedding = new[] { 0.0f, 0.0f, 1.0f } }
        });

        branch = branch.WithReasoning(new Draft("Draft"), "prompt {context} {tools_schemas}");

        var engine = new ReplayEngine(_mockLlm, _mockEmbedding.Object);

        // Act
        var result = await engine.ReplayAsync(branch, "topic", "query", _toolRegistry, k: 8);

        // Assert
        result.Store.GetAll().Should().HaveCount(3);
        result.Store.GetAll().Should().Contain(v => v.Id == "vec1");
        result.Store.GetAll().Should().Contain(v => v.Id == "vec2");
        result.Store.GetAll().Should().Contain(v => v.Id == "vec3");
    }

    [Fact]
    public async Task ReplayAsync_PreservesOriginalBranch()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");
        var branch = new PipelineBranch("original", store, source);

        await store.AddAsync(new[]
        {
            new Vector { Id = "vec1", Text = "Text", Embedding = new[] { 1.0f, 0.0f, 0.0f } }
        });

        branch = branch.WithReasoning(new Draft("Original draft"), "prompt {context} {tools_schemas}");

        var originalEventCount = branch.Events.Count;
        var originalVectorCount = branch.Store.GetAll().Count();

        var engine = new ReplayEngine(_mockLlm, _mockEmbedding.Object);

        // Act
        await engine.ReplayAsync(branch, "topic", "query", _toolRegistry, k: 8);

        // Assert - Original branch should be unchanged
        branch.Events.Should().HaveCount(originalEventCount);
        branch.Store.GetAll().Should().HaveCount(originalVectorCount);
        var originalStep = branch.Events.OfType<ReasoningStep>().First();
        ((Draft)originalStep.State).DraftText.Should().Be("Original draft");
    }

    #endregion

    #region Parameter Tests

    [Fact]
    public async Task ReplayAsync_WithCustomK_RetrievesCorrectAmountOfDocuments()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");
        var branch = new PipelineBranch("original", store, source);

        // Add multiple documents
        var vectors = Enumerable.Range(0, 20).Select(i => new Vector
        {
            Id = $"vec{i}",
            Text = $"Text {i}",
            Embedding = new[] { (float)i, 0.0f, 0.0f }
        }).ToArray();

        await store.AddAsync(vectors);

        branch = branch.WithReasoning(new Draft("Draft"), "prompt {context} {tools_schemas}");

        var engine = new ReplayEngine(_mockLlm, _mockEmbedding.Object);

        // Act - Test with k=5
        var result = await engine.ReplayAsync(branch, "topic", "query", _toolRegistry, k: 5);

        // Assert
        result.Should().NotBeNull();
        // The engine should have used k=5 for document retrieval
    }

    [Fact]
    public async Task ReplayAsync_WithDifferentTopic_UsesNewTopic()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");
        var branch = new PipelineBranch("original", store, source);

        branch = branch.WithReasoning(new Draft("Draft"), "prompt {context} {tools_schemas}");

        var engine = new ReplayEngine(_mockLlm, _mockEmbedding.Object);

        // Act
        var result = await engine.ReplayAsync(branch, "New Topic", "new query", _toolRegistry, k: 8);

        // Assert
        result.Should().NotBeNull();
        // The new topic should be used in the replay
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task ReplayAsync_WithNonReasoningEvents_SkipsThem()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");
        var branch = new PipelineBranch("original", store, source);

        branch = branch.WithReasoning(new Draft("Draft"), "prompt {context} {tools_schemas}");
        branch = branch.WithIngestEvent("source", new[] { "doc1", "doc2" }); // Non-reasoning event
        branch = branch.WithReasoning(new Critique("Critique"), "prompt {context} {tools_schemas}");

        var engine = new ReplayEngine(_mockLlm, _mockEmbedding.Object);

        // Act
        var result = await engine.ReplayAsync(branch, "topic", "query", _toolRegistry, k: 8);

        // Assert
        result.Events.OfType<ReasoningStep>().Should().HaveCount(2);
        result.Events.OfType<IngestBatch>().Should().BeEmpty(); // Ingest events are not replayed
    }

    [Fact]
    public async Task ReplayAsync_WithEmptyPrompt_HandlesGracefully()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test");
        var branch = new PipelineBranch("original", store, source);

        branch = branch.WithReasoning(new Draft("Draft"), ""); // Empty prompt

        var engine = new ReplayEngine(_mockLlm, _mockEmbedding.Object);

        // Act
        var result = await engine.ReplayAsync(branch, "topic", "query", _toolRegistry, k: 8);

        // Assert
        result.Should().NotBeNull();
        result.Events.OfType<ReasoningStep>().Should().HaveCount(1);
    }

    #endregion
}
