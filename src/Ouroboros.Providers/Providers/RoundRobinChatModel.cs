#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using System.Reactive.Linq;

namespace Ouroboros.Providers;

/// <summary>
/// Configuration for a single provider in the round-robin pool.
/// </summary>
public sealed record ProviderConfig(
    string Name,
    ChatEndpointType EndpointType,
    string? Endpoint = null,
    string? ApiKey = null,
    string? Model = null,
    int Weight = 1,
    bool Enabled = true);

/// <summary>
/// Statistics for a provider in the round-robin pool.
/// </summary>
public sealed class ProviderStats
{
    public string Name { get; init; } = "";
    public int TotalRequests { get; set; }
    public int SuccessfulRequests { get; set; }
    public int FailedRequests { get; set; }
    public int ConsecutiveFailures { get; set; }
    public DateTime? LastSuccess { get; set; }
    public DateTime? LastFailure { get; set; }
    public bool IsHealthy => ConsecutiveFailures < 3;
    public double SuccessRate => TotalRequests > 0 ? (double)SuccessfulRequests / TotalRequests : 1.0;
}

/// <summary>
/// Round-robin chat model that distributes requests across multiple providers.
/// Supports weighted distribution, automatic failover, and health tracking.
/// </summary>
public sealed class RoundRobinChatModel : IStreamingThinkingChatModel, ICostAwareChatModel, IDisposable
{
    private readonly List<(IChatCompletionModel Model, ProviderConfig Config, ProviderStats Stats)> _providers = new();
    private readonly object _lock = new();
    private int _currentIndex;
    private readonly bool _failoverEnabled;
    private readonly int _maxRetries;
    private readonly LlmCostTracker _aggregateCostTracker;

    /// <summary>
    /// Gets the cost tracker that aggregates costs across all providers.
    /// </summary>
    public LlmCostTracker? CostTracker => _aggregateCostTracker;

    /// <summary>
    /// Gets statistics for all providers.
    /// </summary>
    public IReadOnlyList<ProviderStats> ProviderStatistics
    {
        get
        {
            lock (_lock)
            {
                return _providers.Select(p => p.Stats).ToList();
            }
        }
    }

    /// <summary>
    /// Gets the number of active (healthy) providers.
    /// </summary>
    public int ActiveProviderCount
    {
        get
        {
            lock (_lock)
            {
                return _providers.Count(p => p.Config.Enabled && p.Stats.IsHealthy);
            }
        }
    }

    /// <summary>
    /// Initializes a new round-robin chat model.
    /// </summary>
    /// <param name="failoverEnabled">If true, automatically retry with next provider on failure.</param>
    /// <param name="maxRetries">Maximum number of providers to try before giving up.</param>
    public RoundRobinChatModel(bool failoverEnabled = true, int maxRetries = 3)
    {
        _failoverEnabled = failoverEnabled;
        _maxRetries = maxRetries;
        _aggregateCostTracker = new LlmCostTracker("round-robin", "Multiple");
    }

    /// <summary>
    /// Adds a provider to the round-robin pool.
    /// </summary>
    public void AddProvider(IChatCompletionModel model, ProviderConfig config)
    {
        lock (_lock)
        {
            var stats = new ProviderStats { Name = config.Name };
            _providers.Add((model, config, stats));
        }
    }

    /// <summary>
    /// Adds a provider with automatic model creation based on config.
    /// </summary>
    public void AddProvider(ProviderConfig config, ChatRuntimeSettings? settings = null)
    {
        var (endpoint, apiKey, _) = ChatConfig.ResolveWithOverrides(
            config.Endpoint,
            config.ApiKey,
            config.EndpointType.ToString());

        // Use default endpoint if not resolved
        endpoint ??= ChatConfig.GetDefaultEndpoint(config.EndpointType);

        var costTracker = new LlmCostTracker(config.Model ?? "unknown", config.Name);
        IChatCompletionModel model = CreateModel(config.EndpointType, endpoint ?? "", apiKey ?? "", config.Model ?? "", settings, costTracker);

        AddProvider(model, config);
    }

