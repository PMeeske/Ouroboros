// <copyright file="DelegationStrategy.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System;
using System.Collections.Immutable;
using Ouroboros.Core.Monads;
using Ouroboros.Pipeline.Planning;

namespace Ouroboros.Pipeline.MultiAgent;

/// <summary>
/// Represents the criteria used to delegate a task to an agent.
/// Encapsulates goal requirements, capability constraints, and agent preferences.
/// </summary>
/// <param name="Goal">The goal that needs to be delegated.</param>
/// <param name="RequiredCapabilities">The list of capabilities required to complete the goal.</param>
/// <param name="MinProficiency">The minimum proficiency level required (0.0 to 1.0).</param>
/// <param name="PreferAvailable">Whether to prefer agents that are currently available.</param>
/// <param name="PreferredRole">The preferred role for the agent, if any.</param>
public sealed record DelegationCriteria(
    Goal Goal,
    IReadOnlyList<string> RequiredCapabilities,
    double MinProficiency,
    bool PreferAvailable,
    AgentRole? PreferredRole)
{
    /// <summary>
    /// Creates delegation criteria from a goal with default settings.
    /// </summary>
    /// <param name="goal">The goal to create criteria for.</param>
    /// <returns>A new <see cref="DelegationCriteria"/> with default settings.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="goal"/> is null.</exception>
    public static DelegationCriteria FromGoal(Goal goal)
    {
        ArgumentNullException.ThrowIfNull(goal);

        return new DelegationCriteria(
            Goal: goal,
            RequiredCapabilities: Array.Empty<string>(),
            MinProficiency: 0.0,
            PreferAvailable: true,
            PreferredRole: null);
    }

    /// <summary>
    /// Creates a new criteria with the specified minimum proficiency level.
    /// </summary>
    /// <param name="minProficiency">The minimum proficiency level (0.0 to 1.0).</param>
    /// <returns>A new <see cref="DelegationCriteria"/> with the updated proficiency requirement.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="minProficiency"/> is not between 0.0 and 1.0.
    /// </exception>
    public DelegationCriteria WithMinProficiency(double minProficiency)
    {
        if (minProficiency < 0.0 || minProficiency > 1.0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(minProficiency),
                minProficiency,
                "Minimum proficiency must be between 0.0 and 1.0.");
        }

        return this with { MinProficiency = minProficiency };
    }

    /// <summary>
    /// Creates a new criteria with the specified preferred role.
    /// </summary>
    /// <param name="role">The preferred role for the agent.</param>
    /// <returns>A new <see cref="DelegationCriteria"/> with the preferred role set.</returns>
    public DelegationCriteria WithPreferredRole(AgentRole role)
    {
        return this with { PreferredRole = role };
    }

    /// <summary>
    /// Creates a new criteria with an additional required capability.
    /// </summary>
    /// <param name="capability">The capability to require.</param>
    /// <returns>A new <see cref="DelegationCriteria"/> with the capability added to requirements.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="capability"/> is null.</exception>
    public DelegationCriteria RequireCapability(string capability)
    {
        ArgumentNullException.ThrowIfNull(capability);

        List<string> newCapabilities = new(RequiredCapabilities) { capability };
        return this with { RequiredCapabilities = newCapabilities };
    }

    /// <summary>
    /// Creates a new criteria with the availability preference set.
    /// </summary>
    /// <param name="preferAvailable">Whether to prefer available agents.</param>
    /// <returns>A new <see cref="DelegationCriteria"/> with the updated preference.</returns>
    public DelegationCriteria WithAvailabilityPreference(bool preferAvailable)
    {
        return this with { PreferAvailable = preferAvailable };
    }
}

