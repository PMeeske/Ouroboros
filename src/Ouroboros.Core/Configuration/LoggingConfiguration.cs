// <copyright file="LoggingConfiguration.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Configuration;

using Microsoft.Extensions.Configuration;
using Serilog;
using Serilog.Events;

/// <summary>
/// Helper class for configuring structured logging with Serilog.
/// </summary>
public static class LoggingConfiguration
{
    /// <summary>
    /// Configures Serilog with the provided configuration and pipeline settings.
    /// </summary>
    /// <returns></returns>
    public static ILogger CreateLogger(IConfiguration configuration, PipelineConfiguration? pipelineConfig = null)
    {
        LoggerConfiguration loggerConfig = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithEnvironmentName();

        // Apply pipeline-specific log level if configured
        if (pipelineConfig?.Observability != null)
        {
            LogEventLevel minLevel = ParseLogLevel(pipelineConfig.Observability.MinimumLogLevel);
            loggerConfig.MinimumLevel.Is(minLevel);
        }

        // Add console sink with appropriate formatting
        loggerConfig.WriteTo.Console(
            outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");

        // Add file sink for persistent logging
        loggerConfig.WriteTo.File(
            path: "logs/pipeline-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}");

        return loggerConfig.CreateLogger();
    }

    /// <summary>
    /// Configures Serilog with default settings for the given environment.
    /// </summary>
    /// <returns></returns>
    public static ILogger CreateDefaultLogger(string environmentName = "Production")
    {
        LogEventLevel logLevel = environmentName == "Development" ? LogEventLevel.Debug : LogEventLevel.Information;

        return new LoggerConfiguration()
            .MinimumLevel.Is(logLevel)
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .Enrich.WithThreadId()
            .Enrich.WithEnvironmentName()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: "logs/pipeline-.log",
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();
    }

    private static LogEventLevel ParseLogLevel(string logLevel)
    {
        return logLevel.ToUpperInvariant() switch
        {
            "VERBOSE" or "TRACE" => LogEventLevel.Verbose,
            "DEBUG" => LogEventLevel.Debug,
            "INFORMATION" or "INFO" => LogEventLevel.Information,
            "WARNING" or "WARN" => LogEventLevel.Warning,
            "ERROR" => LogEventLevel.Error,
            "FATAL" or "CRITICAL" => LogEventLevel.Fatal,
            _ => LogEventLevel.Information,
        };
    }
}
