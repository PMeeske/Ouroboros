// --------------------------------------------------------------------------------------------------------------------
// <copyright file="Introspection.cs" company="Ouroboros">
//   Copyright (c) Ouroboros. All rights reserved.
//   Licensed under the MIT License.
// </copyright>
// <summary>
//   Implements introspection capabilities for self-reflection and metacognition.
//   Provides mechanisms to examine and reason about internal cognitive states.
// </summary>
// --------------------------------------------------------------------------------------------------------------------

namespace Ouroboros.Pipeline.Metacognition;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Ouroboros.Core.Monads;

/// <summary>
/// Processing mode for cognitive operations, representing different mental stances.
/// </summary>
public enum ProcessingMode
{
    /// <summary>
    /// Systematic, logical reasoning focused on analysis and decomposition.
    /// </summary>
    Analytical,

    /// <summary>
    /// Generative, exploratory thinking focused on novel combinations.
    /// </summary>
    Creative,

    /// <summary>
    /// Fast, stimulus-driven responses with minimal deliberation.
    /// </summary>
    Reactive,

    /// <summary>
    /// Meta-level reasoning about own processes and states.
    /// </summary>
    Reflective,

    /// <summary>
    /// Pattern-based recognition drawing on implicit knowledge.
    /// </summary>
    Intuitive
}

/// <summary>
/// Represents a snapshot of the agent's internal cognitive state at a specific moment.
/// This immutable record captures all relevant aspects of cognitive functioning.
/// </summary>
public sealed record InternalState(
    Guid Id,
    DateTime Timestamp,
    ImmutableList<string> ActiveGoals,
    string CurrentFocus,
    double CognitiveLoad,
    double EmotionalValence,
    ImmutableDictionary<string, double> AttentionDistribution,
    ImmutableList<string> WorkingMemoryItems,
    ProcessingMode Mode)
{
    /// <summary>
    /// Creates an initial state with default values.
    /// </summary>
    /// <returns>A new InternalState with neutral defaults.</returns>
    public static InternalState Initial() => new(
        Guid.NewGuid(),
        DateTime.UtcNow,
        ImmutableList<string>.Empty,
        "None",
        0.0,
        0.0,
        ImmutableDictionary<string, double>.Empty,
        ImmutableList<string>.Empty,
        ProcessingMode.Reactive);

    /// <summary>
    /// Creates a copy with updated timestamp and new ID.
    /// </summary>
    /// <returns>A fresh snapshot based on current state.</returns>
    public InternalState Snapshot() => this with
    {
        Id = Guid.NewGuid(),
        Timestamp = DateTime.UtcNow
    };

    /// <summary>
    /// Adds a goal to the active goals list.
    /// </summary>
    /// <param name="goal">The goal to add.</param>
    /// <returns>New state with the goal added.</returns>
    public InternalState WithGoal(string goal) =>
        string.IsNullOrWhiteSpace(goal)
            ? this
            : this with { ActiveGoals = ActiveGoals.Add(goal) };

    /// <summary>
    /// Removes a goal from the active goals list.
    /// </summary>
    /// <param name="goal">The goal to remove.</param>
    /// <returns>New state with the goal removed.</returns>
    public InternalState WithoutGoal(string goal) =>
        this with { ActiveGoals = ActiveGoals.Remove(goal) };

    /// <summary>
    /// Updates the current focus.
    /// </summary>
    /// <param name="focus">The new focus area.</param>
    /// <returns>New state with updated focus.</returns>
    public InternalState WithFocus(string focus) =>
        this with { CurrentFocus = focus ?? "None" };

    /// <summary>
    /// Updates the cognitive load value.
    /// </summary>
    /// <param name="load">The new load value (clamped to 0-1).</param>
    /// <returns>New state with updated cognitive load.</returns>
    public InternalState WithCognitiveLoad(double load) =>
        this with { CognitiveLoad = Math.Clamp(load, 0.0, 1.0) };

    /// <summary>
    /// Updates the emotional valence.
    /// </summary>
    /// <param name="valence">The new valence value (clamped to -1 to 1).</param>
    /// <returns>New state with updated valence.</returns>
    public InternalState WithValence(double valence) =>
        this with { EmotionalValence = Math.Clamp(valence, -1.0, 1.0) };

    /// <summary>
    /// Adds an item to working memory.
    /// </summary>
    /// <param name="item">The item to add.</param>
    /// <returns>New state with the item in working memory.</returns>
    public InternalState WithWorkingMemoryItem(string item) =>
        string.IsNullOrWhiteSpace(item)
            ? this
            : this with { WorkingMemoryItems = WorkingMemoryItems.Add(item) };

    /// <summary>
    /// Sets attention distribution.
    /// </summary>
    /// <param name="distribution">The attention distribution map.</param>
    /// <returns>New state with updated attention.</returns>
    public InternalState WithAttention(ImmutableDictionary<string, double> distribution) =>
        this with { AttentionDistribution = distribution };

    /// <summary>
    /// Changes the processing mode.
    /// </summary>
    /// <param name="mode">The new processing mode.</param>
    /// <returns>New state with updated mode.</returns>
    public InternalState WithMode(ProcessingMode mode) =>
        this with { Mode = mode };
}

