// <copyright file="AgentIdentity.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System;
using System.Collections.Immutable;
using Ouroboros.Core.Monads;

namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Represents a capability that an agent possesses, including proficiency level.
/// </summary>
/// <param name="Name">The unique name identifying this capability.</param>
/// <param name="Description">A human-readable description of what this capability enables.</param>
/// <param name="Proficiency">The agent's proficiency level for this capability, ranging from 0.0 to 1.0.</param>
/// <param name="RequiredTools">The list of tools required to exercise this capability.</param>
public sealed record AgentCapability(
    string Name,
    string Description,
    double Proficiency,
    ImmutableList<string> RequiredTools)
{
    /// <summary>
    /// Creates a new agent capability with the specified parameters.
    /// </summary>
    /// <param name="name">The unique name identifying this capability.</param>
    /// <param name="description">A human-readable description of what this capability enables.</param>
    /// <param name="proficiency">The agent's proficiency level, ranging from 0.0 to 1.0. Defaults to 1.0.</param>
    /// <param name="tools">The tools required to exercise this capability.</param>
    /// <returns>A new <see cref="AgentCapability"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> or <paramref name="description"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="proficiency"/> is not between 0.0 and 1.0.</exception>
    public static AgentCapability Create(string name, string description, double proficiency = 1.0, params string[] tools)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(tools);

        if (proficiency < 0.0 || proficiency > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(proficiency), proficiency, "Proficiency must be between 0.0 and 1.0.");
        }

        return new AgentCapability(
            name,
            description,
            proficiency,
            tools.ToImmutableList());
    }
}

/// <summary>
/// Defines high-level role classifications for agents in multi-agent collaboration.
/// </summary>
public enum AgentRole
{
    /// <summary>
    /// An agent specialized in analyzing information and providing insights.
    /// </summary>
    Analyst,

    /// <summary>
    /// An agent specialized in writing and modifying code.
    /// </summary>
    Coder,

    /// <summary>
    /// An agent specialized in reviewing work and providing feedback.
    /// </summary>
    Reviewer,

    /// <summary>
    /// An agent specialized in creating plans and strategies.
    /// </summary>
    Planner,

    /// <summary>
    /// An agent specialized in executing tasks and actions.
    /// </summary>
    Executor,

    /// <summary>
    /// An agent with specialized domain expertise.
    /// </summary>
    Specialist
}

/// <summary>
/// Represents the unique identity and profile of an agent in a multi-agent system.
/// </summary>
/// <param name="Id">The unique identifier for this agent.</param>
/// <param name="Name">The human-readable name of this agent.</param>
/// <param name="Role">The high-level role classification of this agent.</param>
/// <param name="Capabilities">The list of capabilities this agent possesses.</param>
/// <param name="Metadata">Additional metadata associated with this agent.</param>
/// <param name="CreatedAt">The timestamp when this agent identity was created.</param>
public sealed record AgentIdentity(
    Guid Id,
    string Name,
    AgentRole Role,
    ImmutableList<AgentCapability> Capabilities,
    ImmutableDictionary<string, object> Metadata,
    DateTime CreatedAt)
{
    /// <summary>
    /// Creates a new agent identity with the specified name and role.
    /// </summary>
    /// <param name="name">The human-readable name of the agent.</param>
    /// <param name="role">The high-level role classification of the agent.</param>
    /// <returns>A new <see cref="AgentIdentity"/> instance with a generated ID and current timestamp.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    public static AgentIdentity Create(string name, AgentRole role)
    {
        ArgumentNullException.ThrowIfNull(name);

        return new AgentIdentity(
            Guid.NewGuid(),
            name,
            role,
            ImmutableList<AgentCapability>.Empty,
            ImmutableDictionary<string, object>.Empty,
            DateTime.UtcNow);
    }

    /// <summary>
    /// Creates a new agent identity with the specified capability added.
    /// </summary>
    /// <param name="capability">The capability to add to this agent.</param>
    /// <returns>A new <see cref="AgentIdentity"/> instance with the capability added.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="capability"/> is null.</exception>
    public AgentIdentity WithCapability(AgentCapability capability)
    {
        ArgumentNullException.ThrowIfNull(capability);

        return this with { Capabilities = Capabilities.Add(capability) };
    }

    /// <summary>
    /// Creates a new agent identity with the specified metadata entry added or updated.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <returns>A new <see cref="AgentIdentity"/> instance with the metadata entry.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="key"/> or <paramref name="value"/> is null.</exception>
    public AgentIdentity WithMetadata(string key, object value)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(value);

        return this with { Metadata = Metadata.SetItem(key, value) };
    }

    /// <summary>
    /// Gets a capability by name if it exists.
    /// </summary>
    /// <param name="name">The name of the capability to retrieve.</param>
    /// <returns>An <see cref="Option{T}"/> containing the capability if found, or None otherwise.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    public Option<AgentCapability> GetCapability(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        AgentCapability? capability = Capabilities.Find(c => c.Name.Equals(name, StringComparison.Ordinal));
        return capability is not null
            ? Option<AgentCapability>.Some(capability)
            : Option<AgentCapability>.None();
    }

    /// <summary>
    /// Determines whether this agent has the specified capability.
    /// </summary>
    /// <param name="name">The name of the capability to check for.</param>
    /// <returns><c>true</c> if the agent has the capability; otherwise, <c>false</c>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null.</exception>
    public bool HasCapability(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        return Capabilities.Exists(c => c.Name.Equals(name, StringComparison.Ordinal));
    }

    /// <summary>
    /// Gets the proficiency level for a specific capability.
    /// </summary>
    /// <param name="capabilityName">The name of the capability.</param>
    /// <returns>The proficiency level (0.0 to 1.0) if the capability exists, or 0.0 if not found.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="capabilityName"/> is null.</exception>
    public double GetProficiencyFor(string capabilityName)
    {
        ArgumentNullException.ThrowIfNull(capabilityName);

        AgentCapability? capability = Capabilities.Find(c => c.Name.Equals(capabilityName, StringComparison.Ordinal));
        return capability?.Proficiency ?? 0.0;
    }

    /// <summary>
    /// Gets all capabilities with proficiency above the specified minimum threshold.
    /// </summary>
    /// <param name="minProficiency">The minimum proficiency threshold (exclusive).</param>
    /// <returns>A read-only list of capabilities above the threshold.</returns>
    public IReadOnlyList<AgentCapability> GetCapabilitiesAbove(double minProficiency)
    {
        return Capabilities.FindAll(c => c.Proficiency > minProficiency);
    }
}

