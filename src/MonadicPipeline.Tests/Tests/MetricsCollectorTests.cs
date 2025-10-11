using LangChainPipeline.Diagnostics;
using Xunit;

namespace LangChainPipeline.Tests;

public class MetricsCollectorTests
{
    [Fact]
    public void IncrementCounter_SingleIncrement_ShouldTrackValue()
    {
        // Arrange
        var collector = new MetricsCollector();

        // Act
        collector.IncrementCounter("test_counter", 5.0);
        var metrics = collector.GetMetrics();

        // Assert
        var metric = metrics.FirstOrDefault(m => m.Name == "test_counter");
        Assert.NotNull(metric);
        Assert.Equal(5.0, metric.Value);
        Assert.Equal(MetricType.Counter, metric.Type);
    }

    [Fact]
    public void IncrementCounter_MultipleIncrements_ShouldAccumulate()
    {
        // Arrange
        var collector = new MetricsCollector();

        // Act
        collector.IncrementCounter("test_counter", 5.0);
        collector.IncrementCounter("test_counter", 3.0);
        collector.IncrementCounter("test_counter", 2.0);
        var metrics = collector.GetMetrics();

        // Assert
        var metric = metrics.FirstOrDefault(m => m.Name == "test_counter");
        Assert.NotNull(metric);
        Assert.Equal(10.0, metric.Value);
    }

    [Fact]
    public void IncrementCounter_WithLabels_ShouldTrackSeparately()
    {
        // Arrange
        var collector = new MetricsCollector();
        var labels1 = new Dictionary<string, string> { ["status"] = "success" };
        var labels2 = new Dictionary<string, string> { ["status"] = "failure" };

        // Act
        collector.IncrementCounter("test_counter", 5.0, labels1);
        collector.IncrementCounter("test_counter", 3.0, labels2);
        var metrics = collector.GetMetrics();

        // Assert
        var successMetric = metrics.FirstOrDefault(m =>
            m.Name == "test_counter" && m.Labels.ContainsKey("status") && m.Labels["status"] == "success");
        var failureMetric = metrics.FirstOrDefault(m =>
            m.Name == "test_counter" && m.Labels.ContainsKey("status") && m.Labels["status"] == "failure");

        Assert.NotNull(successMetric);
        Assert.NotNull(failureMetric);
        Assert.Equal(5.0, successMetric.Value);
        Assert.Equal(3.0, failureMetric.Value);
    }

    [Fact]
    public void SetGauge_ShouldUpdateValue()
    {
        // Arrange
        var collector = new MetricsCollector();

        // Act
        collector.SetGauge("test_gauge", 100.0);
        collector.SetGauge("test_gauge", 150.0);
        var metrics = collector.GetMetrics();

        // Assert
        var metric = metrics.FirstOrDefault(m => m.Name == "test_gauge");
        Assert.NotNull(metric);
        Assert.Equal(150.0, metric.Value);
        Assert.Equal(MetricType.Gauge, metric.Type);
    }

    [Fact]
    public void ObserveHistogram_ShouldCalculateStatistics()
    {
        // Arrange
        var collector = new MetricsCollector();

        // Act
        collector.ObserveHistogram("test_histogram", 10.0);
        collector.ObserveHistogram("test_histogram", 20.0);
        collector.ObserveHistogram("test_histogram", 30.0);
        var metrics = collector.GetMetrics();

        // Assert
        var countMetric = metrics.FirstOrDefault(m => m.Name == "test_histogram_count");
        var sumMetric = metrics.FirstOrDefault(m => m.Name == "test_histogram_sum");
        var avgMetric = metrics.FirstOrDefault(m => m.Name == "test_histogram_avg");

        Assert.NotNull(countMetric);
        Assert.NotNull(sumMetric);
        Assert.NotNull(avgMetric);
        Assert.Equal(3.0, countMetric.Value);
        Assert.Equal(60.0, sumMetric.Value);
        Assert.Equal(20.0, avgMetric.Value);
    }

    [Fact]
    public void ObserveSummary_ShouldCalculateStatistics()
    {
        // Arrange
        var collector = new MetricsCollector();

        // Act
        collector.ObserveSummary("test_summary", 10.0);
        collector.ObserveSummary("test_summary", 20.0);
        collector.ObserveSummary("test_summary", 30.0);
        var metrics = collector.GetMetrics();

        // Assert
        var countMetric = metrics.FirstOrDefault(m => m.Name == "test_summary_count");
        var sumMetric = metrics.FirstOrDefault(m => m.Name == "test_summary_sum");
        var avgMetric = metrics.FirstOrDefault(m => m.Name == "test_summary_avg");

        Assert.NotNull(countMetric);
        Assert.NotNull(sumMetric);
        Assert.NotNull(avgMetric);
        Assert.Equal(3.0, countMetric.Value);
        Assert.Equal(60.0, sumMetric.Value);
        Assert.Equal(20.0, avgMetric.Value);
    }

