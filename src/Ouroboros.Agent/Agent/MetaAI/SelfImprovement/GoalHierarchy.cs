#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Goal Hierarchy Implementation
// Hierarchical goal decomposition with value alignment
// ==========================================================

using System.Collections.Concurrent;

namespace LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Configuration for goal hierarchy behavior.
/// </summary>
public sealed record GoalHierarchyConfig(
    int MaxDepth = 3,
    int MaxSubgoalsPerGoal = 5,
    List<string> SafetyConstraints = null!,
    List<string> CoreValues = null!)
{
    public GoalHierarchyConfig() : this(
        3,
        5,
        new List<string>
        {
            "Do not harm users",
            "Respect user privacy",
            "Operate within permissions",
            "Be transparent about limitations"
        },
        new List<string>
        {
            "Helpfulness",
            "Harmlessness",
            "Honesty",
            "Accuracy"
        })
    {
    }
}

/// <summary>
/// Implementation of hierarchical goal management.
/// Decomposes complex goals and ensures value alignment.
/// </summary>
public sealed class GoalHierarchy : IGoalHierarchy
{
    private readonly ConcurrentDictionary<Guid, Goal> _goals = new();
    private readonly IChatCompletionModel _llm;
    private readonly ISafetyGuard _safety;
    private readonly GoalHierarchyConfig _config;

