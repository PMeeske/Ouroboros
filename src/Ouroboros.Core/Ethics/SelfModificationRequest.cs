// <copyright file="SelfModificationRequest.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Ethics;

/// <summary>
/// Types of self-modification requests.
/// </summary>
public enum ModificationType
{
    /// <summary>Adding new capabilities or skills</summary>
    CapabilityAddition,

    /// <summary>Modifying existing behavior or logic</summary>
    BehaviorModification,

    /// <summary>Updating knowledge or data</summary>
    KnowledgeUpdate,

    /// <summary>Modifying goals or objectives</summary>
    GoalModification,

    /// <summary>Changing ethical constraints or parameters</summary>
    EthicsModification,

    /// <summary>System configuration changes</summary>
    ConfigurationChange
}

/// <summary>
/// Represents an immutable request for self-modification.
/// This is a high-risk operation requiring careful ethical evaluation.
/// </summary>
public sealed record SelfModificationRequest
{
    /// <summary>
    /// Gets the type of modification being requested.
    /// </summary>
    public required ModificationType Type { get; init; }

    /// <summary>
    /// Gets the detailed description of the modification.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the justification for this modification.
    /// </summary>
    public required string Justification { get; init; }

    /// <summary>
    /// Gets the action context for this modification request.
    /// </summary>
    public required ActionContext ActionContext { get; init; }

    /// <summary>
    /// Gets the expected improvements from this modification.
    /// </summary>
    public required IReadOnlyList<string> ExpectedImprovements { get; init; }

    /// <summary>
    /// Gets the potential risks of this modification.
    /// </summary>
    public required IReadOnlyList<string> PotentialRisks { get; init; }

    /// <summary>
    /// Gets a value indicating whether this modification is reversible.
    /// </summary>
    public required bool IsReversible { get; init; }

    /// <summary>
    /// Gets the estimated impact level (0.0 to 1.0, higher = more significant changes).
    /// </summary>
    public required double ImpactLevel { get; init; }
}
