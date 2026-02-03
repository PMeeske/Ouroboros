// <copyright file="ThinkingModeTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Providers;

using System.Reactive.Linq;
using FluentAssertions;
using Ouroboros.Providers;
using Ouroboros.Tools;
using Xunit;

/// <summary>
/// Tests for thinking mode support in chat models.
/// </summary>
[Trait("Category", "Unit")]
public class ThinkingModeTests
{
    #region ThinkingResponse Tests

    [Fact]
    public void ThinkingResponse_FromRawText_ParsesThinkTags()
    {
        // Arrange
        string rawText = "<think>Let me analyze this step by step...</think>The answer is 42.";

        // Act
        var response = ThinkingResponse.FromRawText(rawText);

        // Assert
        response.HasThinking.Should().BeTrue();
        response.Thinking.Should().Be("Let me analyze this step by step...");
        response.Content.Should().Be("The answer is 42.");
    }

    [Fact]
    public void ThinkingResponse_FromRawText_ParsesThinkingTags()
    {
        // Arrange
        string rawText = "<thinking>I need to consider multiple factors...</thinking>Here is my conclusion.";

        // Act
        var response = ThinkingResponse.FromRawText(rawText);

        // Assert
        response.HasThinking.Should().BeTrue();
        response.Thinking.Should().Be("I need to consider multiple factors...");
        response.Content.Should().Be("Here is my conclusion.");
    }

    [Fact]
    public void ThinkingResponse_FromRawText_NoThinkingTags_ReturnsFalseHasThinking()
    {
        // Arrange
        string rawText = "This is a plain response without thinking.";

        // Act
        var response = ThinkingResponse.FromRawText(rawText);

        // Assert
        response.HasThinking.Should().BeFalse();
        response.Thinking.Should().BeNull();
        response.Content.Should().Be("This is a plain response without thinking.");
    }

    [Fact]
    public void ThinkingResponse_FromRawText_EmptyString_HandlesGracefully()
    {
        // Act
        var response = ThinkingResponse.FromRawText(string.Empty);

        // Assert
        response.HasThinking.Should().BeFalse();
        response.Content.Should().BeEmpty();
    }

    [Fact]
    public void ThinkingResponse_FromRawText_Null_HandlesGracefully()
    {
        // Act
        var response = ThinkingResponse.FromRawText(null!);

        // Assert
        response.HasThinking.Should().BeFalse();
        response.Content.Should().BeEmpty();
    }

    [Fact]
    public void ThinkingResponse_ToFormattedString_WithThinking_FormatsCorrectly()
    {
        // Arrange
        var response = new ThinkingResponse("My thinking process", "My final answer");

        // Act
        string formatted = response.ToFormattedString();

        // Assert
        formatted.Should().Contain("🤔 Thinking:");
        formatted.Should().Contain("My thinking process");
        formatted.Should().Contain("📝 Response:");
        formatted.Should().Contain("My final answer");
    }

    [Fact]
    public void ThinkingResponse_ToFormattedString_WithoutThinking_ReturnsContentOnly()
    {
        // Arrange
        var response = new ThinkingResponse(null, "My final answer");

        // Act
        string formatted = response.ToFormattedString();

        // Assert
        formatted.Should().Be("My final answer");
        formatted.Should().NotContain("🤔");
    }

    [Fact]
    public void ThinkingResponse_ToFormattedString_CustomPrefixes_UsesCustomPrefixes()
    {
        // Arrange
        var response = new ThinkingResponse("Thinking content", "Answer content");

        // Act
        string formatted = response.ToFormattedString("[THOUGHT]", "[ANSWER]");

        // Assert
        formatted.Should().Contain("[THOUGHT]Thinking content");
        formatted.Should().Contain("[ANSWER]Answer content");
    }

    [Fact]
    public void ThinkingResponse_TokenCounts_WhenProvided_AreAccessible()
    {
        // Arrange
        var response = new ThinkingResponse("Thinking", "Content", ThinkingTokens: 100, ContentTokens: 50);

        // Assert
        response.ThinkingTokens.Should().Be(100);
        response.ContentTokens.Should().Be(50);
    }

    #endregion

    #region ChatRuntimeSettings Tests

    [Fact]
    public void ChatRuntimeSettings_DefaultThinkingMode_IsAuto()
    {
        // Act
        var settings = new ChatRuntimeSettings();

        // Assert
        settings.ThinkingMode.Should().Be(ThinkingMode.Auto);
    }

    [Fact]
    public void ChatRuntimeSettings_ThinkingMode_CanBeSetToEnabled()
    {
        // Act
        var settings = new ChatRuntimeSettings(ThinkingMode: ThinkingMode.Enabled);

        // Assert
        settings.ThinkingMode.Should().Be(ThinkingMode.Enabled);
    }

    [Fact]
    public void ChatRuntimeSettings_ThinkingMode_CanBeSetToDisabled()
    {
        // Act
        var settings = new ChatRuntimeSettings(ThinkingMode: ThinkingMode.Disabled);

        // Assert
        settings.ThinkingMode.Should().Be(ThinkingMode.Disabled);
    }

    [Fact]
    public void ChatRuntimeSettings_ThinkingBudgetTokens_WhenSet_IsAccessible()
    {
        // Act
        var settings = new ChatRuntimeSettings(ThinkingBudgetTokens: 1000);

        // Assert
        settings.ThinkingBudgetTokens.Should().Be(1000);
    }

