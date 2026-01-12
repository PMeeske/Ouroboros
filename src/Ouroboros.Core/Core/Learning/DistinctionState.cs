// <copyright file="DistinctionState.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Learning;

using System.Collections.Immutable;
using Ouroboros.Core.LawsOfForm;

/// <summary>
/// Represents the current state of the Distinction Learning system.
/// Tracks active distinctions, their fitness, and the dream cycle state.
/// </summary>
/// <param name="ActiveDistinctions">Set of currently active distinction strings.</param>
/// <param name="DistinctionFitness">Fitness scores for each distinction.</param>
/// <param name="StateEmbedding">Semantic embedding representing the current state.</param>
/// <param name="CurrentStage">Current stage in the dream cycle.</param>
/// <param name="CycleCount">Number of dream cycles completed.</param>
/// <param name="LastTransition">Timestamp of the last stage transition.</param>
/// <param name="EpistemicCertainty">Certainty level represented as a Form.</param>
public sealed record DistinctionState(
    ImmutableHashSet<string> ActiveDistinctions,
    ImmutableDictionary<string, double> DistinctionFitness,
    float[] StateEmbedding,
    DreamStage CurrentStage,
    int CycleCount,
    DateTime LastTransition,
    Form EpistemicCertainty)
{
    /// <summary>
    /// Creates an initial distinction state in the Void stage.
    /// </summary>
    /// <param name="embeddingSize">Size of the state embedding vector. Default: 384.</param>
    /// <returns>A new distinction state at the beginning of the cycle.</returns>
    public static DistinctionState Initial(int embeddingSize = 384) => new(
        ActiveDistinctions: ImmutableHashSet<string>.Empty,
        DistinctionFitness: ImmutableDictionary<string, double>.Empty,
        StateEmbedding: new float[embeddingSize],
        CurrentStage: DreamStage.Void,
        CycleCount: 0,
        LastTransition: DateTime.UtcNow,
        EpistemicCertainty: Form.Void);

    /// <summary>
    /// Transitions to a new dream stage.
    /// </summary>
    /// <param name="newStage">The new stage to transition to.</param>
    /// <returns>Updated distinction state with new stage.</returns>
    public DistinctionState TransitionTo(DreamStage newStage)
    {
        var incrementCycle = newStage == DreamStage.NewDream || newStage == DreamStage.Void;
        return this with
        {
            CurrentStage = newStage,
            CycleCount = incrementCycle ? this.CycleCount + 1 : this.CycleCount,
            LastTransition = DateTime.UtcNow
        };
    }
}
