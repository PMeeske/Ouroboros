#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Self-Evaluator Implementation
// Metacognitive monitoring and autonomous improvement
// ==========================================================

using System.Collections.Concurrent;

namespace LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Configuration for self-evaluator behavior.
/// </summary>
public sealed record SelfEvaluatorConfig(
    int CalibrationSampleSize = 100,
    double MinConfidenceForPrediction = 0.3,
    int InsightGenerationBatchSize = 20,
    TimeSpan PerformanceAnalysisWindow = default)
{
    public SelfEvaluatorConfig() : this(
        100,
        0.3,
        20,
        TimeSpan.FromDays(7))
    {
    }
}

/// <summary>
/// Represents a recorded prediction for calibration tracking.
/// </summary>
internal sealed record CalibrationRecord(
    double PredictedConfidence,
    bool ActualSuccess,
    DateTime RecordedAt);

/// <summary>
/// Implementation of self-evaluator for metacognitive monitoring.
/// Tracks performance, identifies patterns, and suggests improvements.
/// </summary>
public sealed class SelfEvaluator : ISelfEvaluator
{
    private readonly IChatCompletionModel _llm;
    private readonly ICapabilityRegistry _capabilities;
    private readonly ISkillRegistry _skills;
    private readonly IMemoryStore _memory;
    private readonly IMetaAIPlannerOrchestrator _orchestrator;
    private readonly SelfEvaluatorConfig _config;
    private readonly ConcurrentBag<CalibrationRecord> _calibrationRecords = new();

