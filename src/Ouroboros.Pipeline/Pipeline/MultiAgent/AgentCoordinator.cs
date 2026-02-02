// <copyright file="AgentCoordinator.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System;
using System.Collections.Immutable;
using Ouroboros.Core.Monads;
using Ouroboros.Core.Steps;
using Ouroboros.Pipeline.Planning;

namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Represents the status of an agent task during coordination.
/// </summary>
public enum TaskStatus
{
    /// <summary>
    /// The task is waiting to be assigned to an agent.
    /// </summary>
    Pending,

    /// <summary>
    /// The task has been assigned to an agent but not yet started.
    /// </summary>
    Assigned,

    /// <summary>
    /// The task is currently being executed by an agent.
    /// </summary>
    InProgress,

    /// <summary>
    /// The task has been completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// The task has failed during execution.
    /// </summary>
    Failed,

    /// <summary>
    /// The task was cancelled before completion.
    /// </summary>
    Cancelled
}

/// <summary>
/// Represents a task assigned to an agent for execution during coordination.
/// Tasks are immutable and track their lifecycle from creation through completion.
/// </summary>
/// <param name="Id">The unique identifier for this task.</param>
/// <param name="Goal">The goal that this task is working towards.</param>
/// <param name="AssignedAgentId">The ID of the agent assigned to this task, if any.</param>
/// <param name="Status">The current status of this task.</param>
/// <param name="CreatedAt">The timestamp when this task was created.</param>
/// <param name="StartedAt">The timestamp when this task started execution, if started.</param>
/// <param name="CompletedAt">The timestamp when this task completed, if completed.</param>
/// <param name="Result">The result of the task execution, if completed successfully.</param>
/// <param name="Error">The error message if the task failed.</param>
public sealed record AgentTask(
    Guid Id,
    Goal Goal,
    Guid? AssignedAgentId,
    TaskStatus Status,
    DateTime CreatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt,
    Option<string> Result,
    Option<string> Error)
{
    /// <summary>
    /// Gets the duration of the task execution if the task has both started and completed.
    /// </summary>
    /// <value>The duration of execution, or null if the task hasn't completed.</value>
    public TimeSpan? Duration
    {
        get
        {
            if (StartedAt.HasValue && CompletedAt.HasValue)
            {
                return CompletedAt.Value - StartedAt.Value;
            }

            return null;
        }
    }

    /// <summary>
    /// Creates a new agent task for the specified goal.
    /// </summary>
    /// <param name="goal">The goal that this task will work towards.</param>
    /// <returns>A new <see cref="AgentTask"/> in pending status.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="goal"/> is null.</exception>
    public static AgentTask Create(Goal goal)
    {
        ArgumentNullException.ThrowIfNull(goal);

        return new AgentTask(
            Id: Guid.NewGuid(),
            Goal: goal,
            AssignedAgentId: null,
            Status: TaskStatus.Pending,
            CreatedAt: DateTime.UtcNow,
            StartedAt: null,
            CompletedAt: null,
            Result: Option<string>.None(),
            Error: Option<string>.None());
    }

    /// <summary>
    /// Creates a new task with the specified agent assigned.
    /// </summary>
    /// <param name="agentId">The ID of the agent to assign to this task.</param>
    /// <returns>A new <see cref="AgentTask"/> with the agent assigned and status set to Assigned.</returns>
    public AgentTask AssignTo(Guid agentId)
    {
        return this with
        {
            AssignedAgentId = agentId,
            Status = TaskStatus.Assigned
        };
    }

    /// <summary>
    /// Creates a new task marked as in progress with the current timestamp.
    /// </summary>
    /// <returns>A new <see cref="AgentTask"/> with status set to InProgress and StartedAt timestamp.</returns>
    public AgentTask Start()
    {
        return this with
        {
            Status = TaskStatus.InProgress,
            StartedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Creates a new task marked as completed with the specified result.
    /// </summary>
    /// <param name="result">The result of the task execution.</param>
    /// <returns>A new <see cref="AgentTask"/> with status set to Completed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="result"/> is null.</exception>
    public AgentTask Complete(string result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return this with
        {
            Status = TaskStatus.Completed,
            CompletedAt = DateTime.UtcNow,
            Result = Option<string>.Some(result)
        };
    }

    /// <summary>
    /// Creates a new task marked as failed with the specified error message.
    /// </summary>
    /// <param name="error">The error message describing the failure.</param>
    /// <returns>A new <see cref="AgentTask"/> with status set to Failed.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="error"/> is null.</exception>
    public AgentTask Fail(string error)
    {
        ArgumentNullException.ThrowIfNull(error);

        return this with
        {
            Status = TaskStatus.Failed,
            CompletedAt = DateTime.UtcNow,
            Error = Option<string>.Some(error)
        };
    }
}

/// <summary>
/// Represents the result of a multi-agent coordination session.
/// Contains all tasks, participating agents, and coordination metrics.
/// </summary>
/// <param name="OriginalGoal">The original goal that was coordinated.</param>
/// <param name="Tasks">The list of tasks executed during coordination.</param>
/// <param name="ParticipatingAgents">Dictionary of agents that participated in the coordination.</param>
/// <param name="IsSuccess">Indicates whether the overall coordination was successful.</param>
/// <param name="Summary">A human-readable summary of the coordination result.</param>
/// <param name="TotalDuration">The total time spent on coordination.</param>
public sealed record CoordinationResult(
    Goal OriginalGoal,
    IReadOnlyList<AgentTask> Tasks,
    IReadOnlyDictionary<Guid, AgentIdentity> ParticipatingAgents,
    bool IsSuccess,
    string Summary,
    TimeSpan TotalDuration)
{
    /// <summary>
    /// Gets the count of tasks that completed successfully.
    /// </summary>
    /// <value>The number of completed tasks.</value>
    public int CompletedTaskCount => Tasks.Count(t => t.Status == TaskStatus.Completed);

    /// <summary>
    /// Gets the count of tasks that failed during execution.
    /// </summary>
    /// <value>The number of failed tasks.</value>
    public int FailedTaskCount => Tasks.Count(t => t.Status == TaskStatus.Failed);

    /// <summary>
    /// Gets the success rate as a ratio of completed tasks to total tasks.
    /// </summary>
    /// <value>A value between 0.0 and 1.0, or 1.0 if no tasks were executed.</value>
    public double SuccessRate
    {
        get
        {
            int totalTasks = CompletedTaskCount + FailedTaskCount;
            return totalTasks > 0 ? (double)CompletedTaskCount / totalTasks : 1.0;
        }
    }

    /// <summary>
    /// Creates a successful coordination result.
    /// </summary>
    /// <param name="goal">The goal that was coordinated.</param>
    /// <param name="tasks">The tasks that were executed.</param>
    /// <param name="agents">The agents that participated.</param>
    /// <param name="duration">The total duration of the coordination.</param>
    /// <returns>A <see cref="CoordinationResult"/> indicating success.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public static CoordinationResult Success(
        Goal goal,
        IReadOnlyList<AgentTask> tasks,
        IReadOnlyDictionary<Guid, AgentIdentity> agents,
        TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(agents);

        int completedCount = tasks.Count(t => t.Status == TaskStatus.Completed);
        string summary = $"Coordination completed successfully. {completedCount}/{tasks.Count} tasks completed by {agents.Count} agents.";

        return new CoordinationResult(
            OriginalGoal: goal,
            Tasks: tasks,
            ParticipatingAgents: agents,
            IsSuccess: true,
            Summary: summary,
            TotalDuration: duration);
    }

    /// <summary>
    /// Creates a failed coordination result.
    /// </summary>
    /// <param name="goal">The goal that was coordinated.</param>
    /// <param name="reason">The reason for the failure.</param>
    /// <param name="tasks">The tasks that were attempted.</param>
    /// <param name="duration">The total duration before failure.</param>
    /// <returns>A <see cref="CoordinationResult"/> indicating failure.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public static CoordinationResult Failure(
        Goal goal,
        string reason,
        IReadOnlyList<AgentTask> tasks,
        TimeSpan duration)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(reason);
        ArgumentNullException.ThrowIfNull(tasks);

        string summary = $"Coordination failed: {reason}";

        return new CoordinationResult(
            OriginalGoal: goal,
            Tasks: tasks,
            ParticipatingAgents: ImmutableDictionary<Guid, AgentIdentity>.Empty,
            IsSuccess: false,
            Summary: summary,
            TotalDuration: duration);
    }
}

