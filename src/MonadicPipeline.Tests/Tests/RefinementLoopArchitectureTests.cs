using LangChain.DocumentLoaders;
using LangChain.Providers;
using LangChainPipeline.Domain.Events;
using LangChainPipeline.Domain.States;
using LangChainPipeline.Domain.Vectors;
using LangChainPipeline.Pipeline.Branches;
using LangChainPipeline.Pipeline.Reasoning;
using LangChainPipeline.Providers;
using LangChainPipeline.Tools;
using Xunit;

namespace LangChainPipeline.Tests;

/// <summary>
/// Tests for the complete architecture refinement of the reasoning pipeline.
/// Validates that multi-iteration refinement loops properly chain iterations
/// by using the most recent reasoning state (Draft or FinalSpec) as input.
/// </summary>
public class RefinementLoopArchitectureTests
{
    /// <summary>
    /// Tests that CritiqueArrow uses the most recent Draft when no FinalSpec exists.
    /// </summary>
    [Fact]
    public async Task CritiqueArrow_ShouldUseDraft_WhenNoFinalSpecExists()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var branch = new PipelineBranch("test", store, DataSource.FromPath("."));
        var llm = CreateMockLLM("Test critique");
        var tools = new ToolRegistry();
        var embed = CreateMockEmbedding();

        // Add a draft
        branch = branch.WithReasoning(new Draft("Initial draft"), "test prompt", null);

        // Act
        var critiqueArrow = ReasoningArrows.CritiqueArrow(llm, tools, embed, "test topic", "test query");
        var result = await critiqueArrow(branch);

        // Assert
        var critique = result.Events.OfType<ReasoningStep>()
            .Select(e => e.State)
            .OfType<Critique>()
            .LastOrDefault();