/// <summary>
/// Represents the result of a delegation attempt, including the selected agent and match quality.
/// </summary>
/// <param name="SelectedAgentId">The ID of the selected agent, or null if no match was found.</param>
/// <param name="Reasoning">A human-readable explanation of the delegation decision.</param>
/// <param name="MatchScore">A score indicating how well the agent matches the criteria (0.0 to 1.0).</param>
/// <param name="Alternatives">A list of alternative agent IDs that could also handle the task.</param>
public sealed record DelegationResult(
    Guid? SelectedAgentId,
    string Reasoning,
    double MatchScore,
    IReadOnlyList<Guid> Alternatives)
{
    /// <summary>
    /// Gets a value indicating whether a matching agent was found.
    /// </summary>
    /// <value><c>true</c> if an agent was selected; otherwise, <c>false</c>.</value>
    public bool HasMatch => SelectedAgentId.HasValue;

    /// <summary>
    /// Creates a successful delegation result with the specified agent.
    /// </summary>
    /// <param name="agentId">The ID of the selected agent.</param>
    /// <param name="reasoning">The reasoning for the selection.</param>
    /// <param name="score">The match score (0.0 to 1.0).</param>
    /// <param name="alternatives">Optional list of alternative agent IDs.</param>
    /// <returns>A new <see cref="DelegationResult"/> indicating a successful match.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="reasoning"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="score"/> is not between 0.0 and 1.0.
    /// </exception>
    public static DelegationResult Success(
        Guid agentId,
        string reasoning,
        double score,
        IReadOnlyList<Guid>? alternatives = null)
    {
        ArgumentNullException.ThrowIfNull(reasoning);

        if (score < 0.0 || score > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(score), score, "Score must be between 0.0 and 1.0.");
        }

        return new DelegationResult(
            SelectedAgentId: agentId,
            Reasoning: reasoning,
            MatchScore: score,
            Alternatives: alternatives ?? Array.Empty<Guid>());
    }

    /// <summary>
    /// Creates a delegation result indicating no suitable agent was found.
    /// </summary>
    /// <param name="reason">The reason why no agent could be selected.</param>
    /// <returns>A new <see cref="DelegationResult"/> indicating no match.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="reason"/> is null.</exception>
    public static DelegationResult NoMatch(string reason)
    {
        ArgumentNullException.ThrowIfNull(reason);

        return new DelegationResult(
            SelectedAgentId: null,
            Reasoning: reason,
            MatchScore: 0.0,
            Alternatives: Array.Empty<Guid>());
    }
}

/// <summary>
/// Defines a strategy for delegating tasks to agents based on various criteria.
/// Implementations determine how agents are selected for task execution.
/// </summary>
public interface IDelegationStrategy
{
    /// <summary>
    /// Gets the name of this delegation strategy.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Selects the most suitable agent for the given criteria from the team.
    /// </summary>
    /// <param name="criteria">The delegation criteria to match against.</param>
    /// <param name="team">The team of agents to select from.</param>
    /// <returns>A <see cref="DelegationResult"/> containing the selection outcome.</returns>
    DelegationResult SelectAgent(DelegationCriteria criteria, AgentTeam team);

    /// <summary>
    /// Selects multiple suitable agents for the given criteria from the team.
    /// </summary>
    /// <param name="criteria">The delegation criteria to match against.</param>
    /// <param name="team">The team of agents to select from.</param>
    /// <param name="count">The maximum number of agents to select.</param>
    /// <returns>A read-only list of <see cref="DelegationResult"/> instances, ordered by match score.</returns>
    IReadOnlyList<DelegationResult> SelectAgents(DelegationCriteria criteria, AgentTeam team, int count);
}

/// <summary>
/// A delegation strategy that selects agents based on their capability proficiency.
/// Scores agents by averaging their proficiency across required capabilities.
/// </summary>
/// <remarks>
/// <para>Scoring Algorithm:</para>
/// <list type="number">
///   <item>For each required capability, get the agent's proficiency (0.0 if missing).</item>
///   <item>Filter agents below the minimum proficiency threshold.</item>
///   <item>Calculate average proficiency across all required capabilities.</item>
///   <item>Apply availability bonus (10%) if agent is available and preference is set.</item>
///   <item>Select agent with highest final score.</item>
/// </list>
/// </remarks>
public sealed class CapabilityBasedStrategy : IDelegationStrategy
{
    private const double AvailabilityBonus = 0.10;

    /// <inheritdoc />
    public string Name => "CapabilityBased";

    /// <inheritdoc />
    public DelegationResult SelectAgent(DelegationCriteria criteria, AgentTeam team)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(team);

