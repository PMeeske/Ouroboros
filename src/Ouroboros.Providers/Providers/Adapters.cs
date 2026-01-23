#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using System.Net.Http.Json;
using System.Text;
using LangChain.Providers.Ollama;
using Polly;
using Polly.Retry;
using System.Reactive.Linq;
using System.Text.RegularExpressions;

namespace Ouroboros.Providers;

/// <summary>
/// Represents a response from a model that includes both thinking/reasoning content and the final response.
/// Used by models that support extended thinking (Claude, DeepSeek R1, o1, etc.).
/// </summary>
/// <param name="Thinking">The thinking/reasoning content, if any. This is the model's internal reasoning process.</param>
/// <param name="Content">The final response content after thinking.</param>
/// <param name="ThinkingTokens">Estimated token count for thinking content (if available from API).</param>
/// <param name="ContentTokens">Estimated token count for content (if available from API).</param>
public sealed record ThinkingResponse(
    string? Thinking,
    string Content,
    int? ThinkingTokens = null,
    int? ContentTokens = null)
{
    /// <summary>
    /// Returns true if this response contains thinking content.
    /// </summary>
    public bool HasThinking => !string.IsNullOrEmpty(Thinking);

    /// <summary>
    /// Combines thinking and content into a single formatted string.
    /// </summary>
    /// <param name="thinkingPrefix">Prefix for thinking section (default: "🤔 Thinking:\n").</param>
    /// <param name="contentPrefix">Prefix for content section (default: "\n\n📝 Response:\n").</param>
    public string ToFormattedString(string thinkingPrefix = "🤔 Thinking:\n", string contentPrefix = "\n\n📝 Response:\n")
    {
        if (!HasThinking)
            return Content;

        return $"{thinkingPrefix}{Thinking}{contentPrefix}{Content}";
    }

    /// <summary>
    /// Creates a ThinkingResponse from raw text that may contain thinking tags.
    /// Supports &lt;think&gt;...&lt;/think&gt; format used by some models.
    /// </summary>
    public static ThinkingResponse FromRawText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new ThinkingResponse(null, text ?? string.Empty);

        // Try to extract <think>...</think> tags (used by DeepSeek R1, etc.)
        var thinkMatch = Regex.Match(text, @"<think>(.*?)</think>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (thinkMatch.Success)
        {
            string thinking = thinkMatch.Groups[1].Value.Trim();
            string content = text.Replace(thinkMatch.Value, "").Trim();
            return new ThinkingResponse(thinking, content);
        }

        // Try <thinking>...</thinking> format
        var thinkingMatch = Regex.Match(text, @"<thinking>(.*?)</thinking>", RegexOptions.Singleline | RegexOptions.IgnoreCase);
        if (thinkingMatch.Success)
        {
            string thinking = thinkingMatch.Groups[1].Value.Trim();
            string content = text.Replace(thinkingMatch.Value, "").Trim();
            return new ThinkingResponse(thinking, content);
        }

        // No thinking tags found
        return new ThinkingResponse(null, text);
    }
}

/// <summary>
/// Minimal contract used by <see cref="ToolAwareChatModel"/> to obtain text responses.
/// </summary>
public interface IChatCompletionModel
{
    Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default);
}

/// <summary>
/// Extended contract for models that support streaming responses.
/// </summary>
public interface IStreamingChatModel : IChatCompletionModel
{
    IObservable<string> StreamReasoningContent(string prompt, CancellationToken ct = default);
}

/// <summary>
/// Extended contract for models that support thinking/reasoning mode.
/// These models can return separate thinking content and response content.
/// Examples include Claude (with extended thinking), DeepSeek R1, and o1 models.
/// </summary>
public interface IThinkingChatModel : IChatCompletionModel
{
    /// <summary>
    /// Generates a response with separate thinking and content.
    /// </summary>
    /// <param name="prompt">The input prompt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A ThinkingResponse containing both thinking and content.</returns>
    Task<ThinkingResponse> GenerateWithThinkingAsync(string prompt, CancellationToken ct = default);
}

/// <summary>
/// Extended contract for models that support streaming thinking/reasoning mode.
/// </summary>
public interface IStreamingThinkingChatModel : IThinkingChatModel, IStreamingChatModel
{
    /// <summary>
    /// Streams the thinking and content separately.
    /// </summary>
    /// <param name="prompt">The input prompt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An observable that emits (isThinking, chunk) tuples.</returns>
    IObservable<(bool IsThinking, string Chunk)> StreamWithThinkingAsync(string prompt, CancellationToken ct = default);
}

/// <summary>
/// Adapter for local Ollama models. We attempt to call the SDK when available,
/// falling back to a deterministic stub when the local daemon is not reachable.
/// Supports thinking mode for models like DeepSeek R1 that emit &lt;think&gt; tags.
/// </summary>
public sealed class OllamaChatAdapter : IStreamingThinkingChatModel
{
    private readonly OllamaChatModel _model;
    private readonly string? _culture;