/// <summary>
/// Represents a team of agents that can be coordinated to work on tasks.
/// The team is immutable and provides methods for querying and managing agents.
/// </summary>
public sealed class AgentTeam
{
    private readonly ImmutableDictionary<Guid, AgentState> _agents;

    /// <summary>
    /// Gets an empty agent team with no agents.
    /// </summary>
    public static AgentTeam Empty { get; } = new AgentTeam(ImmutableDictionary<Guid, AgentState>.Empty);

    /// <summary>
    /// Gets the number of agents in the team.
    /// </summary>
    public int Count => _agents.Count;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentTeam"/> class.
    /// </summary>
    /// <param name="agents">The dictionary of agents in the team.</param>
    private AgentTeam(ImmutableDictionary<Guid, AgentState> agents)
    {
        _agents = agents;
    }

    /// <summary>
    /// Creates a new team with the specified agent added.
    /// </summary>
    /// <param name="identity">The identity of the agent to add.</param>
    /// <returns>A new <see cref="AgentTeam"/> with the agent added.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="identity"/> is null.</exception>
    public AgentTeam AddAgent(AgentIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);

        AgentState state = AgentState.ForAgent(identity);
        return new AgentTeam(_agents.SetItem(identity.Id, state));
    }

    /// <summary>
    /// Creates a new team with the specified agent removed.
    /// </summary>
    /// <param name="agentId">The ID of the agent to remove.</param>
    /// <returns>A new <see cref="AgentTeam"/> without the specified agent.</returns>
    public AgentTeam RemoveAgent(Guid agentId)
    {
        return new AgentTeam(_agents.Remove(agentId));
    }

    /// <summary>
    /// Gets the state of an agent by ID.
    /// </summary>
    /// <param name="agentId">The ID of the agent to retrieve.</param>
    /// <returns>An <see cref="Option{T}"/> containing the agent state if found.</returns>
    public Option<AgentState> GetAgent(Guid agentId)
    {
        if (_agents.TryGetValue(agentId, out AgentState? state))
        {
            return Option<AgentState>.Some(state);
        }

        return Option<AgentState>.None();
    }

    /// <summary>
    /// Gets all agents that are currently available to accept new tasks.
    /// </summary>
    /// <returns>A read-only list of available agent states.</returns>
    public IReadOnlyList<AgentState> GetAvailableAgents()
    {
        return _agents.Values
            .Where(a => a.IsAvailable)
            .ToList();
    }

    /// <summary>
    /// Gets all agents that have the specified capability.
    /// </summary>
    /// <param name="capability">The name of the capability to search for.</param>
    /// <returns>A read-only list of agent states with the specified capability.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="capability"/> is null.</exception>
    public IReadOnlyList<AgentState> GetAgentsWithCapability(string capability)
    {
        ArgumentNullException.ThrowIfNull(capability);

        return _agents.Values
            .Where(a => a.Identity.HasCapability(capability))
            .ToList();
    }

    /// <summary>
    /// Gets all agents that have the specified role.
    /// </summary>
    /// <param name="role">The role to search for.</param>
    /// <returns>A read-only list of agent states with the specified role.</returns>
    public IReadOnlyList<AgentState> GetAgentsByRole(AgentRole role)
    {
        return _agents.Values
            .Where(a => a.Identity.Role == role)
            .ToList();
    }

    /// <summary>
    /// Gets all agent states in the team.
    /// </summary>
    /// <returns>A read-only list of all agent states.</returns>
    public IReadOnlyList<AgentState> GetAllAgents()
    {
        return _agents.Values.ToList();
    }

    /// <summary>
    /// Updates the state of an agent in the team.
    /// </summary>
    /// <param name="agentId">The ID of the agent to update.</param>
    /// <param name="newState">The new state for the agent.</param>
    /// <returns>A new <see cref="AgentTeam"/> with the updated agent state.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="newState"/> is null.</exception>
    internal AgentTeam UpdateAgent(Guid agentId, AgentState newState)
    {
        ArgumentNullException.ThrowIfNull(newState);

        if (!_agents.ContainsKey(agentId))
        {
            return this;
        }

        return new AgentTeam(_agents.SetItem(agentId, newState));
    }

    /// <summary>
    /// Gets the identity dictionary for all agents in the team.
    /// </summary>
    /// <returns>A read-only dictionary mapping agent IDs to identities.</returns>
    internal IReadOnlyDictionary<Guid, AgentIdentity> GetIdentityDictionary()
    {
        return _agents.ToImmutableDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Identity);
    }
}

