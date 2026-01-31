#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Adapted Model Type
// Represents a model adapted for a specific task
// ==========================================================

namespace Ouroboros.Agent.MetaAI.MetaLearning;

/// <summary>
/// Represents a model adapted to a new task using few-shot learning.
/// </summary>
public sealed record AdaptedModel(
    string TaskDescription,
    Skill AdaptedSkill,
    int ExamplesUsed,
    double EstimatedPerformance,
    double AdaptationTime,
    List<string> LearnedPatterns);
