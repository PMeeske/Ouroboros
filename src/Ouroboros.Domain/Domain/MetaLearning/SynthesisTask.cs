// <copyright file="SynthesisTask.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.MetaLearning;

/// <summary>
/// Represents a single task in the meta-learning framework.
/// A task consists of training and validation examples for a specific problem.
/// </summary>
/// <param name="Id">Unique identifier for the task.</param>
/// <param name="Name">Human-readable name of the task.</param>
/// <param name="Domain">The domain or category this task belongs to (e.g., "code", "translation").</param>
/// <param name="TrainingExamples">Examples used for task adaptation (support set).</param>
/// <param name="ValidationExamples">Examples used for evaluating task performance (query set).</param>
/// <param name="Description">Optional description of what the task entails.</param>
public sealed record SynthesisTask(
    Guid Id,
    string Name,
    string Domain,
    List<Example> TrainingExamples,
    List<Example> ValidationExamples,
    string? Description = null)
{
    /// <summary>
    /// Creates a new task with random ID.
    /// </summary>
    /// <param name="name">Task name.</param>
    /// <param name="domain">Task domain.</param>
    /// <param name="trainingExamples">Training examples.</param>
    /// <param name="validationExamples">Validation examples.</param>
    /// <param name="description">Optional description.</param>
    /// <returns>A new SynthesisTask.</returns>
    public static SynthesisTask Create(
        string name,
        string domain,
        List<Example> trainingExamples,
        List<Example> validationExamples,
        string? description = null) =>
        new(Guid.NewGuid(), name, domain, trainingExamples, validationExamples, description);

    /// <summary>
    /// Gets the total number of examples in this task.
    /// </summary>
    public int TotalExamples => TrainingExamples.Count + ValidationExamples.Count;

    /// <summary>
    /// Splits examples into training and validation sets.
    /// </summary>
    /// <param name="allExamples">All available examples.</param>
    /// <param name="trainingSplit">Fraction of examples to use for training (0-1).</param>
    /// <returns>Tuple of training and validation examples.</returns>
    public static (List<Example> Training, List<Example> Validation) SplitExamples(
        List<Example> allExamples,
        double trainingSplit = 0.8)
    {
        if (trainingSplit is < 0 or > 1)
        {
            throw new ArgumentException("Training split must be between 0 and 1", nameof(trainingSplit));
        }

        var splitIndex = (int)(allExamples.Count * trainingSplit);
        var training = allExamples.Take(splitIndex).ToList();
        var validation = allExamples.Skip(splitIndex).ToList();
        return (training, validation);
    }
}
