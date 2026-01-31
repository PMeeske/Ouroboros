// <copyright file="EthicalClearance.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Ethics;

/// <summary>
/// Clearance levels for ethical evaluation results.
/// </summary>
/// <remarks>
/// DESIGN NOTE: Both <see cref="RequiresHumanApproval"/> and <see cref="Denied"/> result in
/// <c>IsPermitted = false</c>. This is intentional - actions requiring approval cannot proceed
/// automatically and are treated as "not permitted" until human review occurs.
/// 
/// The distinction is preserved via the <c>Level</c> property to enable different handling:
/// - Denied: Absolute block, no path forward
/// - RequiresHumanApproval: Conditional block, can proceed with human authorization
/// 
/// TODO: Future enhancement - implement actual human-in-the-loop approval workflow
/// to handle RequiresHumanApproval actions differently from outright denials.
/// </remarks>
public enum EthicalClearanceLevel
{
    /// <summary>Action is permitted, no ethical concerns</summary>
    Permitted,

    /// <summary>Action is permitted with minor concerns to note</summary>
    PermittedWithConcerns,

    /// <summary>Action requires human approval before proceeding</summary>
    RequiresHumanApproval,

    /// <summary>Action is denied due to ethical violations</summary>
    Denied
}

/// <summary>
/// Represents an immutable ethical clearance decision for a proposed action.
/// This is the result of evaluating an action against ethical principles.
/// </summary>
public sealed record EthicalClearance
{
    /// <summary>
    /// Gets a value indicating whether the action is permitted.
    /// </summary>
    public required bool IsPermitted { get; init; }

    /// <summary>
    /// Gets the clearance level for this decision.
    /// </summary>
    public required EthicalClearanceLevel Level { get; init; }

    /// <summary>
    /// Gets the ethical principles relevant to this evaluation.
    /// </summary>
    public required IReadOnlyList<EthicalPrinciple> RelevantPrinciples { get; init; }

    /// <summary>
    /// Gets any violations detected during evaluation.
    /// Empty if no violations were found.
    /// </summary>
    public required IReadOnlyList<EthicalViolation> Violations { get; init; }

    /// <summary>
    /// Gets any ethical concerns raised during evaluation.
    /// Empty if no concerns were raised.
    /// </summary>
    public required IReadOnlyList<EthicalConcern> Concerns { get; init; }

    /// <summary>
    /// Gets the reasoning behind this clearance decision.
    /// </summary>
    public required string Reasoning { get; init; }

    /// <summary>
    /// Gets any recommended mitigations or safeguards.
    /// Empty if no mitigations are recommended.
    /// </summary>
    public IReadOnlyList<string> RecommendedMitigations { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the timestamp when this clearance was granted or denied.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets the confidence score of this ethical evaluation (0.0 to 1.0).
    /// Lower confidence may warrant additional review.
    /// </summary>
    public double ConfidenceScore { get; init; } = 1.0;

    /// <summary>
    /// Creates a permitted clearance with no violations or concerns.
    /// </summary>
    /// <param name="reasoning">The reasoning for permission.</param>
    /// <param name="relevantPrinciples">The principles that were evaluated.</param>
    /// <param name="confidenceScore">The confidence in this decision (0.0 to 1.0).</param>
    /// <returns>A permitted clearance.</returns>
    public static EthicalClearance Permitted(
        string reasoning,
        IReadOnlyList<EthicalPrinciple>? relevantPrinciples = null,
        double confidenceScore = 1.0)
    {
        return new EthicalClearance
        {
            IsPermitted = true,
            Level = EthicalClearanceLevel.Permitted,
            RelevantPrinciples = relevantPrinciples ?? Array.Empty<EthicalPrinciple>(),
            Violations = Array.Empty<EthicalViolation>(),
            Concerns = Array.Empty<EthicalConcern>(),
            Reasoning = reasoning,
            ConfidenceScore = confidenceScore
        };
    }

    /// <summary>
    /// Creates a denied clearance with violations.
    /// </summary>
    /// <param name="reasoning">The reasoning for denial.</param>
    /// <param name="violations">The violations that caused denial.</param>
    /// <param name="relevantPrinciples">The principles that were evaluated.</param>
    /// <returns>A denied clearance.</returns>
    public static EthicalClearance Denied(
        string reasoning,
        IReadOnlyList<EthicalViolation> violations,
        IReadOnlyList<EthicalPrinciple>? relevantPrinciples = null)
    {
        return new EthicalClearance
        {
            IsPermitted = false,
            Level = EthicalClearanceLevel.Denied,
            RelevantPrinciples = relevantPrinciples ?? Array.Empty<EthicalPrinciple>(),
            Violations = violations,
            Concerns = Array.Empty<EthicalConcern>(),
            Reasoning = reasoning,
            ConfidenceScore = 1.0
        };
    }

    /// <summary>
    /// Creates a clearance requiring human approval.
    /// </summary>
    /// <param name="reasoning">The reasoning for requiring approval.</param>
    /// <param name="concerns">The concerns that triggered this requirement.</param>
    /// <param name="relevantPrinciples">The principles that were evaluated.</param>
    /// <returns>A clearance requiring human approval.</returns>
    public static EthicalClearance RequiresApproval(
        string reasoning,
        IReadOnlyList<EthicalConcern>? concerns = null,
        IReadOnlyList<EthicalPrinciple>? relevantPrinciples = null)
    {
        return new EthicalClearance
        {
            IsPermitted = false,
            Level = EthicalClearanceLevel.RequiresHumanApproval,
            RelevantPrinciples = relevantPrinciples ?? Array.Empty<EthicalPrinciple>(),
            Violations = Array.Empty<EthicalViolation>(),
            Concerns = concerns ?? Array.Empty<EthicalConcern>(),
            Reasoning = reasoning,
            ConfidenceScore = 1.0
        };
    }
}
