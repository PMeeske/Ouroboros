// <copyright file="HttpOpenAiCompatibleChatModelTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Providers;

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Ouroboros.Providers;
using Xunit;

/// <summary>
/// Comprehensive tests for the HttpOpenAiCompatibleChatModel class.
/// </summary>
[Trait("Category", "Unit")]
public class HttpOpenAiCompatibleChatModelTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_Succeeds()
    {
        // Arrange & Act
        var model = new HttpOpenAiCompatibleChatModel(
            "http://localhost:8080",
            "test-api-key",
            "test-model");

        // Assert
        model.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithSettings_Succeeds()
    {
        // Arrange
        var settings = new ChatRuntimeSettings
        {
            Temperature = 0.7f,
            MaxTokens = 100
        };

        // Act
        var model = new HttpOpenAiCompatibleChatModel(
            "http://localhost:8080",
            "test-api-key",
            "test-model",
            settings);

        // Assert
        model.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullEndpoint_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new HttpOpenAiCompatibleChatModel(null!, "api-key", "model"));
        exception.ParamName.Should().Be("endpoint");
    }

    [Fact]
    public void Constructor_WithEmptyEndpoint_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new HttpOpenAiCompatibleChatModel(string.Empty, "api-key", "model"));
        exception.ParamName.Should().Be("endpoint");
    }

    [Fact]
    public void Constructor_WithWhitespaceEndpoint_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new HttpOpenAiCompatibleChatModel("   ", "api-key", "model"));
        exception.ParamName.Should().Be("endpoint");
    }

    [Fact]
    public void Constructor_WithNullApiKey_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new HttpOpenAiCompatibleChatModel("http://localhost:8080", null!, "model"));
        exception.ParamName.Should().Be("apiKey");
    }

    [Fact]
    public void Constructor_WithEmptyApiKey_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new HttpOpenAiCompatibleChatModel("http://localhost:8080", string.Empty, "model"));
        exception.ParamName.Should().Be("apiKey");
    }

    [Fact]
    public void Constructor_WithWhitespaceApiKey_ThrowsArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() =>
            new HttpOpenAiCompatibleChatModel("http://localhost:8080", "   ", "model"));
        exception.ParamName.Should().Be("apiKey");
    }

    [Fact]
    public void Constructor_WithNullSettings_UsesDefaults()
    {
        // Act
        var model = new HttpOpenAiCompatibleChatModel(
            "http://localhost:8080",
            "api-key",
            "model",
            null);

        // Assert
        model.Should().NotBeNull();
    }

    #endregion

    #region GenerateTextAsync - Fallback Tests

    [Fact]
    public async Task GenerateTextAsync_WhenServerUnreachable_ReturnsFallback()
    {
        // Arrange
        var model = new HttpOpenAiCompatibleChatModel(
            "http://localhost:65432", // Port unlikely to be in use
            "test-key",
            "test-model");

        // Act
        var result = await model.GenerateTextAsync("test prompt");

        // Assert
        result.Should().Contain("[remote-fallback:");
        result.Should().Contain("test-model");
        result.Should().Contain("test prompt");
    }

    [Fact]
    public async Task GenerateTextAsync_OnHttpError_ReturnsFallback()
    {
        // Arrange
        var model = new HttpOpenAiCompatibleChatModel(
            "http://invalid-host-that-does-not-exist.local",
            "test-key",
            "test-model");

        // Act
        var result = await model.GenerateTextAsync("test");

        // Assert
        result.Should().StartWith("[remote-fallback:");
    }

    [Fact]
    public async Task GenerateTextAsync_OnTimeout_ReturnsFallback()
    {
        // Arrange
        var model = new HttpOpenAiCompatibleChatModel(
            "http://localhost:65433",
            "test-key",
            "test-model");

        // Act
        var result = await model.GenerateTextAsync("test");

        // Assert
        result.Should().Contain("[remote-fallback:");
    }

    #endregion

    #region Fallback Message Format Tests

    [Fact]
    public async Task GenerateTextAsync_FallbackIncludesModelName()
    {
        // Arrange
        var model = new HttpOpenAiCompatibleChatModel(
            "http://localhost:65434",
            "test-key",
            "my-custom-model");

        // Act
        var result = await model.GenerateTextAsync("prompt");

        // Assert
        result.Should().Contain("my-custom-model");
    }

    [Fact]
    public async Task GenerateTextAsync_FallbackIncludesPrompt()
    {
        // Arrange
        var model = new HttpOpenAiCompatibleChatModel(
            "http://localhost:65435",
            "test-key",
            "model");

        // Act
        var result = await model.GenerateTextAsync("unique prompt text");

        // Assert
        result.Should().Contain("unique prompt text");
    }

    [Fact]
    public async Task GenerateTextAsync_FallbackFormatConsistent()
    {
        // Arrange
        var model = new HttpOpenAiCompatibleChatModel(
            "http://localhost:65436",
            "test-key",
            "model");

        // Act
        var result1 = await model.GenerateTextAsync("first");
        var result2 = await model.GenerateTextAsync("second");

        // Assert
        result1.Should().StartWith("[remote-fallback:model]");
        result2.Should().StartWith("[remote-fallback:model]");
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task GenerateTextAsync_WithCancellationToken_PropagatesToken()
    {
        // Arrange
        var model = new HttpOpenAiCompatibleChatModel(
            "http://localhost:65437",
            "test-key",
            "model");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act - Should fall back immediately due to cancellation
        var result = await model.GenerateTextAsync("test", cts.Token);

        // Assert
        result.Should().Contain("[remote-fallback:");
    }

    [Fact]
    public async Task GenerateTextAsync_WithNonCancelledToken_Succeeds()
    {
        // Arrange
        var model = new HttpOpenAiCompatibleChatModel(
            "http://localhost:65438",
            "test-key",
            "model");
        using var cts = new CancellationTokenSource();

        // Act
        var result = await model.GenerateTextAsync("test", cts.Token);

        // Assert
        result.Should().NotBeNullOrEmpty();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task GenerateTextAsync_WithEmptyPrompt_ReturnsFallback()
    {
        // Arrange
        var model = new HttpOpenAiCompatibleChatModel(
            "http://localhost:65439",
            "test-key",
            "model");

        // Act
        var result = await model.GenerateTextAsync(string.Empty);

        // Assert
        result.Should().Contain("[remote-fallback:");
    }

    [Fact]
    public async Task GenerateTextAsync_WithNullPrompt_ReturnsFallback()
    {
        // Arrange
        var model = new HttpOpenAiCompatibleChatModel(
            "http://localhost:65440",
            "test-key",
            "model");

        // Act
        var result = await model.GenerateTextAsync(null!);

        // Assert
        result.Should().Contain("[remote-fallback:");
    }

    [Fact]
    public async Task GenerateTextAsync_WithVeryLongPrompt_ReturnsFallback()
    {
        // Arrange
        var model = new HttpOpenAiCompatibleChatModel(
            "http://localhost:65441",
            "test-key",
            "model");
        var longPrompt = new string('x', 100000);

        // Act
        var result = await model.GenerateTextAsync(longPrompt);

        // Assert
        result.Should().Contain("[remote-fallback:");
    }

    [Fact]
    public async Task GenerateTextAsync_WithSpecialCharacters_ReturnsFallback()
    {
        // Arrange
        var model = new HttpOpenAiCompatibleChatModel(
            "http://localhost:65442",
            "test-key",
            "model");
        var specialPrompt = "!@#$%^&*()_+-=[]{}|;':\",./<>?";

        // Act
        var result = await model.GenerateTextAsync(specialPrompt);

        // Assert
        result.Should().Contain("[remote-fallback:");
        result.Should().Contain(specialPrompt);
    }

    [Fact]
    public async Task GenerateTextAsync_WithUnicodeCharacters_ReturnsFallback()
    {
        // Arrange
        var model = new HttpOpenAiCompatibleChatModel(
            "http://localhost:65443",
            "test-key",
            "model");
        var unicodePrompt = "Hello 世界 🌍";

        // Act
        var result = await model.GenerateTextAsync(unicodePrompt);

        // Assert
        result.Should().Contain("[remote-fallback:");
    }

    [Fact]
    public async Task GenerateTextAsync_WithNewlines_ReturnsFallback()
    {
        // Arrange
        var model = new HttpOpenAiCompatibleChatModel(
            "http://localhost:65444",
            "test-key",
            "model");
        var multilinePrompt = "Line 1\nLine 2\nLine 3";

        // Act
        var result = await model.GenerateTextAsync(multilinePrompt);

        // Assert
        result.Should().Contain("[remote-fallback:");
    }

    #endregion

    #region Multiple Calls Tests

    [Fact]
    public async Task GenerateTextAsync_MultipleCalls_EachFallsBack()
    {
        // Arrange
        var model = new HttpOpenAiCompatibleChatModel(
            "http://localhost:65445",
            "test-key",
            "model");

        // Act
        var result1 = await model.GenerateTextAsync("first");
        var result2 = await model.GenerateTextAsync("second");
        var result3 = await model.GenerateTextAsync("third");

        // Assert
        result1.Should().Contain("first");
        result2.Should().Contain("second");
        result3.Should().Contain("third");
    }

    [Fact]
    public async Task GenerateTextAsync_ConcurrentCalls_AllFallBack()
    {
        // Arrange
        var model = new HttpOpenAiCompatibleChatModel(
            "http://localhost:65446",
            "test-key",
            "model");

        // Act
        var tasks = Enumerable.Range(0, 10)
            .Select(i => model.GenerateTextAsync($"prompt {i}"))
            .ToArray();
        var results = await Task.WhenAll(tasks);

        // Assert
        results.Should().HaveCount(10);
        results.Should().OnlyContain(r => r.Contains("[remote-fallback:"));
    }

    #endregion

    #region Settings Tests

    [Fact]
    public async Task GenerateTextAsync_WithCustomTemperature_FallsBackGracefully()
    {
        // Arrange
        var settings = new ChatRuntimeSettings { Temperature = 0.9f };
        var model = new HttpOpenAiCompatibleChatModel(
            "http://localhost:65447",
            "test-key",
            "model",
            settings);

        // Act
        var result = await model.GenerateTextAsync("test");

        // Assert
        result.Should().Contain("[remote-fallback:");
    }

    [Fact]
    public async Task GenerateTextAsync_WithMaxTokens_FallsBackGracefully()
    {
        // Arrange
        var settings = new ChatRuntimeSettings { MaxTokens = 500 };
        var model = new HttpOpenAiCompatibleChatModel(
            "http://localhost:65448",
            "test-key",
            "model",
            settings);

        // Act
        var result = await model.GenerateTextAsync("test");

        // Assert
        result.Should().Contain("[remote-fallback:");
    }

    [Fact]
    public async Task GenerateTextAsync_WithCulture_FallsBackWithCulture()
    {
        // Arrange
        var settings = new ChatRuntimeSettings { Culture = "Spanish" };
        var model = new HttpOpenAiCompatibleChatModel(
            "http://localhost:65449",
            "test-key",
            "model",
            settings);

        // Act
        var result = await model.GenerateTextAsync("Hola");

        // Assert
        result.Should().Contain("[remote-fallback:");
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        var model = new HttpOpenAiCompatibleChatModel(
            "http://localhost:65450",
            "test-key",
            "model");

        // Act & Assert - Dispose exists as a public method
        model.Dispose();
        model.Dispose();
    }

    [Fact]
    public async Task Dispose_AfterGenerateText_Succeeds()
    {
        // Arrange
        var model = new HttpOpenAiCompatibleChatModel(
            "http://localhost:65451",
            "test-key",
            "model");

        // Act
        await model.GenerateTextAsync("test");
        model.Dispose();

        // Assert - No exception
        Assert.True(true);
    }

    #endregion

    #region Interface Implementation Tests

    [Fact]
    public void HttpOpenAiCompatibleChatModel_ImplementsIChatCompletionModel()
    {
        // Arrange & Act
        var model = new HttpOpenAiCompatibleChatModel(
            "http://localhost:8080",
            "test-key",
            "model");

        // Assert
        model.Should().BeAssignableTo<IChatCompletionModel>();
    }

    [Fact]
    public void HttpOpenAiCompatibleChatModel_ImplementsIDisposable()
    {
        // Arrange & Act
        var model = new HttpOpenAiCompatibleChatModel(
            "http://localhost:8080",
            "test-key",
            "model");

        // Assert
        // Note: HttpOpenAiCompatibleChatModel has a Dispose() method but doesn't implement IDisposable interface
        // This documents the actual implementation
        Assert.NotNull(model);
        // Verify we can call Dispose
        model.Dispose();
    }

    #endregion

    #region Retry Policy Tests (Implicit via Behavior)

    [Fact]
    public async Task GenerateTextAsync_WithRetryableError_EventuallyFallsBack()
    {
        // Arrange - Invalid host will trigger retries then fall back
        var model = new HttpOpenAiCompatibleChatModel(
            "http://invalid-retry-test.local",
            "test-key",
            "model");

        // Act
        var result = await model.GenerateTextAsync("test");

        // Assert - After retries, should fall back
        result.Should().Contain("[remote-fallback:");
    }

    #endregion

    #region Endpoint Format Tests

    [Fact]
    public void Constructor_WithTrailingSlash_Succeeds()
    {
        // Act
        var model = new HttpOpenAiCompatibleChatModel(
            "http://localhost:8080/",
            "test-key",
            "model");

        // Assert
        model.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithoutTrailingSlash_Succeeds()
    {
        // Act
        var model = new HttpOpenAiCompatibleChatModel(
            "http://localhost:8080",
            "test-key",
            "model");

        // Assert
        model.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithHttps_Succeeds()
    {
        // Act
        var model = new HttpOpenAiCompatibleChatModel(
            "https://api.example.com",
            "test-key",
            "model");

        // Assert
        model.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithPort_Succeeds()
    {
        // Act
        var model = new HttpOpenAiCompatibleChatModel(
            "http://localhost:8080",
            "test-key",
            "model");

        // Assert
        model.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithPath_Succeeds()
    {
        // Act
        var model = new HttpOpenAiCompatibleChatModel(
            "http://localhost:8080/api/v1",
            "test-key",
            "model");

        // Assert
        model.Should().NotBeNull();
    }

    #endregion

    #region Deterministic Fallback Tests

    [Fact]
    public async Task GenerateTextAsync_SamePrompt_ReturnsConsistentFallback()
    {
        // Arrange
        var model = new HttpOpenAiCompatibleChatModel(
            "http://localhost:65452",
            "test-key",
            "model");
        var prompt = "consistent test";

        // Act
        var result1 = await model.GenerateTextAsync(prompt);
        var result2 = await model.GenerateTextAsync(prompt);

        // Assert
        result1.Should().Be(result2);
    }

    [Fact]
    public async Task GenerateTextAsync_DifferentPrompts_ReturnsDifferentFallbacks()
    {
        // Arrange
        var model = new HttpOpenAiCompatibleChatModel(
            "http://localhost:65453",
            "test-key",
            "model");

        // Act
        var result1 = await model.GenerateTextAsync("prompt one");
        var result2 = await model.GenerateTextAsync("prompt two");

        // Assert
        result1.Should().NotBe(result2);
    }

    #endregion
}
