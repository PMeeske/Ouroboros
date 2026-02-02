// <copyright file="AdaptiveAgent.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Learning;

using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Ouroboros.Core.Monads;
using Ouroboros.Core.Steps;

/// <summary>
/// Specifies the type of adaptation event that occurred.
/// </summary>
public enum AdaptationEventType
{
    /// <summary>
    /// The agent changed its overall strategy or approach.
    /// </summary>
    StrategyChange,

    /// <summary>
    /// Fine-tuning of parameters without changing the overall strategy.
    /// </summary>
    ParameterTune,

    /// <summary>
    /// Update to the underlying model or weights.
    /// </summary>
    ModelUpdate,

    /// <summary>
    /// Acquisition of a new skill or capability.
    /// </summary>
    SkillAcquisition,

    /// <summary>
    /// Reverting a previous adaptation due to performance degradation.
    /// </summary>
    Rollback,
}

/// <summary>
/// Tracks agent performance metrics over time.
/// Provides immutable snapshots of agent capability and learning progress.
/// </summary>
/// <param name="AgentId">Unique identifier for the agent being tracked.</param>
/// <param name="TotalInteractions">Total number of interactions processed by the agent.</param>
/// <param name="SuccessRate">Ratio of successful interactions (0.0 to 1.0).</param>
/// <param name="AverageResponseQuality">Mean quality score of responses (-1.0 to 1.0).</param>
/// <param name="LearningCurve">Historical performance values over time for trend analysis.</param>
/// <param name="LastUpdated">Timestamp of the most recent metric update.</param>
public sealed record AgentPerformance(
    Guid AgentId,
    long TotalInteractions,
    double SuccessRate,
    double AverageResponseQuality,
    ImmutableList<double> LearningCurve,
    DateTime LastUpdated)
{
    /// <summary>
    /// Creates initial performance metrics for a new agent.
    /// </summary>
    /// <param name="agentId">The unique identifier for the agent.</param>
    /// <returns>An AgentPerformance instance with zeroed metrics.</returns>
    public static AgentPerformance Initial(Guid agentId) => new(
        AgentId: agentId,
        TotalInteractions: 0,
        SuccessRate: 0.0,
        AverageResponseQuality: 0.0,
        LearningCurve: ImmutableList<double>.Empty,
        LastUpdated: DateTime.UtcNow);

    /// <summary>
    /// Creates a performance snapshot with an updated learning curve entry.
    /// </summary>
    /// <param name="currentPerformance">The current performance value to record.</param>
    /// <param name="maxCurveLength">Maximum length of the learning curve history (default: 100).</param>
    /// <returns>A new AgentPerformance with the updated learning curve.</returns>
    public AgentPerformance WithLearningCurveEntry(double currentPerformance, int maxCurveLength = 100)
    {
        var newCurve = LearningCurve.Add(currentPerformance);

        // Trim curve if it exceeds maximum length
        if (newCurve.Count > maxCurveLength)
        {
            newCurve = newCurve.RemoveRange(0, newCurve.Count - maxCurveLength);
        }

        return this with
        {
            LearningCurve = newCurve,
            LastUpdated = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Calculates the trend of recent performance (positive = improving, negative = declining).
    /// Uses the slope of the last N entries in the learning curve.
    /// </summary>
    /// <param name="windowSize">Number of recent entries to consider (default: 10).</param>
    /// <returns>The performance trend as a slope value.</returns>
    public double CalculateTrend(int windowSize = 10)
    {
        if (LearningCurve.Count < 2)
        {
            return 0.0;
        }

        var effectiveWindow = Math.Min(windowSize, LearningCurve.Count);
        var recentValues = LearningCurve.Skip(LearningCurve.Count - effectiveWindow).ToList();

        // Simple linear regression slope
        double sumX = 0, sumY = 0, sumXY = 0, sumX2 = 0;
        for (int i = 0; i < recentValues.Count; i++)
        {
            sumX += i;
            sumY += recentValues[i];
            sumXY += i * recentValues[i];
            sumX2 += i * i;
        }

        var n = recentValues.Count;
        var denominator = (n * sumX2) - (sumX * sumX);
        return denominator == 0 ? 0.0 : ((n * sumXY) - (sumX * sumY)) / denominator;
    }

    /// <summary>
    /// Determines if performance is stagnating based on variance in recent performance.
    /// </summary>
    /// <param name="windowSize">Number of recent entries to analyze.</param>
    /// <param name="varianceThreshold">Threshold below which performance is considered stagnant.</param>
    /// <returns>True if performance shows signs of stagnation.</returns>
    public bool IsStagnating(int windowSize = 10, double varianceThreshold = 0.001)
    {
        if (LearningCurve.Count < windowSize)
        {
            return false;
        }

        var recentValues = LearningCurve.Skip(LearningCurve.Count - windowSize).ToList();
        var mean = recentValues.Average();
        var variance = recentValues.Sum(v => Math.Pow(v - mean, 2)) / windowSize;

        return variance < varianceThreshold;
    }
}

/// <summary>
/// Records an adaptation decision made by the agent.
/// Captures the context, rationale, and impact of adaptations for analysis and rollback.
/// </summary>
/// <param name="Id">Unique identifier for this adaptation event.</param>
/// <param name="AgentId">Identifier of the agent that adapted.</param>
/// <param name="EventType">The type of adaptation that occurred.</param>
/// <param name="Description">Human-readable description of what changed and why.</param>
/// <param name="BeforeMetrics">Performance snapshot before the adaptation.</param>
/// <param name="AfterMetrics">Performance snapshot after the adaptation (null if not yet measured).</param>
/// <param name="Timestamp">When the adaptation occurred.</param>
public sealed record AdaptationEvent(
    Guid Id,
    Guid AgentId,
    AdaptationEventType EventType,
    string Description,
    AgentPerformance BeforeMetrics,
    AgentPerformance? AfterMetrics,
    DateTime Timestamp)
{
    /// <summary>
    /// Creates a new adaptation event with auto-generated ID and current timestamp.
    /// </summary>
    /// <param name="agentId">The ID of the adapting agent.</param>
    /// <param name="eventType">The type of adaptation.</param>
    /// <param name="description">Description of the adaptation.</param>
    /// <param name="beforeMetrics">Performance before adaptation.</param>
    /// <returns>A new AdaptationEvent instance.</returns>
    public static AdaptationEvent Create(
        Guid agentId,
        AdaptationEventType eventType,
        string description,
        AgentPerformance beforeMetrics) => new(
            Id: Guid.NewGuid(),
            AgentId: agentId,
            EventType: eventType,
            Description: description,
            BeforeMetrics: beforeMetrics,
            AfterMetrics: null,
            Timestamp: DateTime.UtcNow);

    /// <summary>
    /// Creates a copy with the after-metrics populated.
    /// </summary>
    /// <param name="afterMetrics">The performance metrics after adaptation.</param>
    /// <returns>A new AdaptationEvent with after-metrics set.</returns>
    public AdaptationEvent WithAfterMetrics(AgentPerformance afterMetrics)
        => this with { AfterMetrics = afterMetrics };

    /// <summary>
    /// Calculates the performance delta caused by this adaptation.
    /// </summary>
    /// <returns>The change in average response quality, or null if after-metrics not available.</returns>
    public double? PerformanceDelta => AfterMetrics is not null
        ? AfterMetrics.AverageResponseQuality - BeforeMetrics.AverageResponseQuality
        : null;

    /// <summary>
    /// Determines if this adaptation was beneficial (improved performance).
    /// </summary>
    /// <returns>True if performance improved, false if declined, null if not yet measured.</returns>
    public bool? WasBeneficial => PerformanceDelta.HasValue
        ? PerformanceDelta.Value > 0
        : null;
}

/// <summary>
/// Configuration for adaptive agent behavior.
/// </summary>
/// <param name="AdaptationThreshold">Minimum performance decline to trigger adaptation.</param>
/// <param name="RollbackThreshold">Performance decline after adaptation that triggers rollback.</param>
/// <param name="MinInteractionsBeforeAdaptation">Minimum interactions before allowing adaptation.</param>
/// <param name="EmaAlpha">Exponential moving average smoothing factor (0 &lt; alpha &lt;= 1).</param>
/// <param name="StagnationWindowSize">Window size for detecting stagnation.</param>
/// <param name="MaxAdaptationHistory">Maximum number of adaptation events to retain.</param>
public sealed record AdaptiveAgentConfig(
    double AdaptationThreshold = 0.1,
    double RollbackThreshold = 0.15,
    int MinInteractionsBeforeAdaptation = 50,
    double EmaAlpha = 0.1,
    int StagnationWindowSize = 20,
    int MaxAdaptationHistory = 100)
{
    /// <summary>
    /// Gets the default configuration with sensible defaults.
    /// </summary>
    public static AdaptiveAgentConfig Default => new();

    /// <summary>
    /// Configuration optimized for rapid adaptation.
    /// </summary>
    public static AdaptiveAgentConfig Aggressive => new(
        AdaptationThreshold: 0.05,
        RollbackThreshold: 0.1,
        MinInteractionsBeforeAdaptation: 20,
        EmaAlpha: 0.2,
        StagnationWindowSize: 10,
        MaxAdaptationHistory: 200);

    /// <summary>
    /// Configuration optimized for stability.
    /// </summary>
    public static AdaptiveAgentConfig Conservative => new(
        AdaptationThreshold: 0.2,
        RollbackThreshold: 0.25,
        MinInteractionsBeforeAdaptation: 100,
        EmaAlpha: 0.05,
        StagnationWindowSize: 50,
        MaxAdaptationHistory: 50);
}

/// <summary>
/// Interface for agents that continuously learn and adapt their behavior.
/// </summary>
public interface IAdaptiveAgent
{
    /// <summary>
    /// Gets the unique identifier of this agent.
    /// </summary>
    Guid AgentId { get; }

    /// <summary>
    /// Records an interaction and updates performance metrics.
    /// </summary>
    /// <param name="input">The input that was processed.</param>
    /// <param name="output">The output that was generated.</param>
    /// <param name="quality">Quality score of the interaction (-1.0 to 1.0).</param>
    /// <returns>Result indicating success or failure of the recording.</returns>
    Result<AgentPerformance, string> RecordInteraction(string input, string output, double quality);

    /// <summary>
    /// Determines if the agent should adapt based on current performance metrics.
    /// </summary>
    /// <returns>True if adaptation is recommended.</returns>
    bool ShouldAdapt();

    /// <summary>
    /// Performs an adaptation to improve agent behavior.
    /// </summary>
    /// <returns>Result containing the adaptation event if successful.</returns>
    Result<AdaptationEvent, string> Adapt();

    /// <summary>
    /// Gets the current performance metrics of the agent.
    /// </summary>
    /// <returns>The current AgentPerformance snapshot.</returns>
    AgentPerformance GetPerformance();

    /// <summary>
    /// Gets the history of adaptation events.
    /// </summary>
    /// <returns>Immutable list of adaptation events in chronological order.</returns>
    ImmutableList<AdaptationEvent> GetAdaptationHistory();

    /// <summary>
    /// Rolls back a specific adaptation by ID.
    /// </summary>
    /// <param name="adaptationId">The ID of the adaptation to rollback.</param>
    /// <returns>Result containing the rollback event if successful.</returns>
    Result<AdaptationEvent, string> Rollback(Guid adaptationId);
}

/// <summary>
/// Represents the internal state of an adaptive agent's learning strategy.
/// </summary>
/// <param name="CurrentStrategy">The active learning strategy.</param>
/// <param name="BaselinePerformance">The baseline performance level to compare against.</param>
/// <param name="PreviousStrategies">Stack of previous strategies for rollback support.</param>
internal sealed record AdaptiveState(
    LearningStrategy CurrentStrategy,
    double BaselinePerformance,
    ImmutableStack<LearningStrategy> PreviousStrategies)
{
    public static AdaptiveState Initial() => new(
        CurrentStrategy: LearningStrategy.Default,
        BaselinePerformance: 0.0,
        PreviousStrategies: ImmutableStack<LearningStrategy>.Empty);
}

/// <summary>
/// An agent that continuously learns and adapts its behavior based on feedback.
/// Uses exponential moving average for performance tracking and maintains adaptation history for rollback.
/// Thread-safe implementation suitable for concurrent access.
/// </summary>
public sealed class ContinuouslyLearningAgent : IAdaptiveAgent
{
    private readonly object _lock = new();
    private readonly AdaptiveAgentConfig _config;
    private readonly ExperienceBuffer _experienceBuffer;

    private AgentPerformance _performance;
    private AdaptiveState _state;
    private ImmutableList<AdaptationEvent> _adaptationHistory;

    // Exponential moving average state
    private double _emaQuality;
    private double _emaSuccessRate;
    private bool _emaInitialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="ContinuouslyLearningAgent"/> class.
    /// </summary>
    /// <param name="agentId">Optional agent ID (auto-generated if not provided).</param>
    /// <param name="config">Optional configuration (defaults used if not provided).</param>
    /// <param name="bufferCapacity">Capacity of the internal experience buffer.</param>
    public ContinuouslyLearningAgent(
        Guid? agentId = null,
        AdaptiveAgentConfig? config = null,
        int bufferCapacity = 10000)
    {
        AgentId = agentId ?? Guid.NewGuid();
        _config = config ?? AdaptiveAgentConfig.Default;
        _experienceBuffer = new ExperienceBuffer(bufferCapacity);
        _performance = AgentPerformance.Initial(AgentId);
        _state = AdaptiveState.Initial();
        _adaptationHistory = ImmutableList<AdaptationEvent>.Empty;
        _emaQuality = 0.0;
        _emaSuccessRate = 0.0;
        _emaInitialized = false;
    }

    /// <inheritdoc/>
    public Guid AgentId { get; }

    /// <inheritdoc/>
    public Result<AgentPerformance, string> RecordInteraction(string input, string output, double quality)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return Result<AgentPerformance, string>.Failure("Input cannot be empty");
        }

        if (string.IsNullOrWhiteSpace(output))
        {
            return Result<AgentPerformance, string>.Failure("Output cannot be empty");
        }

        var clampedQuality = Math.Clamp(quality, -1.0, 1.0);
        var isSuccess = clampedQuality > 0;

        lock (_lock)
        {
            // Add to experience buffer for potential replay
            var experience = Experience.Create(
                state: input,
                action: output,
                reward: clampedQuality,
                nextState: string.Empty,
                priority: Math.Abs(clampedQuality) + 0.1);
            _experienceBuffer.Add(experience);

            // Update EMA
            UpdateExponentialMovingAverage(clampedQuality, isSuccess);

            // Update performance record
            var newTotalInteractions = _performance.TotalInteractions + 1;

            _performance = _performance with
            {
                TotalInteractions = newTotalInteractions,
                SuccessRate = _emaSuccessRate,
                AverageResponseQuality = _emaQuality,
                LastUpdated = DateTime.UtcNow,
            };

            // Record to learning curve periodically
            if (newTotalInteractions % 10 == 0)
            {
                _performance = _performance.WithLearningCurveEntry(_emaQuality);
            }

            return Result<AgentPerformance, string>.Success(_performance);
        }
    }

    /// <inheritdoc/>
    public bool ShouldAdapt()
    {
        lock (_lock)
        {
            // Don't adapt too early
            if (_performance.TotalInteractions < _config.MinInteractionsBeforeAdaptation)
            {
                return false;
            }

            // Check for performance decline
            var performanceDecline = _state.BaselinePerformance - _emaQuality;
            if (performanceDecline > _config.AdaptationThreshold)
            {
                return true;
            }

            // Check for stagnation
            if (_performance.IsStagnating(_config.StagnationWindowSize))
            {
                return true;
            }

            // Check for negative trend
            var trend = _performance.CalculateTrend(_config.StagnationWindowSize);
            if (trend < -_config.AdaptationThreshold / 10)
            {
                return true;
            }

            return false;
        }
    }

    /// <inheritdoc/>
    public Result<AdaptationEvent, string> Adapt()
    {
        lock (_lock)
        {
            if (_performance.TotalInteractions < _config.MinInteractionsBeforeAdaptation)
            {
                return Result<AdaptationEvent, string>.Failure(
                    $"Insufficient interactions for adaptation: {_performance.TotalInteractions} < {_config.MinInteractionsBeforeAdaptation}");
            }

            // Determine adaptation type based on current state
            var (eventType, description, newStrategy) = DetermineAdaptation();

            // Create adaptation event
            var adaptationEvent = AdaptationEvent.Create(
                AgentId,
                eventType,
                description,
                _performance);

            // Apply the adaptation
            var previousStrategy = _state.CurrentStrategy;
            _state = _state with
            {
                CurrentStrategy = newStrategy,
                BaselinePerformance = _emaQuality,
                PreviousStrategies = _state.PreviousStrategies.Push(previousStrategy),
            };

            // Add to history with size limit
            _adaptationHistory = _adaptationHistory.Add(adaptationEvent);
            if (_adaptationHistory.Count > _config.MaxAdaptationHistory)
            {
                _adaptationHistory = _adaptationHistory.RemoveAt(0);
            }

            return Result<AdaptationEvent, string>.Success(adaptationEvent);
        }
    }

    /// <inheritdoc/>
    public AgentPerformance GetPerformance()
    {
        lock (_lock)
        {
            return _performance;
        }
    }

    /// <inheritdoc/>
    public ImmutableList<AdaptationEvent> GetAdaptationHistory()
    {
        lock (_lock)
        {
            return _adaptationHistory;
        }
    }

    /// <inheritdoc/>
    public Result<AdaptationEvent, string> Rollback(Guid adaptationId)
    {
        lock (_lock)
        {
            // Find the adaptation to rollback
            var adaptationIndex = _adaptationHistory.FindIndex(e => e.Id == adaptationId);
            if (adaptationIndex < 0)
            {
                return Result<AdaptationEvent, string>.Failure($"Adaptation not found: {adaptationId}");
            }

            var targetAdaptation = _adaptationHistory[adaptationIndex];

            // Can only rollback if we have previous strategies
            if (_state.PreviousStrategies.IsEmpty)
            {
                return Result<AdaptationEvent, string>.Failure("No previous strategies available for rollback");
            }

            // Pop the previous strategy
            var previousStrategy = _state.PreviousStrategies.Peek();
            var remainingStack = _state.PreviousStrategies.Pop();

            // Create rollback event
            var rollbackEvent = AdaptationEvent.Create(
                AgentId,
                AdaptationEventType.Rollback,
                $"Rolled back adaptation: {targetAdaptation.Description}",
                _performance);

            // Apply rollback
            _state = _state with
            {
                CurrentStrategy = previousStrategy,
                BaselinePerformance = targetAdaptation.BeforeMetrics.AverageResponseQuality,
                PreviousStrategies = remainingStack,
            };

            // Update the original adaptation with after-metrics
            _adaptationHistory = _adaptationHistory.SetItem(
                adaptationIndex,
                targetAdaptation.WithAfterMetrics(_performance));

            // Add rollback event
            _adaptationHistory = _adaptationHistory.Add(rollbackEvent);
            if (_adaptationHistory.Count > _config.MaxAdaptationHistory)
            {
                _adaptationHistory = _adaptationHistory.RemoveAt(0);
            }

            return Result<AdaptationEvent, string>.Success(rollbackEvent);
        }
    }

    /// <summary>
    /// Gets the current learning strategy being used.
    /// </summary>
    /// <returns>The current LearningStrategy.</returns>
    public LearningStrategy GetCurrentStrategy()
    {
        lock (_lock)
        {
            return _state.CurrentStrategy;
        }
    }

    /// <summary>
    /// Gets the number of experiences in the buffer.
    /// </summary>
    /// <returns>The count of stored experiences.</returns>
    public int GetExperienceCount() => _experienceBuffer.Count;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdateExponentialMovingAverage(double quality, bool isSuccess)
    {
        var alpha = _config.EmaAlpha;
        var successValue = isSuccess ? 1.0 : 0.0;

        if (!_emaInitialized)
        {
            _emaQuality = quality;
            _emaSuccessRate = successValue;
            _emaInitialized = true;
        }
        else
        {
            _emaQuality = (alpha * quality) + ((1 - alpha) * _emaQuality);
            _emaSuccessRate = (alpha * successValue) + ((1 - alpha) * _emaSuccessRate);
        }
    }

    private (AdaptationEventType Type, string Description, LearningStrategy NewStrategy) DetermineAdaptation()
    {
        var currentStrategy = _state.CurrentStrategy;
        var trend = _performance.CalculateTrend(_config.StagnationWindowSize);
        var isStagnating = _performance.IsStagnating(_config.StagnationWindowSize);

        // If stagnating with low exploration, increase exploration
        if (isStagnating && currentStrategy.ExplorationRate < 0.3)
        {
            var newStrategy = currentStrategy
                .WithExplorationRate(Math.Min(currentStrategy.ExplorationRate * 2, 0.5))
                .WithLearningRate(Math.Min(currentStrategy.LearningRate * 1.5, 0.1));
            return (
                AdaptationEventType.StrategyChange,
                $"Increased exploration due to stagnation (ε: {currentStrategy.ExplorationRate:F3} → {newStrategy.ExplorationRate:F3})",
                newStrategy);
        }

        // If performance is declining, adjust learning rate
        if (trend < -0.01)
        {
            var newStrategy = currentStrategy
                .WithLearningRate(currentStrategy.LearningRate * 0.5);
            return (
                AdaptationEventType.ParameterTune,
                $"Reduced learning rate due to declining performance (lr: {currentStrategy.LearningRate:F5} → {newStrategy.LearningRate:F5})",
                newStrategy);
        }

        // If performing well, reduce exploration for exploitation
        if (_emaQuality > 0.7 && currentStrategy.ExplorationRate > 0.1)
        {
            var newStrategy = currentStrategy
                .WithExplorationRate(currentStrategy.ExplorationRate * 0.7);
            return (
                AdaptationEventType.ParameterTune,
                $"Reduced exploration for exploitation phase (ε: {currentStrategy.ExplorationRate:F3} → {newStrategy.ExplorationRate:F3})",
                newStrategy);
        }

        // Default: small parameter adjustment
        var defaultNewStrategy = currentStrategy
            .WithLearningRate(currentStrategy.LearningRate * 0.9);
        return (
            AdaptationEventType.ParameterTune,
            $"Minor learning rate adjustment (lr: {currentStrategy.LearningRate:F5} → {defaultNewStrategy.LearningRate:F5})",
            defaultNewStrategy);
    }
}

