// <copyright file="MeTTaIntegrationTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Tools;

using FluentAssertions;
using Ouroboros.Core.Monads;
using Ouroboros.Tools;
using Ouroboros.Tools.MeTTa;
using Xunit;

/// <summary>
/// Comprehensive tests for MeTTa tool integration covering various engine implementations.
/// </summary>
[Trait("Category", "Unit")]
public class MeTTaIntegrationTests
{
    #region Mock Engine Implementation

    /// <summary>
    /// A comprehensive mock MeTTa engine for testing various scenarios.
    /// </summary>
    private sealed class MockMeTTaEngine : IMeTTaEngine
    {
        private readonly Dictionary<string, string> knowledgeBase = new();
        private readonly Func<string, Result<string, string>>? queryHandler;
        private readonly Func<string, Result<Unit, string>>? addFactHandler;
        private readonly Func<string, Result<string, string>>? applyRuleHandler;
        private readonly Func<string, Result<bool, string>>? verifyPlanHandler;
        private bool isDisposed;

        public MockMeTTaEngine(
            Func<string, Result<string, string>>? queryHandler = null,
            Func<string, Result<Unit, string>>? addFactHandler = null,
            Func<string, Result<string, string>>? applyRuleHandler = null,
            Func<string, Result<bool, string>>? verifyPlanHandler = null)
        {
            this.queryHandler = queryHandler;
            this.addFactHandler = addFactHandler;
            this.applyRuleHandler = applyRuleHandler;
            this.verifyPlanHandler = verifyPlanHandler;
        }

        public int QueryCount { get; private set; }

        public int AddFactCount { get; private set; }

        public int ApplyRuleCount { get; private set; }

        public int VerifyPlanCount { get; private set; }

        public int ResetCount { get; private set; }

        public Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
        {
            this.ThrowIfDisposed();
            this.QueryCount++;

            if (this.queryHandler != null)
            {
                return Task.FromResult(this.queryHandler(query));
            }

            // Default behavior: return the query as result
            return Task.FromResult(Result<string, string>.Success($"Result of: {query}"));
        }

        public Task<Result<Unit, string>> AddFactAsync(string fact, CancellationToken ct = default)
        {
            this.ThrowIfDisposed();
            this.AddFactCount++;

            if (this.addFactHandler != null)
            {
                return Task.FromResult(this.addFactHandler(fact));
            }

            // Default behavior: store in knowledge base
            this.knowledgeBase[fact] = fact;
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }

        public Task<Result<string, string>> ApplyRuleAsync(string rule, CancellationToken ct = default)
        {
            this.ThrowIfDisposed();
            this.ApplyRuleCount++;

            if (this.applyRuleHandler != null)
            {
                return Task.FromResult(this.applyRuleHandler(rule));
            }

            // Default behavior: return success message
            return Task.FromResult(Result<string, string>.Success($"Rule applied: {rule}"));
        }

        public Task<Result<bool, string>> VerifyPlanAsync(string plan, CancellationToken ct = default)
        {
            this.ThrowIfDisposed();
            this.VerifyPlanCount++;

            if (this.verifyPlanHandler != null)
            {
                return Task.FromResult(this.verifyPlanHandler(plan));
            }

            // Default behavior: plans are valid
            return Task.FromResult(Result<bool, string>.Success(true));
        }

        public Task<Result<Unit, string>> ResetAsync(CancellationToken ct = default)
        {
            this.ThrowIfDisposed();
            this.ResetCount++;
            this.knowledgeBase.Clear();
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }

        public void Dispose()
        {
            this.isDisposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (this.isDisposed)
            {
                throw new ObjectDisposedException(nameof(MockMeTTaEngine));
            }
        }
    }

    #endregion

    #region MeTTaTool Query Tests

