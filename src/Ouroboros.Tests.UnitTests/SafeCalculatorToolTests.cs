// <copyright file="SafeCalculatorToolTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests;

using FluentAssertions;
using Ouroboros.Tools;
using Ouroboros.Tools.MeTTa;
using Xunit;

/// <summary>
/// Tests for the SafeCalculatorTool implementation.
/// </summary>
[Trait("Category", "Unit")]
public class SafeCalculatorToolTests
{
    // Test mock engine for symbolic verification
    private class TestMeTTaEngine : IMeTTaEngine
    {
        private readonly Func<string, Result<string, string>>? queryFunc;

        public TestMeTTaEngine(Func<string, Result<string, string>>? queryFunc = null)
        {
            this.queryFunc = queryFunc;
        }

        public Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
        {
            return Task.FromResult(this.queryFunc?.Invoke(query) ?? Result<string, string>.Success("0"));
        }

        public Task<Result<Unit, string>> AddFactAsync(string fact, CancellationToken ct = default)
        {
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }

        public Task<Result<string, string>> ApplyRuleAsync(string rule, CancellationToken ct = default)
        {
            return Task.FromResult(Result<string, string>.Success("applied"));
        }

        public Task<Result<bool, string>> VerifyPlanAsync(string plan, CancellationToken ct = default)
        {
            return Task.FromResult(Result<bool, string>.Success(true));
        }

        public Task<Result<Unit, string>> ResetAsync(CancellationToken ct = default)
        {
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }

        public void Dispose()
        {
        }
    }
    [Fact]
    public void Name_ReturnsSafeCalculator()
    {
        // Arrange
        var tool = new SafeCalculatorTool();

        // Act
        var name = tool.Name;

        // Assert
        name.Should().Be("safe_calculator");
    }

    [Fact]
    public void Description_ContainsVerificationKeywords()
    {
        // Arrange
        var tool = new SafeCalculatorTool();

        // Act
        var description = tool.Description;

        // Assert
        description.Should().NotBeNullOrWhiteSpace();
        description.Should().Contain("Verified");
        description.Should().Contain("symbolic");
    }

    [Fact]
    public void JsonSchema_ContainsExpressionProperty()
    {
        // Arrange
        var tool = new SafeCalculatorTool();

        // Act
        var schema = tool.JsonSchema;

        // Assert
        schema.Should().NotBeNullOrWhiteSpace();
        schema.Should().Contain("expression");
        schema.Should().Contain("expected_result");
    }

    [Fact]
    public async Task InvokeAsync_WithEmptyInput_ReturnsFailure()
    {
        // Arrange
        var tool = new SafeCalculatorTool();

        // Act
        var result = await tool.InvokeAsync(string.Empty);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be empty");
    }

    [Fact]
    public async Task InvokeAsync_WithSimpleAddition_ReturnsVerifiedResult()
    {
        // Arrange
        var tool = new SafeCalculatorTool();

        // Act
        var result = await tool.InvokeAsync("2+2");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("Verified");
        result.Value.Should().Contain("4");
    }

    [Fact]
    public async Task InvokeAsync_WithComplexExpression_ReturnsVerifiedResult()
    {
        // Arrange
        var tool = new SafeCalculatorTool();

        // Act
        var result = await tool.InvokeAsync("2+2*5");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("Verified");
        result.Value.Should().Contain("12");
    }

    [Fact]
    public async Task InvokeAsync_WithParentheses_ReturnsCorrectResult()
    {
        // Arrange
        var tool = new SafeCalculatorTool();

        // Act
        var result = await tool.InvokeAsync("(10-5)/2");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("2.5");
    }

    [Fact]
    public async Task InvokeAsync_WithJsonInput_ParsesAndCalculates()
    {
        // Arrange
        var tool = new SafeCalculatorTool();
        var json = @"{""expression"": ""5*3""}";

        // Act
        var result = await tool.InvokeAsync(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("15");
    }

    [Fact]
    public async Task InvokeAsync_WithExpectedResultMatching_ReturnsSuccess()
    {
        // Arrange
        var tool = new SafeCalculatorTool();
        var json = @"{""expression"": ""2+3"", ""expected_result"": 5}";

        // Act
        var result = await tool.InvokeAsync(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("5");
    }

    [Fact]
    public async Task InvokeAsync_WithExpectedResultMismatch_ReturnsFailure()
    {
        // Arrange
        var tool = new SafeCalculatorTool();
        var json = @"{""expression"": ""2+3"", ""expected_result"": 6}";

        // Act
        var result = await tool.InvokeAsync(json);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("mismatch");
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidExpression_ReturnsFailure()
    {
        // Arrange
        var tool = new SafeCalculatorTool();

        // Act
        var result = await tool.InvokeAsync("invalid++expression");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("evaluation failed");
    }

    [Fact]
    public async Task InvokeAsync_WithSymbolicEngine_UsesSymbolicVerification()
    {
        // Arrange
        var engine = new TestMeTTaEngine(query => Result<string, string>.Success("12"));
        var tool = new SafeCalculatorTool(engine);

        // Act
        var result = await tool.InvokeAsync("2+2*5");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("Symbolically Verified");
    }

    [Fact]
    public async Task InvokeAsync_WithoutSymbolicEngine_UsesSimulatedVerification()
    {
        // Arrange
        var tool = new SafeCalculatorTool(null);

        // Act
        var result = await tool.InvokeAsync("3*4");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("Verified");
        result.Value.Should().Contain("12");
    }

    [Fact]
    public async Task InvokeAsync_WithInvalidCharacters_ReturnsFailure()
    {
        // Arrange
        var tool = new SafeCalculatorTool();

        // Act
        var result = await tool.InvokeAsync("2+2; DROP TABLE users");

        // Assert
        result.IsFailure.Should().BeTrue();
        // The error message shows evaluation failure for invalid expression
        result.Error.Should().ContainAny("invalid characters", "evaluation failed", "Cannot interpret");
    }

    [Theory]
    [InlineData("1+1", "2")]
    [InlineData("10-5", "5")]
    [InlineData("3*4", "12")]
    [InlineData("20/4", "5")]
    [InlineData("100/10", "10")]
    public async Task InvokeAsync_WithVariousExpressions_ReturnsVerifiedResults(string expression, string expectedValue)
    {
        // Arrange
        var tool = new SafeCalculatorTool();

        // Act
        var result = await tool.InvokeAsync(expression);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("Verified");
        result.Value.Should().Contain(expectedValue);
    }

    [Fact]
    public async Task InvokeAsync_WithSymbolicEngineFailure_ReturnsFailure()
    {
        // Arrange
        var engine = new TestMeTTaEngine(query => Result<string, string>.Failure("Engine error"));
        var tool = new SafeCalculatorTool(engine);

        // Act
        var result = await tool.InvokeAsync("2+2");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Verification failed");
    }

    [Fact]
    public async Task InvokeAsync_WithSymbolicEngineMismatch_ReturnsFailure()
    {
        // Arrange
        var engine = new TestMeTTaEngine(query => Result<string, string>.Success("999")); // Wrong result
        var tool = new SafeCalculatorTool(engine);

        // Act
        var result = await tool.InvokeAsync("2+2");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("does not match");
    }
}