/// <summary>
/// Provides Kleisli arrow operations for adaptive agent pipelines.
/// </summary>
public static class AdaptiveAgentArrow
{
    /// <summary>
    /// Creates a step that records an interaction and returns updated performance.
    /// </summary>
    /// <param name="agent">The adaptive agent to record interactions for.</param>
    /// <returns>A step that transforms interaction data into performance results.</returns>
    public static Step<(string Input, string Output, double Quality), Result<AgentPerformance, string>> RecordInteractionStep(
        IAdaptiveAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        return tuple => Task.FromResult(agent.RecordInteraction(tuple.Input, tuple.Output, tuple.Quality));
    }

    /// <summary>
    /// Creates a step that checks if adaptation is needed and performs it if so.
    /// </summary>
    /// <param name="agent">The adaptive agent to potentially adapt.</param>
    /// <returns>A step that returns the adaptation event if adaptation occurred, or None if not.</returns>
    public static Step<Unit, Option<AdaptationEvent>> TryAdaptStep(IAdaptiveAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        return _ =>
        {
            if (agent.ShouldAdapt())
            {
                var result = agent.Adapt();
                return Task.FromResult(result.IsSuccess
                    ? Option<AdaptationEvent>.Some(result.Value)
                    : Option<AdaptationEvent>.None());
            }

            return Task.FromResult(Option<AdaptationEvent>.None());
        };
    }

