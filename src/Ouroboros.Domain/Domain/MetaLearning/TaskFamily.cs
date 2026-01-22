// <copyright file="TaskFamily.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.MetaLearning;

/// <summary>
/// Represents a family of related tasks for meta-learning.
/// Tasks within a family share common structure but differ in specifics.
/// </summary>
/// <param name="Domain">The domain this task family belongs to.</param>
/// <param name="TrainingTasks">Tasks used for meta-training.</param>
/// <param name="ValidationTasks">Tasks used for meta-validation.</param>
/// <param name="Distribution">Distribution for sampling new tasks.</param>
public sealed record TaskFamily(
    string Domain,
    List<SynthesisTask> TrainingTasks,
    List<SynthesisTask> ValidationTasks,
    TaskDistribution Distribution)
{
    /// <summary>
    /// Gets the total number of tasks in this family.
    /// </summary>
    public int TotalTasks => TrainingTasks.Count + ValidationTasks.Count;

    /// <summary>
    /// Creates a task family from a list of tasks with automatic train/validation split.
    /// </summary>
    /// <param name="domain">Domain name.</param>
    /// <param name="allTasks">All tasks in the family.</param>
    /// <param name="validationSplit">Fraction of tasks to use for validation (0-1).</param>
    /// <returns>A new TaskFamily with split tasks.</returns>
    public static TaskFamily Create(
        string domain,
        List<SynthesisTask> allTasks,
        double validationSplit = 0.2)
    {
        ArgumentNullException.ThrowIfNull(allTasks);
        if (validationSplit is < 0 or > 1)
        {
            throw new ArgumentException("Validation split must be between 0 and 1", nameof(validationSplit));
        }

        var splitIndex = (int)(allTasks.Count * (1 - validationSplit));
        var trainingTasks = allTasks.Take(splitIndex).ToList();
        var validationTasks = allTasks.Skip(splitIndex).ToList();
        var distribution = TaskDistribution.Uniform(trainingTasks);

        return new TaskFamily(domain, trainingTasks, validationTasks, distribution);
    }

    /// <summary>
    /// Samples a batch of training tasks.
    /// </summary>
    /// <param name="batchSize">Number of tasks to sample.</param>
    /// <param name="random">Random number generator.</param>
    /// <returns>List of sampled training tasks.</returns>
    public List<SynthesisTask> SampleTrainingBatch(int batchSize, Random random) =>
        Distribution.SampleBatch(batchSize, random);

    /// <summary>
    /// Gets all training examples across all training tasks.
    /// </summary>
    /// <returns>Flattened list of all training examples.</returns>
    public List<Example> GetAllTrainingExamples() =>
        TrainingTasks.SelectMany(t => t.TrainingExamples).ToList();

    /// <summary>
    /// Gets all validation examples across all validation tasks.
    /// </summary>
    /// <returns>Flattened list of all validation examples.</returns>
    public List<Example> GetAllValidationExamples() =>
        ValidationTasks.SelectMany(t => t.ValidationExamples).ToList();
}