    #endregion

    #region Mock Thinking Chat Model Tests

    [Fact]
    public async Task MockThinkingChatModel_GenerateWithThinking_ReturnsThinkingResponse()
    {
        // Arrange
        var mockModel = new MockThinkingChatModel("Deep analysis", "Final answer");

        // Act
        var response = await mockModel.GenerateWithThinkingAsync("test prompt");

        // Assert
        response.HasThinking.Should().BeTrue();
        response.Thinking.Should().Be("Deep analysis");
        response.Content.Should().Be("Final answer");
    }

    [Fact]
    public async Task MockThinkingChatModel_GenerateText_ReturnsCombinedResponse()
    {
        // Arrange
        var mockModel = new MockThinkingChatModel("Deep analysis", "Final answer");

        // Act
        var text = await mockModel.GenerateTextAsync("test prompt");

        // Assert
        text.Should().Contain("Deep analysis");
        text.Should().Contain("Final answer");
    }

    #endregion

    #region ToolAwareChatModel Thinking Integration Tests

    [Fact]
    public void ToolAwareChatModel_SupportsThinking_ReturnsTrueForThinkingModel()
    {
        // Arrange
        var thinkingModel = new MockThinkingChatModel("thinking", "content");
        var registry = new ToolRegistry();
        var toolAwareModel = new ToolAwareChatModel(thinkingModel, registry);

        // Act & Assert
        toolAwareModel.SupportsThinking.Should().BeTrue();
    }

    [Fact]
    public void ToolAwareChatModel_SupportsThinking_ReturnsFalseForNonThinkingModel()
    {
        // Arrange
        var regularModel = new MockChatModel("content");
        var registry = new ToolRegistry();
        var toolAwareModel = new ToolAwareChatModel(regularModel, registry);

        // Act & Assert
        toolAwareModel.SupportsThinking.Should().BeFalse();
    }

    [Fact]
    public async Task ToolAwareChatModel_GenerateWithThinkingAndTools_ProcessesToolsInContent()
    {
        // Arrange
        var thinkingModel = new MockThinkingChatModel(
            "Let me think about using the math tool...",
            "Result: [TOOL:math 2+2]"
        );
        var registry = new ToolRegistry().WithTool(new SimpleMathToolForThinking());
        var toolAwareModel = new ToolAwareChatModel(thinkingModel, registry);

        // Act
        var (response, tools) = await toolAwareModel.GenerateWithThinkingAndToolsAsync("calculate 2+2");

        // Assert
        response.HasThinking.Should().BeTrue();
        response.Thinking.Should().Contain("math tool");
        response.Content.Should().Contain("[TOOL-RESULT:math] 4");
        tools.Should().HaveCount(1);
        tools[0].ToolName.Should().Be("math");
    }

    [Fact]
    public async Task ToolAwareChatModel_GenerateWithThinkingAndTools_DoesNotProcessToolsInThinking()
    {
        // Arrange
        var thinkingModel = new MockThinkingChatModel(
            "I could use [TOOL:math 1+1] here but I won't",
            "The answer is 2"
        );
        var registry = new ToolRegistry().WithTool(new SimpleMathToolForThinking());
        var toolAwareModel = new ToolAwareChatModel(thinkingModel, registry);

        // Act
        var (response, tools) = await toolAwareModel.GenerateWithThinkingAndToolsAsync("think about math");

        // Assert
        tools.Should().BeEmpty("tools in thinking should not be executed");
        response.Thinking.Should().Contain("[TOOL:math 1+1]", "thinking should remain unchanged");
    }

    #endregion
}

#region Mock Classes

/// <summary>
/// Mock chat model for testing.
/// </summary>
internal class MockChatModel : IChatCompletionModel
{
    private readonly string _response;

    public MockChatModel(string response)
    {
        _response = response;
    }

    public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        return Task.FromResult(_response);
    }
}

/// <summary>
/// Mock chat model that supports thinking mode for testing.
/// </summary>
internal class MockThinkingChatModel : IThinkingChatModel
{
    private readonly string _thinking;
    private readonly string _content;

    public MockThinkingChatModel(string thinking, string content)
    {
        _thinking = thinking;
        _content = content;
    }

    public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        var response = new ThinkingResponse(_thinking, _content);
        return Task.FromResult(response.ToFormattedString());
    }

    public Task<ThinkingResponse> GenerateWithThinkingAsync(string prompt, CancellationToken ct = default)
    {
        return Task.FromResult(new ThinkingResponse(_thinking, _content));
    }
}

/// <summary>
/// Simple math tool for testing thinking mode.
/// Note: This is intentionally a minimal mock that only handles specific expressions.
/// </summary>
internal class SimpleMathToolForThinking : ITool
{
    public string Name => "math";
    public string Description => "Performs basic math operations";
    public string? JsonSchema => null;

    public Task<Ouroboros.Core.Monads.Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            // Simple expression evaluation for testing
            var result = input.Trim() switch
            {
                "10+5" => "15",
                "2+2" => "4",
                "1+1" => "2",
                _ => "unknown"
            };
            return Task.FromResult(Ouroboros.Core.Monads.Result<string, string>.Success(result));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Ouroboros.Core.Monads.Result<string, string>.Failure(ex.Message));
        }
    }
}

#endregion
