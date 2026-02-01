// <copyright file="RewardShaper.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Ouroboros.Domain.Embodied;

namespace Ouroboros.Application.Embodied;

/// <summary>
/// Reward shaping for embodied agents.
/// Provides additional reward signals to guide learning through distance-based shaping,
/// velocity penalties, and curiosity-driven exploration bonuses.
/// </summary>
public sealed class RewardShaper
{
    private readonly ILogger<RewardShaper> logger;
    private readonly double distanceRewardWeight;
    private readonly double velocityPenaltyWeight;
    private readonly double curiosityBonusWeight;
    private readonly int maxNoveltyBufferSize;
    private readonly List<string> noveltyBuffer;

    /// <summary>
    /// Initializes a new instance of the <see cref="RewardShaper"/> class.
    /// </summary>
    /// <param name="distanceRewardWeight">Weight for distance-based reward shaping</param>
    /// <param name="velocityPenaltyWeight">Weight for velocity penalty (encourages smooth motion)</param>
    /// <param name="curiosityBonusWeight">Weight for curiosity-driven exploration bonus</param>
    /// <param name="maxNoveltyBufferSize">Maximum size of novelty tracking buffer</param>
    /// <param name="logger">Logger for diagnostic output</param>
    public RewardShaper(
        double distanceRewardWeight,
        double velocityPenaltyWeight,
        double curiosityBonusWeight,
        int maxNoveltyBufferSize,
        ILogger<RewardShaper> logger)
    {
        this.distanceRewardWeight = distanceRewardWeight;
        this.velocityPenaltyWeight = velocityPenaltyWeight;
        this.curiosityBonusWeight = curiosityBonusWeight;
        this.maxNoveltyBufferSize = maxNoveltyBufferSize;
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.noveltyBuffer = new List<string>();
    }

    /// <summary>
    /// Gets the number of novel states tracked.
    /// </summary>
    public int NoveltyBufferSize => this.noveltyBuffer.Count;

    /// <summary>
    /// Shapes the reward for a state transition.
    /// Combines distance-based rewards, velocity penalties, and curiosity bonuses.
    /// </summary>
    /// <param name="stateBefore">State before the action</param>
    /// <param name="stateAfter">State after the action</param>
    /// <param name="action">Action taken</param>
    /// <param name="baseReward">Base reward from environment</param>
    /// <param name="goalPosition">Optional goal position for distance-based shaping</param>
    /// <returns>Shaped reward value</returns>
    public double ShapeReward(
        SensorState stateBefore,
        SensorState stateAfter,
        EmbodiedAction action,
        double baseReward,
        Vector3? goalPosition = null)
    {
        if (stateBefore == null)
        {
            throw new ArgumentNullException(nameof(stateBefore));
        }

        if (stateAfter == null)
        {
            throw new ArgumentNullException(nameof(stateAfter));
        }

        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        var shapedReward = baseReward;

        // Distance-based reward shaping (potential-based)
        if (goalPosition != null)
        {
            var distanceBefore = this.ComputeDistance(stateBefore.Position, goalPosition);
            var distanceAfter = this.ComputeDistance(stateAfter.Position, goalPosition);
            var distanceReward = (distanceBefore - distanceAfter) * this.distanceRewardWeight;
            shapedReward += distanceReward;

            this.logger.LogTrace(
                "Distance reward: {Reward:F4} (before: {Before:F2}, after: {After:F2})",
                distanceReward,
                distanceBefore,
                distanceAfter);
        }

        // Velocity penalty for smooth motion
        var velocityMagnitude = this.ComputeMagnitude(stateAfter.Velocity);
        var velocityPenalty = -velocityMagnitude * this.velocityPenaltyWeight;
        shapedReward += velocityPenalty;

        this.logger.LogTrace(
            "Velocity penalty: {Penalty:F4} (magnitude: {Magnitude:F2})",
            velocityPenalty,
            velocityMagnitude);

        // Curiosity-driven exploration bonus
        var curiosityBonus = this.ComputeCuriosityBonus(stateAfter);
        shapedReward += curiosityBonus;

        this.logger.LogTrace("Curiosity bonus: {Bonus:F4}", curiosityBonus);

        this.logger.LogDebug(
            "Reward shaping: base={Base:F4}, shaped={Shaped:F4}, action={Action}",
            baseReward,
            shapedReward,
            action.ActionName);

        return shapedReward;
    }

    /// <summary>
    /// Computes curiosity bonus for visiting novel states.
    /// Higher bonus for states that haven't been visited recently.
    /// </summary>
    /// <param name="state">Current state</param>
    /// <returns>Curiosity bonus value</returns>
    private double ComputeCuriosityBonus(SensorState state)
    {
        var stateKey = this.GetStateKey(state);

        // Check if state is novel (not in recent history)
        if (!this.noveltyBuffer.Contains(stateKey))
        {
            // Novel state - add to buffer with FIFO eviction
            this.noveltyBuffer.Add(stateKey);

            while (this.noveltyBuffer.Count > this.maxNoveltyBufferSize)
            {
                this.noveltyBuffer.RemoveAt(0);
                this.logger.LogTrace("Novelty buffer full, removed oldest state (FIFO eviction)");
            }

            // Award curiosity bonus for novel state
            return this.curiosityBonusWeight;
        }

        // State already visited - no bonus
        return 0.0;
    }

    /// <summary>
    /// Clears the novelty tracking buffer.
    /// </summary>
    public void ClearNoveltyBuffer()
    {
        this.noveltyBuffer.Clear();
        this.logger.LogInformation("Novelty buffer cleared");
    }

    /// <summary>
    /// Computes Euclidean distance between two positions.
    /// </summary>
    /// <param name="a">First position</param>
    /// <param name="b">Second position</param>
    /// <returns>Distance value</returns>
    private double ComputeDistance(Vector3 a, Vector3 b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        var dz = a.Z - b.Z;
        return Math.Sqrt((dx * dx) + (dy * dy) + (dz * dz));
    }

    /// <summary>
    /// Computes magnitude of a vector.
    /// </summary>
    /// <param name="v">Vector</param>
    /// <returns>Magnitude value</returns>
    private double ComputeMagnitude(Vector3 v)
    {
        return Math.Sqrt((v.X * v.X) + (v.Y * v.Y) + (v.Z * v.Z));
    }

    /// <summary>
    /// Generates a state key for novelty tracking.
    /// </summary>
    /// <param name="state">Sensor state</param>
    /// <returns>State key string</returns>
    private string GetStateKey(SensorState state)
    {
        // Discretize position for novelty tracking (grid-based)
        var gridSize = 1.0f;
        var gridX = (int)(state.Position.X / gridSize);
        var gridY = (int)(state.Position.Y / gridSize);
        var gridZ = (int)(state.Position.Z / gridSize);
        return $"{gridX},{gridY},{gridZ}";
    }
}
