#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Priority Modulator Implementation
// Phase 3: Affective Dynamics - Threat/opportunity appraisal
// ==========================================================

using System.Collections.Concurrent;

namespace Ouroboros.Agent.MetaAI.Affect;

/// <summary>
/// Implementation of affect-driven priority modulation.
/// </summary>
public sealed class PriorityModulator : IPriorityModulator
{
    private readonly ConcurrentDictionary<Guid, PrioritizedTask> _tasks = new();
    private readonly IChatCompletionModel? _llm;

    public PriorityModulator(IChatCompletionModel? llm = null)
    {
        _llm = llm;
    }

    public PrioritizedTask AddTask(
        string name,
        string description,
        double basePriority,
        DateTime? dueAt = null,
        Dictionary<string, object>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(description);

        basePriority = Math.Clamp(basePriority, 0.0, 1.0);

        var task = new PrioritizedTask(
            Guid.NewGuid(),
            name,
            description,
            basePriority,
            basePriority, // Initially, modulated = base
            new TaskAppraisal(0.0, 0.0, 0.0, 0.0, "Not yet appraised"),
            DateTime.UtcNow,
            dueAt,
            TaskStatus.Pending,
            metadata ?? new Dictionary<string, object>());

        _tasks[task.Id] = task;
        return task;
    }

    public async Task<TaskAppraisal> AppraiseTaskAsync(
        Guid taskId,
        AffectiveState state,
        CancellationToken ct = default)
    {
        if (!_tasks.TryGetValue(taskId, out var task))
        {
            return new TaskAppraisal(0.0, 0.0, 0.0, 0.0, "Task not found");
        }

        // Calculate urgency based on due date
        double urgencyFactor = 0.0;
        if (task.DueAt.HasValue)
        {
            TimeSpan timeLeft = task.DueAt.Value - DateTime.UtcNow;
            if (timeLeft.TotalHours <= 0)
            {
                urgencyFactor = 1.0; // Overdue
            }
            else if (timeLeft.TotalHours <= 24)
            {
                urgencyFactor = 0.8;
            }
            else if (timeLeft.TotalDays <= 7)
            {
                urgencyFactor = 0.5;
            }
            else
            {
                urgencyFactor = 0.2;
            }
        }

        // Calculate threat level based on stress and task characteristics
        double threatLevel = CalculateThreatLevel(task, state);

        // Calculate opportunity score based on curiosity and confidence
        double opportunityScore = CalculateOpportunityScore(task, state);

        // Calculate relevance based on base priority and current state
        double relevanceScore = task.BasePriority * (1.0 + state.Curiosity * 0.3);

        // Build rationale using LLM if available
        string rationale = await BuildRationaleAsync(task, state, threatLevel, opportunityScore, ct);

        var appraisal = new TaskAppraisal(
            threatLevel,
            opportunityScore,
            urgencyFactor,
            relevanceScore,
            rationale);

        // Update task with new appraisal
        _tasks[taskId] = task with { Appraisal = appraisal };

        return appraisal;
    }

    public void ModulatePriorities(AffectiveState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        foreach (var (taskId, task) in _tasks)
        {
            if (task.Status is TaskStatus.Completed or TaskStatus.Failed or TaskStatus.Cancelled)
            {
                continue; // Don't modulate finished tasks
            }

            double modulated = CalculateModulatedPriority(task, state);
            _tasks[taskId] = task with { ModulatedPriority = modulated };
        }
    }

    public PrioritizedTask? GetNextTask()
    {
        return _tasks.Values
            .Where(t => t.Status == TaskStatus.Pending)
            .OrderByDescending(t => t.ModulatedPriority)
            .ThenByDescending(t => t.Appraisal.ThreatLevel)
            .ThenBy(t => t.DueAt)
            .FirstOrDefault();
    }

