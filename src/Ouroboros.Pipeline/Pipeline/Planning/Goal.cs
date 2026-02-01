// <copyright file="Goal.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Planning;

using Ouroboros.Core.Monads;
using Ouroboros.Pipeline.Branches;

/// <summary>
/// Represents a decomposable goal with sub-goals and completion criteria.
/// Goals are immutable and composable following category theory principles.
/// </summary>
/// <param name="Id">Unique identifier for the goal.</param>
/// <param name="Description">Human-readable description of the goal.</param>
/// <param name="SubGoals">Child goals that must be completed.</param>
/// <param name="CompletionCriteria">Predicate determining goal completion.</param>
public sealed record Goal(
    Guid Id,
    string Description,
    IReadOnlyList<Goal> SubGoals,
    Func<PipelineBranch, bool> CompletionCriteria)
{
    /// <summary>
    /// Creates an atomic goal with no sub-goals.
    /// </summary>
    /// <param name="description">Goal description.</param>
    /// <param name="criteria">Completion criteria predicate.</param>
    /// <returns>A new atomic goal.</returns>
    public static Goal Atomic(string description, Func<PipelineBranch, bool> criteria)
    {
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(criteria);

        return new Goal(Guid.NewGuid(), description, [], criteria);
    }

    /// <summary>
    /// Creates an atomic goal that completes when marked externally.
    /// Default completion criteria always returns false (must be marked complete).
    /// </summary>
    /// <param name="description">Goal description.</param>
    /// <returns>A new atomic goal with default completion criteria.</returns>
    public static Goal Atomic(string description)
    {
        ArgumentNullException.ThrowIfNull(description);

        return Atomic(description, _ => false);
    }

    /// <summary>
    /// Creates a new goal with the specified sub-goals.
    /// </summary>
    /// <param name="subGoals">Sub-goals to add.</param>
    /// <returns>A new goal with sub-goals.</returns>
    public Goal WithSubGoals(params Goal[] subGoals)
    {
        ArgumentNullException.ThrowIfNull(subGoals);

        return this with { SubGoals = subGoals };
    }

    /// <summary>
    /// Creates a new goal with additional sub-goals appended.
    /// </summary>
    /// <param name="additionalSubGoals">Sub-goals to append.</param>
    /// <returns>A new goal with all sub-goals.</returns>
    public Goal AppendSubGoals(params Goal[] additionalSubGoals)
    {
        ArgumentNullException.ThrowIfNull(additionalSubGoals);

        return this with { SubGoals = SubGoals.Concat(additionalSubGoals).ToList() };
    }

    /// <summary>
    /// Determines if this goal is complete based on branch state.
    /// Composite goals are complete when all sub-goals are complete.
    /// </summary>
    /// <param name="branch">Current pipeline branch.</param>
    /// <returns>True if goal is complete.</returns>
    public bool IsComplete(PipelineBranch branch)
    {
        ArgumentNullException.ThrowIfNull(branch);

        return SubGoals.Count == 0
            ? CompletionCriteria(branch)
            : SubGoals.All(g => g.IsComplete(branch));
    }

    /// <summary>
    /// Returns incomplete sub-goals for the given branch.
    /// </summary>
    /// <param name="branch">Current pipeline branch.</param>
    /// <returns>Enumerable of incomplete sub-goals.</returns>
    public IEnumerable<Goal> GetIncompleteSubGoals(PipelineBranch branch)
    {
        ArgumentNullException.ThrowIfNull(branch);

        return SubGoals.Where(g => !g.IsComplete(branch));
    }

    /// <summary>
    /// Converts this goal to an Option, returning None if description is empty.
    /// </summary>
    /// <returns>Option containing this goal.</returns>
    public Option<Goal> ToOption() =>
        string.IsNullOrWhiteSpace(Description)
            ? Option<Goal>.None()
            : Option<Goal>.Some(this);

    /// <summary>
    /// Creates a goal with a custom completion criteria based on event type matching.
    /// </summary>
    /// <typeparam name="TEvent">The event type to match.</typeparam>
    /// <param name="description">Goal description.</param>
    /// <returns>A goal that completes when an event of type TEvent exists.</returns>
    public static Goal ForEventType<TEvent>(string description)
        where TEvent : PipelineEvent
    {
        return Atomic(description, branch => branch.Events.OfType<TEvent>().Any());
    }
}
