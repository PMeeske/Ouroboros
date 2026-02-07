// <copyright file="RescorlaWagnerTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Tests.Consciousness;

using FluentAssertions;
using Ouroboros.Application.Personality.Consciousness;
using Xunit;

/// <summary>
/// Tests for the Rescorla-Wagner model of associative learning.
/// Validates the mathematical model and emergent phenomena (blocking, overshadowing, etc.).
/// </summary>
public sealed class RescorlaWagnerTests
{
    /// <summary>
    /// Test that reinforcement with no existing association produces a positive delta.
    /// When ΣV=0, the prediction error (λ - ΣV) is maximal, leading to strong learning.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Reinforce_WithNoExistingAssociation_ProducesPositiveDelta()
    {
        // Arrange
        double csSalience = 0.5;
        double usSalience = 0.5;
        double totalV = 0.0; // No existing association
        double lambda = 1.0;

        // Act
        double delta = RescorlaWagner.Reinforce(csSalience, usSalience, totalV, lambda);

        // Assert
        delta.Should().BeGreaterThan(0.0, "learning should occur when prediction error is high");
        delta.Should().Be(0.5 * 0.5 * (1.0 - 0.0), "delta should equal α*β*(λ-ΣV)");
        delta.Should().Be(0.25);
    }

    /// <summary>
    /// Test that reinforcement near maximum conditioning produces a small delta (diminishing returns).
    /// When ΣV ≈ λ, the prediction error approaches zero, so learning slows.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Reinforce_NearMaxConditioning_ProducesSmallDelta()
    {
        // Arrange
        double csSalience = 0.5;
        double usSalience = 0.5;
        double totalV = 0.95; // Near maximum
        double lambda = 1.0;

        // Act
        double delta = RescorlaWagner.Reinforce(csSalience, usSalience, totalV, lambda);

        // Assert
        delta.Should().BeGreaterThan(0.0, "some learning should still occur");
        delta.Should().BeLessThan(0.05, "learning should be minimal when near asymptote");
        delta.Should().Be(0.5 * 0.5 * (1.0 - 0.95));
        delta.Should().BeApproximately(0.0125, 0.0001);
    }

    /// <summary>
    /// Test that reinforcement at maximum conditioning produces zero delta (no further learning).
    /// When ΣV = λ, prediction error is zero, so ΔV = 0.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Reinforce_AtMaxConditioning_ProducesZeroDelta()
    {
        // Arrange
        double csSalience = 0.5;
        double usSalience = 0.5;
        double totalV = 1.0; // At maximum
        double lambda = 1.0;

        // Act
        double delta = RescorlaWagner.Reinforce(csSalience, usSalience, totalV, lambda);

        // Assert
        delta.Should().Be(0.0, "no learning should occur when prediction is perfect");
    }

    /// <summary>
    /// Test that extinction with existing association produces a negative delta.
    /// When US is absent (λ=0) but ΣV > 0, the prediction error is negative.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Extinguish_WithExistingAssociation_ProducesNegativeDelta()
    {
        // Arrange
        double csSalience = 0.5;
        double usSalience = 0.5;
        double totalV = 0.8; // Strong existing association

        // Act
        double delta = RescorlaWagner.Extinguish(csSalience, usSalience, totalV);

        // Assert
        delta.Should().BeLessThan(0.0, "association should weaken during extinction");
        delta.Should().Be(0.5 * 0.5 * (0.0 - 0.8), "delta should equal α*β*(0-ΣV)");
        delta.Should().Be(-0.2);
    }

    /// <summary>
    /// Test that extinction with no association produces zero delta.
    /// When ΣV = 0, there's nothing to extinguish.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Extinguish_WithNoAssociation_ProducesZeroDelta()
    {
        // Arrange
        double csSalience = 0.5;
        double usSalience = 0.5;
        double totalV = 0.0; // No association

        // Act
        double delta = RescorlaWagner.Extinguish(csSalience, usSalience, totalV);

        // Assert
        delta.Should().Be(0.0, "nothing to extinguish when association is zero");
    }