/// <summary>
/// Report generated from introspection analysis of an internal state.
/// Contains observations, detected anomalies, and recommendations.
/// </summary>
public sealed record IntrospectionReport(
    InternalState StateSnapshot,
    ImmutableList<string> Observations,
    ImmutableList<string> Anomalies,
    ImmutableList<string> Recommendations,
    double SelfAssessmentScore,
    DateTime GeneratedAt)
{
    /// <summary>
    /// Creates an empty report for a given state.
    /// </summary>
    /// <param name="state">The state to report on.</param>
    /// <returns>An empty introspection report.</returns>
    public static IntrospectionReport Empty(InternalState state) => new(
        state,
        ImmutableList<string>.Empty,
        ImmutableList<string>.Empty,
        ImmutableList<string>.Empty,
        0.5,
        DateTime.UtcNow);

    /// <summary>
    /// Indicates whether the report contains any anomalies.
    /// </summary>
    public bool HasAnomalies => !Anomalies.IsEmpty;

    /// <summary>
    /// Indicates whether the report contains recommendations.
    /// </summary>
    public bool HasRecommendations => !Recommendations.IsEmpty;

    /// <summary>
    /// Adds an observation to the report.
    /// </summary>
    /// <param name="observation">The observation to add.</param>
    /// <returns>New report with the observation added.</returns>
    public IntrospectionReport WithObservation(string observation) =>
        this with { Observations = Observations.Add(observation) };

    /// <summary>
    /// Adds an anomaly to the report.
    /// </summary>
    /// <param name="anomaly">The anomaly to add.</param>
    /// <returns>New report with the anomaly added.</returns>
    public IntrospectionReport WithAnomaly(string anomaly) =>
        this with { Anomalies = Anomalies.Add(anomaly) };

    /// <summary>
    /// Adds a recommendation to the report.
    /// </summary>
    /// <param name="recommendation">The recommendation to add.</param>
    /// <returns>New report with the recommendation added.</returns>
    public IntrospectionReport WithRecommendation(string recommendation) =>
        this with { Recommendations = Recommendations.Add(recommendation) };
}

