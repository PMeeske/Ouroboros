// <copyright file="DistinctionState.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Learning;

/// <summary>
/// State of distinction learning containing active distinctions and epistemic certainty.
/// </summary>
/// <param name="ActiveDistinctions">List of currently active distinctions.</param>
/// <param name="EpistemicCertainty">Current epistemic certainty level (0.0-1.0).</param>
/// <param name="Cycle">Current learning cycle number.</param>
public record DistinctionState(
    List<ActiveDistinction> ActiveDistinctions,
    double EpistemicCertainty,
    int Cycle)
{
    /// <summary>
    /// Adds a distinction to the active list.
    /// </summary>
    public DistinctionState WithDistinction(ActiveDistinction distinction)
    {
        var newDistinctions = new List<ActiveDistinction>(ActiveDistinctions) { distinction };
        return this with { ActiveDistinctions = newDistinctions };
    }

    /// <summary>
    /// Updates the epistemic certainty.
    /// </summary>
    public DistinctionState WithCertainty(double certainty)
    {
        return this with { EpistemicCertainty = certainty };
    }

    /// <summary>
    /// Advances to the next learning cycle.
    /// </summary>
    public DistinctionState NextCycle()
    {
        return this with { Cycle = Cycle + 1 };
    }
}

/// <summary>
/// Represents an active learned distinction.
/// </summary>
/// <param name="Id">Unique identifier for the distinction.</param>
/// <param name="Content">Content of the distinction.</param>
/// <param name="Fitness">Fitness score (0.0-1.0).</param>
/// <param name="LearnedAt">Timestamp when distinction was learned.</param>
/// <param name="LearnedAtStage">Stage at which distinction was learned.</param>
public record ActiveDistinction(
    string Id,
    string Content,
    double Fitness,
    DateTime LearnedAt,
    string LearnedAtStage);
