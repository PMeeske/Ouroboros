// <copyright file="MultiModelRouterTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Providers;

using FluentAssertions;
using Ouroboros.Providers;
using Ouroboros.Tests.Mocks;
using Xunit;

/// <summary>
/// Comprehensive tests for the MultiModelRouter class.
/// </summary>
[Trait("Category", "Unit")]
public class MultiModelRouterTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidModels_Succeeds()
    {
        // Arrange
        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["default"] = new MockChatModel("default response"),
            ["coder"] = new MockChatModel("code response"),
        };

        // Act
        var router = new MultiModelRouter(models, "default");

        // Assert
        router.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithEmptyModels_ThrowsArgumentException()
    {
        // Arrange
        var models = new Dictionary<string, IChatCompletionModel>();

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new MultiModelRouter(models, "default"));
        exception.ParamName.Should().Be("models");
    }

    [Fact]
    public void Constructor_WithNullModels_ThrowsException()
    {
        // Act & Assert
        // Note: MultiModelRouter accesses models.Count which throws NullReferenceException, not ArgumentNullException
        Assert.Throws<NullReferenceException>(() =>
            new MultiModelRouter(null!, "default"));
    }

    #endregion

    #region Fallback Model Tests

    [Fact]
    public async Task GenerateTextAsync_WithEmptyPrompt_UsesFallbackModel()
    {
        // Arrange
        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["default"] = new MockChatModel("default response"),
            ["coder"] = new MockChatModel("code response"),
        };
        var router = new MultiModelRouter(models, "default");

        // Act
        var result = await router.GenerateTextAsync(string.Empty);

        // Assert
        result.Should().Be("default response");
    }

    [Fact]
    public async Task GenerateTextAsync_WithWhitespacePrompt_UsesFallbackModel()
    {
        // Arrange
        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["default"] = new MockChatModel("default response"),
            ["coder"] = new MockChatModel("code response"),
        };
        var router = new MultiModelRouter(models, "default");

        // Act
        var result = await router.GenerateTextAsync("   ");

        // Assert
        result.Should().Be("default response");
    }

    [Fact]
    public async Task GenerateTextAsync_WithNullPrompt_UsesFallbackModel()
    {
        // Arrange
        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["default"] = new MockChatModel("default response"),
            ["coder"] = new MockChatModel("code response"),
        };
        var router = new MultiModelRouter(models, "default");

        // Act
        var result = await router.GenerateTextAsync(null!);

        // Assert
        result.Should().Be("default response");
    }

    [Fact]
    public async Task GenerateTextAsync_WithNonMatchingPrompt_UsesFallbackModel()
    {
        // Arrange
        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["default"] = new MockChatModel("default response"),
            ["coder"] = new MockChatModel("code response"),
        };
        var router = new MultiModelRouter(models, "default");

        // Act
        var result = await router.GenerateTextAsync("What is the weather?");

        // Assert
        result.Should().Be("default response");
    }

    [Fact]
    public async Task GenerateTextAsync_WhenFallbackKeyNotPresent_UsesFirstModel()
    {
        // Arrange
        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["model1"] = new MockChatModel("model1 response"),
            ["model2"] = new MockChatModel("model2 response"),
        };
        var router = new MultiModelRouter(models, "nonexistent");

        // Act
        var result = await router.GenerateTextAsync("generic prompt");

        // Assert
        // Should use first model when fallback key doesn't exist
        result.Should().BeOneOf("model1 response", "model2 response");
    }

    #endregion

    #region Code Model Routing Tests

    [Fact]
    public async Task GenerateTextAsync_WithCodeKeyword_UsesCoderModel()
    {
        // Arrange
        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["default"] = new MockChatModel("default response"),
            ["coder"] = new MockChatModel("code response"),
        };
        var router = new MultiModelRouter(models, "default");

        // Act
        var result = await router.GenerateTextAsync("Write some code");

        // Assert
        result.Should().Be("code response");
    }

    [Fact]
    public async Task GenerateTextAsync_WithCodeUpperCase_UsesCoderModel()
    {
        // Arrange
        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["default"] = new MockChatModel("default response"),
            ["coder"] = new MockChatModel("code response"),
        };
        var router = new MultiModelRouter(models, "default");

        // Act
        var result = await router.GenerateTextAsync("Write some CODE");

        // Assert
        result.Should().Be("code response");
    }

    [Fact]
    public async Task GenerateTextAsync_WithCodeMixedCase_UsesCoderModel()
    {
        // Arrange
        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["default"] = new MockChatModel("default response"),
            ["coder"] = new MockChatModel("code response"),
        };
        var router = new MultiModelRouter(models, "default");

        // Act
        var result = await router.GenerateTextAsync("Write some CoDe");

        // Assert
        result.Should().Be("code response");
    }

    [Fact]
    public async Task GenerateTextAsync_WithCodeInMiddle_UsesCoderModel()
    {
        // Arrange
        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["default"] = new MockChatModel("default response"),
            ["coder"] = new MockChatModel("code response"),
        };
        var router = new MultiModelRouter(models, "default");

        // Act
        var result = await router.GenerateTextAsync("Can you help me with code review?");

        // Assert
        result.Should().Be("code response");
    }

    [Fact]
    public async Task GenerateTextAsync_WithCodeKeywordNoCoderModel_UsesFallback()
    {
        // Arrange
        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["default"] = new MockChatModel("default response"),
        };
        var router = new MultiModelRouter(models, "default");

        // Act
        var result = await router.GenerateTextAsync("Write some code");

        // Assert
        result.Should().Be("default response");
    }

    #endregion

    #region Summarize Model Routing Tests

    [Fact]
    public async Task GenerateTextAsync_WithLongPrompt_UsesSummarizeModel()
    {
        // Arrange
        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["default"] = new MockChatModel("default response"),
            ["summarize"] = new MockChatModel("summary response"),
        };
        var router = new MultiModelRouter(models, "default");
        var longPrompt = new string('x', 601); // > 600 chars

        // Act
        var result = await router.GenerateTextAsync(longPrompt);

        // Assert
        result.Should().Be("summary response");
    }

    [Fact]
    public async Task GenerateTextAsync_WithExactly600Chars_UsesDefault()
    {
        // Arrange
        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["default"] = new MockChatModel("default response"),
            ["summarize"] = new MockChatModel("summary response"),
        };
        var router = new MultiModelRouter(models, "default");
        var prompt600 = new string('x', 600); // Exactly 600 chars

        // Act
        var result = await router.GenerateTextAsync(prompt600);

        // Assert
        result.Should().Be("default response");
    }

    [Fact]
    public async Task GenerateTextAsync_WithExactly601Chars_UsesSummarize()
    {
        // Arrange
        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["default"] = new MockChatModel("default response"),
            ["summarize"] = new MockChatModel("summary response"),
        };
        var router = new MultiModelRouter(models, "default");
        var prompt601 = new string('x', 601); // 601 chars

        // Act
        var result = await router.GenerateTextAsync(prompt601);

        // Assert
        result.Should().Be("summary response");
    }

    [Fact]
    public async Task GenerateTextAsync_WithLongPromptNoSummarizeModel_UsesFallback()
    {
        // Arrange
        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["default"] = new MockChatModel("default response"),
        };
        var router = new MultiModelRouter(models, "default");
        var longPrompt = new string('x', 1000);

        // Act
        var result = await router.GenerateTextAsync(longPrompt);

        // Assert
        result.Should().Be("default response");
    }

    #endregion

    #region Reason Model Routing Tests

    [Fact]
    public async Task GenerateTextAsync_WithReasonKeyword_UsesReasonModel()
    {
        // Arrange
        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["default"] = new MockChatModel("default response"),
            ["reason"] = new MockChatModel("reasoning response"),
        };
        var router = new MultiModelRouter(models, "default");

        // Act
        var result = await router.GenerateTextAsync("Can you reason about this?");

        // Assert
        result.Should().Be("reasoning response");
    }

    [Fact]
    public async Task GenerateTextAsync_WithReasonUpperCase_UsesReasonModel()
    {
        // Arrange
        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["default"] = new MockChatModel("default response"),
            ["reason"] = new MockChatModel("reasoning response"),
        };
        var router = new MultiModelRouter(models, "default");

        // Act
        var result = await router.GenerateTextAsync("Can you REASON about this?");

        // Assert
        result.Should().Be("reasoning response");
    }

    [Fact]
    public async Task GenerateTextAsync_WithReasonKeywordNoReasonModel_UsesFallback()
    {
        // Arrange
        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["default"] = new MockChatModel("default response"),
        };
        var router = new MultiModelRouter(models, "default");

        // Act
        var result = await router.GenerateTextAsync("Can you reason about this?");

        // Assert
        result.Should().Be("default response");
    }

    #endregion

    #region Priority Tests

    [Fact]
    public async Task GenerateTextAsync_CodeBeforeSummarize_RoutesToCoder()
    {
        // Arrange
        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["default"] = new MockChatModel("default response"),
            ["coder"] = new MockChatModel("code response"),
            ["summarize"] = new MockChatModel("summary response"),
        };
        var router = new MultiModelRouter(models, "default");
        var longPromptWithCode = new string('x', 700) + " code review";

        // Act
        var result = await router.GenerateTextAsync(longPromptWithCode);

        // Assert
        result.Should().Be("code response"); // Code takes priority over length
    }

    [Fact]
    public async Task GenerateTextAsync_LongPromptBeforeReason_RoutesToSummarize()
    {
        // Arrange
        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["default"] = new MockChatModel("default response"),
            ["summarize"] = new MockChatModel("summary response"),
            ["reason"] = new MockChatModel("reasoning response"),
        };
        var router = new MultiModelRouter(models, "default");
        var longPromptWithReason = new string('x', 700) + " reason about this";

        // Act
        var result = await router.GenerateTextAsync(longPromptWithReason);

        // Assert
        result.Should().Be("summary response"); // Length takes priority over reason keyword
    }

    [Fact]
    public async Task GenerateTextAsync_CodeAndReason_RoutesToCoder()
    {
        // Arrange
        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["default"] = new MockChatModel("default response"),
            ["coder"] = new MockChatModel("code response"),
            ["reason"] = new MockChatModel("reasoning response"),
        };
        var router = new MultiModelRouter(models, "default");

        // Act
        var result = await router.GenerateTextAsync("reason about this code");

        // Assert
        result.Should().Be("code response"); // Code checked first
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task GenerateTextAsync_WithCancellationToken_PropagatesToken()
    {
        // Arrange
        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["default"] = new Mocks.MockChatModel("response", true),
        };
        var router = new MultiModelRouter(models, "default");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await router.GenerateTextAsync("test", cts.Token));
    }

    #endregion

    #region Multiple Models Tests

    [Fact]
    public async Task GenerateTextAsync_WithAllSpecializedModels_RoutesCorrectly()
    {
        // Arrange
        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["default"] = new MockChatModel("default"),
            ["coder"] = new MockChatModel("coder"),
            ["summarize"] = new MockChatModel("summarizer"),
            ["reason"] = new MockChatModel("reasoner"),
        };
        var router = new MultiModelRouter(models, "default");

        // Act & Assert
        (await router.GenerateTextAsync("generic")).Should().Be("default");
        (await router.GenerateTextAsync("write code")).Should().Be("coder");
        (await router.GenerateTextAsync(new string('x', 700))).Should().Be("summarizer");
        (await router.GenerateTextAsync("reason carefully")).Should().Be("reasoner");
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task GenerateTextAsync_WithSingleModel_AlwaysUsesThatModel()
    {
        // Arrange
        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["only"] = new MockChatModel("only response"),
        };
        var router = new MultiModelRouter(models, "only");

        // Act & Assert
        (await router.GenerateTextAsync("test")).Should().Be("only response");
        (await router.GenerateTextAsync("code")).Should().Be("only response");
        (await router.GenerateTextAsync(new string('x', 700))).Should().Be("only response");
        (await router.GenerateTextAsync("reason")).Should().Be("only response");
    }

    [Fact]
    public async Task GenerateTextAsync_WithComplexPrompt_RoutesCorrectly()
    {
        // Arrange
        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["default"] = new MockChatModel("default response"),
            ["coder"] = new MockChatModel("code response"),
        };
        var router = new MultiModelRouter(models, "default");

        // Act
        var result = await router.GenerateTextAsync(
            "Please help me understand this code:\n\n```csharp\nvar x = 10;\n```");

        // Assert
        result.Should().Be("code response");
    }

    [Fact]
    public async Task GenerateTextAsync_WordCodeInNonCodeContext_StillRoutes()
    {
        // Arrange
        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["default"] = new MockChatModel("default response"),
            ["coder"] = new MockChatModel("code response"),
        };
        var router = new MultiModelRouter(models, "default");

        // Act - "code" appears but not in programming context
        var result = await router.GenerateTextAsync("What is the postal code?");

        // Assert
        result.Should().Be("code response"); // Still matches "code" keyword
    }

    #endregion

    #region Async Behavior Tests

    [Fact]
    public async Task GenerateTextAsync_CallsModelAsync()
    {
        // Arrange
        var mockModel = new Mocks.MockChatModel("response");
        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["default"] = mockModel,
        };
        var router = new MultiModelRouter(models, "default");

        // Act
        await router.GenerateTextAsync("test");

        // Assert
        mockModel.CallCount.Should().Be(1);
        mockModel.LastPrompt.Should().Be("test");
    }

    [Fact]
    public async Task GenerateTextAsync_PassesPromptToSelectedModel()
    {
        // Arrange
        var coderModel = new Mocks.MockChatModel("code response");
        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["default"] = new MockChatModel("default"),
            ["coder"] = coderModel,
        };
        var router = new MultiModelRouter(models, "default");

        // Act
        await router.GenerateTextAsync("write code for me");

        // Assert
        coderModel.LastPrompt.Should().Be("write code for me");
    }

    #endregion

    #region Model Selection Logic Tests

    [Fact]
    public async Task SelectModel_ChecksCodeFirst()
    {
        // This test verifies the priority order by checking the implementation behavior
        // Arrange
        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["default"] = new MockChatModel("default"),
            ["coder"] = new MockChatModel("coder"),
            ["summarize"] = new MockChatModel("summarizer"),
            ["reason"] = new MockChatModel("reasoner"),
        };
        var router = new MultiModelRouter(models, "default");

        // A very long prompt with code keyword should route to coder
        var prompt = new string('x', 700) + " code " + " reason ";
        var result = await router.GenerateTextAsync(prompt);

        // Assert
        result.Should().Be("coder");
    }

    [Fact]
    public async Task SelectModel_ChecksLengthSecond()
    {
        // Arrange - no code keyword, but long enough
        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["default"] = new MockChatModel("default"),
            ["summarize"] = new MockChatModel("summarizer"),
            ["reason"] = new MockChatModel("reasoner"),
        };
        var router = new MultiModelRouter(models, "default");

        // Long prompt with reason but no code
        var prompt = new string('x', 700) + " reason about this";
        var result = await router.GenerateTextAsync(prompt);

        // Assert
        result.Should().Be("summarizer");
    }

    [Fact]
    public async Task SelectModel_ChecksReasonThird()
    {
        // Arrange - short prompt with reason keyword
        var models = new Dictionary<string, IChatCompletionModel>
        {
            ["default"] = new MockChatModel("default"),
            ["reason"] = new MockChatModel("reasoner"),
        };
        var router = new MultiModelRouter(models, "default");

        var prompt = "please reason about this problem";
        var result = await router.GenerateTextAsync(prompt);

        // Assert
        result.Should().Be("reasoner");
    }

    #endregion
}
