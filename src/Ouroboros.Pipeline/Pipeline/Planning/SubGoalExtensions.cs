// <copyright file="SubGoalExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Planning;

using Ouroboros.Providers;

/// <summary>
/// Extensions for converting between CollectiveMind SubGoal and Pipeline Goal types.
/// Enables integration of CollectiveMind's intelligent routing with existing goal hierarchy systems.
/// </summary>
public static class SubGoalExtensions
{
    /// <summary>
    /// Converts a Pipeline Goal to a CollectiveMind SubGoal with inferred routing metadata.
    /// </summary>
    /// <param name="goal">The Pipeline Goal to convert.</param>
    /// <param name="index">Index for ID generation (default: 0).</param>
    /// <returns>A SubGoal with routing metadata.</returns>
    public static SubGoal ToSubGoal(this Goal goal, int index = 0)
    {
        ArgumentNullException.ThrowIfNull(goal);
        return SubGoal.FromDescription(goal.Description, index);
    }

    /// <summary>
    /// Converts a CollectiveMind SubGoal to a Pipeline Goal.
    /// </summary>
    /// <param name="subGoal">The SubGoal to convert.</param>
    /// <returns>A Pipeline Goal.</returns>
    public static Goal ToPipelineGoal(this SubGoal subGoal)
    {
        ArgumentNullException.ThrowIfNull(subGoal);
        return Goal.Atomic(subGoal.Description);
    }

    /// <summary>
    /// Converts a list of Pipeline Goals to SubGoals with routing metadata.
    /// </summary>
    /// <param name="goals">The Pipeline Goals to convert.</param>
    /// <returns>List of SubGoals with routing metadata.</returns>
    public static IReadOnlyList<SubGoal> ToSubGoals(this IEnumerable<Goal> goals)
    {
        ArgumentNullException.ThrowIfNull(goals);
        return goals.Select((g, i) => g.ToSubGoal(i)).ToList();
    }

    /// <summary>
    /// Converts SubGoals back to Pipeline Goals.
    /// </summary>
    /// <param name="subGoals">The SubGoals to convert.</param>
    /// <returns>List of Pipeline Goals.</returns>
    public static IReadOnlyList<Goal> ToPipelineGoals(this IEnumerable<SubGoal> subGoals)
    {
        ArgumentNullException.ThrowIfNull(subGoals);
        return subGoals.Select(s => s.ToPipelineGoal()).ToList();
    }

    /// <summary>
    /// Creates a hierarchical Goal with SubGoals converted from the provided goals.
    /// </summary>
    /// <param name="parentDescription">Description for the parent goal.</param>
    /// <param name="subGoals">SubGoals to add as children.</param>
    /// <returns>A composite Pipeline Goal.</returns>
    public static Goal ToHierarchicalGoal(this IEnumerable<SubGoal> subGoals, string parentDescription)
    {
        ArgumentNullException.ThrowIfNull(subGoals);
        ArgumentNullException.ThrowIfNull(parentDescription);

        var parent = Goal.Atomic(parentDescription);
        var children = subGoals.Select(s => s.ToPipelineGoal()).ToArray();
        return parent.WithSubGoals(children);
    }

    /// <summary>
    /// Gets the recommended PathwayTier for a Goal based on its description.
    /// Uses the same inference logic as SubGoal.
    /// </summary>
    /// <param name="goal">The Goal to analyze.</param>
    /// <returns>Recommended PathwayTier for routing.</returns>
    public static PathwayTier GetRecommendedTier(this Goal goal)
    {
        ArgumentNullException.ThrowIfNull(goal);
        var subGoal = goal.ToSubGoal();
        return subGoal.PreferredTier;
    }

    /// <summary>
    /// Gets the inferred SubGoalType for a Goal based on its description.
    /// </summary>
    /// <param name="goal">The Goal to analyze.</param>
    /// <returns>Inferred SubGoalType.</returns>
    public static SubGoalType GetInferredType(this Goal goal)
    {
        ArgumentNullException.ThrowIfNull(goal);
        var subGoal = goal.ToSubGoal();
        return subGoal.Type;
    }

    /// <summary>
    /// Gets the inferred complexity for a Goal based on its description.
    /// </summary>
    /// <param name="goal">The Goal to analyze.</param>
    /// <returns>Inferred SubGoalComplexity.</returns>
    public static SubGoalComplexity GetInferredComplexity(this Goal goal)
    {
        ArgumentNullException.ThrowIfNull(goal);
        var subGoal = goal.ToSubGoal();
        return subGoal.Complexity;
    }
}
