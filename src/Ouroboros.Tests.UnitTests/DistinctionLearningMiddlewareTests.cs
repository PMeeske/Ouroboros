// <copyright file="DistinctionLearningMiddlewareTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests.Tests;

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Ouroboros.Application.Middleware;
using Ouroboros.Application.Personality.Consciousness;
using Ouroboros.Core.DistinctionLearning;
using Ouroboros.Core.Learning;
using Ouroboros.Core.Monads;
using Ouroboros.Pipeline.Middleware;
using Xunit;

/// <summary>
/// Tests for the DistinctionLearningMiddleware.
/// Validates middleware integration with pipeline and learning lifecycle.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DistinctionLearningMiddlewareTests
{
    private readonly Mock<IDistinctionLearner> _mockLearner;
    private readonly ConsciousnessDream _dream;
    private readonly Mock<ILogger<DistinctionLearningMiddleware>> _mockLogger;

    public DistinctionLearningMiddlewareTests()
    {
        _mockLearner = new Mock<IDistinctionLearner>();
        _dream = new ConsciousnessDream();
        _mockLogger = new Mock<ILogger<DistinctionLearningMiddleware>>();
        
        // Setup default successful responses
        _mockLearner.Setup(l => l.UpdateFromDistinctionAsync(
                It.IsAny<DistinctionState>(),
                It.IsAny<Observation>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<DistinctionState, string>.Success(DistinctionState.Initial()));

        _mockLearner.Setup(l => l.RecognizeAsync(
                It.IsAny<DistinctionState>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<DistinctionState, string>.Success(DistinctionState.Initial()));

        _mockLearner.Setup(l => l.DissolveAsync(
                It.IsAny<DistinctionState>(),
                It.IsAny<DissolutionStrategy>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.FromResult(Result<Unit, string>.Success(Unit.Value)));
    }

    #region ProcessAsync Tests

    [Fact]
    public async Task ProcessAsync_CallsNextMiddleware()
    {
        // Arrange
        var sut = new DistinctionLearningMiddleware(_mockLearner.Object, _dream, _mockLogger.Object);
        var context = PipelineContext.FromInput("test input");
        var expectedResult = PipelineResult.Successful("test output");
        var nextCalled = false;

        Task<PipelineResult> Next(PipelineContext ctx, CancellationToken ct)
        {
            nextCalled = true;
            return Task.FromResult(expectedResult);
        }

        // Act
        var result = await sut.ProcessAsync(context, Next);

        // Assert
        nextCalled.Should().BeTrue();
        result.Should().Be(expectedResult);
    }

    [Fact]
    public async Task ProcessAsync_ReturnsResultFromNextImmediately()
    {
        // Arrange
        var sut = new DistinctionLearningMiddleware(_mockLearner.Object, _dream, _mockLogger.Object);
        var context = PipelineContext.FromInput("test input");
        var expectedResult = PipelineResult.Successful("test output");

        Task<PipelineResult> Next(PipelineContext ctx, CancellationToken ct) =>
            Task.FromResult(expectedResult);

        // Act
        var result = await sut.ProcessAsync(context, Next);

        // Assert
        result.Should().Be(expectedResult);
        result.Success.Should().BeTrue();
        result.Output.Should().Be("test output");
    }

    [Fact]
    public async Task ProcessAsync_DoesNotBlockOnLearning()
    {
        // Arrange
        var sut = new DistinctionLearningMiddleware(_mockLearner.Object, _dream, _mockLogger.Object);
        var context = PipelineContext.FromInput("test input");
        var result = PipelineResult.Successful("output");

        Task<PipelineResult> Next(PipelineContext ctx, CancellationToken ct) =>
            Task.FromResult(result);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        await sut.ProcessAsync(context, Next);
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(200, 
            "ProcessAsync should return immediately without waiting for learning");
    }

    [Fact]
    public async Task ProcessAsync_WhenLearningFails_DoesNotFailPipeline()
    {
        // Arrange
        var mockLearnerFails = new Mock<IDistinctionLearner>();
        mockLearnerFails.Setup(l => l.UpdateFromDistinctionAsync(
                It.IsAny<DistinctionState>(),
                It.IsAny<Observation>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<DistinctionState, string>.Failure("Learning failed"));

        var sut = new DistinctionLearningMiddleware(mockLearnerFails.Object, _dream, _mockLogger.Object);
        var context = PipelineContext.FromInput("test input");
        var expectedResult = PipelineResult.Successful("output");

        Task<PipelineResult> Next(PipelineContext ctx, CancellationToken ct) =>
            Task.FromResult(expectedResult);

        // Act
        var result = await sut.ProcessAsync(context, Next);

        // Assert
        result.Should().Be(expectedResult);
        result.Success.Should().BeTrue("pipeline should succeed even if learning fails");
    }

    [Fact]
    public async Task ProcessAsync_WhenLearnerThrows_DoesNotFailPipeline()
    {
        // Arrange
        var mockLearnerThrows = new Mock<IDistinctionLearner>();
        mockLearnerThrows.Setup(l => l.UpdateFromDistinctionAsync(
                It.IsAny<DistinctionState>(),
                It.IsAny<Observation>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Learning error"));

        var sut = new DistinctionLearningMiddleware(mockLearnerThrows.Object, _dream, _mockLogger.Object);
        var context = PipelineContext.FromInput("test input");
        var expectedResult = PipelineResult.Successful("output");

        Task<PipelineResult> Next(PipelineContext ctx, CancellationToken ct) =>
            Task.FromResult(expectedResult);

        // Act
        var result = await sut.ProcessAsync(context, Next);

        // Assert
        result.Should().Be(expectedResult);
        result.Success.Should().BeTrue("pipeline should succeed even if learning throws");
    }

    [Fact]
    public async Task ProcessAsync_WithFailedResult_ReturnsFailedResult()
    {
        // Arrange
        var sut = new DistinctionLearningMiddleware(_mockLearner.Object, _dream, _mockLogger.Object);
        var context = PipelineContext.FromInput("test input");
        var result = PipelineResult.Failed(new Exception("Pipeline failed"));

        Task<PipelineResult> Next(PipelineContext ctx, CancellationToken ct) =>
            Task.FromResult(result);

        // Act
        var actualResult = await sut.ProcessAsync(context, Next);

        // Assert
        actualResult.Success.Should().BeFalse();
        actualResult.Error.Should().NotBeNull();
    }

    [Fact]
    public async Task ProcessAsync_TriggersLearningAsync()
    {
        // Arrange
        var sut = new DistinctionLearningMiddleware(_mockLearner.Object, _dream, _mockLogger.Object);
        var context = PipelineContext.FromInput("test input");
        var result = PipelineResult.Successful("output");

        Task<PipelineResult> Next(PipelineContext ctx, CancellationToken ct) =>
            Task.FromResult(result);

        // Act
        await sut.ProcessAsync(context, Next);
        
        // Give async learning some time to start
        await Task.Delay(200);

        // Assert - learning should have been called at least once
        _mockLearner.Verify(l => l.UpdateFromDistinctionAsync(
            It.IsAny<DistinctionState>(),
            It.IsAny<Observation>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    #endregion

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLearner_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new DistinctionLearningMiddleware(null!, new ConsciousnessDream()));
    }

    [Fact]
    public void Constructor_WithNullDream_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new DistinctionLearningMiddleware(_mockLearner.Object, null!));
    }

    [Fact]
    public void Constructor_WithValidParameters_Succeeds()
    {
        // Act
        var sut = new DistinctionLearningMiddleware(_mockLearner.Object, _dream, _mockLogger.Object);

        // Assert
        sut.Should().NotBeNull();
    }

    #endregion
}
