#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using System.Net.Http.Json;
using System.Reactive.Linq;
using Polly;
using Polly.Retry;

namespace LangChainPipeline.Providers;

/// <summary>
/// HTTP client for GitHub Models API that provides access to various AI models
/// (GPT-4o, o1-preview, Claude 3.5 Sonnet, Llama 3.1, Mistral, etc.) through
/// an OpenAI-compatible API using GitHub Personal Access Token authentication.
/// Includes Polly exponential backoff retry policy to handle rate limiting.
/// </summary>
public sealed class GitHubModelsChatModel : IStreamingChatModel
{
    private readonly HttpClient _client;
    private readonly string _model;
    private readonly ChatRuntimeSettings _settings;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
    private const string DefaultEndpoint = "https://models.inference.ai.azure.com";

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubModelsChatModel"/> class.
    /// </summary>
    /// <param name="githubToken">GitHub Personal Access Token for authentication.</param>
    /// <param name="model">Model name (e.g., gpt-4o, o1-preview, claude-3-5-sonnet, llama-3.1-70b-instruct, mistral-large).</param>
    /// <param name="endpoint">Optional endpoint URL (defaults to https://models.inference.ai.azure.com).</param>
    /// <param name="settings">Optional runtime settings for temperature, max tokens, etc.</param>
    public GitHubModelsChatModel(string githubToken, string model, string? endpoint = null, ChatRuntimeSettings? settings = null)
    {
        if (string.IsNullOrWhiteSpace(githubToken)) 
            throw new ArgumentException("GitHub token is required", nameof(githubToken));
        if (string.IsNullOrWhiteSpace(model)) 
            throw new ArgumentException("Model name is required", nameof(model));

        string resolvedEndpoint = !string.IsNullOrWhiteSpace(endpoint) 
            ? endpoint.TrimEnd('/') 
            : Environment.GetEnvironmentVariable("GITHUB_MODELS_ENDPOINT")?.TrimEnd('/') ?? DefaultEndpoint;

        _client = new HttpClient
        {
            BaseAddress = new Uri(resolvedEndpoint, UriKind.Absolute)
        };
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", githubToken);
        _model = model;
        _settings = settings ?? new ChatRuntimeSettings();
        
        // Create Polly retry policy with exponential backoff for rate limiting (429) and server errors (5xx)
        _retryPolicy = Policy
            .HandleResult<HttpResponseMessage>(r => 
                (int)r.StatusCode == 429 || // Too Many Requests
                (int)r.StatusCode >= 500)   // Server errors
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    // Log retry attempts - using Console for consistency with other providers in this codebase
                    // TODO: Consider migrating to ILogger across all providers
                    Console.WriteLine($"[GitHubModelsChatModel] Retry {retryCount} after {timespan.TotalSeconds}s due to {outcome.Result?.StatusCode}");
                });
    }

    /// <summary>
    /// Factory method to create a GitHubModelsChatModel from environment variables.
    /// Uses GITHUB_TOKEN or GITHUB_MODELS_TOKEN for authentication.
    /// </summary>
    /// <param name="model">Model name.</param>
    /// <param name="settings">Optional runtime settings.</param>
    /// <returns>A configured GitHubModelsChatModel instance.</returns>
    public static GitHubModelsChatModel FromEnvironment(string model, ChatRuntimeSettings? settings = null)
    {
        string? token = Environment.GetEnvironmentVariable("GITHUB_TOKEN") 
                       ?? Environment.GetEnvironmentVariable("GITHUB_MODELS_TOKEN");
        
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                "GitHub token not found. Set GITHUB_TOKEN or GITHUB_MODELS_TOKEN environment variable.");
        }

        string? endpoint = Environment.GetEnvironmentVariable("GITHUB_MODELS_ENDPOINT");
        return new GitHubModelsChatModel(token, model, endpoint, settings);
    }

    /// <inheritdoc/>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        try
        {
            // Use OpenAI-compatible chat completions format (GitHub Models API)
            using JsonContent payload = JsonContent.Create(new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                },
                temperature = _settings.Temperature,
                max_tokens = _settings.MaxTokens > 0 ? _settings.MaxTokens : (int?)null
            });

            HttpResponseMessage response = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _client.PostAsync("/v1/chat/completions", payload, ct).ConfigureAwait(false);
            }).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            string jsonString = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(jsonString);

            // Extract content from OpenAI response format: choices[0].message.content or reasoning_content
            if (doc.RootElement.TryGetProperty("choices", out System.Text.Json.JsonElement choicesElement) &&
                choicesElement.ValueKind == System.Text.Json.JsonValueKind.Array &&
                choicesElement.GetArrayLength() > 0)
            {
                System.Text.Json.JsonElement firstChoice = choicesElement[0];
                if (firstChoice.TryGetProperty("message", out System.Text.Json.JsonElement messageElement))
                {
                    // Try standard content field first
                    if (messageElement.TryGetProperty("content", out System.Text.Json.JsonElement contentElement) &&
                        contentElement.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        string? content = contentElement.GetString();
                        if (!string.IsNullOrEmpty(content))
                        {
                            return content;
                        }
                    }

                    // Fall back to reasoning_content (for o1-preview and similar models)
                    if (messageElement.TryGetProperty("reasoning_content", out System.Text.Json.JsonElement reasoningElement))
                    {
                        return reasoningElement.GetString() ?? string.Empty;
                    }
                }
            }
        }
        catch (HttpRequestException httpEx)
        {
            // Network errors - log for debugging but provide fallback
            Console.WriteLine($"[GitHubModelsChatModel] HTTP error: {httpEx.Message}");
        }
        catch (TaskCanceledException)
        {
            // Timeout - log for debugging but provide fallback
            Console.WriteLine("[GitHubModelsChatModel] Request timed out");
        }
        catch (Exception ex)
        {
            // Unexpected errors - log for debugging but provide fallback
            Console.WriteLine($"[GitHubModelsChatModel] Unexpected error: {ex.GetType().Name} - {ex.Message}");
        }

        return $"[github-models-fallback:{_model}] {prompt}";
    }

    /// <inheritdoc/>
    public IObservable<string> StreamReasoningContent(string prompt, CancellationToken ct = default)
    {
        return Observable.Create<string>(async (observer, token) =>
        {
            try
            {
                using JsonContent payload = JsonContent.Create(new
                {
                    model = _model,
                    messages = new[]
                    {
                        new { role = "user", content = prompt }
                    },
                    temperature = _settings.Temperature,
                    max_tokens = _settings.MaxTokens > 0 ? _settings.MaxTokens : (int?)null,
                    stream = true
                });

                HttpResponseMessage response = await _retryPolicy.ExecuteAsync(async () =>
                {
                    return await _client.PostAsync("/v1/chat/completions", payload, token).ConfigureAwait(false);
                }).ConfigureAwait(false);
                
                response.EnsureSuccessStatusCode();

                using Stream responseStream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                using StreamReader reader = new StreamReader(responseStream);

                while (!reader.EndOfStream && !token.IsCancellationRequested)
                {
                    string? line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;

                    string jsonData = line.Substring(6).Trim();
                    if (jsonData == "[DONE]")
                    {
                        observer.OnCompleted();
                        return;
                    }

                    try
                    {
                        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(jsonData);
                        if (doc.RootElement.TryGetProperty("choices", out System.Text.Json.JsonElement choicesElement) &&
                            choicesElement.ValueKind == System.Text.Json.JsonValueKind.Array &&
                            choicesElement.GetArrayLength() > 0)
                        {
                            System.Text.Json.JsonElement delta = choicesElement[0];
                            if (delta.TryGetProperty("delta", out System.Text.Json.JsonElement deltaElement))
                            {
                                // Try content field
                                if (deltaElement.TryGetProperty("content", out System.Text.Json.JsonElement contentElement))
                                {
                                    string? content = contentElement.GetString();
                                    if (!string.IsNullOrEmpty(content))
                                    {
                                        observer.OnNext(content);
                                    }
                                }
                                // Try reasoning_content field for o1-preview and similar models
                                else if (deltaElement.TryGetProperty("reasoning_content", out System.Text.Json.JsonElement reasoningElement))
                                {
                                    string? reasoning = reasoningElement.GetString();
                                    if (!string.IsNullOrEmpty(reasoning))
                                    {
                                        observer.OnNext(reasoning);
                                    }
                                }
                            }
                        }
                    }
                    catch (System.Text.Json.JsonException)
                    {
                        // Skip malformed JSON chunks
                        continue;
                    }
                }

                observer.OnCompleted();
            }
            catch (Exception ex)
            {
                observer.OnError(ex);
            }
        });
    }

    /// <summary>
    /// Disposes the HTTP client.
    /// </summary>
    public void Dispose()
    {
        _client?.Dispose();
    }
}
