// <copyright file="IOrchestrator.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

// ==========================================================
// Base Orchestrator Interface
// Unified interface for all orchestrators in the suite
// ==========================================================

namespace Ouroboros.Agent;

/// <summary>
/// Represents the execution context for an orchestration operation.
/// Provides a unified way to pass context information across all orchestrators.
/// </summary>
public sealed record OrchestratorContext(
    string OperationId,
    Dictionary<string, object> Metadata,
    CancellationToken CancellationToken = default)
{
    /// <summary>
    /// Creates a new orchestrator context with generated operation ID.
    /// </summary>
    public static OrchestratorContext Create(
        Dictionary<string, object>? metadata = null,
        CancellationToken ct = default) =>
        new OrchestratorContext(
            Guid.NewGuid().ToString(),
            metadata ?? new Dictionary<string, object>(),
            ct);

    /// <summary>
    /// Gets a value from metadata or returns default.
    /// </summary>
    public T? GetMetadata<T>(string key, T? defaultValue = default) =>
        Metadata.TryGetValue(key, out var value) && value is T typed
            ? typed
            : defaultValue;

    /// <summary>
    /// Creates a new context with additional metadata.
    /// </summary>
    public OrchestratorContext WithMetadata(string key, object value)
    {
        var newMetadata = new Dictionary<string, object>(Metadata) { [key] = value };
        return this with { Metadata = newMetadata };
    }
}

/// <summary>
/// Configuration options for orchestrator behavior.
/// Provides a unified configuration pattern across all orchestrators.
/// </summary>
public record OrchestratorConfig
{
    /// <summary>
    /// Gets or initializes whether to enable distributed tracing.
    /// </summary>
    public bool EnableTracing { get; init; } = true;

    /// <summary>
    /// Gets or initializes whether to track performance metrics.
    /// </summary>
    public bool EnableMetrics { get; init; } = true;

    /// <summary>
    /// Gets or initializes whether to enable safety checks.
    /// </summary>
    public bool EnableSafetyChecks { get; init; } = true;

    /// <summary>
    /// Gets or initializes the maximum execution timeout.
    /// </summary>
    public TimeSpan? ExecutionTimeout { get; init; }

    /// <summary>
    /// Gets or initializes the retry configuration.
    /// </summary>
    public RetryConfig? RetryConfig { get; init; }

    /// <summary>
    /// Gets or initializes additional custom settings.
    /// </summary>
    public Dictionary<string, object> CustomSettings { get; init; } = new();

    /// <summary>
    /// Creates a default configuration.
    /// </summary>
    public static OrchestratorConfig Default() => new OrchestratorConfig();

    /// <summary>
    /// Gets a custom setting value or returns default.
    /// </summary>
    public T? GetSetting<T>(string key, T? defaultValue = default) =>
        CustomSettings.TryGetValue(key, out var value) && value is T typed
            ? typed
            : defaultValue;
}

/// <summary>
/// Retry configuration for orchestrator operations.
/// </summary>
public sealed record RetryConfig(
    int MaxRetries = 3,
    TimeSpan InitialDelay = default,
    TimeSpan MaxDelay = default,
    double BackoffMultiplier = 2.0)
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RetryConfig"/> class with defaults.
    /// </summary>
    public RetryConfig()
        : this(3, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(10), 2.0)
    {
    }

    /// <summary>
    /// Creates a default retry configuration.
    /// </summary>
    public static RetryConfig Default() => new RetryConfig();
}

