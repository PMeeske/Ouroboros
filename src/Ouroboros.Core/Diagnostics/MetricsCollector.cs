// <copyright file="MetricsCollector.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Diagnostics;

using System.Collections.Concurrent;
using System.Diagnostics;

/// <summary>
/// Metric type enumeration.
/// </summary>
public enum MetricType
{
    /// <summary>
    /// Counter metric that only increases.
    /// </summary>
    Counter,

    /// <summary>
    /// Gauge metric that can increase or decrease.
    /// </summary>
    Gauge,

    /// <summary>
    /// Histogram metric for value distributions.
    /// </summary>
    Histogram,

    /// <summary>
    /// Summary metric for statistical observations.
    /// </summary>
    Summary,
}

/// <summary>
/// Represents a collected metric with metadata.
/// </summary>
public class Metric
{
    /// <summary>
    /// Gets or initializes the metric name.
    /// </summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>
    /// Gets or initializes the metric type.
    /// </summary>
    public MetricType Type { get; init; }

    /// <summary>
    /// Gets or initializes the metric value.
    /// </summary>
    public double Value { get; init; }

    /// <summary>
    /// Gets or initializes the metric labels for dimensional data.
    /// </summary>
    public Dictionary<string, string> Labels { get; init; } = new();

    /// <summary>
    /// Gets or initializes the timestamp when the metric was collected.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Histogram bucket for tracking value distributions.
/// </summary>
public class HistogramBucket
{
    /// <summary>
    /// Gets or initializes the upper bound of the bucket.
    /// </summary>
    public double UpperBound { get; init; }

    /// <summary>
    /// Gets or sets the count of values in this bucket.
    /// </summary>
    public long Count { get; set; }
}

/// <summary>
/// Collects and aggregates performance metrics for monitoring and observability.
/// </summary>
public class MetricsCollector
{
    private readonly ConcurrentDictionary<string, double> counters = new();
    private readonly ConcurrentDictionary<string, double> gauges = new();
    private readonly ConcurrentDictionary<string, ConcurrentBag<double>> histograms = new();
    private readonly ConcurrentDictionary<string, (double Sum, long Count)> summaries = new();
    private readonly object @lock = new();

    /// <summary>
    /// Gets the singleton instance of the metrics collector.
    /// </summary>
    public static MetricsCollector Instance { get; } = new();

    /// <summary>
    /// Increments a counter metric by the specified value.
    /// </summary>
    /// <param name="name">Metric name.</param>
    /// <param name="value">Value to increment by (default: 1).</param>
    /// <param name="labels">Optional labels for the metric.</param>
    public void IncrementCounter(string name, double value = 1.0, Dictionary<string, string>? labels = null)
    {
        string key = this.BuildKey(name, labels);
        this.counters.AddOrUpdate(key, value, (_, current) => current + value);
    }

    /// <summary>
    /// Sets a gauge metric to a specific value.
    /// </summary>
    /// <param name="name">Metric name.</param>
    /// <param name="value">Value to set.</param>
    /// <param name="labels">Optional labels for the metric.</param>
    public void SetGauge(string name, double value, Dictionary<string, string>? labels = null)
    {
        string key = this.BuildKey(name, labels);
        this.gauges[key] = value;
    }

    /// <summary>
    /// Records an observation in a histogram metric.
    /// </summary>
    /// <param name="name">Metric name.</param>
    /// <param name="value">Observed value.</param>
    /// <param name="labels">Optional labels for the metric.</param>
    public void ObserveHistogram(string name, double value, Dictionary<string, string>? labels = null)
    {
        string key = this.BuildKey(name, labels);
        ConcurrentBag<double> bag = this.histograms.GetOrAdd(key, _ => new ConcurrentBag<double>());
        bag.Add(value);
    }

    /// <summary>
    /// Records an observation in a summary metric.
    /// </summary>
    /// <param name="name">Metric name.</param>
    /// <param name="value">Observed value.</param>
    /// <param name="labels">Optional labels for the metric.</param>
    public void ObserveSummary(string name, double value, Dictionary<string, string>? labels = null)
    {
        string key = this.BuildKey(name, labels);
        this.summaries.AddOrUpdate(
            key,
            (value, 1L),
            (_, current) => (current.Sum + value, current.Count + 1));
    }

