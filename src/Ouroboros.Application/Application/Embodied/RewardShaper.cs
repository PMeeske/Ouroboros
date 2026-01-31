// <copyright file="RewardShaper.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Ouroboros.Domain.Embodied;

namespace Ouroboros.Application.Embodied;

/// <summary>
/// Interface for reward shaping in reinforcement learning.
/// </summary>
public interface IRewardShaper
{
    /// <summary>
    /// Shapes reward based on state transition and action.
    /// </summary>
    /// <param name="rawReward">Raw reward from environment</param>
    /// <param name="previousState">State before action</param>
    /// <param name="action">Action taken</param>
    /// <param name="currentState">State after action</param>
    /// <returns>Shaped reward value</returns>
    double ShapeReward(
        double rawReward,
        SensorState previousState,
        EmbodiedAction action,
        SensorState currentState);

    /// <summary>
    /// Computes curiosity bonus based on state novelty.
    /// </summary>
    /// <param name="state">Current state</param>
    /// <param name="action">Action taken</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Curiosity reward bonus</returns>
    Task<double> ComputeCuriosityRewardAsync(
        SensorState state,
        EmbodiedAction action,
        CancellationToken ct = default);
}

/// <summary>
/// Implements reward shaping for embodied agents.
/// Provides distance-based shaping and curiosity bonuses.
/// </summary>
public sealed class RewardShaper : IRewardShaper
{
    private readonly ILogger<RewardShaper> logger;
    private readonly HashSet<string> visitedStates;
    private readonly double distanceWeight;
    private readonly double curiosityWeight;
    private readonly int maxVisitedStates; // Maximum capacity to prevent unbounded growth

    /// <summary>
    /// Initializes a new instance of the <see cref="RewardShaper"/> class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output</param>
    /// <param name="distanceWeight">Weight for distance-based reward shaping</param>
    /// <param name="curiosityWeight">Weight for curiosity bonus</param>
    /// <param name="maxVisitedStates">Maximum number of visited states to track (default: 10000)</param>
    public RewardShaper(
        ILogger<RewardShaper> logger,
        double distanceWeight = 0.1,
        double curiosityWeight = 0.05,
        int maxVisitedStates = 10000)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.visitedStates = new HashSet<string>();
        this.distanceWeight = distanceWeight;
        this.curiosityWeight = curiosityWeight;
        this.maxVisitedStates = maxVisitedStates;
    }

    /// <inheritdoc/>
    public double ShapeReward(
        double rawReward,
        SensorState previousState,
        EmbodiedAction action,
        SensorState currentState)
    {
        if (previousState == null || currentState == null)
        {
            return rawReward;
        }

        try
        {
            // Start with raw reward
            var shapedReward = rawReward;

            // Add distance-based shaping
            // Reward getting closer to origin (assuming goal is at origin)
            var previousDistance = this.ComputeDistanceToGoal(previousState.Position);
            var currentDistance = this.ComputeDistanceToGoal(currentState.Position);
            var distanceImprovement = previousDistance - currentDistance;

            shapedReward += distanceImprovement * this.distanceWeight;

            // Penalize large velocities (encourage smooth motion)
            var velocityMagnitude = this.ComputeMagnitude(currentState.Velocity);
            if (velocityMagnitude > 5.0)
            {
                shapedReward -= 0.01 * (velocityMagnitude - 5.0);
            }

            this.logger.LogDebug(
                "Shaped reward: Raw={Raw:F3}, Shaped={Shaped:F3}, DistDelta={Dist:F3}",
                rawReward,
                shapedReward,
                distanceImprovement);

            return shapedReward;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error in reward shaping, returning raw reward");
            return rawReward;
        }
    }

    /// <inheritdoc/>
    public async Task<double> ComputeCuriosityRewardAsync(
        SensorState state,
        EmbodiedAction action,
        CancellationToken ct = default)
    {
        try
        {
            if (state == null)
            {
                return 0.0;
            }

            // Discretize state for novelty detection
            var stateKey = this.DiscretizeState(state);

            // Check if state is novel
            if (!this.visitedStates.Contains(stateKey))
            {
                // Implement capacity limit with eviction when needed
                if (this.visitedStates.Count >= this.maxVisitedStates)
                {
                    // Evict a subset of states (simple approach - remove half when capacity is reached)
                    // Note: HashSet does not guarantee insertion order, so this is not true LRU.
                    // For true LRU eviction, consider using LinkedHashSet or maintaining separate order tracking.
                    var toRemove = this.visitedStates.Take(this.maxVisitedStates / 2).ToList();
                    foreach (var key in toRemove)
                    {
                        this.visitedStates.Remove(key);
                    }

                    this.logger.LogDebug("Visited states cache cleared: removed {Count} entries due to capacity limit", toRemove.Count);
                }

                this.visitedStates.Add(stateKey);
                var curiosityBonus = this.curiosityWeight;

                this.logger.LogDebug("Novel state visited, curiosity bonus: {Bonus:F4}", curiosityBonus);
                return curiosityBonus;
            }

            return 0.0;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Error computing curiosity reward");
            return 0.0;
        }
    }

    /// <summary>
    /// Computes distance to goal (assumed at origin).
    /// </summary>
    private double ComputeDistanceToGoal(Vector3 position)
    {
        return Math.Sqrt((position.X * position.X) + (position.Y * position.Y) + (position.Z * position.Z));
    }

    /// <summary>
    /// Computes magnitude of a vector.
    /// </summary>
    private double ComputeMagnitude(Vector3 vector)
    {
        return Math.Sqrt((vector.X * vector.X) + (vector.Y * vector.Y) + (vector.Z * vector.Z));
    }

    /// <summary>
    /// Discretizes state into a string key for novelty tracking.
    /// </summary>
    private string DiscretizeState(SensorState state)
    {
        // Discretize position to 1-unit grid
        var x = (int)Math.Floor(state.Position.X);
        var y = (int)Math.Floor(state.Position.Y);
        var z = (int)Math.Floor(state.Position.Z);

        return $"{x}:{y}:{z}";
    }
}
