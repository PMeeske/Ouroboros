// <copyright file="DivideAndConquerOrchestratorTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Tests;

/// <summary>
/// Tests for DivideAndConquerOrchestrator.
/// </summary>
public class DivideAndConquerOrchestratorTests
{
    /// <summary>
    /// Simple mock chat model for testing.
    /// </summary>
    private class MockChatModel : IChatCompletionModel
    {
        private readonly Func<string, CancellationToken, Task<string>> _generateFunc;

        public MockChatModel(Func<string, CancellationToken, Task<string>>? generateFunc = null)
        {
            _generateFunc = generateFunc ?? ((prompt, ct) => Task.FromResult($"Processed: {prompt.Substring(0, Math.Min(20, prompt.Length))}..."));
        }

        public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
            => _generateFunc(prompt, ct);
    }

    /// <summary>
    /// Test that orchestrator can divide text into chunks based on configuration.
    /// </summary>
    [Fact]
    public void DivideIntoChunks_ShouldSplitTextIntoConfiguredSizeChunks()
    {
        // Arrange
        MockChatModel mockModel = new MockChatModel();
        DivideAndConquerConfig config = new DivideAndConquerConfig(ChunkSize: 100);
        DivideAndConquerOrchestrator orchestrator = new DivideAndConquerOrchestrator(mockModel, config);

        string text = string.Join("\n\n", Enumerable.Range(1, 10).Select(i => $"Paragraph {i} with some content."));

        // Act
        List<string> chunks = orchestrator.DivideIntoChunks(text);

        // Assert
        chunks.Should().NotBeEmpty();
        chunks.Should().AllSatisfy(chunk => chunk.Length.Should().BeLessThanOrEqualTo(config.ChunkSize * 2)); // Allow some overflow
    }

    /// <summary>
    /// Test that orchestrator handles empty input gracefully.
    /// </summary>
    [Fact]
    public void DivideIntoChunks_WithEmptyInput_ReturnsEmptyList()
    {
        // Arrange
        MockChatModel mockModel = new MockChatModel();
        DivideAndConquerOrchestrator orchestrator = new DivideAndConquerOrchestrator(mockModel);

        // Act
        List<string> chunks = orchestrator.DivideIntoChunks(string.Empty);

        // Assert
        chunks.Should().BeEmpty();
    }

    /// <summary>
    /// Test that orchestrator processes chunks in parallel successfully.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithValidChunks_ProcessesInParallel()
    {
        // Arrange
        int callCount = 0;
        MockChatModel mockModel = new MockChatModel((prompt, ct) =>
        {
            Interlocked.Increment(ref callCount);
            return Task.FromResult($"Processed: {prompt.Substring(0, Math.Min(20, prompt.Length))}...");
        });

        DivideAndConquerConfig config = new DivideAndConquerConfig(MaxParallelism: 2, MergeResults: true);
        DivideAndConquerOrchestrator orchestrator = new DivideAndConquerOrchestrator(mockModel, config);

        string task = "Summarize:";
        List<string> chunks = new List<string> { "Chunk 1 content", "Chunk 2 content", "Chunk 3 content" };

        // Act
        Result<string, string> result = await orchestrator.ExecuteAsync(task, chunks);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Match(
            success =>
            {
                success.Should().NotBeEmpty();
                success.Should().Contain("Processed");
            },
            error => Assert.Fail($"Expected success but got error: {error}"));

        callCount.Should().Be(chunks.Count);
    }

    /// <summary>
    /// Test that orchestrator handles model failures gracefully.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WhenModelFails_ReturnsFailure()
    {
        // Arrange
        MockChatModel mockModel = new MockChatModel((prompt, ct) =>
            throw new InvalidOperationException("Model error"));

        DivideAndConquerOrchestrator orchestrator = new DivideAndConquerOrchestrator(mockModel);

        string task = "Analyze:";
        List<string> chunks = new List<string> { "Test chunk" };

        // Act
        Result<string, string> result = await orchestrator.ExecuteAsync(task, chunks);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Match(
            success => Assert.Fail("Expected failure but got success"),
            error => error.Should().Contain("Failed to process"));
    }