    /// <summary>
    /// Records the duration of an operation using a stopwatch pattern.
    /// </summary>
    /// <param name="name">Metric name.</param>
    /// <param name="labels">Optional labels for the metric.</param>
    /// <returns>IDisposable that records duration when disposed.</returns>
    public IDisposable MeasureDuration(string name, Dictionary<string, string>? labels = null)
    {
        return new DurationMeasurement(this, name, labels);
    }

    /// <summary>
    /// Gets all collected metrics as a snapshot.
    /// </summary>
    /// <returns></returns>
    public List<Metric> GetMetrics()
    {
        List<Metric> metrics = new List<Metric>();

        // Add counters
        foreach (KeyValuePair<string, double> kvp in this.counters)
        {
            (string name, Dictionary<string, string> labels) = this.ParseKey(kvp.Key);
            metrics.Add(new Metric
            {
                Name = name,
                Type = MetricType.Counter,
                Value = kvp.Value,
                Labels = labels,
            });
        }

        // Add gauges
        foreach (KeyValuePair<string, double> kvp in this.gauges)
        {
            (string name, Dictionary<string, string> labels) = this.ParseKey(kvp.Key);
            metrics.Add(new Metric
            {
                Name = name,
                Type = MetricType.Gauge,
                Value = kvp.Value,
                Labels = labels,
            });
        }

        // Add histograms (as summary statistics)
        foreach (KeyValuePair<string, ConcurrentBag<double>> kvp in this.histograms)
        {
            (string name, Dictionary<string, string> labels) = this.ParseKey(kvp.Key);
            double[] values = kvp.Value.ToArray();
            if (values.Length > 0)
            {
                metrics.Add(new Metric
                {
                    Name = $"{name}_count",
                    Type = MetricType.Histogram,
                    Value = values.Length,
                    Labels = labels,
                });
                metrics.Add(new Metric
                {
                    Name = $"{name}_sum",
                    Type = MetricType.Histogram,
                    Value = values.Sum(),
                    Labels = labels,
                });
                metrics.Add(new Metric
                {
                    Name = $"{name}_avg",
                    Type = MetricType.Histogram,
                    Value = values.Average(),
                    Labels = labels,
                });
            }
        }

        // Add summaries
        foreach (KeyValuePair<string, (double Sum, long Count)> kvp in this.summaries)
        {
            (string name, Dictionary<string, string> labels) = this.ParseKey(kvp.Key);
            (double sum, long count) = kvp.Value;
            metrics.Add(new Metric
            {
                Name = $"{name}_sum",
                Type = MetricType.Summary,
                Value = sum,
                Labels = labels,
            });
            metrics.Add(new Metric
            {
                Name = $"{name}_count",
                Type = MetricType.Summary,
                Value = count,
                Labels = labels,
            });
            if (count > 0)
            {
                metrics.Add(new Metric
                {
                    Name = $"{name}_avg",
                    Type = MetricType.Summary,
                    Value = sum / count,
                    Labels = labels,
                });
            }
        }

        return metrics;
    }

    /// <summary>
    /// Exports metrics in Prometheus text format.
    /// </summary>
    /// <returns></returns>
    public string ExportPrometheusFormat()
    {
        List<Metric> metrics = this.GetMetrics();
        System.Text.StringBuilder output = new System.Text.StringBuilder();

        foreach (IGrouping<string, Metric> group in metrics.GroupBy(m => m.Name.Split('_')[0]))
        {
            string metricName = group.Key;
            MetricType metricType = group.First().Type;

            // Write HELP comment
            output.AppendLine($"# HELP {metricName} {metricName} metric");
            output.AppendLine($"# TYPE {metricName} {metricType.ToString().ToLowerInvariant()}");

            foreach (Metric? metric in group)
            {
                string labels = metric.Labels.Any()
                    ? "{" + string.Join(",", metric.Labels.Select(kvp => $"{kvp.Key}=\"{kvp.Value}\"")) + "}"
                    : string.Empty;
                output.AppendLine($"{metric.Name}{labels} {metric.Value}");
            }

            output.AppendLine();
        }

        return output.ToString();
    }