/// <summary>
/// Unified metrics for orchestrator performance tracking.
/// </summary>
public sealed record OrchestratorMetrics(
    string OrchestratorName,
    int TotalExecutions,
    int SuccessfulExecutions,
    int FailedExecutions,
    double AverageLatencyMs,
    double SuccessRate,
    DateTime LastExecuted,
    Dictionary<string, double> CustomMetrics)
{
    /// <summary>
    /// Calculates success rate.
    /// </summary>
    public double CalculatedSuccessRate =>
        TotalExecutions > 0 ? (double)SuccessfulExecutions / TotalExecutions : 0.0;

    /// <summary>
    /// Gets a custom metric or returns default.
    /// </summary>
    public double GetCustomMetric(string key, double defaultValue = 0.0) =>
        CustomMetrics.TryGetValue(key, out var value) ? value : defaultValue;

    /// <summary>
    /// Creates initial metrics for a new orchestrator.
    /// </summary>
    public static OrchestratorMetrics Initial(string orchestratorName) =>
        new OrchestratorMetrics(
            orchestratorName,
            TotalExecutions: 0,
            SuccessfulExecutions: 0,
            FailedExecutions: 0,
            AverageLatencyMs: 0.0,
            SuccessRate: 0.0,
            LastExecuted: DateTime.UtcNow,
            CustomMetrics: new Dictionary<string, double>());

    /// <summary>
    /// Records a new execution result.
    /// </summary>
    public OrchestratorMetrics RecordExecution(double latencyMs, bool success)
    {
        int newTotal = TotalExecutions + 1;
        int newSuccess = SuccessfulExecutions + (success ? 1 : 0);
        int newFailed = FailedExecutions + (success ? 0 : 1);
        double newAvgLatency = ((AverageLatencyMs * TotalExecutions) + latencyMs) / newTotal;
        double newSuccessRate = (double)newSuccess / newTotal;

        return this with
        {
            TotalExecutions = newTotal,
            SuccessfulExecutions = newSuccess,
            FailedExecutions = newFailed,
            AverageLatencyMs = newAvgLatency,
            SuccessRate = newSuccessRate,
            LastExecuted = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Adds or updates a custom metric.
    /// </summary>
    public OrchestratorMetrics WithCustomMetric(string key, double value)
    {
        var newCustomMetrics = new Dictionary<string, double>(CustomMetrics)
        {
            [key] = value
        };
        return this with { CustomMetrics = newCustomMetrics };
    }
}

/// <summary>
/// Result of an orchestrator execution with typed output.
/// </summary>
public sealed record OrchestratorResult<T>(
    T? Output,
    bool Success,
    string? ErrorMessage,
    OrchestratorMetrics Metrics,
    TimeSpan ExecutionTime,
    Dictionary<string, object> Metadata)
{
    /// <summary>
    /// Creates a successful result.
    /// </summary>
    public static OrchestratorResult<T> Ok(
        T output,
        OrchestratorMetrics metrics,
        TimeSpan executionTime,
        Dictionary<string, object>? metadata = null) =>
        new OrchestratorResult<T>(
            output,
            Success: true,
            ErrorMessage: null,
            metrics,
            executionTime,
            metadata ?? new Dictionary<string, object>());

    /// <summary>
    /// Creates a failed result.
    /// </summary>
    public static OrchestratorResult<T> Failure(
        string errorMessage,
        OrchestratorMetrics metrics,
        TimeSpan executionTime,
        Dictionary<string, object>? metadata = null) =>
        new OrchestratorResult<T>(
            Output: default,
            Success: false,
            errorMessage,
            metrics,
            executionTime,
            metadata ?? new Dictionary<string, object>());

    /// <summary>
    /// Converts to Result monad.
    /// </summary>
    public Result<T, string> ToResult() =>
        Success && Output != null
            ? Result<T, string>.Success(Output)
            : Result<T, string>.Failure(ErrorMessage ?? "Operation failed");

    /// <summary>
    /// Gets a metadata value or returns default.
    /// </summary>
    public TValue? GetMetadata<TValue>(string key, TValue? defaultValue = default) =>
        Metadata.TryGetValue(key, out var value) && value is TValue typed
            ? typed
            : defaultValue;
}

/// <summary>
/// Base interface for all orchestrators in the Ouroboros suite.
/// Provides a unified contract for orchestration operations with consistent
/// configuration, metrics tracking, and error handling.
/// </summary>
/// <typeparam name="TInput">The input type for orchestration.</typeparam>
/// <typeparam name="TOutput">The output type from orchestration.</typeparam>
public interface IOrchestrator<TInput, TOutput>
{
    /// <summary>
    /// Gets the orchestrator's configuration.
    /// </summary>
    OrchestratorConfig Configuration { get; }

    /// <summary>
    /// Gets the orchestrator's current metrics.
    /// </summary>
    OrchestratorMetrics Metrics { get; }

    /// <summary>
    /// Executes the orchestration with the given input and context.
    /// </summary>
    /// <param name="input">The input to orchestrate.</param>
    /// <param name="context">Execution context with metadata and cancellation.</param>
    /// <returns>Result containing output, metrics, and execution details.</returns>
    Task<OrchestratorResult<TOutput>> ExecuteAsync(
        TInput input,
        OrchestratorContext? context = null);

    /// <summary>
    /// Validates that the orchestrator is ready to execute.
    /// </summary>
    /// <returns>Result indicating readiness with optional error message.</returns>
    Result<bool, string> ValidateReadiness();

    /// <summary>
    /// Gets detailed health information about the orchestrator.
    /// </summary>
    Task<Dictionary<string, object>> GetHealthAsync(CancellationToken ct = default);
}
