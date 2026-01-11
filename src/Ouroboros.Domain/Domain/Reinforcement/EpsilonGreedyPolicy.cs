// <copyright file="EpsilonGreedyPolicy.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using Ouroboros.Core.Monads;
using Ouroboros.Domain.Environment;
using Unit = Ouroboros.Core.Monads.Unit;

namespace Ouroboros.Domain.Reinforcement;

/// <summary>
/// Epsilon-greedy policy for exploration-exploitation balance.
/// Selects random actions with probability epsilon, otherwise chooses greedily.
/// </summary>
public sealed class EpsilonGreedyPolicy : IPolicy
{
    private readonly double epsilon;
    private readonly Random random;
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, double>> qValues;

    /// <summary>
    /// Initializes a new instance of the <see cref="EpsilonGreedyPolicy"/> class.
    /// </summary>
    /// <param name="epsilon">Exploration rate (0-1)</param>
    /// <param name="seed">Random seed for reproducibility</param>
    public EpsilonGreedyPolicy(double epsilon = 0.1, int? seed = null)
    {
        if (epsilon < 0.0 || epsilon > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(epsilon), "Epsilon must be between 0 and 1");
        }

        this.epsilon = epsilon;
        this.random = seed.HasValue ? new Random(seed.Value) : new Random();
        this.qValues = new ConcurrentDictionary<string, ConcurrentDictionary<string, double>>();
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

        EnvironmentAction selectedAction;

        // Epsilon-greedy selection
        if (this.random.NextDouble() < this.epsilon)
        {
            // Explore: random action
            var index = this.random.Next(availableActions.Count);
            selectedAction = availableActions[index];
        }
        else
        {
            // Exploit: greedy action
            selectedAction = this.GetGreedyAction(state, availableActions);
        }

        return ValueTask.FromResult(Result<EnvironmentAction>.Success(selectedAction));
    }

    /// <inheritdoc/>
    public ValueTask<Result<Ouroboros.Core.Monads.Unit>> UpdateAsync(
        EnvironmentState state,
        EnvironmentAction action,
        Observation observation,
        CancellationToken cancellationToken = default)
    {
        var stateKey = this.GetStateKey(state);
        var actionKey = this.GetActionKey(action);

        // Simple Q-learning update
        var stateActions = this.qValues.GetOrAdd(stateKey, _ => new ConcurrentDictionary<string, double>());
        var currentQ = stateActions.GetOrAdd(actionKey, 0.0);

        // Q(s,a) = Q(s,a) + α[r + γ*max(Q(s',a')) - Q(s,a)]
        // Using α=0.1, γ=0.9 for simplicity
        const double alpha = 0.1;
        const double gamma = 0.9;

        var maxNextQ = observation.IsTerminal ? 0.0 : this.GetMaxQValue(observation.State);
        var newQ = currentQ + (alpha * ((observation.Reward + (gamma * maxNextQ)) - currentQ));

        stateActions[actionKey] = newQ;

        return ValueTask.FromResult(Result<Ouroboros.Core.Monads.Unit>.Success(Ouroboros.Core.Monads.Unit.Value));
    }

    private EnvironmentAction GetGreedyAction(
        EnvironmentState state,
        IReadOnlyList<EnvironmentAction> availableActions)
    {
        var stateKey = this.GetStateKey(state);
        var stateActions = this.qValues.GetOrAdd(stateKey, _ => new ConcurrentDictionary<string, double>());

        EnvironmentAction? bestAction = null;
        var bestValue = double.NegativeInfinity;

        foreach (var action in availableActions)
        {
            var actionKey = this.GetActionKey(action);
            var qValue = stateActions.GetOrAdd(actionKey, 0.0);

            if (qValue > bestValue)
            {
                bestValue = qValue;
                bestAction = action;
            }
        }

        // If no best action found, return random
        return bestAction ?? availableActions[this.random.Next(availableActions.Count)];
    }

    private double GetMaxQValue(EnvironmentState state)
    {
        var stateKey = this.GetStateKey(state);
        if (!this.qValues.TryGetValue(stateKey, out var stateActions) || stateActions.IsEmpty)
        {
            return 0.0;
        }

        return stateActions.Values.Max();
    }

    private string GetStateKey(EnvironmentState state)
    {
        // Simple serialization - in production, use proper hashing
        var keys = state.StateData.Keys.OrderBy(k => k);
        var values = keys.Select(k => $"{k}:{state.StateData[k]}");
        return string.Join(";", values);
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
}
