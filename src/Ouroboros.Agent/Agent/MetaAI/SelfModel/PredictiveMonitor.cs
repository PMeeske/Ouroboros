#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Predictive Monitor Implementation
// Phase 2: Predictive self-monitoring with forecasts vs outcomes
// ==========================================================

using System.Collections.Concurrent;

namespace LangChainPipeline.Agent.MetaAI.SelfModel;

/// <summary>
/// Implementation of predictive monitoring for agent self-calibration.
/// </summary>
public sealed class PredictiveMonitor : IPredictiveMonitor
{
    private readonly ConcurrentDictionary<Guid, Forecast> _forecasts = new();
    private readonly ConcurrentBag<AnomalyDetection> _anomalies = new();
    private readonly Dictionary<string, List<double>> _metricHistory = new();
    private readonly IChatCompletionModel? _llm;
    private readonly object _lock = new();

    public PredictiveMonitor(IChatCompletionModel? llm = null)
    {
        _llm = llm;
    }

    public Forecast CreateForecast(
        string description,
        string metricName,
        double predictedValue,
        double confidence,
        DateTime targetTime)
    {
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(metricName);

        var forecast = new Forecast(
            Guid.NewGuid(),
            description,
            metricName,
            predictedValue,
            Math.Clamp(confidence, 0.0, 1.0),
            DateTime.UtcNow,
            targetTime,
            ForecastStatus.Pending,
            null,
            new Dictionary<string, object>());

        _forecasts[forecast.Id] = forecast;
        return forecast;
    }

    public void UpdateForecastOutcome(Guid forecastId, double actualValue)
    {
        if (_forecasts.TryGetValue(forecastId, out Forecast? existing))
        {
            double error = Math.Abs(existing.PredictedValue - actualValue);
            double relativeError = existing.PredictedValue != 0
                ? error / Math.Abs(existing.PredictedValue)
                : error;

            // Forecast is verified if within 10% of predicted value
            bool verified = relativeError < 0.1;

            Forecast updated = existing with
            {
                Status = verified ? ForecastStatus.Verified : ForecastStatus.Failed,
                ActualValue = actualValue
            };

            _forecasts[forecastId] = updated;

            // Record in metric history
            lock (_lock)
            {
                if (!_metricHistory.ContainsKey(existing.MetricName))
                {
                    _metricHistory[existing.MetricName] = new List<double>();
                }
                _metricHistory[existing.MetricName].Add(actualValue);
            }
        }
    }

    public List<Forecast> GetPendingForecasts()
    {
        return _forecasts.Values
            .Where(f => f.Status == ForecastStatus.Pending)
            .OrderBy(f => f.TargetTime)
            .ToList();
    }

    public List<Forecast> GetForecastsByMetric(string metricName, bool includeCompleted = true)
    {
        IEnumerable<Forecast> query = _forecasts.Values
            .Where(f => f.MetricName.Equals(metricName, StringComparison.OrdinalIgnoreCase));

        if (!includeCompleted)
        {
            query = query.Where(f => f.Status == ForecastStatus.Pending);
        }

        return query.OrderByDescending(f => f.PredictionTime).ToList();
    }

    public ForecastCalibration GetCalibration(TimeSpan timeWindow)
    {
        DateTime cutoff = DateTime.UtcNow - timeWindow;
        List<Forecast> recentForecasts = _forecasts.Values
            .Where(f => f.PredictionTime >= cutoff &&
                       f.Status != ForecastStatus.Pending &&
                       f.ActualValue.HasValue)
            .ToList();

        if (!recentForecasts.Any())
        {
            return new ForecastCalibration(
                0, 0, 0, 0.0, 0.0, 0.0, 0.0,
                new Dictionary<string, double>());
        }

        int totalForecasts = recentForecasts.Count;
        int verifiedForecasts = recentForecasts.Count(f => f.Status == ForecastStatus.Verified);
        int failedForecasts = recentForecasts.Count(f => f.Status == ForecastStatus.Failed);
        double averageConfidence = recentForecasts.Average(f => f.Confidence);

        // Calculate average accuracy (inverse of relative error)
        double averageAccuracy = recentForecasts.Average(f =>
        {
            double error = Math.Abs(f.PredictedValue - f.ActualValue!.Value);
            double relativeError = f.PredictedValue != 0
                ? error / Math.Abs(f.PredictedValue)
                : error;
            return Math.Max(0.0, 1.0 - relativeError);
        });

        // Calculate Brier score (lower is better, 0 is perfect)
        double brierScore = recentForecasts.Average(f =>
        {
            double predicted = f.Confidence;
            double actual = f.Status == ForecastStatus.Verified ? 1.0 : 0.0;
            return Math.Pow(predicted - actual, 2);
        });

        // Calibration error (difference between confidence and accuracy)
        double calibrationError = Math.Abs(averageConfidence - averageAccuracy);

        // Calculate per-metric accuracies
        Dictionary<string, double> metricAccuracies = recentForecasts
            .GroupBy(f => f.MetricName)
            .ToDictionary(
                g => g.Key,
                g => g.Average(f =>
                {
                    double error = Math.Abs(f.PredictedValue - f.ActualValue!.Value);
                    double relativeError = f.PredictedValue != 0
                        ? error / Math.Abs(f.PredictedValue)
                        : error;
                    return Math.Max(0.0, 1.0 - relativeError);
                }));

        return new ForecastCalibration(
            totalForecasts,
            verifiedForecasts,
            failedForecasts,
            averageConfidence,
            averageAccuracy,
            brierScore,
            calibrationError,
            metricAccuracies);
    }