    public OllamaChatAdapter(OllamaChatModel model, string? culture = null)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
        _culture = culture;
    }

    /// <inheritdoc/>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        try
        {
            string finalPrompt = _culture is { Length: > 0 } c ? $"Please answer in {c}. {prompt}" : prompt;
            IAsyncEnumerable<LangChain.Providers.ChatResponse> stream = _model.GenerateAsync(finalPrompt, cancellationToken: ct);
            StringBuilder builder = new StringBuilder();

            await foreach (LangChain.Providers.ChatResponse? chunk in stream.WithCancellation(ct).ConfigureAwait(false))
            {
                string text = ExtractResponseText(chunk);
                if (!string.IsNullOrEmpty(text))
                {
                    builder.Append(text);
                }
            }

            if (builder.Length > 0)
            {
                return builder.ToString();
            }

            return ExtractResponseText(null);
        }
        catch
        {
            // Deterministic fallback keeps the pipeline running in offline scenarios.
            return $"[ollama-fallback:{_model.GetType().Name}] {prompt}";
        }
    }

    /// <inheritdoc/>
    public async Task<ThinkingResponse> GenerateWithThinkingAsync(string prompt, CancellationToken ct = default)
    {
        string rawText = await GenerateTextAsync(prompt, ct);
        return ThinkingResponse.FromRawText(rawText);
    }

    /// <inheritdoc/>
    public IObservable<(bool IsThinking, string Chunk)> StreamWithThinkingAsync(string prompt, CancellationToken ct = default)
    {
        return Observable.Create<(bool IsThinking, string Chunk)>(async (observer, token) =>
        {
            try
            {
                string finalPrompt = _culture is { Length: > 0 } c ? $"Please answer in {c}. {prompt}" : prompt;
                IAsyncEnumerable<LangChain.Providers.ChatResponse> stream = _model.GenerateAsync(finalPrompt, cancellationToken: token);

                bool inThinking = false;
                StringBuilder buffer = new();

                await foreach (LangChain.Providers.ChatResponse? chunk in stream.WithCancellation(token).ConfigureAwait(false))
                {
                    string text = ExtractResponseText(chunk);
                    if (string.IsNullOrEmpty(text)) continue;

                    buffer.Append(text);
                    string bufferStr = buffer.ToString();

                    // Check for thinking tag transitions
                    if (!inThinking && bufferStr.Contains("<think>", StringComparison.OrdinalIgnoreCase))
                    {
                        int idx = bufferStr.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
                        string beforeTag = bufferStr[..idx];
                        if (!string.IsNullOrEmpty(beforeTag))
                            observer.OnNext((false, beforeTag));

                        buffer.Clear();
                        buffer.Append(bufferStr[(idx + 7)..]);
                        inThinking = true;
                        continue;
                    }

                    if (inThinking && bufferStr.Contains("</think>", StringComparison.OrdinalIgnoreCase))
                    {
                        int idx = bufferStr.IndexOf("</think>", StringComparison.OrdinalIgnoreCase);
                        string thinkingContent = bufferStr[..idx];
                        if (!string.IsNullOrEmpty(thinkingContent))
                            observer.OnNext((true, thinkingContent));

                        buffer.Clear();
                        buffer.Append(bufferStr[(idx + 8)..]);
                        inThinking = false;
                        continue;
                    }

                    // Emit chunk with current state
                    observer.OnNext((inThinking, text));
                    buffer.Clear();
                }

                // Flush any remaining buffer
                if (buffer.Length > 0)
                    observer.OnNext((inThinking, buffer.ToString()));

                observer.OnCompleted();
            }
            catch (Exception ex)
            {
                observer.OnError(ex);
            }
        });
    }

    /// <inheritdoc/>
    public IObservable<string> StreamReasoningContent(string prompt, CancellationToken ct = default)
    {
        return Observable.Create<string>(async (observer, token) =>
        {
            try
            {
                string finalPrompt = _culture is { Length: > 0 } c ? $"Please answer in {c}. {prompt}" : prompt;
                IAsyncEnumerable<LangChain.Providers.ChatResponse> stream = _model.GenerateAsync(finalPrompt, cancellationToken: token);
                await foreach (LangChain.Providers.ChatResponse? chunk in stream.WithCancellation(token).ConfigureAwait(false))
                {
                    string text = ExtractResponseText(chunk);
                    if (!string.IsNullOrEmpty(text))
                    {
                        observer.OnNext(text);
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

    private static string ExtractResponseText(object? response)
    {
        if (response is null) return string.Empty;

        switch (response)
        {
            case string s:
                return s;
            case IEnumerable<string> strings:
                return string.Join(Environment.NewLine, strings);
        }

        Type type = response.GetType();

        System.Reflection.PropertyInfo? lastMessageProperty = type.GetProperty("LastMessageContent");
        if (lastMessageProperty?.GetValue(response) is string last)
        {
            return last;
        }

        System.Reflection.PropertyInfo? contentProperty = type.GetProperty("Content");
        if (contentProperty?.GetValue(response) is string content)
        {
            return content;
        }

        System.Reflection.PropertyInfo? messageProperty = type.GetProperty("Message");
        if (messageProperty?.GetValue(response) is { } message)
        {
            if (message is string mString)
            {
                return mString;
            }

            if (message is IEnumerable<string> enumerable)
            {
                return string.Join(Environment.NewLine, enumerable);
            }

            string? nestedContent = message.GetType().GetProperty("Content")?.GetValue(message) as string;
            if (!string.IsNullOrWhiteSpace(nestedContent))
            {
                return nestedContent!;
            }
        }

        return response.ToString() ?? string.Empty;
    }
}

/// <summary>
/// Shallow HTTP client that mimics an OpenAI-compatible JSON API. We intentionally
/// keep it permissive – if the call fails we simply echo the prompt with context.
/// Uses Polly for exponential backoff retry policy to handle rate limiting.
/// </summary>
public sealed class HttpOpenAiCompatibleChatModel : IChatCompletionModel
{
    private readonly HttpClient _client;
    private readonly string _model;
    private readonly ChatRuntimeSettings _settings;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

    public HttpOpenAiCompatibleChatModel(string endpoint, string apiKey, string model, ChatRuntimeSettings? settings = null)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) throw new ArgumentException("Endpoint is required", nameof(endpoint));
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("API key is required", nameof(apiKey));

        _client = new HttpClient
        {
            BaseAddress = new Uri(endpoint, UriKind.Absolute)
        };
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
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
                    Console.WriteLine($"[HttpOpenAiCompatibleChatModel] Retry {retryCount} after {timespan.TotalSeconds}s due to {outcome.Result?.StatusCode}");
                });
    }

    /// <inheritdoc/>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        try
        {
            using JsonContent payload = JsonContent.Create(new
            {
                model = _model,
                temperature = _settings.Temperature,
                max_output_tokens = _settings.MaxTokens,
                input = _settings.Culture is { Length: > 0 } c ? $"Please answer in {c}. {prompt}" : prompt
            });

            HttpResponseMessage response = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _client.PostAsync("/v1/responses", payload, ct).ConfigureAwait(false);
            }).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();
            Dictionary<string, object?>? json = await response.Content.ReadFromJsonAsync<Dictionary<string, object?>>(cancellationToken: ct).ConfigureAwait(false);
            if (json is not null && json.TryGetValue("output_text", out object? text) && text is string s)
            {
                return s;
            }
        }
        catch
        {
            // Remote backend not reachable → fall back to indicating failure.
        }
        return $"[remote-fallback:{_model}] {prompt}";
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}