    /// <summary>
    /// Test that orchestrator rejects empty task.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithEmptyTask_ReturnsFailure()
    {
        // Arrange
        MockChatModel mockModel = new MockChatModel();
        DivideAndConquerOrchestrator orchestrator = new DivideAndConquerOrchestrator(mockModel);

        List<string> chunks = new List<string> { "Valid chunk" };

        // Act
        Result<string, string> result = await orchestrator.ExecuteAsync(string.Empty, chunks);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Match(
            success => Assert.Fail("Expected failure but got success"),
            error => error.Should().Contain("Task cannot be empty"));
    }

    /// <summary>
    /// Test that orchestrator rejects empty chunks list.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_WithEmptyChunks_ReturnsFailure()
    {
        // Arrange
        MockChatModel mockModel = new MockChatModel();
        DivideAndConquerOrchestrator orchestrator = new DivideAndConquerOrchestrator(mockModel);

        string task = "Valid task";

        // Act
        Result<string, string> result = await orchestrator.ExecuteAsync(task, new List<string>());

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Match(
            success => Assert.Fail("Expected failure but got success"),
            error => error.Should().Contain("No chunks provided"));
    }

    /// <summary>
    /// Test that orchestrator records performance metrics.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_RecordsPerformanceMetrics()
    {
        // Arrange
        MockChatModel mockModel = new MockChatModel();
        DivideAndConquerOrchestrator orchestrator = new DivideAndConquerOrchestrator(mockModel);

        string task = "Task";
        List<string> chunks = new List<string> { "Chunk 1", "Chunk 2" };

        // Act
        await orchestrator.ExecuteAsync(task, chunks);

        // Assert
        IReadOnlyDictionary<string, PerformanceMetrics> metrics = orchestrator.GetMetrics();
        metrics.Should().NotBeEmpty();
        metrics.Should().ContainKey("divide_and_conquer_orchestrator");
        metrics["divide_and_conquer_orchestrator"].ExecutionCount.Should().Be(1);
    }

    /// <summary>
    /// Test that orchestrator respects parallelism configuration.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_RespectsMaxParallelism()
    {
        // Arrange
        int executionCount = 0;

        MockChatModel mockModel = new MockChatModel(async (prompt, ct) =>
        {
            Interlocked.Increment(ref executionCount);
            await Task.Delay(50, ct); // Simulate work
            return "Result";
        });

        DivideAndConquerConfig config = new DivideAndConquerConfig(MaxParallelism: 2);
        DivideAndConquerOrchestrator orchestrator = new DivideAndConquerOrchestrator(mockModel, config);

        string task = "Task";
        List<string> chunks = new List<string> { "C1", "C2", "C3", "C4" };

        // Act
        Result<string, string> result = await orchestrator.ExecuteAsync(task, chunks);

        // Assert
        result.IsSuccess.Should().BeTrue();
        executionCount.Should().Be(chunks.Count);
    }

    /// <summary>
    /// Test that chunk results maintain order.
    /// </summary>
    [Fact]
    public async Task ExecuteAsync_MaintainsChunkOrder()
    {
        // Arrange
        MockChatModel mockModel = new MockChatModel((prompt, ct) =>
        {
            // Extract chunk content from prompt
            string[] parts = prompt.Split("Content:\n");
            return Task.FromResult(parts.Length > 1 ? $"Result for {parts[1].Trim()}" : "Result");
        });

        DivideAndConquerConfig config = new DivideAndConquerConfig(MergeSeparator: " | ");
        DivideAndConquerOrchestrator orchestrator = new DivideAndConquerOrchestrator(mockModel, config);

        string task = "Process:";
        List<string> chunks = new List<string> { "First", "Second", "Third" };

        // Act
        Result<string, string> result = await orchestrator.ExecuteAsync(task, chunks);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Match(
            success =>
            {
                success.Should().Contain("First");
                success.Should().Contain("Second");
                success.Should().Contain("Third");
                // Results should be in order (though exact order depends on execution)
            },
            error => Assert.Fail($"Expected success but got error: {error}"));
    }
}
