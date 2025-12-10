// <copyright file="Episode.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Domain.Environment;

/// <summary>
/// Represents a complete episode of environment interaction.
/// Contains all steps from initialization to termination.
/// </summary>
/// <param name="Id">Unique identifier for this episode</param>
/// <param name="EnvironmentName">Name of the environment</param>
/// <param name="Steps">The sequence of steps in this episode</param>
/// <param name="TotalReward">Cumulative reward for the episode</param>
/// <param name="StartTime">When the episode started</param>
/// <param name="EndTime">When the episode ended</param>
/// <param name="Success">Whether the episode was successful</param>
/// <param name="Metadata">Additional metadata about the episode</param>
public sealed record Episode(
    Guid Id,
    string EnvironmentName,
    IReadOnlyList<EnvironmentStep> Steps,
    double TotalReward,
    DateTime StartTime,
    DateTime? EndTime = null,
    bool Success = false,
    IReadOnlyDictionary<string, object>? Metadata = null)
{
    /// <summary>
    /// Gets the duration of the episode.
    /// </summary>
    public TimeSpan? Duration => EndTime.HasValue ? EndTime.Value - StartTime : null;

    /// <summary>
    /// Gets the number of steps in the episode.
    /// </summary>
    public int StepCount => Steps.Count;

    /// <summary>
    /// Gets whether the episode is complete (has ended).
    /// </summary>
    public bool IsComplete => EndTime.HasValue;
}
