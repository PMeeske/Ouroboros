using System.Net.Http.Json;
using System.Text;
using LangChain.Providers.Ollama;

namespace LangChainPipeline.Providers;

/// <summary>
/// Minimal contract used by <see cref="ToolAwareChatModel"/> to obtain text responses.
/// </summary>
public interface IChatCompletionModel
{
    Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default);
}

/// <summary>
/// Adapter for local Ollama models. We attempt to call the SDK when available,
/// falling back to a deterministic stub when the local daemon is not reachable.
/// </summary>
public sealed class OllamaChatAdapter : IChatCompletionModel
{
    private readonly OllamaChatModel _model;

    public OllamaChatAdapter(OllamaChatModel model)
    {
        _model = model ?? throw new ArgumentNullException(nameof(model));
    }

    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        try
        {
            var stream = _model.GenerateAsync(prompt, cancellationToken: ct);
            var builder = new StringBuilder();

            await foreach (var chunk in stream.WithCancellation(ct).ConfigureAwait(false))
            {
                var text = ExtractResponseText(chunk);
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

        var type = response.GetType();

        var lastMessageProperty = type.GetProperty("LastMessageContent");
        if (lastMessageProperty?.GetValue(response) is string last)
        {
            return last;
        }

        var contentProperty = type.GetProperty("Content");
        if (contentProperty?.GetValue(response) is string content)
        {
            return content;
        }

        var messageProperty = type.GetProperty("Message");
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

            var nestedContent = message.GetType().GetProperty("Content")?.GetValue(message) as string;
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
/// </summary>
public sealed class HttpOpenAiCompatibleChatModel : IChatCompletionModel
{
    private readonly HttpClient _client;
    private readonly string _model;
    private readonly ChatRuntimeSettings _settings;

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
    }

    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        try
        {
            using var payload = JsonContent.Create(new
            {
                model = _model,
                temperature = _settings.Temperature,
                max_output_tokens = _settings.MaxTokens,
                input = prompt
            });
            using var response = await _client.PostAsync("/v1/responses", payload, ct).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadFromJsonAsync<Dictionary<string, object?>>(cancellationToken: ct).ConfigureAwait(false);
            if (json is not null && json.TryGetValue("output_text", out var text) && text is string s)
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

    public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        IChatCompletionModel target = SelectModel(prompt);
        return target.GenerateTextAsync(prompt, ct);
    }

    private IChatCompletionModel SelectModel(string prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt)) return _models[_fallbackKey];
        if (prompt.Contains("code", StringComparison.OrdinalIgnoreCase) && _models.TryGetValue("coder", out var coder))
            return coder;
        if (prompt.Length > 600 && _models.TryGetValue("summarize", out var summarize))
            return summarize;
        if (prompt.Contains("reason", StringComparison.OrdinalIgnoreCase) && _models.TryGetValue("reason", out var reason))
            return reason;
        return _models.TryGetValue(_fallbackKey, out var fallback) ? fallback : _models.Values.First();
    }
}

/// <summary>
/// Simple embedding abstraction used across the DSL pipeline.
/// </summary>
public interface IEmbeddingModel
{
    Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default);
}

/// <summary>
/// Deterministic embedding generator that hashes the input string. It is not a
/// semantic encoder, but it provides stable vectors for testing and demos when
/// no real embedding service is available.
/// </summary>
public sealed class DeterministicEmbeddingModel : IEmbeddingModel
{
    public Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
    {
        if (input is null) input = string.Empty;
        Span<byte> buffer = stackalloc byte[Math.Max(32, input.Length)];
        int len = System.Text.Encoding.UTF8.GetBytes(input, buffer);
        var hash = System.Security.Cryptography.SHA256.HashData(buffer[..len]);
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

    public async Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
    {
        try
        {
            var response = await _model.CreateEmbeddingsAsync(input, cancellationToken: ct).ConfigureAwait(false);
            if (TryExtractEmbedding(response, out var vector))
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

        var type = response.GetType();

        var vectorProperty = type.GetProperty("Vector");
        if (vectorProperty?.GetValue(response) is IEnumerable<float> vectorEnum)
        {
            embedding = vectorEnum.ToArray();
            return embedding.Length > 0;
        }

        var embeddingsProperty = type.GetProperty("Embeddings");
        if (embeddingsProperty?.GetValue(response) is System.Collections.IEnumerable embeddingsEnum)
        {
            foreach (var entry in embeddingsEnum)
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
                    var entryType = entry.GetType();
                    var vectorInner = entryType.GetProperty("Vector")?.GetValue(entry) as IEnumerable<float>;
                    if (vectorInner is not null)
                    {
                        embedding = vectorInner.ToArray();
                        return embedding.Length > 0;
                    }

                    var inner = entryType.GetProperty("Embedding")?.GetValue(entry) as IEnumerable<float>;
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