    [Fact]
    public void MeasureDuration_ShouldRecordElapsedTime()
    {
        // Arrange
        var collector = new MetricsCollector();

        // Act
        using (collector.MeasureDuration("test_duration"))
        {
            System.Threading.Thread.Sleep(10);
        }
        var metrics = collector.GetMetrics();

        // Assert
        var countMetric = metrics.FirstOrDefault(m => m.Name == "test_duration_count");
        var avgMetric = metrics.FirstOrDefault(m => m.Name == "test_duration_avg");

        Assert.NotNull(countMetric);
        Assert.NotNull(avgMetric);
        Assert.Equal(1.0, countMetric.Value);
        Assert.True(avgMetric.Value >= 10.0); // Should be at least 10ms
    }

    [Fact]
    public void ExportPrometheusFormat_ShouldProduceValidOutput()
    {
        // Arrange
        var collector = new MetricsCollector();
        collector.IncrementCounter("test_counter", 5.0);
        collector.SetGauge("test_gauge", 100.0);

        // Act
        var prometheus = collector.ExportPrometheusFormat();

        // Assert
        Assert.Contains("# HELP test", prometheus);
        Assert.Contains("# TYPE test", prometheus);
        Assert.Contains("test_counter", prometheus);
        Assert.Contains("test_gauge", prometheus);
    }

    [Fact]
    public void Reset_ShouldClearAllMetrics()
    {
        // Arrange
        var collector = new MetricsCollector();
        collector.IncrementCounter("test_counter", 5.0);
        collector.SetGauge("test_gauge", 100.0);

        // Act
        collector.Reset();
        var metrics = collector.GetMetrics();

        // Assert
        Assert.Empty(metrics);
    }

    [Fact]
    public void RecordToolExecution_ShouldTrackMetrics()
    {
        // Arrange
        var collector = new MetricsCollector();

        // Act
        collector.RecordToolExecution("test_tool", 150.0, true);
        collector.RecordToolExecution("test_tool", 200.0, false);
        var metrics = collector.GetMetrics();

        // Assert
        var totalMetrics = metrics.Where(m => m.Name == "tool_executions_total").ToList();
        Assert.Equal(2, totalMetrics.Count);

        var successMetric = totalMetrics.FirstOrDefault(m =>
            m.Labels.ContainsKey("status") && m.Labels["status"] == "success");
        var failureMetric = totalMetrics.FirstOrDefault(m =>
            m.Labels.ContainsKey("status") && m.Labels["status"] == "failure");

        Assert.NotNull(successMetric);
        Assert.NotNull(failureMetric);
        Assert.Equal(1.0, successMetric.Value);
        Assert.Equal(1.0, failureMetric.Value);
    }

    [Fact]
    public void RecordPipelineExecution_ShouldTrackMetrics()
    {
        // Arrange
        var collector = new MetricsCollector();

        // Act
        collector.RecordPipelineExecution("test_pipeline", 500.0, true);
        var metrics = collector.GetMetrics();

        // Assert
        var counterMetric = metrics.FirstOrDefault(m => m.Name == "pipeline_executions_total");
        Assert.NotNull(counterMetric);
        Assert.Equal(1.0, counterMetric.Value);
        Assert.Equal("test_pipeline", counterMetric.Labels["pipeline"]);
    }

    [Fact]
    public void RecordLlmRequest_ShouldTrackTokensAndDuration()
    {
        // Arrange
        var collector = new MetricsCollector();

        // Act
        collector.RecordLlmRequest("gpt-4", 500, 1000.0);
        var metrics = collector.GetMetrics();

        // Assert
        var requestMetric = metrics.FirstOrDefault(m => m.Name == "llm_requests_total");
        var tokenMetric = metrics.FirstOrDefault(m => m.Name == "llm_tokens_total");

        Assert.NotNull(requestMetric);
        Assert.NotNull(tokenMetric);
        Assert.Equal(1.0, requestMetric.Value);
        Assert.Equal(500.0, tokenMetric.Value);
    }

    [Fact]
    public void RecordVectorOperation_ShouldTrackOperations()
    {
        // Arrange
        var collector = new MetricsCollector();

        // Act
        collector.RecordVectorOperation("search", 10, 50.0);
        var metrics = collector.GetMetrics();

        // Assert
        var operationMetric = metrics.FirstOrDefault(m => m.Name == "vector_operations_total");
        var vectorMetric = metrics.FirstOrDefault(m => m.Name == "vectors_processed_total");

        Assert.NotNull(operationMetric);
        Assert.NotNull(vectorMetric);
        Assert.Equal(1.0, operationMetric.Value);
        Assert.Equal(10.0, vectorMetric.Value);
    }

    [Fact]
    public void Singleton_Instance_ShouldReturnSameInstance()
    {
        // Act
        var instance1 = MetricsCollector.Instance;
        var instance2 = MetricsCollector.Instance;

        // Assert
        Assert.Same(instance1, instance2);
    }
}
