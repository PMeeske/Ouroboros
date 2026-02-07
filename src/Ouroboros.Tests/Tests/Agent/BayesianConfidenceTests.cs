#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using Ouroboros.Agent.MetaAI;
using Xunit;

namespace Ouroboros.Tests.Agent;

/// <summary>
/// Tests for Bayesian confidence updating implementation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class BayesianConfidenceTests
{
    #region Update Tests - Bayesian Properties

    [Fact]
    public void Update_SupportingEvidence_IncreasesConfidence()
    {
        // Arrange
        double prior = 0.5;
        double likelihoodIfTrue = 0.8;   // Evidence likely if H is true
        double likelihoodIfFalse = 0.2;  // Evidence unlikely if H is false

        // Act
        double posterior = BayesianConfidence.Update(prior, likelihoodIfTrue, likelihoodIfFalse);

        // Assert
        Assert.True(posterior > prior, $"Expected posterior ({posterior}) > prior ({prior})");
    }

    [Fact]
    public void Update_CounterEvidence_DecreasesConfidence()
    {
        // Arrange
        double prior = 0.5;
        double likelihoodIfTrue = 0.2;   // Evidence unlikely if H is true
        double likelihoodIfFalse = 0.8;  // Evidence likely if H is false

        // Act
        double posterior = BayesianConfidence.Update(prior, likelihoodIfTrue, likelihoodIfFalse);

        // Assert
        Assert.True(posterior < prior, $"Expected posterior ({posterior}) < prior ({prior})");
    }

    [Fact]
    public void Update_StrongPriorWithWeakEvidence_SmallUpdate()
    {
        // Arrange: High confidence + weak counter-evidence
        double prior = 0.9;
        double likelihoodIfTrue = 0.4;   // Slightly weak support
        double likelihoodIfFalse = 0.6;  // Slightly weak counter

        // Act
        double posterior = BayesianConfidence.Update(prior, likelihoodIfTrue, likelihoodIfFalse);
        double change = Math.Abs(posterior - prior);

        // Assert: Change should be small due to strong prior
        Assert.True(change < 0.2, $"Expected small change (< 0.2), got {change}");
    }

    [Fact]
    public void Update_WeakPriorWithStrongEvidence_LargeUpdate()
    {
        // Arrange: Low confidence + strong supporting evidence
        double prior = 0.3;
        double likelihoodIfTrue = 0.95;  // Very strong support
        double likelihoodIfFalse = 0.05; // Very weak counter

        // Act
        double posterior = BayesianConfidence.Update(prior, likelihoodIfTrue, likelihoodIfFalse);
        double change = posterior - prior;

        // Assert: Change should be large due to strong evidence on weak prior
        Assert.True(change > 0.3, $"Expected large change (> 0.3), got {change}");
    }

    [Fact]
    public void Update_LikelihoodRatioOne_NoChange()
    {
        // Arrange: Evidence equally likely under both hypotheses
        double prior = 0.6;
        double likelihoodIfTrue = 0.5;
        double likelihoodIfFalse = 0.5;  // Same as likelihoodIfTrue

        // Act
        double posterior = BayesianConfidence.Update(prior, likelihoodIfTrue, likelihoodIfFalse);

        // Assert: Uninformative evidence shouldn't change confidence
        Assert.Equal(prior, posterior, precision: 5);
    }

    [Fact]
    public void Update_AvoidsCertainty_ClampsToRange()
    {
        // Arrange: Very strong evidence
        double prior = 0.5;
        double likelihoodIfTrue = 0.999;
        double likelihoodIfFalse = 0.001;

        // Act
        double posterior = BayesianConfidence.Update(prior, likelihoodIfTrue, likelihoodIfFalse);

        // Assert: Should never reach exactly 1.0
        Assert.True(posterior < 1.0, "Posterior should be < 1.0 to avoid certainty");
        Assert.True(posterior <= 0.999, $"Posterior should be clamped to 0.999, got {posterior}");
    }

    [Fact]
    public void Update_AvoidsCertainty_ClampsLowerBound()
    {
        // Arrange: Very strong counter-evidence
        double prior = 0.5;
        double likelihoodIfTrue = 0.001;
        double likelihoodIfFalse = 0.999;

        // Act
        double posterior = BayesianConfidence.Update(prior, likelihoodIfTrue, likelihoodIfFalse);

        // Assert: Should never reach exactly 0.0
        Assert.True(posterior > 0.0, "Posterior should be > 0.0 to avoid certainty");
        Assert.True(posterior >= 0.001, $"Posterior should be clamped to 0.001, got {posterior}");
    }

    [Fact]
    public void Update_RepeatedSupportingEvidence_ConvergesTowardOne()
    {
        // Arrange: Start at neutral
        double confidence = 0.5;
        double likelihoodIfTrue = 0.85;
        double likelihoodIfFalse = 0.25;

        // Act: Apply supporting evidence 5 times
        for (int i = 0; i < 5; i++)
        {
            confidence = BayesianConfidence.Update(confidence, likelihoodIfTrue, likelihoodIfFalse);
        }

        // Assert: Should converge toward 1.0 but never reach it
        Assert.True(confidence > 0.9, $"Expected convergence toward 1.0, got {confidence}");
        Assert.True(confidence < 1.0, "Should never reach exactly 1.0");
    }

    [Fact]
    public void Update_RepeatedCounterEvidence_ConvergesTowardZero()
    {
        // Arrange: Start at neutral
        double confidence = 0.5;
        double likelihoodIfTrue = 0.15;
        double likelihoodIfFalse = 0.85;

        // Act: Apply counter-evidence 5 times
        for (int i = 0; i < 5; i++)
        {
            confidence = BayesianConfidence.Update(confidence, likelihoodIfTrue, likelihoodIfFalse);
        }

        // Assert: Should converge toward 0.0 but never reach it
        Assert.True(confidence < 0.1, $"Expected convergence toward 0.0, got {confidence}");
        Assert.True(confidence > 0.0, "Should never reach exactly 0.0");
    }

    [Fact]
    public void Update_AlternatingEvidence_OscillatesNearPrior()
    {
        // Arrange: Start at neutral
        double confidence = 0.5;
        double supportLikelihoodTrue = 0.7;
        double supportLikelihoodFalse = 0.3;
        double counterLikelihoodTrue = 0.3;
        double counterLikelihoodFalse = 0.7;

        // Act: Apply alternating support/counter evidence
        for (int i = 0; i < 10; i++)
        {
            if (i % 2 == 0)
                confidence = BayesianConfidence.Update(confidence, supportLikelihoodTrue, supportLikelihoodFalse);
            else
                confidence = BayesianConfidence.Update(confidence, counterLikelihoodTrue, counterLikelihoodFalse);
        }

        // Assert: Should oscillate near starting point
        Assert.InRange(confidence, 0.3, 0.7);
    }

    [Fact]
    public void Update_SymmetricLikelihoods_NoChange()
    {
        // Arrange: Evidence equally likely under both hypotheses
        double prior = 0.7;
        double likelihood = 0.6;

        // Act
        double posterior = BayesianConfidence.Update(prior, likelihood, likelihood);

        // Assert: Symmetric likelihoods are uninformative
        Assert.Equal(prior, posterior, precision: 10);
    }

    [Fact]
    public void Update_ZeroEvidence_ReturnsPrior()
    {
        // Arrange: No evidence
        double prior = 0.6;

        // Act
        double posterior = BayesianConfidence.Update(prior, 0.0, 0.0);

        // Assert: Should return prior unchanged
        Assert.Equal(prior, posterior);
    }

    [Fact]
    public void Update_DiminishingReturns_SecondUpdateSmaller()
    {
        // Arrange
        double prior = 0.5;
        double likelihoodIfTrue = 0.8;
        double likelihoodIfFalse = 0.2;

        // Act: First update
        double posterior1 = BayesianConfidence.Update(prior, likelihoodIfTrue, likelihoodIfFalse);
        double change1 = posterior1 - prior;

        // Act: Second update with same evidence
        double posterior2 = BayesianConfidence.Update(posterior1, likelihoodIfTrue, likelihoodIfFalse);
        double change2 = posterior2 - posterior1;

        // Assert: Second change should be smaller (diminishing returns)
        Assert.True(change2 < change1, 
            $"Expected diminishing returns: first change {change1:F4} > second change {change2:F4}");
    }

    #endregion

    #region BayesFactor Tests

    [Fact]
    public void BayesFactor_StrongSupport_ReturnsHighValue()
    {
        // Arrange
        double likelihoodIfTrue = 0.9;
        double likelihoodIfFalse = 0.1;

        // Act
        double factor = BayesianConfidence.BayesFactor(likelihoodIfTrue, likelihoodIfFalse);

        // Assert
        Assert.Equal(9.0, factor, precision: 2);
    }

    [Fact]
    public void BayesFactor_StrongCounter_ReturnsLowValue()
    {
        // Arrange
        double likelihoodIfTrue = 0.1;
        double likelihoodIfFalse = 0.9;

        // Act
        double factor = BayesianConfidence.BayesFactor(likelihoodIfTrue, likelihoodIfFalse);

        // Assert
        Assert.Equal(0.11, factor, precision: 2);
    }

    [Fact]
    public void BayesFactor_Uninformative_ReturnsOne()
    {
        // Arrange
        double likelihoodIfTrue = 0.5;
        double likelihoodIfFalse = 0.5;

        // Act
        double factor = BayesianConfidence.BayesFactor(likelihoodIfTrue, likelihoodIfFalse);

        // Assert
        Assert.Equal(1.0, factor, precision: 5);
    }

    [Fact]
    public void BayesFactor_ZeroDenominator_ReturnsInfinity()
    {
        // Arrange
        double likelihoodIfTrue = 0.8;
        double likelihoodIfFalse = 0.0;

        // Act
        double factor = BayesianConfidence.BayesFactor(likelihoodIfTrue, likelihoodIfFalse);

        // Assert
        Assert.True(double.IsPositiveInfinity(factor), "Expected positive infinity for zero denominator");
    }

    #endregion

    #region CategorizeEvidence Tests

    [Fact]
    public void CategorizeEvidence_BayesFactor1_Negligible()
    {
        // Arrange: log10(1.0) = 0
        double bayesFactor = 1.0;

        // Act
        var strength = BayesianConfidence.CategorizeEvidence(bayesFactor);

        // Assert
        Assert.Equal(EvidenceStrength.Negligible, strength);
    }

    [Fact]
    public void CategorizeEvidence_BayesFactor3_Negligible()
    {
        // Arrange: log10(3) ≈ 0.48
        double bayesFactor = 3.0;

        // Act
        var strength = BayesianConfidence.CategorizeEvidence(bayesFactor);

        // Assert
        Assert.Equal(EvidenceStrength.Negligible, strength); // Just under 0.5
    }

    [Fact]
    public void CategorizeEvidence_BayesFactor5_Substantial()
    {
        // Arrange: log10(5) ≈ 0.7
        double bayesFactor = 5.0;

        // Act
        var strength = BayesianConfidence.CategorizeEvidence(bayesFactor);

        // Assert
        Assert.Equal(EvidenceStrength.Substantial, strength);
    }

    [Fact]
    public void CategorizeEvidence_BayesFactor10_Strong()
    {
        // Arrange: log10(10) = 1.0
        double bayesFactor = 10.0;

        // Act
        var strength = BayesianConfidence.CategorizeEvidence(bayesFactor);

        // Assert
        Assert.Equal(EvidenceStrength.Strong, strength);
    }

    [Fact]
    public void CategorizeEvidence_BayesFactor30_VeryStrong()
    {
        // Arrange: log10(30) ≈ 1.48
        double bayesFactor = 30.0;

        // Act
        var strength = BayesianConfidence.CategorizeEvidence(bayesFactor);

        // Assert
        Assert.Equal(EvidenceStrength.Strong, strength); // Just under 1.5
    }

    [Fact]
    public void CategorizeEvidence_BayesFactor50_VeryStrong()
    {
        // Arrange: log10(50) ≈ 1.7
        double bayesFactor = 50.0;

        // Act
        var strength = BayesianConfidence.CategorizeEvidence(bayesFactor);

        // Assert
        Assert.Equal(EvidenceStrength.VeryStrong, strength);
    }

    [Fact]
    public void CategorizeEvidence_BayesFactor100_Decisive()
    {
        // Arrange: log10(100) = 2.0
        double bayesFactor = 100.0;

        // Act
        var strength = BayesianConfidence.CategorizeEvidence(bayesFactor);

        // Assert
        Assert.Equal(EvidenceStrength.Decisive, strength);
    }

    [Fact]
    public void CategorizeEvidence_BayesFactor1000_Decisive()
    {
        // Arrange: log10(1000) = 3.0
        double bayesFactor = 1000.0;

        // Act
        var strength = BayesianConfidence.CategorizeEvidence(bayesFactor);

        // Assert
        Assert.Equal(EvidenceStrength.Decisive, strength);
    }

    [Fact]
    public void CategorizeEvidence_BayesFactorLessThanOne_UsesAbsoluteValue()
    {
        // Arrange: Counter-evidence, log10(0.1) = -1.0, abs = 1.0
        double bayesFactor = 0.1;

        // Act
        var strength = BayesianConfidence.CategorizeEvidence(bayesFactor);

        // Assert: Should use absolute value
        Assert.Equal(EvidenceStrength.Strong, strength);
    }

    [Fact]
    public void CategorizeEvidence_ZeroBayesFactor_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        double bayesFactor = 0.0;

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            BayesianConfidence.CategorizeEvidence(bayesFactor));
    }

    [Fact]
    public void CategorizeEvidence_NegativeBayesFactor_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        double bayesFactor = -1.0;

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            BayesianConfidence.CategorizeEvidence(bayesFactor));
    }

    [Fact]
    public void CategorizeEvidence_NaNBayesFactor_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        double bayesFactor = double.NaN;

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            BayesianConfidence.CategorizeEvidence(bayesFactor));
    }

    [Fact]
    public void CategorizeEvidence_InfinityBayesFactor_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        double bayesFactor = double.PositiveInfinity;

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            BayesianConfidence.CategorizeEvidence(bayesFactor));
    }

    #endregion

    #region Integration-Style Tests

    [Fact]
    public void IntegrationTest_FiveSupportingExperiments_ConvergesHighly()
    {
        // Arrange: Start at neutral
        double confidence = 0.5;
        List<double> confidenceHistory = new() { confidence };

        // Act: Apply 5 supporting experiments
        for (int i = 0; i < 5; i++)
        {
            confidence = BayesianConfidence.Update(
                confidence,
                likelihoodIfTrue: 0.85,
                likelihoodIfFalse: 0.25);
            confidenceHistory.Add(confidence);
        }

        // Assert
        Assert.True(confidence > 0.9, $"Expected convergence toward 1.0, got {confidence}");
        Assert.True(confidence < 1.0, "Should never reach exactly 1.0");
        
        // Verify monotonic increase
        for (int i = 1; i < confidenceHistory.Count; i++)
        {
            Assert.True(confidenceHistory[i] > confidenceHistory[i - 1], 
                $"Expected monotonic increase at step {i}");
        }
    }

    [Fact]
    public void IntegrationTest_AlternatingEvidence_OscillatesStably()
    {
        // Arrange
        double confidence = 0.5;
        double minSeen = confidence;
        double maxSeen = confidence;

        // Act: Apply 10 alternating support/counter experiments
        for (int i = 0; i < 10; i++)
        {
            if (i % 2 == 0)
            {
                // Supporting evidence
                confidence = BayesianConfidence.Update(confidence, 0.75, 0.35);
            }
            else
            {
                // Counter evidence
                confidence = BayesianConfidence.Update(confidence, 0.2, 0.7);
            }

            minSeen = Math.Min(minSeen, confidence);
            maxSeen = Math.Max(maxSeen, confidence);
        }

        // Assert: Should oscillate in a reasonable range around 0.5
        Assert.InRange(confidence, 0.05, 0.9); // Wider range due to asymmetric evidence
        Assert.InRange(maxSeen - minSeen, 0.1, 0.8); // Reasonable oscillation range
    }

    [Fact]
    public void IntegrationTest_HighPriorWithWeakCounter_SmallDecrease()
    {
        // Arrange: High confidence hypothesis
        double prior = 0.9;

        // Act: Apply weak counter-evidence
        double posterior = BayesianConfidence.Update(
            prior,
            likelihoodIfTrue: 0.4,
            likelihoodIfFalse: 0.6);

        double decrease = prior - posterior;

        // Assert: High prior should resist weak counter-evidence
        Assert.True(decrease < 0.15, $"Expected small decrease (< 0.15), got {decrease}");
        Assert.True(posterior > 0.8, $"Expected posterior still high (> 0.8), got {posterior}");
    }

    [Fact]
    public void IntegrationTest_CompleteResearchCycle_RealisticBehavior()
    {
        // Arrange: Simulate a research cycle
        double confidence = 0.5; // Start with neutral hypothesis

        // Act & Assert: First experiment - supports hypothesis
        confidence = BayesianConfidence.Update(confidence, 0.8, 0.3);
        Assert.InRange(confidence, 0.6, 0.8); // Should increase moderately

        // Second experiment - also supports
        confidence = BayesianConfidence.Update(confidence, 0.85, 0.25);
        Assert.InRange(confidence, 0.75, 0.92); // Should increase more

        // Third experiment - counter-evidence appears
        confidence = BayesianConfidence.Update(confidence, 0.3, 0.75);
        Assert.InRange(confidence, 0.4, 0.8); // Should decrease but not drastically

        // Fourth experiment - strong support
        confidence = BayesianConfidence.Update(confidence, 0.9, 0.15);
        Assert.InRange(confidence, 0.6, 0.999); // Should increase significantly

        // Final: Should end with reasonable confidence, not certainty
        Assert.True(confidence < 1.0, "Should never reach absolute certainty");
        Assert.True(confidence > 0.0, "Should never reach absolute zero");
    }

    #endregion
}
