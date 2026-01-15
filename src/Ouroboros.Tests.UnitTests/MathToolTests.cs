// <copyright file="MathToolTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests;

using FluentAssertions;
using Ouroboros.Tools;
using Xunit;

/// <summary>
/// Tests for the MathTool implementation.
/// </summary>
[Trait("Category", "Unit")]
public class MathToolTests
{
    [Fact]
    public void Name_ReturnsMath()
    {
        // Arrange
        var tool = new MathTool();

        // Act
        var name = tool.Name;

        // Assert
        name.Should().Be("math");
    }

    [Fact]
    public void Description_ReturnsValidDescription()
    {
        // Arrange
        var tool = new MathTool();

        // Act
        var description = tool.Description;

        // Assert
        description.Should().NotBeNullOrWhiteSpace();
        description.Should().Contain("arithmetic");
    }

    [Fact]
    public void JsonSchema_ReturnsNull()
    {
        // Arrange
        var tool = new MathTool();

        // Act
        var schema = tool.JsonSchema;

        // Assert
        schema.Should().BeNull();
    }

    [Fact]
    public async Task InvokeAsync_WithSimpleAddition_ReturnsCorrectResult()
    {
        // Arrange
        var tool = new MathTool();

        // Act
        var result = await tool.InvokeAsync("2+2");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("4");
    }

    [Fact]
    public async Task InvokeAsync_WithMultiplication_ReturnsCorrectResult()
    {
        // Arrange
        var tool = new MathTool();

        // Act
        var result = await tool.InvokeAsync("5*3");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("15");
    }

    [Fact]
    public async Task InvokeAsync_WithComplexExpression_ReturnsCorrectResult()
    {
        // Arrange
        var tool = new MathTool();

        // Act
        var result = await tool.InvokeAsync("2+2*5");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("12"); // Order of operations: 2 + (2*5) = 12
    }

    [Fact]
    public async Task InvokeAsync_WithParentheses_ReturnsCorrectResult()
    {
        // Arrange
        var tool = new MathTool();

        // Act
        var result = await tool.InvokeAsync("(10-5)/2");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("2.5");
    }

    [Fact]
    public async Task InvokeAsync_WithEmptyString_ReturnsFailure()
    {
        // Arrange
        var tool = new MathTool();

        // Act
        var result = await tool.InvokeAsync(string.Empty);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task InvokeAsync_WithWhitespace_ReturnsFailure()
    {
        // Arrange
        var tool = new MathTool();

        // Act
        var result = await tool.InvokeAsync("   ");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidExpression_ReturnsFailure()
    {
        // Arrange
        var tool = new MathTool();

        // Act
        var result = await tool.InvokeAsync("invalid");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Math evaluation failed");
    }

    [Theory]
    [InlineData("1+1", "2")]
    [InlineData("10-5", "5")]
    [InlineData("3*4", "12")]
    [InlineData("20/4", "5")]
    public async Task InvokeAsync_WithVariousExpressions_ReturnsCorrectResults(string expression, string expected)
    {
        // Arrange
        var tool = new MathTool();

        // Act
        var result = await tool.InvokeAsync(expression);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    [Fact]
    public async Task InvokeAsync_WithCancellationToken_CanBeCancelled()
    {
        // Arrange
        var tool = new MathTool();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await tool.InvokeAsync("2+2", cts.Token);

        // Assert - Even though cancelled, math tool doesn't respect cancellation (sync operation)
        // This test documents current behavior
        result.IsSuccess.Should().BeTrue();
    }
}
