// <copyright file="BanditPolicy.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using LangChainPipeline.Core.Monads;
using LangChainPipeline.Domain.Environment;

namespace LangChainPipeline.Domain.Reinforcement;

/// <summary>
/// Multi-armed bandit policy using Upper Confidence Bound (UCB) algorithm.
/// Balances exploration and exploitation without requiring state information.
/// </summary>
public sealed class BanditPolicy : IPolicy
{
    private readonly double explorationFactor;
    private readonly ConcurrentDictionary<string, ActionStats> actionStats;
    private int totalTrials;

    /// <summary>
    /// Initializes a new instance of the <see cref="BanditPolicy"/> class.
    /// </summary>
    /// <param name="explorationFactor">UCB exploration factor (typically √2)</param>
    public BanditPolicy(double explorationFactor = 1.414)
    {
        this.explorationFactor = explorationFactor;
        this.actionStats = new ConcurrentDictionary<string, ActionStats>();
        this.totalTrials = 0;
    }

    /// <inheritdoc/>
    public ValueTask<Result<EnvironmentAction>> SelectActionAsync(
        EnvironmentState state,
        IReadOnlyList<EnvironmentAction> availableActions,
        CancellationToken cancellationToken = default)
    {
        if (availableActions == null || availableActions.Count == 0)
        {
            return ValueTask.FromResult(Result<EnvironmentAction>.Failure("No available actions"));
        }

        // UCB selection: argmax_a [Q(a) + c * sqrt(ln(N) / n(a))]
        EnvironmentAction? bestAction = null;
        var bestUcb = double.NegativeInfinity;

        var totalTrialsSnapshot = this.totalTrials;

        foreach (var action in availableActions)
        {
            var actionKey = this.GetActionKey(action);
            var stats = this.actionStats.GetOrAdd(actionKey, _ => new ActionStats());

            double ucbValue;
            if (stats.Count == 0)
            {
                // Never tried this action - give it priority
                ucbValue = double.PositiveInfinity;
            }
            else
            {
                var exploitation = stats.AverageReward;
                var exploration = this.explorationFactor * Math.Sqrt(Math.Log(totalTrialsSnapshot + 1) / stats.Count);
                ucbValue = exploitation + exploration;
            }

            if (ucbValue > bestUcb)
            {
                bestUcb = ucbValue;
                bestAction = action;
            }
        }

        return ValueTask.FromResult(Result<EnvironmentAction>.Success(bestAction!));
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> UpdateAsync(
        EnvironmentState state,
        EnvironmentAction action,
        Observation observation,
        CancellationToken cancellationToken = default)
    {
        var actionKey = this.GetActionKey(action);
        var stats = this.actionStats.GetOrAdd(actionKey, _ => new ActionStats());

        // Update running average
        stats.Update(observation.Reward);
        Interlocked.Increment(ref this.totalTrials);

        return ValueTask.FromResult(Result<Unit>.Success(Unit.Value));
    }

    private string GetActionKey(EnvironmentAction action)
    {
        if (action.Parameters == null || action.Parameters.Count == 0)
        {
            return action.ActionType;
        }

        var keys = action.Parameters.Keys.OrderBy(k => k);
        var values = keys.Select(k => $"{k}:{action.Parameters[k]}");
        return $"{action.ActionType}({string.Join(",", values)})";
    }

    private sealed class ActionStats
    {
        private double totalReward;
        private int count;

        public double AverageReward => this.count > 0 ? this.totalReward / this.count : 0.0;

        public int Count => this.count;

        public void Update(double reward)
        {
            lock (this)
            {
                this.totalReward += reward;
                this.count++;
            }
        }
    }
}
