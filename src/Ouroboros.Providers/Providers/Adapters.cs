#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using System.Net.Http.Json;
using System.Text;
using LangChain.Providers.Ollama;
using Polly;
using Polly.Retry;
using System.Reactive.Linq;

namespace LangChainPipeline.Providers;

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
/// Adapter for local Ollama models. We attempt to call the SDK when available,
/// falling back to a deterministic stub when the local daemon is not reachable.
/// </summary>
public sealed class OllamaChatAdapter : IStreamingChatModel
{
    private readonly OllamaChatModel _model;

    public OllamaChatAdapter(OllamaChatModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
    }

    /// <inheritdoc/>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        try
        {
            IAsyncEnumerable<LangChain.Providers.ChatResponse> stream = _model.GenerateAsync(prompt, cancellationToken: ct);
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
    public IObservable<string> StreamReasoningContent(string prompt, CancellationToken ct = default)
    {
        return Observable.Create<string>(async (observer, token) =>
        {
            try
            {
                IAsyncEnumerable<LangChain.Providers.ChatResponse> stream = _model.GenerateAsync(prompt, cancellationToken: token);
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
                input = prompt
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
/// </summary>
public sealed class OllamaCloudChatModel : IStreamingChatModel
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
                prompt = prompt,
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
    public IObservable<string> StreamReasoningContent(string prompt, CancellationToken ct = default)
    {
        return Observable.Create<string>(async (observer, token) =>
        {
            try
            {
                using JsonContent payload = JsonContent.Create(new
                {
                    model = _model,
                    prompt = prompt,
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
/// HTTP client for LiteLLM proxy endpoints that support OpenAI-compatible chat completions API.
/// Uses standard /v1/chat/completions endpoint with messages format.
/// Includes Polly exponential backoff retry policy to handle rate limiting.
/// </summary>
public sealed class LiteLLMChatModel : IStreamingChatModel
{
    private readonly HttpClient _client;
    private readonly string _model;
    private readonly ChatRuntimeSettings _settings;
    private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;

    public LiteLLMChatModel(string endpoint, string apiKey, string model, ChatRuntimeSettings? settings = null)
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
                    Console.WriteLine($"[LiteLLMChatModel] Retry {retryCount} after {timespan.TotalSeconds}s due to {outcome.Result?.StatusCode}");
                });
    }

    /// <inheritdoc/>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        try
        {
            // Use OpenAI-compatible chat completions format
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

                    // Fall back to reasoning_content (for models that use it)
                    if (messageElement.TryGetProperty("reasoning_content", out System.Text.Json.JsonElement reasoningElement))
                    {
                        return reasoningElement.GetString() ?? string.Empty;
                    }
                }
            }
        }
        catch
        {
            // Remote LiteLLM endpoint not reachable → fall back to indicating failure.
        }

        return $"[litellm-fallback:{_model}] {prompt}";
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _client?.Dispose();
    }

    /// <inheritdoc/>
    public System.IObservable<string> StreamReasoningContent(string prompt, CancellationToken ct = default)
    {
        return System.Reactive.Linq.Observable.Create<string>(async (observer, token) =>
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
                                // Try reasoning_content field for models that use it
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
    /// <inheritdoc/>
    public Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
    {
        if (input is null) input = string.Empty;
        Span<byte> buffer = stackalloc byte[Math.Max(32, input.Length)];
        int len = System.Text.Encoding.UTF8.GetBytes(input, buffer);
        byte[] hash = System.Security.Cryptography.SHA256.HashData(buffer[..len]);
        float[] vector = new float[hash.Length];
        for (int i = 0; i < hash.Length; i++)
        {
            vector[i] = hash[i] / 255f;
        }
        return Task.FromResult(vector);
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
        try
        {
            LangChain.Providers.EmbeddingResponse response = await _model.CreateEmbeddingsAsync(input, cancellationToken: ct).ConfigureAwait(false);
            if (TryExtractEmbedding(response, out float[]? vector))
            {
                return vector;
            }
            return await _fallback.CreateEmbeddingsAsync(input, ct).ConfigureAwait(false);
        }
        catch
        {
            return await _fallback.CreateEmbeddingsAsync(input, ct).ConfigureAwait(false);
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
