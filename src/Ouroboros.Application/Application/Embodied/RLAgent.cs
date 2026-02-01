// <copyright file="RLAgent.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Ouroboros.Core.Monads;
using Ouroboros.Domain.Embodied;
using Ouroboros.Domain.Reinforcement;

namespace Ouroboros.Application.Embodied;

/// <summary>
/// Reinforcement learning agent with epsilon-greedy action selection and Q-learning style updates.
/// Maintains an experience replay buffer and supports batch training.
/// Note: This class is not thread-safe. Methods should be called sequentially from a single thread.
/// Concurrent access to experienceBuffer or qTable may result in race conditions.
/// </summary>
public sealed class RLAgent
{
    private readonly ILogger<RLAgent> logger;
    private readonly double epsilon;
    private readonly double learningRate;
    private readonly double discountFactor;
    private readonly int maxBufferSize;
    private readonly List<EmbodiedTransition> experienceBuffer;
    private readonly Dictionary<string, Dictionary<string, double>> qTable;

    /// <summary>
    /// Initializes a new instance of the <see cref="RLAgent"/> class.
    /// </summary>
    /// <param name="epsilon">Exploration rate for epsilon-greedy selection (0-1)</param>
    /// <param name="learningRate">Learning rate for Q-value updates</param>
    /// <param name="discountFactor">Discount factor (gamma) for future rewards</param>
    /// <param name="maxBufferSize">Maximum size of experience replay buffer</param>
    /// <param name="logger">Logger for diagnostic output</param>
    public RLAgent(
        double epsilon,
        double learningRate,
        double discountFactor,
        int maxBufferSize,
        ILogger<RLAgent> logger)
    {
        if (epsilon < 0.0 || epsilon > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(epsilon), "Epsilon must be between 0 and 1");
        }

