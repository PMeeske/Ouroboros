// <copyright file="QdrantHealthCheckProvider.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Core.Infrastructure.HealthCheck;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Health check provider for Qdrant vector database.
/// </summary>
public sealed class QdrantHealthCheckProvider : IHealthCheckProvider, IDisposable
{
    private const double DegradedThresholdMultiplier = 0.5;
    private readonly HttpClient httpClient;
    private readonly string endpoint;
    private readonly int timeoutSeconds;
    private bool disposed = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="QdrantHealthCheckProvider"/> class.
    /// </summary>
    /// <param name="endpoint">Qdrant endpoint URL.</param>
    /// <param name="timeoutSeconds">Timeout in seconds for health check.</param>
    public QdrantHealthCheckProvider(string endpoint, int timeoutSeconds = 5)
    {
        this.endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        this.timeoutSeconds = timeoutSeconds;
        this.httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(timeoutSeconds),
        };
    }

    /// <inheritdoc/>
    public string ComponentName => "Qdrant";

    /// <inheritdoc/>
    public async Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
    {
        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            HttpResponseMessage response = await this.httpClient.GetAsync(
                $"{this.endpoint}/healthz",
                cancellationToken);

            sw.Stop();

            if (response.IsSuccessStatusCode)
            {
                Dictionary<string, object> details = new Dictionary<string, object>
                {
                    { "endpoint", this.endpoint },
                    { "statusCode", (int)response.StatusCode },
                };

                // Check if response time is slow (more than 50% of timeout threshold)
                long degradedThreshold = (long)(this.timeoutSeconds * 1000 * DegradedThresholdMultiplier);
                if (sw.ElapsedMilliseconds > degradedThreshold)
                {
                    details.Add("warning", "Slow response time");
                    return HealthCheckResult.Degraded(
                        this.ComponentName,
                        sw.ElapsedMilliseconds,
                        details,
                        "Response time exceeds degraded threshold");
                }

                return HealthCheckResult.Healthy(
                    this.ComponentName,
                    sw.ElapsedMilliseconds,
                    details);
            }

            return HealthCheckResult.Unhealthy(
                this.ComponentName,
                sw.ElapsedMilliseconds,
                $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}",
                new Dictionary<string, object>
                {
                    { "endpoint", this.endpoint },
                    { "statusCode", (int)response.StatusCode },
                });
        }
        catch (TaskCanceledException)
        {
            sw.Stop();
            return HealthCheckResult.Unhealthy(
                this.ComponentName,
                sw.ElapsedMilliseconds,
                $"Request timeout after {this.timeoutSeconds}s",
                new Dictionary<string, object>
                {
                    { "endpoint", this.endpoint },
                    { "timeout", this.timeoutSeconds },
                });
        }
        catch (Exception ex)
        {
            sw.Stop();
            return HealthCheckResult.Unhealthy(
                this.ComponentName,
                sw.ElapsedMilliseconds,
                ex.Message,
                new Dictionary<string, object>
                {
                    { "endpoint", this.endpoint },
                    { "exceptionType", ex.GetType().Name },
                });
        }
    }

    /// <summary>
    /// Disposes the HTTP client.
    /// </summary>
    public void Dispose()
    {
        if (!this.disposed)
        {
            this.httpClient?.Dispose();
            this.disposed = true;
        }
    }
}
