// <copyright file="DistinctionLearner.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.DistinctionLearning;

using Microsoft.Extensions.Logging;
using Ouroboros.Core.DistinctionLearning;
using Ouroboros.Core.Learning;
using Ouroboros.Core.Monads;

/// <summary>
/// Implementation of distinction learning from consciousness dream cycles.
/// </summary>
public sealed class DistinctionLearner : IDistinctionLearner
{
    private readonly IDistinctionWeightStorage _storage;
    private readonly ILogger<DistinctionLearner>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistinctionLearner"/> class.
    /// </summary>
    public DistinctionLearner(
        IDistinctionWeightStorage storage,
        ILogger<DistinctionLearner>? logger = null)
    {
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<Result<DistinctionState, string>> UpdateFromDistinctionAsync(
        DistinctionState currentState,
        Observation observation,
        string stage,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            // Extract distinction from observation
            var distinctionId = Guid.NewGuid().ToString();
            var fitness = CalculateFitness(observation, stage);

            var distinction = new ActiveDistinction(
                Id: distinctionId,
                Content: observation.Content,
                Fitness: fitness,
                LearnedAt: observation.Timestamp,
                LearnedAtStage: stage);

            // Update epistemic certainty based on observation
            var newCertainty = UpdateCertainty(currentState.EpistemicCertainty, observation.PriorCertainty, fitness);

            var newState = currentState
                .WithDistinction(distinction)
                .WithCertainty(newCertainty);

            _logger?.LogDebug(
                "Updated distinction state: added {Id} at stage {Stage} with fitness {Fitness}",
                distinctionId,
                stage,
                fitness);

            return Result<DistinctionState, string>.Success(newState);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to update from distinction");
            return Result<DistinctionState, string>.Failure($"Update failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<DistinctionState, string>> RecognizeAsync(
        DistinctionState currentState,
        string circumstance,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            // At Recognition stage, boost certainty of related distinctions
            var recognitionBoost = 0.1;
            var newCertainty = Math.Min(1.0, currentState.EpistemicCertainty + recognitionBoost);

            var newState = currentState
                .WithCertainty(newCertainty)
                .NextCycle();

            _logger?.LogDebug(
                "Recognition applied for {Circumstance}: certainty boosted to {Certainty}",
                circumstance,
                newCertainty);

            return Result<DistinctionState, string>.Success(newState);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to apply recognition");
            return Result<DistinctionState, string>.Failure($"Recognition failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<Unit, string>> DissolveAsync(
        DistinctionState currentState,
        DissolutionStrategy strategy,
        CancellationToken ct = default)
    {
        try
        {
            var listResult = await _storage.ListWeightsAsync(ct);
            if (listResult.IsFailure)
            {
                return Result<Unit, string>.Failure($"Failed to list weights: {listResult.Error}");
            }

            var weights = listResult.Value;
            var toDissolve = strategy switch
            {
                DissolutionStrategy.FitnessThreshold => weights.Where(w => !w.IsDissolved && w.Fitness < DistinctionLearningConstants.DefaultFitnessThreshold).ToList(),
                DissolutionStrategy.OldestFirst => weights.Where(w => !w.IsDissolved).OrderBy(w => w.CreatedAt).Take(10).ToList(),
                DissolutionStrategy.LeastRecentlyUsed => weights.Where(w => !w.IsDissolved).OrderBy(w => w.CreatedAt).Take(10).ToList(),
                DissolutionStrategy.All => weights.Where(w => !w.IsDissolved).ToList(),
                _ => new List<DistinctionWeightMetadata>()
            };

            foreach (var weight in toDissolve)
            {
                var dissolveResult = await _storage.DissolveWeightsAsync(weight.Path, ct);
                if (dissolveResult.IsFailure)
                {
                    _logger?.LogWarning("Failed to dissolve {Id}: {Error}", weight.Id, dissolveResult.Error);
                }
            }

            _logger?.LogInformation(
                "Dissolved {Count} distinctions using strategy {Strategy}",
                toDissolve.Count,
                strategy);

            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to dissolve distinctions");
            return Result<Unit, string>.Failure($"Dissolution failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<double, string>> EvaluateDistinctionFitnessAsync(
        string distinction,
        IEnumerable<Observation> observations,
        CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        try
        {
            var observationList = observations.ToList();
            if (!observationList.Any())
            {
                return Result<double, string>.Success(0.0);
            }

            // Calculate fitness based on how many observations contain the distinction
            var matchCount = 0;
            var totalCertainty = 0.0;

            foreach (var obs in observationList)
            {
                if (obs.Content.Contains(distinction, StringComparison.OrdinalIgnoreCase))
                {
                    matchCount++;
                    totalCertainty += obs.PriorCertainty;
                }
            }

            // Fitness = (match rate * average certainty)
            var matchRate = (double)matchCount / observationList.Count;
            var avgCertainty = matchCount > 0 ? totalCertainty / matchCount : 0.0;
            var fitness = matchRate * avgCertainty;

            _logger?.LogDebug(
                "Evaluated fitness for distinction '{Distinction}': {Fitness} (matches: {Matches}/{Total})",
                distinction,
                fitness,
                matchCount,
                observationList.Count);

            return Result<double, string>.Success(fitness);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to evaluate distinction fitness");
            return Result<double, string>.Failure($"Fitness evaluation failed: {ex.Message}");
        }
    }

    private static double CalculateFitness(Observation observation, string stage)
    {
        // Base fitness on content length and prior certainty
        var contentFactor = Math.Min(1.0, observation.Content.Length / 100.0);
        var certaintyFactor = observation.PriorCertainty;

        // Boost fitness for Recognition stage
        var stageFactor = stage == "Recognition" ? 1.2 : 1.0;

        return Math.Min(1.0, (contentFactor + certaintyFactor) / 2.0 * stageFactor);
    }

    private static double UpdateCertainty(double current, double prior, double fitness)
    {
        // Weighted average of current, prior, and fitness
        return (current * 0.5) + (prior * 0.3) + (fitness * 0.2);
    }
}