    /// <summary>
    /// Test that high salience produces larger learning delta than low salience.
    /// Salience (α) modulates learning rate — more salient stimuli are learned faster.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void HighSalience_ProducesLargerDelta_ThanLowSalience()
    {
        // Arrange
        double highSalience = 0.9;
        double lowSalience = 0.1;
        double usSalience = 0.5;
        double totalV = 0.0;
        double lambda = 1.0;

        // Act
        double highDelta = RescorlaWagner.Reinforce(highSalience, usSalience, totalV, lambda);
        double lowDelta = RescorlaWagner.Reinforce(lowSalience, usSalience, totalV, lambda);

        // Assert
        highDelta.Should().BeGreaterThan(lowDelta, "high salience should produce faster learning");
        highDelta.Should().Be(0.9 * 0.5 * 1.0);
        lowDelta.Should().Be(0.1 * 0.5 * 1.0);
        (highDelta / lowDelta).Should().Be(9.0, "learning rate should scale linearly with salience");
    }

    /// <summary>
    /// Test that ComputeDelta is symmetric in saliences (α*β = β*α).
    /// The order of CS and US salience shouldn't matter.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void ComputeDelta_IsSymmetricInSaliences()
    {
        // Arrange
        double salience1 = 0.3;
        double salience2 = 0.7;
        double maxConditioning = 1.0;
        double totalV = 0.5;

        // Act
        double delta1 = RescorlaWagner.ComputeDelta(salience1, salience2, maxConditioning, totalV);
        double delta2 = RescorlaWagner.ComputeDelta(salience2, salience1, maxConditioning, totalV);

        // Assert
        delta1.Should().Be(delta2, "multiplication is commutative");
    }

    /// <summary>
    /// Test the blocking effect: a pre-trained CS blocks learning to a new CS.
    /// Classic Kamin blocking experiment:
    /// 1. Train CS₁ → US until strong association
    /// 2. Train compound CS₁+CS₂ → US
    /// 3. CS₂ should acquire less association than if trained alone
    ///    because ΣV is already high from CS₁.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Blocking_Effect_PretrainedCsBlocksNewCs()
    {
        // Arrange - Pre-train CS1
        double csSalience = 0.5;
        double usSalience = 0.5;
        double cs1Strength = 0.0;

        // Phase 1: Train CS1 alone for 10 trials
        for (int i = 0; i < 10; i++)
        {
            double totalV = cs1Strength;
            double delta = RescorlaWagner.Reinforce(csSalience, usSalience, totalV, 1.0);
            cs1Strength += delta;
            cs1Strength = Math.Clamp(cs1Strength, 0.0, 1.0);
        }

        double cs1FinalStrength = cs1Strength;

        // Phase 2: Train compound CS1+CS2 for 5 trials
        double cs2Strength = 0.0;
        for (int i = 0; i < 5; i++)
        {
            double totalV = cs1Strength + cs2Strength;
            double delta1 = RescorlaWagner.Reinforce(csSalience, usSalience, totalV, 1.0);
            double delta2 = RescorlaWagner.Reinforce(csSalience, usSalience, totalV, 1.0);

            cs1Strength = Math.Clamp(cs1Strength + delta1, 0.0, 1.0);
            cs2Strength = Math.Clamp(cs2Strength + delta2, 0.0, 1.0);
        }

        // Control: Train CS3 alone for 5 trials (same as Phase 2 for CS2)
        double cs3Strength = 0.0;
        for (int i = 0; i < 5; i++)
        {
            double totalV = cs3Strength;
            double delta = RescorlaWagner.Reinforce(csSalience, usSalience, totalV, 1.0);
            cs3Strength += delta;
            cs3Strength = Math.Clamp(cs3Strength, 0.0, 1.0);
        }

        // Assert
        cs2Strength.Should().BeLessThan(cs3Strength, "CS2 should acquire less association due to blocking by CS1");
        cs1FinalStrength.Should().BeGreaterThan(0.8, "CS1 should be well-trained before compound training");
    }

