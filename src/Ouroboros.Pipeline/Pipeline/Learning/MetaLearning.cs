// <copyright file="MetaLearning.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Learning;

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Ouroboros.Core.Monads;
using Ouroboros.Core.Steps;

/// <summary>
/// Represents a learning strategy configuration for meta-learning optimization.
/// Encapsulates hyperparameters that control the learning process.
/// </summary>
/// <param name="Id">Unique identifier for this strategy.</param>
/// <param name="Name">Human-readable name describing the strategy.</param>
/// <param name="LearningRate">Step size for gradient-based updates (typically 0.0001 to 0.1).</param>
/// <param name="ExplorationRate">Epsilon for exploration vs exploitation trade-off (0 = greedy, 1 = random).</param>
/// <param name="DiscountFactor">Gamma for future reward weighting (0 = myopic, 1 = far-sighted).</param>
/// <param name="BatchSize">Number of samples per learning update.</param>
/// <param name="Parameters">Additional strategy-specific parameters.</param>
public sealed record LearningStrategy(
    Guid Id,
    string Name,
    double LearningRate,
    double ExplorationRate,
    double DiscountFactor,
    int BatchSize,
    ImmutableDictionary<string, double> Parameters)
{
    /// <summary>
    /// Gets the default learning strategy with sensible initial hyperparameters.
    /// Uses Adam-like defaults: LR=0.001, ε=0.1, γ=0.99, batch=32.
    /// </summary>
    public static LearningStrategy Default => new(
        Id: Guid.NewGuid(),
        Name: "Default",
        LearningRate: 0.001,
        ExplorationRate: 0.1,
        DiscountFactor: 0.99,
        BatchSize: 32,
        Parameters: ImmutableDictionary<string, double>.Empty);

    /// <summary>
    /// Creates a new strategy with exploration-focused parameters.
    /// Higher exploration rate for initial learning phases.
    /// </summary>
    /// <param name="name">Optional name for the strategy.</param>
    /// <returns>An exploration-focused learning strategy.</returns>
    public static LearningStrategy Exploratory(string name = "Exploratory") => new(
        Id: Guid.NewGuid(),
        Name: name,
        LearningRate: 0.01,
        ExplorationRate: 0.5,
        DiscountFactor: 0.95,
        BatchSize: 64,
        Parameters: ImmutableDictionary<string, double>.Empty
            .Add("temperature", 1.5)
            .Add("curiosity_weight", 0.3));

    /// <summary>
    /// Creates a new strategy optimized for exploitation and fine-tuning.
    /// Lower exploration rate for converged policies.
    /// </summary>
    /// <param name="name">Optional name for the strategy.</param>
    /// <returns>An exploitation-focused learning strategy.</returns>
    public static LearningStrategy Exploitative(string name = "Exploitative") => new(
        Id: Guid.NewGuid(),
        Name: name,
        LearningRate: 0.0001,
        ExplorationRate: 0.01,
        DiscountFactor: 0.999,
        BatchSize: 128,
        Parameters: ImmutableDictionary<string, double>.Empty
            .Add("temperature", 0.5)
            .Add("curiosity_weight", 0.05));

    /// <summary>
    /// Creates a copy with adjusted learning rate.
    /// </summary>
    /// <param name="newLearningRate">The new learning rate value.</param>
    /// <returns>A new strategy with the updated learning rate.</returns>
    public LearningStrategy WithLearningRate(double newLearningRate)
        => this with { LearningRate = Math.Clamp(newLearningRate, 1e-7, 1.0) };

    /// <summary>
    /// Creates a copy with adjusted exploration rate.
    /// </summary>
    /// <param name="newExplorationRate">The new exploration rate value.</param>
    /// <returns>A new strategy with the updated exploration rate.</returns>
    public LearningStrategy WithExplorationRate(double newExplorationRate)
        => this with { ExplorationRate = Math.Clamp(newExplorationRate, 0.0, 1.0) };

    /// <summary>
    /// Creates a copy with adjusted discount factor.
    /// </summary>
    /// <param name="newDiscountFactor">The new discount factor value.</param>
    /// <returns>A new strategy with the updated discount factor.</returns>
    public LearningStrategy WithDiscountFactor(double newDiscountFactor)
        => this with { DiscountFactor = Math.Clamp(newDiscountFactor, 0.0, 1.0) };

    /// <summary>
    /// Creates a copy with an additional or updated parameter.
    /// </summary>
    /// <param name="key">The parameter name.</param>
    /// <param name="value">The parameter value.</param>
    /// <returns>A new strategy with the updated parameter.</returns>
    public LearningStrategy WithParameter(string key, double value)
        => this with { Parameters = Parameters.SetItem(key, value) };

    /// <summary>
    /// Validates the strategy parameters.
    /// </summary>
    /// <returns>A Result indicating success or validation errors.</returns>
    public Result<Unit, string> Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            return Result<Unit, string>.Failure("Strategy name cannot be empty.");
        }

        if (LearningRate <= 0 || LearningRate > 1)
        {
            return Result<Unit, string>.Failure($"Learning rate must be in (0, 1], got {LearningRate}.");
        }

        if (ExplorationRate < 0 || ExplorationRate > 1)
        {
            return Result<Unit, string>.Failure($"Exploration rate must be in [0, 1], got {ExplorationRate}.");
        }

        if (DiscountFactor < 0 || DiscountFactor > 1)
        {
            return Result<Unit, string>.Failure($"Discount factor must be in [0, 1], got {DiscountFactor}.");
        }

        if (BatchSize <= 0)
        {
            return Result<Unit, string>.Failure($"Batch size must be positive, got {BatchSize}.");
        }

        return Result<Unit, string>.Success(Unit.Value);
    }
}

