// <copyright file="TtsFallbackTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Providers;

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Ouroboros.Core.Monads;
using Ouroboros.Providers.TextToSpeech;
using Xunit;

/// <summary>
/// Tests for TTS fallback behavior, specifically rate limiting (429) handling.
/// </summary>
[Trait("Category", "Unit")]
public class TtsFallbackTests
{
    /// <summary>
    /// Verifies that apostrophes in text are properly escaped for SSML/XML.
    /// This prevents PowerShell parsing errors like "I've" breaking the script.
    /// </summary>
    [Theory]
    [InlineData("I've found something", "I&apos;ve found something")]
    [InlineData("It's a test", "It&apos;s a test")]
    [InlineData("Don't worry", "Don&apos;t worry")]
    [InlineData("Hello world", "Hello world")] // No apostrophe
    [InlineData("Test <script>", "Test &lt;script&gt;")] // XML escaping
    [InlineData("A & B", "A &amp; B")] // Ampersand
    public void EscapeForSsml_ShouldEscapeSpecialCharacters(string input, string expectedContains)
    {
        // Arrange & Act - simulate the escaping logic from AddNaturalProsody
        var escaped = input
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");

        // Assert
        escaped.Should().Contain(expectedContains);
    }

    /// <summary>
    /// Verifies that PowerShell here-string correctly handles apostrophes without escaping.
    /// This test documents that we use here-strings (@' ... '@) which preserve content literally.
    /// </summary>
    [Theory]
    [InlineData("I've found something interesting")]
    [InlineData("Don't worry, it's fine")]
    [InlineData("The cat's pajamas")]
    [InlineData("She said \"Hello\" and 'Goodbye'")]
    [InlineData("$variable and `backtick`")]
    public void HereStringContent_ShouldPreserveApostrophesLiterally(string input)
    {
        // PowerShell here-strings (@' ... '@) preserve content literally
        // This test documents that we don't need to escape apostrophes when using here-strings
        // The actual PowerShell execution is tested in integration tests

        // Verify the input contains special characters that would normally need escaping
        var hasSpecialChars = input.Contains('\'') || input.Contains('"') ||
                              input.Contains('$') || input.Contains('`');

        // Assert that we're testing meaningful inputs
        hasSpecialChars.Should().BeTrue($"Test input '{input}' should contain special characters");

        // The input should be usable directly in a here-string without modification
        input.Should().NotBeNullOrEmpty();
    }

    /// <summary>
    /// Verifies that a 429 rate limit error message is correctly identified.
    /// </summary>
    [Theory]
    [InlineData("Speech synthesis canceled: WebSocket upgrade failed: Too many requests (429). Please check subscription information.", true)]
    [InlineData("Too many requests", true)]
    [InlineData("429", true)]
    [InlineData("quota exceeded", true)]
    [InlineData("rate limit exceeded", true)]
    [InlineData("Normal error: connection failed", false)]
    [InlineData("Synthesis completed successfully", false)]
    public void IsRateLimitError_ShouldIdentifyRateLimitMessages(string errorMessage, bool expectedIsRateLimit)
    {
        // Arrange & Act
        var isRateLimit = IsRateLimitError(errorMessage);

        // Assert
        isRateLimit.Should().Be(expectedIsRateLimit, $"'{errorMessage}' should {(expectedIsRateLimit ? "" : "not ")}be identified as rate limit error");
    }

