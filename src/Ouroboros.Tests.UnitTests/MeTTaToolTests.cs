// <copyright file="MeTTaToolTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests;

using FluentAssertions;
using Ouroboros.Tools;
using Ouroboros.Tools.MeTTa;
using Xunit;

/// <summary>
/// Tests for the MeTTaTool implementation.
/// </summary>
[Trait("Category", "Unit")]
public class MeTTaToolTests
{
    // Test mock engine that returns predictable results
    private class TestMeTTaEngine : IMeTTaEngine
    {
        private readonly Func<string, Result<string, string>>? queryFunc;
        private readonly Func<bool, Result<bool, string>>? verifyFunc;

        public TestMeTTaEngine(
            Func<string, Result<string, string>>? queryFunc = null,
            Func<bool, Result<bool, string>>? verifyFunc = null)
        {
            this.queryFunc = queryFunc;
            this.verifyFunc = verifyFunc;
        }

        public Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
        {
            return Task.FromResult(this.queryFunc?.Invoke(query) ?? Result<string, string>.Success("test result"));
        }

        public Task<Result<Unit, string>> AddFactAsync(string fact, CancellationToken ct = default)
        {
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }

        public Task<Result<string, string>> ApplyRuleAsync(string rule, CancellationToken ct = default)
        {
            return Task.FromResult(Result<string, string>.Success("rule applied"));
        }

        public Task<Result<bool, string>> VerifyPlanAsync(string plan, CancellationToken ct = default)
        {
            return Task.FromResult(this.verifyFunc?.Invoke(true) ?? Result<bool, string>.Success(true));
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
    public void Constructor_WithNullEngine_ThrowsArgumentNullException()
    {
        // Act & Assert
        Action act = () => new MeTTaTool(null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("engine");
    }

    [Fact]
    public void Name_ReturnsMetta()
    {
        // Arrange
        var engine = new TestMeTTaEngine();
        var tool = new MeTTaTool(engine);

        // Act
        var name = tool.Name;

        // Assert
        name.Should().Be("metta");
    }

    [Fact]
    public void Description_ContainsNeuralAndSymbolic()
    {
        // Arrange
        var engine = new TestMeTTaEngine();
        var tool = new MeTTaTool(engine);

        // Act
        var description = tool.Description;

        // Assert
        description.Should().NotBeNullOrWhiteSpace();
        description.Should().Contain("Neural");
        description.Should().Contain("Symbolic");
    }

    [Fact]
    public void JsonSchema_ContainsExpressionProperty()
    {
        // Arrange
        var engine = new TestMeTTaEngine();
        var tool = new MeTTaTool(engine);

        // Act
        var schema = tool.JsonSchema;

        // Assert
        schema.Should().NotBeNullOrWhiteSpace();
        schema.Should().Contain("expression");
        schema.Should().Contain("operation");
    }

    [Fact]
    public async Task InvokeAsync_WithEmptyInput_ReturnsFailure()
    {
        // Arrange
        var engine = new TestMeTTaEngine();
        var tool = new MeTTaTool(engine);

        // Act
        var result = await tool.InvokeAsync(string.Empty);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be empty");
    }

    [Fact]
    public async Task InvokeAsync_WithSimpleExpression_ExecutesQuery()
    {
        // Arrange
        var engine = new TestMeTTaEngine(query => Result<string, string>.Success("3"));
        var tool = new MeTTaTool(engine);

        // Act
        var result = await tool.InvokeAsync("!(+ 1 2)");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("3");
    }

    [Fact]
    public async Task InvokeAsync_WithJsonInput_ParsesAndExecutes()
    {
        // Arrange
        var engine = new TestMeTTaEngine(query => Result<string, string>.Success("result"));
        var tool = new MeTTaTool(engine);
        var json = @"{""expression"": ""!(test query)"", ""operation"": ""query""}";

        // Act
        var result = await tool.InvokeAsync(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithAddFactOperation_CallsAddFactAsync()
    {
        // Arrange
        var engine = new TestMeTTaEngine();
        var tool = new MeTTaTool(engine);
        var json = @"{""expression"": ""(human John)"", ""operation"": ""add_fact""}";

        // Act
        var result = await tool.InvokeAsync(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("added successfully");
    }

    [Fact]
    public async Task InvokeAsync_WithApplyRuleOperation_CallsApplyRuleAsync()
    {
        // Arrange
        var engine = new TestMeTTaEngine();
        var tool = new MeTTaTool(engine);
        var json = @"{""expression"": ""(rule test)"", ""operation"": ""apply_rule""}";

        // Act
        var result = await tool.InvokeAsync(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_WithVerifyPlanOperation_CallsVerifyPlanAsync()
    {
        // Arrange
        var engine = new TestMeTTaEngine(verifyFunc: _ => Result<bool, string>.Success(true));
        var tool = new MeTTaTool(engine);
        var json = @"{""expression"": ""(plan test)"", ""operation"": ""verify_plan""}";

        // Act
        var result = await tool.InvokeAsync(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("valid");
    }

    [Fact]
    public async Task InvokeAsync_WithUnknownOperation_ReturnsFailure()
    {
        // Arrange
        var engine = new TestMeTTaEngine();
        var tool = new MeTTaTool(engine);
        var json = @"{""expression"": ""test"", ""operation"": ""unknown_op""}";

        // Act
        var result = await tool.InvokeAsync(json);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Unknown operation");
    }

    [Fact]
    public async Task InvokeAsync_WithEngineFailure_ReturnsFailure()
    {
        // Arrange
        var engine = new TestMeTTaEngine(query => Result<string, string>.Failure("Engine error"));
        var tool = new MeTTaTool(engine);

        // Act
        var result = await tool.InvokeAsync("!(test)");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Engine error");
    }

    [Fact]
    public async Task InvokeAsync_WithVerifyPlanInvalid_ReturnsInvalidMessage()
    {
        // Arrange
        var engine = new TestMeTTaEngine(verifyFunc: _ => Result<bool, string>.Success(false));
        var tool = new MeTTaTool(engine);
        var json = @"{""expression"": ""(bad plan)"", ""operation"": ""verify_plan""}";

        // Act
        var result = await tool.InvokeAsync(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("invalid");
    }
}
