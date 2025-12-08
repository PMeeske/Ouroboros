// <copyright file="OrchestratorBase.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

// ==========================================================
// Base Orchestrator Implementation
// Abstract base class providing common orchestrator functionality
// ==========================================================

using System.Diagnostics;
using LangChainPipeline.Agent.MetaAI;

namespace LangChainPipeline.Agent;

/// <summary>
/// Abstract base class for orchestrators providing common functionality.
/// Implements unified tracing, metrics, safety checks, and error handling.
/// </summary>
/// <typeparam name="TInput">The input type for orchestration.</typeparam>
/// <typeparam name="TOutput">The output type from orchestration.</typeparam>
public abstract class OrchestratorBase<TInput, TOutput> : IOrchestrator<TInput, TOutput>
{
    private readonly string _orchestratorName;
    private OrchestratorMetrics _metrics;
    private readonly ISafetyGuard? _safetyGuard;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchestratorBase{TInput, TOutput}"/> class.
    /// </summary>
    /// <param name="orchestratorName">Name of the orchestrator for metrics tracking.</param>
    /// <param name="configuration">Configuration for the orchestrator.</param>
    /// <param name="safetyGuard">Optional safety guard for checking operations.</param>
    protected OrchestratorBase(
        string orchestratorName,
        OrchestratorConfig configuration,
        ISafetyGuard? safetyGuard = null)
    {
        ArgumentNullException.ThrowIfNull(orchestratorName);
        ArgumentNullException.ThrowIfNull(configuration);

        _orchestratorName = orchestratorName;
        Configuration = configuration;
        _metrics = OrchestratorMetrics.Initial(orchestratorName);
        _safetyGuard = safetyGuard;
    }

    /// <inheritdoc/>
    public OrchestratorConfig Configuration { get; }

    /// <inheritdoc/>
    public OrchestratorMetrics Metrics => _metrics;

    /// <summary>
    /// Gets the orchestrator name.
    /// </summary>
    protected string OrchestratorName => _orchestratorName;

    /// <summary>
    /// Gets the safety guard if configured.
    /// </summary>
    protected ISafetyGuard? SafetyGuard => _safetyGuard;

    /// <inheritdoc/>
    public async Task<OrchestratorResult<TOutput>> ExecuteAsync(
        TInput input,
        OrchestratorContext? context = null)
    {
        context ??= OrchestratorContext.Create();

        using var activity = Configuration.EnableTracing
            ? ActivitySource.StartActivity($"{_orchestratorName}.execute", ActivityKind.Internal)
            : null;

        var stopwatch = Stopwatch.StartNew();
        var metadata = new Dictionary<string, object>
        {
            ["orchestrator_name"] = _orchestratorName,
            ["operation_id"] = context.OperationId,
            ["started_at"] = DateTime.UtcNow
        };

        try
        {
            // Validate input
            var validationResult = ValidateInput(input, context);
            if (!validationResult.IsSuccess)
            {
                stopwatch.Stop();
                return HandleFailure(
                    validationResult.Error,
                    stopwatch.Elapsed,
                    metadata,
                    activity);
            }

            // Execute with timeout if configured
            TOutput output;
            if (Configuration.ExecutionTimeout.HasValue)
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken);
                cts.CancelAfter(Configuration.ExecutionTimeout.Value);
                output = await ExecuteCoreAsync(input, context with { CancellationToken = cts.Token });
            }
            else
            {
                output = await ExecuteCoreAsync(input, context);
            }