/// <summary>
/// Defines the contract for coordinating multiple agents to execute goals collaboratively.
/// </summary>
public interface IAgentCoordinator
{
    /// <summary>
    /// Gets the team of agents being coordinated.
    /// </summary>
    AgentTeam Team { get; }

    /// <summary>
    /// Executes a single goal by decomposing it into tasks and coordinating agents.
    /// </summary>
    /// <param name="goal">The goal to execute.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A result containing the coordination outcome or an error message.</returns>
    Task<Result<CoordinationResult, string>> ExecuteAsync(Goal goal, CancellationToken ct = default);

    /// <summary>
    /// Executes multiple goals concurrently by coordinating agents in parallel.
    /// </summary>
    /// <param name="goals">The list of goals to execute.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A result containing the coordination outcome or an error message.</returns>
    Task<Result<CoordinationResult, string>> ExecuteParallelAsync(IReadOnlyList<Goal> goals, CancellationToken ct = default);

    /// <summary>
    /// Sets the delegation strategy used for assigning tasks to agents.
    /// </summary>
    /// <param name="strategy">The delegation strategy to use.</param>
    void SetDelegationStrategy(IDelegationStrategy strategy);
}

/// <summary>
/// Orchestrates multiple agents to collaboratively execute goals through task decomposition and delegation.
/// Follows functional programming principles with immutable state and monadic error handling.
/// </summary>
public sealed class AgentCoordinator : IAgentCoordinator
{
    private AgentTeam _team;
    private readonly IMessageBus _messageBus;
    private IDelegationStrategy _delegationStrategy;
    private readonly object _teamLock = new();

