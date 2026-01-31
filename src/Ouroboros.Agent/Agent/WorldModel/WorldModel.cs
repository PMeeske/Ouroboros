// <copyright file="WorldModel.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Agent.WorldModel;

using Microsoft.Extensions.Logging;
using Ouroboros.Core.Monads;
using Ouroboros.Domain.Embodied;
using Ouroboros.Domain.Reinforcement;

/// <summary>
/// Memory-based world model using k-nearest neighbor prediction.
/// Stores observed transitions and uses similarity matching for state prediction.
/// </summary>
public sealed class WorldModel : IWorldModel
{
    private readonly ILogger<WorldModel>? logger;
    private readonly List<StateTransition> transitionMemory;
    private readonly int maxMemorySize;
    private readonly int kNeighbors;
    private readonly Dictionary<string, (int predictions, int correct)> accuracyTracker;
    private readonly object lockObject = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="WorldModel"/> class.
    /// </summary>
    /// <param name="maxMemorySize">Maximum number of transitions to store</param>
    /// <param name="kNeighbors">Number of nearest neighbors to use for prediction</param>
    /// <param name="logger">Optional logger for diagnostics</param>
    public WorldModel(
        int maxMemorySize = 10000,
        int kNeighbors = 5,
        ILogger<WorldModel>? logger = null)
    {
        if (maxMemorySize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxMemorySize), "Must be positive");
        }

        if (kNeighbors <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(kNeighbors), "Must be positive");
        }

        this.maxMemorySize = maxMemorySize;
        this.kNeighbors = kNeighbors;
        this.logger = logger;
        this.transitionMemory = new List<StateTransition>();
        this.accuracyTracker = new Dictionary<string, (int, int)>();
    }

    /// <inheritdoc/>
    public Task<Result<PredictedState, string>> PredictAsync(
        SensorState currentState,
        EmbodiedAction action,
        CancellationToken ct = default)
    {
        if (currentState == null)
        {
            return Task.FromResult(Result<PredictedState, string>.Failure("Current state cannot be null"));
        }

        if (action == null)
        {
            return Task.FromResult(Result<PredictedState, string>.Failure("Action cannot be null"));
        }

        lock (this.lockObject)
        {
            if (this.transitionMemory.Count == 0)
            {
                return Task.FromResult(Result<PredictedState, string>.Failure("No experience available for prediction"));
            }

            // Find k-nearest neighbors based on state-action similarity
            var neighbors = this.FindNearestNeighbors(currentState, action);

            if (neighbors.Count == 0)
            {
                return Task.FromResult(Result<PredictedState, string>.Failure("No similar transitions found"));
            }

            // Aggregate predictions from neighbors
            var predictedState = this.AggregatePredictions(neighbors, currentState, action);

            var confidence = this.CalculateConfidence(neighbors);
            var metadata = new Dictionary<string, object>
            {
                ["neighbors_count"] = neighbors.Count,
                ["average_similarity"] = neighbors.Average(n => n.similarity),
            };

            var predicted = new PredictedState(
                predictedState.state,
                predictedState.reward,
                predictedState.terminal,
                confidence,
                metadata);

            return Task.FromResult(Result<PredictedState, string>.Success(predicted));
        }
    }

    /// <inheritdoc/>
    public Task<Result<Unit, string>> UpdateFromExperienceAsync(
        IReadOnlyList<EmbodiedTransition> transitions,
        CancellationToken ct = default)
    {
        if (transitions == null || transitions.Count == 0)
        {
            return Task.FromResult(Result<Unit, string>.Failure("Transitions list cannot be null or empty"));
        }

        try
        {
            lock (this.lockObject)
            {
                foreach (var transition in transitions)
                {
                    if (transition == null)
                    {
                        continue;
                    }

                    var stateTransition = new StateTransition(
                        transition.StateBefore,
                        transition.Action,
                        transition.StateAfter,
                        transition.Reward,
                        transition.Terminal,
                        DateTime.UtcNow);

                    this.transitionMemory.Add(stateTransition);

                    // Update accuracy tracking
                    this.UpdateAccuracyMetrics(transition);
                }

                // Maintain memory size limit (FIFO)
                while (this.transitionMemory.Count > this.maxMemorySize)
                {
                    this.transitionMemory.RemoveAt(0);
                }

                this.logger?.LogInformation(
                    "Updated world model with {Count} transitions. Total memory: {Total}",
                    transitions.Count,
                    this.transitionMemory.Count);
            }

            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }
        catch (Exception ex)
        {
            this.logger?.LogError(ex, "Failed to update world model from experience");
            return Task.FromResult(Result<Unit, string>.Failure($"Update failed: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public async Task<Result<List<EmbodiedAction>, string>> PlanWithModelAsync(
        SensorState current,
        string goal,
        int horizon = 10,
        CancellationToken ct = default)
    {
        if (current == null)
        {
            return Result<List<EmbodiedAction>, string>.Failure("Current state cannot be null");
        }

        if (string.IsNullOrWhiteSpace(goal))
        {
            return Result<List<EmbodiedAction>, string>.Failure("Goal cannot be null or empty");
        }

        if (horizon <= 0)
        {
            return Result<List<EmbodiedAction>, string>.Failure("Horizon must be positive");
        }

        lock (this.lockObject)
        {
            if (this.transitionMemory.Count == 0)
            {
                return Result<List<EmbodiedAction>, string>.Failure("No experience available for planning");
            }
        }

        try
        {
            // Beam search with greedy action selection
            var plan = await this.BeamSearchAsync(current, goal, horizon, ct);

            this.logger?.LogInformation(
                "Generated plan with {Count} actions for goal: {Goal}",
                plan.Count,
                goal);

            return Result<List<EmbodiedAction>, string>.Success(plan);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            this.logger?.LogError(ex, "Planning failed for goal: {Goal}", goal);
            return Result<List<EmbodiedAction>, string>.Failure($"Planning failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public Task<double> GetUncertaintyAsync(SensorState state, EmbodiedAction action)
    {
        if (state == null || action == null)
        {
            return Task.FromResult(1.0); // Maximum uncertainty
        }

        lock (this.lockObject)
        {
            if (this.transitionMemory.Count == 0)
            {
                return Task.FromResult(1.0); // No data = high uncertainty
            }

            var neighbors = this.FindNearestNeighbors(state, action);

            if (neighbors.Count == 0)
            {
                return Task.FromResult(1.0); // No similar states = high uncertainty
            }

            // Uncertainty based on:
            // 1. Number of neighbors found (more = less uncertain)
            // 2. Average similarity (higher = less uncertain)
            // 3. Variance in predicted outcomes (higher = more uncertain)

            var avgSimilarity = neighbors.Average(n => n.similarity);
            var neighborRatio = Math.Min(1.0, neighbors.Count / (double)this.kNeighbors);

            // Calculate outcome variance
            var rewards = neighbors.Select(n => n.transition.Reward).ToList();
            var rewardVariance = rewards.Count > 1 ? this.CalculateVariance(rewards) : 0.0;
            var normalizedVariance = Math.Min(1.0, rewardVariance / 10.0); // Normalize to 0-1

            // Combine factors: lower similarity, fewer neighbors, higher variance = more uncertainty
            var uncertainty = 1.0 - (avgSimilarity * neighborRatio * (1.0 - normalizedVariance));

            return Task.FromResult(Math.Clamp(uncertainty, 0.0, 1.0));
        }
    }

    /// <inheritdoc/>
    public async Task<Result<List<PredictedState>, string>> SimulateTrajectoryAsync(
        SensorState initialState,
        IReadOnlyList<EmbodiedAction> actions,
        CancellationToken ct = default)
    {
        if (initialState == null)
        {
            return Result<List<PredictedState>, string>.Failure("Initial state cannot be null");
        }

        if (actions == null || actions.Count == 0)
        {
            return Result<List<PredictedState>, string>.Failure("Actions list cannot be null or empty");
        }

        try
        {
            var trajectory = new List<PredictedState>();
            var currentState = initialState;

            foreach (var action in actions)
            {
                ct.ThrowIfCancellationRequested();

                var predictionResult = await this.PredictAsync(currentState, action, ct);

                if (predictionResult.IsFailure)
                {
                    return Result<List<PredictedState>, string>.Failure(
                        $"Trajectory simulation failed at step {trajectory.Count}: {predictionResult.Error}");
                }

                var predicted = predictionResult.Value;
                trajectory.Add(predicted);

                // Stop if terminal state reached
                if (predicted.Terminal)
                {
                    this.logger?.LogInformation(
                        "Trajectory simulation terminated early at step {Step} (terminal state)",
                        trajectory.Count);
                    break;
                }

                currentState = predicted.State;
            }

            return Result<List<PredictedState>, string>.Success(trajectory);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            this.logger?.LogError(ex, "Trajectory simulation failed");
            return Result<List<PredictedState>, string>.Failure($"Simulation failed: {ex.Message}");
        }
    }

    private List<(StateTransition transition, double similarity)> FindNearestNeighbors(
        SensorState state,
        EmbodiedAction action)
    {
        var similarities = new List<(StateTransition transition, double similarity)>();

        foreach (var transition in this.transitionMemory)
        {
            var similarity = this.CalculateSimilarity(state, action, transition);
            similarities.Add((transition, similarity));
        }

        // Return k nearest neighbors sorted by similarity (descending)
        return similarities
            .OrderByDescending(s => s.similarity)
            .Take(this.kNeighbors)
            .ToList();
    }

    private double CalculateSimilarity(
        SensorState state,
        EmbodiedAction action,
        StateTransition transition)
    {
        // Weighted similarity based on multiple factors
        var positionSim = this.PositionSimilarity(state.Position, transition.FromState.Position);
        var velocitySim = this.VelocitySimilarity(state.Velocity, transition.FromState.Velocity);
        var actionSim = this.ActionSimilarity(action, transition.Action);
        var visualSim = this.VisualSimilarity(state.VisualObservation, transition.FromState.VisualObservation);

        // Weighted combination (position and action are most important)
        return (0.4 * positionSim) + (0.3 * actionSim) + (0.2 * velocitySim) + (0.1 * visualSim);
    }

    private double PositionSimilarity(Vector3 a, Vector3 b)
    {
        var distance = (a - b).Magnitude();
        return Math.Exp(-distance / 10.0); // Exponential decay with scale factor
    }

    private double VelocitySimilarity(Vector3 a, Vector3 b)
    {
        var distance = (a - b).Magnitude();
        return Math.Exp(-distance / 5.0);
    }

    private double ActionSimilarity(EmbodiedAction a, EmbodiedAction b)
    {
        var movementDist = (a.Movement - b.Movement).Magnitude();
        var rotationDist = (a.Rotation - b.Rotation).Magnitude();
        return Math.Exp(-(movementDist + rotationDist) / 2.0);
    }

    private double VisualSimilarity(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length == 0 || a.Length != b.Length)
        {
            return 0.5; // Neutral similarity if incompatible
        }

        // Cosine similarity for visual features
        var dotProduct = 0.0;
        var normA = 0.0;
        var normB = 0.0;

        for (var i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        var denominator = Math.Sqrt(normA * normB);
        return denominator > 0 ? (dotProduct / denominator + 1.0) / 2.0 : 0.5; // Normalize to [0,1]
    }

    private (SensorState state, double reward, bool terminal) AggregatePredictions(
        List<(StateTransition transition, double similarity)> neighbors,
        SensorState currentState,
        EmbodiedAction action)
    {
        // Weighted average of neighbor outcomes based on similarity
        var totalWeight = neighbors.Sum(n => n.similarity);

        if (totalWeight == 0)
        {
            // Fallback: simple forward projection
            return (this.ProjectForward(currentState, action), 0.0, false);
        }

        // Weighted position
        var weightedX = 0f;
        var weightedY = 0f;
        var weightedZ = 0f;
        var weightedReward = 0.0;
        var weightedTerminal = 0.0;

        foreach (var (transition, similarity) in neighbors)
        {
            var weight = (float)(similarity / totalWeight);
            var nextPos = transition.ToState.Position;

            weightedX += nextPos.X * weight;
            weightedY += nextPos.Y * weight;
            weightedZ += nextPos.Z * weight;
            weightedReward += transition.Reward * similarity / totalWeight;
            weightedTerminal += (transition.Terminal ? 1.0 : 0.0) * similarity / totalWeight;
        }

        var predictedPosition = new Vector3(weightedX, weightedY, weightedZ);

        // Use the most similar neighbor's full state as template
        var template = neighbors[0].transition.ToState;

        var predictedState = new SensorState(
            predictedPosition,
            template.Rotation,
            template.Velocity,
            template.VisualObservation,
            template.ProprioceptiveState,
            template.CustomSensors,
            DateTime.UtcNow);

        return (predictedState, weightedReward, weightedTerminal > 0.5);
    }

    private SensorState ProjectForward(SensorState current, EmbodiedAction action)
    {
        // Simple kinematic forward model
        var newPosition = current.Position + action.Movement;
        var newVelocity = action.Movement; // Simplified

        return new SensorState(
            newPosition,
            current.Rotation,
            newVelocity,
            current.VisualObservation,
            current.ProprioceptiveState,
            current.CustomSensors,
            DateTime.UtcNow);
    }

    private double CalculateConfidence(List<(StateTransition transition, double similarity)> neighbors)
    {
        if (neighbors.Count == 0)
        {
            return 0.0;
        }

        // Confidence based on average similarity and number of neighbors
        var avgSimilarity = neighbors.Average(n => n.similarity);
        var neighborRatio = Math.Min(1.0, neighbors.Count / (double)this.kNeighbors);

        return avgSimilarity * neighborRatio;
    }

    private async Task<List<EmbodiedAction>> BeamSearchAsync(
        SensorState initialState,
        string goal,
        int horizon,
        CancellationToken ct)
    {
        const int beamWidth = 3;
        var bestPlan = new List<EmbodiedAction>();
        var bestScore = double.NegativeInfinity;

        // Generate candidate actions
        var candidateActions = this.GenerateCandidateActions();

        // Greedy beam search
        for (var depth = 0; depth < horizon; depth++)
        {
            ct.ThrowIfCancellationRequested();

            var currentState = initialState;

            // For each action at this depth
            foreach (var action in candidateActions.Take(beamWidth))
            {
                var predictResult = await this.PredictAsync(currentState, action, ct);

                if (predictResult.IsSuccess)
                {
                    var predicted = predictResult.Value;
                    var score = predicted.Reward - (depth * 0.1); // Prefer shorter plans

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestPlan = new List<EmbodiedAction> { action };
                    }

                    // Early termination if goal-like state reached
                    if (predicted.Terminal || predicted.Reward > 0.9)
                    {
                        return bestPlan;
                    }
                }
            }

            if (bestPlan.Count > 0)
            {
                break; // Found a reasonable action
            }
        }

        // Fallback to best observed action if no plan found
        if (bestPlan.Count == 0 && candidateActions.Count > 0)
        {
            bestPlan.Add(candidateActions[0]);
        }

        return bestPlan;
    }

    private List<EmbodiedAction> GenerateCandidateActions()
    {
        lock (this.lockObject)
        {
            if (this.transitionMemory.Count == 0)
            {
                // Default actions if no experience
                return new List<EmbodiedAction>
                {
                    EmbodiedAction.Move(Vector3.UnitX, "Forward"),
                    EmbodiedAction.Move(new Vector3(-1f, 0f, 0f), "Backward"),
                    EmbodiedAction.Rotate(Vector3.UnitY, "TurnRight"),
                    EmbodiedAction.NoOp(),
                };
            }

            // Sample diverse actions from experience
            var actionClusters = this.transitionMemory
                .GroupBy(t => this.QuantizeAction(t.Action))
                .OrderByDescending(g => g.Average(t => t.Reward))
                .Take(10)
                .Select(g => g.First().Action)
                .ToList();

            return actionClusters.Count > 0 ? actionClusters : new List<EmbodiedAction> { EmbodiedAction.NoOp() };
        }
    }

    private string QuantizeAction(EmbodiedAction action)
    {
        // Simple action quantization for clustering
        var mx = Math.Round(action.Movement.X, 1);
        var my = Math.Round(action.Movement.Y, 1);
        var mz = Math.Round(action.Movement.Z, 1);
        return $"M({mx},{my},{mz})";
    }

    private void UpdateAccuracyMetrics(EmbodiedTransition transition)
    {
        // Track prediction accuracy for future uncertainty estimation
        var key = "global";

        if (!this.accuracyTracker.ContainsKey(key))
        {
            this.accuracyTracker[key] = (0, 0);
        }

        var (predictions, correct) = this.accuracyTracker[key];
        this.accuracyTracker[key] = (predictions + 1, correct + 1);
    }

    private double CalculateVariance(List<double> values)
    {
        if (values.Count < 2)
        {
            return 0.0;
        }

        var mean = values.Average();
        var sumSquaredDiff = values.Sum(v => Math.Pow(v - mean, 2));
        return sumSquaredDiff / values.Count;
    }
}