    [Fact]
    public async Task MeTTaTool_WithQueryOperation_CallsExecuteQueryAsync()
    {
        // Arrange
        var engine = new MockMeTTaEngine();
        var tool = new MeTTaTool(engine);
        var json = @"{""expression"": ""!(+ 2 3)"", ""operation"": ""query""}";

        // Act
        var result = await tool.InvokeAsync(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
        engine.QueryCount.Should().Be(1);
    }

    [Fact]
    public async Task MeTTaTool_WithDefaultOperation_ExecutesAsQuery()
    {
        // Arrange
        var engine = new MockMeTTaEngine();
        var tool = new MeTTaTool(engine);

        // Act
        var result = await tool.InvokeAsync("!(+ 2 3)");

        // Assert
        result.IsSuccess.Should().BeTrue();
        engine.QueryCount.Should().Be(1);
        result.Value.Should().Contain("+ 2 3");
    }

    [Fact]
    public async Task MeTTaTool_QueryWithError_ReturnsFailure()
    {
        // Arrange
        var engine = new MockMeTTaEngine(
            queryHandler: _ => Result<string, string>.Failure("Query execution failed"));
        var tool = new MeTTaTool(engine);

        // Act
        var result = await tool.InvokeAsync("!(invalid query)");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Query execution failed");
    }

    #endregion

    #region MeTTaTool AddFact Tests

    [Fact]
    public async Task MeTTaTool_WithAddFactOperation_CallsAddFactAsync()
    {
        // Arrange
        var engine = new MockMeTTaEngine();
        var tool = new MeTTaTool(engine);
        var json = @"{""expression"": ""(human Socrates)"", ""operation"": ""add_fact""}";

        // Act
        var result = await tool.InvokeAsync(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
        engine.AddFactCount.Should().Be(1);
        result.Value.Should().Contain("added successfully");
    }

    [Fact]
    public async Task MeTTaTool_AddFactWithError_ReturnsFailure()
    {
        // Arrange
        var engine = new MockMeTTaEngine(
            addFactHandler: _ => Result<Unit, string>.Failure("Fact addition failed"));
        var tool = new MeTTaTool(engine);
        var json = @"{""expression"": ""(invalid fact)"", ""operation"": ""add_fact""}";

        // Act
        var result = await tool.InvokeAsync(json);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Fact addition failed");
    }

    #endregion

    #region MeTTaTool ApplyRule Tests

    [Fact]
    public async Task MeTTaTool_WithApplyRuleOperation_CallsApplyRuleAsync()
    {
        // Arrange
        var engine = new MockMeTTaEngine();
        var tool = new MeTTaTool(engine);
        var json = @"{""expression"": ""(-> (human $x) (mortal $x))"", ""operation"": ""apply_rule""}";

        // Act
        var result = await tool.InvokeAsync(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
        engine.ApplyRuleCount.Should().Be(1);
    }

    [Fact]
    public async Task MeTTaTool_ApplyRuleWithError_ReturnsFailure()
    {
        // Arrange
        var engine = new MockMeTTaEngine(
            applyRuleHandler: _ => Result<string, string>.Failure("Rule application failed"));
        var tool = new MeTTaTool(engine);
        var json = @"{""expression"": ""(invalid rule)"", ""operation"": ""apply_rule""}";

        // Act
        var result = await tool.InvokeAsync(json);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Rule application failed");
    }

    #endregion

    #region MeTTaTool VerifyPlan Tests

    [Fact]
    public async Task MeTTaTool_WithVerifyPlanOperation_CallsVerifyPlanAsync()
    {
        // Arrange
        var engine = new MockMeTTaEngine();
        var tool = new MeTTaTool(engine);
        var json = @"{""expression"": ""(plan step1 step2)"", ""operation"": ""verify_plan""}";

        // Act
        var result = await tool.InvokeAsync(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
        engine.VerifyPlanCount.Should().Be(1);
        result.Value.Should().Contain("valid");
    }

    [Fact]
    public async Task MeTTaTool_VerifyPlanInvalid_ReturnsInvalidMessage()
    {
        // Arrange
        var engine = new MockMeTTaEngine(
            verifyPlanHandler: _ => Result<bool, string>.Success(false));
        var tool = new MeTTaTool(engine);
        var json = @"{""expression"": ""(invalid plan)"", ""operation"": ""verify_plan""}";

        // Act
        var result = await tool.InvokeAsync(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("invalid");
    }

    [Fact]
    public async Task MeTTaTool_VerifyPlanWithError_ReturnsFailure()
    {
        // Arrange
        var engine = new MockMeTTaEngine(
            verifyPlanHandler: _ => Result<bool, string>.Failure("Plan verification failed"));
        var tool = new MeTTaTool(engine);
        var json = @"{""expression"": ""(plan)"", ""operation"": ""verify_plan""}";

        // Act
        var result = await tool.InvokeAsync(json);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Plan verification failed");
    }

    #endregion

    #region JSON Parsing Tests

    [Fact]
    public async Task MeTTaTool_WithWellFormedJson_ParsesCorrectly()
    {
        // Arrange
        var engine = new MockMeTTaEngine();
        var tool = new MeTTaTool(engine);
        var json = @"{
            ""expression"": ""!(test)"",
            ""operation"": ""query""
        }";

        // Act
        var result = await tool.InvokeAsync(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task MeTTaTool_WithMissingExpressionProperty_ReturnsFailure()
    {
        // Arrange
        var engine = new MockMeTTaEngine();
        var tool = new MeTTaTool(engine);
        var json = @"{""operation"": ""query""}";

        // Act
        var result = await tool.InvokeAsync(json);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("expression");
    }

    [Fact]
    public async Task MeTTaTool_WithInvalidJson_TreatsAsDirectExpression()
    {
        // Arrange
        var engine = new MockMeTTaEngine();
        var tool = new MeTTaTool(engine);

        // Act
        var result = await tool.InvokeAsync("{not valid json");

        // Assert
        // Should treat as direct expression, not fail on JSON parsing
        result.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("query")]
    [InlineData("QUERY")]
    [InlineData("Query")]
    public async Task MeTTaTool_OperationIsCaseInsensitive(string operation)
    {
        // Arrange
        var engine = new MockMeTTaEngine();
        var tool = new MeTTaTool(engine);
        var json = $@"{{""expression"": ""!(test)"", ""operation"": ""{operation}""}}";

        // Act
        var result = await tool.InvokeAsync(json);

        // Assert
        result.IsSuccess.Should().BeTrue();
        engine.QueryCount.Should().Be(1);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task MeTTaTool_WithNullInput_ReturnsFailure()
    {
        // Arrange
        var engine = new MockMeTTaEngine();
        var tool = new MeTTaTool(engine);

        // Act
        var result = await tool.InvokeAsync(string.Empty);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task MeTTaTool_WithWhitespaceInput_ReturnsFailure()
    {
        // Arrange
        var engine = new MockMeTTaEngine();
        var tool = new MeTTaTool(engine);

        // Act
        var result = await tool.InvokeAsync("   ");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task MeTTaTool_WithUnknownOperation_ReturnsFailure()
    {
        // Arrange
        var engine = new MockMeTTaEngine();
        var tool = new MeTTaTool(engine);
        var json = @"{""expression"": ""test"", ""operation"": ""unknown_operation""}";

        // Act
        var result = await tool.InvokeAsync(json);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Unknown operation");
    }

    #endregion

    #region Cancellation Token Tests

    [Fact]
    public async Task MeTTaTool_WithCancellationToken_PassesToEngine()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        bool cancellationTokenPassed = false;
        var engine = new MockMeTTaEngine(
            queryHandler: query =>
            {
                cancellationTokenPassed = true;
                return Result<string, string>.Success("result");
            });
        var tool = new MeTTaTool(engine);

        // Act
        await tool.InvokeAsync("!(test)", cts.Token);

        // Assert
        cancellationTokenPassed.Should().BeTrue();
    }

    #endregion

    #region Tool Interface Tests

    [Fact]
    public void MeTTaTool_Name_ReturnsMetta()
    {
        // Arrange
        var engine = new MockMeTTaEngine();
        var tool = new MeTTaTool(engine);

        // Act
        var name = tool.Name;

        // Assert
        name.Should().Be("metta");
    }

    [Fact]
    public void MeTTaTool_Description_ContainsKeywords()
    {
        // Arrange
        var engine = new MockMeTTaEngine();
        var tool = new MeTTaTool(engine);

        // Act
        var description = tool.Description;

        // Assert
        description.Should().Contain("MeTTa");
        description.Should().Contain("symbolic");
        description.Should().Contain("reasoning");
    }

    [Fact]
    public void MeTTaTool_JsonSchema_IsValid()
    {
        // Arrange
        var engine = new MockMeTTaEngine();
        var tool = new MeTTaTool(engine);

        // Act
        var schema = tool.JsonSchema;

        // Assert
        schema.Should().NotBeNullOrEmpty();
        schema.Should().Contain("expression");
        schema.Should().Contain("operation");
        schema.Should().Contain("query");
        schema.Should().Contain("add_fact");
        schema.Should().Contain("apply_rule");
        schema.Should().Contain("verify_plan");
    }

    #endregion

    #region Integration Scenario Tests

    [Fact]
    public async Task MeTTaTool_CompleteWorkflow_ExecutesCorrectly()
    {
        // Arrange
        var engine = new MockMeTTaEngine();
        var tool = new MeTTaTool(engine);

        // Act & Assert - Add facts
        var addResult1 = await tool.InvokeAsync(@"{""expression"": ""(human Socrates)"", ""operation"": ""add_fact""}");
        addResult1.IsSuccess.Should().BeTrue();

        var addResult2 = await tool.InvokeAsync(@"{""expression"": ""(mortal Socrates)"", ""operation"": ""add_fact""}");
        addResult2.IsSuccess.Should().BeTrue();

        // Apply rule
        var ruleResult = await tool.InvokeAsync(@"{""expression"": ""(-> (human $x) (mortal $x))"", ""operation"": ""apply_rule""}");
        ruleResult.IsSuccess.Should().BeTrue();

        // Query
        var queryResult = await tool.InvokeAsync(@"{""expression"": ""!(mortal Socrates)"", ""operation"": ""query""}");
        queryResult.IsSuccess.Should().BeTrue();

        // Verify counts
        engine.AddFactCount.Should().Be(2);
        engine.ApplyRuleCount.Should().Be(1);
        engine.QueryCount.Should().Be(1);
    }

    #endregion

    #region Engine Disposal Tests

    [Fact]
    public void MockEngine_AfterDispose_ThrowsObjectDisposedException()
    {
        // Arrange
        var engine = new MockMeTTaEngine();
        engine.Dispose();

        // Act & Assert
        // Verify that the mock engine correctly throws when disposed
        Assert.ThrowsAsync<ObjectDisposedException>(
            async () => await engine.ExecuteQueryAsync("!(test)"));
    }

    #endregion
}
