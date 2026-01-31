#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Learning Episode Types
// Records information about learning episodes
// ==========================================================

namespace Ouroboros.Agent.MetaAI.MetaLearning;

/// <summary>
/// Represents a recorded learning episode for meta-learning analysis.
/// </summary>
public sealed record LearningEpisode(
    Guid Id,
    string TaskType,
    string TaskDescription,
    LearningStrategy StrategyUsed,
    int ExamplesProvided,
    int IterationsRequired,
    double FinalPerformance,
    TimeSpan LearningDuration,
    List<PerformanceSnapshot> ProgressCurve,
    bool Successful,
    string? FailureReason,
    DateTime StartedAt,
    DateTime CompletedAt);

/// <summary>
/// Represents a snapshot of performance at a specific iteration.
/// </summary>
public sealed record PerformanceSnapshot(
    int Iteration,
    double Performance,
    double Loss,
    DateTime Timestamp);
