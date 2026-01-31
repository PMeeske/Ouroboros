#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Meta-Learner Interface
// Enables learning how to learn more effectively
// ==========================================================

namespace Ouroboros.Agent.MetaAI.MetaLearning;

/// <summary>
/// Interface for meta-learning capabilities.
/// Enables the agent to learn how to learn more effectively by optimizing learning strategies,
/// performing few-shot adaptation, and suggesting optimal hyperparameters.
/// </summary>
public interface IMetaLearner
{
    /// <summary>
    /// Analyzes learning history and optimizes learning strategy.
    /// </summary>
    /// <param name="history">Historical learning episodes to analyze</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Optimized learning strategy or error message</returns>
    Task<Result<LearningStrategy, string>> OptimizeLearningStrategyAsync(
        IReadOnlyList<LearningEpisode> history,
        CancellationToken ct = default);

    /// <summary>
    /// Performs few-shot adaptation to a new task.
    /// </summary>
    /// <param name="taskDescription">Description of the task to adapt to</param>
    /// <param name="examples">Few-shot examples for the task</param>
    /// <param name="maxExamples">Maximum number of examples to use (default: 5)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Adapted model or error message</returns>
    Task<Result<AdaptedModel, string>> FewShotAdaptAsync(
        string taskDescription,
        IReadOnlyList<TaskExample> examples,
        int maxExamples = 5,
        CancellationToken ct = default);

    /// <summary>
    /// Suggests optimal hyperparameters for a given task type.
    /// </summary>
    /// <param name="taskType">The type of task (e.g., "classification", "generation")</param>
    /// <param name="context">Additional context information</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Hyperparameter configuration or error message</returns>
    Task<Result<HyperparameterConfig, string>> SuggestHyperparametersAsync(
        string taskType,
        Dictionary<string, object>? context = null,
        CancellationToken ct = default);

    /// <summary>
    /// Evaluates how well current learning approach works.
    /// </summary>
    /// <param name="evaluationWindow">Time window for evaluation</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Learning efficiency report or error message</returns>
    Task<Result<LearningEfficiencyReport, string>> EvaluateLearningEfficiencyAsync(
        TimeSpan evaluationWindow,
        CancellationToken ct = default);

    /// <summary>
    /// Identifies transferable meta-knowledge from past learning.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of meta-knowledge insights or error message</returns>
    Task<Result<List<MetaKnowledge>, string>> ExtractMetaKnowledgeAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Records a learning episode for meta-learning.
    /// </summary>
    /// <param name="episode">The learning episode to record</param>
    void RecordLearningEpisode(LearningEpisode episode);
}