    public SelfEvaluator(
        IChatCompletionModel llm,
        ICapabilityRegistry capabilities,
        ISkillRegistry skills,
        IMemoryStore memory,
        IMetaAIPlannerOrchestrator orchestrator,
        SelfEvaluatorConfig? config = null)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _config = config ?? new SelfEvaluatorConfig();
    }

    /// <summary>
    /// Evaluates current performance across all capabilities.
    /// </summary>
    public async Task<Result<SelfAssessment, string>> EvaluatePerformanceAsync(
        CancellationToken ct = default)
    {
        try
        {
            // Get all capabilities
            var capabilities = await _capabilities.GetCapabilitiesAsync(ct);
            var skills = _skills.GetAllSkills();
            var metrics = _orchestrator.GetMetrics();

            // Calculate capability scores
            var capabilityScores = capabilities.ToDictionary(
                c => c.Name,
                c => c.SuccessRate);

            // Calculate overall performance
            var overallPerformance = capabilities.Any()
                ? capabilities.Average(c => c.SuccessRate)
                : 0.0;

            // Calculate confidence calibration
            var calibration = await GetConfidenceCalibrationAsync(ct);

            // Calculate skill acquisition rate
            var skillAcquisitionRate = CalculateSkillAcquisitionRate(skills);

            // Identify strengths and weaknesses
            var strengths = capabilities
                .Where(c => c.SuccessRate >= 0.8 && c.UsageCount >= 5)
                .Select(c => $"{c.Name} (Success: {c.SuccessRate:P0})")
                .ToList();

            var weaknesses = capabilities
                .Where(c => c.SuccessRate < 0.6 && c.UsageCount >= 5)
                .Select(c => $"{c.Name} (Success: {c.SuccessRate:P0})")
                .ToList();

            // Generate summary using LLM
            var summary = await GenerateAssessmentSummaryAsync(
                overallPerformance,
                calibration,
                skillAcquisitionRate,
                strengths,
                weaknesses,
                ct);

            var assessment = new SelfAssessment(
                overallPerformance,
                calibration,
                skillAcquisitionRate,
                capabilityScores,
                strengths,
                weaknesses,
                DateTime.UtcNow,
                summary);

            return Result<SelfAssessment, string>.Success(assessment);
        }
        catch (Exception ex)
        {
            return Result<SelfAssessment, string>.Failure($"Performance evaluation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Generates insights from recent experiences and performance data.
    /// </summary>
    public async Task<List<Insight>> GenerateInsightsAsync(
        CancellationToken ct = default)
    {
        var insights = new List<Insight>();

        try
        {
            // Analyze recent experiences
            var query = new MemoryQuery(
                Goal: "all",
                Context: new Dictionary<string, object>(),
                MaxResults: _config.InsightGenerationBatchSize,
                MinSimilarity: 0.0);

            var experiences = await _memory.RetrieveRelevantExperiencesAsync(query, ct);

            // Pattern detection: success/failure patterns
            var successfulExperiences = experiences.Where(e => e.Verification.Verified).ToList();
            var failedExperiences = experiences.Where(e => !e.Verification.Verified).ToList();

            if (successfulExperiences.Any())
            {
                var successPattern = await AnalyzePatternAsync(
                    successfulExperiences,
                    "success",
                    ct);

                if (!string.IsNullOrWhiteSpace(successPattern))
                {
                    insights.Add(new Insight(
                        "Success Pattern",
                        successPattern,
                        0.8,
                        successfulExperiences.Take(3).Select(e => e.Goal).ToList(),
                        DateTime.UtcNow));
                }
            }

            if (failedExperiences.Any())
            {
                var failurePattern = await AnalyzePatternAsync(
                    failedExperiences,
                    "failure",
                    ct);

                if (!string.IsNullOrWhiteSpace(failurePattern))
                {
                    insights.Add(new Insight(
                        "Failure Pattern",
                        failurePattern,
                        0.7,
                        failedExperiences.Take(3).Select(e => e.Goal).ToList(),
                        DateTime.UtcNow));
                }
            }

            // Capability insights
            var capabilities = await _capabilities.GetCapabilitiesAsync(ct);
            var improvingCaps = capabilities
                .Where(c => c.UsageCount >= 10 && c.SuccessRate >= 0.7)
                .OrderByDescending(c => c.SuccessRate)
                .Take(3)
                .ToList();

            if (improvingCaps.Any())
            {
                insights.Add(new Insight(
                    "Improving Capabilities",
                    $"Strong performance in: {string.Join(", ", improvingCaps.Select(c => c.Name))}",
                    0.9,
                    improvingCaps.Select(c => $"{c.Name}: {c.SuccessRate:P0}").ToList(),
                    DateTime.UtcNow));
            }

            // Calibration insights
            var calibration = await GetConfidenceCalibrationAsync(ct);
            if (calibration < 0.7)
            {
                insights.Add(new Insight(
                    "Calibration Issue",
                    "Confidence predictions are poorly calibrated. Consider adjusting confidence thresholds.",
                    0.85,
                    new List<string> { $"Calibration score: {calibration:P0}" },
                    DateTime.UtcNow));
            }
        }
        catch
        {
            // Return partial insights on error
        }

        return insights;
    }

    /// <summary>
    /// Suggests improvement strategies based on weaknesses.
    /// </summary>
    public async Task<Result<ImprovementPlan, string>> SuggestImprovementsAsync(
        CancellationToken ct = default)
    {
        try
        {
            var assessment = await EvaluatePerformanceAsync(ct);
            if (!assessment.IsSuccess)
                return Result<ImprovementPlan, string>.Failure(assessment.Error);

            var selfAssessment = assessment.Value;
            var insights = await GenerateInsightsAsync(ct);

            // Use LLM to generate improvement plan
            var prompt = $@"Based on this self-assessment, create an improvement plan:

OVERALL PERFORMANCE: {selfAssessment.OverallPerformance:P0}
CONFIDENCE CALIBRATION: {selfAssessment.ConfidenceCalibration:P0}
SKILL ACQUISITION RATE: {selfAssessment.SkillAcquisitionRate:F2} skills/day

STRENGTHS:
{string.Join("\n", selfAssessment.Strengths.Select(s => $"- {s}"))}

WEAKNESSES:
{string.Join("\n", selfAssessment.Weaknesses.Select(w => $"- {w}"))}

RECENT INSIGHTS:
{string.Join("\n", insights.Take(3).Select(i => $"- {i.Category}: {i.Description}"))}

Create a focused improvement plan with:
1. Primary goal (one clear objective)
2. 3-5 specific actions to take
3. Expected improvements (as percentages)
4. Estimated duration

Format:
GOAL: [goal]
ACTION 1: [action]
ACTION 2: [action]
...
EXPECTED IMPROVEMENTS:
- [metric]: [improvement %]
DURATION: [days/weeks]";

            var response = await _llm.GenerateTextAsync(prompt, ct);
            var plan = ParseImprovementPlan(response);

            return Result<ImprovementPlan, string>.Success(plan);
        }
        catch (Exception ex)
        {
            return Result<ImprovementPlan, string>.Failure($"Improvement planning failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Tracks confidence calibration over time.
    /// </summary>
    public async Task<double> GetConfidenceCalibrationAsync(CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var records = _calibrationRecords
            .Where(r => r.RecordedAt > DateTime.UtcNow.AddDays(-30))
            .Take(_config.CalibrationSampleSize)
            .ToList();

        if (records.Count < 10)
            return 0.5; // Not enough data

        // Calculate calibration using Brier score
        var brierScore = records.Average(r =>
        {
            var predicted = r.PredictedConfidence;
            var actual = r.ActualSuccess ? 1.0 : 0.0;
            return Math.Pow(predicted - actual, 2);
        });

        // Convert Brier score to calibration (0 = worst, 1 = perfect)
        var calibration = 1.0 - brierScore;
        return Math.Max(0.0, Math.Min(1.0, calibration));
    }

    /// <summary>
    /// Records a prediction and its actual outcome for calibration.
    /// </summary>
    public void RecordPrediction(double predictedConfidence, bool actualSuccess)
    {
        if (predictedConfidence < _config.MinConfidenceForPrediction)
            return;

        _calibrationRecords.Add(new CalibrationRecord(
            predictedConfidence,
            actualSuccess,
            DateTime.UtcNow));
    }

    /// <summary>
    /// Gets performance trends over time.
    /// </summary>
    public async Task<List<(DateTime Time, double Value)>> GetPerformanceTrendAsync(
        string metric,
        TimeSpan timeWindow,
        CancellationToken ct = default)
    {
        await Task.CompletedTask;

        var trends = new List<(DateTime, double)>();
        var startTime = DateTime.UtcNow - timeWindow;

        switch (metric.ToLowerInvariant())
        {
            case "success_rate":
                var records = _calibrationRecords
                    .Where(r => r.RecordedAt >= startTime)
                    .OrderBy(r => r.RecordedAt)
                    .ToList();

                // Group by day
                var grouped = records.GroupBy(r => r.RecordedAt.Date);
                foreach (var group in grouped)
                {
                    var successRate = group.Count(r => r.ActualSuccess) / (double)group.Count();
                    trends.Add((group.Key, successRate));
                }
                break;

            case "skill_count":
                var skills = _skills.GetAllSkills();
                // Approximate: assume linear growth
                var currentCount = skills.Count;
                var daysAgo = (int)timeWindow.TotalDays;
                for (int i = daysAgo; i >= 0; i--)
                {
                    var estimatedCount = currentCount * (daysAgo - i) / (double)daysAgo;
                    trends.Add((DateTime.UtcNow.AddDays(-i), estimatedCount));
                }
                break;

            default:
                // Unknown metric
                break;
        }

        return trends;
    }

    // Private helper methods

    private double CalculateSkillAcquisitionRate(IReadOnlyList<Skill> skills)
    {
        if (!skills.Any())
            return 0.0;

        var recentSkills = skills
            .Where(s => s.CreatedAt > DateTime.UtcNow.AddDays(-30))
            .ToList();

        return recentSkills.Count / 30.0; // Skills per day
    }

    private async Task<string> GenerateAssessmentSummaryAsync(
        double overallPerformance,
        double calibration,
        double skillAcquisitionRate,
        List<string> strengths,
        List<string> weaknesses,
        CancellationToken ct)
    {
        var prompt = $@"Generate a concise self-assessment summary:

PERFORMANCE: {overallPerformance:P0}
CALIBRATION: {calibration:P0}
SKILL GROWTH: {skillAcquisitionRate:F2} skills/day

STRENGTHS: {strengths.Count} areas
WEAKNESSES: {weaknesses.Count} areas

Provide a 2-3 sentence summary of current state and trajectory.";

        try
        {
            return await _llm.GenerateTextAsync(prompt, ct);
        }
        catch
        {
            return $"Performance at {overallPerformance:P0} with {strengths.Count} strengths and {weaknesses.Count} areas for improvement.";
        }
    }

    private async Task<string> AnalyzePatternAsync(
        List<Experience> experiences,
        string patternType,
        CancellationToken ct)
    {
        if (!experiences.Any())
            return "";

        var prompt = $@"Analyze these {patternType} experiences and identify common patterns:

{string.Join("\n", experiences.Take(5).Select(e => $"- Goal: {e.Goal}, Quality: {e.Verification.QualityScore:P0}"))}

What patterns do you observe? Provide one concise insight.";

        try
        {
            return await _llm.GenerateTextAsync(prompt, ct);
        }
        catch
        {
            return "";
        }
    }

    private ImprovementPlan ParseImprovementPlan(string response)
    {
        var lines = response.Split('\n');
        var goal = "Improve overall performance";
        var actions = new List<string>();
        var expectedImprovements = new Dictionary<string, double>();
        var duration = TimeSpan.FromDays(7);

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("GOAL:"))
            {
                goal = trimmed.Substring("GOAL:".Length).Trim();
            }
            else if (trimmed.StartsWith("ACTION"))
            {
                var action = trimmed.Split(':').Skip(1).FirstOrDefault()?.Trim();
                if (!string.IsNullOrWhiteSpace(action))
                    actions.Add(action);
            }
            else if (trimmed.StartsWith("DURATION:"))
            {
                var durationStr = trimmed.Substring("DURATION:".Length).Trim().ToLowerInvariant();
                if (durationStr.Contains("day"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(durationStr, @"(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out var days))
                        duration = TimeSpan.FromDays(days);
                }
                else if (durationStr.Contains("week"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(durationStr, @"(\d+)");
                    if (match.Success && int.TryParse(match.Groups[1].Value, out var weeks))
                        duration = TimeSpan.FromDays(weeks * 7);
                }
            }
        }

        // Default actions if none parsed
        if (!actions.Any())
        {
            actions.Add("Focus on weak capabilities");
            actions.Add("Increase practice in low-performing areas");
            actions.Add("Review and learn from failures");
        }

        return new ImprovementPlan(
            goal,
            actions,
            expectedImprovements,
            duration,
            0.8,
            DateTime.UtcNow);
    }
}