/// <summary>
/// Defines the possible status values for an agent in a multi-agent system.
/// </summary>
public enum AgentStatus
{
    /// <summary>
    /// The agent is idle and available to accept tasks.
    /// </summary>
    Idle,

    /// <summary>
    /// The agent is currently busy processing a task.
    /// </summary>
    Busy,

    /// <summary>
    /// The agent is waiting for external input or another agent.
    /// </summary>
    Waiting,

    /// <summary>
    /// The agent has encountered an error state.
    /// </summary>
    Error,

    /// <summary>
    /// The agent is offline and unavailable.
    /// </summary>
    Offline
}

/// <summary>
/// Represents the current state of an agent, including status and task statistics.
/// </summary>
/// <param name="Identity">The identity of the agent.</param>
/// <param name="Status">The current status of the agent.</param>
/// <param name="CurrentTaskId">The ID of the task currently being processed, if any.</param>
/// <param name="CompletedTasks">The number of tasks successfully completed by this agent.</param>
/// <param name="FailedTasks">The number of tasks that failed during processing by this agent.</param>
/// <param name="LastActivityAt">The timestamp of the agent's last activity.</param>
public sealed record AgentState(
    AgentIdentity Identity,
    AgentStatus Status,
    Option<Guid> CurrentTaskId,
    int CompletedTasks,
    int FailedTasks,
    DateTime LastActivityAt)
{
    /// <summary>
    /// Gets the success rate of this agent based on completed and failed tasks.
    /// </summary>
    /// <value>A value between 0.0 and 1.0 representing the success rate, or 1.0 if no tasks have been attempted.</value>
    public double SuccessRate
    {
        get
        {
            int totalTasks = CompletedTasks + FailedTasks;
            return totalTasks > 0 ? (double)CompletedTasks / totalTasks : 1.0;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this agent is available to accept new tasks.
    /// </summary>
    /// <value><c>true</c> if the agent is idle; otherwise, <c>false</c>.</value>
    public bool IsAvailable => Status == AgentStatus.Idle;

    /// <summary>
    /// Creates a new agent state for the specified agent identity.
    /// </summary>
    /// <param name="identity">The identity of the agent.</param>
    /// <returns>A new <see cref="AgentState"/> instance in idle status with no completed or failed tasks.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="identity"/> is null.</exception>
    public static AgentState ForAgent(AgentIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        return new AgentState(
            identity,
            AgentStatus.Idle,
            Option<Guid>.None(),
            CompletedTasks: 0,
            FailedTasks: 0,
            DateTime.UtcNow);
    }

    /// <summary>
    /// Creates a new agent state with the specified status.
    /// </summary>
    /// <param name="status">The new status for the agent.</param>
    /// <returns>A new <see cref="AgentState"/> instance with the updated status and activity timestamp.</returns>
    public AgentState WithStatus(AgentStatus status)
    {
        return this with
        {
            Status = status,
            LastActivityAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a new agent state indicating the agent has started processing a task.
    /// </summary>
    /// <param name="taskId">The ID of the task being started.</param>
    /// <returns>A new <see cref="AgentState"/> instance with busy status and the current task ID set.</returns>
    public AgentState StartTask(Guid taskId)
    {
        return this with
        {
            Status = AgentStatus.Busy,
            CurrentTaskId = Option<Guid>.Some(taskId),
            LastActivityAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a new agent state indicating the current task was completed successfully.
    /// </summary>
    /// <returns>A new <see cref="AgentState"/> instance with idle status, incremented completed tasks, and cleared current task.</returns>
    public AgentState CompleteTask()
    {
        return this with
        {
            Status = AgentStatus.Idle,
            CurrentTaskId = Option<Guid>.None(),
            CompletedTasks = CompletedTasks + 1,
            LastActivityAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a new agent state indicating the current task failed.
    /// </summary>
    /// <returns>A new <see cref="AgentState"/> instance with error status, incremented failed tasks, and cleared current task.</returns>
    public AgentState FailTask()
    {
        return this with
        {
            Status = AgentStatus.Error,
            CurrentTaskId = Option<Guid>.None(),
            FailedTasks = FailedTasks + 1,
            LastActivityAt = DateTime.UtcNow
        };
    }
}
