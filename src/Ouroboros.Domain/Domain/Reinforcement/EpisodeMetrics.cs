// <copyright file="EpisodeMetrics.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Domain.Environment;

namespace Ouroboros.Domain.Reinforcement;

/// <summary>
/// Metrics collected for an episode.
/// Tracks success, reward, cost, and performance characteristics.
/// </summary>
/// <param name="EpisodeId">The episode ID</param>
/// <param name="Success">Whether the episode was successful</param>
/// <param name="TotalReward">Cumulative reward</param>
/// <param name="AverageReward">Average reward per step</param>
/// <param name="StepCount">Number of steps taken</param>
/// <param name="Duration">Duration of the episode</param>
/// <param name="AverageLatency">Average latency per step</param>
/// <param name="TotalCost">Total computational cost (if applicable)</param>
public sealed record EpisodeMetrics(
    Guid EpisodeId,
    bool Success,
    double TotalReward,
    double AverageReward,
    int StepCount,
    TimeSpan Duration,
    TimeSpan AverageLatency,
    double? TotalCost = null)
{
    /// <summary>
    /// Creates metrics from an episode.
    /// </summary>
    /// <param name="episode">The episode to analyze</param>
    /// <returns>Metrics for the episode</returns>
    public static EpisodeMetrics FromEpisode(Episode episode)
    {
        if (!episode.IsComplete)
        {
            throw new InvalidOperationException("Cannot create metrics for incomplete episode");
        }

        var stepCount = episode.StepCount;
        var averageReward = stepCount > 0 ? episode.TotalReward / stepCount : 0.0;
        var duration = episode.Duration ?? TimeSpan.Zero;
        var averageLatency = stepCount > 0 ? TimeSpan.FromTicks(duration.Ticks / stepCount) : TimeSpan.Zero;

        return new EpisodeMetrics(
            episode.Id,
            episode.Success,
            episode.TotalReward,
            averageReward,
            stepCount,
            duration,
            averageLatency);
    }
}
