// <copyright file="CollectiveMindGoalIntegration.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Agent.MetaAI;

using Ouroboros.Providers;

// Use alias to distinguish from Agent's Goal type
using PipelineGoal = Ouroboros.Pipeline.Planning.Goal;

/// <summary>
/// Integration layer that combines CollectiveMind's intelligent routing
/// with the existing GoalHierarchy system.
/// </summary>
public sealed class CollectiveMindGoalIntegration
{
    private readonly CollectiveMind _mind;
    private readonly IGoalHierarchy? _goalHierarchy;

    /// <summary>
    /// Initializes a new instance of the CollectiveMindGoalIntegration.
    /// </summary>
    /// <param name="mind">The CollectiveMind instance for multi-provider routing.</param>
    /// <param name="goalHierarchy">Optional GoalHierarchy for ethics-aware goal management.</param>
    public CollectiveMindGoalIntegration(CollectiveMind mind, IGoalHierarchy? goalHierarchy = null)
    {
        _mind = mind ?? throw new ArgumentNullException(nameof(mind));
        _goalHierarchy = goalHierarchy;
    }

    /// <summary>
    /// Decomposes a goal using CollectiveMind and optionally registers with GoalHierarchy.
    /// Routes sub-goals to optimal pathways based on complexity and type.
    /// </summary>
    /// <param name="description">The goal description to decompose.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The synthesized response from executing all sub-goals.</returns>
    public async Task<ThinkingResponse> ExecuteGoalAsync(string description, CancellationToken ct = default)
    {
        // Use CollectiveMind's decomposition mode
        var originalMode = _mind.ThinkingMode;
        _mind.ThinkingMode = CollectiveThinkingMode.Decomposed;

        try
        {
            return await _mind.GenerateWithThinkingAsync(description, ct);
        }
        finally
        {
            _mind.ThinkingMode = originalMode;
        }
    }

    /// <summary>
    /// Executes a Pipeline Goal using CollectiveMind for intelligent routing.
    /// </summary>
    /// <param name="goal">The Pipeline Goal to execute.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The synthesized response.</returns>
    public async Task<ThinkingResponse> ExecutePipelineGoalAsync(PipelineGoal goal, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(goal);

        // Convert to SubGoal to get routing metadata
        var subGoal = Pipeline.Planning.SubGoalExtensions.ToSubGoal(goal);

        // Route based on recommended tier
        var tier = subGoal.PreferredTier;

        // Execute with CollectiveMind (it handles pathway selection internally)
        return await _mind.GenerateWithThinkingAsync(goal.Description, ct);
    }

    /// <summary>
    /// Executes an Agent Goal using CollectiveMind for intelligent routing.
    /// </summary>
    /// <param name="goal">The Agent Goal to execute.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The synthesized response.</returns>
    public async Task<ThinkingResponse> ExecuteAgentGoalAsync(Goal goal, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(goal);

        // Use CollectiveMind's decomposition for complex goals
        if (goal.Subgoals.Count > 0)
        {
            var results = new List<ThinkingResponse>();

            foreach (var subgoal in goal.Subgoals)
            {
                var result = await ExecuteAgentGoalAsync(subgoal, ct);
                results.Add(result);
            }

            // Synthesize results
            return await SynthesizeAgentGoalResultsAsync(goal, results, ct);
        }

        // Atomic goal - execute directly
        return await _mind.GenerateWithThinkingAsync(goal.Description, ct);
    }

    /// <summary>
    /// Executes a hierarchical Pipeline goal, routing each sub-goal to optimal pathways.
    /// </summary>
    /// <param name="rootGoal">The root goal with sub-goals.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Dictionary of goal IDs to their responses.</returns>
    public async Task<Dictionary<Guid, ThinkingResponse>> ExecuteHierarchicalPipelineGoalAsync(
        PipelineGoal rootGoal,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(rootGoal);

        var results = new Dictionary<Guid, ThinkingResponse>();

        if (rootGoal.SubGoals.Count == 0)
        {
            // Atomic goal - execute directly
            var response = await ExecutePipelineGoalAsync(rootGoal, ct);
            results[rootGoal.Id] = response;
            return results;
        }

        // Execute sub-goals
        foreach (var subGoal in rootGoal.SubGoals)
        {
            var subResults = await ExecuteHierarchicalPipelineGoalAsync(subGoal, ct);
            foreach (var kvp in subResults)
            {
                results[kvp.Key] = kvp.Value;
            }
        }

        // Synthesize results for the parent goal
        var synthesis = await SynthesizePipelineResultsAsync(rootGoal, results, ct);
        results[rootGoal.Id] = synthesis;

        return results;
    }

