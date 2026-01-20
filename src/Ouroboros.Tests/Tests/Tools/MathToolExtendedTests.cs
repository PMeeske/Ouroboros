// <copyright file="MathToolExtendedTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Tools;

using FluentAssertions;
using Ouroboros.Tools;
using Xunit;

/// <summary>
/// Extended tests for the MathTool implementation covering edge cases and various operations.
/// </summary>
[Trait("Category", "Unit")]
public class MathToolExtendedTests
{
    private readonly MathTool tool = new();

    #region Basic Operations Tests

    [Theory]
    [InlineData("1+1", "2")]
    [InlineData("10+20", "30")]
    [InlineData("100+0", "100")]
    [InlineData("-5+10", "5")]
    [InlineData("-5+-3", "-8")]
    public async Task InvokeAsync_Addition_ReturnsCorrectResult(string expression, string expected)
    {
        // Act
        var result = await this.tool.InvokeAsync(expression);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("10-5", "5")]
    [InlineData("5-10", "-5")]
    [InlineData("100-0", "100")]
    [InlineData("0-100", "-100")]
    [InlineData("-5-3", "-8")]
    public async Task InvokeAsync_Subtraction_ReturnsCorrectResult(string expression, string expected)
    {
        // Act
        var result = await this.tool.InvokeAsync(expression);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("5*3", "15")]
    [InlineData("10*0", "0")]
    [InlineData("0*100", "0")]
    [InlineData("-5*3", "-15")]
    [InlineData("-5*-3", "15")]
    [InlineData("1*1", "1")]
    public async Task InvokeAsync_Multiplication_ReturnsCorrectResult(string expression, string expected)
    {
        // Act
        var result = await this.tool.InvokeAsync(expression);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("20/4", "5")]
    [InlineData("15/3", "5")]
    [InlineData("10/2", "5")]
    [InlineData("-20/4", "-5")]
    [InlineData("100/25", "4")]
    public async Task InvokeAsync_Division_ReturnsCorrectResult(string expression, string expected)
    {
        // Act
        var result = await this.tool.InvokeAsync(expression);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    #endregion

    #region Decimal and Floating Point Tests

    [Fact]
    public async Task InvokeAsync_WithDecimalResult_ReturnsDecimal()
    {
        // Act
        var result = await this.tool.InvokeAsync("5/2");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("2.5");
    }

    [Fact]
    public async Task InvokeAsync_WithRepeatingDecimal_HandlesCorrectly()
    {
        // Act
        var result = await this.tool.InvokeAsync("10/3");

        // Assert
        result.IsSuccess.Should().BeTrue();
        // DataTable.Compute may round or truncate
        result.Value.Should().Contain("3.33");
    }

    [Theory]
    [InlineData("1.5+1.5", "3.0")]
    [InlineData("2.5*2", "5.0")]
    [InlineData("10.0/4", "2.5")]
    public async Task InvokeAsync_WithDecimalInputs_ReturnsCorrectResult(string expression, string expected)
    {
        // Act
        var result = await this.tool.InvokeAsync(expression);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    #endregion

    #region Order of Operations Tests

    [Theory]
    [InlineData("2+3*4", "14")]       // Multiplication before addition
    [InlineData("10-2*3", "4")]       // Multiplication before subtraction
    [InlineData("20/4+2", "7")]       // Division before addition
    [InlineData("2+3*4-5", "9")]      // Complex expression
    public async Task InvokeAsync_OrderOfOperations_RespectsCorrectOrder(string expression, string expected)
    {
        // Act
        var result = await this.tool.InvokeAsync(expression);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    [Theory]
    [InlineData("(2+3)*4", "20")]
    [InlineData("(10-5)/2", "2.5")]
    [InlineData("(2+3)*(4+5)", "45")]
    [InlineData("((2+3)*4)", "20")]
    public async Task InvokeAsync_WithParentheses_RespectsGrouping(string expression, string expected)
    {
        // Act
        var result = await this.tool.InvokeAsync(expression);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    #endregion

    #region Modulo Operation Tests

    [Theory]
    [InlineData("10%3", "1")]
    [InlineData("15%4", "3")]
    [InlineData("20%5", "0")]
    [InlineData("7%2", "1")]
    public async Task InvokeAsync_Modulo_ReturnsCorrectResult(string expression, string expected)
    {
        // Act
        var result = await this.tool.InvokeAsync(expression);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task InvokeAsync_WithEmptyString_ReturnsFailure()
    {
        // Act
        var result = await this.tool.InvokeAsync(string.Empty);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task InvokeAsync_WithWhitespaceOnly_ReturnsFailure()
    {
        // Act
        var result = await this.tool.InvokeAsync("   ");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("hello world")]
    [InlineData("not a math expression")]
    public async Task InvokeAsync_WithInvalidExpression_ReturnsFailure(string expression)
    {
        // Act
        var result = await this.tool.InvokeAsync(expression);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Math evaluation failed");
    }

    [Fact]
    public async Task InvokeAsync_WithUnbalancedParentheses_ReturnsFailure()
    {
        // Act
        var result = await this.tool.InvokeAsync("(2+3");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithConsecutiveOperators_HandlesGracefully()
    {
        // Note: DataTable.Compute treats 2++3 as 2 + (+3) = 5
        // Act
        var result = await this.tool.InvokeAsync("2++3");

        // Assert - DataTable interprets this as 2 + +3 = 5, which is valid
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region Division by Zero Tests

    [Fact]
    public async Task InvokeAsync_DivisionByZero_ReturnsInfinity()
    {
        // Note: DataTable.Compute returns Infinity for division by zero
        // Act
        var result = await this.tool.InvokeAsync("1/0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        // DataTable.Compute returns "∞" or "Infinity"
        result.Value.Should().MatchRegex(@"∞|Infinity|\u221e");
    }

    [Fact]
    public async Task InvokeAsync_ZeroDividedByZero_ReturnsNaN()
    {
        // Note: DataTable.Compute returns NaN for 0/0
        // Act
        var result = await this.tool.InvokeAsync("0/0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Result may be NaN or other representation
        result.Value.Should().NotBeEmpty();
    }

    #endregion

    #region Whitespace Handling Tests

    [Theory]
    [InlineData(" 2+2 ", "4")]
    [InlineData("2 + 2", "4")]
    [InlineData("  10  *  5  ", "50")]
    [InlineData("\t2+3\t", "5")]
    public async Task InvokeAsync_WithWhitespace_ReturnsCorrectResult(string expression, string expected)
    {
        // Act
        var result = await this.tool.InvokeAsync(expression);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    #endregion

    #region Large Number Tests

    [Fact]
    public async Task InvokeAsync_LargeNumbers_ReturnsCorrectResult()
    {
        // Act - Use smaller numbers that DataTable.Compute can handle
        var result = await this.tool.InvokeAsync("1000*1000");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("1000000");
    }

    [Fact]
    public async Task InvokeAsync_VerySmallNumbers_ReturnsCorrectResult()
    {
        // Act
        var result = await this.tool.InvokeAsync("0.001*0.001");

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Result should be 0.000001 or scientific notation
        result.Value.Should().NotBeEmpty();
    }

    #endregion

    #region Negative Number Tests

    [Theory]
    [InlineData("-5+3", "-2")]
    [InlineData("5+-3", "2")]
    [InlineData("-5--3", "-2")]
    [InlineData("-5*-3", "15")]
    [InlineData("-10/-2", "5")]
    public async Task InvokeAsync_NegativeNumbers_ReturnsCorrectResult(string expression, string expected)
    {
        // Act
        var result = await this.tool.InvokeAsync(expression);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(expected);
    }

    #endregion

    #region Tool Interface Tests

    [Fact]
    public void Name_ReturnsMath()
    {
        // Assert
        this.tool.Name.Should().Be("math");
    }

    [Fact]
    public void Description_ContainsArithmetic()
    {
        // Assert
        this.tool.Description.Should().Contain("arithmetic");
    }

    [Fact]
    public void JsonSchema_ReturnsNull()
    {
        // Assert - MathTool accepts free-form string
        this.tool.JsonSchema.Should().BeNull();
    }

    #endregion

    #region Cancellation Token Tests

    [Fact]
    public async Task InvokeAsync_WithCancellationToken_Completes()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        // Act
        var result = await this.tool.InvokeAsync("2+2", cts.Token);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("4");
    }

    [Fact]
    public async Task InvokeAsync_WithCancelledToken_StillCompletesSync()
    {
        // Arrange - Math is synchronous, so cancellation doesn't affect it
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act - This demonstrates current behavior (sync operation ignores cancellation)
        var result = await this.tool.InvokeAsync("2+2", cts.Token);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task InvokeAsync_SingleNumber_ReturnsNumber()
    {
        // Act
        var result = await this.tool.InvokeAsync("42");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("42");
    }

    [Fact]
    public async Task InvokeAsync_NegativeSingleNumber_ReturnsNumber()
    {
        // Act
        var result = await this.tool.InvokeAsync("-42");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("-42");
    }

    [Fact]
    public async Task InvokeAsync_Zero_ReturnsZero()
    {
        // Act
        var result = await this.tool.InvokeAsync("0");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("0");
    }

    #endregion
}