        IReadOnlyList<DelegationResult> results = SelectAgents(criteria, team, 1);
        return results.Count > 0
            ? results[0]
            : DelegationResult.NoMatch("No agents meet the capability requirements.");
    }

    /// <inheritdoc />
    public IReadOnlyList<DelegationResult> SelectAgents(DelegationCriteria criteria, AgentTeam team, int count)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(team);

        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Count must be greater than zero.");
        }

        IReadOnlyList<AgentState> agents = team.GetAllAgents();

        if (agents.Count == 0)
        {
            return Array.Empty<DelegationResult>();
        }

        List<(AgentState Agent, double Score)> scoredAgents = new();

        foreach (AgentState agent in agents)
        {
            double score = CalculateCapabilityScore(agent, criteria);

            if (score >= criteria.MinProficiency)
            {
                // Apply availability bonus if preferred
                if (criteria.PreferAvailable && agent.IsAvailable)
                {
                    score = Math.Min(1.0, score + AvailabilityBonus);
                }

                scoredAgents.Add((agent, score));
            }
        }

        List<DelegationResult> results = scoredAgents
            .OrderByDescending(x => x.Score)
            .Take(count)
            .Select((x, index) =>
            {
                IReadOnlyList<Guid> alternatives = index == 0
                    ? scoredAgents
                        .OrderByDescending(a => a.Score)
                        .Skip(1)
                        .Take(3)
                        .Select(a => a.Agent.Identity.Id)
                        .ToList()
                    : Array.Empty<Guid>();

                string reasoning = BuildCapabilityReasoning(x.Agent, criteria, x.Score);
                return DelegationResult.Success(x.Agent.Identity.Id, reasoning, x.Score, alternatives);
            })
            .ToList();

        return results;
    }

    /// <summary>
    /// Calculates the capability score for an agent based on required capabilities.
    /// </summary>
    /// <param name="agent">The agent to score.</param>
    /// <param name="criteria">The delegation criteria.</param>
    /// <returns>The capability score (0.0 to 1.0).</returns>
    private static double CalculateCapabilityScore(AgentState agent, DelegationCriteria criteria)
    {
        if (criteria.RequiredCapabilities.Count == 0)
        {
            // No specific capabilities required - return base score from success rate
            return agent.SuccessRate;
        }

        double totalProficiency = 0.0;
        int matchedCapabilities = 0;

        foreach (string capability in criteria.RequiredCapabilities)
        {
            double proficiency = agent.Identity.GetProficiencyFor(capability);
            if (proficiency > 0.0)
            {
                totalProficiency += proficiency;
                matchedCapabilities++;
            }
        }

        // Return weighted average: capability coverage * average proficiency
        double coverage = (double)matchedCapabilities / criteria.RequiredCapabilities.Count;
        double averageProficiency = matchedCapabilities > 0
            ? totalProficiency / matchedCapabilities
            : 0.0;

        return coverage * averageProficiency;
    }

    /// <summary>
    /// Builds a human-readable reasoning string for the capability-based selection.
    /// </summary>
    private static string BuildCapabilityReasoning(AgentState agent, DelegationCriteria criteria, double score)
    {
        if (criteria.RequiredCapabilities.Count == 0)
        {
            return $"Selected '{agent.Identity.Name}' based on success rate ({agent.SuccessRate:P0}). Score: {score:F2}";
        }

        int matchedCount = criteria.RequiredCapabilities
            .Count(c => agent.Identity.HasCapability(c));

        return $"Selected '{agent.Identity.Name}' matching {matchedCount}/{criteria.RequiredCapabilities.Count} " +
               $"required capabilities with score {score:F2}.";
    }
}

/// <summary>
/// A delegation strategy that selects agents based on their role classification.
/// Falls back to capability matching if no agents match the preferred role.
/// </summary>
/// <remarks>
/// <para>Selection Algorithm:</para>
/// <list type="number">
///   <item>Filter agents matching the preferred role (if specified).</item>
///   <item>If no role match, fall back to all agents.</item>
///   <item>Score remaining agents by capability proficiency.</item>
///   <item>Apply role bonus (20%) for exact role matches.</item>
///   <item>Select agent with highest final score.</item>
/// </list>
/// </remarks>
public sealed class RoleBasedStrategy : IDelegationStrategy
{
    private const double RoleBonus = 0.20;
    private readonly CapabilityBasedStrategy _fallbackStrategy = new();

    /// <inheritdoc />
    public string Name => "RoleBased";

    /// <inheritdoc />
    public DelegationResult SelectAgent(DelegationCriteria criteria, AgentTeam team)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(team);