/// <summary>
/// Represents a comparison between two internal states, tracking deltas and changes.
/// </summary>
public sealed record StateComparison(
    InternalState Before,
    InternalState After,
    double CognitiveLoadDelta,
    double ValenceDelta,
    ImmutableDictionary<string, double> AttentionChanges,
    string Interpretation)
{
    /// <summary>
    /// Creates a comparison between two states with computed deltas.
    /// </summary>
    /// <param name="before">The earlier state.</param>
    /// <param name="after">The later state.</param>
    /// <returns>A StateComparison with computed deltas.</returns>
    public static StateComparison Create(InternalState before, InternalState after)
    {
        var loadDelta = after.CognitiveLoad - before.CognitiveLoad;
        var valenceDelta = after.EmotionalValence - before.EmotionalValence;
        var attentionChanges = ComputeAttentionChanges(before.AttentionDistribution, after.AttentionDistribution);
        var interpretation = InterpretChanges(loadDelta, valenceDelta, attentionChanges, before.Mode, after.Mode);

        return new StateComparison(before, after, loadDelta, valenceDelta, attentionChanges, interpretation);
    }

    /// <summary>
    /// Time elapsed between the two states.
    /// </summary>
    public TimeSpan TimeElapsed => After.Timestamp - Before.Timestamp;

    /// <summary>
    /// Indicates whether cognitive load increased significantly.
    /// </summary>
    public bool CognitiveLoadIncreased => CognitiveLoadDelta > 0.1;

    /// <summary>
    /// Indicates whether cognitive load decreased significantly.
    /// </summary>
    public bool CognitiveLoadDecreased => CognitiveLoadDelta < -0.1;

    /// <summary>
    /// Indicates whether the processing mode changed.
    /// </summary>
    public bool ModeChanged => Before.Mode != After.Mode;

    /// <summary>
    /// Goals added between states.
    /// </summary>
    public ImmutableList<string> GoalsAdded =>
        After.ActiveGoals.Except(Before.ActiveGoals).ToImmutableList();

    /// <summary>
    /// Goals removed between states.
    /// </summary>
    public ImmutableList<string> GoalsRemoved =>
        Before.ActiveGoals.Except(After.ActiveGoals).ToImmutableList();

    private static ImmutableDictionary<string, double> ComputeAttentionChanges(
        ImmutableDictionary<string, double> before,
        ImmutableDictionary<string, double> after)
    {
        var allKeys = before.Keys.Union(after.Keys).ToHashSet();
        var builder = ImmutableDictionary.CreateBuilder<string, double>();

        foreach (var key in allKeys)
        {
            var beforeVal = before.GetValueOrDefault(key, 0.0);
            var afterVal = after.GetValueOrDefault(key, 0.0);
            var delta = afterVal - beforeVal;
            if (Math.Abs(delta) > 0.01)
            {
                builder[key] = delta;
            }
        }

        return builder.ToImmutable();
    }

    private static string InterpretChanges(
        double loadDelta,
        double valenceDelta,
        ImmutableDictionary<string, double> attentionChanges,
        ProcessingMode beforeMode,
        ProcessingMode afterMode)
    {
        var interpretations = new List<string>();

        // Cognitive load interpretation
        if (loadDelta > 0.3)
        {
            interpretations.Add("Significant cognitive load increase detected, indicating complex processing demands.");
        }
        else if (loadDelta < -0.3)
        {
            interpretations.Add("Substantial cognitive load reduction, suggesting task completion or simplification.");
        }
        else if (Math.Abs(loadDelta) > 0.1)
        {
            interpretations.Add(loadDelta > 0
                ? "Moderate cognitive load increase observed."
                : "Moderate cognitive load decrease observed.");
        }

        // Valence interpretation
        if (valenceDelta > 0.3)
        {
            interpretations.Add("Positive shift in emotional valence, indicating favorable processing outcome.");
        }
        else if (valenceDelta < -0.3)
        {
            interpretations.Add("Negative shift in emotional valence, suggesting processing difficulties or challenges.");
        }

        // Mode change interpretation
        if (beforeMode != afterMode)
        {
            interpretations.Add($"Processing mode shifted from {beforeMode} to {afterMode}.");
        }

        // Attention changes interpretation
        if (attentionChanges.Count > 3)
        {
            interpretations.Add("Multiple attention redistributions detected, indicating broad context switching.");
        }
        else if (attentionChanges.Count > 0)
        {
            var majorShift = attentionChanges.MaxBy(kvp => Math.Abs(kvp.Value));
            if (Math.Abs(majorShift.Value) > 0.2)
            {
                interpretations.Add($"Major attention shift toward '{majorShift.Key}' detected.");
            }
        }

        return interpretations.Count > 0
            ? string.Join(" ", interpretations)
            : "No significant state changes detected between snapshots.";
    }
}

/// <summary>
/// Interface for introspection operations on cognitive state.
/// </summary>
public interface IIntrospector
{
    /// <summary>
    /// Captures the current internal state as an immutable snapshot.
    /// </summary>
    /// <returns>Result containing the captured state or an error message.</returns>
    Result<InternalState, string> CaptureState();

    /// <summary>
    /// Analyzes an internal state and generates an introspection report.
    /// </summary>
    /// <param name="state">The state to analyze.</param>
    /// <returns>Result containing the report or an error message.</returns>
    Result<IntrospectionReport, string> Analyze(InternalState state);

    /// <summary>
    /// Compares two states and generates a comparison report.
    /// </summary>
    /// <param name="before">The earlier state.</param>
    /// <param name="after">The later state.</param>
    /// <returns>Result containing the comparison or an error message.</returns>
    Result<StateComparison, string> CompareStates(InternalState before, InternalState after);

    /// <summary>
    /// Identifies patterns across a history of states.
    /// </summary>
    /// <param name="history">The state history to analyze.</param>
    /// <returns>Result containing pattern observations or an error message.</returns>
    Result<ImmutableList<string>, string> IdentifyPatterns(IEnumerable<InternalState> history);

    /// <summary>
    /// Retrieves the history of captured states.
    /// </summary>
    /// <returns>Result containing the state history or an error message.</returns>
    Result<ImmutableList<InternalState>, string> GetStateHistory();

    /// <summary>
    /// Sets the current focus area.
    /// </summary>
    /// <param name="focus">The focus to set.</param>
    /// <returns>Result indicating success or an error message.</returns>
    Result<Unit, string> SetCurrentFocus(string focus);

    /// <summary>
    /// Adds a goal to the active goals list.
    /// </summary>
    /// <param name="goal">The goal to add.</param>
    /// <returns>Result indicating success or an error message.</returns>
    Result<Unit, string> AddGoal(string goal);

