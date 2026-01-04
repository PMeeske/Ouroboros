// <copyright file="ContradictionDetectorTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.LawsOfForm;

using FluentAssertions;
using Ouroboros.Core.LawsOfForm;
using Xunit;

/// <summary>
/// Tests for ContradictionDetector.
/// </summary>
[Trait("Category", "Unit")]
public class ContradictionDetectorTests
{
    [Fact]
    public void Analyze_ConsistentResponse_ReturnsMark()
    {
        // Arrange
        var extractor = new SimpleClaimExtractor();
        var detector = new ContradictionDetector(extractor);
        var response = new LlmResponse(
            "The sky is blue. Water is wet. Grass is green.",
            confidence: 0.9);

        // Act
        var result = detector.Analyze(response);

        // Assert
        result.IsMark().Should().BeTrue();
    }

    [Fact]
    public void Analyze_InsufficientClaims_ReturnsVoid()
    {
        // Arrange
        var extractor = new SimpleClaimExtractor();
        var detector = new ContradictionDetector(extractor);
        var response = new LlmResponse("Hello.", confidence: 0.9);

        // Act
        var result = detector.Analyze(response);

        // Assert
        result.IsVoid().Should().BeTrue();
    }

    [Fact]
    public void Analyze_WithContradiction_ReturnsImaginary()
    {
        // Arrange
        var extractor = new MockClaimExtractor(new[]
        {
            new Claim("The sky is blue", 0.9, "model"),
            new Claim("The sky is not blue", 0.9, "model")
        });
        var detector = new ContradictionDetector(extractor);
        var response = new LlmResponse("contradictory text", confidence: 0.9);

        // Act
        var result = detector.Analyze(response);

        // Assert
        result.IsImaginary().Should().BeTrue();
    }

    [Fact]
    public void AnalyzeMultiple_ConsistentResponses_ReturnsMark()
    {
        // Arrange
        var extractor = new MockClaimExtractor(new[]
        {
            new Claim("The earth is round", 0.9, "model1"),
            new Claim("The earth is spherical", 0.9, "model2")
        });
        var detector = new ContradictionDetector(extractor);
        var responses = new[]
        {
            new LlmResponse("text1", confidence: 0.9),
            new LlmResponse("text2", confidence: 0.9)
        };

        // Act
        var result = detector.AnalyzeMultiple(responses);

        // Assert
        result.IsMark().Should().BeTrue();
    }

    [Fact]
    public void AnalyzeMultiple_ContradictoryResponses_ReturnsImaginary()
    {
        // Arrange
        var claims = new List<Claim>();
        var extractor = new MockClaimExtractor(claims);
        var detector = new ContradictionDetector(extractor);

        // First response claims
        claims.Add(new Claim("Python is the best language", 0.9, "model1"));

        // Second response claims (contradictory)
        claims.Add(new Claim("Python is not the best language", 0.9, "model2"));

        var responses = new[]
        {
            new LlmResponse("text1", confidence: 0.9),
            new LlmResponse("text2", confidence: 0.9)
        };

        // Act
        var result = detector.AnalyzeMultiple(responses);

        // Assert
        result.IsImaginary().Should().BeTrue();
    }

    [Fact]
    public void CheckPair_SimilarAndOpposite_ReturnsImaginary()
    {
        // Arrange
        var extractor = new SimpleClaimExtractor();
        var detector = new ContradictionDetector(extractor);
        var claim1 = new Claim("The sky is blue", 0.9, "model");
        var claim2 = new Claim("The sky is not blue", 0.9, "model");

        // Act
        var result = detector.CheckPair(claim1, claim2);

        // Assert
        result.IsImaginary().Should().BeTrue();
    }

    [Fact]
    public void CheckPair_SimilarAndConsistent_ReturnsMark()
    {
        // Arrange
        var extractor = new SimpleClaimExtractor();
        var detector = new ContradictionDetector(extractor);
        var claim1 = new Claim("The sky is blue", 0.9, "model1");
        var claim2 = new Claim("The sky is azure blue", 0.9, "model2");

        // Act
        var result = detector.CheckPair(claim1, claim2);

        // Assert
        result.IsMark().Should().BeTrue();
    }

    [Fact]
    public void CheckPair_Unrelated_ReturnsVoid()
    {
        // Arrange
        var extractor = new SimpleClaimExtractor();
        var detector = new ContradictionDetector(extractor);
        var claim1 = new Claim("The sky is blue", 0.9, "model");
        var claim2 = new Claim("Water is wet", 0.9, "model");

        // Act
        var result = detector.CheckPair(claim1, claim2);

        // Assert
        result.IsVoid().Should().BeTrue();
    }

    [Fact]
    public void CheckPair_LowConfidence_ReturnsVoid()
    {
        // Arrange
        var extractor = new SimpleClaimExtractor();
        var detector = new ContradictionDetector(extractor);
        var claim1 = new Claim("The sky is blue", 0.5, "model"); // Low confidence
        var claim2 = new Claim("The sky is not blue", 0.9, "model");

        // Act
        var result = detector.CheckPair(claim1, claim2);

        // Assert
        result.IsVoid().Should().BeTrue();
    }

    [Fact]
    public void SimpleClaimExtractor_ExtractsSentences()
    {
        // Arrange
        var extractor = new SimpleClaimExtractor();
        var text = "This is sentence one. This is sentence two! And here is sentence three?";

        // Act
        var claims = extractor.ExtractClaims(text, "test");

        // Assert
        claims.Should().HaveCount(3);
        claims.All(c => c.Source == "test").Should().BeTrue();
    }

    [Fact]
    public void SimpleClaimExtractor_EmptyText_ReturnsEmpty()
    {
        // Arrange
        var extractor = new SimpleClaimExtractor();

        // Act
        var claims = extractor.ExtractClaims(string.Empty, "test");

        // Assert
        claims.Should().BeEmpty();
    }

    [Fact]
    public void SimpleClaimExtractor_FiltersShortFragments()
    {
        // Arrange
        var extractor = new SimpleClaimExtractor();
        var text = "Hi. This is a longer sentence that should be kept.";

        // Act
        var claims = extractor.ExtractClaims(text, "test");

        // Assert
        claims.Should().HaveCount(1);
        claims[0].Statement.Should().Contain("longer sentence");
    }

    [Fact]
    public void Claim_ClampsConfidence()
    {
        // Arrange & Act
        var tooHigh = new Claim("test", 1.5, "source");
        var tooLow = new Claim("test", -0.5, "source");

        // Assert
        tooHigh.Confidence.Should().Be(1.0);
        tooLow.Confidence.Should().Be(0.0);
    }

    private sealed class MockClaimExtractor : IClaimExtractor
    {
        private readonly IReadOnlyList<Claim> claims;

        public MockClaimExtractor(IReadOnlyList<Claim> claims)
        {
            this.claims = claims;
        }

        public IReadOnlyList<Claim> ExtractClaims(string text, string source)
        {
            return this.claims;
        }
    }
}