        IReadOnlyList<DelegationResult> results = SelectAgents(criteria, team, 1);
        return results.Count > 0
            ? results[0]
            : DelegationResult.NoMatch("No agents match the required role or capabilities.");
    }

    /// <inheritdoc />
    public IReadOnlyList<DelegationResult> SelectAgents(DelegationCriteria criteria, AgentTeam team, int count)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(team);

        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Count must be greater than zero.");
        }

        if (!criteria.PreferredRole.HasValue)
        {
            // No role preference - delegate to capability-based strategy
            return _fallbackStrategy.SelectAgents(criteria, team, count);
        }

        AgentRole preferredRole = criteria.PreferredRole.Value;
        IReadOnlyList<AgentState> roleMatchedAgents = team.GetAgentsByRole(preferredRole);
        IReadOnlyList<AgentState> allAgents = team.GetAllAgents();

        List<(AgentState Agent, double Score, bool RoleMatch)> scoredAgents = new();

        // Score role-matched agents with bonus
        foreach (AgentState agent in roleMatchedAgents)
        {
            double baseScore = CalculateBaseScore(agent, criteria);
            double finalScore = Math.Min(1.0, baseScore + RoleBonus);

            if (finalScore >= criteria.MinProficiency)
            {
                scoredAgents.Add((agent, finalScore, true));
            }
        }

        // Score other agents without bonus as fallback candidates
        foreach (AgentState agent in allAgents)
        {
            if (agent.Identity.Role == preferredRole)
            {
                continue; // Already scored above
            }

            double score = CalculateBaseScore(agent, criteria);

            if (score >= criteria.MinProficiency)
            {
                scoredAgents.Add((agent, score, false));
            }
        }

        List<DelegationResult> results = scoredAgents
            .OrderByDescending(x => x.Score)
            .Take(count)
            .Select((x, index) =>
            {
                IReadOnlyList<Guid> alternatives = index == 0
                    ? scoredAgents
                        .OrderByDescending(a => a.Score)
                        .Skip(1)
                        .Take(3)
                        .Select(a => a.Agent.Identity.Id)
                        .ToList()
                    : Array.Empty<Guid>();

                string reasoning = x.RoleMatch
                    ? $"Selected '{x.Agent.Identity.Name}' with matching role '{preferredRole}'. Score: {x.Score:F2}"
                    : $"Selected '{x.Agent.Identity.Name}' as fallback (no role match). Score: {x.Score:F2}";

                return DelegationResult.Success(x.Agent.Identity.Id, reasoning, x.Score, alternatives);
            })
            .ToList();

        return results;
    }

    /// <summary>
    /// Calculates the base score for an agent considering capabilities and availability.
    /// </summary>
    private static double CalculateBaseScore(AgentState agent, DelegationCriteria criteria)
    {
        double score = agent.SuccessRate;

        if (criteria.RequiredCapabilities.Count > 0)
        {
            int matched = criteria.RequiredCapabilities.Count(c => agent.Identity.HasCapability(c));
            double coverage = (double)matched / criteria.RequiredCapabilities.Count;
            score = (score + coverage) / 2.0;
        }

        if (criteria.PreferAvailable && agent.IsAvailable)
        {
            score = Math.Min(1.0, score + 0.05);
        }

        return score;
    }
}

/// <summary>
/// A delegation strategy that selects the least busy available agent.
/// Uses success rate as a tiebreaker when multiple agents have equal load.
/// </summary>
/// <remarks>
/// <para>Selection Algorithm:</para>
/// <list type="number">
///   <item>Filter agents that are currently available (idle).</item>
///   <item>If no available agents and not strictly required, consider all agents.</item>
///   <item>Sort by current task count (ascending) then success rate (descending).</item>
///   <item>Calculate load score: inverse of (completed + failed + current) normalized.</item>
///   <item>Select agent with lowest load and highest success rate.</item>
/// </list>
/// </remarks>
public sealed class LoadBalancingStrategy : IDelegationStrategy
{
    /// <inheritdoc />
    public string Name => "LoadBalancing";

    /// <inheritdoc />
    public DelegationResult SelectAgent(DelegationCriteria criteria, AgentTeam team)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(team);

