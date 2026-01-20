// <copyright file="ConfidenceGatedPipelineTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.LawsOfForm;

using FluentAssertions;
using Ouroboros.Core.LawsOfForm;
using Xunit;

/// <summary>
/// Tests for ConfidenceGatedPipeline.
/// </summary>
[Trait("Category", "Unit")]
public class ConfidenceGatedPipelineTests
{
    [Fact]
    public async Task GateByConfidence_HighConfidence_ReturnsSome()
    {
        // Arrange
        var response = new LlmResponse("test", confidence: 0.9);
        var gate = ConfidenceGatedPipeline.GateByConfidence(threshold: 0.8);

        // Act
        var result = await gate(response);

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value.Should().Be(response);
    }

    [Fact]
    public async Task GateByConfidence_LowConfidence_ReturnsNone()
    {
        // Arrange
        var response = new LlmResponse("test", confidence: 0.5);
        var gate = ConfidenceGatedPipeline.GateByConfidence(threshold: 0.8);

        // Act
        var result = await gate(response);

        // Assert
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public async Task RouteByConfidence_HighConfidence_CallsHighConfidenceHandler()
    {
        // Arrange
        var response = new LlmResponse("test", confidence: 0.9);
        var router = ConfidenceGatedPipeline.RouteByConfidence<string>(
            onHighConfidence: r => "high",
            onLowConfidence: r => "low",
            onUncertain: r => "uncertain");

        // Act
        var result = await router(response);

        // Assert
        result.Should().Be("high");
    }

    [Fact]
    public async Task RouteByConfidence_LowConfidence_CallsLowConfidenceHandler()
    {
        // Arrange
        var response = new LlmResponse("test", confidence: 0.2);
        var router = ConfidenceGatedPipeline.RouteByConfidence<string>(
            onHighConfidence: r => "high",
            onLowConfidence: r => "low",
            onUncertain: r => "uncertain",
            highThreshold: 0.8,
            lowThreshold: 0.3);

        // Act
        var result = await router(response);

        // Assert
        result.Should().Be("low");
    }

    [Fact]
    public async Task RouteByConfidence_MediumConfidence_CallsUncertainHandler()
    {
        // Arrange
        var response = new LlmResponse("test", confidence: 0.5);
        var router = ConfidenceGatedPipeline.RouteByConfidence<string>(
            onHighConfidence: r => "high",
            onLowConfidence: r => "low",
            onUncertain: r => "uncertain",
            highThreshold: 0.8,
            lowThreshold: 0.3);

        // Act
        var result = await router(response);

        // Assert
        result.Should().Be("uncertain");
    }

    [Fact]
    public void CombineOpinions_StrongConsensus_ReturnsMark()
    {
        // Arrange
        var opinions = new[]
        {
            (Form.Mark, 3.0),
            (Form.Mark, 2.0),
            (Form.Void, 1.0)
        };

        // Act
        var result = ConfidenceGatedPipeline.CombineOpinions(opinions);

        // Assert
        result.IsMark().Should().BeTrue();
    }

    [Fact]
    public void CombineOpinions_WithImaginary_ReturnsImaginary()
    {
        // Arrange
        var opinions = new[]
        {
            (Form.Mark, 2.0),
            (Form.Imaginary, 1.0)
        };

        // Act
        var result = ConfidenceGatedPipeline.CombineOpinions(opinions);

        // Assert
        result.IsImaginary().Should().BeTrue();
    }

    [Fact]
    public async Task GateByConfidenceResult_HighConfidence_ReturnsSuccess()
    {
        // Arrange
        var response = new LlmResponse("test", confidence: 0.9);
        var gate = ConfidenceGatedPipeline.GateByConfidenceResult(threshold: 0.8);

        // Act
        var result = await gate(response);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(response);
    }

    [Fact]
    public async Task GateByConfidenceResult_LowConfidence_ReturnsFailure()
    {
        // Arrange
        var response = new LlmResponse("test", confidence: 0.5);
        var gate = ConfidenceGatedPipeline.GateByConfidenceResult(threshold: 0.8);

        // Act
        var result = await gate(response);

        // Assert
        // 0.5 is between lowThreshold (0.4) and highThreshold (0.8), so it's Imaginary state
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Uncertain state");
    }

    [Fact]
    public async Task FilterByConfidence_FiltersLowConfidence()
    {
        // Arrange
        var responses = new[]
        {
            new LlmResponse("high1", confidence: 0.9),
            new LlmResponse("low", confidence: 0.5),
            new LlmResponse("high2", confidence: 0.85)
        };

        var filter = ConfidenceGatedPipeline.FilterByConfidence(threshold: 0.8);

        // Act
        var result = await filter(responses);

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(r => r.Text == "high1");
        result.Should().Contain(r => r.Text == "high2");
    }

    [Fact]
    public void AggregateResponses_WithConsensus_ReturnsBestResponse()
    {
        // Arrange
        var responses = new[]
        {
            new LlmResponse("response1", confidence: 0.9),
            new LlmResponse("response2", confidence: 0.85),
            new LlmResponse("response3", confidence: 0.87)
        };

        // Act
        var result = ConfidenceGatedPipeline.AggregateResponses(responses);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Text.Should().Be("response1"); // Highest confidence
    }

    [Fact]
    public void AggregateResponses_NoConsensus_ReturnsFailure()
    {
        // Arrange
        var responses = new[]
        {
            new LlmResponse("high", confidence: 0.9),
            new LlmResponse("low", confidence: 0.2)
        };

        // Act
        var result = ConfidenceGatedPipeline.AggregateResponses(
            responses,
            highThreshold: 0.8,
            lowThreshold: 0.3);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No clear consensus");
    }

    [Fact]
    public void AggregateResponses_EmptyList_ReturnsFailure()
    {
        // Act
        var result = ConfidenceGatedPipeline.AggregateResponses(Array.Empty<LlmResponse>());

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No responses");
    }

    [Fact]
    public void LlmResponse_ClampsConfidence()
    {
        // Arrange & Act
        var tooHigh = new LlmResponse("test", confidence: 1.5);
        var tooLow = new LlmResponse("test", confidence: -0.5);

        // Assert
        tooHigh.Confidence.Should().Be(1.0);
        tooLow.Confidence.Should().Be(0.0);
    }

    [Fact]
    public void LlmResponse_DefaultValues()
    {
        // Arrange & Act
        var response = new LlmResponse("test");

        // Assert
        response.Confidence.Should().Be(1.0);
        response.ToolCalls.Should().BeEmpty();
        response.Metadata.Should().BeEmpty();
        response.ModelName.Should().BeNull();
        response.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}