    /// <inheritdoc />
    public AgentTeam Team
    {
        get
        {
            lock (_teamLock)
            {
                return _team;
            }
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentCoordinator"/> class with default round-robin strategy.
    /// </summary>
    /// <param name="team">The team of agents to coordinate.</param>
    /// <param name="messageBus">The message bus for inter-agent communication.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public AgentCoordinator(AgentTeam team, IMessageBus messageBus)
        : this(team, messageBus, DelegationStrategyFactory.RoundRobin())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentCoordinator"/> class with a specified delegation strategy.
    /// </summary>
    /// <param name="team">The team of agents to coordinate.</param>
    /// <param name="messageBus">The message bus for inter-agent communication.</param>
    /// <param name="strategy">The delegation strategy for task assignment.</param>
    /// <exception cref="ArgumentNullException">Thrown when any parameter is null.</exception>
    public AgentCoordinator(AgentTeam team, IMessageBus messageBus, IDelegationStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(team);
        ArgumentNullException.ThrowIfNull(messageBus);
        ArgumentNullException.ThrowIfNull(strategy);

        _team = team;
        _messageBus = messageBus;
        _delegationStrategy = strategy;
    }

    /// <inheritdoc />
    public void SetDelegationStrategy(IDelegationStrategy strategy)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        _delegationStrategy = strategy;
    }

    /// <inheritdoc />
    public async Task<Result<CoordinationResult, string>> ExecuteAsync(Goal goal, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(goal);

        DateTime startTime = DateTime.UtcNow;

        try
        {
            // Check if we have any agents available
            IReadOnlyList<AgentState> availableAgents = Team.GetAvailableAgents();
            if (availableAgents.Count == 0)
            {
                return Result<CoordinationResult, string>.Failure("No available agents to execute the goal.");
            }

            // Decompose the goal into tasks
            IReadOnlyList<AgentTask> tasks = DecomposeGoalToTasks(goal);

            if (tasks.Count == 0)
            {
                return Result<CoordinationResult, string>.Failure("Goal could not be decomposed into any tasks.");
            }

            // Execute all tasks
            List<AgentTask> completedTasks = new List<AgentTask>();
            Dictionary<Guid, AgentIdentity> participatingAgents = new Dictionary<Guid, AgentIdentity>();

            foreach (AgentTask task in tasks)
            {
                ct.ThrowIfCancellationRequested();

                Result<AgentTask, string> taskResult = await ExecuteTaskAsync(task, participatingAgents, ct).ConfigureAwait(false);

                if (taskResult.IsSuccess)
                {
                    completedTasks.Add(taskResult.Value);
                }
                else
                {
                    AgentTask failedTask = task.Fail(taskResult.Error);
                    completedTasks.Add(failedTask);
                }
            }

            TimeSpan duration = DateTime.UtcNow - startTime;

            // Check if all tasks completed successfully
            bool allSucceeded = completedTasks.All(t => t.Status == TaskStatus.Completed);

            if (allSucceeded)
            {
                return Result<CoordinationResult, string>.Success(
                    CoordinationResult.Success(goal, completedTasks, participatingAgents, duration));
            }
            else
            {
                int failedCount = completedTasks.Count(t => t.Status == TaskStatus.Failed);
                return Result<CoordinationResult, string>.Success(
                    CoordinationResult.Failure(goal, $"{failedCount} tasks failed during execution.", completedTasks, duration));
            }
        }
        catch (OperationCanceledException)
        {
            TimeSpan duration = DateTime.UtcNow - startTime;
            return Result<CoordinationResult, string>.Failure($"Coordination was cancelled after {duration.TotalSeconds:F2} seconds.");
        }
        catch (Exception ex)
        {
            TimeSpan duration = DateTime.UtcNow - startTime;
            return Result<CoordinationResult, string>.Failure($"Coordination failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<CoordinationResult, string>> ExecuteParallelAsync(IReadOnlyList<Goal> goals, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(goals);

        if (goals.Count == 0)
        {
            return Result<CoordinationResult, string>.Failure("No goals provided for parallel execution.");
        }

        DateTime startTime = DateTime.UtcNow;

        try
        {
            // Create tasks for each goal
            List<Task<Result<CoordinationResult, string>>> coordinationTasks =
                goals.Select(g => ExecuteAsync(g, ct)).ToList();

            // Execute all in parallel
            Result<CoordinationResult, string>[] results = await Task.WhenAll(coordinationTasks).ConfigureAwait(false);

            // Aggregate results
            List<AgentTask> allTasks = new List<AgentTask>();
            Dictionary<Guid, AgentIdentity> allAgents = new Dictionary<Guid, AgentIdentity>();
            int successCount = 0;

            foreach (Result<CoordinationResult, string> result in results)
            {
                if (result.IsSuccess)
                {
                    CoordinationResult coordination = result.Value;
                    allTasks.AddRange(coordination.Tasks);

                    foreach (KeyValuePair<Guid, AgentIdentity> kvp in coordination.ParticipatingAgents)
                    {
                        allAgents.TryAdd(kvp.Key, kvp.Value);
                    }

                    if (coordination.IsSuccess)
                    {
                        successCount++;
                    }
                }
            }

            TimeSpan duration = DateTime.UtcNow - startTime;

            // Create a composite goal for the result
            Goal compositeGoal = Goal.Atomic($"Parallel execution of {goals.Count} goals");

            bool overallSuccess = successCount == goals.Count;

            if (overallSuccess)
            {
                return Result<CoordinationResult, string>.Success(
                    CoordinationResult.Success(compositeGoal, allTasks, allAgents, duration));
            }
            else
            {
                int failedGoals = goals.Count - successCount;
                return Result<CoordinationResult, string>.Success(
                    CoordinationResult.Failure(compositeGoal, $"{failedGoals}/{goals.Count} goals failed.", allTasks, duration));
            }
        }
        catch (OperationCanceledException)
        {
            TimeSpan duration = DateTime.UtcNow - startTime;
            return Result<CoordinationResult, string>.Failure($"Parallel coordination was cancelled after {duration.TotalSeconds:F2} seconds.");
        }
        catch (Exception ex)
        {
            TimeSpan duration = DateTime.UtcNow - startTime;
            return Result<CoordinationResult, string>.Failure($"Parallel coordination failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a pipeline step that executes a goal through coordination.
    /// </summary>
    /// <returns>A <see cref="Step{TInput, TOutput}"/> that transforms a goal into a coordination result.</returns>
    public Step<Goal, Result<CoordinationResult, string>> CreateExecutionStep()
    {
        return async (Goal goal) => await ExecuteAsync(goal).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a pipeline step that executes multiple goals in parallel.
    /// </summary>
    /// <returns>A <see cref="Step{TInput, TOutput}"/> that transforms a list of goals into a coordination result.</returns>
    public Step<IReadOnlyList<Goal>, Result<CoordinationResult, string>> CreateParallelExecutionStep()
    {
        return async (IReadOnlyList<Goal> goals) => await ExecuteParallelAsync(goals).ConfigureAwait(false);
    }

    /// <summary>
    /// Decomposes a goal into individual tasks for execution.
    /// </summary>
    /// <param name="goal">The goal to decompose.</param>
    /// <returns>A list of agent tasks derived from the goal.</returns>
    private static IReadOnlyList<AgentTask> DecomposeGoalToTasks(Goal goal)
    {
        List<AgentTask> tasks = new List<AgentTask>();

        if (goal.SubGoals.Count == 0)
        {
            // Atomic goal - create a single task
            tasks.Add(AgentTask.Create(goal));
        }
        else
        {
            // Composite goal - create tasks for each sub-goal
            foreach (Goal subGoal in goal.SubGoals)
            {
                IReadOnlyList<AgentTask> subTasks = DecomposeGoalToTasks(subGoal);
                tasks.AddRange(subTasks);
            }
        }

        return tasks;
    }

    /// <summary>
    /// Executes a single task by assigning it to an appropriate agent.
    /// </summary>
    /// <param name="task">The task to execute.</param>
    /// <param name="participatingAgents">Dictionary to track participating agents.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A result containing the completed task or an error message.</returns>
    private async Task<Result<AgentTask, string>> ExecuteTaskAsync(
        AgentTask task,
        Dictionary<Guid, AgentIdentity> participatingAgents,
        CancellationToken ct)
    {
        // Get available agents
        IReadOnlyList<AgentState> availableAgents = Team.GetAvailableAgents();

        if (availableAgents.Count == 0)
        {
            return Result<AgentTask, string>.Failure("No available agents to execute the task.");
        }

        // Select an agent using the delegation strategy
        DelegationCriteria criteria = DelegationCriteria.FromGoal(task.Goal);
        DelegationResult delegationResult = _delegationStrategy.SelectAgent(criteria, Team);

        if (!delegationResult.HasMatch)
        {
            return Result<AgentTask, string>.Failure($"Delegation strategy could not select an appropriate agent: {delegationResult.Reasoning}");
        }

        Option<AgentState> selectedAgentOption = Team.GetAgent(delegationResult.SelectedAgentId!.Value);

        if (!selectedAgentOption.HasValue)
        {
            return Result<AgentTask, string>.Failure("Selected agent is no longer available.");
        }

        AgentState selectedAgent = selectedAgentOption.Value!;

        // Track participating agent
        participatingAgents.TryAdd(selectedAgent.Identity.Id, selectedAgent.Identity);

        // Update team state
        lock (_teamLock)
        {
            _team = _team.UpdateAgent(selectedAgent.Identity.Id, selectedAgent.StartTask(task.Id));
        }

        // Assign and start the task
        AgentTask assignedTask = task.AssignTo(selectedAgent.Identity.Id).Start();

        try
        {
            // Publish task assignment message
            AgentMessage taskMessage = AgentMessage.CreateRequest(
                senderId: Guid.Empty, // Coordinator
                receiverId: selectedAgent.Identity.Id,
                topic: "task.assigned",
                payload: $"Task assigned: {task.Goal.Description}");

            await _messageBus.PublishAsync(taskMessage, ct).ConfigureAwait(false);

            // Simulate task execution (in a real implementation, this would await actual work)
            await Task.Delay(TimeSpan.FromMilliseconds(100), ct).ConfigureAwait(false);

            // Complete the task
            AgentTask completedTask = assignedTask.Complete($"Task completed by agent {selectedAgent.Identity.Name}");

            // Update team state to reflect completion
            lock (_teamLock)
            {
                Option<AgentState> currentStateOption = _team.GetAgent(selectedAgent.Identity.Id);
                if (currentStateOption.HasValue)
                {
                    _team = _team.UpdateAgent(selectedAgent.Identity.Id, currentStateOption.Value!.CompleteTask());
                }
            }

            // Publish completion message
            AgentMessage completionMessage = AgentMessage.CreateNotification(
                senderId: selectedAgent.Identity.Id,
                topic: "task.completed",
                payload: $"Task completed: {task.Goal.Description}");

            await _messageBus.PublishAsync(completionMessage, ct).ConfigureAwait(false);

            return Result<AgentTask, string>.Success(completedTask);
        }
        catch (OperationCanceledException)
        {
            // Update team state to reflect cancellation
            lock (_teamLock)
            {
                Option<AgentState> currentStateOption = _team.GetAgent(selectedAgent.Identity.Id);
                if (currentStateOption.HasValue)
                {
                    _team = _team.UpdateAgent(selectedAgent.Identity.Id, currentStateOption.Value!.FailTask());
                }
            }

            throw;
        }
        catch (Exception ex)
        {
            // Update team state to reflect failure
            lock (_teamLock)
            {
                Option<AgentState> currentStateOption = _team.GetAgent(selectedAgent.Identity.Id);
                if (currentStateOption.HasValue)
                {
                    _team = _team.UpdateAgent(selectedAgent.Identity.Id, currentStateOption.Value!.FailTask());
                }
            }

            AgentTask failedTask = assignedTask.Fail(ex.Message);
            return Result<AgentTask, string>.Success(failedTask);
        }
    }
}

/// <summary>
/// Provides extension methods for composing agent coordination with pipeline steps.
/// </summary>
public static class AgentCoordinatorExtensions
{
    /// <summary>
    /// Pipes a goal through the coordinator for execution.
    /// </summary>
    /// <param name="goalStep">The step that produces a goal.</param>
    /// <param name="coordinator">The agent coordinator to use for execution.</param>
    /// <returns>A step that produces the coordination result.</returns>
    public static Step<TInput, Result<CoordinationResult, string>> ThenCoordinate<TInput>(
        this Step<TInput, Goal> goalStep,
        IAgentCoordinator coordinator)
    {
        ArgumentNullException.ThrowIfNull(goalStep);
        ArgumentNullException.ThrowIfNull(coordinator);

        return async (TInput input) =>
        {
            Goal goal = await goalStep(input).ConfigureAwait(false);
            return await coordinator.ExecuteAsync(goal).ConfigureAwait(false);
        };
    }

    /// <summary>
    /// Pipes multiple goals through the coordinator for parallel execution.
    /// </summary>
    /// <param name="goalsStep">The step that produces a list of goals.</param>
    /// <param name="coordinator">The agent coordinator to use for execution.</param>
    /// <returns>A step that produces the coordination result.</returns>
    public static Step<TInput, Result<CoordinationResult, string>> ThenCoordinateParallel<TInput>(
        this Step<TInput, IReadOnlyList<Goal>> goalsStep,
        IAgentCoordinator coordinator)
    {
        ArgumentNullException.ThrowIfNull(goalsStep);
        ArgumentNullException.ThrowIfNull(coordinator);

        return async (TInput input) =>
        {
            IReadOnlyList<Goal> goals = await goalsStep(input).ConfigureAwait(false);
            return await coordinator.ExecuteParallelAsync(goals).ConfigureAwait(false);
        };
    }

    /// <summary>
    /// Creates an agent team from a collection of agent identities.
    /// </summary>
    /// <param name="identities">The agent identities to add to the team.</param>
    /// <returns>A new <see cref="AgentTeam"/> containing all specified agents.</returns>
    public static AgentTeam ToAgentTeam(this IEnumerable<AgentIdentity> identities)
    {
        ArgumentNullException.ThrowIfNull(identities);

        AgentTeam team = AgentTeam.Empty;

        foreach (AgentIdentity identity in identities)
        {
            team = team.AddAgent(identity);
        }

        return team;
    }

    /// <summary>
    /// Filters a coordination result to include only successful tasks.
    /// </summary>
    /// <param name="result">The coordination result to filter.</param>
    /// <returns>A list of successfully completed tasks.</returns>
    public static IReadOnlyList<AgentTask> GetSuccessfulTasks(this CoordinationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.Tasks
            .Where(t => t.Status == TaskStatus.Completed)
            .ToList();
    }

    /// <summary>
    /// Filters a coordination result to include only failed tasks.
    /// </summary>
    /// <param name="result">The coordination result to filter.</param>
    /// <returns>A list of failed tasks.</returns>
    public static IReadOnlyList<AgentTask> GetFailedTasks(this CoordinationResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return result.Tasks
            .Where(t => t.Status == TaskStatus.Failed)
            .ToList();
    }
}
