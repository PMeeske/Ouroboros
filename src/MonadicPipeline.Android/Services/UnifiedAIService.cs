using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace MonadicPipeline.Android.Services;

/// <summary>
/// Unified AI service supporting multiple providers
/// </summary>
public class UnifiedAIService
{
    private readonly AIProviderService _providerService;
    private readonly HttpClient _httpClient;
    private readonly SymbolicReasoningEngine _reasoningEngine;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnifiedAIService"/> class.
    /// </summary>
    public UnifiedAIService()
    {
        _providerService = new AIProviderService();
        _httpClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        _reasoningEngine = new SymbolicReasoningEngine();
    }

    /// <summary>
    /// Gets the symbolic reasoning engine
    /// </summary>
    public SymbolicReasoningEngine ReasoningEngine => _reasoningEngine;

    /// <summary>
    /// Generate a response using the active provider
    /// </summary>
    public async Task<string> GenerateAsync(
        string prompt,
        string? model = null,
        Action<string>? streamCallback = null,
        CancellationToken cancellationToken = default)
    {
        var config = _providerService.GetActiveProviderConfig();
        var useModel = model ?? config.DefaultModel ?? "default";

        return config.Provider switch
        {
            AIProvider.Ollama => await GenerateOllamaAsync(config, useModel, prompt, streamCallback, cancellationToken),
            AIProvider.OpenAI => await GenerateOpenAIAsync(config, useModel, prompt, streamCallback, cancellationToken),
            AIProvider.Anthropic => await GenerateAnthropicAsync(config, useModel, prompt, streamCallback, cancellationToken),
            AIProvider.Google => await GenerateGoogleAsync(config, useModel, prompt, streamCallback, cancellationToken),
            AIProvider.Meta => await GenerateMetaAsync(config, useModel, prompt, streamCallback, cancellationToken),
            AIProvider.Cohere => await GenerateCohereAsync(config, useModel, prompt, streamCallback, cancellationToken),
            AIProvider.Mistral => await GenerateMistralAsync(config, useModel, prompt, streamCallback, cancellationToken),
            AIProvider.HuggingFace => await GenerateHuggingFaceAsync(config, useModel, prompt, streamCallback, cancellationToken),
            AIProvider.AzureOpenAI => await GenerateAzureOpenAIAsync(config, useModel, prompt, streamCallback, cancellationToken),
            _ => throw new NotSupportedException($"Provider {config.Provider} not supported")
        };
    }

    /// <summary>
    /// Generate with symbolic reasoning augmentation
    /// </summary>
    public async Task<string> GenerateWithReasoningAsync(
        string prompt,
        string? model = null,
        CancellationToken cancellationToken = default)
    {
        // First, perform symbolic reasoning on the prompt
        var reasoningContext = _reasoningEngine.ExportKnowledgeBase();
        
        // Augment prompt with reasoning context if knowledge base is not empty
        var augmentedPrompt = prompt;
        if (_reasoningEngine.GetAllFacts().Count > 0)
        {
            augmentedPrompt = $@"Knowledge Base Context:
{reasoningContext}

User Query:
{prompt}

Please use the knowledge base context above to inform your response.";
        }

        return await GenerateAsync(augmentedPrompt, model, null, cancellationToken);
    }