    /// <summary>
    /// Resets all collected metrics.
    /// </summary>
    public void Reset()
    {
        this.counters.Clear();
        this.gauges.Clear();
        this.histograms.Clear();
        this.summaries.Clear();
    }

    private string BuildKey(string name, Dictionary<string, string>? labels)
    {
        if (labels == null || !labels.Any())
        {
            return name;
        }

        string sortedLabels = string.Join(",", labels.OrderBy(kvp => kvp.Key)
            .Select(kvp => $"{kvp.Key}={kvp.Value}"));
        return $"{name}|{sortedLabels}";
    }

    private (string Name, Dictionary<string, string> Labels) ParseKey(string key)
    {
        string[] parts = key.Split('|');
        if (parts.Length == 1)
        {
            return (parts[0], new Dictionary<string, string>());
        }

        Dictionary<string, string> labels = new Dictionary<string, string>();
        foreach (string labelPair in parts[1].Split(','))
        {
            string[] kv = labelPair.Split('=');
            if (kv.Length == 2)
            {
                labels[kv[0]] = kv[1];
            }
        }

        return (parts[0], labels);
    }

    private class DurationMeasurement : IDisposable
    {
        private readonly MetricsCollector collector;
        private readonly string name;
        private readonly Dictionary<string, string>? labels;
        private readonly Stopwatch stopwatch;

        public DurationMeasurement(MetricsCollector collector, string name, Dictionary<string, string>? labels)
        {
            this.collector = collector;
            this.name = name;
            this.labels = labels;
            this.stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            this.stopwatch.Stop();
            this.collector.ObserveHistogram(this.name, this.stopwatch.Elapsed.TotalMilliseconds, this.labels);
        }
    }
}

/// <summary>
/// Extension methods for metrics collection.
/// </summary>
public static class MetricsExtensions
{
    /// <summary>
    /// Records tool execution metrics.
    /// </summary>
    public static void RecordToolExecution(this MetricsCollector collector, string toolName, double durationMs, bool success)
    {
        Dictionary<string, string> labels = new Dictionary<string, string>
        {
            ["tool_name"] = toolName,
            ["status"] = success ? "success" : "failure",
        };

        collector.IncrementCounter("tool_executions_total", 1, labels);
        collector.ObserveHistogram("tool_execution_duration_ms", durationMs, labels);
    }

    /// <summary>
    /// Records pipeline execution metrics.
    /// </summary>
    public static void RecordPipelineExecution(this MetricsCollector collector, string pipelineName, double durationMs, bool success)
    {
        Dictionary<string, string> labels = new Dictionary<string, string>
        {
            ["pipeline"] = pipelineName,
            ["status"] = success ? "success" : "failure",
        };

        collector.IncrementCounter("pipeline_executions_total", 1, labels);
        collector.ObserveHistogram("pipeline_execution_duration_ms", durationMs, labels);
    }

    /// <summary>
    /// Records LLM request metrics.
    /// </summary>
    public static void RecordLlmRequest(this MetricsCollector collector, string model, int tokenCount, double durationMs)
    {
        Dictionary<string, string> labels = new Dictionary<string, string>
        {
            ["model"] = model,
        };

        collector.IncrementCounter("llm_requests_total", 1, labels);
        collector.IncrementCounter("llm_tokens_total", tokenCount, labels);
        collector.ObserveHistogram("llm_request_duration_ms", durationMs, labels);
    }

    /// <summary>
    /// Records vector store operation metrics.
    /// </summary>
    public static void RecordVectorOperation(this MetricsCollector collector, string operation, int vectorCount, double durationMs)
    {
        Dictionary<string, string> labels = new Dictionary<string, string>
        {
            ["operation"] = operation,
        };

        collector.IncrementCounter("vector_operations_total", 1, labels);
        collector.IncrementCounter("vectors_processed_total", vectorCount, labels);
        collector.ObserveHistogram("vector_operation_duration_ms", durationMs, labels);
    }
}
