#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Predictive Monitor Interface
// Phase 2: Predictive self-monitoring with forecasts vs outcomes
// ==========================================================

namespace LangChainPipeline.Agent.MetaAI.SelfModel;

/// <summary>
/// Represents a forecast made by the agent.
/// </summary>
public sealed record Forecast(
    Guid Id,
    string Description,
    string MetricName,
    double PredictedValue,
    double Confidence,
    DateTime PredictionTime,
    DateTime TargetTime,
    ForecastStatus Status,
    double? ActualValue,
    Dictionary<string, object> Metadata);

/// <summary>
/// Status of a forecast.
/// </summary>
public enum ForecastStatus
{
    /// <summary>Forecast is pending - target time not reached</summary>
    Pending,
    
    /// <summary>Forecast verified - outcome matches prediction</summary>
    Verified,
    
    /// <summary>Forecast failed - outcome differs from prediction</summary>
    Failed,
    
    /// <summary>Forecast cancelled - no longer relevant</summary>
    Cancelled
}

/// <summary>
/// Calibration metrics for forecast accuracy.
/// </summary>
public sealed record ForecastCalibration(
    int TotalForecasts,
    int VerifiedForecasts,
    int FailedForecasts,
    double AverageConfidence,
    double AverageAccuracy,
    double BrierScore,
    double CalibrationError,
    Dictionary<string, double> MetricAccuracies);

/// <summary>
/// Anomaly detection result.
/// </summary>
public sealed record AnomalyDetection(
    string MetricName,
    double ObservedValue,
    double ExpectedValue,
    double Deviation,
    bool IsAnomaly,
    string Severity,
    DateTime DetectedAt,
    List<string> PossibleCauses);

/// <summary>
/// Interface for predictive monitoring and forecast tracking.
/// Compares predictions with outcomes for self-calibration.
/// </summary>
public interface IPredictiveMonitor
{
    /// <summary>
    /// Creates a new forecast.
    /// </summary>
    /// <param name="description">Forecast description</param>
    /// <param name="metricName">Name of the metric being forecast</param>
    /// <param name="predictedValue">Predicted value</param>
    /// <param name="confidence">Confidence level (0.0-1.0)</param>
    /// <param name="targetTime">When the forecast should be validated</param>
    /// <returns>The created forecast</returns>
    Forecast CreateForecast(
        string description,
        string metricName,
        double predictedValue,
        double confidence,
        DateTime targetTime);

    /// <summary>
    /// Updates a forecast with the actual outcome.
    /// </summary>
    /// <param name="forecastId">Forecast ID</param>
    /// <param name="actualValue">Actual observed value</param>
    void UpdateForecastOutcome(Guid forecastId, double actualValue);

    /// <summary>
    /// Gets all pending forecasts.
    /// </summary>
    /// <returns>List of pending forecasts</returns>
    List<Forecast> GetPendingForecasts();

    /// <summary>
    /// Gets forecasts for a specific metric.
    /// </summary>
    /// <param name="metricName">Metric name</param>
    /// <param name="includeCompleted">Include completed forecasts</param>
    /// <returns>List of forecasts for the metric</returns>
    List<Forecast> GetForecastsByMetric(string metricName, bool includeCompleted = true);

    /// <summary>
    /// Gets forecast calibration metrics.
    /// </summary>
    /// <param name="timeWindow">Time window for calibration calculation</param>
    /// <returns>Calibration metrics</returns>
    ForecastCalibration GetCalibration(TimeSpan timeWindow);

    /// <summary>
    /// Detects anomalies in observed metrics.
    /// </summary>
    /// <param name="metricName">Metric to check</param>
    /// <param name="observedValue">Observed value</param>
    /// <returns>Anomaly detection result</returns>
    Task<AnomalyDetection> DetectAnomalyAsync(
        string metricName,
        double observedValue,
        CancellationToken ct = default);

    /// <summary>
    /// Gets recent anomalies.
    /// </summary>
    /// <param name="count">Number of anomalies to retrieve</param>
    /// <returns>List of recent anomalies</returns>
    List<AnomalyDetection> GetRecentAnomalies(int count = 10);

    /// <summary>
    /// Forecasts future performance based on trends.
    /// </summary>
    /// <param name="metricName">Metric to forecast</param>
    /// <param name="horizon">Time horizon for forecast</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Forecast for the metric</returns>
    Task<Result<Forecast, string>> ForecastMetricAsync(
        string metricName,
        TimeSpan horizon,
        CancellationToken ct = default);

    /// <summary>
    /// Validates all pending forecasts that have reached their target time.
    /// </summary>
    /// <returns>Number of forecasts validated</returns>
    int ValidatePendingForecasts();
}
