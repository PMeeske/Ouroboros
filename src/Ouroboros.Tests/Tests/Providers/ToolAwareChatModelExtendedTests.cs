// <copyright file="ToolAwareChatModelExtendedTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Providers;

using FluentAssertions;
using Ouroboros.Core.Monads;
using Ouroboros.Providers;
using Ouroboros.Tests.Mocks;
using Ouroboros.Tools;
using Xunit;

/// <summary>
/// Extended tests for the ToolAwareChatModel implementation.
/// </summary>
[Trait("Category", "Unit")]
public class ToolAwareChatModelExtendedTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidDependencies_Succeeds()
    {
        // Arrange
        var mockModel = new MockChatModel("Response");
        var registry = new ToolRegistry();

        // Act
        var toolAwareModel = new ToolAwareChatModel(mockModel, registry);

        // Assert
        toolAwareModel.Should().NotBeNull();
        toolAwareModel.InnerModel.Should().BeSameAs(mockModel);
    }

    [Fact]
    public void Constructor_WithNullModel_AllowsNull()
    {
        // Note: The current implementation uses primary constructor and doesn't validate null
        // This documents the actual behavior
        var registry = new ToolRegistry();

        // Act - Creating with null model will throw NullReferenceException when used
        var toolAwareModel = new ToolAwareChatModel(null!, registry);

        // Assert - It creates, but will fail when used
        toolAwareModel.InnerModel.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithNullRegistry_AllowsNull()
    {
        // Note: The current implementation uses primary constructor and doesn't validate null
        // This documents the actual behavior
        var mockModel = new MockChatModel("Response");

        // Act - Creating with null registry will throw NullReferenceException when used
        var toolAwareModel = new ToolAwareChatModel(mockModel, null!);

        // Assert - It creates, but will fail when tools are referenced
        toolAwareModel.Should().NotBeNull();
    }

    #endregion

    #region GenerateWithToolsAsync Tests

    [Fact]
    public async Task GenerateWithToolsAsync_NoToolCalls_ReturnsOriginalText()
    {
        // Arrange
        var mockModel = new MockChatModel("This is a plain response.");
        var registry = new ToolRegistry();
        var toolAwareModel = new ToolAwareChatModel(mockModel, registry);

        // Act
        var (text, tools) = await toolAwareModel.GenerateWithToolsAsync("test prompt");

        // Assert
        text.Should().Be("This is a plain response.");
        tools.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateWithToolsAsync_WithSingleToolCall_ExecutesTool()
    {
        // Arrange
        var mockModel = new MockChatModel("Calculate: [TOOL:math 10+5]");
        var registry = new ToolRegistry().WithTool(new MathTool());
        var toolAwareModel = new ToolAwareChatModel(mockModel, registry);

        // Act
        var (text, tools) = await toolAwareModel.GenerateWithToolsAsync("Calculate 10+5");

        // Assert
        tools.Should().HaveCount(1);
        tools[0].ToolName.Should().Be("math");
        tools[0].Arguments.Should().Be("10+5");
        tools[0].Output.Should().Be("15");
        text.Should().Contain("[TOOL-RESULT:math] 15");
    }

    [Fact]
    public async Task GenerateWithToolsAsync_WithMultipleToolCalls_ExecutesAll()
    {
        // Arrange
        var mockModel = new MockChatModel(
            "First: [TOOL:math 2*3] Second: [TOOL:math 4+5]");
        var registry = new ToolRegistry().WithTool(new MathTool());
        var toolAwareModel = new ToolAwareChatModel(mockModel, registry);

        // Act
        var (text, tools) = await toolAwareModel.GenerateWithToolsAsync("Calculate");

        // Assert
        tools.Should().HaveCount(2);
        tools[0].Output.Should().Be("6");
        tools[1].Output.Should().Be("9");
    }

    [Fact]
    public async Task GenerateWithToolsAsync_WithNonExistentTool_ReturnsError()
    {
        // Arrange
        var mockModel = new MockChatModel("[TOOL:unknown args]");
        var registry = new ToolRegistry();
        var toolAwareModel = new ToolAwareChatModel(mockModel, registry);

        // Act
        var (text, tools) = await toolAwareModel.GenerateWithToolsAsync("test");

        // Assert
        text.Should().Contain("error");
        text.Should().Contain("not found");
    }

    [Fact]
    public async Task GenerateWithToolsAsync_WithToolThatThrows_CapturesError()
    {
        // Arrange
        var mockModel = new MockChatModel("[TOOL:throwing test]");
        var registry = new ToolRegistry()
            .WithTool(new ThrowingMockTool("throwing"));
        var toolAwareModel = new ToolAwareChatModel(mockModel, registry);

        // Act
        var (text, tools) = await toolAwareModel.GenerateWithToolsAsync("test");

        // Assert
        tools.Should().HaveCount(1);
        tools[0].Output.Should().Contain("error");
    }

    [Fact]
    public async Task GenerateWithToolsAsync_WithEmptyToolArgs_HandlesGracefully()
    {
        // Arrange
        var mockModel = new MockChatModel("[TOOL:math]");
        var registry = new ToolRegistry().WithTool(new MathTool());
        var toolAwareModel = new ToolAwareChatModel(mockModel, registry);

        // Act
        var (text, tools) = await toolAwareModel.GenerateWithToolsAsync("test");

        // Assert
        tools.Should().HaveCount(1);
        // Empty args should result in empty input to MathTool which fails
        text.Should().Contain("[TOOL-RESULT:math]");
    }

    [Fact]
    public async Task GenerateWithToolsAsync_WithCustomTool_ExecutesSuccessfully()
    {
        // Arrange
        var customTool = new MockTool("echo", "Echoes input",
            input => Result<string, string>.Success($"Echo: {input}"));
        var mockModel = new MockChatModel("[TOOL:echo hello world]");
        var registry = new ToolRegistry().WithTool(customTool);
        var toolAwareModel = new ToolAwareChatModel(mockModel, registry);

        // Act
        var (text, tools) = await toolAwareModel.GenerateWithToolsAsync("test");

        // Assert
        tools.Should().HaveCount(1);
        tools[0].Output.Should().Be("Echo: hello world");
    }

    #endregion

    #region GenerateWithToolsResultAsync Tests

    [Fact]
    public async Task GenerateWithToolsResultAsync_OnSuccess_ReturnsSuccessResult()
    {
        // Arrange
        var mockModel = new MockChatModel("Response with [TOOL:math 3+3]");
        var registry = new ToolRegistry().WithTool(new MathTool());
        var toolAwareModel = new ToolAwareChatModel(mockModel, registry);

        // Act
        var result = await toolAwareModel.GenerateWithToolsResultAsync("test");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var (text, tools) = result.Value;
        tools.Should().HaveCount(1);
        text.Should().Contain("[TOOL-RESULT:math] 6");
    }

    [Fact]
    public async Task GenerateWithToolsResultAsync_OnModelError_ReturnsFailure()
    {
        // Arrange
        var mockModel = new ThrowingMockChatModel(new Exception("Model error"));
        var registry = new ToolRegistry();
        var toolAwareModel = new ToolAwareChatModel(mockModel, registry);

        // Act
        var result = await toolAwareModel.GenerateWithToolsResultAsync("test");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("failed");
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task GenerateWithToolsAsync_WithCancellationToken_PropagatesToken()
    {
        // Arrange
        var mockModel = new Mocks.MockChatModel("Response", true);
        var registry = new ToolRegistry();
        var toolAwareModel = new ToolAwareChatModel(mockModel, registry);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await toolAwareModel.GenerateWithToolsAsync("test", cts.Token));
    }

    [Fact]
    public async Task GenerateWithToolsResultAsync_WithCancellation_ReturnsFailureResult()
    {
        // Arrange
        var mockModel = new Mocks.MockChatModel("Response", true);
        var registry = new ToolRegistry();
        var toolAwareModel = new ToolAwareChatModel(mockModel, registry);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act - GenerateWithToolsResultAsync catches exceptions and returns failure
        var result = await toolAwareModel.GenerateWithToolsResultAsync("test", cts.Token);

        // Assert - Exception is caught and wrapped as failure
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("failed");
    }

    #endregion

    #region Tool Pattern Matching Tests

    [Fact]
    public async Task GenerateWithToolsAsync_WithNestedBrackets_HandlesCorrectly()
    {
        // Arrange - Tool pattern is [TOOL:name args]
        var mockModel = new MockChatModel("Code: {\"test\": [1,2,3]} [TOOL:math 1+1]");
        var registry = new ToolRegistry().WithTool(new MathTool());
        var toolAwareModel = new ToolAwareChatModel(mockModel, registry);

        // Act
        var (text, tools) = await toolAwareModel.GenerateWithToolsAsync("test");

        // Assert
        tools.Should().HaveCount(1);
        tools[0].Output.Should().Be("2");
        text.Should().Contain("{\"test\": [1,2,3]}");
    }

    [Fact]
    public async Task GenerateWithToolsAsync_WithMultipleSameTools_ExecutesEach()
    {
        // Arrange
        var mockModel = new MockChatModel("[TOOL:math 1+1] [TOOL:math 2+2] [TOOL:math 3+3]");
        var registry = new ToolRegistry().WithTool(new MathTool());
        var toolAwareModel = new ToolAwareChatModel(mockModel, registry);

        // Act
        var (text, tools) = await toolAwareModel.GenerateWithToolsAsync("test");

        // Assert
        tools.Should().HaveCount(3);
        tools[0].Output.Should().Be("2");
        tools[1].Output.Should().Be("4");
        tools[2].Output.Should().Be("6");
    }

    [Fact]
    public async Task GenerateWithToolsAsync_WithToolsAndText_PreservesSurroundingText()
    {
        // Arrange
        var mockModel = new MockChatModel("The answer to 2+2 is [TOOL:math 2+2]. That's it!");
        var registry = new ToolRegistry().WithTool(new MathTool());
        var toolAwareModel = new ToolAwareChatModel(mockModel, registry);

        // Act
        var (text, tools) = await toolAwareModel.GenerateWithToolsAsync("test");

        // Assert
        text.Should().Contain("The answer to 2+2 is");
        text.Should().Contain("That's it!");
        text.Should().Contain("[TOOL-RESULT:math] 4");
    }

    #endregion

    #region Tool with Schema Tests

    [Fact]
    public async Task GenerateWithToolsAsync_WithToolHavingSchema_Works()
    {
        // Arrange
        var schemaJson = "{\"type\": \"object\", \"properties\": {\"value\": {\"type\": \"number\"}}}";
        var tool = new MockTool("typed_tool", "A typed tool", schemaJson);
        var mockModel = new MockChatModel("[TOOL:typed_tool {\"value\": 42}]");
        var registry = new ToolRegistry().WithTool(tool);
        var toolAwareModel = new ToolAwareChatModel(mockModel, registry);

        // Act
        var (text, tools) = await toolAwareModel.GenerateWithToolsAsync("test");

        // Assert
        tools.Should().HaveCount(1);
        tools[0].Arguments.Should().Contain("value");
    }

    #endregion

    #region Fail-Safe Tool Tests

    [Fact]
    public async Task GenerateWithToolsAsync_WithFailingTool_ContinuesWithOtherTools()
    {
        // Arrange
        var failingTool = new FailingMockTool("fail", "Tool failed!");
        var mathTool = new MathTool();
        var mockModel = new MockChatModel("[TOOL:fail test] then [TOOL:math 5+5]");
        var registry = new ToolRegistry()
            .WithTool(failingTool)
            .WithTool(mathTool);
        var toolAwareModel = new ToolAwareChatModel(mockModel, registry);

        // Act
        var (text, tools) = await toolAwareModel.GenerateWithToolsAsync("test");

        // Assert - Both tools should be attempted
        tools.Should().HaveCount(2);
        tools[1].Output.Should().Be("10");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task GenerateWithToolsAsync_WithEmptyResponse_ReturnsEmpty()
    {
        // Arrange
        var mockModel = new MockChatModel(string.Empty);
        var registry = new ToolRegistry();
        var toolAwareModel = new ToolAwareChatModel(mockModel, registry);

        // Act
        var (text, tools) = await toolAwareModel.GenerateWithToolsAsync("test");

        // Assert
        text.Should().BeEmpty();
        tools.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateWithToolsAsync_WithWhitespaceResponse_ReturnsWhitespace()
    {
        // Arrange
        var mockModel = new MockChatModel("   ");
        var registry = new ToolRegistry();
        var toolAwareModel = new ToolAwareChatModel(mockModel, registry);

        // Act
        var (text, tools) = await toolAwareModel.GenerateWithToolsAsync("test");

        // Assert
        text.Should().Be("   ");
        tools.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateWithToolsAsync_WithMalformedToolTag_HandlesGracefully()
    {
        // Arrange - Missing closing bracket
        var mockModel = new MockChatModel("[TOOL:math 2+2 some text");
        var registry = new ToolRegistry().WithTool(new MathTool());
        var toolAwareModel = new ToolAwareChatModel(mockModel, registry);

        // Act
        var (text, tools) = await toolAwareModel.GenerateWithToolsAsync("test");

        // Assert - Should not match incomplete tool tag
        text.Should().Be("[TOOL:math 2+2 some text");
        tools.Should().BeEmpty();
    }

    #endregion
}