    /// <summary>
    /// Test the overshadowing effect: more salient CS acquires more association strength.
    /// When CS₁ (high salience) and CS₂ (low salience) are trained together,
    /// CS₁ should acquire more association strength than CS₂.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Overshadowing_Effect_HighSalienceCsOvershadowsLowSalience()
    {
        // Arrange
        double highSalience = 0.9;
        double lowSalience = 0.1;
        double usSalience = 0.5;
        double cs1Strength = 0.0; // High salience CS
        double cs2Strength = 0.0; // Low salience CS

        // Train compound CS1+CS2 for 10 trials
        for (int i = 0; i < 10; i++)
        {
            double totalV = cs1Strength + cs2Strength;
            double delta1 = RescorlaWagner.Reinforce(highSalience, usSalience, totalV, 1.0);
            double delta2 = RescorlaWagner.Reinforce(lowSalience, usSalience, totalV, 1.0);

            cs1Strength = Math.Clamp(cs1Strength + delta1, 0.0, 1.0);
            cs2Strength = Math.Clamp(cs2Strength + delta2, 0.0, 1.0);
        }

        // Assert
        cs1Strength.Should().BeGreaterThan(cs2Strength, "high salience CS should overshadow low salience CS");
        cs1Strength.Should().BeGreaterThan(0.5, "high salience CS should acquire substantial association");
        cs2Strength.Should().BeLessThan(0.3, "low salience CS should acquire minimal association");
    }

    /// <summary>
    /// Test extinction curve: repeated extinction trials show decelerating decrease (not linear).
    /// The rate of extinction slows as association approaches zero.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Extinction_Curve_ShowsDeceleratingDecrease()
    {
        // Arrange - Start with strong association
        double csSalience = 0.5;
        double usSalience = 0.5;
        double strength = 0.8;

        // Track extinction deltas
        var deltas = new List<double>();

        // Apply 10 extinction trials
        for (int i = 0; i < 10; i++)
        {
            double totalV = strength;
            double delta = RescorlaWagner.Extinguish(csSalience, usSalience, totalV);
            deltas.Add(Math.Abs(delta));
            strength = Math.Clamp(strength + delta, 0.0, 1.0);
        }

        // Assert - Each delta should be smaller than the previous (diminishing effect)
        for (int i = 1; i < deltas.Count; i++)
        {
            deltas[i].Should().BeLessThan(deltas[i - 1],
                $"extinction delta at trial {i + 1} should be smaller than trial {i} (decelerating curve)");
        }

        strength.Should().BeLessThan(0.2, "association should be substantially weakened after extinction");
    }

    /// <summary>
    /// Test acquisition curve: repeated reinforcement shows decelerating increase (not linear).
    /// Learning rate slows as association approaches asymptote.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Acquisition_Curve_ShowsDeceleratingIncrease()
    {
        // Arrange
        double csSalience = 0.5;
        double usSalience = 0.5;
        double strength = 0.0;

        // Track reinforcement deltas
        var deltas = new List<double>();

        // Apply 15 reinforcement trials
        for (int i = 0; i < 15; i++)
        {
            double totalV = strength;
            double delta = RescorlaWagner.Reinforce(csSalience, usSalience, totalV, 1.0);
            deltas.Add(delta);
            strength = Math.Clamp(strength + delta, 0.0, 1.0);
        }

        // Assert - Each delta should be smaller than the previous (diminishing returns)
        for (int i = 1; i < deltas.Count; i++)
        {
            deltas[i].Should().BeLessThan(deltas[i - 1],
                $"learning delta at trial {i + 1} should be smaller than trial {i} (negatively accelerated curve)");
        }

        strength.Should().BeGreaterThan(0.9, "association should be strong after many trials");
    }

