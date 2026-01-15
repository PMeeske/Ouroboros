// <copyright file="RecursiveChunkProcessorTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests;

using FluentAssertions;
using Ouroboros.Core.Monads;
using Ouroboros.Core.Processing;
using Xunit;

/// <summary>
/// Tests for RecursiveChunkProcessor functionality.
/// </summary>
[Trait("Category", "Unit")]
public class RecursiveChunkProcessorTests
{
    [Fact]
    public async Task ProcessLargeContextAsync_WithSmallContext_ProcessesSuccessfully()
    {
        // Arrange
        var processedChunks = new List<string>();

        Func<string, Task<Result<string>>> processFunc = async chunk =>
        {
            await Task.Delay(10); // Simulate processing
            processedChunks.Add(chunk);
            return Result<string>.Success($"Processed: {chunk.Substring(0, Math.Min(20, chunk.Length))}...");
        };

        Func<IEnumerable<string>, Task<Result<string>>> combineFunc = async chunks =>
        {
            await Task.CompletedTask;
            return Result<string>.Success(string.Join(" | ", chunks));
        };

        var processor = new RecursiveChunkProcessor(processFunc, combineFunc);
        var input = "This is a small test input for processing.";

        // Act
        var result = await processor.ProcessLargeContextAsync<string, string>(
            input,
            maxChunkSize: 512,
            strategy: ChunkingStrategy.Fixed);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrEmpty();
        processedChunks.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ProcessLargeContextAsync_WithLargeContext_SplitsIntoChunks()
    {
        // Arrange
        var processedChunks = new List<string>();

        Func<string, Task<Result<string>>> processFunc = async chunk =>
        {
            await Task.Delay(10);
            processedChunks.Add(chunk);
            return Result<string>.Success($"Chunk {processedChunks.Count}");
        };

        Func<IEnumerable<string>, Task<Result<string>>> combineFunc = async chunks =>
        {
            await Task.CompletedTask;
            var combined = $"Combined {chunks.Count()} chunks";
            return Result<string>.Success(combined);
        };

        var processor = new RecursiveChunkProcessor(processFunc, combineFunc);

        // Create a large context (approximately 2000 tokens worth of text)
        var largeInput = string.Join(" ", Enumerable.Repeat("This is a test sentence.", 500));

        // Act
        var result = await processor.ProcessLargeContextAsync<string, string>(
            largeInput,
            maxChunkSize: 256,  // Small chunks to force splitting
            strategy: ChunkingStrategy.Fixed);

        // Assert
        result.IsSuccess.Should().BeTrue();
        processedChunks.Count.Should().BeGreaterThan(1, "large context should be split into multiple chunks");
    }

    [Fact]
    public async Task ProcessLargeContextAsync_WithFixedStrategy_UsesConsistentChunkSize()
    {
        // Arrange
        var chunkSizes = new List<int>();

        Func<string, Task<Result<string>>> processFunc = async chunk =>
        {
            await Task.Delay(5);
            chunkSizes.Add(chunk.Length);
            return Result<string>.Success("processed");
        };

        Func<IEnumerable<string>, Task<Result<string>>> combineFunc = async chunks =>
        {
            await Task.CompletedTask;
            return Result<string>.Success("combined");
        };

        var processor = new RecursiveChunkProcessor(processFunc, combineFunc);
        var input = string.Join(" ", Enumerable.Repeat("Word.", 1000));

        // Act
        var result = await processor.ProcessLargeContextAsync<string, string>(
            input,
            maxChunkSize: 256,
            strategy: ChunkingStrategy.Fixed);

        // Assert
        result.IsSuccess.Should().BeTrue();
        chunkSizes.Should().NotBeEmpty();

        // All chunks should be similar in size (allowing for boundary conditions)
        var avgSize = chunkSizes.Average();
        chunkSizes.Should().AllSatisfy(size =>
            Math.Abs(size - avgSize).Should().BeLessThan(avgSize * 0.5));
    }

    [Fact]
    public async Task ProcessLargeContextAsync_WithAdaptiveStrategy_LearnsFromSuccess()
    {
        // Arrange
        var callCount = 0;

        Func<string, Task<Result<string>>> processFunc = async chunk =>
        {
            await Task.Delay(5);
            callCount++;
            return Result<string>.Success("success");
        };

        Func<IEnumerable<string>, Task<Result<string>>> combineFunc = async chunks =>
        {
            await Task.CompletedTask;
            return Result<string>.Success("combined");
        };

        var processor = new RecursiveChunkProcessor(processFunc, combineFunc);
        var input = string.Join(" ", Enumerable.Repeat("Test.", 500));

        // Act - First call to establish baseline
        var result1 = await processor.ProcessLargeContextAsync<string, string>(
            input,
            maxChunkSize: 512,
            strategy: ChunkingStrategy.Adaptive);

        // Second call should potentially use learned optimal chunk size
        var result2 = await processor.ProcessLargeContextAsync<string, string>(
            input,
            maxChunkSize: 512,
            strategy: ChunkingStrategy.Adaptive);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        callCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ProcessLargeContextAsync_WhenChunkProcessingFails_ReturnsFailure()
    {
        // Arrange
        var failOnSecondChunk = 0;

        Func<string, Task<Result<string>>> processFunc = async chunk =>
        {
            await Task.Delay(5);
            failOnSecondChunk++;
            if (failOnSecondChunk == 2)
            {
                return Result<string>.Failure("Simulated chunk processing failure");
            }

            return Result<string>.Success("success");
        };

        Func<IEnumerable<string>, Task<Result<string>>> combineFunc = async chunks =>
        {
            await Task.CompletedTask;
            return Result<string>.Success("combined");
        };

        var processor = new RecursiveChunkProcessor(processFunc, combineFunc);
        var input = string.Join(" ", Enumerable.Repeat("Test.", 500));

        // Act
        var result = await processor.ProcessLargeContextAsync<string, string>(
            input,
            maxChunkSize: 256,  // Force multiple chunks
            strategy: ChunkingStrategy.Fixed);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to process");
    }

    [Fact]
    public async Task ProcessLargeContextAsync_WhenCombineFails_ReturnsFailure()
    {
        // Arrange
        Func<string, Task<Result<string>>> processFunc = async chunk =>
        {
            await Task.Delay(5);
            return Result<string>.Success("processed");
        };

        Func<IEnumerable<string>, Task<Result<string>>> combineFunc = async chunks =>
        {
            await Task.Delay(5);
            return Result<string>.Failure("Simulated combine failure");
        };

        var processor = new RecursiveChunkProcessor(processFunc, combineFunc);
        var input = string.Join(" ", Enumerable.Repeat("Test.", 200));

        // Act
        var result = await processor.ProcessLargeContextAsync<string, string>(
            input,
            maxChunkSize: 256,
            strategy: ChunkingStrategy.Fixed);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Failed to combine");
    }

    [Fact]
    public async Task ProcessLargeContextAsync_WithCancellation_ReturnsCancelledError()
    {
        // Arrange
        var cts = new CancellationTokenSource();

        Func<string, Task<Result<string>>> processFunc = async chunk =>
        {
            await Task.Delay(100); // Longer delay to allow cancellation
            return Result<string>.Success("processed");
        };

        Func<IEnumerable<string>, Task<Result<string>>> combineFunc = async chunks =>
        {
            await Task.CompletedTask;
            return Result<string>.Success("combined");
        };

        var processor = new RecursiveChunkProcessor(processFunc, combineFunc);
        var input = string.Join(" ", Enumerable.Repeat("Test.", 500));

        // Act
        cts.CancelAfter(50); // Cancel quickly
        var result = await processor.ProcessLargeContextAsync<string, string>(
            input,
            maxChunkSize: 256,
            strategy: ChunkingStrategy.Fixed,
            cancellationToken: cts.Token);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cancelled");
    }

    [Fact]
    public async Task ProcessLargeContextAsync_WithNonStringInput_ReturnsFailure()
    {
        // Arrange
        Func<string, Task<Result<string>>> processFunc = async chunk =>
        {
            await Task.CompletedTask;
            return Result<string>.Success("processed");
        };

        Func<IEnumerable<string>, Task<Result<string>>> combineFunc = async chunks =>
        {
            await Task.CompletedTask;
            return Result<string>.Success("combined");
        };

        var processor = new RecursiveChunkProcessor(processFunc, combineFunc);

        // Act
        var result = await processor.ProcessLargeContextAsync<int, string>(
            12345,
            maxChunkSize: 256,
            strategy: ChunkingStrategy.Fixed);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("string input");
    }

    [Fact]
    public void Constructor_WithNullProcessFunc_ThrowsArgumentNullException()
    {
        // Arrange
        Func<IEnumerable<string>, Task<Result<string>>> combineFunc = async chunks =>
        {
            await Task.CompletedTask;
            return Result<string>.Success("combined");
        };

        // Act & Assert
        Action act = () => new RecursiveChunkProcessor(null!, combineFunc);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("processChunkFunc");
    }

    [Fact]
    public void Constructor_WithNullCombineFunc_ThrowsArgumentNullException()
    {
        // Arrange
        Func<string, Task<Result<string>>> processFunc = async chunk =>
        {
            await Task.CompletedTask;
            return Result<string>.Success("processed");
        };

        // Act & Assert
        Action act = () => new RecursiveChunkProcessor(processFunc, null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("combineResultsFunc");
    }
}
