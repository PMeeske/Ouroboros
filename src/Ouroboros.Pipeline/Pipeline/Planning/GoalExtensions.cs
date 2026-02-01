// <copyright file="GoalExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Planning;

using Ouroboros.Core.Monads;
using Ouroboros.Pipeline.Branches;

/// <summary>
/// Extension methods for Goal composition and manipulation.
/// </summary>
public static class GoalExtensions
{
    /// <summary>
    /// Binds a transformation function to a Goal wrapped in Result.
    /// </summary>
    /// <param name="result">The Result containing a Goal.</param>
    /// <param name="binder">The async transformation function.</param>
    /// <returns>The transformed Result.</returns>
    public static async Task<Result<Goal>> BindAsync(
        this Result<Goal> result,
        Func<Goal, Task<Result<Goal>>> binder)
    {
        ArgumentNullException.ThrowIfNull(binder);

        return result.IsSuccess
            ? await binder(result.Value)
            : Result<Goal>.Failure(result.Error);
    }

    /// <summary>
    /// Binds a transformation function to a Task containing Result of Goal.
    /// </summary>
    /// <param name="taskResult">The Task containing Result of Goal.</param>
    /// <param name="binder">The async transformation function.</param>
    /// <returns>The transformed Result.</returns>
    public static async Task<Result<Goal>> BindAsync(
        this Task<Result<Goal>> taskResult,
        Func<Goal, Task<Result<Goal>>> binder)
    {
        ArgumentNullException.ThrowIfNull(taskResult);
        ArgumentNullException.ThrowIfNull(binder);

        Result<Goal> result = await taskResult;
        return await result.BindAsync(binder);
    }

    /// <summary>
    /// Maps a transformation function over a Goal in a Result.
    /// </summary>
    /// <param name="result">The Result containing a Goal.</param>
    /// <param name="mapper">The transformation function.</param>
    /// <returns>A Result containing the transformed Goal.</returns>
    public static Result<Goal> Map(this Result<Goal> result, Func<Goal, Goal> mapper)
    {
        ArgumentNullException.ThrowIfNull(mapper);

        return result.IsSuccess
            ? Result<Goal>.Success(mapper(result.Value))
            : Result<Goal>.Failure(result.Error);
    }

    /// <summary>
    /// Flattens a goal hierarchy to a list of all goals (depth-first).
    /// </summary>
    /// <param name="goal">The root goal.</param>
    /// <returns>Enumerable of all goals in the hierarchy.</returns>
    public static IEnumerable<Goal> Flatten(this Goal goal)
    {
        ArgumentNullException.ThrowIfNull(goal);

        yield return goal;
        foreach (Goal subGoal in goal.SubGoals.SelectMany(Flatten))
        {
            yield return subGoal;
        }
    }

    /// <summary>
    /// Counts total goals in the hierarchy.
    /// </summary>
    /// <param name="goal">The root goal.</param>
    /// <returns>Total number of goals.</returns>
    public static int TotalCount(this Goal goal)
    {
        ArgumentNullException.ThrowIfNull(goal);

        return goal.Flatten().Count();
    }

    /// <summary>
    /// Counts completed goals for a given branch.
    /// </summary>
    /// <param name="goal">The root goal.</param>
    /// <param name="branch">The pipeline branch.</param>
    /// <returns>Number of completed goals.</returns>
    public static int CompletedCount(this Goal goal, PipelineBranch branch)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(branch);

        return goal.Flatten().Count(g => g.IsComplete(branch));
    }

    /// <summary>
    /// Calculates completion progress as a percentage (0.0 to 1.0).
    /// </summary>
    /// <param name="goal">The root goal.</param>
    /// <param name="branch">The pipeline branch.</param>
    /// <returns>Progress percentage.</returns>
    public static double Progress(this Goal goal, PipelineBranch branch)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(branch);

        int total = goal.TotalCount();
        return total == 0 ? 1.0 : (double)goal.CompletedCount(branch) / total;
    }

    /// <summary>
    /// Returns all leaf goals (goals with no sub-goals).
    /// </summary>
    /// <param name="goal">The root goal.</param>
    /// <returns>Enumerable of leaf goals.</returns>
    public static IEnumerable<Goal> GetLeafGoals(this Goal goal)
    {
        ArgumentNullException.ThrowIfNull(goal);

        return goal.Flatten().Where(g => g.SubGoals.Count == 0);
    }

    /// <summary>
    /// Returns the maximum depth of the goal hierarchy.
    /// </summary>
    /// <param name="goal">The root goal.</param>
    /// <returns>Maximum depth (1 for atomic goals).</returns>
    public static int MaxDepth(this Goal goal)
    {
        ArgumentNullException.ThrowIfNull(goal);

        return goal.SubGoals.Count == 0
            ? 1
            : 1 + goal.SubGoals.Max(g => g.MaxDepth());
    }

    /// <summary>
    /// Creates a string representation of the goal hierarchy.
    /// </summary>
    /// <param name="goal">The root goal.</param>
    /// <param name="indent">Indentation string (default: empty).</param>
    /// <returns>Formatted string representation.</returns>
    public static string ToTreeString(this Goal goal, string indent = "")
    {
        ArgumentNullException.ThrowIfNull(goal);

        List<string> lines = new List<string> { $"{indent}- {goal.Description}" };

        foreach (Goal subGoal in goal.SubGoals)
        {
            lines.Add(subGoal.ToTreeString(indent + "  "));
        }

        return string.Join(Environment.NewLine, lines);
    }

    /// <summary>
    /// Filters the goal hierarchy, keeping only goals that match the predicate.
    /// </summary>
    /// <param name="goal">The root goal.</param>
    /// <param name="predicate">Filter predicate.</param>
    /// <returns>Filtered goals or the original if no matches.</returns>
    public static Option<Goal> Filter(this Goal goal, Func<Goal, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(predicate);

        if (!predicate(goal))
        {
            return Option<Goal>.None();
        }

        Goal[] filteredSubGoals = goal.SubGoals
            .Select(g => g.Filter(predicate))
            .Where(o => o.HasValue)
            .Select(o => o.Value!)
            .ToArray();

        return Option<Goal>.Some(goal.WithSubGoals(filteredSubGoals));
    }
}