    private static IChatCompletionModel CreateModel(
        ChatEndpointType endpointType,
        string endpoint,
        string apiKey,
        string model,
        ChatRuntimeSettings? settings,
        LlmCostTracker? costTracker)
    {
        return endpointType switch
        {
            ChatEndpointType.Anthropic => new AnthropicChatModel(apiKey, model, settings, costTracker: costTracker),
            ChatEndpointType.OllamaCloud => new OllamaCloudChatModel(endpoint, apiKey, model, settings, costTracker),
            ChatEndpointType.OllamaLocal => new OllamaCloudChatModel(endpoint, "ollama", model, settings, costTracker),
            ChatEndpointType.GitHubModels => new GitHubModelsChatModel(apiKey, model, endpoint, settings, costTracker),
            _ => new LiteLLMChatModel(endpoint, apiKey, model, settings, costTracker)
        };
    }

    /// <summary>
    /// Gets the next provider using weighted round-robin selection.
    /// </summary>
    private (IChatCompletionModel Model, ProviderConfig Config, ProviderStats Stats)? GetNextProvider(HashSet<int>? excludeIndices = null)
    {
        lock (_lock)
        {
            if (_providers.Count == 0) return null;

            int startIndex = _currentIndex;
            int attempts = 0;

            while (attempts < _providers.Count)
            {
                var (model, config, stats) = _providers[_currentIndex];
                _currentIndex = (_currentIndex + 1) % _providers.Count;
                attempts++;

                // Skip disabled, unhealthy, or excluded providers
                if (!config.Enabled) continue;
                if (!stats.IsHealthy && _failoverEnabled) continue;
                if (excludeIndices?.Contains(_currentIndex) == true) continue;

                return (model, config, stats);
            }

            // If failover is disabled or all providers are unhealthy, try any enabled provider
            foreach (var provider in _providers)
            {
                if (provider.Config.Enabled && excludeIndices?.Contains(_providers.IndexOf(provider)) != true)
                    return provider;
            }

            return null;
        }
    }

