#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Goal Hierarchy Interface
// Hierarchical goal decomposition and value alignment
// ==========================================================

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Types of goals in the hierarchy.
/// </summary>
public enum GoalType
{
    /// <summary>Primary goal - the main objective</summary>
    Primary,

    /// <summary>Secondary goal - supporting objective</summary>
    Secondary,

    /// <summary>Instrumental goal - means to achieve other goals</summary>
    Instrumental,

    /// <summary>Safety goal - constraint or boundary condition</summary>
    Safety
}

/// <summary>
/// Represents a goal with hierarchical structure.
/// </summary>
public sealed record Goal(
    Guid Id,
    string Description,
    GoalType Type,
    double Priority,
    Goal? ParentGoal,
    List<Goal> Subgoals,
    Dictionary<string, object> Constraints,
    DateTime CreatedAt,
    bool IsComplete,
    string? CompletionReason)
{
    /// <summary>
    /// Creates a new goal with default values.
    /// </summary>
    public Goal(string description, GoalType type, double priority)
        : this(
            Guid.NewGuid(),
            description,
            type,
            priority,
            ParentGoal: null,
            Subgoals: new List<Goal>(),
            Constraints: new Dictionary<string, object>(),
            DateTime.UtcNow,
            IsComplete: false,
            CompletionReason: null)
    {
    }
}

/// <summary>
/// Result of goal conflict detection.
/// </summary>
public sealed record GoalConflict(
    Goal Goal1,
    Goal Goal2,
    string ConflictType,
    string Description,
    List<string> SuggestedResolutions);

/// <summary>
/// Interface for hierarchical goal management and value alignment.
/// </summary>
public interface IGoalHierarchy
{
    /// <summary>
    /// Adds a goal to the hierarchy.
    /// </summary>
    /// <param name="goal">The goal to add</param>
    void AddGoal(Goal goal);

    /// <summary>
    /// Gets a goal by ID.
    /// </summary>
    /// <param name="id">The goal ID</param>
    /// <returns>The goal if found, null otherwise</returns>
    Goal? GetGoal(Guid id);

    /// <summary>
    /// Gets all active goals (not completed).
    /// </summary>
    /// <returns>List of active goals</returns>
    List<Goal> GetActiveGoals();

    /// <summary>
    /// Decomposes a complex goal into subgoals.
    /// </summary>
    /// <param name="goal">The goal to decompose</param>
    /// <param name="maxDepth">Maximum decomposition depth</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The goal with populated subgoals</returns>
    Task<Result<Goal, string>> DecomposeGoalAsync(
        Goal goal,
        int maxDepth = 3,
        CancellationToken ct = default);

    /// <summary>
    /// Detects conflicts between goals.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of detected conflicts</returns>
    Task<List<GoalConflict>> DetectConflictsAsync(CancellationToken ct = default);

    /// <summary>
    /// Checks if a goal aligns with safety constraints and values.
    /// </summary>
    /// <param name="goal">The goal to check</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if aligned, false otherwise with reason</returns>
    Task<Result<bool, string>> CheckValueAlignmentAsync(
        Goal goal,
        CancellationToken ct = default);

    /// <summary>
    /// Marks a goal as complete.
    /// </summary>
    /// <param name="id">The goal ID</param>
    /// <param name="reason">Completion reason</param>
    void CompleteGoal(Guid id, string reason);

    /// <summary>
    /// Gets the goal hierarchy as a tree structure.
    /// </summary>
    /// <returns>Root goals with their subgoal trees</returns>
    List<Goal> GetGoalTree();

    /// <summary>
    /// Prioritizes goals based on dependencies and importance.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Ordered list of goals to pursue</returns>
    Task<List<Goal>> PrioritizeGoalsAsync(CancellationToken ct = default);
}