    /// <summary>
    /// Test that US salience (β) affects both acquisition and extinction rate.
    /// Higher US salience means both faster learning and faster extinction.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void UsSalience_AffectsBothAcquisitionAndExtinctionRate()
    {
        // Arrange
        double csSalience = 0.5;
        double highUsSalience = 0.9;
        double lowUsSalience = 0.1;

        // Acquisition with high US salience
        double highUsStrength = 0.0;
        for (int i = 0; i < 5; i++)
        {
            double delta = RescorlaWagner.Reinforce(csSalience, highUsSalience, highUsStrength, 1.0);
            highUsStrength = Math.Clamp(highUsStrength + delta, 0.0, 1.0);
        }

        // Acquisition with low US salience
        double lowUsStrength = 0.0;
        for (int i = 0; i < 5; i++)
        {
            double delta = RescorlaWagner.Reinforce(csSalience, lowUsSalience, lowUsStrength, 1.0);
            lowUsStrength = Math.Clamp(lowUsStrength + delta, 0.0, 1.0);
        }

        // Assert acquisition
        highUsStrength.Should().BeGreaterThan(lowUsStrength, "high US salience should produce faster acquisition");

        // Now test extinction
        double highUsExtinction = highUsStrength;
        for (int i = 0; i < 3; i++)
        {
            double delta = RescorlaWagner.Extinguish(csSalience, highUsSalience, highUsExtinction);
            highUsExtinction = Math.Clamp(highUsExtinction + delta, 0.0, 1.0);
        }

        double lowUsExtinction = lowUsStrength;
        for (int i = 0; i < 3; i++)
        {
            double delta = RescorlaWagner.Extinguish(csSalience, lowUsSalience, lowUsExtinction);
            lowUsExtinction = Math.Clamp(lowUsExtinction + delta, 0.0, 1.0);
        }

        // Assert extinction
        var highUsDecline = highUsStrength - highUsExtinction;
        var lowUsDecline = lowUsStrength - lowUsExtinction;
        highUsDecline.Should().BeGreaterThan(lowUsDecline, "high US salience should produce faster extinction");
    }

    /// <summary>
    /// Test that prediction error drives learning in both directions.
    /// Positive error (λ > ΣV) → positive ΔV (acquisition).
    /// Negative error (λ &lt; ΣV) → negative ΔV (overexpectation).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void PredictionError_DrivesLearningBidirectionally()
    {
        // Arrange
        double csSalience = 0.5;
        double usSalience = 0.5;

        // Positive prediction error: expected nothing, got something
        double positiveErrorDelta = RescorlaWagner.ComputeDelta(csSalience, usSalience, 1.0, 0.0);

        // Negative prediction error: expected something, got nothing
        double negativeErrorDelta = RescorlaWagner.ComputeDelta(csSalience, usSalience, 0.0, 1.0);

        // Zero prediction error: expected exactly what got
        double zeroDelta = RescorlaWagner.ComputeDelta(csSalience, usSalience, 0.5, 0.5);

        // Assert
        positiveErrorDelta.Should().BeGreaterThan(0.0, "positive prediction error should increase association");
        negativeErrorDelta.Should().BeLessThan(0.0, "negative prediction error should decrease association");
        zeroDelta.Should().Be(0.0, "zero prediction error should produce no change");
    }

    /// <summary>
    /// Test that multiple CSs compete for limited association strength.
    /// The sum of all associations approaches λ, not each individual association.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void MultipleCs_CompeteForLimitedAssociationStrength()
    {
        // Arrange - Train 3 CSs simultaneously
        double csSalience = 0.5;
        double usSalience = 0.5;
        double lambda = 1.0;
        double cs1 = 0.0;
        double cs2 = 0.0;
        double cs3 = 0.0;

        // Train all three together for 20 trials
        for (int i = 0; i < 20; i++)
        {
            double totalV = cs1 + cs2 + cs3;
            double delta1 = RescorlaWagner.Reinforce(csSalience, usSalience, totalV, lambda);
            double delta2 = RescorlaWagner.Reinforce(csSalience, usSalience, totalV, lambda);
            double delta3 = RescorlaWagner.Reinforce(csSalience, usSalience, totalV, lambda);

            cs1 = Math.Clamp(cs1 + delta1, 0.0, 1.0);
            cs2 = Math.Clamp(cs2 + delta2, 0.0, 1.0);
            cs3 = Math.Clamp(cs3 + delta3, 0.0, 1.0);
        }

        double totalAssociation = cs1 + cs2 + cs3;

        // Assert
        totalAssociation.Should().BeLessThanOrEqualTo(lambda * 1.1, "total association should approach but not greatly exceed lambda");
        cs1.Should().BeApproximately(cs2, 0.05, "CSs with equal salience should acquire similar strengths");
        cs2.Should().BeApproximately(cs3, 0.05, "CSs with equal salience should acquire similar strengths");
    }
}
