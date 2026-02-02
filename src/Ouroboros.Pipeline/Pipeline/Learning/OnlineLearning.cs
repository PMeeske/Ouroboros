// <copyright file="OnlineLearning.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Learning;

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Ouroboros.Core.Monads;
using Ouroboros.Core.Steps;

/// <summary>
/// Specifies the type of feedback received for learning.
/// </summary>
public enum FeedbackType
{
    /// <summary>
    /// Explicit feedback directly provided by a user or evaluator.
    /// </summary>
    Explicit,

    /// <summary>
    /// Implicit feedback inferred from user behavior (clicks, time spent, etc.).
    /// </summary>
    Implicit,

    /// <summary>
    /// Corrective feedback that includes the correct or preferred output.
    /// </summary>
    Corrective,

    /// <summary>
    /// Comparative feedback ranking multiple outputs.
    /// </summary>
    Comparative,
}

/// <summary>
/// Represents immutable feedback data for online learning.
/// Captures the complete context of a learning signal including source, quality rating, and metadata.
/// </summary>
/// <param name="Id">Unique identifier for this feedback instance.</param>
/// <param name="SourceId">Identifier of the component or model that produced the output.</param>
/// <param name="InputContext">The input context that led to the output being evaluated.</param>
/// <param name="Output">The actual output that was produced and is being evaluated.</param>
/// <param name="Score">Quality rating in the range [-1, 1] where -1 is worst and 1 is best.</param>
/// <param name="Type">The type of feedback provided.</param>
/// <param name="Timestamp">When this feedback was recorded.</param>
/// <param name="Tags">Categorical tags for organizing and filtering feedback.</param>
public sealed record Feedback(
    Guid Id,
    string SourceId,
    string InputContext,
    string Output,
    double Score,
    FeedbackType Type,
    DateTime Timestamp,
    ImmutableList<string> Tags)
{
    /// <summary>
    /// Creates explicit feedback with a user-provided score.
    /// </summary>
    /// <param name="sourceId">Identifier of the producing component.</param>
    /// <param name="inputContext">The input context.</param>
    /// <param name="output">The produced output.</param>
    /// <param name="score">Quality score in [-1, 1].</param>
    /// <param name="tags">Optional tags for categorization.</param>
    /// <returns>A new Feedback instance with Explicit type.</returns>
    public static Feedback Explicit(
        string sourceId,
        string inputContext,
        string output,
        double score,
        params string[] tags)
        => new(
            Guid.NewGuid(),
            sourceId,
            inputContext,
            output,
            Math.Clamp(score, -1.0, 1.0),
            FeedbackType.Explicit,
            DateTime.UtcNow,
            tags.ToImmutableList());

    /// <summary>
    /// Creates implicit feedback inferred from user behavior.
    /// </summary>
    /// <param name="sourceId">Identifier of the producing component.</param>
    /// <param name="inputContext">The input context.</param>
    /// <param name="output">The produced output.</param>
    /// <param name="score">Inferred quality score in [-1, 1].</param>
    /// <param name="tags">Optional tags for categorization.</param>
    /// <returns>A new Feedback instance with Implicit type.</returns>
    public static Feedback Implicit(
        string sourceId,
        string inputContext,
        string output,
        double score,
        params string[] tags)
        => new(
            Guid.NewGuid(),
            sourceId,
            inputContext,
            output,
            Math.Clamp(score, -1.0, 1.0),
            FeedbackType.Implicit,
            DateTime.UtcNow,
            tags.ToImmutableList());

    /// <summary>
    /// Creates corrective feedback with the preferred output.
    /// </summary>
    /// <param name="sourceId">Identifier of the producing component.</param>
    /// <param name="inputContext">The input context.</param>
    /// <param name="actualOutput">The output that was produced.</param>
    /// <param name="preferredOutput">The preferred/correct output (stored in metadata).</param>
    /// <param name="tags">Optional tags for categorization.</param>
    /// <returns>A new Feedback instance with Corrective type and negative score.</returns>
    public static Feedback Corrective(
        string sourceId,
        string inputContext,
        string actualOutput,
        string preferredOutput,
        params string[] tags)
        => new Feedback(
            Guid.NewGuid(),
            sourceId,
            inputContext,
            actualOutput,
            -0.5, // Negative score indicates correction needed
            FeedbackType.Corrective,
            DateTime.UtcNow,
            tags.ToImmutableList().Add($"preferred:{preferredOutput}"));

    /// <summary>
    /// Creates comparative feedback ranking one output against another.
    /// </summary>
    /// <param name="sourceId">Identifier of the producing component.</param>
    /// <param name="inputContext">The input context.</param>
    /// <param name="chosenOutput">The output that was preferred.</param>
    /// <param name="rejectedOutput">The output that was rejected.</param>
    /// <param name="preferenceStrength">How strongly the chosen output was preferred (0, 1].</param>
    /// <param name="tags">Optional tags for categorization.</param>
    /// <returns>A new Feedback instance with Comparative type.</returns>
    public static Feedback Comparative(
        string sourceId,
        string inputContext,
        string chosenOutput,
        string rejectedOutput,
        double preferenceStrength = 0.5,
        params string[] tags)
        => new Feedback(
            Guid.NewGuid(),
            sourceId,
            inputContext,
            chosenOutput,
            Math.Clamp(preferenceStrength, 0.0, 1.0),
            FeedbackType.Comparative,
            DateTime.UtcNow,
            tags.ToImmutableList().Add($"rejected:{rejectedOutput}"));

    /// <summary>
    /// Creates a copy with additional tags.
    /// </summary>
    /// <param name="newTags">Tags to add.</param>
    /// <returns>A new Feedback with the additional tags.</returns>
    public Feedback WithTags(params string[] newTags)
        => this with { Tags = Tags.AddRange(newTags) };

    /// <summary>
    /// Validates the feedback data.
    /// </summary>
    /// <returns>A Result indicating success or validation errors.</returns>
    public Result<Unit, string> Validate()
    {
        if (string.IsNullOrWhiteSpace(SourceId))
        {
            return Result<Unit, string>.Failure("SourceId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(InputContext))
        {
            return Result<Unit, string>.Failure("InputContext cannot be empty.");
        }

        if (Score < -1.0 || Score > 1.0)
        {
            return Result<Unit, string>.Failure($"Score must be in [-1, 1], got {Score}.");
        }

        return Result<Unit, string>.Success(Unit.Value);
    }
}

/// <summary>
/// Represents a parameter update computed by online learning.
/// Captures the full update context including gradient and confidence.
/// </summary>
/// <param name="ParameterName">The name of the parameter being updated.</param>
/// <param name="OldValue">The previous value of the parameter.</param>
/// <param name="NewValue">The computed new value for the parameter.</param>
/// <param name="Gradient">The gradient or direction of the update.</param>
/// <param name="Confidence">Confidence in this update, in range [0, 1].</param>
public sealed record LearningUpdate(
    string ParameterName,
    double OldValue,
    double NewValue,
    double Gradient,
    double Confidence)
{
    /// <summary>
    /// Computes the magnitude of this update.
    /// </summary>
    public double Magnitude => Math.Abs(NewValue - OldValue);

    /// <summary>
    /// Creates a learning update with computed new value from gradient.
    /// </summary>
    /// <param name="parameterName">Name of the parameter.</param>
    /// <param name="currentValue">Current parameter value.</param>
    /// <param name="gradient">Computed gradient.</param>
    /// <param name="learningRate">Learning rate for scaling.</param>
    /// <param name="confidence">Confidence in the update.</param>
    /// <returns>A new LearningUpdate instance.</returns>
    public static LearningUpdate FromGradient(
        string parameterName,
        double currentValue,
        double gradient,
        double learningRate,
        double confidence = 1.0)
    {
        var delta = -learningRate * gradient; // Gradient descent: move opposite to gradient
        var newValue = currentValue + delta;
        return new LearningUpdate(
            parameterName,
            currentValue,
            newValue,
            gradient,
            Math.Clamp(confidence, 0.0, 1.0));
    }

    /// <summary>
    /// Creates a scaled version of this update.
    /// </summary>
    /// <param name="scale">The scaling factor to apply.</param>
    /// <returns>A new LearningUpdate with scaled values.</returns>
    public LearningUpdate Scale(double scale)
    {
        var scaledDelta = (NewValue - OldValue) * scale;
        return this with
        {
            NewValue = OldValue + scaledDelta,
            Gradient = Gradient * scale,
        };
    }

    /// <summary>
    /// Merges this update with another by averaging.
    /// </summary>
    /// <param name="other">The other update to merge with.</param>
    /// <returns>A merged LearningUpdate.</returns>
    public LearningUpdate MergeWith(LearningUpdate other)
    {
        if (ParameterName != other.ParameterName)
        {
            throw new ArgumentException($"Cannot merge updates for different parameters: {ParameterName} vs {other.ParameterName}");
        }

        var totalConfidence = Confidence + other.Confidence;
        var w1 = Confidence / totalConfidence;
        var w2 = other.Confidence / totalConfidence;

        return new LearningUpdate(
            ParameterName,
            OldValue,
            (NewValue * w1) + (other.NewValue * w2),
            (Gradient * w1) + (other.Gradient * w2),
            Math.Max(Confidence, other.Confidence));
    }
}

/// <summary>
/// Performance metrics for online learning tracking.
/// </summary>
/// <param name="ProcessedCount">Total number of feedback items processed.</param>
/// <param name="AverageScore">Running average of feedback scores.</param>
/// <param name="ScoreVariance">Variance in feedback scores.</param>
/// <param name="UpdateCount">Number of parameter updates applied.</param>
/// <param name="AverageGradientMagnitude">Average magnitude of gradients.</param>
/// <param name="ConvergenceMetric">Metric indicating convergence (lower = more converged).</param>
/// <param name="LastUpdateTime">Timestamp of the last update.</param>
public sealed record OnlineLearningMetrics(
    int ProcessedCount,
    double AverageScore,
    double ScoreVariance,
    int UpdateCount,
    double AverageGradientMagnitude,
    double ConvergenceMetric,
    DateTime LastUpdateTime)
{
    /// <summary>
    /// Gets empty metrics representing no learning has occurred.
    /// </summary>
    public static OnlineLearningMetrics Empty => new(
        ProcessedCount: 0,
        AverageScore: 0.0,
        ScoreVariance: 0.0,
        UpdateCount: 0,
        AverageGradientMagnitude: 0.0,
        ConvergenceMetric: 1.0,
        LastUpdateTime: DateTime.MinValue);

    /// <summary>
    /// Updates metrics with a new feedback score using Welford's algorithm.
    /// </summary>
    /// <param name="score">The new feedback score.</param>
    /// <returns>Updated metrics.</returns>
    public OnlineLearningMetrics WithNewScore(double score)
    {
        var newCount = ProcessedCount + 1;
        var delta = score - AverageScore;
        var newAverage = AverageScore + (delta / newCount);
        var delta2 = score - newAverage;

        var newVariance = ProcessedCount == 0
            ? 0.0
            : ((ScoreVariance * ProcessedCount) + (delta * delta2)) / newCount;

        return this with
        {
            ProcessedCount = newCount,
            AverageScore = newAverage,
            ScoreVariance = newVariance,
            LastUpdateTime = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Updates metrics with gradient information from an update.
    /// </summary>
    /// <param name="gradientMagnitude">The magnitude of the gradient.</param>
    /// <returns>Updated metrics.</returns>
    public OnlineLearningMetrics WithGradient(double gradientMagnitude)
    {
        var newUpdateCount = UpdateCount + 1;
        var newAvgMagnitude = AverageGradientMagnitude + ((gradientMagnitude - AverageGradientMagnitude) / newUpdateCount);

        // Update convergence metric as exponential moving average of gradient magnitude
        var newConvergence = (0.95 * ConvergenceMetric) + (0.05 * gradientMagnitude);

        return this with
        {
            UpdateCount = newUpdateCount,
            AverageGradientMagnitude = newAvgMagnitude,
            ConvergenceMetric = newConvergence,
            LastUpdateTime = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Computes an overall performance score.
    /// </summary>
    /// <returns>A normalized performance score in [0, 1].</returns>
    public double ComputePerformanceScore()
    {
        if (ProcessedCount == 0)
        {
            return 0.0;
        }

        var scoreComponent = (AverageScore + 1.0) / 2.0; // Map [-1, 1] to [0, 1]
        var stabilityComponent = 1.0 / (1.0 + ScoreVariance);
        var convergenceComponent = 1.0 / (1.0 + ConvergenceMetric);

        return (0.5 * scoreComponent) + (0.25 * stabilityComponent) + (0.25 * convergenceComponent);
    }
}

/// <summary>
/// Interface for online learning algorithms that process streaming feedback.
/// </summary>
public interface IOnlineLearner
{
    /// <summary>
    /// Gets the current performance metrics.
    /// </summary>
    OnlineLearningMetrics Metrics { get; }

    /// <summary>
    /// Processes a single feedback item and computes updates.
    /// </summary>
    /// <param name="feedback">The feedback to process.</param>
    /// <returns>A Result containing the computed updates or an error.</returns>
    Result<IReadOnlyList<LearningUpdate>, string> ProcessFeedback(Feedback feedback);

    /// <summary>
    /// Processes a batch of feedback items.
    /// </summary>
    /// <param name="batch">The batch of feedback to process.</param>
    /// <returns>A Result containing the aggregated updates or an error.</returns>
    Result<IReadOnlyList<LearningUpdate>, string> ProcessBatch(IEnumerable<Feedback> batch);

    /// <summary>
    /// Gets all pending updates that have not yet been applied.
    /// </summary>
    /// <returns>The list of pending updates.</returns>
    IReadOnlyList<LearningUpdate> GetPendingUpdates();

    /// <summary>
    /// Applies all accumulated updates to the internal parameters.
    /// </summary>
    /// <returns>A Result indicating success or failure.</returns>
    Result<int, string> ApplyUpdates();

    /// <summary>
    /// Gets the current value of a parameter.
    /// </summary>
    /// <param name="parameterName">The name of the parameter.</param>
    /// <returns>An Option containing the parameter value if it exists.</returns>
    Option<double> GetParameter(string parameterName);

    /// <summary>
    /// Sets a parameter value directly.
    /// </summary>
    /// <param name="parameterName">The name of the parameter.</param>
    /// <param name="value">The value to set.</param>
    void SetParameter(string parameterName, double value);
}

/// <summary>
/// Configuration for gradient-based online learning.
/// </summary>
/// <param name="LearningRate">Base learning rate for updates (typically 0.001 to 0.1).</param>
/// <param name="Momentum">Momentum coefficient for smoothing updates (0 = no momentum, 0.9 = high momentum).</param>
/// <param name="AdaptiveLearningRate">Whether to use adaptive learning rates per parameter.</param>
/// <param name="GradientClipThreshold">Maximum gradient magnitude before clipping (prevents exploding gradients).</param>
/// <param name="MinConfidenceThreshold">Minimum confidence required to apply an update.</param>
/// <param name="BatchAccumulationSize">Number of updates to accumulate before applying.</param>
public sealed record GradientLearnerConfig(
    double LearningRate,
    double Momentum,
    bool AdaptiveLearningRate,
    double GradientClipThreshold,
    double MinConfidenceThreshold,
    int BatchAccumulationSize)
{
    /// <summary>
    /// Gets the default configuration with sensible hyperparameters.
    /// </summary>
    public static GradientLearnerConfig Default => new(
        LearningRate: 0.01,
        Momentum: 0.9,
        AdaptiveLearningRate: true,
        GradientClipThreshold: 1.0,
        MinConfidenceThreshold: 0.1,
        BatchAccumulationSize: 1);

    /// <summary>
    /// Creates a conservative configuration with slower, more stable learning.
    /// </summary>
    public static GradientLearnerConfig Conservative => new(
        LearningRate: 0.001,
        Momentum: 0.95,
        AdaptiveLearningRate: true,
        GradientClipThreshold: 0.5,
        MinConfidenceThreshold: 0.3,
        BatchAccumulationSize: 10);

    /// <summary>
    /// Creates an aggressive configuration for faster learning.
    /// </summary>
    public static GradientLearnerConfig Aggressive => new(
        LearningRate: 0.1,
        Momentum: 0.5,
        AdaptiveLearningRate: false,
        GradientClipThreshold: 5.0,
        MinConfidenceThreshold: 0.0,
        BatchAccumulationSize: 1);

    /// <summary>
    /// Validates the configuration.
    /// </summary>
    /// <returns>A Result indicating success or validation errors.</returns>
    public Result<Unit, string> Validate()
    {
        if (LearningRate <= 0 || LearningRate > 1)
        {
            return Result<Unit, string>.Failure($"LearningRate must be in (0, 1], got {LearningRate}.");
        }

        if (Momentum < 0 || Momentum >= 1)
        {
            return Result<Unit, string>.Failure($"Momentum must be in [0, 1), got {Momentum}.");
        }

        if (GradientClipThreshold <= 0)
        {
            return Result<Unit, string>.Failure($"GradientClipThreshold must be positive, got {GradientClipThreshold}.");
        }

        if (MinConfidenceThreshold < 0 || MinConfidenceThreshold > 1)
        {
            return Result<Unit, string>.Failure($"MinConfidenceThreshold must be in [0, 1], got {MinConfidenceThreshold}.");
        }

        if (BatchAccumulationSize <= 0)
        {
            return Result<Unit, string>.Failure($"BatchAccumulationSize must be positive, got {BatchAccumulationSize}.");
        }

        return Result<Unit, string>.Success(Unit.Value);
    }
}

/// <summary>
/// Thread-safe gradient-based online learner implementing stochastic gradient descent
/// with momentum and adaptive learning rates.
/// </summary>
public sealed class GradientBasedLearner : IOnlineLearner
{
    private readonly object _lock = new();
    private readonly GradientLearnerConfig _config;
    private readonly ConcurrentDictionary<string, double> _parameters;
    private readonly ConcurrentDictionary<string, double> _momentumBuffer;
    private readonly ConcurrentDictionary<string, double> _squaredGradientBuffer;
    private readonly ConcurrentQueue<LearningUpdate> _pendingUpdates;
    private OnlineLearningMetrics _metrics;

    /// <summary>
    /// Initializes a new instance of the <see cref="GradientBasedLearner"/> class.
    /// </summary>
    /// <param name="config">The learner configuration.</param>
    /// <param name="initialParameters">Optional initial parameter values.</param>
    public GradientBasedLearner(
        GradientLearnerConfig? config = null,
        IReadOnlyDictionary<string, double>? initialParameters = null)
    {
        _config = config ?? GradientLearnerConfig.Default;
        _parameters = new ConcurrentDictionary<string, double>(
            initialParameters ?? new Dictionary<string, double>());
        _momentumBuffer = new ConcurrentDictionary<string, double>();
        _squaredGradientBuffer = new ConcurrentDictionary<string, double>();
        _pendingUpdates = new ConcurrentQueue<LearningUpdate>();
        _metrics = OnlineLearningMetrics.Empty;
    }

    /// <inheritdoc />
    public OnlineLearningMetrics Metrics
    {
        get
        {
            lock (_lock)
            {
                return _metrics;
            }
        }
    }

    /// <inheritdoc />
    public Result<IReadOnlyList<LearningUpdate>, string> ProcessFeedback(Feedback feedback)
    {
        var validationResult = feedback.Validate();
        if (validationResult.IsFailure)
        {
            return Result<IReadOnlyList<LearningUpdate>, string>.Failure(validationResult.Error);
        }

        try
        {
            var updates = ComputeUpdates(feedback);

            lock (_lock)
            {
                _metrics = _metrics.WithNewScore(feedback.Score);

                foreach (var update in updates)
                {
                    _pendingUpdates.Enqueue(update);
                    _metrics = _metrics.WithGradient(Math.Abs(update.Gradient));
                }
            }

            // Auto-apply if batch size reached
            if (_pendingUpdates.Count >= _config.BatchAccumulationSize)
            {
                ApplyUpdates();
            }

            return Result<IReadOnlyList<LearningUpdate>, string>.Success(updates);
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<LearningUpdate>, string>.Failure(
                $"Failed to process feedback: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Result<IReadOnlyList<LearningUpdate>, string> ProcessBatch(IEnumerable<Feedback> batch)
    {
        var feedbackList = batch.ToList();
        if (feedbackList.Count == 0)
        {
            return Result<IReadOnlyList<LearningUpdate>, string>.Success(
                Array.Empty<LearningUpdate>());
        }

        var allUpdates = new List<LearningUpdate>();
        var errors = new List<string>();

        foreach (var feedback in feedbackList)
        {
            var result = ProcessFeedback(feedback);
            if (result.IsSuccess)
            {
                allUpdates.AddRange(result.Value);
            }
            else
            {
                errors.Add(result.Error);
            }
        }

        if (errors.Count > 0 && allUpdates.Count == 0)
        {
            return Result<IReadOnlyList<LearningUpdate>, string>.Failure(
                $"All feedback processing failed: {string.Join("; ", errors)}");
        }

        // Aggregate updates by parameter
        var aggregatedUpdates = AggregateUpdates(allUpdates);

        return Result<IReadOnlyList<LearningUpdate>, string>.Success(aggregatedUpdates);
    }

    /// <inheritdoc />
    public IReadOnlyList<LearningUpdate> GetPendingUpdates()
    {
        return _pendingUpdates.ToArray();
    }

    /// <inheritdoc />
    public Result<int, string> ApplyUpdates()
    {
        try
        {
            var updates = new List<LearningUpdate>();

            while (_pendingUpdates.TryDequeue(out var update))
            {
                updates.Add(update);
            }

            if (updates.Count == 0)
            {
                return Result<int, string>.Success(0);
            }

            // Aggregate and apply
            var aggregated = AggregateUpdates(updates);
            var appliedCount = 0;

            foreach (var update in aggregated)
            {
                if (update.Confidence >= _config.MinConfidenceThreshold)
                {
                    // Apply momentum
                    var momentum = _momentumBuffer.GetOrAdd(update.ParameterName, 0.0);
                    var gradientWithMomentum = (_config.Momentum * momentum) +
                                               ((1 - _config.Momentum) * update.Gradient);
                    _momentumBuffer[update.ParameterName] = gradientWithMomentum;

                    // Compute effective learning rate (adaptive if configured)
                    var effectiveLr = ComputeEffectiveLearningRate(update.ParameterName, update.Gradient);

                    // Clip gradient
                    var clippedGradient = Math.Clamp(
                        gradientWithMomentum,
                        -_config.GradientClipThreshold,
                        _config.GradientClipThreshold);

                    // Apply update
                    var delta = -effectiveLr * clippedGradient;
                    _parameters.AddOrUpdate(
                        update.ParameterName,
                        update.OldValue + delta,
                        (_, current) => current + delta);

                    appliedCount++;
                }
            }

            return Result<int, string>.Success(appliedCount);
        }
        catch (Exception ex)
        {
            return Result<int, string>.Failure($"Failed to apply updates: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Option<double> GetParameter(string parameterName)
    {
        return _parameters.TryGetValue(parameterName, out var value)
            ? Option<double>.Some(value)
            : Option<double>.None();
    }

    /// <inheritdoc />
    public void SetParameter(string parameterName, double value)
    {
        _parameters[parameterName] = value;
    }

    /// <summary>
    /// Gets all current parameter values as an immutable dictionary.
    /// </summary>
    /// <returns>The current parameters.</returns>
    public ImmutableDictionary<string, double> GetAllParameters()
    {
        return _parameters.ToImmutableDictionary();
    }

    /// <summary>
    /// Resets the learner state while preserving parameters.
    /// </summary>
    public void ResetState()
    {
        lock (_lock)
        {
            _momentumBuffer.Clear();
            _squaredGradientBuffer.Clear();
            while (_pendingUpdates.TryDequeue(out _))
            {
            }

            _metrics = OnlineLearningMetrics.Empty;
        }
    }

    private List<LearningUpdate> ComputeUpdates(Feedback feedback)
    {
        var updates = new List<LearningUpdate>();

        // For each parameter, compute gradient based on feedback
        foreach (var kvp in _parameters)
        {
            var parameterName = kvp.Key;
            var currentValue = kvp.Value;

            // Compute gradient as negative score (we want to maximize score)
            // Scale by feedback type importance
            var typeWeight = feedback.Type switch
            {
                FeedbackType.Explicit => 1.0,
                FeedbackType.Corrective => 1.5,
                FeedbackType.Comparative => 0.8,
                FeedbackType.Implicit => 0.5,
                _ => 1.0,
            };

            // Gradient is negative of score (to maximize score via gradient descent)
            var gradient = -feedback.Score * typeWeight;

            // Confidence based on feedback type and recency
            var confidence = ComputeConfidence(feedback);

            var update = LearningUpdate.FromGradient(
                parameterName,
                currentValue,
                gradient,
                _config.LearningRate,
                confidence);

            updates.Add(update);
        }

        // If no parameters exist, create a default "bias" parameter
        if (updates.Count == 0)
        {
            var defaultParam = "bias";
            var currentValue = _parameters.GetOrAdd(defaultParam, 0.0);
            var gradient = -feedback.Score;
            var confidence = ComputeConfidence(feedback);

            updates.Add(LearningUpdate.FromGradient(
                defaultParam,
                currentValue,
                gradient,
                _config.LearningRate,
                confidence));
        }

        return updates;
    }

    private double ComputeConfidence(Feedback feedback)
    {
        // Base confidence by feedback type
        var baseConfidence = feedback.Type switch
        {
            FeedbackType.Explicit => 1.0,
            FeedbackType.Corrective => 0.95,
            FeedbackType.Comparative => 0.8,
            FeedbackType.Implicit => 0.6,
            _ => 0.5,
        };

        // Decay confidence based on age (half-life of 1 hour)
        var age = DateTime.UtcNow - feedback.Timestamp;
        var timeFactor = Math.Exp(-age.TotalHours * 0.693); // ln(2) ≈ 0.693

        return baseConfidence * timeFactor;
    }

    private double ComputeEffectiveLearningRate(string parameterName, double gradient)
    {
        if (!_config.AdaptiveLearningRate)
        {
            return _config.LearningRate;
        }

        // RMSprop-style adaptive learning rate
        var epsilon = 1e-8;
        var decayRate = 0.99;

        var squaredGrad = _squaredGradientBuffer.GetOrAdd(parameterName, 0.0);
        var newSquaredGrad = (decayRate * squaredGrad) + ((1 - decayRate) * gradient * gradient);
        _squaredGradientBuffer[parameterName] = newSquaredGrad;

        return _config.LearningRate / (Math.Sqrt(newSquaredGrad) + epsilon);
    }

    private List<LearningUpdate> AggregateUpdates(List<LearningUpdate> updates)
    {
        var grouped = updates.GroupBy(u => u.ParameterName);
        var aggregated = new List<LearningUpdate>();

        foreach (var group in grouped)
        {
            var merged = group.Aggregate((a, b) => a.MergeWith(b));
            aggregated.Add(merged);
        }

        return aggregated;
    }
}

/// <summary>
/// Provides Kleisli arrow operations for online learning pipelines.
/// </summary>
public static class OnlineLearningArrow
{
    /// <summary>
    /// Creates a step that processes a single feedback item through the learner.
    /// </summary>
    /// <param name="learner">The online learner to use.</param>
    /// <returns>A step that transforms feedback into learning updates.</returns>
    public static Step<Feedback, Result<IReadOnlyList<LearningUpdate>, string>> ProcessFeedbackStep(
        IOnlineLearner learner)
    {
        return feedback => Task.FromResult(learner.ProcessFeedback(feedback));
    }

    /// <summary>
    /// Creates a step that processes a batch of feedback items.
    /// </summary>
    /// <param name="learner">The online learner to use.</param>
    /// <returns>A step that transforms a batch of feedback into learning updates.</returns>
    public static Step<IEnumerable<Feedback>, Result<IReadOnlyList<LearningUpdate>, string>> ProcessBatchStep(
        IOnlineLearner learner)
    {
        return batch => Task.FromResult(learner.ProcessBatch(batch));
    }

    /// <summary>
    /// Creates a step that applies pending updates and returns the count.
    /// </summary>
    /// <param name="learner">The online learner to use.</param>
    /// <returns>A step that applies updates and returns the count.</returns>
    public static Step<Unit, Result<int, string>> ApplyUpdatesStep(IOnlineLearner learner)
    {
        return _ => Task.FromResult(learner.ApplyUpdates());
    }

    /// <summary>
    /// Creates a step that retrieves the current performance metrics.
    /// </summary>
    /// <param name="learner">The online learner to use.</param>
    /// <returns>A step that returns the current metrics.</returns>
    public static Step<Unit, OnlineLearningMetrics> GetMetricsStep(IOnlineLearner learner)
    {
        return _ => Task.FromResult(learner.Metrics);
    }

    /// <summary>
    /// Creates a step that extracts explicit feedback from a scored output.
    /// </summary>
    /// <param name="sourceId">The source identifier.</param>
    /// <returns>A step that creates explicit feedback from input/output/score tuples.</returns>
    public static Step<(string Input, string Output, double Score), Feedback> CreateExplicitFeedbackStep(
        string sourceId)
    {
        return tuple => Task.FromResult(
            Feedback.Explicit(sourceId, tuple.Input, tuple.Output, tuple.Score));
    }

    /// <summary>
    /// Creates a step that extracts corrective feedback from output corrections.
    /// </summary>
    /// <param name="sourceId">The source identifier.</param>
    /// <returns>A step that creates corrective feedback from input/actual/preferred tuples.</returns>
    public static Step<(string Input, string ActualOutput, string PreferredOutput), Feedback> CreateCorrectiveFeedbackStep(
        string sourceId)
    {
        return tuple => Task.FromResult(
            Feedback.Corrective(sourceId, tuple.Input, tuple.ActualOutput, tuple.PreferredOutput));
    }

    /// <summary>
    /// Composes a full learning pipeline from feedback collection to update application.
    /// </summary>
    /// <param name="learner">The online learner to use.</param>
    /// <param name="sourceId">The source identifier for feedback.</param>
    /// <returns>A step that processes scored output and returns the applied update count.</returns>
    public static Step<(string Input, string Output, double Score), Result<int, string>> FullLearningPipeline(
        IOnlineLearner learner,
        string sourceId)
    {
        return async tuple =>
        {
            var feedback = Feedback.Explicit(sourceId, tuple.Input, tuple.Output, tuple.Score);
            var processResult = learner.ProcessFeedback(feedback);

            if (processResult.IsFailure)
            {
                return Result<int, string>.Failure(processResult.Error);
            }

            return learner.ApplyUpdates();
        };
    }

    /// <summary>
    /// Creates a step that filters feedback based on a predicate.
    /// </summary>
    /// <param name="predicate">The filter predicate.</param>
    /// <returns>A step that returns Some(feedback) if predicate passes, None otherwise.</returns>
    public static Step<Feedback, Option<Feedback>> FilterFeedbackStep(
        Func<Feedback, bool> predicate)
    {
        return feedback => Task.FromResult(
            predicate(feedback)
                ? Option<Feedback>.Some(feedback)
                : Option<Feedback>.None());
    }

    /// <summary>
    /// Creates a step that enriches feedback with additional tags.
    /// </summary>
    /// <param name="tagGenerator">Function that generates tags based on feedback.</param>
    /// <returns>A step that adds generated tags to the feedback.</returns>
    public static Step<Feedback, Feedback> EnrichFeedbackStep(
        Func<Feedback, IEnumerable<string>> tagGenerator)
    {
        return feedback =>
        {
            var newTags = tagGenerator(feedback);
            return Task.FromResult(feedback.WithTags(newTags.ToArray()));
        };
    }
}

/// <summary>
/// Extension methods for integrating online learning with other pipeline components.
/// </summary>
public static class OnlineLearningExtensions
{
    /// <summary>
    /// Converts feedback to an experience for replay buffer storage.
    /// </summary>
    /// <param name="feedback">The feedback to convert.</param>
    /// <param name="nextContext">The resulting context after the feedback.</param>
    /// <returns>An Experience record for the replay buffer.</returns>
    public static Experience ToExperience(this Feedback feedback, string nextContext)
    {
        return Experience.Create(
            state: feedback.InputContext,
            action: feedback.Output,
            reward: feedback.Score,
            nextState: nextContext,
            priority: feedback.Type switch
            {
                FeedbackType.Explicit => 1.0,
                FeedbackType.Corrective => 1.5,
                FeedbackType.Comparative => 0.8,
                FeedbackType.Implicit => 0.5,
                _ => 1.0,
            },
            metadata: ImmutableDictionary<string, object>.Empty
                .Add("feedbackId", feedback.Id)
                .Add("sourceId", feedback.SourceId)
                .Add("feedbackType", feedback.Type.ToString()));
    }

    /// <summary>
    /// Converts an experience back to feedback for reprocessing.
    /// </summary>
    /// <param name="experience">The experience to convert.</param>
    /// <returns>A Feedback record derived from the experience.</returns>
    public static Feedback ToFeedback(this Experience experience)
    {
        var sourceId = experience.Metadata.TryGetValue("sourceId", out var sid)
            ? sid?.ToString() ?? "unknown"
            : "unknown";

        var feedbackType = experience.Metadata.TryGetValue("feedbackType", out var ft)
            ? Enum.TryParse<FeedbackType>(ft?.ToString(), out var parsed) ? parsed : FeedbackType.Implicit
            : FeedbackType.Implicit;

        return new Feedback(
            Id: experience.Metadata.TryGetValue("feedbackId", out var fid) && fid is Guid guid
                ? guid
                : Guid.NewGuid(),
            SourceId: sourceId,
            InputContext: experience.State,
            Output: experience.Action,
            Score: experience.Reward,
            Type: feedbackType,
            Timestamp: experience.Timestamp,
            Tags: ImmutableList<string>.Empty);
    }

    /// <summary>
    /// Creates a learning-aware step that collects feedback for each execution.
    /// </summary>
    /// <typeparam name="TInput">The input type.</typeparam>
    /// <typeparam name="TOutput">The output type.</typeparam>
    /// <param name="step">The step to wrap.</param>
    /// <param name="learner">The learner to send feedback to.</param>
    /// <param name="sourceId">The source identifier.</param>
    /// <param name="scoreFunc">Function to compute score from input and output.</param>
    /// <returns>A step that executes the original step and records feedback.</returns>
    public static Step<TInput, TOutput> WithLearning<TInput, TOutput>(
        this Step<TInput, TOutput> step,
        IOnlineLearner learner,
        string sourceId,
        Func<TInput, TOutput, double> scoreFunc)
        where TInput : notnull
        where TOutput : notnull
    {
        return async input =>
        {
            var output = await step(input);

            var score = scoreFunc(input, output);
            var feedback = Feedback.Explicit(
                sourceId,
                input.ToString()!,
                output.ToString()!,
                score);

            learner.ProcessFeedback(feedback);

            return output;
        };
    }
}