            stopwatch.Stop();
            return HandleSuccess(output, stopwatch.Elapsed, metadata, activity);
        }
        catch (OperationCanceledException ex)
        {
            stopwatch.Stop();
            return HandleFailure(
                $"Operation cancelled: {ex.Message}",
                stopwatch.Elapsed,
                metadata,
                activity);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return HandleFailure(
                $"Execution failed: {ex.Message}",
                stopwatch.Elapsed,
                metadata,
                activity);
        }
    }

    /// <inheritdoc/>
    public virtual Result<bool, string> ValidateReadiness()
    {
        // Base implementation - derived classes can override
        return Result<bool, string>.Success(true);
    }

    /// <summary>
    /// Activity source for tracing.
    /// </summary>
    private static readonly ActivitySource ActivitySource = new("Ouroboros.Orchestrator", typeof(OrchestratorBase<,>).Assembly.GetName().Version?.ToString() ?? "1.0.0");

    /// <inheritdoc/>
    public virtual async Task<Dictionary<string, object>> GetHealthAsync(CancellationToken ct = default)
    {
        var health = new Dictionary<string, object>
        {
            ["orchestrator_name"] = _orchestratorName,
            ["status"] = "healthy",
            ["total_executions"] = _metrics.TotalExecutions,
            ["success_rate"] = _metrics.SuccessRate,
            ["average_latency_ms"] = _metrics.AverageLatencyMs,
            ["last_executed"] = _metrics.LastExecuted,
            ["configuration"] = new Dictionary<string, object>
            {
                ["tracing_enabled"] = Configuration.EnableTracing,
                ["metrics_enabled"] = Configuration.EnableMetrics,
                ["safety_checks_enabled"] = Configuration.EnableSafetyChecks,
                ["execution_timeout"] = Configuration.ExecutionTimeout?.ToString() ?? "none"
            }
        };

        // Allow derived classes to add health info
        var customHealth = await GetCustomHealthAsync(ct);
        foreach (var kvp in customHealth)
        {
            health[kvp.Key] = kvp.Value;
        }

        return health;
    }

    /// <summary>
    /// Core execution logic to be implemented by derived orchestrators.
    /// </summary>
    /// <param name="input">The input to process.</param>
    /// <param name="context">Execution context.</param>
    /// <returns>The orchestration output.</returns>
    protected abstract Task<TOutput> ExecuteCoreAsync(TInput input, OrchestratorContext context);

    /// <summary>
    /// Validates input before execution. Override to add custom validation.
    /// </summary>
    /// <param name="input">The input to validate.</param>
    /// <param name="context">Execution context.</param>
    /// <returns>Result indicating validity with optional error message.</returns>
    protected virtual Result<bool, string> ValidateInput(TInput input, OrchestratorContext context)
    {
        if (input == null)
        {
            return Result<bool, string>.Failure("Input cannot be null");
        }

        return Result<bool, string>.Success(true);
    }

    /// <summary>
    /// Allows derived classes to provide custom health information.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Dictionary of custom health metrics.</returns>
    protected virtual Task<Dictionary<string, object>> GetCustomHealthAsync(CancellationToken ct)
    {
        return Task.FromResult(new Dictionary<string, object>());
    }

    /// <summary>
    /// Records a metric for this orchestrator.
    /// </summary>
    /// <param name="latencyMs">Execution latency in milliseconds.</param>
    /// <param name="success">Whether execution succeeded.</param>
    protected void RecordMetric(double latencyMs, bool success)
    {
        if (Configuration.EnableMetrics)
        {
            _metrics = _metrics.RecordExecution(latencyMs, success);
        }
    }

    /// <summary>
    /// Records a custom metric.
    /// </summary>
    /// <param name="key">Metric key.</param>
    /// <param name="value">Metric value.</param>
    protected void RecordCustomMetric(string key, double value)
    {
        if (Configuration.EnableMetrics)
        {
            _metrics = _metrics.WithCustomMetric(key, value);
        }
    }

    /// <summary>
    /// Checks safety of an operation if safety guard is configured.
    /// </summary>
    /// <param name="action">Action to check.</param>
    /// <param name="parameters">Action parameters.</param>
    /// <param name="permissionLevel">Required permission level.</param>
    /// <returns>Safety check result.</returns>
    protected SafetyCheckResult CheckSafety(
        string action,
        Dictionary<string, object> parameters,
        PermissionLevel permissionLevel = PermissionLevel.ReadOnly)
    {
        if (!Configuration.EnableSafetyChecks || _safetyGuard == null)
        {
            return new SafetyCheckResult(
                Safe: true,
                Violations: new List<string>(),
                Warnings: new List<string>(),
                RequiredLevel: permissionLevel);
        }

        return _safetyGuard.CheckSafety(action, parameters, permissionLevel);
    }

    /// <summary>
    /// Executes an operation with retry logic if configured.
    /// </summary>
    /// <typeparam name="T">Return type of operation.</typeparam>
    /// <param name="operation">Operation to execute.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result of the operation.</returns>
    protected async Task<Result<T, string>> ExecuteWithRetryAsync<T>(
        Func<Task<T>> operation,
        CancellationToken ct = default)
    {
        var retryConfig = Configuration.RetryConfig ?? RetryConfig.Default();
        var attempt = 0;
        var delay = retryConfig.InitialDelay;

        while (true)
        {
            attempt++;
            try
            {
                var result = await operation();
                return Result<T, string>.Success(result);
            }
            catch (Exception) when (attempt < retryConfig.MaxRetries && !ct.IsCancellationRequested)
            {
                // Add jitter to prevent thundering herd
                var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, (int)(delay.TotalMilliseconds * 0.1)));
                await Task.Delay(delay + jitter, ct);
                delay = TimeSpan.FromMilliseconds(
                    Math.Min(delay.TotalMilliseconds * retryConfig.BackoffMultiplier,
                             retryConfig.MaxDelay.TotalMilliseconds));
            }
            catch (Exception ex)
            {
                return Result<T, string>.Failure($"Operation failed after {attempt} attempts: {ex.Message}");
            }
        }
    }

    private OrchestratorResult<TOutput> HandleSuccess(
        TOutput output,
        TimeSpan executionTime,
        Dictionary<string, object> metadata,
        Activity? activity)
    {
        RecordMetric(executionTime.TotalMilliseconds, success: true);

        if (Configuration.EnableTracing && activity != null)
        {
            activity.SetTag("orchestrator.success", true);
            activity.SetTag("orchestrator.duration_ms", executionTime.TotalMilliseconds);
            activity.SetStatus(ActivityStatusCode.Ok);
        }

        metadata["completed_at"] = DateTime.UtcNow;
        metadata["success"] = true;

        return OrchestratorResult<TOutput>.Ok(output, _metrics, executionTime, metadata);
    }

    private OrchestratorResult<TOutput> HandleFailure(
        string errorMessage,
        TimeSpan executionTime,
        Dictionary<string, object> metadata,
        Activity? activity)
    {
        RecordMetric(executionTime.TotalMilliseconds, success: false);

        if (Configuration.EnableTracing && activity != null)
        {
            activity.SetTag("orchestrator.success", false);
            activity.SetTag("orchestrator.duration_ms", executionTime.TotalMilliseconds);
            activity.SetTag("orchestrator.error", errorMessage);
            activity.SetStatus(ActivityStatusCode.Error);
        }

        metadata["completed_at"] = DateTime.UtcNow;
        metadata["success"] = false;
        metadata["error"] = errorMessage;

        return OrchestratorResult<TOutput>.Failure(errorMessage, _metrics, executionTime, metadata);
    }
}
