// <copyright file="ProposedAction.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Ethics;

/// <summary>
/// Represents an immutable proposed action to be evaluated ethically.
/// </summary>
public sealed record ProposedAction
{
    /// <summary>
    /// Gets the type or category of action (e.g., "file_operation", "network_request", "self_modification").
    /// </summary>
    public required string ActionType { get; init; }

    /// <summary>
    /// Gets the detailed description of what the action does.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the parameters or configuration for this action.
    /// </summary>
    public required IReadOnlyDictionary<string, object> Parameters { get; init; }

    /// <summary>
    /// Gets the target entity or resource this action affects (e.g., file path, user ID, system component).
    /// </summary>
    public string? TargetEntity { get; init; }

    /// <summary>
    /// Gets the potential effects or side effects of this action.
    /// </summary>
    public required IReadOnlyList<string> PotentialEffects { get; init; }

    /// <summary>
    /// Gets additional metadata about this action.
    /// </summary>
    public IReadOnlyDictionary<string, object> Metadata { get; init; } = 
        new Dictionary<string, object>();
}
