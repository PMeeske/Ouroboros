// <copyright file="OuroborosCliIntegration.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Ouroboros.CLI;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ouroboros.Application.Integration;

/// <summary>
/// Integrates the full Ouroboros system into CLI commands.
/// Ensures all commands have access to the unified core by default.
/// </summary>
public static class OuroborosCliIntegration
{
    private static IServiceProvider? _serviceProvider;
    private static IOuroborosCore? _ouroborosCore;
    private static OuroborosTelemetry? _telemetry;

    /// <summary>
    /// Initializes the Ouroboros system for CLI usage.
    /// Should be called early in Program.cs startup.
    /// </summary>
    public static async Task<IServiceProvider> InitializeAsync(string[] args)
    {
        if (_serviceProvider != null)
        {
            return _serviceProvider;
        }

        var builder = Host.CreateDefaultBuilder(args);

        // Configure services
        builder.ConfigureServices((context, services) =>
        {
            // Load Ouroboros configuration
            var config = context.Configuration;
            var ouroborosConfig = OuroborosConfiguration.Load(config);

            // Register configuration
            services.AddSingleton(ouroborosConfig);

            // Add full Ouroboros system with monitoring
            services.AddOuroborosFullWithMonitoring();

            // Add logging
            services.AddLogging();
        });

        var host = builder.Build();
        _serviceProvider = host.Services;

        // Resolve core components
        _ouroborosCore = _serviceProvider.GetService<IOuroborosCore>();
        _telemetry = _serviceProvider.GetService<OuroborosTelemetry>();

        return _serviceProvider;
    }

    /// <summary>
    /// Gets the Ouroboros core instance.
    /// Initializes system if not already done.
    /// </summary>
    public static IOuroborosCore? GetCore()
    {
        return _ouroborosCore;
    }

    /// <summary>
    /// Gets the telemetry instance.
    /// </summary>
    public static OuroborosTelemetry? GetTelemetry()
    {
        return _telemetry;
    }

    /// <summary>
    /// Gets the service provider for dependency resolution.
    /// </summary>
    public static IServiceProvider? GetServiceProvider()
    {
        return _serviceProvider;
    }

    /// <summary>
    /// Checks if Ouroboros system is initialized.
    /// </summary>
    public static bool IsInitialized => _serviceProvider != null;

    /// <summary>
    /// Executes a goal using the Ouroboros system if initialized.
    /// Falls back gracefully if system is not initialized.
    /// </summary>
    public static async Task<bool> TryExecuteGoalAsync(
        string goal,
        ExecutionConfig? config = null,
        Action<ExecutionResult>? onSuccess = null,
        Action<string>? onError = null)
    {
        if (_ouroborosCore == null)
        {
            return false; // Not initialized, fall back to regular command handling
        }

        config ??= ExecutionConfig.Default;
        _telemetry?.RecordGoalExecution(true, TimeSpan.Zero);

        var result = await _ouroborosCore.ExecuteGoalAsync(goal, config);

        result.Match(
            success =>
            {
                _telemetry?.RecordGoalExecution(true, success.Duration);
                onSuccess?.Invoke(success);
            },
            error =>
            {
                _telemetry?.RecordError("goal_execution", "execution_failed");
                onError?.Invoke(error);
            });

        return true;
    }

    /// <summary>
    /// Performs reasoning using the Ouroboros system if initialized.
    /// </summary>
    public static async Task<bool> TryReasonAsync(
        string query,
        ReasoningConfig? config = null,
        Action<ReasoningResult>? onSuccess = null,
        Action<string>? onError = null)
    {
        if (_ouroborosCore == null)
        {
            return false;
        }

        config ??= ReasoningConfig.Default;
        var startTime = DateTime.UtcNow;

        var result = await _ouroborosCore.ReasonAboutAsync(query, config);

        var duration = DateTime.UtcNow - startTime;
        _telemetry?.RecordReasoningQuery(
            duration,
            config.UseSymbolicReasoning,
            config.UseCausalInference,
            config.UseAbduction);

        result.Match(
            onSuccess ?? (_ => { }),
            onError ?? (_ => { }));

        return true;
    }

