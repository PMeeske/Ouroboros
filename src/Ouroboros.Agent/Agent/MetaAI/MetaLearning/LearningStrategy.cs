#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Learning Strategy Types
// Defines learning approaches and strategies
// ==========================================================

namespace Ouroboros.Agent.MetaAI.MetaLearning;

/// <summary>
/// Represents a learning strategy for a specific type of task.
/// </summary>
public sealed record LearningStrategy(
    string Name,
    string Description,
    LearningApproach Approach,
    HyperparameterConfig Hyperparameters,
    List<string> SuitableTaskTypes,
    double ExpectedEfficiency,
    Dictionary<string, object> CustomConfig);

/// <summary>
/// Enumeration of learning approaches.
/// </summary>
public enum LearningApproach
{
    /// <summary>
    /// Supervised learning with labeled examples
    /// </summary>
    Supervised,

    /// <summary>
    /// Reinforcement learning with rewards
    /// </summary>
    Reinforcement,

    /// <summary>
    /// Self-supervised learning from unlabeled data
    /// </summary>
    SelfSupervised,

    /// <summary>
    /// Learning by imitating expert demonstrations
    /// </summary>
    ImitationLearning,

    /// <summary>
    /// Progressive learning from simple to complex
    /// </summary>
    CurriculumLearning,

    /// <summary>
    /// Meta-learning using gradient-based optimization
    /// </summary>
    MetaGradient,

    /// <summary>
    /// Prototypical learning with similarity metrics
    /// </summary>
    PrototypicalLearning,
}
