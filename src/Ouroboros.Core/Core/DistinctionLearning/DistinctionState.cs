// <copyright file="DistinctionState.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.DistinctionLearning;

/// <summary>
/// Represents the state of distinction learning at a point in time.
/// Tracks active distinctions, their fitness, and learning progress.
/// </summary>
public sealed record DistinctionState(
    List<ActiveDistinction> ActiveDistinctions,
    double EpistemicCertainty,
    int CycleCount,
    DateTime LastUpdated)
{
    /// <summary>
    /// Creates an initial empty distinction state.
    /// </summary>
    public static DistinctionState Initial() => new(
        ActiveDistinctions: new List<ActiveDistinction>(),
        EpistemicCertainty: 0.0,
        CycleCount: 0,
        LastUpdated: DateTime.UtcNow);

    /// <summary>
    /// Creates a new state with an additional distinction.
    /// </summary>
    public DistinctionState WithDistinction(ActiveDistinction distinction)
    {
        var newDistinctions = new List<ActiveDistinction>(ActiveDistinctions) { distinction };
        return this with
        {
            ActiveDistinctions = newDistinctions,
            LastUpdated = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a new state with updated epistemic certainty.
    /// </summary>
    public DistinctionState WithCertainty(double certainty)
    {
        return this with
        {
            EpistemicCertainty = Math.Clamp(certainty, 0.0, 1.0),
            LastUpdated = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a new state with incremented cycle count.
    /// </summary>
    public DistinctionState NextCycle()
    {
        return this with
        {
            CycleCount = CycleCount + 1,
            LastUpdated = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Represents an active distinction being tracked.
/// </summary>
public sealed record ActiveDistinction(
    string Id,
    string Content,
    double Fitness,
    DateTime LearnedAt,
    string LearnedAtStage);