    /// <summary>
    /// Records telemetry for CLI operations.
    /// </summary>
    public static void RecordCliOperation(string operation, bool success, TimeSpan duration)
    {
        _telemetry?.RecordGoalExecution(success, duration, new Dictionary<string, object>
        {
            ["operation"] = operation,
            ["cli"] = true
        });
    }

    /// <summary>
    /// Gets health status of the Ouroboros system.
    /// </summary>
    public static string GetHealthStatus()
    {
        if (_ouroborosCore == null)
        {
            return "Not initialized";
        }

        var status = new System.Text.StringBuilder();
        status.AppendLine("Ouroboros System Status:");
        status.AppendLine($"  Episodic Memory: {(_ouroborosCore.EpisodicMemory != null ? "✓" : "✗")}");
        status.AppendLine($"  MeTTa Reasoning: {(_ouroborosCore.MeTTaReasoning != null ? "✓" : "✗")}");
        status.AppendLine($"  Hierarchical Planner: {(_ouroborosCore.HierarchicalPlanner != null ? "✓" : "✗")}");
        status.AppendLine($"  Causal Reasoning: {(_ouroborosCore.CausalReasoning != null ? "✓" : "✗")}");
        status.AppendLine($"  Consciousness: {(_ouroborosCore.Consciousness != null ? "✓" : "✗")}");
        status.AppendLine($"  Reflection: {(_ouroborosCore.Reflection != null ? "✓" : "✗")}");

        return status.ToString();
    }

    /// <summary>
    /// Ensures Ouroboros is initialized before command execution.
    /// Call this at the start of any CLI command that should integrate with Ouroboros.
    /// </summary>
    public static async Task EnsureInitializedAsync(string[] args)
    {
        if (!IsInitialized)
        {
            try
            {
                await InitializeAsync(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] Could not initialize Ouroboros system: {ex.Message}");
                Console.WriteLine("[INFO] Commands will run in standalone mode");
            }
        }
    }

    /// <summary>
    /// Broadcasts information to consciousness if available.
    /// Used to make the system aware of CLI activities.
    /// </summary>
    public static async Task BroadcastToConsciousnessAsync(string content, string source)
    {
        if (_ouroborosCore?.Consciousness != null)
        {
            await _ouroborosCore.Consciousness.BroadcastToConsciousnessAsync(
                content,
                source);
        }
    }
}

/// <summary>
/// Extension methods for integrating Ouroboros into CLI commands.
/// </summary>
public static class CommandIntegrationExtensions
{
    /// <summary>
    /// Wraps a CLI command with Ouroboros integration.
    /// Ensures telemetry and consciousness integration.
    /// </summary>
    public static async Task WithOuroborosIntegrationAsync(
        this Task commandTask,
        string commandName,
        string description)
    {
        var startTime = DateTime.UtcNow;
        var success = false;

        try
        {
            // Broadcast command start to consciousness
            await OuroborosCliIntegration.BroadcastToConsciousnessAsync(
                $"Starting CLI command: {commandName}",
                "CLI");

            await commandTask;
            success = true;

            // Broadcast completion
            await OuroborosCliIntegration.BroadcastToConsciousnessAsync(
                $"Completed CLI command: {commandName}",
                "CLI");
        }
        finally
        {
            var duration = DateTime.UtcNow - startTime;
            OuroborosCliIntegration.RecordCliOperation(commandName, success, duration);
        }
    }

    /// <summary>
    /// Wraps a CLI command with Ouroboros integration and returns result.
    /// </summary>
    public static async Task<T> WithOuroborosIntegrationAsync<T>(
        this Task<T> commandTask,
        string commandName,
        string description)
    {
        var startTime = DateTime.UtcNow;
        var success = false;
        T result;

        try
        {
            await OuroborosCliIntegration.BroadcastToConsciousnessAsync(
                $"Starting CLI command: {commandName} - {description}",
                "CLI");

            result = await commandTask;
            success = true;

            await OuroborosCliIntegration.BroadcastToConsciousnessAsync(
                $"Completed CLI command: {commandName}",
                "CLI");

            return result;
        }
        finally
        {
            var duration = DateTime.UtcNow - startTime;
            OuroborosCliIntegration.RecordCliOperation(commandName, success, duration);
        }
    }
}
