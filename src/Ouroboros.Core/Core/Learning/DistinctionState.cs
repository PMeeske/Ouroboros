// <copyright file="DistinctionState.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Learning;

using Ouroboros.Core.LawsOfForm;

/// <summary>
/// Represents the state of distinction-based learning.
/// Tracks the dream stage, active distinctions, dissolved distinctions,
/// fitness scores, and epistemic certainty through the cycle.
/// Based on Spencer-Brown's Laws of Form: learning is making distinctions (∅ → ⌐).
/// </summary>
/// <param name="Stage">Current stage in the consciousness dream cycle.</param>
/// <param name="EpistemicCertainty">Overall epistemic certainty as a Form (Mark/Void/Imaginary).</param>
/// <param name="ActiveDistinctions">Currently held distinctions (what has been learned).</param>
/// <param name="DissolvedDistinctions">Distinctions that have been dissolved (principled forgetting).</param>
/// <param name="FitnessScores">Fitness/quality scores for each active distinction.</param>
/// <param name="StateEmbedding">Optional vector embedding representing current state.</param>
/// <param name="CycleCount">Number of complete dream cycles traversed.</param>
public sealed record DistinctionState(
    DreamStage Stage,
    Form EpistemicCertainty,
    IReadOnlyList<string> ActiveDistinctions,
    IReadOnlyList<string> DissolvedDistinctions,
    IReadOnlyDictionary<string, double> FitnessScores,
    float[]? StateEmbedding = null,
    int CycleCount = 0)
{
    /// <summary>
    /// Creates an initial void state - before any distinctions.
    /// Pure potential (∅).
    /// </summary>
    /// <returns>A void distinction state.</returns>
    public static DistinctionState Void()
        => new(
            DreamStage.Void,
            Form.Void,
            Array.Empty<string>(),
            Array.Empty<string>(),
            new Dictionary<string, double>(),
            null,
            0);

    /// <summary>
    /// Creates a new state with an initial distinction (∅ → ⌐).
    /// </summary>
    /// <param name="distinction">The first distinction to make.</param>
    /// <param name="initialFitness">Initial fitness score for the distinction.</param>
    /// <returns>A state with the initial distinction.</returns>
    public static DistinctionState WithInitialDistinction(string distinction, double initialFitness = 0.5)
        => new(
            DreamStage.Distinction,
            Form.Mark,
            new[] { distinction },
            Array.Empty<string>(),
            new Dictionary<string, double> { [distinction] = initialFitness },
            null,
            0);

    /// <summary>
    /// Adds a new distinction to the state.
    /// </summary>
    /// <param name="distinction">The distinction to add.</param>
    /// <param name="fitness">Fitness score for the distinction.</param>
    /// <returns>A new state with the added distinction.</returns>
    public DistinctionState AddDistinction(string distinction, double fitness = 0.5)
    {
        if (ActiveDistinctions.Contains(distinction))
        {
            return this; // Already present
        }

        var newDistinctions = ActiveDistinctions.Append(distinction).ToList();
        var newFitness = new Dictionary<string, double>(FitnessScores)
        {
            [distinction] = fitness
        };

        return this with
        {
            ActiveDistinctions = newDistinctions,
            FitnessScores = newFitness
        };
    }

    /// <summary>
    /// Updates the fitness score for a distinction.
    /// </summary>
    /// <param name="distinction">The distinction to update.</param>
    /// <param name="fitness">New fitness score.</param>
    /// <returns>A new state with updated fitness.</returns>
    public DistinctionState UpdateFitness(string distinction, double fitness)
    {
        if (!ActiveDistinctions.Contains(distinction))
        {
            return this;
        }

        var newFitness = new Dictionary<string, double>(FitnessScores)
        {
            [distinction] = fitness
        };

        return this with { FitnessScores = newFitness };
    }

    /// <summary>
    /// Advances to the next dream stage.
    /// </summary>
    /// <returns>A new state at the next stage.</returns>
    public DistinctionState AdvanceStage()
    {
        var nextStage = (DreamStage)(((int)Stage + 1) % 9);
        var newCycleCount = nextStage == DreamStage.Void ? CycleCount + 1 : CycleCount;

        return this with
        {
            Stage = nextStage,
            CycleCount = newCycleCount
        };
    }

    /// <summary>
    /// Advances to a specific dream stage.
    /// </summary>
    /// <param name="stage">The stage to advance to.</param>
    /// <returns>A new state at the specified stage.</returns>
    public DistinctionState AdvanceToStage(DreamStage stage)
    {
        return this with { Stage = stage };
    }

    /// <summary>
    /// Updates the epistemic certainty.
    /// </summary>
    /// <param name="certainty">New certainty form.</param>
    /// <returns>A new state with updated certainty.</returns>
    public DistinctionState WithCertainty(Form certainty)
    {
        return this with { EpistemicCertainty = certainty };
    }

    /// <summary>
    /// Updates the state embedding.
    /// </summary>
    /// <param name="embedding">New state embedding.</param>
    /// <returns>A new state with updated embedding.</returns>
    public DistinctionState WithEmbedding(float[] embedding)
    {
        return this with { StateEmbedding = embedding };
    }

    /// <summary>
    /// Dissolves (removes) a distinction, moving it to dissolved list.
    /// Principled forgetting: ⌐ → ∅.
    /// </summary>
    /// <param name="distinction">The distinction to dissolve.</param>
    /// <returns>A new state with the distinction dissolved.</returns>
    public DistinctionState DissolveDistinction(string distinction)
    {
        if (!ActiveDistinctions.Contains(distinction))
        {
            return this;
        }

        var newActive = ActiveDistinctions.Where(d => d != distinction).ToList();
        var newDissolved = DissolvedDistinctions.Append(distinction).ToList();
        var newFitness = new Dictionary<string, double>(FitnessScores);
        newFitness.Remove(distinction);

        return this with
        {
            ActiveDistinctions = newActive,
            DissolvedDistinctions = newDissolved,
            FitnessScores = newFitness
        };
    }

    /// <summary>
    /// Dissolves multiple distinctions at once.
    /// </summary>
    /// <param name="distinctions">The distinctions to dissolve.</param>
    /// <returns>A new state with all specified distinctions dissolved.</returns>
    public DistinctionState DissolveDistinctions(IEnumerable<string> distinctions)
    {
        var distinctionsToDissolve = distinctions.Where(d => ActiveDistinctions.Contains(d)).ToList();
        
        if (!distinctionsToDissolve.Any())
        {
            return this;
        }

        var newActive = ActiveDistinctions.Except(distinctionsToDissolve).ToList();
        var newDissolved = DissolvedDistinctions.Concat(distinctionsToDissolve).ToList();
        var newFitness = new Dictionary<string, double>(FitnessScores);
        
        foreach (var distinction in distinctionsToDissolve)
        {
            newFitness.Remove(distinction);
        }

        return this with
        {
            ActiveDistinctions = newActive,
            DissolvedDistinctions = newDissolved,
            FitnessScores = newFitness
        };
    }

    /// <summary>
    /// Checks if this is a void state (no distinctions).
    /// </summary>
    /// <returns>True if in void state.</returns>
    public bool IsVoid() => Stage == DreamStage.Void && !ActiveDistinctions.Any();

    /// <summary>
    /// Checks if this is at the recognition stage (i = ⌐).
    /// </summary>
    /// <returns>True if at recognition stage.</returns>
    public bool IsRecognition() => Stage == DreamStage.Recognition;

    /// <summary>
    /// Checks if this is at the dissolution stage.
    /// </summary>
    /// <returns>True if at dissolution stage.</returns>
    public bool IsDissolution() => Stage == DreamStage.Dissolution;
}