    private async Task<string> GenerateOllamaAsync(
        AIProviderConfig config,
        string model,
        string prompt,
        Action<string>? streamCallback,
        CancellationToken cancellationToken)
    {
        var request = new
        {
            model = model,
            prompt = prompt,
            stream = streamCallback != null,
            options = new
            {
                temperature = config.Temperature ?? 0.7,
                num_predict = config.MaxTokens ?? 2000
            }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        ConfigureHeaders(config);

        using var response = await _httpClient.PostAsync(
            $"{config.Endpoint}/api/generate",
            content,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        if (streamCallback != null)
        {
            return await ProcessStreamingResponse(response, streamCallback, cancellationToken);
        }
        else
        {
            var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(cancellationToken);
            return result?.Response ?? string.Empty;
        }
    }

    private async Task<string> GenerateOpenAIAsync(
        AIProviderConfig config,
        string model,
        string prompt,
        Action<string>? streamCallback,
        CancellationToken cancellationToken)
    {
        var request = new
        {
            model = model,
            messages = new[] { new { role = "user", content = prompt } },
            temperature = config.Temperature ?? 0.7,
            max_tokens = config.MaxTokens ?? 2000,
            stream = streamCallback != null
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        ConfigureHeaders(config);

        using var response = await _httpClient.PostAsync(
            $"{config.Endpoint}/chat/completions",
            content,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        if (streamCallback != null)
        {
            return await ProcessOpenAIStreamingResponse(response, streamCallback, cancellationToken);
        }
        else
        {
            var result = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonDoc = JsonDocument.Parse(result);
            return jsonDoc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;
        }
    }

    private async Task<string> GenerateAnthropicAsync(
        AIProviderConfig config,
        string model,
        string prompt,
        Action<string>? streamCallback,
        CancellationToken cancellationToken)
    {
        var request = new
        {
            model = model,
            messages = new[] { new { role = "user", content = prompt } },
            max_tokens = config.MaxTokens ?? 2000,
            temperature = config.Temperature ?? 0.7,
            stream = streamCallback != null
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        ConfigureHeaders(config);
        _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

        using var response = await _httpClient.PostAsync(
            $"{config.Endpoint}/messages",
            content,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadAsStringAsync(cancellationToken);
        var jsonDoc = JsonDocument.Parse(result);
        return jsonDoc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;
    }

    private async Task<string> GenerateGoogleAsync(
        AIProviderConfig config,
        string model,
        string prompt,
        Action<string>? streamCallback,
        CancellationToken cancellationToken)
    {
        var request = new
        {
            contents = new[]
            {
                new
                {
                    parts = new[] { new { text = prompt } }
                }
            },
            generationConfig = new
            {
                temperature = config.Temperature ?? 0.7,
                maxOutputTokens = config.MaxTokens ?? 2000
            }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        var url = $"{config.Endpoint}/models/{model}:generateContent?key={config.ApiKey}";

        using var response = await _httpClient.PostAsync(url, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadAsStringAsync(cancellationToken);
        var jsonDoc = JsonDocument.Parse(result);
        return jsonDoc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;
    }

    private async Task<string> GenerateMetaAsync(
        AIProviderConfig config,
        string model,
        string prompt,
        Action<string>? streamCallback,
        CancellationToken cancellationToken)
    {
        // Meta LLaMA via Together.ai or similar API
        return await GenerateOpenAIAsync(config, model, prompt, streamCallback, cancellationToken);
    }

    private async Task<string> GenerateCohereAsync(
        AIProviderConfig config,
        string model,
        string prompt,
        Action<string>? streamCallback,
        CancellationToken cancellationToken)
    {
        var request = new
        {
            model = model,
            prompt = prompt,
            max_tokens = config.MaxTokens ?? 2000,
            temperature = config.Temperature ?? 0.7,
            stream = streamCallback != null
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        ConfigureHeaders(config);

        using var response = await _httpClient.PostAsync(
            $"{config.Endpoint}/generate",
            content,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadAsStringAsync(cancellationToken);
        var jsonDoc = JsonDocument.Parse(result);
        return jsonDoc.RootElement
            .GetProperty("generations")[0]
            .GetProperty("text")
            .GetString() ?? string.Empty;
    }

    private async Task<string> GenerateMistralAsync(
        AIProviderConfig config,
        string model,
        string prompt,
        Action<string>? streamCallback,
        CancellationToken cancellationToken)
    {
        // Mistral uses OpenAI-compatible API
        return await GenerateOpenAIAsync(config, model, prompt, streamCallback, cancellationToken);
    }

    private async Task<string> GenerateHuggingFaceAsync(
        AIProviderConfig config,
        string model,
        string prompt,
        Action<string>? streamCallback,
        CancellationToken cancellationToken)
    {
        var request = new
        {
            inputs = prompt,
            parameters = new
            {
                max_new_tokens = config.MaxTokens ?? 2000,
                temperature = config.Temperature ?? 0.7
            }
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        ConfigureHeaders(config);

        using var response = await _httpClient.PostAsync(
            $"{config.Endpoint}/models/{model}",
            content,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadAsStringAsync(cancellationToken);
        var jsonDoc = JsonDocument.Parse(result);
        return jsonDoc.RootElement[0]
            .GetProperty("generated_text")
            .GetString() ?? string.Empty;
    }

    private async Task<string> GenerateAzureOpenAIAsync(
        AIProviderConfig config,
        string model,
        string prompt,
        Action<string>? streamCallback,
        CancellationToken cancellationToken)
    {
        var request = new
        {
            messages = new[] { new { role = "user", content = prompt } },
            temperature = config.Temperature ?? 0.7,
            max_tokens = config.MaxTokens ?? 2000,
            stream = streamCallback != null
        };

        var content = new StringContent(
            JsonSerializer.Serialize(request),
            Encoding.UTF8,
            "application/json");

        ConfigureHeaders(config);

        var url = $"{config.Endpoint}/openai/deployments/{config.DeploymentName}/chat/completions?api-version=2023-05-15";

        using var response = await _httpClient.PostAsync(url, content, cancellationToken);
        response.EnsureSuccessStatusCode();

        if (streamCallback != null)
        {
            return await ProcessOpenAIStreamingResponse(response, streamCallback, cancellationToken);
        }
        else
        {
            var result = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonDoc = JsonDocument.Parse(result);
            return jsonDoc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? string.Empty;
        }
    }

    private void ConfigureHeaders(AIProviderConfig config)
    {
        _httpClient.DefaultRequestHeaders.Clear();

        if (!string.IsNullOrEmpty(config.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", config.ApiKey);
        }

        if (!string.IsNullOrEmpty(config.OrganizationId))
        {
            _httpClient.DefaultRequestHeaders.Add("OpenAI-Organization", config.OrganizationId);
        }

        foreach (var (key, value) in config.CustomHeaders)
        {
            _httpClient.DefaultRequestHeaders.Add(key, value);
        }
    }

    private async Task<string> ProcessStreamingResponse(
        HttpResponseMessage response,
        Action<string> callback,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var fullResponse = new StringBuilder();

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (!string.IsNullOrEmpty(line))
            {
                try
                {
                    var jsonResponse = JsonSerializer.Deserialize<OllamaGenerateResponse>(line);
                    if (jsonResponse?.Response != null)
                    {
                        fullResponse.Append(jsonResponse.Response);
                        callback(jsonResponse.Response);
                    }

                    if (jsonResponse?.Done == true)
                    {
                        break;
                    }
                }
                catch
                {
                    // Skip malformed JSON lines
                }
            }
        }

        return fullResponse.ToString();
    }

    private async Task<string> ProcessOpenAIStreamingResponse(
        HttpResponseMessage response,
        Action<string> callback,
        CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var fullResponse = new StringBuilder();

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            
            if (line?.StartsWith("data: ") == true)
            {
                var data = line.Substring(6);
                
                if (data == "[DONE]")
                {
                    break;
                }

                try
                {
                    var jsonDoc = JsonDocument.Parse(data);
                    var delta = jsonDoc.RootElement
                        .GetProperty("choices")[0]
                        .GetProperty("delta");

                    if (delta.TryGetProperty("content", out var content))
                    {
                        var text = content.GetString();
                        if (!string.IsNullOrEmpty(text))
                        {
                            fullResponse.Append(text);
                            callback(text);
                        }
                    }
                }
                catch
                {
                    // Skip malformed JSON lines
                }
            }
        }

        return fullResponse.ToString();
    }
}

/// <summary>
/// Response from Ollama generate API
/// </summary>
public class OllamaGenerateResponse
{
    /// <summary>
    /// Gets or sets the model name
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Gets or sets the response text
    /// </summary>
    public string? Response { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether generation is complete
    /// </summary>
    public bool Done { get; set; }
}
