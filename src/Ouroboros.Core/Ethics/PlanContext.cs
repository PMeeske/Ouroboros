// <copyright file="PlanContext.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Ethics;

/// <summary>
/// Represents an immutable context for plan evaluation.
/// Contains information about the plan, its goal, and execution context.
/// </summary>
public sealed record PlanContext
{
    /// <summary>
    /// Gets the plan being evaluated.
    /// </summary>
    public required Plan Plan { get; init; }

    /// <summary>
    /// Gets the action context for this plan.
    /// </summary>
    public required ActionContext ActionContext { get; init; }

    /// <summary>
    /// Gets the estimated risk level of executing this plan (0.0 to 1.0).
    /// </summary>
    public double EstimatedRisk { get; init; } = 0.5;

    /// <summary>
    /// Gets the expected benefits of executing this plan.
    /// </summary>
    public IReadOnlyList<string> ExpectedBenefits { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the potential negative consequences of this plan.
    /// </summary>
    public IReadOnlyList<string> PotentialConsequences { get; init; } = Array.Empty<string>();
}
