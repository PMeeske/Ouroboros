// <copyright file="EasyPipelineTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Easy;

using FluentAssertions;
using Moq;
using Ouroboros.Easy;
using Xunit;

/// <summary>
/// Unit tests for the Easy API pipeline builder.
/// </summary>
public class EasyPipelineTests
{
    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Component", "Easy")]
    public void Create_WithTopic_ShouldCreatePipeline()
    {
        // Arrange & Act
        var pipeline = Pipeline.Create("quantum computing");

        // Assert
        pipeline.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Component", "Easy")]
    public void Draft_ShouldAddDraftOperation()
    {
        // Arrange
        var pipeline = Pipeline.Create("AI ethics");

        // Act
        var result = pipeline.Draft();

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(pipeline);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Component", "Easy")]
    public void MethodChaining_ShouldWork()
    {
        // Arrange & Act
        var pipeline = Pipeline.Create("machine learning")
            .Draft()
            .Critique()
            .Improve()
            .WithModel("llama3")
            .WithTemperature(0.7);

        // Assert
        pipeline.Should().NotBeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Component", "Easy")]
    public void WithTemperature_WithInvalidValue_ShouldThrow()
    {
        // Arrange
        var pipeline = Pipeline.Create("topic");

        // Act & Assert
        pipeline.Invoking(p => p.WithTemperature(-0.1))
            .Should().Throw<ArgumentOutOfRangeException>();
        
        pipeline.Invoking(p => p.WithTemperature(1.5))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Component", "Easy")]
    public void WithContextDocuments_WithInvalidValue_ShouldThrow()
    {
        // Arrange
        var pipeline = Pipeline.Create("topic");

        // Act & Assert
        pipeline.Invoking(p => p.WithContextDocuments(0))
            .Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Component", "Easy")]
    public void ToDSL_ShouldGenerateReadableFormat()
    {
        // Arrange
        var pipeline = Pipeline.Create("quantum computing")
            .Draft()
            .Critique()
            .Improve()
            .WithModel("llama3")
            .WithTemperature(0.8);

        // Act
        var dsl = pipeline.ToDSL();

        // Assert
        dsl.Should().Contain("quantum computing");
        dsl.Should().Contain("Draft");
        dsl.Should().Contain("Critique");
        dsl.Should().Contain("Improve");
        dsl.Should().Contain("llama3");
        dsl.Should().Contain("0.8");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Component", "Easy")]
    public void BasicReasoning_ShouldCreatePipelineWithDraftCritiqueImprove()
    {
        // Act
        var pipeline = Pipeline.BasicReasoning("AI safety");
        var dsl = pipeline.ToDSL();

        // Assert
        dsl.Should().Contain("Draft");
        dsl.Should().Contain("Critique");
        dsl.Should().Contain("Improve");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Component", "Easy")]
    public void FullReasoning_ShouldIncludeThinkStep()
    {
        // Act
        var pipeline = Pipeline.FullReasoning("neural networks");
        var dsl = pipeline.ToDSL();

        // Assert
        dsl.Should().Contain("Think");
        dsl.Should().Contain("Draft");
        dsl.Should().Contain("Critique");
        dsl.Should().Contain("Improve");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Component", "Easy")]
    public void IterativeReasoning_ShouldHaveMultipleCritiqueImprove()
    {
        // Act
        var pipeline = Pipeline.IterativeReasoning("topic", iterations: 3);
        var dsl = pipeline.ToDSL();

        // Assert
        dsl.Should().Contain("Draft");
        // Should have 3 Critique-Improve cycles
        var critiqueCount = dsl.Split("Critique").Length - 1;
        var improveCount = dsl.Split("Improve").Length - 1;
        
        critiqueCount.Should().Be(3);
        improveCount.Should().Be(3);
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Component", "Easy")]
    public void IterativeReasoning_WithInvalidIterations_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Pipeline.IterativeReasoning("topic", iterations: 0));
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Component", "Easy")]
    public void Summarize_ShouldCreateSummarizationPipeline()
    {
        // Act
        var pipeline = Pipeline.Summarize("long document");
        var dsl = pipeline.ToDSL();

        // Assert
        dsl.Should().Contain("Draft");
        dsl.Should().Contain("Summarize");
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Component", "Easy")]
    public void ToCore_WithoutModels_ShouldThrow()
    {
        // Arrange
        var pipeline = Pipeline.Create("topic").Draft();

        // Act & Assert
        pipeline.Invoking(p => p.ToCore())
            .Should().Throw<InvalidOperationException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    [Trait("Component", "Easy")]
    public void ToCore_WithModels_ShouldReturnKleisliArrow()
    {
        // Arrange
        var mockChatCompletion = new MockChatCompletionModel("test response");
        var mockChat = new ToolAwareChatModel(
            mockChatCompletion,
            new ToolRegistry());
        var mockEmbed = Mock.Of<IEmbeddingModel>();
        
        var pipeline = Pipeline.Create("topic")
            .Draft()
            .WithChatModel(mockChat)
            .WithEmbeddingModel(mockEmbed);

        // Act
        var arrow = pipeline.ToCore();

        // Assert
        arrow.Should().NotBeNull();
    }

    private class MockChatCompletionModel : IChatCompletionModel
    {
        private readonly string _response;

        public MockChatCompletionModel(string response)
        {
            _response = response;
        }

        public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
        {
            return Task.FromResult(_response);
        }
    }
}
