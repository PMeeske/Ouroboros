// <copyright file="VisualProcessorTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Application.Embodied;

namespace Ouroboros.Tests;

/// <summary>
/// Unit tests for VisualProcessor.
/// </summary>
[Trait("Category", "Unit")]
public sealed class VisualProcessorTests
{
    private readonly VisualProcessor processor;

    public VisualProcessorTests()
    {
        this.processor = new VisualProcessor(NullLogger<VisualProcessor>.Instance);
    }

    [Fact]
    public async Task ProcessVisualObservationAsync_WithValidInput_ShouldReturnFeatures()
    {
        // Arrange
        var width = 84;
        var height = 84;
        var channels = 3;
        var pixels = new byte[width * height * channels];

        // Act
        var result = await this.processor.ProcessVisualObservationAsync(pixels, width, height, channels);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Length.Should().BeGreaterThan(0);
        result.Value.All(f => f >= 0 && f <= 1).Should().BeTrue("Features should be normalized");
    }

    [Fact]
    public async Task ProcessVisualObservationAsync_WithNullPixels_ShouldReturnFailure()
    {
        // Act
        var result = await this.processor.ProcessVisualObservationAsync(null!, 84, 84, 3);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be null");
    }

    [Fact]
    public async Task ProcessVisualObservationAsync_WithEmptyPixels_ShouldReturnFailure()
    {
        // Act
        var result = await this.processor.ProcessVisualObservationAsync(Array.Empty<byte>(), 84, 84, 3);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be null or empty");
    }

    [Fact]
    public async Task ProcessVisualObservationAsync_WithInvalidDimensions_ShouldReturnFailure()
    {
        // Arrange
        var pixels = new byte[100];

        // Act
        var result = await this.processor.ProcessVisualObservationAsync(pixels, 0, 84, 3);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("must be positive");
    }

    [Fact]
    public async Task ProcessVisualObservationAsync_WithInvalidChannels_ShouldReturnFailure()
    {
        // Arrange
        var pixels = new byte[100];

        // Act
        var result = await this.processor.ProcessVisualObservationAsync(pixels, 84, 84, 5);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Channels must be between");
    }

    [Fact]
    public async Task ProcessVisualObservationAsync_WithMismatchedSize_ShouldReturnFailure()
    {
        // Arrange
        var pixels = new byte[100]; // Wrong size

        // Act
        var result = await this.processor.ProcessVisualObservationAsync(pixels, 84, 84, 3);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Expected");
    }

    [Fact]
    public async Task DetectObjectsAsync_WithValidPixels_ShouldReturnObjects()
    {
        // Arrange
        var pixels = new byte[10000]; // Large enough to trigger mock detection

        // Act
        var result = await this.processor.DetectObjectsAsync(pixels);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task DetectObjectsAsync_WithNullPixels_ShouldReturnFailure()
    {
        // Act
        var result = await this.processor.DetectObjectsAsync(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be null");
    }

    [Fact]
    public async Task DetectObjectsAsync_WithSmallImage_ShouldReturnEmptyList()
    {
        // Arrange
        var pixels = new byte[100]; // Small image

        // Act
        var result = await this.processor.DetectObjectsAsync(pixels);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    public async Task ProcessVisualObservationAsync_WithDifferentChannels_ShouldSucceed(int channels)
    {
        // Arrange
        var width = 32;
        var height = 32;
        var pixels = new byte[width * height * channels];

        // Act
        var result = await this.processor.ProcessVisualObservationAsync(pixels, width, height, channels);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }
}
