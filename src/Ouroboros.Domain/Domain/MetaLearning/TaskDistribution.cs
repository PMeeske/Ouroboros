// <copyright file="TaskDistribution.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.MetaLearning;

/// <summary>
/// Represents a distribution over tasks for meta-learning.
/// Used to sample diverse tasks during meta-training.
/// </summary>
/// <param name="Name">Name of the distribution.</param>
/// <param name="Parameters">Configuration parameters for the distribution.</param>
/// <param name="Sampler">Function that generates tasks from this distribution.</param>
public sealed record TaskDistribution(
    string Name,
    Dictionary<string, object> Parameters,
    Func<Random, SynthesisTask> Sampler)
{
    /// <summary>
    /// Creates a simple uniform distribution over a fixed set of tasks.
    /// </summary>
    /// <param name="tasks">The set of tasks to sample from.</param>
    /// <returns>A TaskDistribution that uniformly samples from the given tasks.</returns>
    public static TaskDistribution Uniform(List<SynthesisTask> tasks)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        if (tasks.Count == 0)
        {
            throw new ArgumentException("Task list cannot be empty", nameof(tasks));
        }

        return new TaskDistribution(
            Name: "Uniform",
            Parameters: new Dictionary<string, object> { ["TaskCount"] = tasks.Count },
            Sampler: random => tasks[random.Next(tasks.Count)]);
    }

    /// <summary>
    /// Creates a weighted distribution over tasks.
    /// </summary>
    /// <param name="weightedTasks">Tasks with their sampling weights.</param>
    /// <returns>A TaskDistribution that samples according to weights.</returns>
    public static TaskDistribution Weighted(Dictionary<SynthesisTask, double> weightedTasks)
    {
        ArgumentNullException.ThrowIfNull(weightedTasks);
        if (weightedTasks.Count == 0)
        {
            throw new ArgumentException("Task list cannot be empty", nameof(weightedTasks));
        }

        var tasks = weightedTasks.Keys.ToList();
        var weights = weightedTasks.Values.ToList();
        var totalWeight = weights.Sum();

        return new TaskDistribution(
            Name: "Weighted",
            Parameters: new Dictionary<string, object>
            {
                ["TaskCount"] = tasks.Count,
                ["TotalWeight"] = totalWeight,
            },
            Sampler: random =>
            {
                var target = random.NextDouble() * totalWeight;
                var cumulative = 0.0;
                for (var i = 0; i < tasks.Count; i++)
                {
                    cumulative += weights[i];
                    if (cumulative >= target)
                    {
                        return tasks[i];
                    }
                }

                return tasks[^1];
            });
    }

    /// <summary>
    /// Samples a task from this distribution.
    /// </summary>
    /// <param name="random">Random number generator.</param>
    /// <returns>A sampled task.</returns>
    public SynthesisTask Sample(Random random) => Sampler(random);

    /// <summary>
    /// Samples multiple tasks from this distribution.
    /// </summary>
    /// <param name="count">Number of tasks to sample.</param>
    /// <param name="random">Random number generator.</param>
    /// <returns>List of sampled tasks.</returns>
    public List<SynthesisTask> SampleBatch(int count, Random random)
    {
        var tasks = new List<SynthesisTask>(count);
        for (var i = 0; i < count; i++)
        {
            tasks.Add(Sample(random));
        }

        return tasks;
    }
}
