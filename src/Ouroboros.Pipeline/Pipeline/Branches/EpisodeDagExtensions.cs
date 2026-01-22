// <copyright file="EpisodeDagExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Branches;

using Ouroboros.Domain.Environment;
using Ouroboros.Domain.Events;

/// <summary>
/// Extension methods for integrating environment episodes into the DAG.
/// Provides Phase 1 (Embodiment) episode tracing capabilities.
/// </summary>
public static class EpisodeDagExtensions
{
    /// <summary>
    /// Records a completed environment episode into the DAG.
    /// This method links embodied agent experiences to the pipeline execution trace,
    /// enabling episode replay, analysis, and learning from environment interactions.
    /// </summary>
    /// <param name="branch">The pipeline branch to record the episode in.</param>
    /// <param name="episode">The completed episode to record.</param>
    /// <returns>A new PipelineBranch with the episode recorded.</returns>
    public static PipelineBranch RecordEpisode(this PipelineBranch branch, Episode episode)
    {
        ArgumentNullException.ThrowIfNull(branch);
        ArgumentNullException.ThrowIfNull(episode);

        return branch.WithEpisode(episode);
    }

    /// <summary>
    /// Records multiple episodes into the DAG in sequence.
    /// Useful for batch recording of training episodes or experience replay.
    /// </summary>
    /// <param name="branch">The pipeline branch to record episodes in.</param>
    /// <param name="episodes">The episodes to record.</param>
    /// <returns>A new PipelineBranch with all episodes recorded.</returns>
    public static PipelineBranch RecordEpisodes(this PipelineBranch branch, IEnumerable<Episode> episodes)
    {
        ArgumentNullException.ThrowIfNull(branch);
        ArgumentNullException.ThrowIfNull(episodes);

        PipelineBranch current = branch;
        foreach (Episode episode in episodes)
        {
            current = current.WithEpisode(episode);
        }

        return current;
    }

    /// <summary>
    /// Retrieves all environment episodes recorded in this branch.
    /// Useful for episode replay, analysis, and reinforcement learning.
    /// </summary>
    /// <param name="branch">The pipeline branch to query.</param>
    /// <returns>All episodes recorded in this branch, in chronological order.</returns>
    public static IEnumerable<Episode> GetEpisodes(this PipelineBranch branch)
    {
        ArgumentNullException.ThrowIfNull(branch);

        return branch.Events
            .OfType<EpisodeEvent>()
            .Select(e => e.Episode)
            .OrderBy(e => e.StartTime);
    }

    /// <summary>
    /// Gets episode statistics from the branch.
    /// Useful for training metrics and performance analysis.
    /// </summary>
    /// <param name="branch">The pipeline branch to analyze.</param>
    /// <returns>Statistics about recorded episodes.</returns>
    public static EpisodeStatistics GetEpisodeStatistics(this PipelineBranch branch)
    {
        ArgumentNullException.ThrowIfNull(branch);

        var episodes = branch.GetEpisodes().ToList();

        if (episodes.Count == 0)
        {
            return new EpisodeStatistics(
                TotalEpisodes: 0,
                SuccessfulEpisodes: 0,
                SuccessRate: 0.0,
                AverageReward: 0.0,
                AverageSteps: 0.0,
                TotalReward: 0.0);
        }

        int successCount = episodes.Count(e => e.Success);
        double totalReward = episodes.Sum(e => e.TotalReward);
        double avgSteps = episodes.Average(e => e.StepCount);

        return new EpisodeStatistics(
            TotalEpisodes: episodes.Count,
            SuccessfulEpisodes: successCount,
            SuccessRate: (double)successCount / episodes.Count,
            AverageReward: totalReward / episodes.Count,
            AverageSteps: avgSteps,
            TotalReward: totalReward);
    }

    /// <summary>
    /// Finds the most successful episode in the branch based on reward.
    /// </summary>
    /// <param name="branch">The pipeline branch to search.</param>
    /// <returns>The episode with the highest reward, or null if no episodes exist.</returns>
    public static Episode? GetBestEpisode(this PipelineBranch branch)
    {
        ArgumentNullException.ThrowIfNull(branch);

        return branch.GetEpisodes()
            .OrderByDescending(e => e.TotalReward)
            .FirstOrDefault();
    }
}

/// <summary>
/// Statistics about recorded episodes in a branch.
/// </summary>
/// <param name="TotalEpisodes">Total number of episodes recorded.</param>
/// <param name="SuccessfulEpisodes">Number of successful episodes.</param>
/// <param name="SuccessRate">Ratio of successful to total episodes (0.0 to 1.0).</param>
/// <param name="AverageReward">Mean reward across all episodes.</param>
/// <param name="AverageSteps">Mean number of steps per episode.</param>
/// <param name="TotalReward">Sum of all episode rewards.</param>
public sealed record EpisodeStatistics(
    int TotalEpisodes,
    int SuccessfulEpisodes,
    double SuccessRate,
    double AverageReward,
    double AverageSteps,
    double TotalReward);
