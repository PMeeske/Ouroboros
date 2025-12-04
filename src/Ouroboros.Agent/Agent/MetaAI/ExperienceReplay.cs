#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Experience Replay - Train on stored experiences
// ==========================================================

namespace LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Represents a training batch from experience replay.
/// </summary>
public sealed record TrainingBatch(
    List<Experience> Experiences,
    Dictionary<string, double> Metrics,
    DateTime CreatedAt);

/// <summary>
/// Represents training result from experience replay.
/// </summary>
public sealed record TrainingResult(
    int ExperiencesProcessed,
    Dictionary<string, double> ImprovedMetrics,
    List<string> LearnedPatterns,
    bool Success);

/// <summary>
/// Configuration for experience replay.
/// </summary>
public sealed record ExperienceReplayConfig(
    int BatchSize = 10,
    double MinQualityScore = 0.6,
    int MaxExperiences = 100,
    bool PrioritizeHighQuality = true);

/// <summary>
/// Interface for experience replay capabilities.
/// </summary>
public interface IExperienceReplay
{
    /// <summary>
    /// Trains the orchestrator on stored experiences.
    /// </summary>
    Task<Result<TrainingResult, string>> TrainOnExperiencesAsync(
        ExperienceReplayConfig? config = null,
        CancellationToken ct = default);

    /// <summary>
    /// Analyzes experiences to extract patterns.
    /// </summary>
    Task<List<string>> AnalyzeExperiencePatternsAsync(
        List<Experience> experiences,
        CancellationToken ct = default);

    /// <summary>
    /// Selects experiences for training based on priority.
    /// </summary>
    Task<List<Experience>> SelectTrainingExperiencesAsync(
        ExperienceReplayConfig config,
        CancellationToken ct = default);
}

/// <summary>
/// Implementation of experience replay for continual learning.
/// </summary>
public sealed class ExperienceReplay : IExperienceReplay
{
    private readonly IMemoryStore _memory;
    private readonly ISkillRegistry _skills;
    private readonly IChatCompletionModel _llm;

    public ExperienceReplay(
        IMemoryStore memory,
        ISkillRegistry skills,
        IChatCompletionModel llm)
    {
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
    }

