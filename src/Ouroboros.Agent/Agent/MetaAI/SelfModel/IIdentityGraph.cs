#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Identity Graph Interface
// Phase 2: Agent identity with capabilities, resources, commitments, performance
// ==========================================================

namespace Ouroboros.Agent.MetaAI.SelfModel;

/// <summary>
/// Represents a resource tracked by the agent.
/// </summary>
public sealed record AgentResource(
    string Name,
    string Type,
    double Available,
    double Total,
    string Unit,
    DateTime LastUpdated,
    Dictionary<string, object> Metadata);

/// <summary>
/// Represents a commitment made by the agent.
/// </summary>
public sealed record AgentCommitment(
    Guid Id,
    string Description,
    DateTime Deadline,
    double Priority,
    CommitmentStatus Status,
    double ProgressPercent,
    List<string> Dependencies,
    Dictionary<string, object> Metadata,
    DateTime CreatedAt,
    DateTime? CompletedAt);

/// <summary>
/// Status of an agent commitment.
/// </summary>
public enum CommitmentStatus
{
    /// <summary>Commitment is planned but not started</summary>
    Planned,
    
    /// <summary>Commitment is in progress</summary>
    InProgress,
    
    /// <summary>Commitment is completed successfully</summary>
    Completed,
    
    /// <summary>Commitment failed</summary>
    Failed,
    
    /// <summary>Commitment was cancelled</summary>
    Cancelled,
    
    /// <summary>Commitment is at risk of missing deadline</summary>
    AtRisk
}

/// <summary>
/// Performance metrics for the agent.
/// </summary>
public sealed record AgentPerformance(
    double OverallSuccessRate,
    double AverageResponseTime,
    int TotalTasks,
    int SuccessfulTasks,
    int FailedTasks,
    Dictionary<string, double> CapabilitySuccessRates,
    Dictionary<string, double> ResourceUtilization,
    DateTime MeasurementPeriodStart,
    DateTime MeasurementPeriodEnd);

/// <summary>
/// Complete identity state of the agent.
/// </summary>
public sealed record AgentIdentityState(
    Guid AgentId,
    string Name,
    List<AgentCapability> Capabilities,
    List<AgentResource> Resources,
    List<AgentCommitment> Commitments,
    AgentPerformance Performance,
    DateTime StateTimestamp,
    Dictionary<string, object> Metadata);

/// <summary>
/// Interface for agent identity graph management.
/// Tracks capabilities, resources, commitments, and performance.
/// </summary>
public interface IIdentityGraph
{
    /// <summary>
    /// Gets the complete identity state of the agent.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Current agent identity state</returns>
    Task<AgentIdentityState> GetStateAsync(CancellationToken ct = default);

    /// <summary>
    /// Registers or updates a resource.
    /// </summary>
    /// <param name="resource">The resource to register</param>
    void RegisterResource(AgentResource resource);

    /// <summary>
    /// Gets current resource availability.
    /// </summary>
    /// <param name="resourceName">Name of the resource</param>
    /// <returns>Resource information if found</returns>
    AgentResource? GetResource(string resourceName);

    /// <summary>
    /// Creates a new commitment.
    /// </summary>
    /// <param name="description">Commitment description</param>
    /// <param name="deadline">Deadline for completion</param>
    /// <param name="priority">Priority level (0.0-1.0)</param>
    /// <param name="dependencies">List of dependency IDs</param>
    /// <returns>The created commitment</returns>
    AgentCommitment CreateCommitment(
        string description,
        DateTime deadline,
        double priority,
        List<string>? dependencies = null);

    /// <summary>
    /// Updates commitment status and progress.
    /// </summary>
    /// <param name="commitmentId">Commitment ID</param>
    /// <param name="status">New status</param>
    /// <param name="progressPercent">Progress percentage (0-100)</param>
    void UpdateCommitment(Guid commitmentId, CommitmentStatus status, double progressPercent);

    /// <summary>
    /// Gets all active commitments.
    /// </summary>
    /// <returns>List of active commitments ordered by priority</returns>
    List<AgentCommitment> GetActiveCommitments();

    /// <summary>
    /// Gets commitments at risk of missing deadline.
    /// </summary>
    /// <returns>List of at-risk commitments</returns>
    List<AgentCommitment> GetAtRiskCommitments();

    /// <summary>
    /// Updates performance metrics.
    /// </summary>
    /// <param name="taskResult">Result of a completed task</param>
    void RecordTaskResult(ExecutionResult taskResult);

    /// <summary>
    /// Gets performance summary for a specific time window.
    /// </summary>
    /// <param name="timeWindow">Time window to analyze</param>
    /// <returns>Performance metrics for the window</returns>
    AgentPerformance GetPerformanceSummary(TimeSpan timeWindow);

    /// <summary>
    /// Persists the current identity state.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    Task SaveStateAsync(CancellationToken ct = default);

    /// <summary>
    /// Loads identity state from persistence.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    Task LoadStateAsync(CancellationToken ct = default);
}
