// <copyright file="DeepSeekChatModel.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Reactive.Linq;

namespace Ouroboros.Providers.DeepSeek;

/// <summary>
/// Chat model implementation for DeepSeek models via Ollama (local or cloud).
/// Supports DeepSeek R1 models (7B, 8B, 14B, 32B, 70B) through Ollama infrastructure.
/// Provides both local inference and Ollama Cloud API access.
/// DeepSeek R1 models support extended thinking mode with &lt;think&gt; tags.
/// </summary>
public sealed class DeepSeekChatModel : IStreamingThinkingChatModel
{
    private readonly IChatCompletionModel _underlyingModel;
    private readonly string _modelName;

    /// <summary>
    /// Model identifier for DeepSeek R1 distilled 7B (local).
    /// </summary>
    public const string ModelDeepSeekR1_7B = "deepseek-r1:7b";

    /// <summary>
    /// Model identifier for DeepSeek R1 distilled 8B (local).
    /// </summary>
    public const string ModelDeepSeekR1_8B = "deepseek-r1:8b";

    /// <summary>
    /// Model identifier for DeepSeek R1 distilled 14B (local).
    /// </summary>
    public const string ModelDeepSeekR1_14B = "deepseek-r1:14b";

    /// <summary>
    /// Model identifier for DeepSeek R1 32B (local/cloud).
    /// </summary>
    public const string ModelDeepSeekR1_32B = "deepseek-r1:32b";

    /// <summary>
    /// Model identifier for DeepSeek R1 70B (cloud).
    /// </summary>
    public const string ModelDeepSeekR1_70B = "deepseek-r1:70b";

    /// <summary>
    /// Initializes a new instance of the <see cref="DeepSeekChatModel"/> class for local Ollama.
    /// </summary>
    /// <param name="ollamaModel">Ollama chat model instance.</param>
    public DeepSeekChatModel(LangChain.Providers.Ollama.OllamaChatModel ollamaModel)
    {
        if (ollamaModel == null) throw new ArgumentNullException(nameof(ollamaModel));
        _underlyingModel = new OllamaChatAdapter(ollamaModel);
        _modelName = "deepseek-local";
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeepSeekChatModel"/> class for Ollama Cloud.
    /// </summary>
    /// <param name="endpoint">Ollama Cloud endpoint URL.</param>
    /// <param name="apiKey">Ollama Cloud API key.</param>
    /// <param name="model">DeepSeek model name (e.g., deepseek-r1:32b).</param>
    /// <param name="settings">Optional runtime settings.</param>
    public DeepSeekChatModel(
        string endpoint,
        string apiKey,
        string model,
        ChatRuntimeSettings? settings = null)
    {
        if (string.IsNullOrWhiteSpace(endpoint)) throw new ArgumentException("Endpoint is required", nameof(endpoint));
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("API key is required", nameof(apiKey));
        if (string.IsNullOrWhiteSpace(model)) throw new ArgumentException("Model is required", nameof(model));

        _underlyingModel = new OllamaCloudChatModel(endpoint, apiKey, model, settings);
        _modelName = model;
    }

    /// <summary>
    /// Creates a local DeepSeek model using Ollama.
    /// </summary>
    /// <param name="provider">Ollama provider instance.</param>
    /// <param name="model">DeepSeek model name (default: deepseek-r1:8b).</param>
    /// <returns>Configured DeepSeek chat model for local inference.</returns>
    public static DeepSeekChatModel CreateLocal(
        LangChain.Providers.Ollama.OllamaProvider provider,
        string model = ModelDeepSeekR1_8B)
    {
        var ollamaModel = new LangChain.Providers.Ollama.OllamaChatModel(provider, model);
        return new DeepSeekChatModel(ollamaModel);
    }

    /// <summary>
    /// Creates a DeepSeek model using Ollama Cloud from environment variables.
    /// Reads OLLAMA_CLOUD_ENDPOINT and OLLAMA_CLOUD_API_KEY (or DEEPSEEK_API_KEY as fallback).
    /// </summary>
    /// <param name="model">DeepSeek model name (default: deepseek-r1:32b).</param>
    /// <param name="settings">Optional runtime settings.</param>
    /// <returns>Configured DeepSeek chat model for Ollama Cloud.</returns>
    /// <exception cref="InvalidOperationException">Thrown when required environment variables are not set.</exception>
    public static DeepSeekChatModel FromEnvironment(
        string model = ModelDeepSeekR1_32B,
        ChatRuntimeSettings? settings = null)
    {
        string? endpoint = Environment.GetEnvironmentVariable("OLLAMA_CLOUD_ENDPOINT")
                          ?? Environment.GetEnvironmentVariable("CHAT_ENDPOINT");
        string? apiKey = Environment.GetEnvironmentVariable("OLLAMA_CLOUD_API_KEY")
                        ?? Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY")
                        ?? Environment.GetEnvironmentVariable("CHAT_API_KEY");

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException(
                "OLLAMA_CLOUD_ENDPOINT or CHAT_ENDPOINT environment variable is not set. " +
                "Set it to your Ollama Cloud endpoint (e.g., https://api.ollama.ai)");
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException(
                "OLLAMA_CLOUD_API_KEY, DEEPSEEK_API_KEY, or CHAT_API_KEY environment variable is not set. " +
                "Get your API key from your Ollama Cloud dashboard");
        }

        return new DeepSeekChatModel(endpoint, apiKey, model, settings);
    }

    /// <inheritdoc/>
    public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        return _underlyingModel.GenerateTextAsync(prompt, ct);
    }

    /// <inheritdoc/>
    public async Task<ThinkingResponse> GenerateWithThinkingAsync(string prompt, CancellationToken ct = default)
    {
        if (_underlyingModel is IThinkingChatModel thinkingModel)
        {
            return await thinkingModel.GenerateWithThinkingAsync(prompt, ct);
        }

        // Fallback: parse thinking from raw text
        string result = await GenerateTextAsync(prompt, ct);
        return ThinkingResponse.FromRawText(result);
    }

    /// <inheritdoc/>
    public IObservable<(bool IsThinking, string Chunk)> StreamWithThinkingAsync(string prompt, CancellationToken ct = default)
    {
        if (_underlyingModel is IStreamingThinkingChatModel streamingThinkingModel)
        {
            return streamingThinkingModel.StreamWithThinkingAsync(prompt, ct);
        }

        // Fallback to non-thinking streaming
        if (_underlyingModel is IStreamingChatModel streamingModel)
        {
            return streamingModel.StreamReasoningContent(prompt, ct)
                .Select(chunk => (false, chunk));
        }

        // Ultimate fallback to non-streaming
        return System.Reactive.Linq.Observable.FromAsync(async () =>
        {
            var response = await GenerateWithThinkingAsync(prompt, ct);
            return (response.HasThinking, response.ToFormattedString());
        });
    }

    /// <inheritdoc/>
    public IObservable<string> StreamReasoningContent(string prompt, CancellationToken ct = default)
    {
        if (_underlyingModel is IStreamingChatModel streamingModel)
        {
            return streamingModel.StreamReasoningContent(prompt, ct);
        }

        // Fallback to non-streaming
        return System.Reactive.Linq.Observable.FromAsync(async () => await GenerateTextAsync(prompt, ct));
    }
}