    /// <summary>
    /// Creates a step that retrieves the current performance metrics.
    /// </summary>
    /// <param name="agent">The adaptive agent to query.</param>
    /// <returns>A step that returns the current performance snapshot.</returns>
    public static Step<Unit, AgentPerformance> GetPerformanceStep(IAdaptiveAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        return _ => Task.FromResult(agent.GetPerformance());
    }

    /// <summary>
    /// Creates a step that retrieves the adaptation history.
    /// </summary>
    /// <param name="agent">The adaptive agent to query.</param>
    /// <returns>A step that returns the adaptation history.</returns>
    public static Step<Unit, ImmutableList<AdaptationEvent>> GetAdaptationHistoryStep(IAdaptiveAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        return _ => Task.FromResult(agent.GetAdaptationHistory());
    }

    /// <summary>
    /// Creates a step that rolls back a specific adaptation.
    /// </summary>
    /// <param name="agent">The adaptive agent to perform rollback on.</param>
    /// <returns>A step that returns the rollback event if successful.</returns>
    public static Step<Guid, Result<AdaptationEvent, string>> RollbackStep(IAdaptiveAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        return adaptationId => Task.FromResult(agent.Rollback(adaptationId));
    }

    /// <summary>
    /// Creates a full learning pipeline that records an interaction and optionally triggers adaptation.
    /// </summary>
    /// <param name="agent">The adaptive agent.</param>
    /// <returns>A step that processes interaction data and returns the performance along with any adaptation.</returns>
    public static Step<(string Input, string Output, double Quality), Result<(AgentPerformance Performance, AdaptationEvent? Adaptation), string>> FullLearningPipeline(
        IAdaptiveAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        return async tuple =>
        {
            // Record the interaction
            var recordResult = agent.RecordInteraction(tuple.Input, tuple.Output, tuple.Quality);
            if (recordResult.IsFailure)
            {
                return Result<(AgentPerformance Performance, AdaptationEvent? Adaptation), string>.Failure(recordResult.Error);
            }

            // Check for and perform adaptation if needed
            AdaptationEvent? adaptation = null;
            if (agent.ShouldAdapt())
            {
                var adaptResult = agent.Adapt();
                if (adaptResult.IsSuccess)
                {
                    adaptation = adaptResult.Value;
                }
            }

            await Task.CompletedTask; // Ensure async context
            return Result<(AgentPerformance Performance, AdaptationEvent? Adaptation), string>.Success(
                (recordResult.Value, adaptation));
        };
    }