/// <summary>
/// HTTP client specifically designed for Ollama Cloud API endpoints.
/// Uses Ollama's native JSON API format with /api/generate endpoint.
/// Includes Polly exponential backoff retry policy to handle rate limiting.
/// Supports thinking mode for models like DeepSeek R1 that emit &lt;think&gt; tags.
/// </summary>
public sealed class OllamaCloudChatModel : IStreamingThinkingChatModel
{
    private readonly HttpClient _client;
    private readonly string _model;
    private readonly ChatRuntimeSettings _settings;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

    public OllamaCloudChatModel(string endpoint, string apiKey, string model, ChatRuntimeSettings? settings = null)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) throw new ArgumentException("Endpoint is required", nameof(endpoint));
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("API key is required", nameof(apiKey));

        _client = new HttpClient
        {
            BaseAddress = new Uri(endpoint.TrimEnd('/'), UriKind.Absolute)
        };
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
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
                    Console.WriteLine($"[OllamaCloudChatModel] Retry {retryCount} after {timespan.TotalSeconds}s due to {outcome.Result?.StatusCode}");
                });
    }

    /// <inheritdoc/>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        try
        {
            // Use Ollama's native /api/generate endpoint and JSON format
            var payloadObject = new
            {
                model = _model,
                prompt = _settings.Culture is { Length: > 0 } c ? $"Please answer in {c}. {prompt}" : prompt,
                stream = false,
                options = new
                {
                    temperature = _settings.Temperature,
                    num_predict = _settings.MaxTokens > 0 ? _settings.MaxTokens : (int?)null
                }
            };

            HttpResponseMessage response = await _retryPolicy.ExecuteAsync(async () =>
            {
                // Create fresh JsonContent for each retry attempt (content can only be used once)
                using JsonContent payload = JsonContent.Create(payloadObject);
                return await _client.PostAsync("/api/generate", payload, ct).ConfigureAwait(false);
            }).ConfigureAwait(false);

            string rawContent = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[OllamaCloudChatModel] HTTP {(int)response.StatusCode}: {rawContent}");
                return $"[ollama-cloud-fallback:{_model}] {prompt}";
            }

            using var doc = System.Text.Json.JsonDocument.Parse(rawContent);
            if (doc.RootElement.TryGetProperty("response", out var responseElement))
            {
                return responseElement.GetString() ?? "";
            }

            Console.WriteLine($"[OllamaCloudChatModel] No 'response' field in: {rawContent}");
        }
        catch (Exception ex)
        {
            // Log the actual error for debugging
            Console.WriteLine($"[OllamaCloudChatModel] Error: {ex.GetType().Name}: {ex.Message}");
        }
        return $"[ollama-cloud-fallback:{_model}] {prompt}";
    }

    /// <inheritdoc/>
    public async Task<ThinkingResponse> GenerateWithThinkingAsync(string prompt, CancellationToken ct = default)
    {
        string rawText = await GenerateTextAsync(prompt, ct);
        return ThinkingResponse.FromRawText(rawText);
    }

    /// <inheritdoc/>
    public IObservable<(bool IsThinking, string Chunk)> StreamWithThinkingAsync(string prompt, CancellationToken ct = default)
    {
        return Observable.Create<(bool IsThinking, string Chunk)>(async (observer, token) =>
        {
            try
            {
                using JsonContent payload = JsonContent.Create(new
                {
                    model = _model,
                    prompt = _settings.Culture is { Length: > 0 } c ? $"Please answer in {c}. {prompt}" : prompt,
                    stream = true,
                    options = new
                    {
                        temperature = _settings.Temperature,
                        num_predict = _settings.MaxTokens > 0 ? _settings.MaxTokens : (int?)null
                    }
                });

                HttpResponseMessage response = await _retryPolicy.ExecuteAsync(async () =>
                {
                    return await _client.PostAsync("/api/generate", payload, token).ConfigureAwait(false);
                }).ConfigureAwait(false);

                response.EnsureSuccessStatusCode();

                using Stream responseStream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                using StreamReader reader = new StreamReader(responseStream);

                bool inThinking = false;
                StringBuilder buffer = new();

                while (!reader.EndOfStream && !token.IsCancellationRequested)
                {
                    string? line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    try
                    {
                        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(line);
                        if (doc.RootElement.TryGetProperty("response", out System.Text.Json.JsonElement responseElement))
                        {
                            string? content = responseElement.GetString();
                            if (!string.IsNullOrEmpty(content))
                            {
                                buffer.Append(content);
                                string bufferStr = buffer.ToString();

                                // Check for thinking tag transitions
                                if (!inThinking && bufferStr.Contains("<think>", StringComparison.OrdinalIgnoreCase))
                                {
                                    int idx = bufferStr.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
                                    string beforeTag = bufferStr[..idx];
                                    if (!string.IsNullOrEmpty(beforeTag))
                                        observer.OnNext((false, beforeTag));

                                    buffer.Clear();
                                    buffer.Append(bufferStr[(idx + 7)..]);
                                    inThinking = true;
                                    continue;
                                }

                                if (inThinking && bufferStr.Contains("</think>", StringComparison.OrdinalIgnoreCase))
                                {
                                    int idx = bufferStr.IndexOf("</think>", StringComparison.OrdinalIgnoreCase);
                                    string thinkingContent = bufferStr[..idx];
                                    if (!string.IsNullOrEmpty(thinkingContent))
                                        observer.OnNext((true, thinkingContent));

                                    buffer.Clear();
                                    buffer.Append(bufferStr[(idx + 8)..]);
                                    inThinking = false;
                                    continue;
                                }

                                // Emit chunk with current state
                                observer.OnNext((inThinking, content));
                                buffer.Clear();
                            }
                        }

                        if (doc.RootElement.TryGetProperty("done", out System.Text.Json.JsonElement doneElement) && doneElement.GetBoolean())
                        {
                            // Flush any remaining buffer
                            if (buffer.Length > 0)
                                observer.OnNext((inThinking, buffer.ToString()));
                            observer.OnCompleted();
                            return;
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
                    prompt = _settings.Culture is { Length: > 0 } c ? $"Please answer in {c}. {prompt}" : prompt,
                    stream = true,
                    options = new
                    {
                        temperature = _settings.Temperature,
                        num_predict = _settings.MaxTokens > 0 ? _settings.MaxTokens : (int?)null
                    }
                });

                HttpResponseMessage response = await _retryPolicy.ExecuteAsync(async () =>
                {
                    return await _client.PostAsync("/api/generate", payload, token).ConfigureAwait(false);
                }).ConfigureAwait(false);

                response.EnsureSuccessStatusCode();

                using Stream responseStream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false);
                using StreamReader reader = new StreamReader(responseStream);

                while (!reader.EndOfStream && !token.IsCancellationRequested)
                {
                    string? line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    try
                    {
                        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(line);
                        if (doc.RootElement.TryGetProperty("response", out System.Text.Json.JsonElement responseElement))
                        {
                            string? content = responseElement.GetString();
                            if (!string.IsNullOrEmpty(content))
                            {
                                observer.OnNext(content);
                            }
                        }

                        if (doc.RootElement.TryGetProperty("done", out System.Text.Json.JsonElement doneElement) && doneElement.GetBoolean())
                        {
                            observer.OnCompleted();
                            return;
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

    public void Dispose()
    {
        _client?.Dispose();
    }
}

/// <summary>
/// Base class for OpenAI-compatible chat models that use /v1/chat/completions endpoint.
/// Provides shared implementation for request/response handling and streaming.
/// Supports thinking mode via reasoning_content field (DeepSeek, o1, etc.) and thinking tags.
/// </summary>
public abstract class OpenAiCompatibleChatModelBase : IStreamingThinkingChatModel
{
    private readonly HttpClient _client;
    private readonly string _model;
    private readonly ChatRuntimeSettings _settings;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
    private readonly string _providerName;

    /// <summary>Gets the model name.</summary>
    protected string ModelName => _model;

    /// <summary>Gets the settings.</summary>
    protected ChatRuntimeSettings Settings => _settings;

    protected OpenAiCompatibleChatModelBase(string endpoint, string apiKey, string model, string providerName, ChatRuntimeSettings? settings = null)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) throw new ArgumentException("Endpoint is required", nameof(endpoint));
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("API key is required", nameof(apiKey));

        _client = new HttpClient
        {
            BaseAddress = new Uri(endpoint.TrimEnd('/'), UriKind.Absolute)
        };
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        _model = model;
        _settings = settings ?? new ChatRuntimeSettings();
        _providerName = providerName;

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
                    Console.WriteLine($"[{_providerName}] Retry {retryCount} after {timespan.TotalSeconds}s due to {outcome.Result?.StatusCode}");
                });
    }

    /// <inheritdoc/>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        var response = await GenerateWithThinkingAsync(prompt, ct);
        // Return combined thinking + content for backward compatibility if thinking is present
        return response.HasThinking ? response.ToFormattedString() : response.Content;
    }

    /// <inheritdoc/>
    public async Task<ThinkingResponse> GenerateWithThinkingAsync(string prompt, CancellationToken ct = default)
    {
        try
        {
            // Use OpenAI-compatible chat completions format
            using JsonContent payload = JsonContent.Create(new
            {
                model = _model,
                messages = new[]
                {
                    new { role = "user", content = _settings.Culture is { Length: > 0 } c ? $"Please answer in {c}. {prompt}" : prompt }
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

            string? reasoningContent = null;
            string? content = null;
            int? reasoningTokens = null;
            int? contentTokens = null;

            // Try to extract usage info
            if (doc.RootElement.TryGetProperty("usage", out var usageElement))
            {
                if (usageElement.TryGetProperty("reasoning_tokens", out var rtElement))
                    reasoningTokens = rtElement.GetInt32();
                if (usageElement.TryGetProperty("completion_tokens", out var ctElement))
                    contentTokens = ctElement.GetInt32();
            }

            // Extract content from OpenAI response format: choices[0].message
            if (doc.RootElement.TryGetProperty("choices", out System.Text.Json.JsonElement choicesElement) &&
                choicesElement.ValueKind == System.Text.Json.JsonValueKind.Array &&
                choicesElement.GetArrayLength() > 0)
            {
                System.Text.Json.JsonElement firstChoice = choicesElement[0];
                if (firstChoice.TryGetProperty("message", out System.Text.Json.JsonElement messageElement))
                {
                    // Extract reasoning_content if present (DeepSeek, o1, etc.)
                    if (messageElement.TryGetProperty("reasoning_content", out System.Text.Json.JsonElement reasoningElement) &&
                        reasoningElement.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        reasoningContent = reasoningElement.GetString();
                    }

                    // Extract standard content
                    if (messageElement.TryGetProperty("content", out System.Text.Json.JsonElement contentElement) &&
                        contentElement.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        content = contentElement.GetString();
                    }
                }
            }

            // If we have explicit reasoning_content, return structured response
            if (!string.IsNullOrEmpty(reasoningContent))
            {
                return new ThinkingResponse(reasoningContent, content ?? string.Empty, reasoningTokens, contentTokens);
            }

            // Otherwise, try to parse thinking tags from content
            if (!string.IsNullOrEmpty(content))
            {
                var parsed = ThinkingResponse.FromRawText(content);
                return parsed with { ThinkingTokens = reasoningTokens, ContentTokens = contentTokens };
            }

            return new ThinkingResponse(null, string.Empty);
        }
        catch
        {
            // Remote endpoint not reachable → fall back to indicating failure.
        }

        return new ThinkingResponse(null, GetFallbackMessage(prompt));
    }

    /// <inheritdoc/>
    public IObservable<(bool IsThinking, string Chunk)> StreamWithThinkingAsync(string prompt, CancellationToken ct = default)
    {
        return Observable.Create<(bool IsThinking, string Chunk)>(async (observer, token) =>
        {
            try
            {
                using JsonContent payload = JsonContent.Create(new
                {
                    model = _model,
                    messages = new[]
                    {
                        new { role = "user", content = _settings.Culture is { Length: > 0 } c ? $"Please answer in {c}. {prompt}" : prompt }
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

                bool inThinkingFromTag = false;
                StringBuilder tagBuffer = new();

                while (!reader.EndOfStream && !token.IsCancellationRequested)
                {
                    string? line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data: ")) continue;

                    string jsonData = line.Substring(6).Trim();
                    if (jsonData == "[DONE]")
                    {
                        // Flush any remaining buffer
                        if (tagBuffer.Length > 0)
                            observer.OnNext((inThinkingFromTag, tagBuffer.ToString()));
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
                                // Check for reasoning_content (structured thinking from API)
                                if (deltaElement.TryGetProperty("reasoning_content", out System.Text.Json.JsonElement reasoningElement))
                                {
                                    string? reasoning = reasoningElement.GetString();
                                    if (!string.IsNullOrEmpty(reasoning))
                                    {
                                        observer.OnNext((true, reasoning));
                                    }
                                    continue;
                                }

                                // Check for content
                                if (deltaElement.TryGetProperty("content", out System.Text.Json.JsonElement contentElement))
                                {
                                    string? content = contentElement.GetString();
                                    if (!string.IsNullOrEmpty(content))
                                    {
                                        // Check for thinking tags in content
                                        tagBuffer.Append(content);
                                        string bufferStr = tagBuffer.ToString();

                                        if (!inThinkingFromTag && bufferStr.Contains("<think>", StringComparison.OrdinalIgnoreCase))
                                        {
                                            int idx = bufferStr.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
                                            string beforeTag = bufferStr[..idx];
                                            if (!string.IsNullOrEmpty(beforeTag))
                                                observer.OnNext((false, beforeTag));

                                            tagBuffer.Clear();
                                            tagBuffer.Append(bufferStr[(idx + 7)..]);
                                            inThinkingFromTag = true;
                                            continue;
                                        }

                                        if (inThinkingFromTag && bufferStr.Contains("</think>", StringComparison.OrdinalIgnoreCase))
                                        {
                                            int idx = bufferStr.IndexOf("</think>", StringComparison.OrdinalIgnoreCase);
                                            string thinkingContent = bufferStr[..idx];
                                            if (!string.IsNullOrEmpty(thinkingContent))
                                                observer.OnNext((true, thinkingContent));

                                            tagBuffer.Clear();
                                            tagBuffer.Append(bufferStr[(idx + 8)..]);
                                            inThinkingFromTag = false;
                                            continue;
                                        }

                                        // Emit with current state
                                        observer.OnNext((inThinkingFromTag, content));
                                        tagBuffer.Clear();
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

    /// <inheritdoc/>
    public System.IObservable<string> StreamReasoningContent(string prompt, CancellationToken ct = default)
    {
        // Flatten the thinking stream to just emit all chunks (both thinking and content)
        return StreamWithThinkingAsync(prompt, ct).Select(tuple => tuple.Chunk);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _client?.Dispose();
    }

    /// <summary>
    /// Gets the fallback message to return when the API call fails.
    /// </summary>
    protected virtual string GetFallbackMessage(string prompt)
    {
        return $"[{_providerName.ToLowerInvariant()}-fallback:{_model}] {prompt}";
    }
}

/// <summary>
/// HTTP client for LiteLLM proxy endpoints that support OpenAI-compatible chat completions API.
/// Uses standard /v1/chat/completions endpoint with messages format.
/// Includes Polly exponential backoff retry policy to handle rate limiting.
/// </summary>
public sealed class LiteLLMChatModel : OpenAiCompatibleChatModelBase
{
    public LiteLLMChatModel(string endpoint, string apiKey, string model, ChatRuntimeSettings? settings = null)
        : base(endpoint, apiKey, model, "LiteLLMChatModel", settings)
    {
    }
}

/// <summary>
/// Naive ensemble that routes requests based on simple heuristics. Real routing
/// logic is outside the scope of the repair, but preserving the public surface
/// lets CLI switches keep working.
/// </summary>
public sealed class MultiModelRouter : IChatCompletionModel
{
    private readonly IReadOnlyDictionary<string, IChatCompletionModel> _models;
    private readonly string _fallbackKey;

    public MultiModelRouter(IReadOnlyDictionary<string, IChatCompletionModel> models, string fallbackKey)
    {
        if (models.Count == 0) throw new ArgumentException("At least one model is required", nameof(models));
        _models = models;
        _fallbackKey = fallbackKey;
    }

    /// <inheritdoc/>
    public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        IChatCompletionModel target = SelectModel(prompt);
        return target.GenerateTextAsync(prompt, ct);
    }

    private IChatCompletionModel SelectModel(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return _models[_fallbackKey];
        if (prompt.Contains("code", StringComparison.OrdinalIgnoreCase) && _models.TryGetValue("coder", out IChatCompletionModel? coder))
            return coder;
        if (prompt.Length > 600 && _models.TryGetValue("summarize", out IChatCompletionModel? summarize))
            return summarize;
        if (prompt.Contains("reason", StringComparison.OrdinalIgnoreCase) && _models.TryGetValue("reason", out IChatCompletionModel? reason))
            return reason;
        return _models.TryGetValue(_fallbackKey, out IChatCompletionModel? fallback) ? fallback : _models.Values.First();
    }
}

/// <summary>
/// Deterministic embedding generator that hashes the input string. It is not a
/// semantic encoder, but it provides stable vectors for testing and demos when
/// no real embedding service is available.
/// </summary>
public sealed class DeterministicEmbeddingModel : IEmbeddingModel
{
    /// <summary>
    /// Default vector dimension matching nomic-embed-text (768).
    /// </summary>
    public const int DefaultDimension = 768;

    private readonly int _dimension;

    /// <summary>
    /// Initializes a new instance with the default dimension (768).
    /// </summary>
    public DeterministicEmbeddingModel() : this(DefaultDimension) { }

    /// <summary>
    /// Initializes a new instance with a custom dimension.
    /// </summary>
    /// <param name="dimension">The vector dimension to generate.</param>
    public DeterministicEmbeddingModel(int dimension)
    {
        _dimension = dimension > 0 ? dimension : DefaultDimension;
    }

    /// <inheritdoc/>
    public Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
    {
        if (input is null) input = string.Empty;

        // Compress long inputs instead of truncating to preserve semantic information
        // This captures essence from entire text rather than just the beginning
        byte[] buffer;
        if (input.Length > 2000)
        {
            // Use compression: extract semantic fingerprint from entire text
            buffer = CompressTextForEmbedding(input);
        }
        else
        {
            buffer = System.Text.Encoding.UTF8.GetBytes(input);
        }

        byte[] hash = System.Security.Cryptography.SHA256.HashData(buffer);

        // Generate a vector of the target dimension by cycling through hash bytes
        float[] vector = new float[_dimension];
        for (int i = 0; i < _dimension; i++)
        {
            // Use hash bytes cyclically and add position-based variation
            byte hashByte = hash[i % hash.Length];
            float positionFactor = (float)Math.Sin(i * 0.1) * 0.1f;
            vector[i] = (hashByte / 255f) + positionFactor;
        }

        // Normalize the vector for better similarity comparisons
        float magnitude = 0f;
        for (int i = 0; i < _dimension; i++)
        {
            magnitude += vector[i] * vector[i];
        }
        magnitude = (float)Math.Sqrt(magnitude);
        if (magnitude > 0)
        {
            for (int i = 0; i < _dimension; i++)
            {
                vector[i] /= magnitude;
            }
        }

        return Task.FromResult(vector);
    }

    /// <summary>
    /// Compresses long text for embedding by extracting semantic fingerprint from entire content.
    /// Uses chunking and rolling hash to capture information from throughout the text.
    /// </summary>
    private static byte[] CompressTextForEmbedding(string input)
    {
        const int chunkSize = 200;  // Characters per chunk
        const int maxChunks = 20;   // Sample up to 20 chunks

        // Sample chunks from throughout the text
        int totalChunks = (input.Length + chunkSize - 1) / chunkSize;
        int stride = Math.Max(1, totalChunks / maxChunks);

        using var ms = new System.IO.MemoryStream();
        using var writer = new System.IO.BinaryWriter(ms);

        // Hash each sampled chunk and combine
        int chunksProcessed = 0;
        for (int i = 0; i < input.Length && chunksProcessed < maxChunks; i += chunkSize * stride)
        {
            int end = Math.Min(i + chunkSize, input.Length);
            string chunk = input[i..end];

            // Get hash of this chunk
            byte[] chunkBytes = System.Text.Encoding.UTF8.GetBytes(chunk);
            byte[] chunkHash = System.Security.Cryptography.MD5.HashData(chunkBytes);
            writer.Write(chunkHash);
            chunksProcessed++;
        }

        // Add length as additional semantic signal
        writer.Write(input.Length);

        // Add word count signature
        int wordCount = 0;
        bool inWord = false;
        foreach (char c in input)
        {
            if (char.IsLetterOrDigit(c))
            {
                if (!inWord) { wordCount++; inWord = true; }
            }
            else { inWord = false; }
        }
        writer.Write(wordCount);

        // Add character frequency signature (top 8 chars)
        var freqs = new Dictionary<char, int>();
        foreach (char c in input.Where(char.IsLetter).Select(char.ToLowerInvariant))
        {
            freqs[c] = freqs.GetValueOrDefault(c) + 1;
        }
        foreach (var (ch, count) in freqs.OrderByDescending(kv => kv.Value).Take(8))
        {
            writer.Write((byte)ch);
            writer.Write((ushort)Math.Min(count, ushort.MaxValue));
        }

        return ms.ToArray();
    }
}

/// <summary>
/// Adapter that wraps the Ollama embedding API when available. If the daemon
/// cannot be reached we fall back to deterministic embeddings.
/// </summary>
public sealed class OllamaEmbeddingAdapter : IEmbeddingModel
{
    private readonly OllamaEmbeddingModel _model;
    private readonly DeterministicEmbeddingModel _fallback = new();

    public OllamaEmbeddingAdapter(OllamaEmbeddingModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
    }

    /// <inheritdoc/>
    public async Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
    {
        string safeInput = SanitizeForEmbedding(input);

        try
        {
            LangChain.Providers.EmbeddingResponse response = await _model.CreateEmbeddingsAsync(safeInput, cancellationToken: ct).ConfigureAwait(false);
            if (TryExtractEmbedding(response, out float[]? vector))
            {
                return vector;
            }
        }
        catch
        {
            // LangChain encoding error - fall through to fallback
        }

        // Use deterministic fallback (hash-based embedding)
        return await _fallback.CreateEmbeddingsAsync(safeInput, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Sanitizes text for embedding by removing problematic characters.
    /// </summary>
    private static string SanitizeForEmbedding(string? text, int maxLength = 6000)
    {
        if (string.IsNullOrEmpty(text)) return "empty";

        // First pass: build clean string, skipping problematic characters
        var sb = new System.Text.StringBuilder(Math.Min(text.Length, maxLength));
        foreach (char c in text)
        {
            if (sb.Length >= maxLength) break;

            // Skip control characters (except newline/tab), surrogates, and null
            if (c == '\0') continue;
            if (char.IsSurrogate(c)) continue;
            if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t') continue;

            // Skip emoji and other high Unicode (above BMP can cause issues)
            if (c > 0xFFFF || (c >= 0xD800 && c <= 0xDFFF)) continue;

            // Skip common problematic ranges
            if (c >= 0x1F600 && c <= 0x1F64F) continue; // Emoticons
            if (c >= 0x1F300 && c <= 0x1F5FF) continue; // Misc symbols
            if (c >= 0x1F680 && c <= 0x1F6FF) continue; // Transport
            if (c >= 0x2600 && c <= 0x26FF) continue;   // Misc symbols
            if (c >= 0x2700 && c <= 0x27BF) continue;   // Dingbats

            sb.Append(c);
        }

        if (sb.Length == 0) return "empty";

        // Second pass: ensure valid UTF-8 round-trip
        try
        {
            var utf8 = System.Text.Encoding.UTF8;
            byte[] bytes = utf8.GetBytes(sb.ToString());
            // Limit byte size to prevent buffer overflow (4KB safe limit)
            if (bytes.Length > 4000)
            {
                // Truncate at byte level, then decode back
                bytes = bytes[..4000];
                // Find last valid UTF-8 sequence
                int lastValid = 4000;
                while (lastValid > 0 && (bytes[lastValid - 1] & 0xC0) == 0x80)
                    lastValid--;
                if (lastValid > 0 && lastValid < 4000)
                    bytes = bytes[..lastValid];
                return utf8.GetString(bytes);
            }
            return sb.ToString();
        }
        catch
        {
            // Ultimate fallback: ASCII only
            var ascii = new System.Text.StringBuilder();
            foreach (char c in sb.ToString())
            {
                if (c < 128 && ascii.Length < 2000)
                    ascii.Append(c);
            }
            return ascii.Length > 0 ? ascii.ToString() : "empty";
        }
    }

    private static bool TryExtractEmbedding(object? response, out float[] embedding)
    {
        embedding = Array.Empty<float>();
        if (response is null)
        {
            return false;
        }

        switch (response)
        {
            case float[] floats:
                embedding = floats;
                return true;
            case IReadOnlyList<float> roList:
                embedding = roList.ToArray();
                return true;
            case IEnumerable<float> enumerable:
                embedding = enumerable.ToArray();
                return true;
        }

        Type type = response.GetType();

        System.Reflection.PropertyInfo? vectorProperty = type.GetProperty("Vector");
        if (vectorProperty?.GetValue(response) is IEnumerable<float> vectorEnum)
        {
            embedding = vectorEnum.ToArray();
            return embedding.Length > 0;
        }

        System.Reflection.PropertyInfo? embeddingsProperty = type.GetProperty("Embeddings");
        if (embeddingsProperty?.GetValue(response) is System.Collections.IEnumerable embeddingsEnum)
        {
            foreach (object? entry in embeddingsEnum)
            {
                if (entry is float[] entryArray)
                {
                    embedding = entryArray;
                    return embedding.Length > 0;
                }

                if (entry is IEnumerable<float> direct)
                {
                    embedding = direct.ToArray();
                    if (embedding.Length > 0)
                    {
                        return true;
                    }
                }
                else if (entry is { })
                {
                    Type entryType = entry.GetType();
                    IEnumerable<float>? vectorInner = entryType.GetProperty("Vector")?.GetValue(entry) as IEnumerable<float>;
                    if (vectorInner is not null)
                    {
                        embedding = vectorInner.ToArray();
                        return embedding.Length > 0;
                    }

                    IEnumerable<float>? inner = entryType.GetProperty("Embedding")?.GetValue(entry) as IEnumerable<float>;
                    if (inner is not null)
                    {
                        embedding = inner.ToArray();
                        return embedding.Length > 0;
                    }
                }
            }
        }

        return false;
    }
}

/// <summary>
/// Embedding adapter specifically for Ollama Cloud API endpoints.
/// Uses Ollama's native /api/embeddings endpoint and JSON format.
/// Includes Polly exponential backoff retry policy to handle rate limiting.
/// </summary>
public sealed class OllamaCloudEmbeddingModel : IEmbeddingModel
{
    private readonly HttpClient _client;
    private readonly string _model;
    private readonly DeterministicEmbeddingModel _fallback = new();
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

    public OllamaCloudEmbeddingModel(string endpoint, string apiKey, string model)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) throw new ArgumentException("Endpoint is required", nameof(endpoint));
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("API key is required", nameof(apiKey));

        _client = new HttpClient
        {
            BaseAddress = new Uri(endpoint.TrimEnd('/'), UriKind.Absolute)
        };
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        _model = model;

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
                    Console.WriteLine($"[OllamaCloudEmbeddingModel] Retry {retryCount} after {timespan.TotalSeconds}s due to {outcome.Result?.StatusCode}");
                });
    }

    /// <inheritdoc/>
    public async Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
    {
        try
        {
            // Use Ollama's native /api/embeddings endpoint and JSON format
            using JsonContent payload = JsonContent.Create(new
            {
                model = _model,
                prompt = input
            });

            HttpResponseMessage response = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _client.PostAsync("/api/embeddings", payload, ct).ConfigureAwait(false);
            }).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            Dictionary<string, object?>? json = await response.Content.ReadFromJsonAsync<Dictionary<string, object?>>(cancellationToken: ct).ConfigureAwait(false);
            if (json is not null && json.TryGetValue("embedding", out object? embeddingValue))
            {
                if (embeddingValue is System.Text.Json.JsonElement jsonElement && jsonElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    List<float> floats = new List<float>();
                    foreach (System.Text.Json.JsonElement element in jsonElement.EnumerateArray())
                    {
                        if (element.TryGetSingle(out float value))
                        {
                            floats.Add(value);
                        }
                    }
                    if (floats.Count > 0)
                    {
                        return floats.ToArray();
                    }
                }
            }
        }
        catch
        {
            // Remote Ollama Cloud not reachable → fall back to deterministic embedding
        }
        return await _fallback.CreateEmbeddingsAsync(input, ct).ConfigureAwait(false);
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}

/// <summary>
/// Embedding provider for LiteLLM proxy using OpenAI-compatible /v1/embeddings endpoint.
/// Supports various embedding models proxied through LiteLLM.
/// Includes Polly exponential backoff retry policy to handle rate limiting.
/// </summary>
public sealed class LiteLLMEmbeddingModel : IEmbeddingModel
{
    private readonly HttpClient _client;
    private readonly string _model;
    private readonly DeterministicEmbeddingModel _fallback = new();
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

    /// <summary>
    /// Initializes a new instance of the <see cref="LiteLLMEmbeddingModel"/> class.
    /// </summary>
    /// <param name="endpoint">LiteLLM proxy endpoint URL.</param>
    /// <param name="apiKey">API key for authentication.</param>
    /// <param name="model">Model name (e.g., text-embedding-ada-002, nomic-embed-text).</param>
    public LiteLLMEmbeddingModel(string endpoint, string apiKey, string model)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) throw new ArgumentException("Endpoint is required", nameof(endpoint));
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("API key is required", nameof(apiKey));

        _client = new HttpClient
        {
            BaseAddress = new Uri(endpoint.TrimEnd('/'), UriKind.Absolute),
        };
        _client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
        _model = model;

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
                    Console.WriteLine($"[LiteLLMEmbeddingModel] Retry {retryCount} after {timespan.TotalSeconds}s due to {outcome.Result?.StatusCode}");
                });
    }

    /// <inheritdoc/>
    public async Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
    {
        try
        {
            // LiteLLM uses OpenAI-compatible /v1/embeddings endpoint
            using JsonContent payload = JsonContent.Create(new
            {
                model = _model,
                input = input,
            });

            HttpResponseMessage response = await _retryPolicy.ExecuteAsync(async () =>
            {
                return await _client.PostAsync("/v1/embeddings", payload, ct).ConfigureAwait(false);
            }).ConfigureAwait(false);

            response.EnsureSuccessStatusCode();

            using System.Text.Json.JsonDocument doc = await System.Text.Json.JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct).ConfigureAwait(false), cancellationToken: ct).ConfigureAwait(false);

            // OpenAI format: { "data": [{ "embedding": [...] }] }
            if (doc.RootElement.TryGetProperty("data", out System.Text.Json.JsonElement dataElement) &&
                dataElement.ValueKind == System.Text.Json.JsonValueKind.Array)
            {
                foreach (System.Text.Json.JsonElement item in dataElement.EnumerateArray())
                {
                    if (item.TryGetProperty("embedding", out System.Text.Json.JsonElement embeddingElement) &&
                        embeddingElement.ValueKind == System.Text.Json.JsonValueKind.Array)
                    {
                        List<float> floats = new List<float>();
                        foreach (System.Text.Json.JsonElement element in embeddingElement.EnumerateArray())
                        {
                            if (element.TryGetSingle(out float value))
                            {
                                floats.Add(value);
                            }
                            else if (element.TryGetDouble(out double dblValue))
                            {
                                floats.Add((float)dblValue);
                            }
                        }

                        if (floats.Count > 0)
                        {
                            return floats.ToArray();
                        }
                    }
                }
            }
        }
        catch
        {
            // LiteLLM proxy not reachable or error occurred → fall back to deterministic embedding
        }

        return await _fallback.CreateEmbeddingsAsync(input, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Disposes the HTTP client.
    /// </summary>
    public void Dispose()
    {
        _client?.Dispose();
    }
}