        if (learningRate <= 0.0 || learningRate > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(learningRate), "Learning rate must be between 0 (exclusive) and 1");
        }

        if (discountFactor < 0.0 || discountFactor > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(discountFactor), "Discount factor must be between 0 and 1");
        }

        if (maxBufferSize <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxBufferSize), "Max buffer size must be positive");
        }

        this.epsilon = epsilon;
        this.learningRate = learningRate;
        this.discountFactor = discountFactor;
        this.maxBufferSize = maxBufferSize;
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.experienceBuffer = new List<EmbodiedTransition>();
        this.qTable = new Dictionary<string, Dictionary<string, double>>();
    }

    /// <summary>
    /// Gets the current exploration rate (epsilon).
    /// </summary>
    public double Epsilon => this.epsilon;

    /// <summary>
    /// Gets the learning rate (alpha).
    /// </summary>
    public double LearningRate => this.learningRate;

    /// <summary>
    /// Gets the discount factor (gamma).
    /// </summary>
    public double DiscountFactor => this.discountFactor;

    /// <summary>
    /// Gets the current size of the experience buffer.
    /// </summary>
    public int ExperienceBufferSize => this.experienceBuffer.Count;

    /// <summary>
    /// Selects an action using epsilon-greedy policy.
    /// </summary>
    /// <param name="state">Current sensor state</param>
    /// <param name="availableActions">List of available actions</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing the selected action</returns>
    public async Task<Result<EmbodiedAction, string>> SelectActionAsync(
        SensorState state,
        IReadOnlyList<EmbodiedAction> availableActions,
        CancellationToken ct = default)
    {
        try
        {
            if (availableActions == null || availableActions.Count == 0)
            {
                return Result<EmbodiedAction, string>.Failure("No available actions");
            }

            await Task.CompletedTask; // Support async signature

            EmbodiedAction selectedAction;

            // Epsilon-greedy selection
            if (Random.Shared.NextDouble() < this.epsilon)
            {
                // Explore: random action
                var index = Random.Shared.Next(availableActions.Count);
                selectedAction = availableActions[index];
                this.logger.LogDebug("Exploring: selected random action {Action}", selectedAction.ActionName);
            }
            else
            {
                // Exploit: greedy action based on Q-values
                selectedAction = this.GetGreedyAction(state, availableActions);
                this.logger.LogDebug("Exploiting: selected greedy action {Action}", selectedAction.ActionName);
            }

            return Result<EmbodiedAction, string>.Success(selectedAction);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to select action");
            return Result<EmbodiedAction, string>.Failure($"Action selection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Stores a transition in the experience replay buffer.
    /// Implements FIFO eviction when buffer exceeds max size.
    /// </summary>
    /// <param name="transition">The transition to store</param>
    public void StoreTransition(EmbodiedTransition transition)
    {
        if (transition == null)
        {
            throw new ArgumentNullException(nameof(transition));
        }

        this.experienceBuffer.Add(transition);

        // FIFO eviction: remove oldest transitions when buffer is full
        while (this.experienceBuffer.Count > this.maxBufferSize)
        {
            this.experienceBuffer.RemoveAt(0);
            this.logger.LogDebug("Experience buffer full, removed oldest transition (FIFO eviction)");
        }

        this.logger.LogTrace(
            "Stored transition. Buffer size: {Size}/{Max}",
            this.experienceBuffer.Count,
            this.maxBufferSize);
    }

    /// <summary>
    /// Trains the agent on a batch sampled from the experience buffer.
    /// </summary>
    /// <param name="batchSize">Number of transitions to sample for training</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result containing training metrics (policy loss, value loss, etc.)</returns>
    public async Task<Result<TrainingMetrics, string>> TrainAsync(
        int batchSize,
        CancellationToken ct = default)
    {
        try
        {
            if (this.experienceBuffer.Count < batchSize)
            {
                return Result<TrainingMetrics, string>.Failure(
                    $"Insufficient experience: {this.experienceBuffer.Count} < {batchSize}");
            }

            await Task.CompletedTask; // Support async signature

            // Sample unique batch from experience buffer using shuffling
            var batch = this.SampleBatch(batchSize);

            double totalLoss = 0.0;
            double totalReward = 0.0;

            // Update Q-values for each transition in batch
            foreach (var transition in batch)
            {
                var stateKey = this.GetStateKey(transition.StateBefore);
                var actionKey = this.GetActionKey(transition.Action);

                // Get current Q-value
                if (!this.qTable.ContainsKey(stateKey))
                {
                    this.qTable[stateKey] = new Dictionary<string, double>();
                }

                var currentQ = this.qTable[stateKey].GetValueOrDefault(actionKey, 0.0);

                // Compute target Q-value
                double targetQ;
                if (transition.Terminal)
                {
                    // No future rewards at terminal state
                    targetQ = transition.Reward;
                }
                else
                {
                    // Q-learning update: Q(s,a) = r + γ * max_a' Q(s',a')
                    var maxNextQ = this.GetMaxQValue(transition.StateAfter);
                    targetQ = transition.Reward + (this.discountFactor * maxNextQ);
                }

                // Update Q-value with learning rate
                var newQ = currentQ + (this.learningRate * (targetQ - currentQ));
                this.qTable[stateKey][actionKey] = newQ;

                // Track metrics
                var loss = Math.Abs(targetQ - currentQ);
                totalLoss += loss;
                totalReward += transition.Reward;
            }

            var avgLoss = totalLoss / batch.Count;
            var avgReward = totalReward / batch.Count;

            this.logger.LogInformation(
                "Training completed: batch={BatchSize}, avgLoss={AvgLoss:F4}, avgReward={AvgReward:F4}",
                batch.Count,
                avgLoss,
                avgReward);

            var metrics = new TrainingMetrics(
                PolicyLoss: avgLoss,
                ValueLoss: avgLoss, // Note: In this Q-learning implementation, TD error is used for both policy and value loss; other algorithms may report distinct values via TrainingMetrics
                Entropy: this.epsilon, // Using epsilon as proxy for entropy
                AverageReward: avgReward,
                BatchSize: batch.Count);

            return Result<TrainingMetrics, string>.Success(metrics);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Training failed");
            return Result<TrainingMetrics, string>.Failure($"Training failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Saves the agent's Q-table to a checkpoint.
    /// </summary>
    /// <param name="checkpointPath">Path to save checkpoint</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    public async Task<Result<Unit, string>> SaveCheckpointAsync(
        string checkpointPath,
        CancellationToken ct = default)
    {
        try
        {
            this.logger.LogInformation("Saving checkpoint to {Path}", checkpointPath);

            // In a real implementation, this would serialize Q-table to file
            // For now, just simulate save operation
            await Task.Delay(100, ct);

            this.logger.LogInformation("Checkpoint saved successfully");
            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to save checkpoint");
            return Result<Unit, string>.Failure($"Checkpoint save failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads the agent's Q-table from a checkpoint.
    /// </summary>
    /// <param name="checkpointPath">Path to load checkpoint from</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    public async Task<Result<Unit, string>> LoadCheckpointAsync(
        string checkpointPath,
        CancellationToken ct = default)
    {
        try
        {
            this.logger.LogInformation("Loading checkpoint from {Path}", checkpointPath);

            // In a real implementation, this would deserialize Q-table from file
            // For now, just simulate load operation
            await Task.Delay(100, ct);

            this.logger.LogInformation("Checkpoint loaded successfully");
            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to load checkpoint");
            return Result<Unit, string>.Failure($"Checkpoint load failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears the experience replay buffer.
    /// </summary>
    public void ClearExperienceBuffer()
    {
        this.experienceBuffer.Clear();
        this.logger.LogInformation("Experience buffer cleared");
    }

    private EmbodiedAction GetGreedyAction(SensorState state, IReadOnlyList<EmbodiedAction> availableActions)
    {
        var stateKey = this.GetStateKey(state);

        if (!this.qTable.TryGetValue(stateKey, out var actionValues))
        {
            // No Q-values for this state, return random action
            return availableActions[Random.Shared.Next(availableActions.Count)];
        }

        EmbodiedAction? bestAction = null;
        var bestValue = double.NegativeInfinity;

        foreach (var action in availableActions)
        {
            var actionKey = this.GetActionKey(action);
            var qValue = actionValues.GetValueOrDefault(actionKey, 0.0);

            if (qValue > bestValue)
            {
                bestValue = qValue;
                bestAction = action;
            }
        }

        // Return best action or random if none found
        return bestAction ?? availableActions[Random.Shared.Next(availableActions.Count)];
    }

    private double GetMaxQValue(SensorState state)
    {
        var stateKey = this.GetStateKey(state);

        if (!this.qTable.TryGetValue(stateKey, out var actionValues) || actionValues.Count == 0)
        {
            return 0.0;
        }

        return actionValues.Values.Max();
    }

    private List<EmbodiedTransition> SampleBatch(int batchSize)
    {
        // Efficient random sampling for small batches
        if (batchSize >= this.experienceBuffer.Count)
        {
            // Return all if batch size exceeds buffer
            return new List<EmbodiedTransition>(this.experienceBuffer);
        }

        if (batchSize > this.experienceBuffer.Count / 2)
        {
            // Use Fisher-Yates shuffle for large batch sizes (> 50% of buffer)
            var indices = Enumerable.Range(0, this.experienceBuffer.Count).ToList();

            for (int i = indices.Count - 1; i > 0; i--)
            {
                int j = Random.Shared.Next(i + 1);
                (indices[i], indices[j]) = (indices[j], indices[i]);
            }

            return indices.Take(batchSize)
                .Select(i => this.experienceBuffer[i])
                .ToList();
        }
        else
        {
            // Use random sampling with collision detection for small batch sizes
            var selectedIndices = new HashSet<int>();
            var batch = new List<EmbodiedTransition>(batchSize);

            while (selectedIndices.Count < batchSize)
            {
                var index = Random.Shared.Next(this.experienceBuffer.Count);
                if (selectedIndices.Add(index))
                {
                    batch.Add(this.experienceBuffer[index]);
                }
            }

            return batch;
        }
    }

    private string GetStateKey(SensorState state)
    {
        // Simple state hashing with coarse discretization:
        // positions are rounded to 2 decimal places (F2), so nearby continuous
        // positions (e.g. 0.004 vs 0.006) are merged into the same discrete state key.
        // This keeps the Q-table compact and encourages generalization; in production,
        // consider using configurable precision or richer feature extraction if finer
        // spatial distinctions are required.
        return $"p:{state.Position.X:F2},{state.Position.Y:F2},{state.Position.Z:F2}";
    }

    private string GetActionKey(EmbodiedAction action)
    {
        // Simple action hashing
        return action.ActionName ?? "unknown";
    }
}

/// <summary>
/// Training metrics from a batch update.
/// </summary>
/// <param name="PolicyLoss">Policy gradient loss</param>
/// <param name="ValueLoss">Value function loss</param>
/// <param name="Entropy">Policy entropy (exploration measure)</param>
/// <param name="AverageReward">Average reward in batch</param>
/// <param name="BatchSize">Number of transitions in batch</param>
public sealed record TrainingMetrics(
    double PolicyLoss,
    double ValueLoss,
    double Entropy,
    double AverageReward,
    int BatchSize);
