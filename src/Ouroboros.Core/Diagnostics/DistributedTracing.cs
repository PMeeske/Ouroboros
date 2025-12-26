// <copyright file="DistributedTracing.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Diagnostics;

using System.Diagnostics;

/// <summary>
/// Distributed tracing implementation using System.Diagnostics.Activity.
/// Provides OpenTelemetry-compatible tracing without external dependencies.
/// </summary>
public static class DistributedTracing
{
    /// <summary>
    /// Activity source for the Ouroboros system.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new("Ouroboros", "1.0.0");

    /// <summary>
    /// Starts a new activity (span) for tracing.
    /// </summary>
    /// <param name="name">Activity name.</param>
    /// <param name="kind">Activity kind (Internal, Client, Server, Producer, Consumer).</param>
    /// <param name="tags">Optional tags to add to the activity.</param>
    /// <returns>Activity instance or null if tracing is disabled.</returns>
    public static Activity? StartActivity(
        string name,
        ActivityKind kind = ActivityKind.Internal,
        Dictionary<string, object?>? tags = null)
    {
        Activity? activity = ActivitySource.StartActivity(name, kind);

        if (activity != null && tags != null)
        {
            foreach ((string key, object? value) in tags)
            {
                activity.SetTag(key, value);
            }
        }

        return activity;
    }

    /// <summary>
    /// Records an event in the current activity.
    /// </summary>
    /// <param name="name">Event name.</param>
    /// <param name="tags">Optional tags for the event.</param>
    public static void RecordEvent(string name, Dictionary<string, object?>? tags = null)
    {
        Activity? activity = Activity.Current;
        if (activity == null)
        {
            return;
        }

        IEnumerable<KeyValuePair<string, object?>> tagsArray = tags?.Select(kvp => new KeyValuePair<string, object?>(kvp.Key, kvp.Value))
            ?? Array.Empty<KeyValuePair<string, object?>>();

        activity.AddEvent(new ActivityEvent(name, tags: new ActivityTagsCollection(tagsArray)));
    }

    /// <summary>
    /// Records an exception in the current activity.
    /// </summary>
    /// <param name="exception">Exception to record.</param>
    public static void RecordException(Exception exception)
    {
        Activity? activity = Activity.Current;
        if (activity == null)
        {
            return;
        }

        activity.SetStatus(ActivityStatusCode.Error, exception.Message);
        activity.SetTag("exception.type", exception.GetType().FullName);
        activity.SetTag("exception.message", exception.Message);
        activity.SetTag("exception.stacktrace", exception.StackTrace);
    }

    /// <summary>
    /// Sets the status of the current activity.
    /// </summary>
    /// <param name="code">Status code.</param>
    /// <param name="description">Optional description.</param>
    public static void SetStatus(ActivityStatusCode code, string? description = null)
    {
        Activity? activity = Activity.Current;
        if (activity == null)
        {
            return;
        }

        activity.SetStatus(code, description);
    }

    /// <summary>
    /// Adds a tag to the current activity.
    /// </summary>
    /// <param name="key">Tag key.</param>
    /// <param name="value">Tag value.</param>
    public static void AddTag(string key, object? value)
    {
        Activity? activity = Activity.Current;
        if (activity == null)
        {
            return;
        }

        activity.SetTag(key, value);
    }

    /// <summary>
    /// Gets the current trace ID for correlation.
    /// </summary>
    /// <returns>Trace ID string or null if no active activity.</returns>
    public static string? GetTraceId()
    {
        return Activity.Current?.TraceId.ToString();
    }

    /// <summary>
    /// Gets the current span ID for correlation.
    /// </summary>
    /// <returns>Span ID string or null if no active activity.</returns>
    public static string? GetSpanId()
    {
        return Activity.Current?.SpanId.ToString();
    }
}

/// <summary>
/// Extension methods for tracing common pipeline operations.
/// </summary>
public static class TracingExtensions
{
    /// <summary>
    /// Traces a tool execution.
    /// </summary>
    /// <param name="toolName">Name of the tool.</param>
    /// <param name="input">Tool input.</param>
    /// <returns>Activity for the tool execution.</returns>
    public static Activity? TraceToolExecution(string toolName, string input)
    {
        Dictionary<string, object?> tags = new Dictionary<string, object?>
        {
            ["tool.name"] = toolName,
            ["tool.input_length"] = input.Length,
        };

        return DistributedTracing.StartActivity($"tool.{toolName}", ActivityKind.Internal, tags);
    }