    /// <summary>
    /// Removes a goal from the active goals list.
    /// </summary>
    /// <param name="goal">The goal to remove.</param>
    /// <returns>Result indicating success or an error message.</returns>
    Result<Unit, string> RemoveGoal(string goal);

    /// <summary>
    /// Updates the cognitive load value.
    /// </summary>
    /// <param name="load">The cognitive load (0 to 1).</param>
    /// <returns>Result indicating success or an error message.</returns>
    Result<Unit, string> SetCognitiveLoad(double load);

    /// <summary>
    /// Updates the emotional valence.
    /// </summary>
    /// <param name="valence">The valence value (-1 to 1).</param>
    /// <returns>Result indicating success or an error message.</returns>
    Result<Unit, string> SetValence(double valence);

    /// <summary>
    /// Sets the processing mode.
    /// </summary>
    /// <param name="mode">The processing mode to set.</param>
    /// <returns>Result indicating success or an error message.</returns>
    Result<Unit, string> SetMode(ProcessingMode mode);
}

/// <summary>
/// Thread-safe implementation of IIntrospector with rolling history and pattern detection.
/// </summary>
public sealed class CognitiveIntrospector : IIntrospector
{
    private readonly object _lock = new();
    private readonly int _maxHistorySize;
    private readonly List<InternalState> _history;
    private InternalState _currentState;

    private const double HighCognitiveLoadThreshold = 0.8;
    private const double LowCognitiveLoadThreshold = 0.2;
    private const double AttentionDriftThreshold = 0.3;
    private const double ValenceExtremeThreshold = 0.7;

    /// <summary>
    /// Initializes a new instance of the <see cref="CognitiveIntrospector"/> class.
    /// </summary>
    /// <param name="maxHistorySize">Maximum number of states to retain in history.</param>
    public CognitiveIntrospector(int maxHistorySize = 100)
    {
        _maxHistorySize = maxHistorySize > 0 ? maxHistorySize : 100;
        _history = new List<InternalState>();
        _currentState = InternalState.Initial();
    }

    /// <inheritdoc/>
    public Result<InternalState, string> CaptureState()
    {
        lock (_lock)
        {
            try
            {
                var snapshot = _currentState.Snapshot();
                _history.Add(snapshot);

                // Maintain rolling window
                while (_history.Count > _maxHistorySize)
                {
                    _history.RemoveAt(0);
                }

                return Result<InternalState, string>.Success(snapshot);
            }
            catch (Exception ex)
            {
                return Result<InternalState, string>.Failure($"Failed to capture state: {ex.Message}");
            }
        }
    }

