// <copyright file="HybridSearchTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Pipeline.GraphRAG.Models;

namespace Ouroboros.Tests.GraphRAG;

/// <summary>
/// Tests for HybridSearchConfig and HybridSearchResult.
/// </summary>
[Trait("Category", "Unit")]
public class HybridSearchTests
{
    [Fact]
    public void HybridSearchConfig_Default_ShouldHaveBalancedWeights()
    {
        // Act
        var config = HybridSearchConfig.Default;

        // Assert
        config.VectorWeight.Should().Be(0.5);
        config.SymbolicWeight.Should().Be(0.5);
        config.MaxResults.Should().Be(10);
        config.MaxHops.Should().Be(2);
    }

    [Fact]
    public void HybridSearchConfig_VectorFocused_ShouldFavorVector()
    {
        // Act
        var config = HybridSearchConfig.VectorFocused;

        // Assert
        config.VectorWeight.Should().BeGreaterThan(config.SymbolicWeight);
    }

    [Fact]
    public void HybridSearchConfig_SymbolicFocused_ShouldFavorSymbolic()
    {
        // Act
        var config = HybridSearchConfig.SymbolicFocused;

        // Assert
        config.SymbolicWeight.Should().BeGreaterThan(config.VectorWeight);
    }

    [Fact]
    public void HybridSearchResult_Empty_ShouldHaveNoMatches()
    {
        // Act
        var result = HybridSearchResult.Empty;

        // Assert
        result.Matches.Should().BeEmpty();
        result.Inferences.Should().BeEmpty();
        result.ReasoningChain.Should().BeEmpty();
        result.TotalRelevance.Should().Be(0);
    }

    [Fact]
    public void HybridSearchResult_TotalRelevance_ShouldSumMatchScores()
    {
        // Arrange
        var matches = new List<SearchMatch>
        {
            new SearchMatch("e1", "Entity1", "Type", "Content", 0.8, 0.9, 0.7),
            new SearchMatch("e2", "Entity2", "Type", "Content", 0.6, 0.7, 0.5)
        };
        var result = new HybridSearchResult(matches, [], []);

        // Act & Assert
        result.TotalRelevance.Should().BeApproximately(1.4, 0.01);
    }

    [Fact]
    public void HybridSearchResult_TopMatches_ShouldReturnOrderedByRelevance()
    {
        // Arrange
        var matches = new List<SearchMatch>
        {
            new SearchMatch("e1", "Entity1", "Type", "Content", 0.5, 0.6, 0.4),
            new SearchMatch("e2", "Entity2", "Type", "Content", 0.9, 0.95, 0.85),
            new SearchMatch("e3", "Entity3", "Type", "Content", 0.7, 0.8, 0.6)
        };
        var result = new HybridSearchResult(matches, [], []);

        // Act
        var top2 = result.TopMatches(2).ToList();

        // Assert
        top2.Should().HaveCount(2);
        top2[0].EntityId.Should().Be("e2"); // Highest relevance
        top2[1].EntityId.Should().Be("e3"); // Second highest
    }

    [Fact]
    public void Inference_ShouldStorePremiseAndConclusion()
    {
        // Act
        var inference = new Inference(
            ["A is related to B", "B implies C"],
            "Therefore A relates to C",
            0.85,
            "TransitiveRule");

        // Assert
        inference.Premise.Should().HaveCount(2);
        inference.Conclusion.Should().Contain("relates to C");
        inference.Confidence.Should().Be(0.85);
        inference.Rule.Should().Be("TransitiveRule");
    }

    [Fact]
    public void ReasoningChainStep_ShouldCaptureStepDetails()
    {
        // Act
        var step = new ReasoningChainStep(
            1,
            "VectorSearch",
            "Found 5 similar entities",
            ["e1", "e2", "e3"]);

        // Assert
        step.StepNumber.Should().Be(1);
        step.Operation.Should().Be("VectorSearch");
        step.Description.Should().Contain("5 similar");
        step.EntitiesInvolved.Should().HaveCount(3);
    }
}
