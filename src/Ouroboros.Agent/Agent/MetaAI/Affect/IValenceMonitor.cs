#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Valence Monitor Interface
// Phase 3: Affective Dynamics - Synthetic affective states
// ==========================================================

namespace LangChainPipeline.Agent.MetaAI.Affect;

/// <summary>
/// Represents the current affective state of an agent.
/// </summary>
public sealed record AffectiveState(
    Guid Id,
    double Valence,
    double Stress,
    double Confidence,
    double Curiosity,
    double Arousal,
    DateTime Timestamp,
    Dictionary<string, object> Metadata);

/// <summary>
/// Represents a valence signal measurement.
/// </summary>
public sealed record ValenceSignal(
    string Source,
    double Value,
    SignalType Type,
    DateTime Timestamp,
    TimeSpan? Duration);

/// <summary>
/// Types of valence signals.
/// </summary>
public enum SignalType
{
    /// <summary>Stress indicator from system load or failures</summary>
    Stress,
    
    /// <summary>Confidence from successful task completions</summary>
    Confidence,
    
    /// <summary>Curiosity from novelty detection</summary>
    Curiosity,
    
    /// <summary>General valence (positive/negative affect)</summary>
    Valence,
    
    /// <summary>Arousal level (energy/activation)</summary>
    Arousal
}

/// <summary>
/// Result of stress detection using signal analysis.
/// </summary>
public sealed record StressDetectionResult(
    double StressLevel,
    double Frequency,
    double Amplitude,
    bool IsAnomalous,
    List<double> SpectralPeaks,
    string Analysis,
    DateTime DetectedAt);

/// <summary>
/// Configuration for affect monitoring.
/// </summary>
public sealed record AffectConfig(
    double StressThreshold = 0.7,
    double ConfidenceDecayRate = 0.01,
    double CuriosityBoostFactor = 0.2,
    int SignalHistorySize = 1000,
    int FourierWindowSize = 64);

/// <summary>
/// Interface for monitoring and computing synthetic affective states.
/// Tracks valence, stress, confidence, curiosity, and arousal.
/// </summary>
public interface IValenceMonitor
{
    /// <summary>
    /// Gets the current affective state.
    /// </summary>
    /// <returns>Current affective state</returns>
    AffectiveState GetCurrentState();

    /// <summary>
    /// Records a valence signal from a source.
    /// </summary>
    /// <param name="source">Signal source identifier</param>
    /// <param name="value">Signal value (typically -1.0 to 1.0 for valence, 0.0 to 1.0 for others)</param>
    /// <param name="type">Type of signal</param>
    void RecordSignal(string source, double value, SignalType type);

    /// <summary>
    /// Computes stress level using Fourier-based signal analysis.
    /// Analyzes frequency patterns in recent stress signals to detect anomalies.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Stress detection result with spectral analysis</returns>
    Task<StressDetectionResult> DetectStressAsync(CancellationToken ct = default);

    /// <summary>
    /// Updates confidence based on task outcome.
    /// </summary>
    /// <param name="taskId">Task identifier</param>
    /// <param name="success">Whether the task succeeded</param>
    /// <param name="weight">Weight of this outcome (0.0 to 1.0)</param>
    void UpdateConfidence(string taskId, bool success, double weight = 1.0);

    /// <summary>
    /// Updates curiosity based on novelty detection.
    /// </summary>
    /// <param name="noveltyScore">Novelty score (0.0 to 1.0)</param>
    /// <param name="context">Context description</param>
    void UpdateCuriosity(double noveltyScore, string context);

    /// <summary>
    /// Gets recent signals of a specific type.
    /// </summary>
    /// <param name="type">Signal type</param>
    /// <param name="count">Number of signals to retrieve</param>
    /// <returns>List of recent signals</returns>
    List<ValenceSignal> GetRecentSignals(SignalType type, int count = 100);

    /// <summary>
    /// Gets the signal history for spectral analysis.
    /// </summary>
    /// <param name="type">Signal type</param>
    /// <returns>Array of signal values</returns>
    double[] GetSignalHistory(SignalType type);

    /// <summary>
    /// Computes the running average for a signal type.
    /// </summary>
    /// <param name="type">Signal type</param>
    /// <param name="windowSize">Window size for averaging</param>
    /// <returns>Running average value</returns>
    double GetRunningAverage(SignalType type, int windowSize = 10);

    /// <summary>
    /// Resets all affective states to baseline.
    /// </summary>
    void Reset();

    /// <summary>
    /// Gets affective state history.
    /// </summary>
    /// <param name="count">Number of states to retrieve</param>
    /// <returns>List of recent affective states</returns>
    List<AffectiveState> GetStateHistory(int count = 50);
}
