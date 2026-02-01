// <copyright file="HierarchicalGoalPlanner.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Planning;

using Ouroboros.Core.Monads;
using Ouroboros.Core.Steps;
using Ouroboros.Pipeline.Branches;
using Ouroboros.Providers;

/// <summary>
/// Executes goals hierarchically, selecting the appropriate pipeline steps for each sub-goal.
/// Implements functional composition patterns for goal execution.
/// </summary>
public static class HierarchicalGoalPlanner
{
    /// <summary>
    /// Creates a step that executes a goal hierarchy using the provided step selector.
    /// </summary>
    /// <param name="goal">The goal to execute.</param>
    /// <param name="stepSelector">Function that maps goals to executable steps.</param>
    /// <returns>A step that transforms the branch by executing the goal.</returns>
    public static Step<PipelineBranch, PipelineBranch> ExecuteGoalArrow(
        Goal goal,
        Func<Goal, Step<PipelineBranch, PipelineBranch>> stepSelector)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(stepSelector);

        return async branch =>
        {
            if (goal.IsComplete(branch))
            {
                return branch;
            }

            if (goal.SubGoals.Count == 0)
            {
                Step<PipelineBranch, PipelineBranch> step = stepSelector(goal);
                return await step(branch);
            }

            PipelineBranch current = branch;
            foreach (Goal subGoal in goal.GetIncompleteSubGoals(current))
            {
                current = await ExecuteGoalArrow(subGoal, stepSelector)(current);
            }

            return current;
        };
    }

    /// <summary>
    /// Creates a step that executes a goal with Result-based error handling.
    /// </summary>
    /// <param name="goal">The goal to execute.</param>
    /// <param name="stepSelector">Function that maps goals to Result-producing steps.</param>
    /// <returns>A step producing Result with the transformed branch.</returns>
    public static Step<PipelineBranch, Result<PipelineBranch>> ExecuteGoalSafeArrow(
        Goal goal,
        Func<Goal, Step<PipelineBranch, Result<PipelineBranch>>> stepSelector)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(stepSelector);

        return async branch =>
        {
            if (goal.IsComplete(branch))
            {
                return Result<PipelineBranch>.Success(branch);
            }

            try
            {
                if (goal.SubGoals.Count == 0)
                {
                    Step<PipelineBranch, Result<PipelineBranch>> step = stepSelector(goal);
                    return await step(branch);
                }

                Result<PipelineBranch> current = Result<PipelineBranch>.Success(branch);

                foreach (Goal subGoal in goal.GetIncompleteSubGoals(branch))
                {
                    if (current.IsFailure)
                    {
                        return current;
                    }

                    current = await ExecuteGoalSafeArrow(subGoal, stepSelector)(current.Value);
                }

                return current;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Result<PipelineBranch>.Failure($"Goal execution failed for '{goal.Description}': {ex.Message}");
            }
        };
    }

    /// <summary>
    /// Creates a composed pipeline that decomposes and executes a goal.
    /// </summary>
    /// <param name="llm">Language model for decomposition.</param>
    /// <param name="rootGoal">The root goal to process.</param>
    /// <param name="stepSelector">Function mapping goals to steps.</param>
    /// <param name="maxDepth">Maximum decomposition depth.</param>
    /// <returns>A complete decompose-and-execute step.</returns>
    public static Step<PipelineBranch, Result<PipelineBranch>> DecomposeAndExecuteArrow(
        ToolAwareChatModel llm,
        Goal rootGoal,
        Func<Goal, Step<PipelineBranch, Result<PipelineBranch>>> stepSelector,
        int maxDepth = 3)
    {
        ArgumentNullException.ThrowIfNull(llm);
        ArgumentNullException.ThrowIfNull(rootGoal);
        ArgumentNullException.ThrowIfNull(stepSelector);

        return async branch =>
        {
            Result<Goal> decomposedResult = await GoalDecomposer.DecomposeRecursiveArrow(llm, rootGoal, maxDepth)(branch);

            if (decomposedResult.IsFailure)
            {
                return Result<PipelineBranch>.Failure(decomposedResult.Error);
            }

            return await ExecuteGoalSafeArrow(decomposedResult.Value, stepSelector)(branch);
        };
    }

    /// <summary>
    /// Creates a step that executes goals in parallel when possible.
    /// </summary>
    /// <param name="goal">The goal with independent sub-goals.</param>
    /// <param name="stepSelector">Function mapping goals to steps.</param>
    /// <param name="maxParallelism">Maximum number of parallel executions.</param>
    /// <returns>A step that executes independent sub-goals in parallel.</returns>
    public static Step<PipelineBranch, Result<PipelineBranch>> ExecuteGoalParallelArrow(
        Goal goal,
        Func<Goal, Step<PipelineBranch, Result<PipelineBranch>>> stepSelector,
        int maxParallelism = 4)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(stepSelector);

        return async branch =>
        {
            if (goal.IsComplete(branch))
            {
                return Result<PipelineBranch>.Success(branch);
            }

            if (goal.SubGoals.Count == 0)
            {
                return await stepSelector(goal)(branch);
            }

            try
            {
                List<Goal> incompleteGoals = goal.GetIncompleteSubGoals(branch).ToList();
                SemaphoreSlim semaphore = new SemaphoreSlim(maxParallelism);

                IEnumerable<Task<Result<PipelineBranch>>> tasks = incompleteGoals.Select(async subGoal =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        return await ExecuteGoalSafeArrow(subGoal, stepSelector)(branch);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                });

                Result<PipelineBranch>[] results = await Task.WhenAll(tasks);

                // Check for any failures
                List<Result<PipelineBranch>> failures = results.Where(r => r.IsFailure).ToList();
                if (failures.Count != 0)
                {
                    string errors = string.Join("; ", failures.Select(f => f.Error));
                    return Result<PipelineBranch>.Failure($"Parallel goal execution failed: {errors}");
                }

                // Return the last successful branch (contains accumulated state)
                Result<PipelineBranch>? lastSuccess = results.LastOrDefault(r => r.IsSuccess);
                return lastSuccess.HasValue && lastSuccess.Value.IsSuccess
                    ? lastSuccess.Value
                    : Result<PipelineBranch>.Success(branch);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Result<PipelineBranch>.Failure($"Parallel execution failed: {ex.Message}");
            }
        };
    }
}