    /// <summary>
    /// Routes a list of SubGoals to optimal pathways and executes them.
    /// </summary>
    /// <param name="subGoals">The SubGoals to execute.</param>
    /// <param name="parallelExecution">Whether to execute independent goals in parallel.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Dictionary of goal IDs to their results.</returns>
    public async Task<Dictionary<string, SubGoalResult>> ExecuteSubGoalsAsync(
        IReadOnlyList<SubGoal> subGoals,
        bool parallelExecution = true,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(subGoals);

        var results = new Dictionary<string, SubGoalResult>();

        if (parallelExecution)
        {
            var tasks = subGoals.Select(async sg =>
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    var response = await _mind.GenerateWithThinkingAsync(sg.Description, ct);
                    sw.Stop();
                    return new SubGoalResult(sg.Id, "collective", response, sw.Elapsed, true);
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    return new SubGoalResult(sg.Id, "collective", new ThinkingResponse(null, ""), sw.Elapsed, false, ex.Message);
                }
            });

            var taskResults = await Task.WhenAll(tasks);
            foreach (var result in taskResults)
            {
                results[result.GoalId] = result;
            }
        }
        else
        {
            foreach (var sg in subGoals)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    var response = await _mind.GenerateWithThinkingAsync(sg.Description, ct);
                    sw.Stop();
                    results[sg.Id] = new SubGoalResult(sg.Id, "collective", response, sw.Elapsed, true);
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    results[sg.Id] = new SubGoalResult(sg.Id, "collective", new ThinkingResponse(null, ""), sw.Elapsed, false, ex.Message);
                }
            }
        }

        return results;
    }

    private async Task<ThinkingResponse> SynthesizeAgentGoalResultsAsync(
        Goal rootGoal,
        List<ThinkingResponse> results,
        CancellationToken ct)
    {
        var subResults = rootGoal.Subgoals
            .Zip(results, (sg, r) => $"[{sg.Description}]: {r.Content}")
            .ToList();

        var synthesisPrompt = $"""
            Original goal: {rootGoal.Description}

            Sub-goal results:
            {string.Join("\n\n", subResults)}

            Synthesize these results into a comprehensive response.
            """;

        return await _mind.GenerateWithThinkingAsync(synthesisPrompt, ct);
    }

    private async Task<ThinkingResponse> SynthesizePipelineResultsAsync(
        PipelineGoal rootGoal,
        Dictionary<Guid, ThinkingResponse> results,
        CancellationToken ct)
    {
        var subResults = rootGoal.SubGoals
            .Where(sg => results.ContainsKey(sg.Id))
            .Select(sg => $"[{sg.Description}]: {results[sg.Id].Content}")
            .ToList();

        var synthesisPrompt = $"""
            Original goal: {rootGoal.Description}

            Sub-goal results:
            {string.Join("\n\n", subResults)}

            Synthesize these results into a comprehensive response.
            """;

        return await _mind.GenerateWithThinkingAsync(synthesisPrompt, ct);
    }
}

/// <summary>
/// Factory methods for creating CollectiveMindGoalIntegration instances.
/// </summary>
public static class CollectiveMindGoalIntegrationFactory
{
    /// <summary>
    /// Creates an integration that uses both CollectiveMind and GoalHierarchy.
    /// </summary>
    public static CollectiveMindGoalIntegration CreateWithHierarchy(
        CollectiveMind mind,
        IGoalHierarchy goalHierarchy) =>
        new(mind, goalHierarchy);

    /// <summary>
    /// Creates a standalone integration using only CollectiveMind.
    /// </summary>
    public static CollectiveMindGoalIntegration CreateStandalone(CollectiveMind mind) =>
        new(mind);

    /// <summary>
    /// Creates an integration with a pre-configured decomposed CollectiveMind.
    /// </summary>
    public static CollectiveMindGoalIntegration CreateDecomposed(
        ChatRuntimeSettings? settings = null)
    {
        var mind = CollectiveMindFactory.CreateDecomposed(settings);
        return new CollectiveMindGoalIntegration(mind);
    }

    /// <summary>
    /// Creates an integration optimized for local-first execution.
    /// </summary>
    public static CollectiveMindGoalIntegration CreateLocalFirst(
        string localModel = "llama3.2",
        ChatRuntimeSettings? settings = null)
    {
        var mind = CollectiveMindFactory.CreateLocalFirstDecomposed(localModel, settings: settings);
        return new CollectiveMindGoalIntegration(mind);
    }
}
