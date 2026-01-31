// <copyright file="EthicsTypes.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Ethics;

/// <summary>
/// Minimal representation of a Goal for ethics evaluation.
/// This is a lightweight version to avoid circular dependencies.
/// </summary>
public sealed record Goal
{
    /// <summary>
    /// Gets the unique identifier for this goal.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the description of what this goal aims to achieve.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the type of goal.
    /// </summary>
    public required string Type { get; init; }

    /// <summary>
    /// Gets the priority of this goal (0.0 to 1.0).
    /// </summary>
    public required double Priority { get; init; }
}

/// <summary>
/// Minimal representation of a Plan for ethics evaluation.
/// This is a lightweight version to avoid circular dependencies.
/// </summary>
public sealed record Plan
{
    /// <summary>
    /// Gets the goal this plan aims to achieve.
    /// </summary>
    public required string Goal { get; init; }

    /// <summary>
    /// Gets the steps in this plan.
    /// </summary>
    public required IReadOnlyList<PlanStep> Steps { get; init; }

    /// <summary>
    /// Gets the confidence scores for various aspects of the plan.
    /// </summary>
    public IReadOnlyDictionary<string, double> ConfidenceScores { get; init; } =
        new Dictionary<string, double>();

    /// <summary>
    /// Gets when this plan was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Minimal representation of a plan step for ethics evaluation.
/// </summary>
public sealed record PlanStep
{
    /// <summary>
    /// Gets the action to be performed.
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// Gets the parameters for this action.
    /// </summary>
    public required IReadOnlyDictionary<string, object> Parameters { get; init; }

    /// <summary>
    /// Gets the expected outcome of this step.
    /// </summary>
    public required string ExpectedOutcome { get; init; }

    /// <summary>
    /// Gets the confidence score for this step (0.0 to 1.0).
    /// </summary>
    public double ConfidenceScore { get; init; } = 1.0;
}

/// <summary>
/// Minimal representation of a Skill for ethics evaluation.
/// This is a lightweight version to avoid circular dependencies.
/// </summary>
public sealed record Skill
{
    /// <summary>
    /// Gets the name of the skill.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the description of what this skill does.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the prerequisites for using this skill.
    /// </summary>
    public IReadOnlyList<string> Prerequisites { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the steps involved in executing this skill.
    /// </summary>
    public IReadOnlyList<PlanStep> Steps { get; init; } = Array.Empty<PlanStep>();

    /// <summary>
    /// Gets the historical success rate of this skill (0.0 to 1.0).
    /// </summary>
    public double SuccessRate { get; init; } = 0.0;

    /// <summary>
    /// Gets the number of times this skill has been used.
    /// </summary>
    public int UsageCount { get; init; } = 0;

    /// <summary>
    /// Gets when this skill was created.
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Gets when this skill was last used.
    /// </summary>
    public DateTime LastUsed { get; init; } = DateTime.UtcNow;
}