    /// <summary>
    /// Trains the orchestrator on stored experiences.
    /// </summary>
    public async Task<Result<TrainingResult, string>> TrainOnExperiencesAsync(
        ExperienceReplayConfig? config = null,
        CancellationToken ct = default)
    {
        config ??= new ExperienceReplayConfig();

        try
        {
            // Select experiences for training
            List<Experience> experiences = await SelectTrainingExperiencesAsync(config, ct);

            if (experiences.Count == 0)
            {
                return Result<TrainingResult, string>.Success(
                    new TrainingResult(0, new Dictionary<string, double>(), new List<string>(), true));
            }

            // Analyze patterns
            List<string> patterns = await AnalyzeExperiencePatternsAsync(experiences, ct);

            // Extract skills from high-quality experiences
            int skillsExtracted = 0;
            foreach (Experience? exp in experiences.Where(e => e.Verification.QualityScore > 0.8))
            {
                string skillName = $"learned_skill_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
                Result<Skill, string> skillResult = await _skills.ExtractSkillAsync(
                    exp.Execution,
                    skillName,
                    $"Learned from goal: {exp.Goal}");

                if (skillResult.IsSuccess)
                {
                    skillsExtracted++;
                }
            }

            // Calculate improved metrics
            Dictionary<string, double> improvedMetrics = new Dictionary<string, double>
            {
                ["patterns_discovered"] = patterns.Count,
                ["skills_extracted"] = skillsExtracted,
                ["avg_quality"] = experiences.Average(e => e.Verification.QualityScore)
            };

            TrainingResult result = new TrainingResult(
                experiences.Count,
                improvedMetrics,
                patterns,
                Success: true);

            return Result<TrainingResult, string>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<TrainingResult, string>.Failure($"Training failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Analyzes experiences to extract patterns.
    /// </summary>
    public async Task<List<string>> AnalyzeExperiencePatternsAsync(
        List<Experience> experiences,
        CancellationToken ct = default)
    {
        List<string> patterns = new List<string>();

        try
        {
            // Group experiences by goal similarity
            IEnumerable<IGrouping<string, Experience>> goalGroups = experiences
                .GroupBy(e => ExtractGoalType(e.Goal))
                .Where(g => g.Count() > 1);

            foreach (IGrouping<string, Experience>? group in goalGroups)
            {
                // Find common successful patterns
                List<Experience> successfulExperiences = group.Where(e => e.Verification.Verified).ToList();

                if (successfulExperiences.Count > 0)
                {
                    List<string> commonActions = FindCommonActions(successfulExperiences);
                    if (commonActions.Any())
                    {
                        patterns.Add($"Pattern for {group.Key}: {string.Join(" -> ", commonActions)}");
                    }
                }
            }

            // Use LLM to identify deeper patterns if available
            if (patterns.Any())
            {
                string patternPrompt = BuildPatternAnalysisPrompt(experiences);
                string analysis = await _llm.GenerateTextAsync(patternPrompt, ct);

                // Extract insights from LLM analysis
                List<string> insights = ExtractInsights(analysis);
                patterns.AddRange(insights);
            }
        }
        catch
        {
            // Fallback to simple pattern detection
        }

        return patterns.Distinct().ToList();
    }

    /// <summary>
    /// Selects experiences for training based on priority.
    /// </summary>
    public async Task<List<Experience>> SelectTrainingExperiencesAsync(
        ExperienceReplayConfig config,
        CancellationToken ct = default)
    {
        MemoryStatistics stats = await _memory.GetStatisticsAsync();

        // Get all experiences and filter
        MemoryQuery query = new MemoryQuery(
            Goal: "",
            Context: null,
            MaxResults: config.MaxExperiences,
            MinSimilarity: 0.0);

        List<Experience> allExperiences = await _memory.RetrieveRelevantExperiencesAsync(query, ct);

        // Filter by quality
        List<Experience> qualityFiltered = allExperiences
            .Where(e => e.Verification.QualityScore >= config.MinQualityScore)
            .ToList();

        // Prioritize based on configuration
        if (config.PrioritizeHighQuality)
        {
            qualityFiltered = qualityFiltered
                .OrderByDescending(e => e.Verification.QualityScore)
                .ThenByDescending(e => e.Timestamp)
                .Take(config.BatchSize)
                .ToList();
        }
        else
        {
            // Diverse sampling - mix of quality levels
            qualityFiltered = qualityFiltered
                .OrderBy(_ => Guid.NewGuid()) // Random sampling
                .Take(config.BatchSize)
                .ToList();
        }

        return qualityFiltered;
    }

    private string ExtractGoalType(string goal)
    {
        // Simple categorization - in production use more sophisticated NLP
        string goalLower = goal.ToLowerInvariant();

        if (goalLower.Contains("calculate") || goalLower.Contains("compute"))
            return "calculation";
        if (goalLower.Contains("analyze") || goalLower.Contains("examine"))
            return "analysis";
        if (goalLower.Contains("create") || goalLower.Contains("generate"))
            return "creation";
        if (goalLower.Contains("explain") || goalLower.Contains("describe"))
            return "explanation";

        return "general";
    }

    private List<string> FindCommonActions(List<Experience> experiences)
    {
        // Find actions that appear in all successful experiences
        List<List<string>> actionLists = experiences
            .Select(e => e.Plan.Steps.Select(s => s.Action).ToList())
            .ToList();

        if (!actionLists.Any())
            return new List<string>();

        List<string> commonActions = actionLists
            .SelectMany(actions => actions)
            .GroupBy(a => a)
            .Where(g => g.Count() >= actionLists.Count * 0.5) // Present in at least 50%
            .Select(g => g.Key)
            .ToList();

        return commonActions;
    }

    private string BuildPatternAnalysisPrompt(List<Experience> experiences)
    {
        string prompt = "Analyze the following successful experiences and identify common patterns:\n\n";

        foreach (Experience? exp in experiences.Take(5))
        {
            prompt += $"Goal: {exp.Goal}\n";
            prompt += $"Steps: {string.Join(" -> ", exp.Plan.Steps.Select(s => s.Action))}\n";
            prompt += $"Quality: {exp.Verification.QualityScore:P0}\n\n";
        }

        prompt += "What are the common successful patterns? List them briefly.";

        return prompt;
    }

    private List<string> ExtractInsights(string analysis)
    {
        // Simple extraction - in production use more sophisticated parsing
        List<string> insights = new List<string>();
        string[] lines = analysis.Split('\n');

        foreach (string line in lines)
        {
            if (line.Trim().StartsWith("-") || line.Trim().StartsWith("•"))
            {
                insights.Add(line.Trim().TrimStart('-', '•').Trim());
            }
        }

        return insights;
    }
}