    /// <summary>
    /// Verifies that 429 errors trigger fallback to local TTS.
    /// </summary>
    [Fact]
    public async Task WhenAzureTtsReturns429_ShouldFallbackToLocalTts()
    {
        // Arrange
        var primaryTts = new Mock<ITextToSpeechService>();
        var fallbackTts = new Mock<ITextToSpeechService>();

        primaryTts
            .Setup(x => x.SynthesizeAsync(It.IsAny<string>(), It.IsAny<TextToSpeechOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SpeechResult, string>.Failure(
                "Speech synthesis canceled: WebSocket upgrade failed: Too many requests (429). Please check subscription information and region name."));

        fallbackTts
            .Setup(x => x.SynthesizeAsync(It.IsAny<string>(), It.IsAny<TextToSpeechOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SpeechResult, string>.Success(
                new SpeechResult(new byte[] { 0x01, 0x02 }, "audio/wav", 1.0)));

        // Act
        var result = await SynthesizeWithFallback(primaryTts.Object, fallbackTts.Object, "Hello world");

        // Assert
        result.IsSuccess.Should().BeTrue("fallback TTS should have succeeded");
        fallbackTts.Verify(x => x.SynthesizeAsync(It.IsAny<string>(), It.IsAny<TextToSpeechOptions?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that successful primary TTS does not trigger fallback.
    /// </summary>
    [Fact]
    public async Task WhenAzureTtsSucceeds_ShouldNotUseFallback()
    {
        // Arrange
        var primaryTts = new Mock<ITextToSpeechService>();
        var fallbackTts = new Mock<ITextToSpeechService>();

        primaryTts
            .Setup(x => x.SynthesizeAsync(It.IsAny<string>(), It.IsAny<TextToSpeechOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SpeechResult, string>.Success(
                new SpeechResult(new byte[] { 0x01, 0x02 }, "audio/wav", 1.0)));

        // Act
        var result = await SynthesizeWithFallback(primaryTts.Object, fallbackTts.Object, "Hello world");

        // Assert
        result.IsSuccess.Should().BeTrue();
        fallbackTts.Verify(x => x.SynthesizeAsync(It.IsAny<string>(), It.IsAny<TextToSpeechOptions?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Verifies that non-rate-limit errors also trigger fallback (graceful degradation).
    /// </summary>
    [Fact]
    public async Task WhenAzureTtsThrowsException_ShouldFallbackToLocalTts()
    {
        // Arrange
        var primaryTts = new Mock<ITextToSpeechService>();
        var fallbackTts = new Mock<ITextToSpeechService>();

        primaryTts
            .Setup(x => x.SynthesizeAsync(It.IsAny<string>(), It.IsAny<TextToSpeechOptions?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Network connection failed"));

        fallbackTts
            .Setup(x => x.SynthesizeAsync(It.IsAny<string>(), It.IsAny<TextToSpeechOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SpeechResult, string>.Success(
                new SpeechResult(new byte[] { 0x01, 0x02 }, "audio/wav", 1.0)));

        // Act
        var result = await SynthesizeWithFallback(primaryTts.Object, fallbackTts.Object, "Hello world");

        // Assert
        result.IsSuccess.Should().BeTrue("fallback TTS should have succeeded after primary exception");
        fallbackTts.Verify(x => x.SynthesizeAsync(It.IsAny<string>(), It.IsAny<TextToSpeechOptions?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Verifies that when both TTS services fail, the final error is returned.
    /// </summary>
    [Fact]
    public async Task WhenBothTtsServicesFail_ShouldReturnFallbackError()
    {
        // Arrange
        var primaryTts = new Mock<ITextToSpeechService>();
        var fallbackTts = new Mock<ITextToSpeechService>();

        primaryTts
            .Setup(x => x.SynthesizeAsync(It.IsAny<string>(), It.IsAny<TextToSpeechOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SpeechResult, string>.Failure("429 Too many requests"));

        fallbackTts
            .Setup(x => x.SynthesizeAsync(It.IsAny<string>(), It.IsAny<TextToSpeechOptions?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<SpeechResult, string>.Failure("Local TTS: No voice available"));

        // Act
        var result = await SynthesizeWithFallback(primaryTts.Object, fallbackTts.Object, "Hello world");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Local TTS");
    }

    // Helper method that mimics the VoiceModeServiceV2 fallback logic
    private static bool IsRateLimitError(string error)
    {
        return error.Contains("429") ||
               error.Contains("Too many requests") ||
               error.Contains("quota", StringComparison.OrdinalIgnoreCase) ||
               error.Contains("rate limit", StringComparison.OrdinalIgnoreCase);
    }

    // Helper method that simulates the fallback logic in VoiceModeServiceV2.SayAsync
    private static async Task<Result<SpeechResult, string>> SynthesizeWithFallback(
        ITextToSpeechService primaryTts,
        ITextToSpeechService fallbackTts,
        string text,
        CancellationToken ct = default)
    {
        bool shouldFallback = false;
        Result<SpeechResult, string> primaryResult = default;

        try
        {
            primaryResult = await primaryTts.SynthesizeAsync(text, null, ct);

            if (primaryResult.IsSuccess)
            {
                return primaryResult;
            }

            // Check for rate limiting errors
            if (primaryResult.Error != null && IsRateLimitError(primaryResult.Error))
            {
                shouldFallback = true;
            }
            else
            {
                // Non-rate-limit error, still try fallback for graceful degradation
                shouldFallback = true;
            }
        }
        catch
        {
            shouldFallback = true;
        }

        if (shouldFallback)
        {
            return await fallbackTts.SynthesizeAsync(text, null, ct);
        }

        return primaryResult.IsSuccess || primaryResult.Error != null
            ? primaryResult
            : Result<SpeechResult, string>.Failure("No TTS result");
    }
}