    /// <inheritdoc/>
    public Result<IntrospectionReport, string> Analyze(InternalState state)
    {
        if (state is null)
        {
            return Result<IntrospectionReport, string>.Failure("Cannot analyze null state.");
        }

        try
        {
            var report = IntrospectionReport.Empty(state);

            // Analyze cognitive load
            report = AnalyzeCognitiveLoad(report, state);

            // Analyze emotional valence
            report = AnalyzeValence(report, state);

            // Analyze attention distribution
            report = AnalyzeAttention(report, state);

            // Analyze working memory
            report = AnalyzeWorkingMemory(report, state);

            // Analyze goals
            report = AnalyzeGoals(report, state);

            // Analyze processing mode
            report = AnalyzeMode(report, state);

            // Calculate self-assessment score
            var score = CalculateSelfAssessmentScore(state, report);
            report = report with { SelfAssessmentScore = score, GeneratedAt = DateTime.UtcNow };

            return Result<IntrospectionReport, string>.Success(report);
        }
        catch (Exception ex)
        {
            return Result<IntrospectionReport, string>.Failure($"Analysis failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public Result<StateComparison, string> CompareStates(InternalState before, InternalState after)
    {
        if (before is null || after is null)
        {
            return Result<StateComparison, string>.Failure("Cannot compare null states.");
        }

        try
        {
            var comparison = StateComparison.Create(before, after);
            return Result<StateComparison, string>.Success(comparison);
        }
        catch (Exception ex)
        {
            return Result<StateComparison, string>.Failure($"Comparison failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public Result<ImmutableList<string>, string> IdentifyPatterns(IEnumerable<InternalState> history)
    {
        if (history is null)
        {
            return Result<ImmutableList<string>, string>.Failure("Cannot identify patterns in null history.");
        }

        try
        {
            var states = history.ToList();
            if (states.Count < 2)
            {
                return Result<ImmutableList<string>, string>.Success(
                    ImmutableList.Create("Insufficient history for pattern detection."));
            }

            var patterns = new List<string>();

            // Pattern: Cognitive load trends
            var loadTrend = DetectTrend(states.Select(s => s.CognitiveLoad).ToList());
            if (loadTrend != TrendDirection.Stable)
            {
                patterns.Add($"Cognitive load shows {loadTrend.ToString().ToLowerInvariant()} trend over time.");
            }

            // Pattern: Valence oscillation
            var valenceOscillation = DetectOscillation(states.Select(s => s.EmotionalValence).ToList());
            if (valenceOscillation)
            {
                patterns.Add("Emotional valence shows oscillating pattern, indicating processing instability.");
            }

            // Pattern: Mode switching frequency
            var modeChanges = CountModeChanges(states);
            if (modeChanges > states.Count / 3)
            {
                patterns.Add($"Frequent processing mode changes detected ({modeChanges} transitions), suggesting task-switching behavior.");
            }

            // Pattern: Goal completion rate
            var goalPattern = AnalyzeGoalPatterns(states);
            if (!string.IsNullOrEmpty(goalPattern))
            {
                patterns.Add(goalPattern);
            }

            // Pattern: Focus stability
            var focusChanges = CountFocusChanges(states);
            if (focusChanges > states.Count / 2)
            {
                patterns.Add("Attention focus is unstable, changing frequently between states.");
            }

            // Pattern: Working memory saturation
            var avgMemoryItems = states.Average(s => s.WorkingMemoryItems.Count);
            if (avgMemoryItems > 7)
            {
                patterns.Add($"Working memory consistently near capacity (avg: {avgMemoryItems:F1} items).");
            }

            // Pattern: Chronic high load
            var highLoadStates = states.Count(s => s.CognitiveLoad > HighCognitiveLoadThreshold);
            if (highLoadStates > states.Count * 0.6)
            {
                patterns.Add("Chronic high cognitive load detected across majority of states.");
            }

            if (patterns.Count == 0)
            {
                patterns.Add("No significant patterns detected in state history.");
            }

            return Result<ImmutableList<string>, string>.Success(patterns.ToImmutableList());
        }
        catch (Exception ex)
        {
            return Result<ImmutableList<string>, string>.Failure($"Pattern identification failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public Result<ImmutableList<InternalState>, string> GetStateHistory()
    {
        lock (_lock)
        {
            return Result<ImmutableList<InternalState>, string>.Success(_history.ToImmutableList());
        }
    }

    /// <inheritdoc/>
    public Result<Unit, string> SetCurrentFocus(string focus)
    {
        if (string.IsNullOrWhiteSpace(focus))
        {
            return Result<Unit, string>.Failure("Focus cannot be null or empty.");
        }

        lock (_lock)
        {
            _currentState = _currentState.WithFocus(focus);
            return Result<Unit, string>.Success(Unit.Value);
        }
    }

    /// <inheritdoc/>
    public Result<Unit, string> AddGoal(string goal)
    {
        if (string.IsNullOrWhiteSpace(goal))
        {
            return Result<Unit, string>.Failure("Goal cannot be null or empty.");
        }

        lock (_lock)
        {
            if (_currentState.ActiveGoals.Contains(goal))
            {
                return Result<Unit, string>.Failure($"Goal '{goal}' already exists.");
            }

            _currentState = _currentState.WithGoal(goal);
            return Result<Unit, string>.Success(Unit.Value);
        }
    }

    /// <inheritdoc/>
    public Result<Unit, string> RemoveGoal(string goal)
    {
        if (string.IsNullOrWhiteSpace(goal))
        {
            return Result<Unit, string>.Failure("Goal cannot be null or empty.");
        }

        lock (_lock)
        {
            if (!_currentState.ActiveGoals.Contains(goal))
            {
                return Result<Unit, string>.Failure($"Goal '{goal}' not found.");
            }

            _currentState = _currentState.WithoutGoal(goal);
            return Result<Unit, string>.Success(Unit.Value);
        }
    }

    /// <inheritdoc/>
    public Result<Unit, string> SetCognitiveLoad(double load)
    {
        lock (_lock)
        {
            _currentState = _currentState.WithCognitiveLoad(load);
            return Result<Unit, string>.Success(Unit.Value);
        }
    }

    /// <inheritdoc/>
    public Result<Unit, string> SetValence(double valence)
    {
        lock (_lock)
        {
            _currentState = _currentState.WithValence(valence);
            return Result<Unit, string>.Success(Unit.Value);
        }
    }

    /// <inheritdoc/>
    public Result<Unit, string> SetMode(ProcessingMode mode)
    {
        lock (_lock)
        {
            _currentState = _currentState.WithMode(mode);
            return Result<Unit, string>.Success(Unit.Value);
        }
    }

    private static IntrospectionReport AnalyzeCognitiveLoad(IntrospectionReport report, InternalState state)
    {
        report = report.WithObservation($"Cognitive load: {state.CognitiveLoad:P0}");

        if (state.CognitiveLoad > HighCognitiveLoadThreshold)
        {
            report = report
                .WithAnomaly($"High cognitive load detected ({state.CognitiveLoad:P0}).")
                .WithRecommendation("Consider reducing task complexity or delegating subtasks.");
        }
        else if (state.CognitiveLoad < LowCognitiveLoadThreshold)
        {
            report = report.WithObservation("Low cognitive load indicates available processing capacity.");
        }

        return report;
    }

    private static IntrospectionReport AnalyzeValence(IntrospectionReport report, InternalState state)
    {
        var valenceDesc = state.EmotionalValence switch
        {
            > 0.5 => "positive",
            < -0.5 => "negative",
            _ => "neutral"
        };

        report = report.WithObservation($"Emotional valence: {valenceDesc} ({state.EmotionalValence:+0.00;-0.00})");

        if (Math.Abs(state.EmotionalValence) > ValenceExtremeThreshold)
        {
            report = report
                .WithAnomaly($"Extreme emotional valence detected ({state.EmotionalValence:+0.00;-0.00}).")
                .WithRecommendation("Monitor for potential affect-cognition interference.");
        }

        return report;
    }

    private static IntrospectionReport AnalyzeAttention(IntrospectionReport report, InternalState state)
    {
        if (state.AttentionDistribution.IsEmpty)
        {
            return report.WithObservation("No attention distribution data available.");
        }

        var totalAttention = state.AttentionDistribution.Values.Sum();
        report = report.WithObservation($"Attention distributed across {state.AttentionDistribution.Count} areas.");

        // Check for attention fragmentation
        if (state.AttentionDistribution.Count > 5)
        {
            report = report
                .WithAnomaly("Attention fragmentation detected across multiple areas.")
                .WithRecommendation("Consider consolidating focus to fewer priority areas.");
        }

        // Check for attention drift (no dominant focus)
        var maxAttention = state.AttentionDistribution.Values.DefaultIfEmpty(0).Max();
        if (maxAttention < AttentionDriftThreshold && state.AttentionDistribution.Count > 1)
        {
            report = report
                .WithAnomaly("Attention drift detected - no dominant focus area.")
                .WithRecommendation("Establish clearer priority focus to improve processing efficiency.");
        }

        return report;
    }

    private static IntrospectionReport AnalyzeWorkingMemory(IntrospectionReport report, InternalState state)
    {
        var itemCount = state.WorkingMemoryItems.Count;
        report = report.WithObservation($"Working memory contains {itemCount} items.");

        if (itemCount > 7)
        {
            report = report
                .WithAnomaly($"Working memory approaching capacity ({itemCount} items).")
                .WithRecommendation("Consider chunking information or offloading to external memory.");
        }
        else if (itemCount == 0)
        {
            report = report.WithObservation("Working memory is empty, ready for new information.");
        }

        return report;
    }

    private static IntrospectionReport AnalyzeGoals(IntrospectionReport report, InternalState state)
    {
        var goalCount = state.ActiveGoals.Count;
        report = report.WithObservation($"Tracking {goalCount} active goals.");

        if (goalCount > 5)
        {
            report = report
                .WithAnomaly($"Multiple concurrent goals ({goalCount}) may cause priority conflicts.")
                .WithRecommendation("Consider prioritizing or sequencing goals to reduce cognitive overhead.");
        }
        else if (goalCount == 0)
        {
            report = report
                .WithAnomaly("No active goals detected.")
                .WithRecommendation("Establish goals to provide direction for processing.");
        }

        return report;
    }

    private static IntrospectionReport AnalyzeMode(IntrospectionReport report, InternalState state)
    {
        var modeDesc = state.Mode switch
        {
            ProcessingMode.Analytical => "systematic logical analysis",
            ProcessingMode.Creative => "generative exploration",
            ProcessingMode.Reactive => "stimulus-driven response",
            ProcessingMode.Reflective => "meta-cognitive reflection",
            ProcessingMode.Intuitive => "pattern-based recognition",
            _ => "unknown processing"
        };

        report = report.WithObservation($"Current processing mode: {state.Mode} ({modeDesc}).");

        // Mode-specific recommendations
        if (state.Mode == ProcessingMode.Reactive && state.CognitiveLoad > 0.5)
        {
            report = report.WithRecommendation(
                "High load in reactive mode - consider shifting to analytical mode for deliberate processing.");
        }

        return report;
    }

    private static double CalculateSelfAssessmentScore(InternalState state, IntrospectionReport report)
    {
        var score = 0.5; // Baseline

        // Cognitive load impact (optimal around 0.4-0.6)
        var loadDeviation = Math.Abs(state.CognitiveLoad - 0.5);
        score -= loadDeviation * 0.2;

        // Anomaly penalty
        score -= report.Anomalies.Count * 0.1;

        // Goal presence bonus
        if (state.ActiveGoals.Count > 0 && state.ActiveGoals.Count <= 3)
        {
            score += 0.1;
        }

        // Focus clarity bonus
        if (state.CurrentFocus != "None")
        {
            score += 0.05;
        }

        // Mode appropriateness (reflective mode in introspection is good)
        if (state.Mode == ProcessingMode.Reflective)
        {
            score += 0.1;
        }

        return Math.Clamp(score, 0.0, 1.0);
    }

    private static TrendDirection DetectTrend(List<double> values)
    {
        if (values.Count < 3)
        {
            return TrendDirection.Stable;
        }

        var increases = 0;
        var decreases = 0;

        for (var i = 1; i < values.Count; i++)
        {
            var delta = values[i] - values[i - 1];
            if (delta > 0.05)
            {
                increases++;
            }
            else if (delta < -0.05)
            {
                decreases++;
            }
        }

        var threshold = values.Count / 3;
        if (increases > threshold && increases > decreases * 2)
        {
            return TrendDirection.Increasing;
        }

        if (decreases > threshold && decreases > increases * 2)
        {
            return TrendDirection.Decreasing;
        }

        return TrendDirection.Stable;
    }

    private static bool DetectOscillation(List<double> values)
    {
        if (values.Count < 4)
        {
            return false;
        }

        var signChanges = 0;
        for (var i = 2; i < values.Count; i++)
        {
            var prevDelta = values[i - 1] - values[i - 2];
            var currDelta = values[i] - values[i - 1];
            if (Math.Sign(prevDelta) != Math.Sign(currDelta) && Math.Abs(prevDelta) > 0.1 && Math.Abs(currDelta) > 0.1)
            {
                signChanges++;
            }
        }

        return signChanges >= values.Count / 3;
    }

    private static int CountModeChanges(List<InternalState> states)
    {
        var changes = 0;
        for (var i = 1; i < states.Count; i++)
        {
            if (states[i].Mode != states[i - 1].Mode)
            {
                changes++;
            }
        }

        return changes;
    }

    private static int CountFocusChanges(List<InternalState> states)
    {
        var changes = 0;
        for (var i = 1; i < states.Count; i++)
        {
            if (states[i].CurrentFocus != states[i - 1].CurrentFocus)
            {
                changes++;
            }
        }

        return changes;
    }

    private static string AnalyzeGoalPatterns(List<InternalState> states)
    {
        if (states.Count < 2)
        {
            return string.Empty;
        }

        var firstGoals = states[0].ActiveGoals.ToHashSet();
        var lastGoals = states[^1].ActiveGoals.ToHashSet();

        var completed = firstGoals.Except(lastGoals).Count();
        var added = lastGoals.Except(firstGoals).Count();

        if (completed > 0 && added > 0)
        {
            return $"Goal dynamics: {completed} goals completed, {added} new goals added during observation period.";
        }

        if (completed > 0)
        {
            return $"Goal completion detected: {completed} goals achieved during observation period.";
        }

        if (added > 0)
        {
            return $"Goal expansion: {added} new goals emerged during observation period.";
        }

        return string.Empty;
    }

    private enum TrendDirection
    {
        Stable,
        Increasing,
        Decreasing
    }
}

/// <summary>
/// Provides Kleisli arrow combinators for introspection operations.
/// </summary>
public static class IntrospectionArrow
{
    /// <summary>
    /// Creates an arrow that captures the current cognitive state.
    /// </summary>
    /// <param name="introspector">The introspector to use.</param>
    /// <returns>A step that captures state.</returns>
    public static Step<Unit, Result<InternalState, string>> CaptureStateArrow(IIntrospector introspector) =>
        _ => Task.FromResult(introspector.CaptureState());

    /// <summary>
    /// Creates an arrow that analyzes an internal state.
    /// </summary>
    /// <param name="introspector">The introspector to use.</param>
    /// <returns>A step that analyzes state.</returns>
    public static Step<InternalState, Result<IntrospectionReport, string>> AnalyzeArrow(IIntrospector introspector) =>
        state => Task.FromResult(introspector.Analyze(state));

    /// <summary>
    /// Creates an arrow that compares two states.
    /// </summary>
    /// <param name="introspector">The introspector to use.</param>
    /// <returns>A step that compares states.</returns>
    public static Step<(InternalState Before, InternalState After), Result<StateComparison, string>> CompareArrow(IIntrospector introspector) =>
        pair => Task.FromResult(introspector.CompareStates(pair.Before, pair.After));

    /// <summary>
    /// Creates an arrow that identifies patterns in state history.
    /// </summary>
    /// <param name="introspector">The introspector to use.</param>
    /// <returns>A step that identifies patterns.</returns>
    public static Step<IEnumerable<InternalState>, Result<ImmutableList<string>, string>> IdentifyPatternsArrow(IIntrospector introspector) =>
        history => Task.FromResult(introspector.IdentifyPatterns(history));

    /// <summary>
    /// Creates a composed arrow that captures state and then analyzes it.
    /// </summary>
    /// <param name="introspector">The introspector to use.</param>
    /// <returns>A step that captures and analyzes state.</returns>
    public static Step<Unit, Result<IntrospectionReport, string>> FullIntrospectionArrow(IIntrospector introspector) =>
        async _ =>
        {
            var captureResult = introspector.CaptureState();
            if (!captureResult.IsSuccess)
            {
                return Result<IntrospectionReport, string>.Failure(captureResult.Error);
            }

            return introspector.Analyze(captureResult.Value);
        };

    /// <summary>
    /// Creates an arrow that sets focus and then captures state.
    /// </summary>
    /// <param name="introspector">The introspector to use.</param>
    /// <returns>A step that sets focus and captures state.</returns>
    public static Step<string, Result<InternalState, string>> FocusAndCaptureArrow(IIntrospector introspector) =>
        focus =>
        {
            var focusResult = introspector.SetCurrentFocus(focus);
            if (!focusResult.IsSuccess)
            {
                return Task.FromResult(Result<InternalState, string>.Failure(focusResult.Error));
            }

            return Task.FromResult(introspector.CaptureState());
        };

    /// <summary>
    /// Creates an arrow that performs introspection with pattern analysis.
    /// </summary>
    /// <param name="introspector">The introspector to use.</param>
    /// <returns>A step that captures state, analyzes it, and identifies patterns.</returns>
    public static Step<Unit, Result<(IntrospectionReport Report, ImmutableList<string> Patterns), string>> DeepIntrospectionArrow(IIntrospector introspector) =>
        async _ =>
        {
            // Capture current state
            var captureResult = introspector.CaptureState();
            if (!captureResult.IsSuccess)
            {
                return Result<(IntrospectionReport, ImmutableList<string>), string>.Failure(captureResult.Error);
            }

            // Analyze the captured state
            var analyzeResult = introspector.Analyze(captureResult.Value);
            if (!analyzeResult.IsSuccess)
            {
                return Result<(IntrospectionReport, ImmutableList<string>), string>.Failure(analyzeResult.Error);
            }

            // Get history and identify patterns
            var historyResult = introspector.GetStateHistory();
            if (!historyResult.IsSuccess)
            {
                return Result<(IntrospectionReport, ImmutableList<string>), string>.Failure(historyResult.Error);
            }

            var patternsResult = introspector.IdentifyPatterns(historyResult.Value);
            if (!patternsResult.IsSuccess)
            {
                return Result<(IntrospectionReport, ImmutableList<string>), string>.Failure(patternsResult.Error);
            }

            return Result<(IntrospectionReport, ImmutableList<string>), string>.Success((analyzeResult.Value, patternsResult.Value));
        };

    /// <summary>
    /// Creates an arrow that monitors state transitions over time.
    /// </summary>
    /// <param name="introspector">The introspector to use.</param>
    /// <returns>A step that compares current state with previous state if available.</returns>
    public static Step<Unit, Result<Option<StateComparison>, string>> MonitorTransitionArrow(IIntrospector introspector) =>
        _ =>
        {
            var historyResult = introspector.GetStateHistory();
            if (!historyResult.IsSuccess)
            {
                return Task.FromResult(Result<Option<StateComparison>, string>.Failure(historyResult.Error));
            }

            var history = historyResult.Value;
            if (history.Count < 2)
            {
                return Task.FromResult(Result<Option<StateComparison>, string>.Success(Option<StateComparison>.None()));
            }

            var before = history[^2];
            var after = history[^1];
            var comparisonResult = introspector.CompareStates(before, after);

            return Task.FromResult(comparisonResult.IsSuccess
                ? Result<Option<StateComparison>, string>.Success(Option<StateComparison>.Some(comparisonResult.Value))
                : Result<Option<StateComparison>, string>.Failure(comparisonResult.Error));
        };
}