    /// <summary>
    /// Traces a pipeline execution.
    /// </summary>
    /// <param name="pipelineName">Name of the pipeline.</param>
    /// <returns>Activity for the pipeline execution.</returns>
    public static Activity? TracePipelineExecution(string pipelineName)
    {
        Dictionary<string, object?> tags = new Dictionary<string, object?>
        {
            ["pipeline.name"] = pipelineName,
        };

        return DistributedTracing.StartActivity($"pipeline.{pipelineName}", ActivityKind.Internal, tags);
    }

    /// <summary>
    /// Traces an LLM request.
    /// </summary>
    /// <param name="model">Model name.</param>
    /// <param name="promptLength">Length of the prompt.</param>
    /// <returns>Activity for the LLM request.</returns>
    public static Activity? TraceLlmRequest(string model, int promptLength)
    {
        Dictionary<string, object?> tags = new Dictionary<string, object?>
        {
            ["llm.model"] = model,
            ["llm.prompt_length"] = promptLength,
        };

        return DistributedTracing.StartActivity("llm.request", ActivityKind.Client, tags);
    }

    /// <summary>
    /// Traces a vector store operation.
    /// </summary>
    /// <param name="operation">Operation type (e.g., "search", "insert", "delete").</param>
    /// <param name="vectorCount">Number of vectors involved.</param>
    /// <returns>Activity for the vector operation.</returns>
    public static Activity? TraceVectorOperation(string operation, int vectorCount)
    {
        Dictionary<string, object?> tags = new Dictionary<string, object?>
        {
            ["vector.operation"] = operation,
            ["vector.count"] = vectorCount,
        };

        return DistributedTracing.StartActivity($"vector.{operation}", ActivityKind.Internal, tags);
    }

    /// <summary>
    /// Adds completion tags to an LLM activity.
    /// </summary>
    /// <param name="activity">Activity to update.</param>
    /// <param name="responseLength">Length of the response.</param>
    /// <param name="tokenCount">Number of tokens used.</param>
    public static void CompleteLlmRequest(this Activity? activity, int responseLength, int tokenCount)
    {
        if (activity == null)
        {
            return;
        }

        activity.SetTag("llm.response_length", responseLength);
        activity.SetTag("llm.token_count", tokenCount);
        activity.SetStatus(ActivityStatusCode.Ok);
    }

    /// <summary>
    /// Adds completion tags to a tool activity.
    /// </summary>
    /// <param name="activity">Activity to update.</param>
    /// <param name="success">Whether the tool execution succeeded.</param>
    /// <param name="outputLength">Length of the output.</param>
    public static void CompleteToolExecution(this Activity? activity, bool success, int outputLength)
    {
        if (activity == null)
        {
            return;
        }

        activity.SetTag("tool.success", success);
        activity.SetTag("tool.output_length", outputLength);
        activity.SetStatus(success ? ActivityStatusCode.Ok : ActivityStatusCode.Error);
    }
}

/// <summary>
/// Configuration for ActivityListener to enable tracing.
/// </summary>
public static class TracingConfiguration
{
    private static ActivityListener? listener;

    /// <summary>
    /// Enables tracing with the specified callback for handling activities.
    /// </summary>
    /// <param name="onActivityStarted">Callback when an activity starts.</param>
    /// <param name="onActivityStopped">Callback when an activity stops.</param>
    public static void EnableTracing(
        Action<Activity>? onActivityStarted = null,
        Action<Activity>? onActivityStopped = null)
    {
        if (listener != null)
        {
            return; // Already enabled
        }

        listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == "Ouroboros",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = onActivityStarted,
            ActivityStopped = onActivityStopped,
        };

        ActivitySource.AddActivityListener(listener);
    }

    /// <summary>
    /// Disables tracing.
    /// </summary>
    public static void DisableTracing()
    {
        listener?.Dispose();
        listener = null;
    }

    /// <summary>
    /// Enables console tracing for debugging.
    /// </summary>
    public static void EnableConsoleTracing()
    {
        EnableTracing(
            onActivityStarted: activity =>
            {
                Console.WriteLine($"[TRACE START] {activity.OperationName} - TraceId: {activity.TraceId}, SpanId: {activity.SpanId}");
                foreach (KeyValuePair<string, string?> tag in activity.Tags)
                {
                    Console.WriteLine($"  {tag.Key}: {tag.Value}");
                }
            },
            onActivityStopped: activity =>
            {
                string status = activity.Status == ActivityStatusCode.Ok ? "✓" :
                            activity.Status == ActivityStatusCode.Error ? "✗" : "?";
                Console.WriteLine($"[TRACE END  ] {status} {activity.OperationName} - Duration: {activity.Duration.TotalMilliseconds:F2}ms");
            });
    }
}
