// <copyright file="CliStepBase.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Cli;

/// <summary>
/// Base class providing common utilities for CLI pipeline steps.
/// Offers helper methods for parsing, tracing, and step creation.
/// </summary>
public static class CliStepBase
{
    /// <summary>
    /// Parses a string argument from the args, removing quotes if present.
    /// </summary>
    /// <param name="args">The raw argument string.</param>
    /// <returns>The parsed string, or empty if null/whitespace.</returns>
    public static string ParseString(string? args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            return string.Empty;
        }

        string trimmed = args.Trim();

        // Remove surrounding quotes
        if ((trimmed.StartsWith('"') && trimmed.EndsWith('"')) ||
            (trimmed.StartsWith('\'') && trimmed.EndsWith('\'')))
        {
            return trimmed[1..^1];
        }

        return trimmed;
    }

    /// <summary>
    /// Parses an integer argument from the args.
    /// </summary>
    /// <param name="args">The raw argument string.</param>
    /// <param name="defaultValue">Default value if parsing fails.</param>
    /// <returns>The parsed integer or default value.</returns>
    public static int ParseInt(string? args, int defaultValue = 0)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            return defaultValue;
        }

        return int.TryParse(args.Trim(), out int result) ? result : defaultValue;
    }

    /// <summary>
    /// Parses a boolean argument from the args.
    /// </summary>
    /// <param name="args">The raw argument string.</param>
    /// <param name="defaultValue">Default value if parsing fails.</param>
    /// <returns>The parsed boolean or default value.</returns>
    public static bool ParseBool(string? args, bool defaultValue = false)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            return defaultValue;
        }

        string trimmed = args.Trim().ToLowerInvariant();
        return trimmed is "true" or "yes" or "1" or "on";
    }

    /// <summary>
    /// Creates a no-op step that records an event but doesn't change state.
    /// </summary>
    /// <param name="eventName">The event name to record.</param>
    /// <returns>A no-op step.</returns>
    public static Step<CliPipelineState, CliPipelineState> NoOp(string eventName)
        => s =>
        {
            s.Branch = s.Branch.WithIngestEvent($"noop:{eventName}", Array.Empty<string>());
            return Task.FromResult(s);
        };

    /// <summary>
    /// Writes a trace message if tracing is enabled.
    /// </summary>
    /// <param name="state">The current pipeline state.</param>
    /// <param name="message">The message to trace.</param>
    public static void Trace(CliPipelineState state, string message)
    {
        if (state.Trace)
        {
            Console.WriteLine($"[trace] {message}");
        }
    }

    /// <summary>
    /// Creates a step that wraps the action with trace logging.
    /// </summary>
    /// <param name="name">The step name for tracing.</param>
    /// <param name="action">The action to execute.</param>
    /// <returns>A traced step.</returns>
    public static Step<CliPipelineState, CliPipelineState> TracedStep(
        string name,
        Func<CliPipelineState, Task<CliPipelineState>> action)
        => async s =>
        {
            Trace(s, $"Starting {name}");
            var result = await action(s);
            Trace(result, $"Completed {name}");
            return result;
        };

    /// <summary>
    /// Creates a step that catches exceptions and records them as events.
    /// </summary>
    /// <param name="name">The step name for error recording.</param>
    /// <param name="action">The action to execute.</param>
    /// <returns>A safe step that won't throw.</returns>
    public static Step<CliPipelineState, CliPipelineState> SafeStep(
        string name,
        Func<CliPipelineState, Task<CliPipelineState>> action)
        => async s =>
        {
            try
            {
                return await action(s);
            }
            catch (Exception ex)
            {
                s.Branch = s.Branch.WithIngestEvent($"error:{name}:{ex.GetType().Name}", Array.Empty<string>());
                Trace(s, $"Error in {name}: {ex.Message}");
                return s;
            }
        };

    /// <summary>
    /// Normalizes topic and query from pipeline state.
    /// </summary>
    /// <param name="state">The pipeline state.</param>
    /// <returns>Tuple of (topic, query).</returns>
    public static (string Topic, string Query) NormalizeTopicQuery(CliPipelineState state)
    {
        string topic = string.IsNullOrWhiteSpace(state.Topic)
            ? (string.IsNullOrWhiteSpace(state.Prompt) ? "topic" : state.Prompt)
            : state.Topic;

        string query = string.IsNullOrWhiteSpace(state.Query)
            ? (string.IsNullOrWhiteSpace(state.Prompt) ? topic : state.Prompt)
            : state.Query;

        return (topic, query);
    }
}
