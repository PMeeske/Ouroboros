// <copyright file="EpisodeRunner.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Core.Monads;
using Ouroboros.Domain.Environment;
using Ouroboros.Domain.Reinforcement;

namespace Ouroboros.Application.Services.Reinforcement;

/// <summary>
/// Service for running episodes in an environment with a policy.
/// </summary>
public sealed class EpisodeRunner
{
    private readonly IEnvironmentActor environment;
    private readonly IPolicy policy;
    private readonly string environmentName;

    /// <summary>
    /// Initializes a new instance of the <see cref="EpisodeRunner"/> class.
    /// </summary>
    /// <param name="environment">The environment actor</param>
    /// <param name="policy">The policy to use</param>
    /// <param name="environmentName">Name of the environment</param>
    public EpisodeRunner(IEnvironmentActor environment, IPolicy policy, string environmentName)
    {
        this.environment = environment ?? throw new ArgumentNullException(nameof(environment));
        this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
        this.environmentName = environmentName ?? throw new ArgumentNullException(nameof(environmentName));
    }

    /// <summary>
    /// Runs a single episode.
    /// </summary>
    /// <param name="maxSteps">Maximum number of steps</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing the completed episode</returns>
    public async Task<Result<Episode>> RunEpisodeAsync(
        int maxSteps = 100,
        CancellationToken cancellationToken = default)
    {
        var episodeId = Guid.NewGuid();
        var steps = new List<EnvironmentStep>();
        var startTime = DateTime.UtcNow;

        // Reset environment
        var resetResult = await this.environment.ResetAsync(cancellationToken);
        if (resetResult.IsFailure)
        {
            return Result<Episode>.Failure($"Failed to reset environment: {resetResult.Error}");
        }

        var currentState = resetResult.Value;
        var totalReward = 0.0;
        var stepNumber = 0;
        var success = false;

        while (stepNumber < maxSteps)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Get available actions
            var actionsResult = await this.environment.GetAvailableActionsAsync(cancellationToken);
            if (actionsResult.IsFailure)
            {
                return Result<Episode>.Failure($"Failed to get available actions: {actionsResult.Error}");
            }

            // Select action using policy
            var actionResult = await this.policy.SelectActionAsync(
                currentState,
                actionsResult.Value,
                cancellationToken);

            if (actionResult.IsFailure)
            {
                return Result<Episode>.Failure($"Failed to select action: {actionResult.Error}");
            }

            var selectedAction = actionResult.Value;

            // Execute action
            var stepTimestamp = DateTime.UtcNow;
            var observationResult = await this.environment.ExecuteActionAsync(selectedAction, cancellationToken);

            if (observationResult.IsFailure)
            {
                return Result<Episode>.Failure($"Failed to execute action: {observationResult.Error}");
            }

            var observation = observationResult.Value;
            totalReward += observation.Reward;

            // Record step
            var envStep = new EnvironmentStep(
                stepNumber,
                currentState,
                selectedAction,
                observation,
                stepTimestamp);

            steps.Add(envStep);

            // Update policy
            var updateResult = await this.policy.UpdateAsync(
                currentState,
                selectedAction,
                observation,
                cancellationToken);

            if (updateResult.IsFailure)
            {
                // Log warning but continue
                Console.WriteLine($"Warning: Failed to update policy: {updateResult.Error}");
            }

            // Check if terminal
            if (observation.IsTerminal)
            {
                success = observation.Reward > 0; // Simple heuristic
                break;
            }

            currentState = observation.State;
            stepNumber++;
        }

        var endTime = DateTime.UtcNow;

        var episode = new Episode(
            episodeId,
            this.environmentName,
            steps,
            totalReward,
            startTime,
            endTime,
            success);

        return Result<Episode>.Success(episode);
    }

    /// <summary>
    /// Runs multiple episodes.
    /// </summary>
    /// <param name="episodeCount">Number of episodes to run</param>
    /// <param name="maxStepsPerEpisode">Maximum steps per episode</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result containing list of episodes</returns>
    public async Task<Result<IReadOnlyList<Episode>>> RunMultipleEpisodesAsync(
        int episodeCount,
        int maxStepsPerEpisode = 100,
        CancellationToken cancellationToken = default)
    {
        var episodes = new List<Episode>();

        for (var i = 0; i < episodeCount; i++)
        {
            var result = await this.RunEpisodeAsync(maxStepsPerEpisode, cancellationToken);

            if (result.IsFailure)
            {
                return Result<IReadOnlyList<Episode>>.Failure($"Episode {i} failed: {result.Error}");
            }

            episodes.Add(result.Value);
        }

        return Result<IReadOnlyList<Episode>>.Success(episodes);
    }
}
