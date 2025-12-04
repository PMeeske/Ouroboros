// <copyright file="EnvironmentDetector.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Core;

/// <summary>
/// Detects the current runtime environment (local development vs. production/staging).
/// </summary>
public static class EnvironmentDetector
{
    /// <summary>
    /// Determines if the application is running in local development mode.
    /// Checks multiple indicators including environment variables, endpoints, and deployment context.
    /// </summary>
    /// <returns>True if running in local development, false otherwise.</returns>
    public static bool IsLocalDevelopment()
    {
        // Check ASPNETCORE_ENVIRONMENT or DOTNET_ENVIRONMENT first
        string? environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                         ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

        if (!string.IsNullOrWhiteSpace(environment))
        {
            // Development or Local environment names indicate local development
            if (environment.Equals("Development", StringComparison.OrdinalIgnoreCase) ||
                environment.Equals("Local", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Production or Staging explicitly indicate non-local
            if (environment.Equals("Production", StringComparison.OrdinalIgnoreCase) ||
                environment.Equals("Staging", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Check for Kubernetes environment (indicates non-local deployment)
        if (IsRunningInKubernetes())
        {
            return false;
        }

        // Check for localhost Ollama endpoint as indicator of local development
        string? ollamaEndpoint = Environment.GetEnvironmentVariable("PIPELINE__LlmProvider__OllamaEndpoint");
        if (!string.IsNullOrWhiteSpace(ollamaEndpoint) &&
            (ollamaEndpoint.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
             ollamaEndpoint.Contains("127.0.0.1")))
        {
            return true;
        }

        // Default to production (safe default - assume non-local unless proven otherwise)
        return false;
    }

    /// <summary>
    /// Determines if the application is running inside a Kubernetes cluster.
    /// </summary>
    /// <returns>True if running in Kubernetes, false otherwise.</returns>
    public static bool IsRunningInKubernetes()
    {
        // Check for Kubernetes service account token (standard way to detect K8s)
        if (Directory.Exists("/var/run/secrets/kubernetes.io/serviceaccount"))
        {
            return true;
        }

        // Check for KUBERNETES_SERVICE_HOST environment variable
        string? k8sHost = Environment.GetEnvironmentVariable("KUBERNETES_SERVICE_HOST");
        if (!string.IsNullOrWhiteSpace(k8sHost))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the current environment name from environment variables.
    /// </summary>
    /// <returns>Environment name (Development, Staging, Production) or null if not set.</returns>
    public static string? GetEnvironmentName()
    {
        return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
               ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
    }

    /// <summary>
    /// Determines if the application is running in production mode.
    /// </summary>
    /// <returns>True if running in production, false otherwise.</returns>
    public static bool IsProduction()
    {
        string? environment = GetEnvironmentName();

        if (!string.IsNullOrWhiteSpace(environment))
        {
            return environment.Equals("Production", StringComparison.OrdinalIgnoreCase);
        }

        // If in Kubernetes and no environment set, assume production
        if (IsRunningInKubernetes())
        {
            return true;
        }

        // Default to production (safe default)
        return true;
    }

    /// <summary>
    /// Determines if the application is running in staging mode.
    /// </summary>
    /// <returns>True if running in staging, false otherwise.</returns>
    public static bool IsStaging()
    {
        string? environment = GetEnvironmentName();
        return !string.IsNullOrWhiteSpace(environment) &&
               environment.Equals("Staging", StringComparison.OrdinalIgnoreCase);
    }
}