/// <summary>
/// Tracks learning performance metrics over time.
/// Provides statistical measures of learning progress and efficiency.
/// </summary>
/// <param name="TotalEpisodes">Total number of learning episodes completed.</param>
/// <param name="AverageReward">Mean reward across all episodes.</param>
/// <param name="RewardVariance">Variance in episode rewards.</param>
/// <param name="ConvergenceRate">Rate at which rewards are stabilizing (lower = more stable).</param>
/// <param name="LearningEfficiency">Reward improvement per episode (higher = faster learning).</param>
/// <param name="Timestamps">Ordered list of measurement timestamps.</param>
public sealed record LearningMetrics(
    int TotalEpisodes,
    double AverageReward,
    double RewardVariance,
    double ConvergenceRate,
    double LearningEfficiency,
    ImmutableList<DateTime> Timestamps)
{
    /// <summary>
    /// Gets empty metrics representing no learning has occurred.
    /// </summary>
    public static LearningMetrics Empty => new(
        TotalEpisodes: 0,
        AverageReward: 0.0,
        RewardVariance: 0.0,
        ConvergenceRate: 1.0,
        LearningEfficiency: 0.0,
        Timestamps: ImmutableList<DateTime>.Empty);

    /// <summary>
    /// Creates metrics from a sequence of episode rewards.
    /// </summary>
    /// <param name="rewards">The sequence of rewards from each episode.</param>
    /// <returns>Computed learning metrics.</returns>
    public static LearningMetrics FromRewards(IEnumerable<double> rewards)
    {
        var rewardList = rewards.ToList();
        if (rewardList.Count == 0)
        {
            return Empty;
        }

        var totalEpisodes = rewardList.Count;
        var averageReward = rewardList.Average();
        var variance = rewardList.Sum(r => Math.Pow(r - averageReward, 2)) / totalEpisodes;

        // Compute convergence rate as change in rolling average
        var convergenceRate = ComputeConvergenceRate(rewardList);

        // Compute learning efficiency as reward improvement per episode
        var efficiency = ComputeLearningEfficiency(rewardList);

        return new LearningMetrics(
            TotalEpisodes: totalEpisodes,
            AverageReward: averageReward,
            RewardVariance: variance,
            ConvergenceRate: convergenceRate,
            LearningEfficiency: efficiency,
            Timestamps: ImmutableList.Create(DateTime.UtcNow));
    }

    /// <summary>
    /// Updates metrics with a new episode reward.
    /// Uses Welford's online algorithm for variance computation.
    /// </summary>
    /// <param name="newReward">The reward from the latest episode.</param>
    /// <returns>Updated learning metrics.</returns>
    public LearningMetrics WithNewReward(double newReward)
    {
        var newTotal = TotalEpisodes + 1;
        var delta = newReward - AverageReward;
        var newAverage = AverageReward + (delta / newTotal);
        var delta2 = newReward - newAverage;

        // Welford's online variance update
        var newVariance = TotalEpisodes == 0
            ? 0.0
            : ((RewardVariance * TotalEpisodes) + (delta * delta2)) / newTotal;

        // Update convergence rate (exponential moving average of absolute delta)
        var newConvergence = (0.9 * ConvergenceRate) + (0.1 * Math.Abs(delta));

        // Update efficiency (positive improvement trend)
        var improvementRate = delta > 0 ? delta / Math.Max(Math.Abs(AverageReward), 1e-6) : 0.0;
        var newEfficiency = (0.95 * LearningEfficiency) + (0.05 * improvementRate);

        return this with
        {
            TotalEpisodes = newTotal,
            AverageReward = newAverage,
            RewardVariance = newVariance,
            ConvergenceRate = newConvergence,
            LearningEfficiency = newEfficiency,
            Timestamps = Timestamps.Add(DateTime.UtcNow),
        };
    }

    /// <summary>
    /// Computes a normalized performance score for strategy comparison.
    /// Higher scores indicate better learning performance.
    /// </summary>
    /// <returns>A normalized performance score in [0, 1].</returns>
    public double ComputePerformanceScore()
    {
        if (TotalEpisodes == 0)
        {
            return 0.0;
        }

        // Normalize components
        var rewardScore = Math.Tanh(AverageReward); // Maps to [-1, 1]
        var stabilityScore = 1.0 / (1.0 + RewardVariance); // Higher variance = lower score
        var convergenceScore = 1.0 / (1.0 + ConvergenceRate); // Lower convergence rate = better
        var efficiencyScore = Math.Tanh(LearningEfficiency * 10); // Scale efficiency

        // Weighted combination
        return (0.4 * rewardScore) +
               (0.2 * stabilityScore) +
               (0.2 * convergenceScore) +
               (0.2 * efficiencyScore);
    }

    private static double ComputeConvergenceRate(List<double> rewards)
    {
        if (rewards.Count < 10)
        {
            return 1.0;
        }

        // Compare recent vs overall average
        var recentCount = Math.Min(10, rewards.Count / 4);
        var recentAvg = rewards.Skip(rewards.Count - recentCount).Average();
        var overallAvg = rewards.Average();

        return Math.Abs(recentAvg - overallAvg) / Math.Max(Math.Abs(overallAvg), 1e-6);
    }

    private static double ComputeLearningEfficiency(List<double> rewards)
    {
        if (rewards.Count < 2)
        {
            return 0.0;
        }

        // Linear regression slope normalized by count
        var n = rewards.Count;
        var sumX = (n * (n - 1)) / 2.0;
        var sumX2 = (n * (n - 1) * ((2 * n) - 1)) / 6.0;
        var sumY = rewards.Sum();
        var sumXY = rewards.Select((r, i) => r * i).Sum();

        var slope = ((n * sumXY) - (sumX * sumY)) / ((n * sumX2) - (sumX * sumX));

        return slope;
    }
}

