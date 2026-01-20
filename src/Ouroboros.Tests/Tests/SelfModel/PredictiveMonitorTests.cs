using Ouroboros.Agent.MetaAI;
using Ouroboros.Agent.MetaAI.SelfModel;
using Xunit;

namespace Ouroboros.Tests.Tests.SelfModel;

/// <summary>
/// Tests for PredictiveMonitor implementation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PredictiveMonitorTests
{
    [Fact]
    public void CreateForecast_Should_CreatePendingForecast()
    {
        // Arrange
        var monitor = new PredictiveMonitor();
        DateTime targetTime = DateTime.UtcNow.AddHours(1);

        // Act
        Forecast forecast = monitor.CreateForecast(
            "CPU utilization forecast",
            "cpu_utilization",
            75.5,
            0.8,
            targetTime);

        // Assert
        Assert.NotNull(forecast);
        Assert.Equal("cpu_utilization", forecast.MetricName);
        Assert.Equal(75.5, forecast.PredictedValue);
        Assert.Equal(0.8, forecast.Confidence);
        Assert.Equal(ForecastStatus.Pending, forecast.Status);
        Assert.Null(forecast.ActualValue);
    }

    [Fact]
    public void UpdateForecastOutcome_Should_VerifyAccurateForecast()
    {
        // Arrange
        var monitor = new PredictiveMonitor();
        Forecast forecast = monitor.CreateForecast(
            "Test forecast",
            "test_metric",
            100.0,
            0.9,
            DateTime.UtcNow.AddHours(1));

        // Act
        monitor.UpdateForecastOutcome(forecast.Id, 105.0); // Within 10% threshold

        // Assert
        List<Forecast> forecasts = monitor.GetForecastsByMetric("test_metric");
        Forecast? updated = forecasts.FirstOrDefault(f => f.Id == forecast.Id);
        Assert.NotNull(updated);
        Assert.Equal(ForecastStatus.Verified, updated.Status);
        Assert.Equal(105.0, updated.ActualValue);
    }

    [Fact]
    public void UpdateForecastOutcome_Should_FailInaccurateForecast()
    {
        // Arrange
        var monitor = new PredictiveMonitor();
        Forecast forecast = monitor.CreateForecast(
            "Test forecast",
            "test_metric",
            100.0,
            0.9,
            DateTime.UtcNow.AddHours(1));

        // Act
        monitor.UpdateForecastOutcome(forecast.Id, 150.0); // Beyond 10% threshold

        // Assert
        List<Forecast> forecasts = monitor.GetForecastsByMetric("test_metric");
        Forecast? updated = forecasts.FirstOrDefault(f => f.Id == forecast.Id);
        Assert.NotNull(updated);
        Assert.Equal(ForecastStatus.Failed, updated.Status);
    }

    [Fact]
    public void GetCalibration_Should_CalculateMetrics()
    {
        // Arrange
        var monitor = new PredictiveMonitor();
        
        // Create and verify some forecasts
        Forecast f1 = monitor.CreateForecast("F1", "metric1", 100.0, 0.8, DateTime.UtcNow);
        monitor.UpdateForecastOutcome(f1.Id, 102.0); // Verified

        Forecast f2 = monitor.CreateForecast("F2", "metric1", 200.0, 0.9, DateTime.UtcNow);
        monitor.UpdateForecastOutcome(f2.Id, 250.0); // Failed

        // Act
        ForecastCalibration calibration = monitor.GetCalibration(TimeSpan.FromDays(1));

        // Assert
        Assert.Equal(2, calibration.TotalForecasts);
        Assert.Equal(1, calibration.VerifiedForecasts);
        Assert.Equal(1, calibration.FailedForecasts);
        Assert.True(calibration.AverageConfidence > 0);
    }

    [Fact]
    public async Task DetectAnomalyAsync_Should_IdentifyOutlier()
    {
        // Arrange
        var monitor = new PredictiveMonitor();
        
        // Build up history with normal values
        for (int i = 0; i < 10; i++)
        {
            Forecast f = monitor.CreateForecast($"F{i}", "response_time", 100.0 + i, 0.8, DateTime.UtcNow);
            monitor.UpdateForecastOutcome(f.Id, 100.0 + i);
        }

        // Act
        AnomalyDetection anomaly = await monitor.DetectAnomalyAsync("response_time", 200.0);

        // Assert
        Assert.True(anomaly.IsAnomaly);
        Assert.Equal("response_time", anomaly.MetricName);
        Assert.Equal(200.0, anomaly.ObservedValue);
        Assert.True(anomaly.Deviation > 0);
    }

    [Fact]
    public async Task DetectAnomalyAsync_Should_NotFlagNormalValue()
    {
        // Arrange
        var monitor = new PredictiveMonitor();
        
        // Build up history
        for (int i = 0; i < 10; i++)
        {
            Forecast f = monitor.CreateForecast($"F{i}", "response_time", 100.0 + i, 0.8, DateTime.UtcNow);
            monitor.UpdateForecastOutcome(f.Id, 100.0 + i);
        }

        // Act
        AnomalyDetection anomaly = await monitor.DetectAnomalyAsync("response_time", 105.0);

        // Assert
        Assert.False(anomaly.IsAnomaly);
        Assert.Equal("Normal", anomaly.Severity);
    }

    [Fact]
    public async Task ForecastMetricAsync_Should_CreateForecast()
    {
        // Arrange
        var monitor = new PredictiveMonitor();
        
        // Build up history
        for (int i = 0; i < 10; i++)
        {
            Forecast f = monitor.CreateForecast($"F{i}", "cpu_usage", 50.0 + (i * 2), 0.8, DateTime.UtcNow);
            monitor.UpdateForecastOutcome(f.Id, 50.0 + (i * 2));
        }

        // Act
        Result<Forecast, string> result = await monitor.ForecastMetricAsync("cpu_usage", TimeSpan.FromHours(1));

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("cpu_usage", result.Value.MetricName);
        Assert.True(result.Value.Confidence > 0);
    }

    [Fact]
    public async Task GetRecentAnomalies_Should_ReturnLatestAnomalies()
    {
        // Arrange
        var monitor = new PredictiveMonitor();
        
        // Create history with at least 10 data points for reliable anomaly detection
        for (int i = 0; i < 10; i++)
        {
            Forecast f = monitor.CreateForecast($"F{i}", "metric1", 100.0, 0.8, DateTime.UtcNow);
            monitor.UpdateForecastOutcome(f.Id, 100.0 + (i * 0.5)); // Slight variance
        }
        
        // Trigger anomaly with a value far outside normal range
        AnomalyDetection anomaly = await monitor.DetectAnomalyAsync("metric1", 500.0);
        
        // Assert anomaly was detected
        Assert.True(anomaly.IsAnomaly, "Anomaly should be detected for value 500 when mean is ~100");

        // Act
        List<AnomalyDetection> anomalies = monitor.GetRecentAnomalies(10);

        // Assert
        Assert.NotEmpty(anomalies);
        Assert.All(anomalies, a => Assert.True(a.IsAnomaly));
    }

    [Fact]
    public void ValidatePendingForecasts_Should_CancelExpiredForecasts()
    {
        // Arrange
        var monitor = new PredictiveMonitor();
        DateTime pastTime = DateTime.UtcNow.AddHours(-1);
        monitor.CreateForecast("Expired forecast", "test_metric", 100.0, 0.8, pastTime);

        // Act
        int validated = monitor.ValidatePendingForecasts();

        // Assert
        Assert.Equal(1, validated);
        List<Forecast> pending = monitor.GetPendingForecasts();
        Assert.Empty(pending);
    }
}
