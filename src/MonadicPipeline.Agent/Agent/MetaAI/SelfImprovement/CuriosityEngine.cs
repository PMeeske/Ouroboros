// ==========================================================
// Curiosity Engine Implementation
// Intrinsic motivation and autonomous exploration
// ==========================================================

using System.Collections.Concurrent;

namespace LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Implementation of curiosity-driven exploration.
/// </summary>
public sealed class CuriosityEngine : ICuriosityEngine
{
    private readonly IChatCompletionModel _llm;
    private readonly IMemoryStore _memory;
    private readonly ISkillRegistry _skills;
    private readonly ISafetyGuard _safety;
    private readonly CuriosityEngineConfig _config;
    private readonly ConcurrentBag<(Plan plan, double novelty, DateTime when)> _explorationHistory = new();
    private int _totalExplorations = 0;
    private int _sessionExplorations = 0;

    public CuriosityEngine(
        IChatCompletionModel llm,
        IMemoryStore memory,
        ISkillRegistry skills,
        ISafetyGuard safety,
        CuriosityEngineConfig? config = null)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        _safety = safety ?? throw new ArgumentNullException(nameof(safety));
        _config = config ?? new CuriosityEngineConfig();
    }

    /// <summary>
    /// Computes the novelty score for a potential action or plan.
    /// </summary>
    public async Task<double> ComputeNoveltyAsync(
        Plan plan,
        CancellationToken ct = default)
    {
        if (plan == null)
            return 0.0;

        try
        {
            // Check similarity to past experiences
            var query = new MemoryQuery(
                plan.Goal,
                null,
                MaxResults: 20,
                MinSimilarity: 0.0);

            var experiences = await _memory.RetrieveRelevantExperiencesAsync(query, ct);

            if (!experiences.Any())
                return 1.0; // Completely novel - no similar experiences

            // Calculate average similarity to past experiences
            var similarities = new List<double>();

            foreach (var exp in experiences)
            {
                var similarity = CalculateActionSimilarity(plan, exp.Plan);
                similarities.Add(similarity);
            }

            var avgSimilarity = similarities.Average();
            var novelty = 1.0 - avgSimilarity; // Higher novelty when less similar to past

            return Math.Clamp(novelty, 0.0, 1.0);
        }
        catch
        {
            return 0.5; // Default moderate novelty
        }
    }

    /// <summary>
    /// Generates an exploratory plan to learn something new.
    /// </summary>
    public async Task<Result<Plan, string>> GenerateExploratoryPlanAsync(
        CancellationToken ct = default)
    {
        try
        {
            // Identify unexplored areas
            var opportunities = await IdentifyExplorationOpportunitiesAsync(5, ct);

            if (!opportunities.Any())
            {
                return Result<Plan, string>.Failure("No exploration opportunities identified");
            }

            // Pick the most promising opportunity
            var bestOpportunity = opportunities.OrderByDescending(o => o.InformationGainEstimate).First();

            // Generate plan for exploration
            var prompt = $@"Create an exploratory plan for learning:

Exploration Goal: {bestOpportunity.Description}
Expected Information Gain: {bestOpportunity.InformationGainEstimate:P0}
Novelty: {bestOpportunity.NoveltyScore:P0}

Design a safe, structured exploration that will:
1. Maximize learning about this new area
2. Stay within safety boundaries
3. Build upon existing capabilities

Create 3-5 concrete steps for exploration.

Format:
STEP 1: [action]
EXPECTED: [what we'll learn]
CONFIDENCE: [0-1]

STEP 2: ...";

            var response = await _llm.GenerateTextAsync(prompt, ct);

            // Parse plan
            var steps = ParseExploratorySteps(response);

            // Safety check all steps
            if (_config.EnableSafeExploration)
            {
                foreach (var step in steps)
                {
                    var safetyCheck = _safety.CheckSafety(
                        step.Action,
                        step.Parameters,
                        PermissionLevel.UserDataWithConfirmation);

                    if (!safetyCheck.Safe)
                    {
                        return Result<Plan, string>.Failure(
                            $"Exploration plan failed safety check: {string.Join(", ", safetyCheck.Violations)}");
                    }
                }
            }

            var plan = new Plan(
                $"Explore: {bestOpportunity.Description}",
                steps,
                new Dictionary<string, double> 
                { 
                    ["exploratory"] = 1.0,
                    ["novelty"] = bestOpportunity.NoveltyScore 
                },
                DateTime.UtcNow);

            return Result<Plan, string>.Success(plan);
        }
        catch (Exception ex)
        {
            return Result<Plan, string>.Failure($"Exploratory plan generation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Decides whether to explore or exploit based on current state.
    /// </summary>
    public async Task<bool> ShouldExploreAsync(
        string? currentGoal = null,
        CancellationToken ct = default)
    {
        // Check session exploration limit
        if (_sessionExplorations >= _config.MaxExplorationPerSession)
            return false;

        // Calculate exploration probability using epsilon-greedy strategy
        var random = new Random();
        var explorationProbability = 1.0 - _config.ExploitationBias;

        // Increase exploration if we haven't explored much recently
        var recentExplorations = _explorationHistory
            .Where(e => e.when > DateTime.UtcNow.AddHours(-24))
            .Count();

        if (recentExplorations < 5)
        {
            explorationProbability += 0.2;
        }

        // If we have a goal, check if it's novel enough
        if (!string.IsNullOrWhiteSpace(currentGoal))
        {
            var goalPlan = new Plan(currentGoal, new List<PlanStep>(), new Dictionary<string, double>(), DateTime.UtcNow);
            var novelty = await ComputeNoveltyAsync(goalPlan, ct);

            if (novelty > _config.ExplorationThreshold)
            {
                // Goal is already novel, no need for additional exploration
                return false;
            }
        }

        return random.NextDouble() < explorationProbability;
    }

    /// <summary>
    /// Identifies novel exploration opportunities.
    /// </summary>
    public async Task<List<ExplorationOpportunity>> IdentifyExplorationOpportunitiesAsync(
        int maxOpportunities = 5,
        CancellationToken ct = default)
    {
        var opportunities = new List<ExplorationOpportunity>();

        try
        {
            // Analyze what hasn't been explored
            var allSkills = _skills.GetAllSkills();
            var experiences = await GetAllExperiences(ct);

            var prompt = $@"Identify unexplored areas for learning:

Current Skills ({allSkills.Count}):
{string.Join("\n", allSkills.Take(10).Select(s => $"- {s.Name}: {s.Description}"))}

Recent Experience Domains:
{string.Join("\n", experiences.Take(10).Select(e => $"- {e.Goal}"))}

Suggest {maxOpportunities} novel exploration areas that:
1. Differ from current capabilities
2. Could expand the agent's knowledge
3. Are safe to explore
4. Have potential for learning

Format each as:
OPPORTUNITY: [description]
NOVELTY: [0-1]
INFO_GAIN: [0-1]
";

            var response = await _llm.GenerateTextAsync(prompt, ct);

            // Parse opportunities
            var lines = response.Split('\n');
            string? description = null;
            double novelty = 0.7;
            double infoGain = 0.6;

            foreach (var line in lines)
            {
                var trimmed = line.Trim();

                if (trimmed.StartsWith("OPPORTUNITY:", StringComparison.OrdinalIgnoreCase))
                {
                    if (description != null)
                    {
                        opportunities.Add(new ExplorationOpportunity(
                            description,
                            novelty,
                            infoGain,
                            new List<string>(),
                            DateTime.UtcNow));
                    }

                    description = trimmed.Substring("OPPORTUNITY:".Length).Trim();
                    novelty = 0.7;
                    infoGain = 0.6;
                }
                else if (trimmed.StartsWith("NOVELTY:", StringComparison.OrdinalIgnoreCase))
                {
                    var novStr = trimmed.Substring("NOVELTY:".Length).Trim();
                    if (double.TryParse(novStr, out var nov))
                        novelty = Math.Clamp(nov, 0.0, 1.0);
                }
                else if (trimmed.StartsWith("INFO_GAIN:", StringComparison.OrdinalIgnoreCase))
                {
                    var gainStr = trimmed.Substring("INFO_GAIN:".Length).Trim();
                    if (double.TryParse(gainStr, out var gain))
                        infoGain = Math.Clamp(gain, 0.0, 1.0);
                }
            }

            if (description != null)
            {
                opportunities.Add(new ExplorationOpportunity(
                    description,
                    novelty,
                    infoGain,
                    new List<string>(),
                    DateTime.UtcNow));
            }
        }
        catch
        {
            // Return empty list on error
        }

        return opportunities.Take(maxOpportunities).ToList();
    }

    /// <summary>
    /// Estimates the information gain from exploring a particular area.
    /// </summary>
    public async Task<double> EstimateInformationGainAsync(
        string explorationDescription,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(explorationDescription))
            return 0.0;

        try
        {
            // Check how much we already know about this area
            var query = new MemoryQuery(
                explorationDescription,
                null,
                MaxResults: 10,
                MinSimilarity: 0.5);

            var experiences = await _memory.RetrieveRelevantExperiencesAsync(query, ct);

            // Less knowledge = higher potential information gain
            if (!experiences.Any())
                return 0.9; // High potential

            var coverage = Math.Min(experiences.Count / 10.0, 1.0);
            var informationGain = 1.0 - (coverage * 0.7); // Some gain even with knowledge

            return Math.Clamp(informationGain, 0.1, 1.0);
        }
        catch
        {
            return 0.5; // Default moderate gain
        }
    }

    /// <summary>
    /// Records the outcome of an exploration attempt.
    /// </summary>
    public void RecordExploration(Plan plan, ExecutionResult execution, double actualNovelty)
    {
        if (plan == null || execution == null)
            return;

        _explorationHistory.Add((plan, actualNovelty, DateTime.UtcNow));
        _totalExplorations++;
        _sessionExplorations++;
    }

    /// <summary>
    /// Gets exploration statistics.
    /// </summary>
    public Dictionary<string, double> GetExplorationStats()
    {
        var stats = new Dictionary<string, double>();

        stats["total_explorations"] = _totalExplorations;
        stats["session_explorations"] = _sessionExplorations;
        
        var recent = _explorationHistory
            .Where(e => e.when > DateTime.UtcNow.AddDays(-7))
            .ToList();

        stats["explorations_last_week"] = recent.Count;
        stats["avg_novelty"] = recent.Any() ? recent.Average(e => e.novelty) : 0.0;

        return stats;
    }

    // Private helper methods

    private double CalculateActionSimilarity(Plan plan1, Plan plan2)
    {
        if (plan1.Steps.Count == 0 || plan2.Steps.Count == 0)
            return 0.0;

        var actions1 = plan1.Steps.Select(s => s.Action).ToHashSet();
        var actions2 = plan2.Steps.Select(s => s.Action).ToHashSet();

        var intersection = actions1.Intersect(actions2).Count();
        var union = actions1.Union(actions2).Count();

        return union > 0 ? (double)intersection / union : 0.0;
    }

    private async Task<List<Experience>> GetAllExperiences(CancellationToken ct)
    {
        var query = new MemoryQuery(
            Goal: "",
            Context: null,
            MaxResults: 100,
            MinSimilarity: 0.0);

        return await _memory.RetrieveRelevantExperiencesAsync(query, ct);
    }

    private List<PlanStep> ParseExploratorySteps(string response)
    {
        var steps = new List<PlanStep>();
        var lines = response.Split('\n');

        string? currentAction = null;
        string? currentExpected = null;
        double currentConfidence = 0.7;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            if (trimmed.StartsWith("STEP"))
            {
                if (currentAction != null)
                {
                    steps.Add(new PlanStep(
                        currentAction,
                        new Dictionary<string, object> { ["expected_learning"] = currentExpected ?? "" },
                        currentExpected ?? "",
                        currentConfidence));
                }

                currentAction = trimmed.Split(':').Skip(1).FirstOrDefault()?.Trim() ?? "";
                currentExpected = "";
                currentConfidence = 0.7;
            }
            else if (trimmed.StartsWith("EXPECTED:", StringComparison.OrdinalIgnoreCase))
            {
                currentExpected = trimmed.Substring("EXPECTED:".Length).Trim();
            }
            else if (trimmed.StartsWith("CONFIDENCE:", StringComparison.OrdinalIgnoreCase))
            {
                var confStr = trimmed.Substring("CONFIDENCE:".Length).Trim();
                if (double.TryParse(confStr, out var conf))
                {
                    currentConfidence = Math.Clamp(conf, 0.0, 1.0);
                }
            }
        }

        if (currentAction != null)
        {
            steps.Add(new PlanStep(
                currentAction,
                new Dictionary<string, object> { ["expected_learning"] = currentExpected ?? "" },
                currentExpected ?? "",
                currentConfidence));
        }

        return steps;
    }
}
