#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Priority Modulator Interface
// Phase 3: Affective Dynamics - Threat/opportunity appraisal
// ==========================================================

namespace Ouroboros.Agent.MetaAI.Affect;

/// <summary>
/// Represents a task with priority modulation.
/// </summary>
public sealed record PrioritizedTask(
    Guid Id,
    string Name,
    string Description,
    double BasePriority,
    double ModulatedPriority,
    TaskAppraisal Appraisal,
    DateTime CreatedAt,
    DateTime? DueAt,
    TaskStatus Status,
    Dictionary<string, object> Metadata);

/// <summary>
/// Result of threat/opportunity appraisal.
/// </summary>
public sealed record TaskAppraisal(
    double ThreatLevel,
    double OpportunityScore,
    double UrgencyFactor,
    double RelevanceScore,
    string Rationale);

/// <summary>
/// Task status.
/// </summary>
public enum TaskStatus
{
    /// <summary>Task is pending execution</summary>
    Pending,
    
    /// <summary>Task is currently executing</summary>
    InProgress,
    
    /// <summary>Task completed successfully</summary>
    Completed,
    
    /// <summary>Task failed</summary>
    Failed,
    
    /// <summary>Task was cancelled</summary>
    Cancelled,
    
    /// <summary>Task is blocked by another task</summary>
    Blocked
}

/// <summary>
/// Interface for affect-driven priority modulation.
/// Adjusts task priorities based on affective state and threat/opportunity appraisal.
/// </summary>
public interface IPriorityModulator
{
    /// <summary>
    /// Adds a task to the priority queue.
    /// </summary>
    /// <param name="name">Task name</param>
    /// <param name="description">Task description</param>
    /// <param name="basePriority">Base priority (0.0 to 1.0)</param>
    /// <param name="dueAt">Optional due date</param>
    /// <param name="metadata">Optional metadata</param>
    /// <returns>The created task</returns>
    PrioritizedTask AddTask(
        string name,
        string description,
        double basePriority,
        DateTime? dueAt = null,
        Dictionary<string, object>? metadata = null);

    /// <summary>
    /// Appraises a task for threat/opportunity.
    /// </summary>
    /// <param name="taskId">Task ID</param>
    /// <param name="state">Current affective state</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Task appraisal</returns>
    Task<TaskAppraisal> AppraiseTaskAsync(
        Guid taskId,
        AffectiveState state,
        CancellationToken ct = default);

    /// <summary>
    /// Modulates all task priorities based on current affective state.
    /// </summary>
    /// <param name="state">Current affective state</param>
    void ModulatePriorities(AffectiveState state);

    /// <summary>
    /// Gets the next task to execute based on modulated priorities.
    /// </summary>
    /// <returns>Next task or null if queue is empty</returns>
    PrioritizedTask? GetNextTask();

    /// <summary>
    /// Gets all tasks in priority order.
    /// </summary>
    /// <param name="includeDone">Include completed/cancelled tasks</param>
    /// <returns>List of tasks</returns>
    List<PrioritizedTask> GetTasks(bool includeDone = false);

    /// <summary>
    /// Updates task status.
    /// </summary>
    /// <param name="taskId">Task ID</param>
    /// <param name="status">New status</param>
    void UpdateTaskStatus(Guid taskId, TaskStatus status);

    /// <summary>
    /// Removes a task from the queue.
    /// </summary>
    /// <param name="taskId">Task ID</param>
    void RemoveTask(Guid taskId);

    /// <summary>
    /// Gets queue statistics.
    /// </summary>
    /// <returns>Queue statistics</returns>
    QueueStatistics GetStatistics();

    /// <summary>
    /// Reorders tasks based on threat level (high threat = higher priority).
    /// </summary>
    void PrioritizeByThreat();

    /// <summary>
    /// Reorders tasks based on opportunity score (high opportunity = higher priority).
    /// </summary>
    void PrioritizeByOpportunity();
}

/// <summary>
/// Queue statistics.
/// </summary>
public sealed record QueueStatistics(
    int TotalTasks,
    int PendingTasks,
    int InProgressTasks,
    int CompletedTasks,
    int FailedTasks,
    double AverageBasePriority,
    double AverageModulatedPriority,
    double HighestThreat,
    double HighestOpportunity);