    /// <inheritdoc/>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        var response = await GenerateWithThinkingAsync(prompt, ct);
        return response.HasThinking ? response.ToFormattedString() : response.Content;
    }

    /// <inheritdoc/>
    public async Task<ThinkingResponse> GenerateWithThinkingAsync(string prompt, CancellationToken ct = default)
    {
        HashSet<int> triedIndices = new();
        Exception? lastException = null;
        int retries = 0;

        while (retries < _maxRetries)
        {
            var provider = GetNextProvider(triedIndices);
            if (provider == null)
            {
                throw new InvalidOperationException("No available providers in round-robin pool");
            }

            var (model, config, stats) = provider.Value;
            int providerIndex = _providers.IndexOf(provider.Value);
            triedIndices.Add(providerIndex);

            try
            {
                stats.TotalRequests++;
                _aggregateCostTracker.StartRequest();

                ThinkingResponse result;
                if (model is IThinkingChatModel thinkingModel)
                {
                    result = await thinkingModel.GenerateWithThinkingAsync(prompt, ct);
                }
                else
                {
                    string text = await model.GenerateTextAsync(prompt, ct);
                    result = new ThinkingResponse(null, text);
                }

                // Check for fallback responses (indicates failure)
                if (result.Content.Contains("-fallback:"))
                {
                    throw new InvalidOperationException($"Provider {config.Name} returned fallback response");
                }

                // Success
                stats.SuccessfulRequests++;
                stats.ConsecutiveFailures = 0;
                stats.LastSuccess = DateTime.UtcNow;

                // Track costs from provider's tracker
                if (model is ICostAwareChatModel costAware && costAware.CostTracker != null)
                {
                    var metrics = costAware.CostTracker.GetSessionMetrics();
                    _aggregateCostTracker.EndRequest((int)metrics.TotalInputTokens, (int)metrics.TotalOutputTokens);
                }
                else
                {
                    _aggregateCostTracker.EndRequest(0, 0);
                }

                return result;
            }
            catch (Exception ex)
            {
                stats.FailedRequests++;
                stats.ConsecutiveFailures++;
                stats.LastFailure = DateTime.UtcNow;
                lastException = ex;

                Console.WriteLine($"  ⚠ Provider '{config.Name}' failed: {ex.Message}");

                if (!_failoverEnabled)
                    throw;

                retries++;
            }
        }

        throw new AggregateException($"All {retries} providers failed", lastException!);
    }

    /// <inheritdoc/>
    public IObservable<string> StreamReasoningContent(string prompt, CancellationToken ct = default)
    {
        return StreamWithThinkingAsync(prompt, ct).Select(t => t.Chunk);
    }

    /// <inheritdoc/>
    public IObservable<(bool IsThinking, string Chunk)> StreamWithThinkingAsync(string prompt, CancellationToken ct = default)
    {
        return Observable.Create<(bool IsThinking, string Chunk)>(async (observer, token) =>
        {
            HashSet<int> triedIndices = new();
            int retries = 0;

            while (retries < _maxRetries)
            {
                var provider = GetNextProvider(triedIndices);
                if (provider == null)
                {
                    observer.OnError(new InvalidOperationException("No available providers"));
                    return;
                }

                var (model, config, stats) = provider.Value;
                int providerIndex = _providers.IndexOf(provider.Value);
                triedIndices.Add(providerIndex);

                try
                {
                    stats.TotalRequests++;

                    if (model is IStreamingThinkingChatModel streamingThinking)
                    {
                        bool hasContent = false;
                        await streamingThinking.StreamWithThinkingAsync(prompt, token)
                            .ForEachAsync(chunk =>
                            {
                                hasContent = true;
                                observer.OnNext(chunk);
                            }, token);

                        if (hasContent)
                        {
                            stats.SuccessfulRequests++;
                            stats.ConsecutiveFailures = 0;
                            stats.LastSuccess = DateTime.UtcNow;
                            observer.OnCompleted();
                            return;
                        }
                    }
                    else
                    {
                        // Fall back to non-streaming
                        string result = await model.GenerateTextAsync(prompt, token);
                        if (!result.Contains("-fallback:"))
                        {
                            stats.SuccessfulRequests++;
                            stats.ConsecutiveFailures = 0;
                            stats.LastSuccess = DateTime.UtcNow;
                            observer.OnNext((false, result));
                            observer.OnCompleted();
                            return;
                        }
                    }

                    throw new InvalidOperationException($"Provider {config.Name} returned empty or fallback response");
                }
                catch (Exception ex)
                {
                    stats.FailedRequests++;
                    stats.ConsecutiveFailures++;
                    stats.LastFailure = DateTime.UtcNow;

                    Console.WriteLine($"  ⚠ Provider '{config.Name}' streaming failed: {ex.Message}");

                    if (!_failoverEnabled)
                    {
                        observer.OnError(ex);
                        return;
                    }

                    retries++;
                }
            }

            observer.OnError(new InvalidOperationException($"All {retries} providers failed"));
        });
    }

    /// <summary>
    /// Resets the health status of all providers.
    /// </summary>
    public void ResetHealth()
    {
        lock (_lock)
        {
            foreach (var (_, _, stats) in _providers)
            {
                stats.ConsecutiveFailures = 0;
            }
        }
    }

    /// <summary>
    /// Gets a formatted status summary of all providers.
    /// </summary>
    public string GetStatusSummary()
    {
        lock (_lock)
        {
            if (_providers.Count == 0)
                return "No providers configured";

            var lines = new List<string>
            {
                $"Round-Robin Pool: {_providers.Count} providers ({ActiveProviderCount} healthy)",
                ""
            };

            foreach (var (_, config, stats) in _providers)
            {
                string status = stats.IsHealthy ? "✓" : "✗";
                string enabled = config.Enabled ? "" : " [disabled]";
                lines.Add($"  {status} {config.Name}{enabled}: {stats.SuccessfulRequests}/{stats.TotalRequests} ({stats.SuccessRate:P0})");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_lock)
        {
            foreach (var (model, _, _) in _providers)
            {
                if (model is IDisposable disposable)
                    disposable.Dispose();
            }
            _providers.Clear();
        }
    }
}

