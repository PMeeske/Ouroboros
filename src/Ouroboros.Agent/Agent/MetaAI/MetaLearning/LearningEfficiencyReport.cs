#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Learning Efficiency Report Type
// Reports on learning efficiency metrics
// ==========================================================

namespace Ouroboros.Agent.MetaAI.MetaLearning;

/// <summary>
/// Report on learning efficiency and bottlenecks.
/// </summary>
public sealed record LearningEfficiencyReport(
    double AverageIterationsToLearn,
    double AverageExamplesNeeded,
    double SuccessRate,
    double LearningSpeedTrend,
    Dictionary<string, double> EfficiencyByTaskType,
    List<string> Bottlenecks,
    List<string> Recommendations);
