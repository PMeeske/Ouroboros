#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Capability Registry Interface
// Agent self-model and capability tracking
// ==========================================================

namespace LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Represents a capability that the agent possesses.
/// </summary>
public sealed record AgentCapability(
    string Name,
    string Description,
    List<string> RequiredTools,
    double SuccessRate,
    double AverageLatency,
    List<string> KnownLimitations,
    int UsageCount,
    DateTime CreatedAt,
    DateTime LastUsed,
    Dictionary<string, object> Metadata);

/// <summary>
/// Interface for agent capability self-modeling.
/// Allows the agent to understand and track its own capabilities.
/// </summary>
public interface ICapabilityRegistry
{
    /// <summary>
    /// Gets all capabilities the agent possesses.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of agent capabilities</returns>
    Task<List<AgentCapability>> GetCapabilitiesAsync(CancellationToken ct = default);

    /// <summary>
    /// Checks if the agent can handle a given task.
    /// </summary>
    /// <param name="task">The task description</param>
    /// <param name="context">Optional context information</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if the agent can handle the task, false otherwise</returns>
    Task<bool> CanHandleAsync(
        string task,
        Dictionary<string, object>? context = null,
        CancellationToken ct = default);

    /// <summary>
    /// Gets a specific capability by name.
    /// </summary>
    /// <param name="name">The capability name</param>
    /// <returns>The capability if found, null otherwise</returns>
    AgentCapability? GetCapability(string name);

    /// <summary>
    /// Updates capability metrics after execution.
    /// </summary>
    /// <param name="name">The capability name</param>
    /// <param name="result">The execution result</param>
    /// <param name="ct">Cancellation token</param>
    Task UpdateCapabilityAsync(
        string name,
        ExecutionResult result,
        CancellationToken ct = default);

    /// <summary>
    /// Registers a new capability.
    /// </summary>
    /// <param name="capability">The capability to register</param>
    void RegisterCapability(AgentCapability capability);

    /// <summary>
    /// Identifies capability gaps for a given task.
    /// </summary>
    /// <param name="task">The task description</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of missing capabilities needed for the task</returns>
    Task<List<string>> IdentifyCapabilityGapsAsync(
        string task,
        CancellationToken ct = default);

    /// <summary>
    /// Suggests alternatives when a task cannot be handled.
    /// </summary>
    /// <param name="task">The task description</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of alternative approaches or suggestions</returns>
    Task<List<string>> SuggestAlternativesAsync(
        string task,
        CancellationToken ct = default);
}
