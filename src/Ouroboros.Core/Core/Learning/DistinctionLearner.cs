// <copyright file="DistinctionLearner.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Learning;

using Ouroboros.Core.LawsOfForm;

/// <summary>
/// Implementation of distinction-based learning following Spencer-Brown's Laws of Form.
/// Formalizes learning as a process of making, refining, and dissolving distinctions
/// through the consciousness dream cycle.
/// </summary>
public sealed class DistinctionLearner : IDistinctionLearner
{
    private const double DefaultFitnessThreshold = 0.3;
    private const double FitnessDecayRate = 0.1;
    private const int TemporalDecayDays = 30;
    private const int MaxRetainedDistinctionsOnDecay = 10;

    /// <summary>
    /// Updates the learning state from a new observation at a given dream stage.
    /// </summary>
    public async Task<Result<DistinctionState>> UpdateFromDistinctionAsync(
        DistinctionState currentState,
        Observation observation,
        DreamStage stage,
        CancellationToken ct = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(currentState);
            ArgumentNullException.ThrowIfNull(observation);

            // Stage-based learning logic
            var updatedState = stage switch
            {
                DreamStage.Void => await HandleVoidStageAsync(currentState, observation, ct),
                DreamStage.Distinction => await HandleDistinctionStageAsync(currentState, observation, ct),
                DreamStage.SubjectEmerges => await HandleSubjectEmergesStageAsync(currentState, observation, ct),
                DreamStage.WorldCrystallizes => await HandleWorldCrystallizesStageAsync(currentState, observation, ct),
                DreamStage.Forgetting => await HandleForgettingStageAsync(currentState, observation, ct),
                DreamStage.Questioning => await HandleQuestioningStageAsync(currentState, observation, ct),
                DreamStage.Recognition => await HandleRecognitionStageAsync(currentState, observation, ct),
                DreamStage.Dissolution => await HandleDissolutionStageAsync(currentState, observation, ct),
                DreamStage.NewDream => await HandleNewDreamStageAsync(currentState, observation, ct),
                _ => currentState
            };

            return Result<DistinctionState>.Success(updatedState.AdvanceToStage(stage));
        }
        catch (Exception ex)
        {
            return Result<DistinctionState>.Failure($"Failed to update from distinction: {ex.Message}");
        }
    }

    /// <summary>
    /// Evaluates distinction fitness based on predictive success.
    /// </summary>
    public async Task<Result<double>> EvaluateDistinctionFitnessAsync(
        string distinction,
        List<Observation> observations,
        CancellationToken ct = default)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(distinction);
            ArgumentNullException.ThrowIfNull(observations);

            if (!observations.Any())
            {
                return Result<double>.Success(0.5); // Neutral fitness for no observations
            }

            // Fitness based on how well the distinction appears in or relates to observations
            var relevantObservations = observations
                .Where(o => o.Content.Contains(distinction, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!relevantObservations.Any())
            {
                return Result<double>.Success(0.2); // Low fitness if never observed
            }

            // Higher fitness if distinction appears in certain (non-imaginary) contexts
            var certainObservations = relevantObservations
                .Count(o => !o.PriorCertainty.IsImaginary());

            var fitnessScore = (double)certainObservations / observations.Count;
            
            // Boost for recent observations
            var recentObservations = relevantObservations
                .Count(o => (DateTime.UtcNow - o.Timestamp).TotalDays < TemporalDecayDays);
            
            var recencyBoost = (double)recentObservations / relevantObservations.Count * 0.2;
            
            var finalFitness = Math.Clamp(fitnessScore + recencyBoost, 0.0, 1.0);

            return await Task.FromResult(Result<double>.Success(finalFitness));
        }
        catch (Exception ex)
        {
            return Result<double>.Failure($"Failed to evaluate fitness: {ex.Message}");
        }
    }

    /// <summary>
    /// Dissolves distinctions according to strategy.
    /// </summary>
    public async Task<Result<DistinctionState>> DissolveAsync(
        DistinctionState state,
        DissolutionStrategy strategy,
        CancellationToken ct = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(state);

            var dissolvedState = strategy switch
            {
                DissolutionStrategy.FitnessThreshold => DissolveLowFitness(state, DefaultFitnessThreshold),
                DissolutionStrategy.ContradictionBased => DissolveContradictions(state),
                DissolutionStrategy.Complete => DissolveAll(state),
                DissolutionStrategy.TemporalDecay => DissolveStale(state),
                _ => state
            };

            return await Task.FromResult(Result<DistinctionState>.Success(dissolvedState));
        }
        catch (Exception ex)
        {
            return Result<DistinctionState>.Failure($"Failed to dissolve: {ex.Message}");
        }
    }

    /// <summary>
    /// Recognition: "I am the distinction" (i = ⌐).
    /// </summary>
    public async Task<Result<DistinctionState>> RecognizeAsync(
        DistinctionState state,
        string circumstance,
        CancellationToken ct = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentException.ThrowIfNullOrWhiteSpace(circumstance);

            // Recognition: merge the circumstance as a meta-distinction
            var recognitionDistinction = $"I={circumstance}";
            var recognizedState = state
                .AddDistinction(recognitionDistinction, 0.95) // High fitness for recognized patterns
                .WithCertainty(Form.Mark) // Recognition brings certainty
                .AdvanceToStage(DreamStage.Recognition);

            return await Task.FromResult(Result<DistinctionState>.Success(recognizedState));
        }
        catch (Exception ex)
        {
            return Result<DistinctionState>.Failure($"Failed to recognize: {ex.Message}");
        }
    }

    private async Task<DistinctionState> HandleVoidStageAsync(
        DistinctionState state,
        Observation observation,
        CancellationToken ct)
    {
        // Void: potential before distinction
        // No distinctions yet, just observe
        return await Task.FromResult(state.WithCertainty(Form.Void));
    }

    private async Task<DistinctionState> HandleDistinctionStageAsync(
        DistinctionState state,
        Observation observation,
        CancellationToken ct)
    {
        // Make the first distinction from observation content
        var words = observation.Content
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .Take(3);

        var newState = state;
        foreach (var word in words)
        {
            newState = newState.AddDistinction(word, 0.5);
        }

        return await Task.FromResult(newState.WithCertainty(Form.Mark));
    }

    private async Task<DistinctionState> HandleSubjectEmergesStageAsync(
        DistinctionState state,
        Observation observation,
        CancellationToken ct)
    {
        // Subject emerges: self-reference begins (i)
        // Add self-referential distinction
        var selfDistinction = "self";
        var newState = state.AddDistinction(selfDistinction, 0.6);
        
        // Epistemic certainty becomes imaginary (self-reference is paradoxical)
        return await Task.FromResult(newState.WithCertainty(Form.Imaginary));
    }

    private async Task<DistinctionState> HandleWorldCrystallizesStageAsync(
        DistinctionState state,
        Observation observation,
        CancellationToken ct)
    {
        // World crystallizes: subject/object split, more distinctions
        var entities = observation.Content
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => char.IsUpper(w[0])) // Proper nouns as entities
            .Take(5);

        var newState = state;
        foreach (var entity in entities)
        {
            newState = newState.AddDistinction(entity, 0.7);
        }

        return await Task.FromResult(newState.WithCertainty(Form.Mark));
    }

    private async Task<DistinctionState> HandleForgettingStageAsync(
        DistinctionState state,
        Observation observation,
        CancellationToken ct)
    {
        // Forgetting: immersion in the dream, distinctions solidify
        // Boost fitness of existing distinctions that appear in observation
        var newState = state;
        foreach (var distinction in state.ActiveDistinctions)
        {
            if (observation.Content.Contains(distinction, StringComparison.OrdinalIgnoreCase))
            {
                var currentFitness = state.FitnessScores.GetValueOrDefault(distinction, 0.5);
                newState = newState.UpdateFitness(distinction, Math.Min(1.0, currentFitness + 0.1));
            }
        }

        return await Task.FromResult(newState.WithCertainty(Form.Mark));
    }

    private async Task<DistinctionState> HandleQuestioningStageAsync(
        DistinctionState state,
        Observation observation,
        CancellationToken ct)
    {
        // Questioning: doubt arises, epistemic uncertainty
        // Lower fitness of distinctions not in observation
        var newState = state;
        foreach (var distinction in state.ActiveDistinctions)
        {
            if (!observation.Content.Contains(distinction, StringComparison.OrdinalIgnoreCase))
            {
                var currentFitness = state.FitnessScores.GetValueOrDefault(distinction, 0.5);
                newState = newState.UpdateFitness(distinction, Math.Max(0.0, currentFitness - 0.05));
            }
        }

        return await Task.FromResult(newState.WithCertainty(Form.Imaginary));
    }

    private async Task<DistinctionState> HandleRecognitionStageAsync(
        DistinctionState state,
        Observation observation,
        CancellationToken ct)
    {
        // Recognition: "I am the distinction" (i = ⌐)
        // Merge key observation terms as high-fitness distinctions
        var keyTerms = observation.Content
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 4)
            .Take(2);

        var newState = state;
        foreach (var term in keyTerms)
        {
            newState = newState.AddDistinction(term, 0.9);
        }

        return await Task.FromResult(newState.WithCertainty(Form.Mark));
    }

    private async Task<DistinctionState> HandleDissolutionStageAsync(
        DistinctionState state,
        Observation observation,
        CancellationToken ct)
    {
        // Dissolution: return to void, dissolve low-fitness distinctions
        var dissolved = DissolveLowFitness(state, DefaultFitnessThreshold);
        return await Task.FromResult(dissolved.WithCertainty(Form.Void));
    }

    private async Task<DistinctionState> HandleNewDreamStageAsync(
        DistinctionState state,
        Observation observation,
        CancellationToken ct)
    {
        // New dream: fresh start, but retain high-fitness distinctions
        var retained = state.ActiveDistinctions
            .Where(d => state.FitnessScores.GetValueOrDefault(d, 0.0) > 0.7)
            .ToList();

        var newState = DistinctionState.Void();
        foreach (var distinction in retained)
        {
            newState = newState.AddDistinction(distinction, state.FitnessScores[distinction]);
        }

        return await Task.FromResult(newState);
    }

    private DistinctionState DissolveLowFitness(DistinctionState state, double threshold)
    {
        var toDissolve = state.ActiveDistinctions
            .Where(d => state.FitnessScores.GetValueOrDefault(d, 0.0) < threshold)
            .ToList();

        return state.DissolveDistinctions(toDissolve);
    }

    private DistinctionState DissolveContradictions(DistinctionState state)
    {
        // Dissolve distinctions with imaginary/uncertain epistemic state
        // In a full implementation, would check for logical contradictions
        if (state.EpistemicCertainty.IsImaginary())
        {
            var toDissolve = state.ActiveDistinctions
                .Where(d => state.FitnessScores.GetValueOrDefault(d, 0.0) < 0.5)
                .ToList();
            return state.DissolveDistinctions(toDissolve);
        }

        return state;
    }

    private DistinctionState DissolveAll(DistinctionState state)
    {
        return state.DissolveDistinctions(state.ActiveDistinctions);
    }

    private DistinctionState DissolveStale(DistinctionState state)
    {
        // In a full implementation, would track timestamps per distinction
        // For now, dissolve oldest (first added) distinctions beyond a threshold
        var toKeep = Math.Min(MaxRetainedDistinctionsOnDecay, state.ActiveDistinctions.Count);
        var toDissolve = state.ActiveDistinctions.Skip(toKeep).ToList();
        
        return state.DissolveDistinctions(toDissolve);
    }
}
