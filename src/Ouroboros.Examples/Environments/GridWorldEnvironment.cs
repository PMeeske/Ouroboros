// <copyright file="GridWorldEnvironment.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Core.Monads;
using Ouroboros.Domain.Environment;

namespace Ouroboros.Examples.Environments;

/// <summary>
/// Simple grid world environment for testing RL algorithms.
/// Agent navigates a grid to reach a goal while avoiding obstacles.
/// </summary>
public sealed class GridWorldEnvironment : IEnvironmentActor
{
    private readonly int width;
    private readonly int height;
    private readonly (int X, int Y) goalPosition;
    private readonly HashSet<(int X, int Y)> obstacles;
    private (int X, int Y) agentPosition;
    private int stepCount;
    private const int MaxSteps = 100;

    /// <summary>
    /// Initializes a new instance of the <see cref="GridWorldEnvironment"/> class.
    /// </summary>
    /// <param name="width">Width of the grid</param>
    /// <param name="height">Height of the grid</param>
    /// <param name="startPosition">Starting position of the agent</param>
    /// <param name="goalPosition">Goal position</param>
    /// <param name="obstacles">Set of obstacle positions</param>
    public GridWorldEnvironment(
        int width = 5,
        int height = 5,
        (int X, int Y)? startPosition = null,
        (int X, int Y)? goalPosition = null,
        HashSet<(int X, int Y)>? obstacles = null)
    {
        this.width = width;
        this.height = height;
        this.agentPosition = startPosition ?? (0, 0);
        this.goalPosition = goalPosition ?? (width - 1, height - 1);
        this.obstacles = obstacles ?? new HashSet<(int X, int Y)>();
        this.stepCount = 0;
    }

    /// <inheritdoc/>
    public ValueTask<Result<EnvironmentState>> GetStateAsync(CancellationToken cancellationToken = default)
    {
        var state = this.CreateState();
        return ValueTask.FromResult(Result<EnvironmentState>.Success(state));
    }

    /// <inheritdoc/>
    public ValueTask<Result<Observation>> ExecuteActionAsync(
        EnvironmentAction action,
        CancellationToken cancellationToken = default)
    {
        this.stepCount++;

        var oldPosition = this.agentPosition;

        // Execute the action
        switch (action.ActionType.ToUpperInvariant())
        {
            case "UP":
                this.agentPosition = (this.agentPosition.X, this.agentPosition.Y - 1);
                break;
            case "DOWN":
                this.agentPosition = (this.agentPosition.X, this.agentPosition.Y + 1);
                break;
            case "LEFT":
                this.agentPosition = (this.agentPosition.X - 1, this.agentPosition.Y);
                break;
            case "RIGHT":
                this.agentPosition = (this.agentPosition.X + 1, this.agentPosition.Y);
                break;
            default:
                return ValueTask.FromResult(Result<Observation>.Failure($"Invalid action: {action.ActionType}"));
        }

        // Check boundaries
        if (this.agentPosition.X < 0 || this.agentPosition.X >= this.width ||
            this.agentPosition.Y < 0 || this.agentPosition.Y >= this.height)
        {
            this.agentPosition = oldPosition; // Revert invalid move
        }

        // Check obstacles
        if (this.obstacles.Contains(this.agentPosition))
        {
            this.agentPosition = oldPosition; // Revert collision
        }

        // Calculate reward
        double reward;
        bool isTerminal;

        if (this.agentPosition == this.goalPosition)
        {
            reward = 100.0; // Large positive reward for reaching goal
            isTerminal = true;
        }
        else if (this.stepCount >= MaxSteps)
        {
            reward = -10.0; // Penalty for timeout
            isTerminal = true;
        }
        else
        {
            // Small negative reward for each step (encourages efficiency)
            reward = -1.0;
            isTerminal = false;
        }

        var newState = this.CreateState(isTerminal);
        var info = new Dictionary<string, object>
        {
            ["step_count"] = this.stepCount,
            ["distance_to_goal"] = this.GetDistanceToGoal(),
        };

        var observation = new Observation(newState, reward, isTerminal, info);

        return ValueTask.FromResult(Result<Observation>.Success(observation));
    }

    /// <inheritdoc/>
    public ValueTask<Result<EnvironmentState>> ResetAsync(CancellationToken cancellationToken = default)
    {
        this.agentPosition = (0, 0);
        this.stepCount = 0;
        var state = this.CreateState();
        return ValueTask.FromResult(Result<EnvironmentState>.Success(state));
    }

    /// <inheritdoc/>
    public ValueTask<Result<IReadOnlyList<EnvironmentAction>>> GetAvailableActionsAsync(
        CancellationToken cancellationToken = default)
    {
        var actions = new List<EnvironmentAction>
        {
            new("UP"),
            new("DOWN"),
            new("LEFT"),
            new("RIGHT"),
        };

        return ValueTask.FromResult(Result<IReadOnlyList<EnvironmentAction>>.Success(actions));
    }

    private EnvironmentState CreateState(bool isTerminal = false)
    {
        var stateData = new Dictionary<string, object>
        {
            ["agent_x"] = this.agentPosition.X,
            ["agent_y"] = this.agentPosition.Y,
            ["goal_x"] = this.goalPosition.X,
            ["goal_y"] = this.goalPosition.Y,
            ["step_count"] = this.stepCount,
        };

        return new EnvironmentState(stateData, isTerminal);
    }

    private double GetDistanceToGoal()
    {
        // Manhattan distance
        return Math.Abs(this.agentPosition.X - this.goalPosition.X) +
               Math.Abs(this.agentPosition.Y - this.goalPosition.Y);
    }
}