    public List<PrioritizedTask> GetTasks(bool includeDone = false)
    {
        var tasks = _tasks.Values.AsEnumerable();
        if (!includeDone)
        {
            tasks = tasks.Where(t => t.Status is TaskStatus.Pending or TaskStatus.InProgress or TaskStatus.Blocked);
        }
        return tasks.OrderByDescending(t => t.ModulatedPriority).ToList();
    }

    public void UpdateTaskStatus(Guid taskId, TaskStatus status)
    {
        if (_tasks.TryGetValue(taskId, out var task))
        {
            _tasks[taskId] = task with { Status = status };
        }
    }

    public void RemoveTask(Guid taskId)
    {
        _tasks.TryRemove(taskId, out _);
    }

    public QueueStatistics GetStatistics()
    {
        var tasks = _tasks.Values.ToList();

        var pending = tasks.Where(t => t.Status == TaskStatus.Pending).ToList();
        var inProgress = tasks.Where(t => t.Status == TaskStatus.InProgress).ToList();
        var completed = tasks.Where(t => t.Status == TaskStatus.Completed).ToList();
        var failed = tasks.Where(t => t.Status == TaskStatus.Failed).ToList();

        return new QueueStatistics(
            tasks.Count,
            pending.Count,
            inProgress.Count,
            completed.Count,
            failed.Count,
            tasks.Count > 0 ? tasks.Average(t => t.BasePriority) : 0.0,
            tasks.Count > 0 ? tasks.Average(t => t.ModulatedPriority) : 0.0,
            tasks.Count > 0 ? tasks.Max(t => t.Appraisal.ThreatLevel) : 0.0,
            tasks.Count > 0 ? tasks.Max(t => t.Appraisal.OpportunityScore) : 0.0);
    }

    public void PrioritizeByThreat()
    {
        // Re-order by updating modulated priorities
        var tasks = _tasks.Values
            .Where(t => t.Status == TaskStatus.Pending)
            .OrderByDescending(t => t.Appraisal.ThreatLevel)
            .ToList();

        for (int i = 0; i < tasks.Count; i++)
        {
            double newPriority = 1.0 - ((double)i / Math.Max(tasks.Count, 1));
            _tasks[tasks[i].Id] = tasks[i] with { ModulatedPriority = newPriority };
        }
    }

    public void PrioritizeByOpportunity()
    {
        var tasks = _tasks.Values
            .Where(t => t.Status == TaskStatus.Pending)
            .OrderByDescending(t => t.Appraisal.OpportunityScore)
            .ToList();

        for (int i = 0; i < tasks.Count; i++)
        {
            double newPriority = 1.0 - ((double)i / Math.Max(tasks.Count, 1));
            _tasks[tasks[i].Id] = tasks[i] with { ModulatedPriority = newPriority };
        }
    }

    private double CalculateThreatLevel(PrioritizedTask task, AffectiveState state)
    {
        // Threat increases with:
        // - High stress
        // - Low confidence
        // - Time pressure
        // - High base priority (important tasks are more threatening if at risk)

        double stressContribution = state.Stress * 0.3;
        double confidenceContribution = (1.0 - state.Confidence) * 0.2;
        double priorityContribution = task.BasePriority * 0.3;

        double timeContribution = 0.0;
        if (task.DueAt.HasValue)
        {
            TimeSpan timeLeft = task.DueAt.Value - DateTime.UtcNow;
            if (timeLeft.TotalHours <= 0)
            {
                timeContribution = 0.3; // Overdue
            }
            else if (timeLeft.TotalHours <= 24)
            {
                timeContribution = 0.2;
            }
        }

        return Math.Clamp(
            stressContribution + confidenceContribution + priorityContribution + timeContribution,
            0.0, 1.0);
    }

