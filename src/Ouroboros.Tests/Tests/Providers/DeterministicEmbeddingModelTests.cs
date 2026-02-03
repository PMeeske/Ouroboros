// <copyright file="DeterministicEmbeddingModelTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Providers;

using FluentAssertions;
using Ouroboros.Providers;
using Xunit;

/// <summary>
/// Comprehensive tests for the DeterministicEmbeddingModel class.
/// </summary>
[Trait("Category", "Unit")]
public class DeterministicEmbeddingModelTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithDefaultDimension_CreatesModel()
    {
        // Act
        var model = new DeterministicEmbeddingModel();

        // Assert
        model.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithCustomDimension_CreatesModel()
    {
        // Act
        var model = new DeterministicEmbeddingModel(512);

        // Assert
        model.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithZeroDimension_UsesDefaultDimension()
    {
        // Arrange & Act
        var model = new DeterministicEmbeddingModel(0);
        var result = model.CreateEmbeddingsAsync("test").Result;

        // Assert
        result.Length.Should().Be(DeterministicEmbeddingModel.DefaultDimension);
    }

    [Fact]
    public void Constructor_WithNegativeDimension_UsesDefaultDimension()
    {
        // Arrange & Act
        var model = new DeterministicEmbeddingModel(-100);
        var result = model.CreateEmbeddingsAsync("test").Result;

        // Assert
        result.Length.Should().Be(DeterministicEmbeddingModel.DefaultDimension);
    }

    #endregion

    #region CreateEmbeddingsAsync - Basic Tests

    [Fact]
    public async Task CreateEmbeddingsAsync_WithValidInput_ReturnsVector()
    {
        // Arrange
        var model = new DeterministicEmbeddingModel();

        // Act
        var result = await model.CreateEmbeddingsAsync("test input");

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().Be(DeterministicEmbeddingModel.DefaultDimension);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WithCustomDimension_ReturnsCorrectSize()
    {
        // Arrange
        var dimension = 512;
        var model = new DeterministicEmbeddingModel(dimension);

        // Act
        var result = await model.CreateEmbeddingsAsync("test");

        // Assert
        result.Length.Should().Be(dimension);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WithEmptyString_ReturnsVector()
    {
        // Arrange
        var model = new DeterministicEmbeddingModel();

        // Act
        var result = await model.CreateEmbeddingsAsync(string.Empty);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().Be(DeterministicEmbeddingModel.DefaultDimension);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WithNull_ReturnsVector()
    {
        // Arrange
        var model = new DeterministicEmbeddingModel();

        // Act
        var result = await model.CreateEmbeddingsAsync(null!);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().Be(DeterministicEmbeddingModel.DefaultDimension);
    }

    #endregion

    #region Determinism Tests

    [Fact]
    public async Task CreateEmbeddingsAsync_SameInput_ReturnsSameVector()
    {
        // Arrange
        var model = new DeterministicEmbeddingModel();
        var input = "deterministic test";

        // Act
        var result1 = await model.CreateEmbeddingsAsync(input);
        var result2 = await model.CreateEmbeddingsAsync(input);

        // Assert
        result1.Should().Equal(result2);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_DifferentInput_ReturnsDifferentVector()
    {
        // Arrange
        var model = new DeterministicEmbeddingModel();

        // Act
        var result1 = await model.CreateEmbeddingsAsync("input one");
        var result2 = await model.CreateEmbeddingsAsync("input two");

        // Assert
        result1.Should().NotEqual(result2);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_SameInputMultipleCalls_ConsistentAcrossCalls()
    {
        // Arrange
        var model = new DeterministicEmbeddingModel();
        var input = "consistency test";

        // Act
        var results = new List<float[]>();
        for (int i = 0; i < 5; i++)
        {
            results.Add(await model.CreateEmbeddingsAsync(input));
        }

        // Assert
        for (int i = 1; i < results.Count; i++)
        {
            results[i].Should().Equal(results[0]);
        }
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_DifferentModels_SameInputReturnsSameVector()
    {
        // Arrange
        var model1 = new DeterministicEmbeddingModel();
        var model2 = new DeterministicEmbeddingModel();
        var input = "cross-instance test";

        // Act
        var result1 = await model1.CreateEmbeddingsAsync(input);
        var result2 = await model2.CreateEmbeddingsAsync(input);

        // Assert
        result1.Should().Equal(result2);
    }

    #endregion

    #region Normalization Tests

    [Fact]
    public async Task CreateEmbeddingsAsync_ReturnsNormalizedVector()
    {
        // Arrange
        var model = new DeterministicEmbeddingModel();

        // Act
        var result = await model.CreateEmbeddingsAsync("test");

        // Assert - Vector should be normalized (magnitude ≈ 1.0)
        var magnitude = Math.Sqrt(result.Sum(x => x * x));
        magnitude.Should().BeApproximately(1.0f, 0.0001f);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_AllVectors_AreNormalized()
    {
        // Arrange
        var model = new DeterministicEmbeddingModel();
        var inputs = new[] { "short", "medium length text", "a very long piece of text for testing", string.Empty };

        // Act & Assert
        foreach (var input in inputs)
        {
            var result = await model.CreateEmbeddingsAsync(input);
            var magnitude = Math.Sqrt(result.Sum(x => x * x));
            magnitude.Should().BeApproximately(1.0f, 0.0001f, $"Input: '{input}'");
        }
    }

    #endregion

    #region Long Text Compression Tests

    [Fact]
    public async Task CreateEmbeddingsAsync_WithLongText_ReturnsVector()
    {
        // Arrange
        var model = new DeterministicEmbeddingModel();
        var longText = new string('x', 3000); // > 2000 chars triggers compression

        // Act
        var result = await model.CreateEmbeddingsAsync(longText);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().Be(DeterministicEmbeddingModel.DefaultDimension);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_LongText_IsDeterministic()
    {
        // Arrange
        var model = new DeterministicEmbeddingModel();
        var longText = new string('a', 5000);

        // Act
        var result1 = await model.CreateEmbeddingsAsync(longText);
        var result2 = await model.CreateEmbeddingsAsync(longText);

        // Assert
        result1.Should().Equal(result2);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_LongText_DifferentFromShortVersion()
    {
        // Arrange
        var model = new DeterministicEmbeddingModel();
        var shortText = "test";
        var longText = new string('t', 2001) + "est"; // Forces compression path

        // Act
        var shortResult = await model.CreateEmbeddingsAsync(shortText);
        var longResult = await model.CreateEmbeddingsAsync(longText);

        // Assert
        shortResult.Should().NotEqual(longResult);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_VeryLongText_HandlesGracefully()
    {
        // Arrange
        var model = new DeterministicEmbeddingModel();
        var veryLongText = new string('x', 100000); // 100k characters

        // Act
        var result = await model.CreateEmbeddingsAsync(veryLongText);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().Be(DeterministicEmbeddingModel.DefaultDimension);
        var magnitude = Math.Sqrt(result.Sum(x => x * x));
        magnitude.Should().BeApproximately(1.0f, 0.0001f);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_LongTextWithVariedContent_ProducesDifferentVectors()
    {
        // Arrange
        var model = new DeterministicEmbeddingModel();
        var text1 = new string('a', 3000);
        var text2 = new string('b', 3000);

        // Act
        var result1 = await model.CreateEmbeddingsAsync(text1);
        var result2 = await model.CreateEmbeddingsAsync(text2);

        // Assert
        result1.Should().NotEqual(result2);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public async Task CreateEmbeddingsAsync_WithWhitespace_ReturnsVector()
    {
        // Arrange
        var model = new DeterministicEmbeddingModel();

        // Act
        var result = await model.CreateEmbeddingsAsync("   ");

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().Be(DeterministicEmbeddingModel.DefaultDimension);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WithNewlines_ReturnsVector()
    {
        // Arrange
        var model = new DeterministicEmbeddingModel();

        // Act
        var result = await model.CreateEmbeddingsAsync("line1\nline2\nline3");

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().Be(DeterministicEmbeddingModel.DefaultDimension);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WithSpecialCharacters_ReturnsVector()
    {
        // Arrange
        var model = new DeterministicEmbeddingModel();

        // Act
        var result = await model.CreateEmbeddingsAsync("!@#$%^&*()_+-=[]{}|;':\",./<>?");

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().Be(DeterministicEmbeddingModel.DefaultDimension);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WithUnicodeCharacters_ReturnsVector()
    {
        // Arrange
        var model = new DeterministicEmbeddingModel();

        // Act
        var result = await model.CreateEmbeddingsAsync("Hello 世界 🌍");

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().Be(DeterministicEmbeddingModel.DefaultDimension);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WithEmoji_ReturnsVector()
    {
        // Arrange
        var model = new DeterministicEmbeddingModel();

        // Act
        var result = await model.CreateEmbeddingsAsync("😀😃😄😁🤔💡");

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().Be(DeterministicEmbeddingModel.DefaultDimension);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task CreateEmbeddingsAsync_WithCancellationToken_Succeeds()
    {
        // Arrange
        var model = new DeterministicEmbeddingModel();
        using var cts = new CancellationTokenSource();

        // Act
        var result = await model.CreateEmbeddingsAsync("test", cts.Token);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().Be(DeterministicEmbeddingModel.DefaultDimension);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WithCancelledToken_CompletesImmediately()
    {
        // Arrange
        var model = new DeterministicEmbeddingModel();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act - Deterministic operation doesn't actually check cancellation
        // but should complete quickly
        var result = await model.CreateEmbeddingsAsync("test", cts.Token);

        // Assert
        result.Should().NotBeNull();
    }

    #endregion

    #region Vector Properties Tests

    [Fact]
    public async Task CreateEmbeddingsAsync_VectorValues_AreInValidRange()
    {
        // Arrange
        var model = new DeterministicEmbeddingModel();

        // Act
        var result = await model.CreateEmbeddingsAsync("test");

        // Assert - After normalization, values should be reasonable
        result.Should().OnlyContain(v => v >= -2.0f && v <= 2.0f);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_VectorContainsNoNaN()
    {
        // Arrange
        var model = new DeterministicEmbeddingModel();

        // Act
        var result = await model.CreateEmbeddingsAsync("test");

        // Assert
        result.Should().NotContain(float.NaN);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_VectorContainsNoInfinity()
    {
        // Arrange
        var model = new DeterministicEmbeddingModel();

        // Act
        var result = await model.CreateEmbeddingsAsync("test");

        // Assert
        result.Should().NotContain(float.PositiveInfinity);
        result.Should().NotContain(float.NegativeInfinity);
    }

    #endregion

    #region Different Dimensions Tests

    [Fact]
    public async Task CreateEmbeddingsAsync_WithDimension128_ReturnsCorrectSize()
    {
        // Arrange
        var model = new DeterministicEmbeddingModel(128);

        // Act
        var result = await model.CreateEmbeddingsAsync("test");

        // Assert
        result.Length.Should().Be(128);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_WithDimension1536_ReturnsCorrectSize()
    {
        // Arrange
        var model = new DeterministicEmbeddingModel(1536); // OpenAI ada-002 size

        // Act
        var result = await model.CreateEmbeddingsAsync("test");

        // Assert
        result.Length.Should().Be(1536);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_DifferentDimensions_ProduceDifferentVectors()
    {
        // Arrange
        var model768 = new DeterministicEmbeddingModel(768);
        var model512 = new DeterministicEmbeddingModel(512);
        var input = "test";

        // Act
        var result768 = await model768.CreateEmbeddingsAsync(input);
        var result512 = await model512.CreateEmbeddingsAsync(input);

        // Assert
        result768.Length.Should().Be(768);
        result512.Length.Should().Be(512);
        // Can't directly compare as different sizes, but both should be normalized
        var mag768 = Math.Sqrt(result768.Sum(x => x * x));
        var mag512 = Math.Sqrt(result512.Sum(x => x * x));
        mag768.Should().BeApproximately(1.0f, 0.0001f);
        mag512.Should().BeApproximately(1.0f, 0.0001f);
    }

    #endregion

    #region Hash-Based Behavior Tests

    [Fact]
    public async Task CreateEmbeddingsAsync_SimilarInputs_ProduceDifferentVectors()
    {
        // Arrange
        var model = new DeterministicEmbeddingModel();

        // Act
        var result1 = await model.CreateEmbeddingsAsync("test");
        var result2 = await model.CreateEmbeddingsAsync("Test"); // Different case

        // Assert
        result1.Should().NotEqual(result2);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_OneBitDifference_ProducesCompletelyDifferentVector()
    {
        // Arrange
        var model = new DeterministicEmbeddingModel();

        // Act
        var result1 = await model.CreateEmbeddingsAsync("test");
        var result2 = await model.CreateEmbeddingsAsync("text"); // One char different

        // Assert
        result1.Should().NotEqual(result2);
        // Vectors should be significantly different (hash avalanche effect)
        var differences = result1.Zip(result2, (a, b) => Math.Abs(a - b)).Count(d => d > 0.01f);
        differences.Should().BeGreaterThan(result1.Length / 2); // Most values should differ
    }

    #endregion

    #region Semantic Fingerprint Tests (Long Text Compression)

    [Fact]
    public async Task CreateEmbeddingsAsync_LongTextWithSameContent_IsDeterministic()
    {
        // Arrange
        var model = new DeterministicEmbeddingModel();
        var content = "This is a test sentence. ";
        var longText = string.Concat(Enumerable.Repeat(content, 100)); // 2400+ chars

        // Act
        var result1 = await model.CreateEmbeddingsAsync(longText);
        var result2 = await model.CreateEmbeddingsAsync(longText);

        // Assert
        result1.Should().Equal(result2);
    }

    [Fact]
    public async Task CreateEmbeddingsAsync_LongTextWithDifferentWords_ProducesDifferentVectors()
    {
        // Arrange
        var model = new DeterministicEmbeddingModel();
        var text1 = string.Concat(Enumerable.Repeat("apple ", 400));
        var text2 = string.Concat(Enumerable.Repeat("orange ", 400));

        // Act
        var result1 = await model.CreateEmbeddingsAsync(text1);
        var result2 = await model.CreateEmbeddingsAsync(text2);

        // Assert
        result1.Should().NotEqual(result2);
    }

    #endregion

    #region Const DefaultDimension Tests

    [Fact]
    public void DefaultDimension_MatchesNomicEmbedTextSize()
    {
        // Assert - nomic-embed-text uses 768 dimensions
        DeterministicEmbeddingModel.DefaultDimension.Should().Be(768);
    }

    #endregion
}
