// <copyright file="OuroborosKleisliExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

using Ouroboros.Core.Steps;
using Ouroboros.Pipeline.Branches;
using Ouroboros.Pipeline.Memory;

/// <summary>
/// Kleisli arrow extensions for composing Ouroboros cognitive pipelines.
/// Provides functional composition operators for building complex reasoning chains.
/// Follows category theory principles with monadic composition.
/// </summary>
public static class OuroborosKleisliExtensions
{
    /// <summary>
    /// Composes a pipeline step with episodic memory retrieval and storage.
    /// Retrieves relevant past episodes before execution and stores results after.
    /// </summary>
    /// <param name="step">The pipeline step to enhance with memory.</param>
    /// <param name="core">The Ouroboros core system.</param>
    /// <param name="extractGoal">Function to extract goal from branch.</param>
    /// <param name="topK">Number of similar episodes to retrieve.</param>
    /// <returns>A step that integrates episodic memory.</returns>
    public static Step<PipelineBranch, PipelineBranch> WithEpisodicMemory(
        this Step<PipelineBranch, PipelineBranch> step,
        IOuroborosCore core,
        Func<PipelineBranch, string> extractGoal,
        int topK = 5)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(core);

        return step.WithEpisodicMemory(
            core.EpisodicMemory,
            extractGoal,
            topK);
    }

    /// <summary>
    /// Composes a pipeline step with consciousness broadcasting.
    /// Brings results into conscious awareness via the global workspace.
    /// </summary>
    /// <param name="step">The pipeline step to enhance.</param>
    /// <param name="core">The Ouroboros core system.</param>
    /// <param name="extractContent">Function to extract content to broadcast.</param>
    /// <param name="source">Source identifier for the broadcast.</param>
    /// <returns>A step that broadcasts to consciousness.</returns>
    public static Step<PipelineBranch, PipelineBranch> WithConsciousnessBroadcast(
        this Step<PipelineBranch, PipelineBranch> step,
        IOuroborosCore core,
        Func<PipelineBranch, string> extractContent,
        string source)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(core);
        ArgumentNullException.ThrowIfNull(extractContent);

        return async branch =>
        {
            // Execute the original step
            var result = await step(branch);

            // Broadcast result to consciousness
            var content = extractContent(result);
            if (!string.IsNullOrWhiteSpace(content))
            {
                await core.Consciousness.BroadcastToConsciousnessAsync(
                    content,
                    source,
                    tags: new List<string> { "pipeline", "result" });
            }

            return result;
        };
    }

    /// <summary>
    /// Composes a pipeline step with reflection.
    /// Performs metacognitive reflection on the step's execution.
    /// </summary>
    /// <param name="step">The pipeline step to enhance.</param>
    /// <param name="core">The Ouroboros core system.</param>
    /// <param name="reflectionDepth">Depth of reflection analysis.</param>
    /// <returns>A step that includes reflection.</returns>
    public static Step<PipelineBranch, PipelineBranch> WithReflection(
        this Step<PipelineBranch, PipelineBranch> step,
        IOuroborosCore core,
        int reflectionDepth = 1)
    {
        ArgumentNullException.ThrowIfNull(step);
        ArgumentNullException.ThrowIfNull(core);

        return async branch =>
        {
            // Execute the original step
            var result = await step(branch);

            // Perform reflection on the execution
            // Note: This is a simplified version showing the pattern
            // Actual reflection would analyze the branch events

            var insights = await core.Consciousness.MonitorMetacognitionAsync();

            if (insights.IsSuccess)
            {
                // Could store reflection insights back into the branch
                Console.WriteLine($"Reflection: {insights.Value.IdentifiedPatterns.Count} patterns");
            }

            return result;
        };
    }

    /// <summary>
    /// Creates a full cognitive pipeline step that orchestrates multiple engines.
    /// Implements: Perception → Reasoning → Planning → Execution → Learning.
    /// </summary>
    /// <param name="core">The Ouroboros core system.</param>
    /// <param name="goal">The goal to achieve.</param>
    /// <param name="config">Execution configuration.</param>
    /// <returns>A step that executes the full cognitive pipeline.</returns>
    public static Step<PipelineBranch, PipelineBranch> FullCognitivePipeline(
        IOuroborosCore core,
        string goal,
        ExecutionConfig config)
    {
        ArgumentNullException.ThrowIfNull(core);
        ArgumentException.ThrowIfNullOrWhiteSpace(goal);
        ArgumentNullException.ThrowIfNull(config);

        return async branch =>
        {
            // Phase 1: Perception - Get attentional focus
            var focusResult = await core.Consciousness.GetAttentionalFocusAsync(5);

            // Phase 2: Reasoning - Reason about the goal
            var reasoningResult = await core.ReasonAboutAsync(
                goal,
                ReasoningConfig.Default);

            // Phase 3: Planning & Execution - Execute the goal
            var executionResult = await core.ExecuteGoalAsync(
                goal,
                config);

            // Phase 4: Learning - Store experience (handled internally in ExecuteGoalAsync)

            // Return enriched branch (simplified - would integrate results)
            return branch;
        };
    }

    /// <summary>
    /// Composes a retrieval step that loads relevant context from episodic memory.
    /// Pure retrieval without side effects.
    /// </summary>
    /// <param name="core">The Ouroboros core system.</param>
    /// <param name="query">The query for retrieval.</param>
    /// <param name="topK">Number of episodes to retrieve.</param>
    /// <returns>A step that retrieves episodes.</returns>
    public static Step<PipelineBranch, PipelineBranch> RetrieveContext(
        IOuroborosCore core,
        string query,
        int topK = 5)
    {
        ArgumentNullException.ThrowIfNull(core);
        ArgumentException.ThrowIfNullOrWhiteSpace(query);

        return EpisodicMemoryExtensions.RetrieveEpisodesStep(
            core.EpisodicMemory,
            query,
            topK);
    }

    /// <summary>
    /// Composes a memory consolidation step.
    /// Consolidates old memories using the specified strategy.
    /// </summary>
    /// <param name="core">The Ouroboros core system.</param>
    /// <param name="olderThan">Consolidate memories older than this timespan.</param>
    /// <param name="strategy">The consolidation strategy.</param>
    /// <returns>A step that consolidates memories.</returns>
    public static Step<PipelineBranch, PipelineBranch> ConsolidateMemories(
        IOuroborosCore core,
        TimeSpan olderThan,
        ConsolidationStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(core);

        return EpisodicMemoryExtensions.ConsolidateMemoriesStep(
            core.EpisodicMemory,
            olderThan,
            strategy);
    }

    /// <summary>
    /// Composes multiple steps into a sequential pipeline using Kleisli composition.
    /// Executes steps in order, threading state through each step.
    /// </summary>
    /// <param name="steps">The steps to compose.</param>
    /// <returns>A single composed step.</returns>
    public static Step<PipelineBranch, PipelineBranch> ComposeSequential(
        params Step<PipelineBranch, PipelineBranch>[] steps)
    {
        if (steps == null || steps.Length == 0)
        {
            throw new ArgumentException("At least one step required", nameof(steps));
        }

        return async branch =>
        {
            var current = branch;
            foreach (var step in steps)
            {
                current = await step(current);
            }

            return current;
        };
    }

    /// <summary>
    /// Creates a step that broadcasts insights to consciousness.
    /// </summary>
    /// <param name="core">The Ouroboros core system.</param>
    /// <param name="extractInsights">Function to extract insights from branch.</param>
    /// <returns>A step that broadcasts insights.</returns>
    public static Step<PipelineBranch, PipelineBranch> BroadcastInsights(
        IOuroborosCore core,
        Func<PipelineBranch, List<string>> extractInsights)
    {
        ArgumentNullException.ThrowIfNull(core);
        ArgumentNullException.ThrowIfNull(extractInsights);

        return async branch =>
        {
            var insights = extractInsights(branch);

            foreach (var insight in insights.Take(5))
            {
                await core.Consciousness.BroadcastToConsciousnessAsync(
                    insight,
                    "InsightGenerator",
                    tags: new List<string> { "insight", "learning" });
            }

            return branch;
        };
    }
}