    private double CalculateOpportunityScore(PrioritizedTask task, AffectiveState state)
    {
        // Opportunity increases with:
        // - High curiosity
        // - High confidence
        // - Positive valence
        // - Novel tasks

        double curiosityContribution = state.Curiosity * 0.4;
        double confidenceContribution = state.Confidence * 0.3;
        double valenceContribution = Math.Max(0, state.Valence) * 0.2;

        // Check for "learning" or "explore" keywords in task
        bool isExploratoryTask = task.Name.Contains("learn", StringComparison.OrdinalIgnoreCase) ||
                                 task.Description.Contains("explore", StringComparison.OrdinalIgnoreCase) ||
                                 task.Description.Contains("research", StringComparison.OrdinalIgnoreCase);

        double explorationBonus = isExploratoryTask ? 0.2 : 0.0;

        return Math.Clamp(
            curiosityContribution + confidenceContribution + valenceContribution + explorationBonus,
            0.0, 1.0);
    }

    private double CalculateModulatedPriority(PrioritizedTask task, AffectiveState state)
    {
        double basePriority = task.BasePriority;

        // Modulation factors based on affective state:
        // - High stress: prioritize urgent/important tasks
        // - High curiosity: boost novel/exploratory tasks
        // - Low confidence: favor familiar, lower-risk tasks
        // - Positive valence: more open to challenging tasks

        double stressModifier = 1.0;
        if (state.Stress > 0.6)
        {
            // Under high stress, boost high-priority tasks, suppress low-priority
            stressModifier = basePriority > 0.5 ? 1.2 : 0.8;
        }

        double curiosityModifier = 1.0;
        if (state.Curiosity > 0.5)
        {
            // High curiosity boosts novel tasks
            if (task.Description.Contains("new", StringComparison.OrdinalIgnoreCase) ||
                task.Description.Contains("explore", StringComparison.OrdinalIgnoreCase))
            {
                curiosityModifier = 1.2;
            }
        }

        double confidenceModifier = 1.0;
        if (state.Confidence < 0.3)
        {
            // Low confidence: prefer familiar, lower-risk tasks
            confidenceModifier = basePriority > 0.7 ? 0.9 : 1.1;
        }

        double valenceModifier = 1.0 + (state.Valence * 0.1); // Small boost for positive valence

        double modulatedPriority = basePriority * stressModifier * curiosityModifier * confidenceModifier * valenceModifier;

        // Add threat/opportunity from appraisal
        modulatedPriority += (task.Appraisal.ThreatLevel * 0.2) + (task.Appraisal.OpportunityScore * 0.1);

        return Math.Clamp(modulatedPriority, 0.0, 1.0);
    }

    private async Task<string> BuildRationaleAsync(
        PrioritizedTask task,
        AffectiveState state,
        double threatLevel,
        double opportunityScore,
        CancellationToken ct)
    {
        if (_llm == null)
        {
            // Build simple rationale without LLM
            var parts = new List<string>();

            if (threatLevel > 0.6)
            {
                parts.Add($"High threat ({threatLevel:P0})");
            }
            if (opportunityScore > 0.5)
            {
                parts.Add($"Good opportunity ({opportunityScore:P0})");
            }
            if (state.Stress > 0.7)
            {
                parts.Add("Agent under stress");
            }
            if (state.Curiosity > 0.6)
            {
                parts.Add("Agent is curious");
            }

            return parts.Count > 0
                ? string.Join("; ", parts)
                : "Standard priority assessment";
        }

        try
        {
            string prompt = $"""
                Provide a brief (1 sentence) rationale for task appraisal:
                Task: {task.Name}
                Threat Level: {threatLevel:P0}
                Opportunity: {opportunityScore:P0}
                Agent Stress: {state.Stress:P0}
                Agent Confidence: {state.Confidence:P0}
                """;

            string? response = await _llm.GenerateTextAsync(prompt, ct);

            // Handle null or empty LLM response with fallback
            if (string.IsNullOrWhiteSpace(response))
            {
                return $"Threat: {threatLevel:P0}, Opportunity: {opportunityScore:P0}";
            }

            return response;
        }
        catch
        {
            return $"Threat: {threatLevel:P0}, Opportunity: {opportunityScore:P0}";
        }
    }
}