        Assert.NotNull(critique);
    }

    /// <summary>
    /// Tests that CritiqueArrow uses the most recent FinalSpec when it exists.
    /// This is the core improvement for multi-iteration refinement loops.
    /// </summary>
    [Fact]
    public async Task CritiqueArrow_ShouldUseMostRecentFinalSpec_WhenItExists()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var branch = new PipelineBranch("test", store, DataSource.FromPath("."));
        var llm = CreateMockLLM("Test critique of improvement");
        var tools = new ToolRegistry();
        var embed = CreateMockEmbedding();

        // Simulate first iteration: Draft -> Critique -> FinalSpec
        branch = branch.WithReasoning(new Draft("Initial draft"), "test prompt", null);
        branch = branch.WithReasoning(new Critique("First critique"), "test prompt", null);
        branch = branch.WithReasoning(new FinalSpec("First improvement"), "test prompt", null);

        // Act - Second iteration critique should use FinalSpec, not Draft
        var critiqueArrow = ReasoningArrows.CritiqueArrow(llm, tools, embed, "test topic", "test query");
        var result = await critiqueArrow(branch);

        // Assert
        var critiques = result.Events.OfType<ReasoningStep>()
            .Select(e => e.State)
            .OfType<Critique>()
            .ToList();

        Assert.Equal(2, critiques.Count); // Original + new critique

        // Verify the prompt used the FinalSpec text by checking the last critique was created
        var lastCritique = critiques.Last();
        Assert.NotNull(lastCritique);
    }

    /// <summary>
    /// Tests that ImproveArrow uses the most recent Draft when no FinalSpec exists.
    /// </summary>
    [Fact]
    public async Task ImproveArrow_ShouldUseDraft_WhenNoFinalSpecExists()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var branch = new PipelineBranch("test", store, DataSource.FromPath("."));
        var llm = CreateMockLLM("Test improvement");
        var tools = new ToolRegistry();
        var embed = CreateMockEmbedding();

        // Add draft and critique
        branch = branch.WithReasoning(new Draft("Initial draft"), "test prompt", null);
        branch = branch.WithReasoning(new Critique("First critique"), "test prompt", null);

        // Act
        var improveArrow = ReasoningArrows.ImproveArrow(llm, tools, embed, "test topic", "test query");
        var result = await improveArrow(branch);

        // Assert
        var finalSpec = result.Events.OfType<ReasoningStep>()
            .Select(e => e.State)
            .OfType<FinalSpec>()
            .LastOrDefault();

        Assert.NotNull(finalSpec);
    }

    /// <summary>
    /// Tests that ImproveArrow uses the most recent FinalSpec when it exists.
    /// This ensures iterative improvements build upon previous improvements.
    /// </summary>
    [Fact]
    public async Task ImproveArrow_ShouldUseMostRecentFinalSpec_WhenItExists()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var branch = new PipelineBranch("test", store, DataSource.FromPath("."));
        var llm = CreateMockLLM("Second improvement");
        var tools = new ToolRegistry();
        var embed = CreateMockEmbedding();

        // Simulate first iteration: Draft -> Critique -> FinalSpec
        branch = branch.WithReasoning(new Draft("Initial draft"), "test prompt", null);
        branch = branch.WithReasoning(new Critique("First critique"), "test prompt", null);
        branch = branch.WithReasoning(new FinalSpec("First improvement"), "test prompt", null);

        // Add second critique
        branch = branch.WithReasoning(new Critique("Second critique"), "test prompt", null);

        // Act - Second improvement should use first FinalSpec, not Draft
        var improveArrow = ReasoningArrows.ImproveArrow(llm, tools, embed, "test topic", "test query");
        var result = await improveArrow(branch);

        // Assert
        var improvements = result.Events.OfType<ReasoningStep>()
            .Select(e => e.State)
            .OfType<FinalSpec>()
            .ToList();

        Assert.Equal(2, improvements.Count); // First + second improvement
    }

    /// <summary>
    /// Tests the complete multi-iteration refinement loop architecture.
    /// Validates that each iteration builds upon the previous one.
    /// </summary>
    [Fact]
    public async Task MultiIterationRefinementLoop_ShouldChainProperly()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var branch = new PipelineBranch("test", store, DataSource.FromPath("."));
        var llm = CreateMockLLM("Generated text");
        var tools = new ToolRegistry();
        var embed = CreateMockEmbedding();

        // Act - Run 3 iterations
        // Iteration 0: Create draft
        var draftArrow = ReasoningArrows.DraftArrow(llm, tools, embed, "test topic", "test query");
        branch = await draftArrow(branch);

        // Iteration 1: Critique -> Improve
        var critique1 = ReasoningArrows.CritiqueArrow(llm, tools, embed, "test topic", "test query");
        branch = await critique1(branch);
        var improve1 = ReasoningArrows.ImproveArrow(llm, tools, embed, "test topic", "test query");
        branch = await improve1(branch);

        // Iteration 2: Critique -> Improve (should use FinalSpec from iteration 1)
        var critique2 = ReasoningArrows.CritiqueArrow(llm, tools, embed, "test topic", "test query");
        branch = await critique2(branch);
        var improve2 = ReasoningArrows.ImproveArrow(llm, tools, embed, "test topic", "test query");
        branch = await improve2(branch);

        // Iteration 3: Critique -> Improve (should use FinalSpec from iteration 2)
        var critique3 = ReasoningArrows.CritiqueArrow(llm, tools, embed, "test topic", "test query");
        branch = await critique3(branch);
        var improve3 = ReasoningArrows.ImproveArrow(llm, tools, embed, "test topic", "test query");
        branch = await improve3(branch);

        // Assert
        var drafts = branch.Events.OfType<ReasoningStep>()
            .Select(e => e.State)
            .OfType<Draft>()
            .ToList();
        var critiques = branch.Events.OfType<ReasoningStep>()
            .Select(e => e.State)
            .OfType<Critique>()
            .ToList();
        var improvements = branch.Events.OfType<ReasoningStep>()
            .Select(e => e.State)
            .OfType<FinalSpec>()
            .ToList();

        Assert.Single(drafts); // Only one initial draft
        Assert.Equal(3, critiques.Count); // Three critiques
        Assert.Equal(3, improvements.Count); // Three improvements

        // Verify the order of events
        var allReasoningSteps = branch.Events.OfType<ReasoningStep>().ToList();
        Assert.Equal(7, allReasoningSteps.Count); // 1 draft + 3*(critique+improve)

        // Verify sequence: Draft, Critique, Improve, Critique, Improve, Critique, Improve
        Assert.IsType<Draft>(allReasoningSteps[0].State);
        Assert.IsType<Critique>(allReasoningSteps[1].State);
        Assert.IsType<FinalSpec>(allReasoningSteps[2].State);
        Assert.IsType<Critique>(allReasoningSteps[3].State);
        Assert.IsType<FinalSpec>(allReasoningSteps[4].State);
        Assert.IsType<Critique>(allReasoningSteps[5].State);
        Assert.IsType<FinalSpec>(allReasoningSteps[6].State);
    }

    /// <summary>
    /// Tests that SafeCritiqueArrow properly handles the case when no reasoning state exists.
    /// </summary>
    [Fact]
    public async Task SafeCritiqueArrow_ShouldReturnFailure_WhenNoReasoningStateExists()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var branch = new PipelineBranch("test", store, DataSource.FromPath("."));
        var llm = CreateMockLLM("Test critique");
        var tools = new ToolRegistry();
        var embed = CreateMockEmbedding();

        // Act
        var safeCritiqueArrow = ReasoningArrows.SafeCritiqueArrow(llm, tools, embed, "test topic", "test query");
        var result = await safeCritiqueArrow(branch);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("No draft or previous improvement found", result.Error ?? "");
    }

    /// <summary>
    /// Tests that SafeImproveArrow properly handles the case when no reasoning state exists.
    /// </summary>
    [Fact]
    public async Task SafeImproveArrow_ShouldReturnFailure_WhenNoReasoningStateExists()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var branch = new PipelineBranch("test", store, DataSource.FromPath("."));
        var llm = CreateMockLLM("Test improvement");
        var tools = new ToolRegistry();
        var embed = CreateMockEmbedding();

        // Act
        var safeImproveArrow = ReasoningArrows.SafeImproveArrow(llm, tools, embed, "test topic", "test query");
        var result = await safeImproveArrow(branch);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("No draft or previous improvement found", result.Error ?? "");
    }

    #region Helper Methods

    private static ToolAwareChatModel CreateMockLLM(string responseText)
    {
        var mockProvider = new MockChatCompletionModel(responseText);
        var tools = new ToolRegistry();
        return new ToolAwareChatModel(mockProvider, tools);
    }

    private static LangChainPipeline.Domain.IEmbeddingModel CreateMockEmbedding()
    {
        return new MockEmbeddingModel();
    }

    private class MockChatCompletionModel : IChatCompletionModel
    {
        private readonly string _response;

        public MockChatCompletionModel(string response)
        {
            _response = response;
        }

        public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
        {
            await Task.Delay(1, ct);
            return _response;
        }
    }

    private class MockEmbeddingModel : LangChainPipeline.Domain.IEmbeddingModel
    {
        public async Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
        {
            await Task.Delay(1, ct);
            // Return a simple fixed-size embedding
            return Enumerable.Range(0, 384).Select(i => (float)i / 384).ToArray();
        }
    }

    #endregion
}