    /// <summary>
    /// Creates a step that processes a batch of interactions.
    /// </summary>
    /// <param name="agent">The adaptive agent.</param>
    /// <returns>A step that processes multiple interactions and returns aggregated results.</returns>
    public static Step<IEnumerable<(string Input, string Output, double Quality)>, Result<AgentPerformance, string>> ProcessBatchStep(
        IAdaptiveAgent agent)
    {
        ArgumentNullException.ThrowIfNull(agent);
        return interactions =>
        {
            Result<AgentPerformance, string> lastResult = Result<AgentPerformance, string>.Failure("No interactions provided");

            foreach (var (input, output, quality) in interactions)
            {
                lastResult = agent.RecordInteraction(input, output, quality);
                if (!lastResult.IsSuccess)
                {
                    return Task.FromResult(lastResult);
                }
            }

            return Task.FromResult(lastResult);
        };
    }

    /// <summary>
    /// Creates a conditional adaptation step that only adapts when a predicate is satisfied.
    /// </summary>
    /// <param name="agent">The adaptive agent.</param>
    /// <param name="predicate">Predicate to determine if adaptation should proceed.</param>
    /// <returns>A step that conditionally adapts based on the predicate.</returns>
    public static Step<AgentPerformance, Option<AdaptationEvent>> ConditionalAdaptStep(
        IAdaptiveAgent agent,
        Func<AgentPerformance, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(agent);
        ArgumentNullException.ThrowIfNull(predicate);

        return performance =>
        {
            if (predicate(performance) && agent.ShouldAdapt())
            {
                var result = agent.Adapt();
                return Task.FromResult(result.IsSuccess
                    ? Option<AdaptationEvent>.Some(result.Value)
                    : Option<AdaptationEvent>.None());
            }

            return Task.FromResult(Option<AdaptationEvent>.None());
        };
    }
}
