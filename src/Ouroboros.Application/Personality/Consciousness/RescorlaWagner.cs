// <copyright file="RescorlaWagner.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Personality.Consciousness;

/// <summary>
/// Implements the Rescorla-Wagner model of associative learning (1972).
/// ΔV = αβ(λ - ΣV)
///
/// References:
/// - Rescorla, R.A. &amp; Wagner, A.R. (1972). A theory of Pavlovian conditioning:
///   Variations in the effectiveness of reinforcement and nonreinforcement.
///   In A.H. Black &amp; W.F. Prokasy (Eds.), Classical Conditioning II: Current Research and Theory (pp. 64-99).
///   New York: Appleton-Century-Crofts.
/// </summary>
public static class RescorlaWagner
{
    /// <summary>
    /// Computes the change in association strength for a single CS-US pairing.
    /// </summary>
    /// <param name="csSalience">α — salience of the conditioned stimulus (0-1).
    /// Higher values mean the CS is more noticeable.</param>
    /// <param name="usSalience">β — salience of the unconditioned stimulus (0-1).
    /// Higher values mean the US is more impactful.</param>
    /// <param name="maxConditioning">λ — maximum association strength supportable
    /// by this US. Typically 1.0 for US-present trials, 0.0 for extinction trials.</param>
    /// <param name="totalAssociationStrength">ΣV — sum of association strengths
    /// of ALL conditioned stimuli present on this trial.</param>
    /// <returns>ΔV — the change to apply to this CS's association strength.</returns>
    public static double ComputeDelta(
        double csSalience,
        double usSalience,
        double maxConditioning,
        double totalAssociationStrength)
    {
        return csSalience * usSalience * (maxConditioning - totalAssociationStrength);
    }

    /// <summary>
    /// Computes the change for a reinforcement trial (US present).
    /// λ = 1.0 (or configurable max).
    /// </summary>
    /// <param name="csSalience">α — salience of the conditioned stimulus (0-1).</param>
    /// <param name="usSalience">β — salience of the unconditioned stimulus (0-1).</param>
    /// <param name="totalAssociationStrength">ΣV — sum of association strengths
    /// of ALL conditioned stimuli present on this trial.</param>
    /// <param name="lambda">λ — maximum association strength (default: 1.0).</param>
    /// <returns>ΔV — the change to apply to this CS's association strength.</returns>
    public static double Reinforce(
        double csSalience,
        double usSalience,
        double totalAssociationStrength,
        double lambda = 1.0)
    {
        return ComputeDelta(csSalience, usSalience, lambda, totalAssociationStrength);
    }

    /// <summary>
    /// Computes the change for an extinction trial (US absent).
    /// λ = 0.0 — drives association toward zero.
    /// </summary>
    /// <param name="csSalience">α — salience of the conditioned stimulus (0-1).</param>
    /// <param name="usSalience">β — salience of the unconditioned stimulus (0-1).</param>
    /// <param name="totalAssociationStrength">ΣV — sum of association strengths
    /// of ALL conditioned stimuli present on this trial.</param>
    /// <returns>ΔV — the change to apply to this CS's association strength (typically negative).</returns>
    public static double Extinguish(
        double csSalience,
        double usSalience,
        double totalAssociationStrength)
    {
        return ComputeDelta(csSalience, usSalience, 0.0, totalAssociationStrength);
    }
}