/// <summary>
/// Defines the interface for meta-learning strategy evaluation and adaptation.
/// Implements the outer loop of learning-to-learn optimization.
/// </summary>
public interface IMetaLearner
{
    /// <summary>
    /// Evaluates the effectiveness of a learning strategy based on metrics.
    /// </summary>
    /// <param name="strategy">The strategy to evaluate.</param>
    /// <param name="metrics">The learning metrics observed under this strategy.</param>
    /// <returns>A score representing strategy effectiveness (higher is better).</returns>
    double EvaluateStrategy(LearningStrategy strategy, LearningMetrics metrics);

    /// <summary>
    /// Adapts a learning strategy based on observed performance metrics.
    /// Uses Bayesian optimization principles to suggest improved hyperparameters.
    /// </summary>
    /// <param name="current">The current learning strategy.</param>
    /// <param name="metrics">The metrics observed under the current strategy.</param>
    /// <returns>An adapted strategy with optimized hyperparameters, or an error.</returns>
    Result<LearningStrategy, string> AdaptStrategy(LearningStrategy current, LearningMetrics metrics);

    /// <summary>
    /// Selects the best strategy from a collection based on metrics.
    /// </summary>
    /// <param name="strategies">The candidate strategies to evaluate.</param>
    /// <param name="metrics">The current learning metrics for context.</param>
    /// <returns>The best strategy, or an error if no valid strategy exists.</returns>
    Result<LearningStrategy, string> SelectBestStrategy(
        IEnumerable<LearningStrategy> strategies,
        LearningMetrics metrics);
}