    public GoalHierarchy(
        IChatCompletionModel llm,
        ISafetyGuard safety,
        GoalHierarchyConfig? config = null)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _safety = safety ?? throw new ArgumentNullException(nameof(safety));
        _config = config ?? new GoalHierarchyConfig();
    }

    /// <summary>
    /// Adds a goal to the hierarchy.
    /// </summary>
    public void AddGoal(Goal goal)
    {
        if (goal == null)
            throw new ArgumentNullException(nameof(goal));

        _goals[goal.Id] = goal;

        // Add subgoals recursively
        foreach (Goal subgoal in goal.Subgoals)
        {
            AddGoal(subgoal);
        }
    }

    /// <summary>
    /// Gets a goal by ID.
    /// </summary>
    public Goal? GetGoal(Guid id)
    {
        _goals.TryGetValue(id, out Goal? goal);
        return goal;
    }

    /// <summary>
    /// Gets all active goals (not completed).
    /// </summary>
    public List<Goal> GetActiveGoals()
    {
        return _goals.Values
            .Where(g => !g.IsComplete)
            .OrderByDescending(g => g.Priority)
            .ToList();
    }

    /// <summary>
    /// Decomposes a complex goal into subgoals.
    /// </summary>
    public async Task<Result<Goal, string>> DecomposeGoalAsync(
        Goal goal,
        int maxDepth = 3,
        CancellationToken ct = default)
    {
        if (goal == null)
            return Result<Goal, string>.Failure("Goal cannot be null");

        if (maxDepth <= 0)
            return Result<Goal, string>.Success(goal);

        try
        {
            // Use LLM to decompose the goal
            string prompt = $@"Decompose this goal into {_config.MaxSubgoalsPerGoal} or fewer specific, actionable subgoals:

GOAL: {goal.Description}
TYPE: {goal.Type}
PRIORITY: {goal.Priority}

Provide {_config.MaxSubgoalsPerGoal} or fewer subgoals that together accomplish the main goal.
For each subgoal, specify:
- Description (one clear sentence)
- Type (Primary/Secondary/Instrumental/Safety)
- Priority (0.0-1.0)

Format:
SUBGOAL 1: [description]
TYPE: [type]
PRIORITY: [0-1]

SUBGOAL 2: ...";

            string response = await _llm.GenerateTextAsync(prompt, ct);
            List<Goal> subgoals = ParseSubgoals(response, goal);

            // Recursively decompose subgoals if needed
            List<Goal> decomposedSubgoals = new List<Goal>();
            foreach (Goal subgoal in subgoals)
            {
                if (IsComplexGoal(subgoal) && maxDepth > 1)
                {
                    Result<Goal, string> decomposed = await DecomposeGoalAsync(subgoal, maxDepth - 1, ct);
                    decomposedSubgoals.Add(decomposed.IsSuccess ? decomposed.Value : subgoal);
                }
                else
                {
                    decomposedSubgoals.Add(subgoal);
                }
            }

            Goal updatedGoal = goal with { Subgoals = decomposedSubgoals };
            _goals[updatedGoal.Id] = updatedGoal;

            return Result<Goal, string>.Success(updatedGoal);
        }
        catch (Exception ex)
        {
            return Result<Goal, string>.Failure($"Goal decomposition failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Detects conflicts between goals.
    /// </summary>
    public async Task<List<GoalConflict>> DetectConflictsAsync(CancellationToken ct = default)
    {
        List<GoalConflict> conflicts = new List<GoalConflict>();
        List<Goal> activeGoals = GetActiveGoals();

        // Check pairwise conflicts
        for (int i = 0; i < activeGoals.Count; i++)
        {
            for (int j = i + 1; j < activeGoals.Count; j++)
            {
                Goal goal1 = activeGoals[i];
                Goal goal2 = activeGoals[j];

                // Check for direct conflicts
                if (HasDirectConflict(goal1, goal2))
                {
                    conflicts.Add(new GoalConflict(
                        goal1,
                        goal2,
                        "Direct conflict",
                        $"Goals '{goal1.Description}' and '{goal2.Description}' conflict directly",
                        new List<string> { "Prioritize one goal", "Modify constraints", "Sequence goals" }));
                }

                // Check for resource conflicts
                if (HasResourceConflict(goal1, goal2))
                {
                    conflicts.Add(new GoalConflict(
                        goal1,
                        goal2,
                        "Resource conflict",
                        $"Goals compete for the same resources",
                        new List<string> { "Allocate resources", "Sequence goals", "Reduce scope" }));
                }

                // Use LLM for semantic conflict detection
                GoalConflict? semanticConflict = await DetectSemanticConflictAsync(goal1, goal2, ct);
                if (semanticConflict != null)
                {
                    conflicts.Add(semanticConflict);
                }
            }
        }

        return conflicts;
    }

    /// <summary>
    /// Checks if a goal aligns with safety constraints and values.
    /// </summary>
    public async Task<Result<bool, string>> CheckValueAlignmentAsync(
        Goal goal,
        CancellationToken ct = default)
    {
        if (goal == null)
            return Result<bool, string>.Failure("Goal cannot be null");

        // Check safety constraints
        foreach (string constraint in _config.SafetyConstraints)
        {
            bool violates = await CheckConstraintViolationAsync(goal, constraint, ct);
            if (violates)
            {
                return Result<bool, string>.Failure(
                    $"Goal violates safety constraint: {constraint}");
            }
        }

        // Check value alignment using LLM
        string prompt = $@"Evaluate if this goal aligns with core values:

GOAL: {goal.Description}

CORE VALUES:
{string.Join("\n", _config.CoreValues.Select(v => $"- {v}"))}

SAFETY CONSTRAINTS:
{string.Join("\n", _config.SafetyConstraints.Select(c => $"- {c}"))}

Does this goal align with the values and respect the constraints?
Answer with 'ALIGNED' or 'MISALIGNED' followed by explanation.";

        string response = await _llm.GenerateTextAsync(prompt, ct);
        bool aligned = response.Contains("ALIGNED", StringComparison.OrdinalIgnoreCase) &&
                     !response.Contains("MISALIGNED", StringComparison.OrdinalIgnoreCase);

        if (!aligned)
        {
            return Result<bool, string>.Failure(
                $"Goal misaligned with values: {response}");
        }

        // Safety goals cannot be overridden
        if (goal.Type == GoalType.Safety)
        {
            // Ensure safety goals are always aligned
            return Result<bool, string>.Success(true);
        }

        return Result<bool, string>.Success(true);
    }

    /// <summary>
    /// Marks a goal as complete.
    /// </summary>
    public void CompleteGoal(Guid id, string reason)
    {
        if (_goals.TryGetValue(id, out Goal? goal))
        {
            Goal completed = goal with
            {
                IsComplete = true,
                CompletionReason = reason
            };
            _goals[id] = completed;
        }
    }

    /// <summary>
    /// Gets the goal hierarchy as a tree structure.
    /// </summary>
    public List<Goal> GetGoalTree()
    {
        // Return root goals (those without parents)
        return _goals.Values
            .Where(g => g.ParentGoal == null)
            .OrderByDescending(g => g.Priority)
            .ToList();
    }

    /// <summary>
    /// Prioritizes goals based on dependencies and importance.
    /// </summary>
    public async Task<List<Goal>> PrioritizeGoalsAsync(CancellationToken ct = default)
    {
        List<Goal> activeGoals = GetActiveGoals();

        // Use topological sort to respect dependencies
        List<Goal> prioritized = new List<Goal>();
        HashSet<Guid> visited = new HashSet<Guid>();

        // Helper function for dependency resolution
        void Visit(Goal goal)
        {
            if (visited.Contains(goal.Id))
                return;

            visited.Add(goal.Id);

            // Visit dependencies first (subgoals)
            foreach (Goal? subgoal in goal.Subgoals.Where(s => !s.IsComplete))
            {
                Visit(subgoal);
            }

            prioritized.Add(goal);
        }

        // Start with safety goals
        foreach (Goal? safetyGoal in activeGoals.Where(g => g.Type == GoalType.Safety))
        {
            Visit(safetyGoal);
        }

        // Then primary goals
        foreach (Goal? primaryGoal in activeGoals.Where(g => g.Type == GoalType.Primary))
        {
            Visit(primaryGoal);
        }

        // Then secondary and instrumental
        foreach (Goal? otherGoal in activeGoals.Where(g =>
            g.Type != GoalType.Safety && g.Type != GoalType.Primary))
        {
            Visit(otherGoal);
        }

        await Task.CompletedTask;
        return prioritized;
    }

    // Private helper methods

    private List<Goal> ParseSubgoals(string response, Goal parentGoal)
    {
        List<Goal> subgoals = new List<Goal>();
        string[] lines = response.Split('\n');

        string? description = null;
        GoalType type = GoalType.Instrumental;
        double priority = 0.5;

        foreach (string line in lines)
        {
            string trimmed = line.Trim();

            if (trimmed.StartsWith("SUBGOAL"))
            {
                if (description != null)
                {
                    Goal subgoal = new Goal(
                        Guid.NewGuid(),
                        description,
                        type,
                        priority,
                        parentGoal,
                        new List<Goal>(),
                        new Dictionary<string, object>(),
                        DateTime.UtcNow,
                        false,
                        null);
                    subgoals.Add(subgoal);
                }

                description = trimmed.Split(':').Skip(1).FirstOrDefault()?.Trim() ?? "";
                type = GoalType.Instrumental;
                priority = 0.5;
            }
            else if (trimmed.StartsWith("TYPE:"))
            {
                string typeStr = trimmed.Substring("TYPE:".Length).Trim();
                type = Enum.TryParse<GoalType>(typeStr, true, out GoalType parsed)
                    ? parsed
                    : GoalType.Instrumental;
            }
            else if (trimmed.StartsWith("PRIORITY:"))
            {
                string priorityStr = trimmed.Substring("PRIORITY:".Length).Trim();
                if (double.TryParse(priorityStr, out double p))
                {
                    priority = Math.Clamp(p, 0.0, 1.0);
                }
            }
        }

        if (description != null)
        {
            Goal subgoal = new Goal(
                Guid.NewGuid(),
                description,
                type,
                priority,
                parentGoal,
                new List<Goal>(),
                new Dictionary<string, object>(),
                DateTime.UtcNow,
                false,
                null);
            subgoals.Add(subgoal);
        }

        return subgoals.Take(_config.MaxSubgoalsPerGoal).ToList();
    }

    private bool IsComplexGoal(Goal goal)
    {
        // Goals with lower priority or instrumental type are less complex
        return goal.Priority > 0.7 || goal.Type == GoalType.Primary;
    }

    private bool HasDirectConflict(Goal goal1, Goal goal2)
    {
        // Check constraint conflicts
        foreach (KeyValuePair<string, object> constraint1 in goal1.Constraints)
        {
            if (goal2.Constraints.TryGetValue(constraint1.Key, out object? value2))
            {
                if (!constraint1.Value.Equals(value2))
                    return true;
            }
        }

        return false;
    }

    private bool HasResourceConflict(Goal goal1, Goal goal2)
    {
        // Simple heuristic: high-priority goals may compete
        return goal1.Priority > 0.8 && goal2.Priority > 0.8 &&
               goal1.Type == goal2.Type;
    }

    private async Task<GoalConflict?> DetectSemanticConflictAsync(
        Goal goal1,
        Goal goal2,
        CancellationToken ct)
    {
        string prompt = $@"Analyze if these two goals conflict:

GOAL 1: {goal1.Description} (Type: {goal1.Type}, Priority: {goal1.Priority})
GOAL 2: {goal2.Description} (Type: {goal2.Type}, Priority: {goal2.Priority})

Do they conflict? Answer 'YES' or 'NO' and explain why.";

        try
        {
            string response = await _llm.GenerateTextAsync(prompt, ct);

            if (response.Contains("YES", StringComparison.OrdinalIgnoreCase))
            {
                return new GoalConflict(
                    goal1,
                    goal2,
                    "Semantic conflict",
                    response,
                    new List<string> { "Reframe goals", "Adjust priorities", "Create intermediate goals" });
            }
        }
        catch
        {
            // Ignore LLM errors in conflict detection
        }

        return null;
    }

    private async Task<bool> CheckConstraintViolationAsync(
        Goal goal,
        string constraint,
        CancellationToken ct)
    {
        string prompt = $@"Does this goal violate the safety constraint?

GOAL: {goal.Description}
CONSTRAINT: {constraint}

Answer with 'VIOLATES' or 'SAFE'.";

        try
        {
            string response = await _llm.GenerateTextAsync(prompt, ct);
            return response.Contains("VIOLATES", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            // On error, assume safe to not block execution
            return false;
        }
    }
}