        IReadOnlyList<DelegationResult> results = SelectAgents(criteria, team, 1);
        return results.Count > 0
            ? results[0]
            : DelegationResult.NoMatch("No agents available for load balancing.");
    }

    /// <inheritdoc />
    public IReadOnlyList<DelegationResult> SelectAgents(DelegationCriteria criteria, AgentTeam team, int count)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(team);

        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Count must be greater than zero.");
        }

        IReadOnlyList<AgentState> candidates = criteria.PreferAvailable
            ? team.GetAvailableAgents()
            : team.GetAllAgents();

        // If no available agents but availability not strictly required, fall back to all
        if (candidates.Count == 0 && criteria.PreferAvailable)
        {
            candidates = team.GetAllAgents();
        }

        if (candidates.Count == 0)
        {
            return Array.Empty<DelegationResult>();
        }

        // Calculate max tasks for normalization
        int maxTasks = candidates.Max(a => a.CompletedTasks + a.FailedTasks);
        if (maxTasks == 0)
        {
            maxTasks = 1; // Avoid division by zero
        }

        List<(AgentState Agent, double Score)> scoredAgents = candidates
            .Select(agent =>
            {
                int totalTasks = agent.CompletedTasks + agent.FailedTasks;
                int currentLoad = agent.IsAvailable ? 0 : 1;

                // Load score: lower is better, so we invert
                // Score = (1 - normalized_load) * weight + success_rate * weight
                double loadFactor = 1.0 - ((double)(totalTasks + currentLoad) / (maxTasks + 1));
                double successFactor = agent.SuccessRate;

                // Weighted combination: 60% load balance, 40% success rate
                double score = (loadFactor * 0.6) + (successFactor * 0.4);

                return (Agent: agent, Score: score);
            })
            .OrderByDescending(x => x.Score)
            .ToList();

        List<DelegationResult> results = scoredAgents
            .Take(count)
            .Select((x, index) =>
            {
                IReadOnlyList<Guid> alternatives = index == 0
                    ? scoredAgents
                        .Skip(1)
                        .Take(3)
                        .Select(a => a.Agent.Identity.Id)
                        .ToList()
                    : Array.Empty<Guid>();

                int totalTasks = x.Agent.CompletedTasks + x.Agent.FailedTasks;
                string availability = x.Agent.IsAvailable ? "available" : "busy";
                string reasoning = $"Selected '{x.Agent.Identity.Name}' ({availability}) with " +
                                   $"{totalTasks} completed tasks and {x.Agent.SuccessRate:P0} success rate. " +
                                   $"Load score: {x.Score:F2}";

                return DelegationResult.Success(x.Agent.Identity.Id, reasoning, x.Score, alternatives);
            })
            .ToList();

        return results;
    }
}

/// <summary>
/// A delegation strategy that cycles through agents in round-robin order.
/// Maintains internal state to ensure fair distribution of tasks across agents.
/// </summary>
/// <remarks>
/// <para>Selection Algorithm:</para>
/// <list type="number">
///   <item>Maintain a rotating index across invocations.</item>
///   <item>Filter candidates based on availability preference.</item>
///   <item>Select agent at current index and advance.</item>
///   <item>Score is based on position in rotation (first = 1.0, decreasing).</item>
///   <item>Thread-safe through internal locking.</item>
/// </list>
/// </remarks>
public sealed class RoundRobinStrategy : IDelegationStrategy
{
    private int _currentIndex;
    private readonly object _lock = new();

    /// <inheritdoc />
    public string Name => "RoundRobin";

    /// <inheritdoc />
    public DelegationResult SelectAgent(DelegationCriteria criteria, AgentTeam team)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(team);

        IReadOnlyList<DelegationResult> results = SelectAgents(criteria, team, 1);
        return results.Count > 0
            ? results[0]
            : DelegationResult.NoMatch("No agents available for round-robin selection.");
    }

    /// <inheritdoc />
    public IReadOnlyList<DelegationResult> SelectAgents(DelegationCriteria criteria, AgentTeam team, int count)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(team);

        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Count must be greater than zero.");
        }

        IReadOnlyList<AgentState> candidates = criteria.PreferAvailable
            ? team.GetAvailableAgents()
            : team.GetAllAgents();

        // Fall back to all agents if no available ones
        if (candidates.Count == 0 && criteria.PreferAvailable)
        {
            candidates = team.GetAllAgents();
        }

        if (candidates.Count == 0)
        {
            return Array.Empty<DelegationResult>();
        }

        List<DelegationResult> results = new();
        int actualCount = Math.Min(count, candidates.Count);

        lock (_lock)
        {
            for (int i = 0; i < actualCount; i++)
            {
                int index = (_currentIndex + i) % candidates.Count;
                AgentState agent = candidates[index];

                // Score decreases based on position in selection order
                double score = 1.0 - ((double)i / actualCount);

                IReadOnlyList<Guid> alternatives = i == 0 && candidates.Count > 1
                    ? Enumerable.Range(1, Math.Min(3, candidates.Count - 1))
                        .Select(j => candidates[(_currentIndex + j) % candidates.Count].Identity.Id)
                        .ToList()
                    : Array.Empty<Guid>();

                string reasoning = $"Selected '{agent.Identity.Name}' in round-robin position {index + 1}/{candidates.Count}.";
                results.Add(DelegationResult.Success(agent.Identity.Id, reasoning, score, alternatives));
            }

            // Advance the index for next selection
            _currentIndex = (_currentIndex + actualCount) % candidates.Count;
        }

        return results;
    }

    /// <summary>
    /// Resets the round-robin index to start from the first agent.
    /// </summary>
    public void Reset()
    {
        lock (_lock)
        {
            _currentIndex = 0;
        }
    }
}

