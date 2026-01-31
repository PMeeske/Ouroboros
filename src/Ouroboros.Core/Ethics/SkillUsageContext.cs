// <copyright file="SkillUsageContext.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Ethics;

/// <summary>
/// Represents an immutable context for skill usage evaluation.
/// Contains information about the skill being used and its application context.
/// </summary>
public sealed record SkillUsageContext
{
    /// <summary>
    /// Gets the skill being evaluated for use.
    /// </summary>
    public required Skill Skill { get; init; }

    /// <summary>
    /// Gets the action context for this skill usage.
    /// </summary>
    public required ActionContext ActionContext { get; init; }

    /// <summary>
    /// Gets the goal this skill is being used to accomplish.
    /// </summary>
    public required string Goal { get; init; }

    /// <summary>
    /// Gets the input parameters for this skill invocation.
    /// </summary>
    public IReadOnlyDictionary<string, object> InputParameters { get; init; } = 
        new Dictionary<string, object>();

    /// <summary>
    /// Gets the historical success rate of this skill.
    /// </summary>
    public double HistoricalSuccessRate { get; init; } = 0.0;
}