    public async Task<AnomalyDetection> DetectAnomalyAsync(
        string metricName,
        double observedValue,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(metricName);

        // Get historical data for the metric
        List<double> history;
        lock (_lock)
        {
            if (!_metricHistory.ContainsKey(metricName))
            {
                _metricHistory[metricName] = new List<double>();
            }
            history = _metricHistory[metricName].TakeLast(100).ToList();
        }

        if (history.Count < 5)
        {
            // Not enough data for anomaly detection
            return new AnomalyDetection(
                metricName,
                observedValue,
                observedValue,
                0.0,
                false,
                "Normal",
                DateTime.UtcNow,
                new List<string>());
        }

        // Calculate statistical properties
        double mean = history.Average();
        double variance = history.Average(v => Math.Pow(v - mean, 2));
        double stdDev = Math.Sqrt(variance);
        double deviation = Math.Abs(observedValue - mean);
        double zScore = stdDev > 0 ? deviation / stdDev : 0.0;

        // Anomaly if beyond 2 standard deviations
        bool isAnomaly = zScore > 2.0;
        string severity = zScore switch
        {
            > 3.0 => "Critical",
            > 2.5 => "High",
            > 2.0 => "Medium",
            _ => "Normal"
        };

        List<string> possibleCauses = new List<string>();
        if (isAnomaly && _llm != null)
        {
            // Use LLM to suggest possible causes
            string prompt = $@"The metric '{metricName}' has an anomalous value.

Observed: {observedValue:F2}
Expected: {mean:F2}
Deviation: {deviation:F2} ({zScore:F2} standard deviations)

Suggest 2-3 possible causes for this anomaly. Be specific and concise.
Format each cause on a new line starting with '- '";

            try
            {
                string response = await _llm.GenerateTextAsync(prompt, ct);
                possibleCauses = response.Split('\n')
                    .Select(l => l.Trim())
                    .Where(l => l.StartsWith("- "))
                    .Select(l => l.Substring(2).Trim())
                    .ToList();
            }
            catch
            {
                // Fallback to generic causes
                possibleCauses.Add("Statistical variation");
                possibleCauses.Add("Change in system behavior");
            }
        }
        else if (isAnomaly)
        {
            possibleCauses.Add("Statistical outlier detected");
        }

        var anomaly = new AnomalyDetection(
            metricName,
            observedValue,
            mean,
            deviation,
            isAnomaly,
            severity,
            DateTime.UtcNow,
            possibleCauses);

        if (isAnomaly)
        {
            _anomalies.Add(anomaly);
        }

        return anomaly;
    }

    public List<AnomalyDetection> GetRecentAnomalies(int count = 10)
    {
        return _anomalies
            .OrderByDescending(a => a.DetectedAt)
            .Take(count)
            .ToList();
    }

    public async Task<Result<Forecast, string>> ForecastMetricAsync(
        string metricName,
        TimeSpan horizon,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(metricName);

        // Get historical data
        List<double> history;
        lock (_lock)
        {
            if (!_metricHistory.ContainsKey(metricName))
            {
                return Result<Forecast, string>.Failure($"No historical data for metric '{metricName}'");
            }
            history = _metricHistory[metricName].TakeLast(50).ToList();
        }

        if (history.Count < 3)
        {
            return Result<Forecast, string>.Failure($"Insufficient data for metric '{metricName}'");
        }

        // Simple linear trend forecast
        double mean = history.Average();
        double trend = history.Count > 1
            ? (history[^1] - history[0]) / (history.Count - 1)
            : 0.0;

        int periodsAhead = (int)(horizon.TotalHours / 1.0); // Assume hourly granularity
        double predictedValue = mean + (trend * periodsAhead);

        // Calculate confidence based on variance
        double variance = history.Average(v => Math.Pow(v - mean, 2));
        double confidence = variance > 0 ? Math.Max(0.3, 1.0 - (Math.Sqrt(variance) / mean)) : 0.7;

        Forecast forecast = CreateForecast(
            $"Forecast for {metricName} at {horizon.TotalHours:F1} hours ahead",
            metricName,
            predictedValue,
            confidence,
            DateTime.UtcNow + horizon);

        await Task.CompletedTask;
        return Result<Forecast, string>.Success(forecast);
    }

    public int ValidatePendingForecasts()
    {
        DateTime now = DateTime.UtcNow;
        int validated = 0;

        List<Forecast> dueForecasts = _forecasts.Values
            .Where(f => f.Status == ForecastStatus.Pending && f.TargetTime <= now)
            .ToList();

        foreach (Forecast forecast in dueForecasts)
        {
            // Auto-mark as cancelled if no actual value provided
            Forecast updated = forecast with { Status = ForecastStatus.Cancelled };
            _forecasts[forecast.Id] = updated;
            validated++;
        }

        return validated;
    }
}