/// <summary>
/// A delegation strategy that combines capability, availability, and success rate
/// using a weighted scoring algorithm for optimal agent selection.
/// </summary>
/// <remarks>
/// <para>Weighted Scoring Algorithm:</para>
/// <list type="number">
///   <item>Capability Score (40%): Average proficiency across required capabilities.</item>
///   <item>Availability Score (25%): 1.0 if available, 0.3 if busy.</item>
///   <item>Success Rate Score (25%): Historical task success rate.</item>
///   <item>Role Match Score (10%): 1.0 if role matches, 0.5 otherwise.</item>
///   <item>Final Score = Σ(weight × component_score)</item>
/// </list>
/// </remarks>
public sealed class BestFitStrategy : IDelegationStrategy
{
    private const double CapabilityWeight = 0.40;
    private const double AvailabilityWeight = 0.25;
    private const double SuccessRateWeight = 0.25;
    private const double RoleMatchWeight = 0.10;

    /// <inheritdoc />
    public string Name => "BestFit";

    /// <inheritdoc />
    public DelegationResult SelectAgent(DelegationCriteria criteria, AgentTeam team)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(team);

        IReadOnlyList<DelegationResult> results = SelectAgents(criteria, team, 1);
        return results.Count > 0
            ? results[0]
            : DelegationResult.NoMatch("No agents meet the best-fit criteria.");
    }

    /// <inheritdoc />
    public IReadOnlyList<DelegationResult> SelectAgents(DelegationCriteria criteria, AgentTeam team, int count)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(team);

        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Count must be greater than zero.");
        }

        IReadOnlyList<AgentState> agents = team.GetAllAgents();

        if (agents.Count == 0)
        {
            return Array.Empty<DelegationResult>();
        }

        List<(AgentState Agent, double Score, ScoreBreakdown Breakdown)> scoredAgents = agents
            .Select(agent => CalculateWeightedScore(agent, criteria))
            .Where(x => x.Score >= criteria.MinProficiency)
            .OrderByDescending(x => x.Score)
            .ToList();

        List<DelegationResult> results = scoredAgents
            .Take(count)
            .Select((x, index) =>
            {
                IReadOnlyList<Guid> alternatives = index == 0
                    ? scoredAgents
                        .Skip(1)
                        .Take(3)
                        .Select(a => a.Agent.Identity.Id)
                        .ToList()
                    : Array.Empty<Guid>();

                string reasoning = BuildBestFitReasoning(x.Agent, x.Breakdown, x.Score);
                return DelegationResult.Success(x.Agent.Identity.Id, reasoning, x.Score, alternatives);
            })
            .ToList();

        return results;
    }

    /// <summary>
    /// Calculates the weighted score for an agent across all criteria dimensions.
    /// </summary>
    private static (AgentState Agent, double Score, ScoreBreakdown Breakdown) CalculateWeightedScore(
        AgentState agent,
        DelegationCriteria criteria)
    {
        // Calculate capability score
        double capabilityScore = CalculateCapabilityScore(agent, criteria);

        // Calculate availability score
        double availabilityScore = agent.IsAvailable ? 1.0 : 0.3;

        // Success rate is already 0.0 to 1.0
        double successRateScore = agent.SuccessRate;

        // Calculate role match score
        double roleMatchScore = criteria.PreferredRole.HasValue &&
                                agent.Identity.Role == criteria.PreferredRole.Value
            ? 1.0
            : 0.5;

        // Calculate weighted sum
        double totalScore =
            (capabilityScore * CapabilityWeight) +
            (availabilityScore * AvailabilityWeight) +
            (successRateScore * SuccessRateWeight) +
            (roleMatchScore * RoleMatchWeight);

        ScoreBreakdown breakdown = new(capabilityScore, availabilityScore, successRateScore, roleMatchScore);

        return (agent, totalScore, breakdown);
    }

    /// <summary>
    /// Calculates the capability score component.
    /// </summary>
    private static double CalculateCapabilityScore(AgentState agent, DelegationCriteria criteria)
    {
        if (criteria.RequiredCapabilities.Count == 0)
        {
            // No specific requirements - return average of all capability proficiencies
            ImmutableList<AgentCapability> capabilities = agent.Identity.Capabilities;
            return capabilities.Count > 0
                ? capabilities.Average(c => c.Proficiency)
                : 0.5; // Neutral score if no capabilities defined
        }

        double totalProficiency = 0.0;

        foreach (string capability in criteria.RequiredCapabilities)
        {
            totalProficiency += agent.Identity.GetProficiencyFor(capability);
        }

        return totalProficiency / criteria.RequiredCapabilities.Count;
    }

    /// <summary>
    /// Builds the reasoning string for best-fit selection.
    /// </summary>
    private static string BuildBestFitReasoning(AgentState agent, ScoreBreakdown breakdown, double totalScore)
    {
        return $"Selected '{agent.Identity.Name}' with best-fit score {totalScore:F2}. " +
               $"Breakdown: Capability={breakdown.Capability:F2}, Availability={breakdown.Availability:F2}, " +
               $"SuccessRate={breakdown.SuccessRate:F2}, RoleMatch={breakdown.RoleMatch:F2}";
    }

    /// <summary>
    /// Internal record for tracking score component breakdown.
    /// </summary>
    private readonly record struct ScoreBreakdown(
        double Capability,
        double Availability,
        double SuccessRate,
        double RoleMatch);
}

