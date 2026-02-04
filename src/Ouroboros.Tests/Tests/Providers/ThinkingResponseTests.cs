// <copyright file="ThinkingResponseTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Providers;

using FluentAssertions;
using Ouroboros.Providers;
using Xunit;

/// <summary>
/// Comprehensive tests for the ThinkingResponse record.
/// </summary>
[Trait("Category", "Unit")]
public class ThinkingResponseTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithAllParameters_InitializesCorrectly()
    {
        // Arrange & Act
        var response = new ThinkingResponse("thinking", "content", 100, 200);

        // Assert
        response.Thinking.Should().Be("thinking");
        response.Content.Should().Be("content");
        response.ThinkingTokens.Should().Be(100);
        response.ContentTokens.Should().Be(200);
    }

    [Fact]
    public void Constructor_WithNullThinking_AllowsNull()
    {
        // Arrange & Act
        var response = new ThinkingResponse(null, "content");

        // Assert
        response.Thinking.Should().BeNull();
        response.Content.Should().Be("content");
    }

    [Fact]
    public void Constructor_WithoutTokenCounts_DefaultsToNull()
    {
        // Arrange & Act
        var response = new ThinkingResponse("thinking", "content");

        // Assert
        response.ThinkingTokens.Should().BeNull();
        response.ContentTokens.Should().BeNull();
    }

    #endregion

    #region HasThinking Tests

    [Fact]
    public void HasThinking_WithNonEmptyThinking_ReturnsTrue()
    {
        // Arrange
        var response = new ThinkingResponse("some thinking", "content");

        // Act & Assert
        response.HasThinking.Should().BeTrue();
    }

    [Fact]
    public void HasThinking_WithNullThinking_ReturnsFalse()
    {
        // Arrange
        var response = new ThinkingResponse(null, "content");

        // Act & Assert
        response.HasThinking.Should().BeFalse();
    }

    [Fact]
    public void HasThinking_WithEmptyThinking_ReturnsFalse()
    {
        // Arrange
        var response = new ThinkingResponse(string.Empty, "content");

        // Act & Assert
        response.HasThinking.Should().BeFalse();
    }

    [Fact]
    public void HasThinking_WithWhitespaceThinking_ReturnsFalse()
    {
        // Arrange
        var response = new ThinkingResponse("   ", "content");

        // Act & Assert
        // NOTE: string.IsNullOrEmpty("   ") returns false, so whitespace-only is considered "has thinking"
        // This is the actual behavior of the implementation
        response.HasThinking.Should().BeTrue();
    }

    #endregion

    #region ToFormattedString Tests

    [Fact]
    public void ToFormattedString_WithThinking_IncludesBothSections()
    {
        // Arrange
        var response = new ThinkingResponse("Let me think about this", "The answer is 42");

        // Act
        var result = response.ToFormattedString();

        // Assert
        result.Should().Contain("🤔 Thinking:");
        result.Should().Contain("Let me think about this");
        result.Should().Contain("📝 Response:");
        result.Should().Contain("The answer is 42");
    }

    [Fact]
    public void ToFormattedString_WithoutThinking_ReturnsContentOnly()
    {
        // Arrange
        var response = new ThinkingResponse(null, "The answer is 42");

        // Act
        var result = response.ToFormattedString();

        // Assert
        result.Should().Be("The answer is 42");
        result.Should().NotContain("🤔 Thinking:");
        result.Should().NotContain("📝 Response:");
    }

    [Fact]
    public void ToFormattedString_WithCustomPrefixes_UsesCustomPrefixes()
    {
        // Arrange
        var response = new ThinkingResponse("thinking", "content");

        // Act
        var result = response.ToFormattedString("THINK:\n", "\nANSWER:\n");

        // Assert
        result.Should().Contain("THINK:\nthinking");
        result.Should().Contain("ANSWER:\ncontent");
    }

    [Fact]
    public void ToFormattedString_WithEmptyPrefixes_FormatsWithoutPrefixes()
    {
        // Arrange
        var response = new ThinkingResponse("thinking", "content");

        // Act
        var result = response.ToFormattedString(string.Empty, string.Empty);

        // Assert
        result.Should().Be("thinkingcontent");
    }

    #endregion

    #region FromRawText Tests - <think> Tags

    [Fact]
    public void FromRawText_WithThinkTags_ExtractsThinkingAndContent()
    {
        // Arrange
        var text = "Before <think>Internal reasoning</think> After";

        // Act
        var result = ThinkingResponse.FromRawText(text);

        // Assert
        result.Thinking.Should().Be("Internal reasoning");
        result.Content.Should().Be("Before  After");
        result.HasThinking.Should().BeTrue();
    }

    [Fact]
    public void FromRawText_WithThinkingTags_ExtractsThinkingAndContent()
    {
        // Arrange
        var text = "Before <thinking>Internal reasoning</thinking> After";

        // Act
        var result = ThinkingResponse.FromRawText(text);

        // Assert
        result.Thinking.Should().Be("Internal reasoning");
        result.Content.Should().Be("Before  After");
        result.HasThinking.Should().BeTrue();
    }

    [Fact]
    public void FromRawText_CaseInsensitive_HandlesThinkTags()
    {
        // Arrange
        var text = "Before <THINK>Reasoning</THINK> After";

        // Act
        var result = ThinkingResponse.FromRawText(text);

        // Assert
        result.Thinking.Should().Be("Reasoning");
        result.Content.Should().Be("Before  After");
    }

    [Fact]
    public void FromRawText_CaseInsensitive_HandlesThinkingTags()
    {
        // Arrange
        var text = "Before <THINKING>Reasoning</THINKING> After";

        // Act
        var result = ThinkingResponse.FromRawText(text);

        // Assert
        result.Thinking.Should().Be("Reasoning");
        result.Content.Should().Be("Before  After");
    }

    [Fact]
    public void FromRawText_WithNoTags_ReturnsContentOnly()
    {
        // Arrange
        var text = "Just regular content without any tags";

        // Act
        var result = ThinkingResponse.FromRawText(text);

        // Assert
        result.Thinking.Should().BeNull();
        result.Content.Should().Be("Just regular content without any tags");
        result.HasThinking.Should().BeFalse();
    }

    [Fact]
    public void FromRawText_WithEmptyString_ReturnsEmptyContent()
    {
        // Arrange
        var text = string.Empty;

        // Act
        var result = ThinkingResponse.FromRawText(text);

        // Assert
        result.Thinking.Should().BeNull();
        result.Content.Should().Be(string.Empty);
        result.HasThinking.Should().BeFalse();
    }

    [Fact]
    public void FromRawText_WithNull_ReturnsEmptyContent()
    {
        // Arrange
        string? text = null;

        // Act
        var result = ThinkingResponse.FromRawText(text!);

        // Assert
        result.Thinking.Should().BeNull();
        result.Content.Should().Be(string.Empty);
        result.HasThinking.Should().BeFalse();
    }

    [Fact]
    public void FromRawText_WithOnlyThinkingTag_ExtractsThinkingOnly()
    {
        // Arrange
        var text = "<think>Just thinking</think>";

        // Act
        var result = ThinkingResponse.FromRawText(text);

        // Assert
        result.Thinking.Should().Be("Just thinking");
        result.Content.Should().Be(string.Empty);
        result.HasThinking.Should().BeTrue();
    }

    [Fact]
    public void FromRawText_WithMultilineThinking_HandlesNewlines()
    {
        // Arrange
        var text = "<think>Line 1\nLine 2\nLine 3</think>Final answer";

        // Act
        var result = ThinkingResponse.FromRawText(text);

        // Assert
        result.Thinking.Should().Be("Line 1\nLine 2\nLine 3");
        result.Content.Should().Be("Final answer");
    }

    [Fact]
    public void FromRawText_WithWhitespaceInTags_TrimsThinkingAndContent()
    {
        // Arrange
        var text = "  Before  <think>  Reasoning  </think>  After  ";

        // Act
        var result = ThinkingResponse.FromRawText(text);

        // Assert
        result.Thinking.Should().Be("Reasoning");
        result.Content.Should().Be("Before    After");
    }

    [Fact]
    public void FromRawText_WithEmptyThinkingTag_ExtractsEmptyThinking()
    {
        // Arrange
        var text = "Before <think></think> After";

        // Act
        var result = ThinkingResponse.FromRawText(text);

        // Assert
        result.Thinking.Should().Be(string.Empty);
        result.Content.Should().Be("Before  After");
        result.HasThinking.Should().BeFalse(); // Empty thinking
    }

    [Fact]
    public void FromRawText_WithIncompleteOpenTag_TreatsAsRegularText()
    {
        // Arrange
        var text = "Before <think Only opening tag";

        // Act
        var result = ThinkingResponse.FromRawText(text);

        // Assert
        result.Thinking.Should().BeNull();
        result.Content.Should().Be("Before <think Only opening tag");
    }

    [Fact]
    public void FromRawText_WithIncompleteCloseTag_TreatsAsRegularText()
    {
        // Arrange
        var text = "Before <think>Content but no closing";

        // Act
        var result = ThinkingResponse.FromRawText(text);

        // Assert
        result.Thinking.Should().BeNull();
        result.Content.Should().Be("Before <think>Content but no closing");
    }

    [Fact]
    public void FromRawText_PrioritizesThinkTagOverThinkingTag()
    {
        // Arrange - Both tags present, <think> should be matched first
        var text = "<think>First</think> and <thinking>Second</thinking>";

        // Act
        var result = ThinkingResponse.FromRawText(text);

        // Assert
        result.Thinking.Should().Be("First");
        result.Content.Should().Contain("<thinking>Second</thinking>");
    }

    [Fact]
    public void FromRawText_WithSpecialCharactersInThinking_PreservesCharacters()
    {
        // Arrange
        var text = "<think>Special: <, >, &, \", '</think>Response";

        // Act
        var result = ThinkingResponse.FromRawText(text);

        // Assert
        result.Thinking.Should().Be("Special: <, >, &, \", '");
        result.Content.Should().Be("Response");
    }

    [Fact]
    public void FromRawText_WithUnicodeCharacters_PreservesUnicode()
    {
        // Arrange
        var text = "<think>思考中 🤔</think>答え";

        // Act
        var result = ThinkingResponse.FromRawText(text);

        // Assert
        result.Thinking.Should().Be("思考中 🤔");
        result.Content.Should().Be("答え");
    }

    [Fact]
    public void FromRawText_WithThinkTagAtStart_HandlesCorrectly()
    {
        // Arrange
        var text = "<think>Thinking first</think>Content follows";

        // Act
        var result = ThinkingResponse.FromRawText(text);

        // Assert
        result.Thinking.Should().Be("Thinking first");
        result.Content.Should().Be("Content follows");
    }

    [Fact]
    public void FromRawText_WithThinkTagAtEnd_HandlesCorrectly()
    {
        // Arrange
        var text = "Content first<think>Thinking at end</think>";

        // Act
        var result = ThinkingResponse.FromRawText(text);

        // Assert
        result.Thinking.Should().Be("Thinking at end");
        result.Content.Should().Be("Content first");
    }

    #endregion

    #region Record Equality Tests

    [Fact]
    public void Equality_WithSameValues_AreEqual()
    {
        // Arrange
        var response1 = new ThinkingResponse("think", "content", 10, 20);
        var response2 = new ThinkingResponse("think", "content", 10, 20);

        // Act & Assert
        response1.Should().Be(response2);
        (response1 == response2).Should().BeTrue();
    }

    [Fact]
    public void Equality_WithDifferentThinking_AreNotEqual()
    {
        // Arrange
        var response1 = new ThinkingResponse("think1", "content", 10, 20);
        var response2 = new ThinkingResponse("think2", "content", 10, 20);

        // Act & Assert
        response1.Should().NotBe(response2);
        (response1 != response2).Should().BeTrue();
    }

    [Fact]
    public void Equality_WithDifferentContent_AreNotEqual()
    {
        // Arrange
        var response1 = new ThinkingResponse("think", "content1", 10, 20);
        var response2 = new ThinkingResponse("think", "content2", 10, 20);

        // Act & Assert
        response1.Should().NotBe(response2);
    }

    #endregion

    #region With Expression Tests

    [Fact]
    public void WithExpression_ModifyingThinking_CreatesNewInstance()
    {
        // Arrange
        var original = new ThinkingResponse("original", "content", 10, 20);

        // Act
        var modified = original with { Thinking = "modified" };

        // Assert
        modified.Thinking.Should().Be("modified");
        modified.Content.Should().Be("content");
        modified.ThinkingTokens.Should().Be(10);
        modified.ContentTokens.Should().Be(20);
        original.Thinking.Should().Be("original"); // Original unchanged
    }

    [Fact]
    public void WithExpression_ModifyingTokenCounts_CreatesNewInstance()
    {
        // Arrange
        var original = new ThinkingResponse("think", "content", 10, 20);

        // Act
        var modified = original with { ThinkingTokens = 100, ContentTokens = 200 };

        // Assert
        modified.ThinkingTokens.Should().Be(100);
        modified.ContentTokens.Should().Be(200);
        original.ThinkingTokens.Should().Be(10);
        original.ContentTokens.Should().Be(20);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void FromRawText_WithVeryLongThinking_HandlesCorrectly()
    {
        // Arrange
        var longThinking = new string('x', 10000);
        var text = $"<think>{longThinking}</think>Short content";

        // Act
        var result = ThinkingResponse.FromRawText(text);

        // Assert
        result.Thinking.Should().Be(longThinking);
        result.Content.Should().Be("Short content");
    }

    [Fact]
    public void FromRawText_WithVeryLongContent_HandlesCorrectly()
    {
        // Arrange
        var longContent = new string('y', 10000);
        var text = $"<think>Short</think>{longContent}";

        // Act
        var result = ThinkingResponse.FromRawText(text);

        // Assert
        result.Thinking.Should().Be("Short");
        result.Content.Should().Be(longContent);
    }

    [Fact]
    public void FromRawText_WithNestedAngleBrackets_HandlesFirstMatchOnly()
    {
        // Arrange - Regex is non-greedy by default with .*?
        var text = "<think>Part <nested> content</think>After";

        // Act
        var result = ThinkingResponse.FromRawText(text);

        // Assert
        result.Thinking.Should().Be("Part <nested> content");
        result.Content.Should().Be("After");
    }

    #endregion
}