/// <summary>
/// Builder for creating RoundRobinChatModel instances with fluent API.
/// </summary>
public sealed class RoundRobinBuilder
{
    private readonly List<ProviderConfig> _configs = new();
    private readonly ChatRuntimeSettings? _settings;
    private bool _failoverEnabled = true;
    private int _maxRetries = 3;

    public RoundRobinBuilder(ChatRuntimeSettings? settings = null)
    {
        _settings = settings;
    }

    /// <summary>
    /// Adds a provider to the pool.
    /// </summary>
    public RoundRobinBuilder AddProvider(
        string name,
        ChatEndpointType endpointType,
        string? model = null,
        string? endpoint = null,
        string? apiKey = null,
        int weight = 1)
    {
        _configs.Add(new ProviderConfig(name, endpointType, endpoint, apiKey, model, weight));
        return this;
    }

    /// <summary>
    /// Adds Anthropic Claude to the pool.
    /// </summary>
    public RoundRobinBuilder AddAnthropic(string model = "claude-sonnet-4-20250514", string? apiKey = null)
        => AddProvider("Anthropic", ChatEndpointType.Anthropic, model, apiKey: apiKey);

    /// <summary>
    /// Adds OpenAI to the pool.
    /// </summary>
    public RoundRobinBuilder AddOpenAI(string model = "gpt-4o", string? apiKey = null)
        => AddProvider("OpenAI", ChatEndpointType.OpenAI, model, apiKey: apiKey);

    /// <summary>
    /// Adds DeepSeek to the pool.
    /// </summary>
    public RoundRobinBuilder AddDeepSeek(string model = "deepseek-chat", string? apiKey = null)
        => AddProvider("DeepSeek", ChatEndpointType.DeepSeek, model, apiKey: apiKey);

    /// <summary>
    /// Adds Groq to the pool.
    /// </summary>
    public RoundRobinBuilder AddGroq(string model = "llama-3.1-70b-versatile", string? apiKey = null)
        => AddProvider("Groq", ChatEndpointType.Groq, model, apiKey: apiKey);

    /// <summary>
    /// Adds local Ollama to the pool.
    /// </summary>
    public RoundRobinBuilder AddOllama(string model = "llama3.2", string endpoint = "http://localhost:11434")
        => AddProvider("Ollama", ChatEndpointType.OllamaLocal, model, endpoint);

    /// <summary>
    /// Adds Google Gemini to the pool.
    /// </summary>
    public RoundRobinBuilder AddGoogle(string model = "gemini-2.0-flash", string? apiKey = null)
        => AddProvider("Google", ChatEndpointType.Google, model, apiKey: apiKey);

    /// <summary>
    /// Adds Mistral AI to the pool.
    /// </summary>
    public RoundRobinBuilder AddMistral(string model = "mistral-large", string? apiKey = null)
        => AddProvider("Mistral", ChatEndpointType.Mistral, model, apiKey: apiKey);

    /// <summary>
    /// Enables or disables automatic failover.
    /// </summary>
    public RoundRobinBuilder WithFailover(bool enabled = true)
    {
        _failoverEnabled = enabled;
        return this;
    }

    /// <summary>
    /// Sets the maximum number of retry attempts.
    /// </summary>
    public RoundRobinBuilder WithMaxRetries(int retries)
    {
        _maxRetries = retries;
        return this;
    }

    /// <summary>
    /// Builds the RoundRobinChatModel.
    /// </summary>
    public RoundRobinChatModel Build()
    {
        var model = new RoundRobinChatModel(_failoverEnabled, _maxRetries);

        foreach (var config in _configs)
        {
            model.AddProvider(config, _settings);
        }

        return model;
    }
}