/// <summary>
/// A delegation strategy that combines multiple strategies with configurable weights.
/// Aggregates scores from each strategy to produce a final selection.
/// </summary>
/// <remarks>
/// <para>Composite Algorithm:</para>
/// <list type="number">
///   <item>Execute each child strategy independently.</item>
///   <item>For each agent, collect scores from all strategies that selected it.</item>
///   <item>Calculate weighted average: Σ(strategy_weight × strategy_score) / Σ(weights)</item>
///   <item>Select agent with highest composite score.</item>
/// </list>
/// </remarks>
public sealed class CompositeStrategy : IDelegationStrategy
{
    private readonly IReadOnlyList<(IDelegationStrategy Strategy, double Weight)> _strategies;

    /// <inheritdoc />
    public string Name => "Composite";

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeStrategy"/> class.
    /// </summary>
    /// <param name="strategies">The weighted strategies to combine.</param>
    private CompositeStrategy(IReadOnlyList<(IDelegationStrategy Strategy, double Weight)> strategies)
    {
        _strategies = strategies;
    }

    /// <summary>
    /// Creates a new composite strategy from the specified weighted strategies.
    /// </summary>
    /// <param name="strategies">The strategies and their weights.</param>
    /// <returns>A new <see cref="CompositeStrategy"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="strategies"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when no strategies are provided.</exception>
    public static CompositeStrategy Create(params (IDelegationStrategy Strategy, double Weight)[] strategies)
    {
        ArgumentNullException.ThrowIfNull(strategies);

        if (strategies.Length == 0)
        {
            throw new ArgumentException("At least one strategy must be provided.", nameof(strategies));
        }

        foreach ((IDelegationStrategy strategy, double weight) in strategies)
        {
            ArgumentNullException.ThrowIfNull(strategy);

            if (weight <= 0.0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(strategies),
                    weight,
                    $"Strategy weight must be positive. Got {weight} for {strategy.Name}.");
            }
        }

