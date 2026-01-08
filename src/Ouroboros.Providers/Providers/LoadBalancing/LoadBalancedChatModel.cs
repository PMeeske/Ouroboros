// <copyright file="LoadBalancedChatModel.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Diagnostics;
using System.Net;
using Polly;
using Polly.Retry;

namespace Ouroboros.Providers.LoadBalancing;

/// <summary>
/// Chat model wrapper that provides load balancing across multiple provider instances.
/// Automatically handles rate limiting (429) by rotating to healthy providers.
/// Uses Polly for resilient retry logic with exponential backoff.
/// Implements IChatCompletionModel for seamless integration with existing code.
/// </summary>
public sealed class LoadBalancedChatModel : IChatCompletionModel, IDisposable
{
    private readonly IProviderLoadBalancer<IChatCompletionModel> _loadBalancer;
    private readonly AsyncRetryPolicy _retryPolicy;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoadBalancedChatModel"/> class.
    /// </summary>
    /// <param name="strategy">The load balancing strategy to use. If null, defaults to AdaptiveHealth.</param>
    public LoadBalancedChatModel(IProviderSelectionStrategy? strategy = null)
    {
        _loadBalancer = new ProviderLoadBalancer<IChatCompletionModel>(strategy);
        _retryPolicy = CreateRetryPolicy();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LoadBalancedChatModel"/> class with a custom load balancer.
    /// </summary>
    /// <param name="loadBalancer">Custom load balancer instance.</param>
    public LoadBalancedChatModel(IProviderLoadBalancer<IChatCompletionModel> loadBalancer)
    {
        _loadBalancer = loadBalancer ?? throw new ArgumentNullException(nameof(loadBalancer));
        _retryPolicy = CreateRetryPolicy();
    }

    /// <summary>
    /// Creates Polly retry policy for handling provider failures and selection retries.
    /// </summary>
    private static AsyncRetryPolicy CreateRetryPolicy()
    {
        return Policy
            .Handle<HttpRequestException>(ex => IsRateLimitError(ex))
            .Or<InvalidOperationException>(ex => ex.Message.Contains("No healthy providers"))
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1)),
                onRetry: (exception, timespan, retryCount, context) =>
                {
                    Console.WriteLine($"[LoadBalancedChatModel] Retry {retryCount} after {timespan.TotalSeconds}s due to {exception.GetType().Name}: {exception.Message}");
                });
    }

    /// <summary>
    /// Registers a provider with the load balancer.
    /// </summary>
    /// <param name="providerId">Unique identifier for the provider.</param>
    /// <param name="model">The chat model instance.</param>
    public void RegisterProvider(string providerId, IChatCompletionModel model)
    {
        if (_loadBalancer is ProviderLoadBalancer<IChatCompletionModel> balancer)
        {
            balancer.RegisterProvider(providerId, model);
        }
        else
        {
            throw new InvalidOperationException("Cannot register provider with custom load balancer");
        }
    }

    /// <summary>
    /// Gets the current health status of all providers.
    /// </summary>
    public IReadOnlyDictionary<string, ProviderHealthStatus> GetHealthStatus()
    {
        return _loadBalancer.GetHealthStatus();
    }

    /// <summary>
    /// Gets the load balancing strategy being used.
    /// </summary>
    public IProviderSelectionStrategy Strategy => _loadBalancer.Strategy;

    /// <summary>
    /// Gets the total number of registered providers.
    /// </summary>
    public int ProviderCount => _loadBalancer.ProviderCount;

    /// <summary>
    /// Gets the number of currently healthy providers.
    /// </summary>
    public int HealthyProviderCount => _loadBalancer.HealthyProviderCount;

    /// <inheritdoc/>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        List<string> attemptedProviders = new();

        try
        {
            return await _retryPolicy.ExecuteAsync(async () =>
            {
                // Select a provider
                Result<ProviderSelectionResult<IChatCompletionModel>, string> selectionResult =
                    await _loadBalancer.SelectProviderAsync();

                if (selectionResult.IsFailure)
                {
                    // No healthy providers available
                    string error = selectionResult.Match(_ => string.Empty, err => err);
                    Console.WriteLine($"[LoadBalancedChatModel] Provider selection failed: {error}");
                    
                    // Throw to trigger Polly retry
                    throw new InvalidOperationException($"No healthy providers available: {error}");
                }

                ProviderSelectionResult<IChatCompletionModel> selection = selectionResult.Match(
                    result => result,
                    _ => throw new InvalidOperationException("Should not reach here"));

                attemptedProviders.Add(selection.ProviderId);
                Stopwatch sw = Stopwatch.StartNew();

                try
                {
                    string result = await selection.Provider.GenerateTextAsync(prompt, ct);
                    sw.Stop();

                    // Record successful execution
                    _loadBalancer.RecordExecution(
                        selection.ProviderId,
                        sw.Elapsed.TotalMilliseconds,
                        success: true,
                        wasRateLimited: false);

                    Console.WriteLine($"[LoadBalancedChatModel] Success with provider '{selection.ProviderId}' " +
                                    $"in {sw.Elapsed.TotalMilliseconds:F0}ms");

                    return result;
                }
                catch (HttpRequestException ex) when (IsRateLimitError(ex))
                {
                    sw.Stop();

                    // Record rate limited execution
                    _loadBalancer.RecordExecution(
                        selection.ProviderId,
                        sw.Elapsed.TotalMilliseconds,
                        success: false,
                        wasRateLimited: true);

                    Console.WriteLine($"[LoadBalancedChatModel] Provider '{selection.ProviderId}' rate limited (429). " +
                                    $"Attempting with another provider...");

                    // Re-throw to trigger Polly retry with exponential backoff
                    throw;
                }
                catch (Exception ex)
                {
                    sw.Stop();

                    // Record failed execution
                    _loadBalancer.RecordExecution(
                        selection.ProviderId,
                        sw.Elapsed.TotalMilliseconds,
                        success: false,
                        wasRateLimited: false);

                    Console.WriteLine($"[LoadBalancedChatModel] Provider '{selection.ProviderId}' failed: {ex.Message}");

                    // Re-throw to trigger Polly retry
                    throw;
                }
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // All retries exhausted - return error message for graceful degradation
            Console.WriteLine($"[LoadBalancedChatModel] All retries exhausted: {ex.Message}");
            
            string providersAttempted = attemptedProviders.Count > 0 
                ? $"Attempted: {string.Join(", ", attemptedProviders)}" 
                : "No providers could be selected";
            
            return $"[load-balanced-error] All providers exhausted. {providersAttempted}. Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Checks if an exception indicates a rate limit error (HTTP 429).
    /// </summary>
    private static bool IsRateLimitError(Exception ex)
    {
        if (ex is HttpRequestException httpEx)
        {
            // Check for 429 status code in various ways
            string message = httpEx.Message;
            if (message.Contains("429") || message.Contains("Too Many Requests", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Check StatusCode property if available (requires .NET 5+)
            if (httpEx.StatusCode.HasValue && httpEx.StatusCode.Value == HttpStatusCode.TooManyRequests)
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;

        // Load balancer doesn't own the providers, so no disposal needed
        _disposed = true;
    }
}