/// <summary>
/// Historical performance record for a learning strategy.
/// Used for Bayesian optimization-style adaptation.
/// </summary>
/// <param name="StrategyId">The strategy's unique identifier.</param>
/// <param name="Score">The observed performance score.</param>
/// <param name="Timestamp">When this performance was observed.</param>
/// <param name="Hyperparameters">The hyperparameter configuration at observation time.</param>
public sealed record StrategyPerformance(
    Guid StrategyId,
    double Score,
    DateTime Timestamp,
    ImmutableDictionary<string, double> Hyperparameters);

/// <summary>
/// Adaptive meta-learner implementing Bayesian optimization-inspired strategy adaptation.
/// Tracks historical performance to guide hyperparameter exploration and exploitation.
/// </summary>
public sealed class AdaptiveMetaLearner : IMetaLearner
{
    private readonly ConcurrentDictionary<Guid, List<StrategyPerformance>> _history = new();
    private readonly Random _random;
    private readonly double _explorationWeight;
    private readonly int _historyLimit;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdaptiveMetaLearner"/> class.
    /// </summary>
    /// <param name="explorationWeight">Weight for exploration vs exploitation (default: 0.2).</param>
    /// <param name="historyLimit">Maximum history entries per strategy (default: 100).</param>
    /// <param name="seed">Optional random seed for reproducibility.</param>
    public AdaptiveMetaLearner(
        double explorationWeight = 0.2,
        int historyLimit = 100,
        int? seed = null)
    {
        _explorationWeight = Math.Clamp(explorationWeight, 0.0, 1.0);
        _historyLimit = Math.Max(1, historyLimit);
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double EvaluateStrategy(LearningStrategy strategy, LearningMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(strategy);
        ArgumentNullException.ThrowIfNull(metrics);

        // Base score from metrics
        var baseScore = metrics.ComputePerformanceScore();

        // Bonus for exploration (UCB-style)
        var explorationBonus = ComputeExplorationBonus(strategy.Id);

        // Penalty for invalid configurations
        var validationPenalty = strategy.Validate().IsFailure ? -0.5 : 0.0;

        // Hyperparameter quality heuristics
        var hyperparamScore = ComputeHyperparameterScore(strategy, metrics);

        return baseScore + (explorationBonus * _explorationWeight) + validationPenalty + (hyperparamScore * 0.1);
    }

    /// <inheritdoc/>
    public Result<LearningStrategy, string> AdaptStrategy(LearningStrategy current, LearningMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(metrics);

        try
        {
            // Record current performance
            RecordPerformance(current, metrics);

            // Analyze learning state
            var state = AnalyzeLearningState(metrics);

            // Apply Bayesian-inspired adaptation
            var adapted = state switch
            {
                LearningState.Exploring => AdaptForExploration(current, metrics),
                LearningState.Converging => AdaptForConvergence(current, metrics),
                LearningState.Converged => AdaptForExploitation(current, metrics),
                LearningState.Diverging => AdaptForRecovery(current, metrics),
                LearningState.Stagnant => AdaptForEscapeStagnation(current, metrics),
                _ => current,
            };

            // Add Gaussian noise for exploration
            var explored = ApplyGaussianExploration(adapted);

            return Result<LearningStrategy, string>.Success(explored);
        }
        catch (Exception ex)
        {
            return Result<LearningStrategy, string>.Failure($"Strategy adaptation failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public Result<LearningStrategy, string> SelectBestStrategy(
        IEnumerable<LearningStrategy> strategies,
        LearningMetrics metrics)
    {
        ArgumentNullException.ThrowIfNull(strategies);
        ArgumentNullException.ThrowIfNull(metrics);

        var strategyList = strategies.ToList();
        if (strategyList.Count == 0)
        {
            return Result<LearningStrategy, string>.Failure("No strategies provided for selection.");
        }

        try
        {
            // Evaluate all strategies
            var scored = strategyList
                .Select(s => (Strategy: s, Score: EvaluateStrategy(s, metrics)))
                .OrderByDescending(x => x.Score)
                .ToList();

            // Apply softmax selection for stochastic exploration
            var selected = SoftmaxSelect(scored);

            return Result<LearningStrategy, string>.Success(selected);
        }
        catch (Exception ex)
        {
            return Result<LearningStrategy, string>.Failure($"Strategy selection failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the historical performance data for a strategy.
    /// </summary>
    /// <param name="strategyId">The strategy identifier.</param>
    /// <returns>The performance history, if available.</returns>
    public Option<IReadOnlyList<StrategyPerformance>> GetHistory(Guid strategyId)
    {
        return _history.TryGetValue(strategyId, out var history)
            ? Option<IReadOnlyList<StrategyPerformance>>.Some(history.AsReadOnly())
            : Option<IReadOnlyList<StrategyPerformance>>.None();
    }

    private void RecordPerformance(LearningStrategy strategy, LearningMetrics metrics)
    {
        var performance = new StrategyPerformance(
            StrategyId: strategy.Id,
            Score: metrics.ComputePerformanceScore(),
            Timestamp: DateTime.UtcNow,
            Hyperparameters: ImmutableDictionary<string, double>.Empty
                .Add("learning_rate", strategy.LearningRate)
                .Add("exploration_rate", strategy.ExplorationRate)
                .Add("discount_factor", strategy.DiscountFactor)
                .Add("batch_size", strategy.BatchSize));

        var history = _history.GetOrAdd(strategy.Id, _ => new List<StrategyPerformance>());

        lock (history)
        {
            history.Add(performance);

            // Trim to limit
            while (history.Count > _historyLimit)
            {
                history.RemoveAt(0);
            }
        }
    }

    private double ComputeExplorationBonus(Guid strategyId)
    {
        if (!_history.TryGetValue(strategyId, out var history) || history.Count == 0)
        {
            return 1.0; // Maximum bonus for unexplored strategies
        }

        // UCB-style bonus: sqrt(ln(total) / visits)
        var totalVisits = _history.Values.Sum(h => h.Count);
        var strategyVisits = history.Count;

        return Math.Sqrt(Math.Log(totalVisits + 1) / (strategyVisits + 1));
    }

    private static double ComputeHyperparameterScore(LearningStrategy strategy, LearningMetrics metrics)
    {
        var score = 0.0;

        // Prefer lower learning rates when converging
        if (metrics.ConvergenceRate < 0.1)
        {
            score += strategy.LearningRate < 0.001 ? 0.1 : -0.05;
        }

        // Prefer higher exploration when performance is low
        if (metrics.AverageReward < 0)
        {
            score += strategy.ExplorationRate > 0.3 ? 0.1 : -0.05;
        }

        // Prefer larger batch sizes when variance is high
        if (metrics.RewardVariance > 0.5)
        {
            score += strategy.BatchSize >= 64 ? 0.1 : -0.05;
        }

        return score;
    }

    private static LearningState AnalyzeLearningState(LearningMetrics metrics)
    {
        if (metrics.TotalEpisodes < 10)
        {
            return LearningState.Exploring;
        }

        // Check for convergence
        if (metrics.ConvergenceRate < 0.05 && metrics.RewardVariance < 0.1)
        {
            return LearningState.Converged;
        }

        // Check for divergence (increasing variance)
        if (metrics.RewardVariance > 1.0 && metrics.LearningEfficiency < 0)
        {
            return LearningState.Diverging;
        }

        // Check for stagnation
        if (Math.Abs(metrics.LearningEfficiency) < 0.001 && metrics.TotalEpisodes > 50)
        {
            return LearningState.Stagnant;
        }

        // Check for converging
        if (metrics.ConvergenceRate < 0.2 && metrics.LearningEfficiency > 0)
        {
            return LearningState.Converging;
        }

        return LearningState.Exploring;
    }

    private static LearningStrategy AdaptForExploration(LearningStrategy current, LearningMetrics metrics)
    {
        // Increase exploration and learning rate
        return current
            .WithLearningRate(Math.Min(current.LearningRate * 1.5, 0.1))
            .WithExplorationRate(Math.Min(current.ExplorationRate * 1.2, 0.5));
    }

    private static LearningStrategy AdaptForConvergence(LearningStrategy current, LearningMetrics metrics)
    {
        // Gradually decrease rates
        return current
            .WithLearningRate(current.LearningRate * 0.9)
            .WithExplorationRate(current.ExplorationRate * 0.95);
    }

    private static LearningStrategy AdaptForExploitation(LearningStrategy current, LearningMetrics metrics)
    {
        // Minimize exploration, fine-tune learning rate
        return current
            .WithLearningRate(current.LearningRate * 0.5)
            .WithExplorationRate(Math.Max(current.ExplorationRate * 0.5, 0.01));
    }

    private static LearningStrategy AdaptForRecovery(LearningStrategy current, LearningMetrics metrics)
    {
        // Reduce learning rate significantly, increase exploration
        return current
            .WithLearningRate(current.LearningRate * 0.3)
            .WithExplorationRate(Math.Min(current.ExplorationRate + 0.2, 0.8));
    }

    private static LearningStrategy AdaptForEscapeStagnation(LearningStrategy current, LearningMetrics metrics)
    {
        // Bump learning rate and exploration to escape local optima
        return current
            .WithLearningRate(Math.Min(current.LearningRate * 3.0, 0.05))
            .WithExplorationRate(Math.Min(current.ExplorationRate + 0.3, 0.7));
    }

    private LearningStrategy ApplyGaussianExploration(LearningStrategy strategy)
    {
        // Apply small Gaussian perturbations
        var lrNoise = 1.0 + (SampleGaussian() * 0.1);
        var erNoise = 1.0 + (SampleGaussian() * 0.05);
        var dfNoise = 1.0 + (SampleGaussian() * 0.02);

        return strategy
            .WithLearningRate(strategy.LearningRate * lrNoise)
            .WithExplorationRate(strategy.ExplorationRate * erNoise)
            .WithDiscountFactor(strategy.DiscountFactor * dfNoise);
    }

    private double SampleGaussian()
    {
        // Box-Muller transform
        var u1 = 1.0 - _random.NextDouble();
        var u2 = 1.0 - _random.NextDouble();
        return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
    }

    private LearningStrategy SoftmaxSelect(List<(LearningStrategy Strategy, double Score)> scored)
    {
        if (scored.Count == 1)
        {
            return scored[0].Strategy;
        }

        // Softmax with temperature
        var temperature = 0.5;
        var maxScore = scored.Max(s => s.Score);
        var expScores = scored.Select(s => Math.Exp((s.Score - maxScore) / temperature)).ToList();
        var sumExp = expScores.Sum();

        var probabilities = expScores.Select(e => e / sumExp).ToList();

        // Sample from distribution
        var sample = _random.NextDouble();
        var cumulative = 0.0;

        for (int i = 0; i < probabilities.Count; i++)
        {
            cumulative += probabilities[i];
            if (sample <= cumulative)
            {
                return scored[i].Strategy;
            }
        }

        return scored[^1].Strategy;
    }
}

/// <summary>
/// Represents the current state of the learning process.
/// </summary>
internal enum LearningState
{
    /// <summary>Initial exploration phase.</summary>
    Exploring,

    /// <summary>Learning is progressing toward convergence.</summary>
    Converging,

    /// <summary>Learning has converged to a stable solution.</summary>
    Converged,

    /// <summary>Learning is unstable and diverging.</summary>
    Diverging,

    /// <summary>Learning has stagnated with no improvement.</summary>
    Stagnant,
}

/// <summary>
/// Provides Kleisli arrow factories for meta-learning pipeline integration.
/// Enables composable meta-learning workflows using Step abstractions.
/// </summary>
public static class MetaLearningArrow
{
    /// <summary>
    /// Creates an arrow that evaluates a strategy's effectiveness.
    /// </summary>
    /// <param name="metaLearner">The meta-learner to use for evaluation.</param>
    /// <returns>A step that scores a strategy-metrics pair.</returns>
    public static Step<(LearningStrategy Strategy, LearningMetrics Metrics), double> EvaluateArrow(
        IMetaLearner metaLearner)
    {
        ArgumentNullException.ThrowIfNull(metaLearner);
        return input => Task.FromResult(metaLearner.EvaluateStrategy(input.Strategy, input.Metrics));
    }

    /// <summary>
    /// Creates an arrow that adapts a learning strategy based on metrics.
    /// This is the core meta-learning operation for hyperparameter optimization.
    /// </summary>
    /// <param name="metaLearner">The meta-learner to use for adaptation.</param>
    /// <returns>A step that transforms strategy-metrics pairs into adapted strategies.</returns>
    public static Step<(LearningStrategy Strategy, LearningMetrics Metrics), Result<LearningStrategy, string>> AdaptArrow(
        IMetaLearner metaLearner)
    {
        ArgumentNullException.ThrowIfNull(metaLearner);
        return input => Task.FromResult(metaLearner.AdaptStrategy(input.Strategy, input.Metrics));
    }

    /// <summary>
    /// Creates an arrow that selects the best strategy from candidates.
    /// </summary>
    /// <param name="metaLearner">The meta-learner to use for selection.</param>
    /// <returns>A step that selects the best strategy given candidates and metrics.</returns>
    public static Step<(IEnumerable<LearningStrategy> Strategies, LearningMetrics Metrics), Result<LearningStrategy, string>> SelectArrow(
        IMetaLearner metaLearner)
    {
        ArgumentNullException.ThrowIfNull(metaLearner);
        return input => Task.FromResult(metaLearner.SelectBestStrategy(input.Strategies, input.Metrics));
    }

    /// <summary>
    /// Creates a composed arrow that evaluates, adapts, and validates a strategy.
    /// Implements the full meta-learning cycle as a single composable step.
    /// </summary>
    /// <param name="metaLearner">The meta-learner to use.</param>
    /// <returns>A step implementing the complete adaptation cycle.</returns>
    public static Step<(LearningStrategy Strategy, LearningMetrics Metrics), Result<LearningStrategy, string>> AdaptAndValidateArrow(
        IMetaLearner metaLearner)
    {
        ArgumentNullException.ThrowIfNull(metaLearner);

        return async input =>
        {
            var adaptResult = metaLearner.AdaptStrategy(input.Strategy, input.Metrics);
            if (adaptResult.IsFailure)
            {
                return adaptResult;
            }

            var adapted = adaptResult.Value;
            var validationResult = adapted.Validate();

            return validationResult.IsSuccess
                ? Result<LearningStrategy, string>.Success(adapted)
                : Result<LearningStrategy, string>.Failure($"Adapted strategy failed validation: {validationResult.Error}");
        };
    }

    /// <summary>
    /// Creates an arrow that updates metrics with a new reward observation.
    /// </summary>
    /// <returns>A step that updates learning metrics with a new reward.</returns>
    public static Step<(LearningMetrics Metrics, double Reward), LearningMetrics> UpdateMetricsArrow()
        => input => Task.FromResult(input.Metrics.WithNewReward(input.Reward));

    /// <summary>
    /// Creates an arrow that computes a performance score from metrics.
    /// </summary>
    /// <returns>A step that computes a normalized performance score.</returns>
    public static Step<LearningMetrics, double> PerformanceScoreArrow()
        => metrics => Task.FromResult(metrics.ComputePerformanceScore());

    /// <summary>
    /// Creates an arrow for iterative meta-learning over multiple episodes.
    /// Applies adaptation after each episode's metrics update.
    /// </summary>
    /// <param name="metaLearner">The meta-learner to use.</param>
    /// <param name="maxIterations">Maximum number of adaptation iterations.</param>
    /// <param name="convergenceThreshold">Stop when improvement falls below this threshold.</param>
    /// <returns>A step that performs iterative meta-learning.</returns>
    public static Step<(LearningStrategy Initial, IEnumerable<double> Rewards), Result<(LearningStrategy Final, LearningMetrics Metrics), string>> IterativeAdaptArrow(
        IMetaLearner metaLearner,
        int maxIterations = 10,
        double convergenceThreshold = 0.001)
    {
        ArgumentNullException.ThrowIfNull(metaLearner);

        return async input =>
        {
            var strategy = input.Initial;
            var metrics = LearningMetrics.FromRewards(input.Rewards);
            var previousScore = metrics.ComputePerformanceScore();

            for (int i = 0; i < maxIterations; i++)
            {
                var adaptResult = metaLearner.AdaptStrategy(strategy, metrics);
                if (adaptResult.IsFailure)
                {
                    return Result<(LearningStrategy Final, LearningMetrics Metrics), string>.Failure(adaptResult.Error);
                }

                strategy = adaptResult.Value;
                var currentScore = metaLearner.EvaluateStrategy(strategy, metrics);

                // Check convergence
                if (Math.Abs(currentScore - previousScore) < convergenceThreshold)
                {
                    break;
                }

                previousScore = currentScore;
            }

            return Result<(LearningStrategy Final, LearningMetrics Metrics), string>.Success((strategy, metrics));
        };
    }
}