        return new CompositeStrategy(strategies.ToList());
    }

    /// <inheritdoc />
    public DelegationResult SelectAgent(DelegationCriteria criteria, AgentTeam team)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(team);

        IReadOnlyList<DelegationResult> results = SelectAgents(criteria, team, 1);
        return results.Count > 0
            ? results[0]
            : DelegationResult.NoMatch("No agents selected by composite strategy.");
    }

    /// <inheritdoc />
    public IReadOnlyList<DelegationResult> SelectAgents(DelegationCriteria criteria, AgentTeam team, int count)
    {
        ArgumentNullException.ThrowIfNull(criteria);
        ArgumentNullException.ThrowIfNull(team);

        if (count <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), count, "Count must be greater than zero.");
        }

        // Collect results from all strategies
        Dictionary<Guid, List<(double Score, double Weight, string StrategyName)>> agentScores = new();
        double totalWeight = _strategies.Sum(s => s.Weight);

        foreach ((IDelegationStrategy strategy, double weight) in _strategies)
        {
            DelegationResult result = strategy.SelectAgent(criteria, team);

            if (result.HasMatch && result.SelectedAgentId.HasValue)
            {
                Guid agentId = result.SelectedAgentId.Value;

                if (!agentScores.ContainsKey(agentId))
                {
                    agentScores[agentId] = new List<(double, double, string)>();
                }

                agentScores[agentId].Add((result.MatchScore, weight, strategy.Name));
            }

            // Also consider alternatives
            foreach (Guid altId in result.Alternatives)
            {
                if (!agentScores.ContainsKey(altId))
                {
                    agentScores[altId] = new List<(double, double, string)>();
                }

                // Alternatives get a reduced score (50% of primary)
                agentScores[altId].Add((result.MatchScore * 0.5, weight * 0.5, strategy.Name));
            }
        }

        if (agentScores.Count == 0)
        {
            return Array.Empty<DelegationResult>();
        }

        // Calculate composite scores
        List<(Guid AgentId, double CompositeScore, string Contributions)> compositeScores = agentScores
            .Select(kvp =>
            {
                double weightedSum = kvp.Value.Sum(s => s.Score * s.Weight);
                double appliedWeight = kvp.Value.Sum(s => s.Weight);
                double compositeScore = weightedSum / totalWeight;

                string contributions = string.Join(", ",
                    kvp.Value.Select(s => $"{s.StrategyName}:{s.Score:F2}"));

                return (AgentId: kvp.Key, CompositeScore: compositeScore, Contributions: contributions);
            })
            .Where(x => x.CompositeScore >= criteria.MinProficiency)
            .OrderByDescending(x => x.CompositeScore)
            .ToList();

        List<DelegationResult> results = compositeScores
            .Take(count)
            .Select((x, index) =>
            {
                IReadOnlyList<Guid> alternatives = index == 0
                    ? compositeScores
                        .Skip(1)
                        .Take(3)
                        .Select(a => a.AgentId)
                        .ToList()
                    : Array.Empty<Guid>();

                string reasoning = $"Composite selection with score {x.CompositeScore:F2}. " +
                                   $"Strategy contributions: [{x.Contributions}]";

                return DelegationResult.Success(x.AgentId, reasoning, x.CompositeScore, alternatives);
            })
            .ToList();

        return results;
    }
}

/// <summary>
/// Factory class for creating common delegation strategy instances.
/// Provides convenient access to pre-configured strategies.
/// </summary>
public static class DelegationStrategyFactory
{
    /// <summary>
    /// Creates a capability-based delegation strategy.
    /// Selects agents based on their proficiency in required capabilities.
    /// </summary>
    /// <returns>A new <see cref="CapabilityBasedStrategy"/> instance.</returns>
    public static IDelegationStrategy ByCapability()
    {
        return new CapabilityBasedStrategy();
    }

    /// <summary>
    /// Creates a role-based delegation strategy.
    /// Selects agents based on role matching, with capability fallback.
    /// </summary>
    /// <returns>A new <see cref="RoleBasedStrategy"/> instance.</returns>
    public static IDelegationStrategy ByRole()
    {
        return new RoleBasedStrategy();
    }

    /// <summary>
    /// Creates a load-balancing delegation strategy.
    /// Selects the least busy available agent.
    /// </summary>
    /// <returns>A new <see cref="LoadBalancingStrategy"/> instance.</returns>
    public static IDelegationStrategy ByLoad()
    {
        return new LoadBalancingStrategy();
    }

    /// <summary>
    /// Creates a round-robin delegation strategy.
    /// Cycles through agents in order for fair distribution.
    /// </summary>
    /// <returns>A new <see cref="RoundRobinStrategy"/> instance.</returns>
    public static IDelegationStrategy RoundRobin()
    {
        return new RoundRobinStrategy();
    }

    /// <summary>
    /// Creates a best-fit delegation strategy.
    /// Uses weighted scoring across capability, availability, and success rate.
    /// </summary>
    /// <returns>A new <see cref="BestFitStrategy"/> instance.</returns>
    public static IDelegationStrategy BestFit()
    {
        return new BestFitStrategy();
    }

    /// <summary>
    /// Creates a composite delegation strategy from multiple weighted strategies.
    /// </summary>
    /// <param name="weighted">The strategies and their weights.</param>
    /// <returns>A new <see cref="CompositeStrategy"/> instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="weighted"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when no strategies are provided.</exception>
    public static IDelegationStrategy Composite(params (IDelegationStrategy Strategy, double Weight)[] weighted)
    {
        return CompositeStrategy.Create(weighted);
    }

    /// <summary>
    /// Creates a balanced composite strategy combining capability, load, and best-fit approaches.
    /// </summary>
    /// <returns>A pre-configured <see cref="CompositeStrategy"/> with balanced weights.</returns>
    public static IDelegationStrategy Balanced()
    {
        return CompositeStrategy.Create(
            (new CapabilityBasedStrategy(), 0.35),
            (new LoadBalancingStrategy(), 0.35),
            (new BestFitStrategy(), 0.30));
    }
}
