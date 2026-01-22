// <copyright file="DistinctionLearningMiddleware.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Middleware;

using Microsoft.Extensions.Logging;
using Ouroboros.Application.Personality.Consciousness;
using Ouroboros.Core.DistinctionLearning;
using Ouroboros.Pipeline.Middleware;

/// <summary>
/// Pipeline middleware that triggers distinction learning on each interaction.
/// Walks through the consciousness dream cycle and learns at each stage.
/// </summary>
public sealed class DistinctionLearningMiddleware : IPipelineMiddleware
{
    private readonly IDistinctionLearner _learner;
    private readonly ConsciousnessDream _dream;
    private readonly ILogger<DistinctionLearningMiddleware>? _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistinctionLearningMiddleware"/> class.
    /// </summary>
    public DistinctionLearningMiddleware(
        IDistinctionLearner learner,
        ConsciousnessDream dream,
        ILogger<DistinctionLearningMiddleware>? logger = null)
    {
        _learner = learner ?? throw new ArgumentNullException(nameof(learner));
        _dream = dream ?? throw new ArgumentNullException(nameof(dream));
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<PipelineResult> ProcessAsync(
        PipelineContext context,
        Func<PipelineContext, CancellationToken, Task<PipelineResult>> next,
        CancellationToken ct = default)
    {
        // 1. Execute the main pipeline
        var result = await next(context, ct);

        // 2. Learn from this interaction (async, don't block)
        _ = LearnFromInteractionAsync(context, result, ct);

        return result;
    }

    private async Task LearnFromInteractionAsync(
        PipelineContext context,
        PipelineResult result,
        CancellationToken ct)
    {
        try
        {
            var state = DistinctionState.Initial();
            var circumstance = context.Input;

            // Walk through the dream cycle
            await foreach (var moment in _dream.WalkTheDream(circumstance, ct))
            {
                var observation = new Observation(
                    Content: circumstance,
                    Timestamp: DateTime.UtcNow,
                    PriorCertainty: state.EpistemicCertainty,
                    Context: new Dictionary<string, object>
                    {
                        ["stage"] = moment.Stage.ToString(),
                        ["result_success"] = result.Success,
                        ["output_length"] = result.Output?.Length ?? 0
                    });

                // Update state at each stage
                var updateResult = await _learner.UpdateFromDistinctionAsync(
                    state, observation, moment.Stage.ToString(), ct);

                if (updateResult.IsSuccess)
                {
                    state = updateResult.Value;
                }

                // At Recognition, apply self-insight
                if (moment.Stage == DreamStage.Recognition)
                {
                    var recognizeResult = await _learner.RecognizeAsync(
                        state, circumstance, ct);

                    if (recognizeResult.IsSuccess)
                    {
                        state = recognizeResult.Value;
                    }
                }

                // At Dissolution, clean up low-fitness distinctions
                if (moment.Stage == DreamStage.Dissolution)
                {
                    await _learner.DissolveAsync(
                        state, DissolutionStrategy.FitnessThreshold, ct);
                }
            }

            _logger?.LogDebug(
                "Learned from interaction: {ActiveCount} active distinctions, cycle {Cycle}",
                state.ActiveDistinctions.Count,
                state.CycleCount);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Distinction learning failed for interaction");
            // Don't fail the main pipeline
        }
    }
}
