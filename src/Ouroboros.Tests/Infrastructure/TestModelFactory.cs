// <copyright file="TestModelFactory.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Infrastructure;

using Ouroboros.Domain;
using Ouroboros.Providers;
using Ouroboros.Tests.Mocks;

/// <summary>
/// Factory for creating model instances in tests.
/// By default uses fast mock models. Can be configured via environment variables
/// to use cloud or local models for integration testing.
/// </summary>
/// <remarks>
/// Environment variables:
/// - TEST_USE_CLOUD_MODEL: Set to "true" to use cloud models (requires CHAT_ENDPOINT and CHAT_API_KEY)
/// - TEST_USE_LOCAL_MODEL: Set to "true" to use local Ollama models (requires Ollama daemon running)
/// - TEST_CHAT_MODEL: Override the chat model name (default: "llama3" for local, "gpt-4" for cloud)
/// - TEST_EMBED_MODEL: Override the embedding model name (default: "nomic-embed-text").
/// </remarks>
public static class TestModelFactory
{
    private static readonly bool UseCloudModel = Environment.GetEnvironmentVariable("TEST_USE_CLOUD_MODEL") == "true";
    private static readonly bool UseLocalModel = Environment.GetEnvironmentVariable("TEST_USE_LOCAL_MODEL") == "true";
    private static readonly string? ChatModelOverride = Environment.GetEnvironmentVariable("TEST_CHAT_MODEL");
    private static readonly string? EmbedModelOverride = Environment.GetEnvironmentVariable("TEST_EMBED_MODEL");

    /// <summary>
    /// Gets whether tests are configured to use real models (cloud or local).
    /// </summary>
    public static bool UsesRealModels => UseCloudModel || UseLocalModel;

    /// <summary>
    /// Creates a chat completion model for testing.
    /// Returns a mock by default for fast tests.
    /// </summary>
    /// <param name="response">Optional fixed response for mock model.</param>
    /// <param name="responseFactory">Optional factory function for dynamic mock responses.</param>
    /// <returns>A chat completion model instance.</returns>
    public static IChatCompletionModel CreateChatModel(
        string? response = null,
        Func<string, string>? responseFactory = null)
    {
        if (UseCloudModel)
        {
            return CreateCloudChatModel();
        }

        if (UseLocalModel)
        {
            return CreateLocalChatModel();
        }

        // Default: use fast mock
        if (responseFactory != null)
        {
            return new MockChatModel(responseFactory);
        }

        return new MockChatModel(response ?? GetDefaultMockResponse());
    }

    /// <summary>
    /// Creates an embedding model for testing.
    /// Returns a mock by default for fast tests.
    /// </summary>
    /// <param name="embeddingSize">Size of embeddings for mock model. Default: 384.</param>
    /// <returns>An embedding model instance.</returns>
    public static IEmbeddingModel CreateEmbeddingModel(int embeddingSize = 384)
    {
        if (UseCloudModel)
        {
            return CreateCloudEmbeddingModel();
        }

        if (UseLocalModel)
        {
            return CreateLocalEmbeddingModel();
        }

        // Default: use fast mock
        return new MockEmbeddingModel(embeddingSize);
    }

    /// <summary>
    /// Creates a tool-aware chat model for testing.
    /// </summary>
    /// <param name="tools">Optional tool registry.</param>
    /// <param name="response">Optional fixed response for mock model.</param>
    /// <returns>A tool-aware chat model instance.</returns>
    public static ToolAwareChatModel CreateToolAwareChatModel(
        ToolRegistry? tools = null,
        string? response = null)
    {
        var chatModel = CreateChatModel(response);
        return new ToolAwareChatModel(chatModel, tools ?? new ToolRegistry());
    }

    /// <summary>
    /// Creates both chat and embedding models as a tuple for convenience.
    /// </summary>
    /// <returns>A tuple of (chat model, embedding model).</returns>
    public static (IChatCompletionModel Chat, IEmbeddingModel Embed) CreateModels()
    {
        return (CreateChatModel(), CreateEmbeddingModel());
    }

    /// <summary>
    /// Skips the test if real models are not available.
    /// Use this for tests that require actual LLM responses.
    /// </summary>
    /// <exception cref="Xunit.SkipException">Thrown when real models are not configured.</exception>
    public static void SkipIfMockModels()
    {
        if (!UsesRealModels)
        {
            throw new Xunit.SkipException(
                "Test requires real models. Set TEST_USE_CLOUD_MODEL=true or TEST_USE_LOCAL_MODEL=true");
        }
    }

    private static IChatCompletionModel CreateCloudChatModel()
    {
        var (endpoint, key, type) = ChatConfig.Resolve();
        var model = ChatModelOverride ?? "gpt-4";

        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(key))
        {
            throw new InvalidOperationException(
                "Cloud model requested but CHAT_ENDPOINT and CHAT_API_KEY not set");
        }

        return type == ChatEndpointType.OllamaCloud
            ? new OllamaCloudChatModel(endpoint, key, model)
            : new HttpOpenAiCompatibleChatModel(endpoint, key, model);
    }

    private static IChatCompletionModel CreateLocalChatModel()
    {
        var model = ChatModelOverride ?? "llama3";
        var provider = new LangChain.Providers.Ollama.OllamaProvider();
        var ollamaModel = new LangChain.Providers.Ollama.OllamaChatModel(provider, model);
        return new OllamaChatAdapter(ollamaModel);
    }

    private static IEmbeddingModel CreateCloudEmbeddingModel()
    {
        var (endpoint, key, _) = ChatConfig.Resolve();
        var model = EmbedModelOverride ?? "nomic-embed-text";

        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(key))
        {
            throw new InvalidOperationException(
                "Cloud model requested but CHAT_ENDPOINT and CHAT_API_KEY not set");
        }

        return new OllamaCloudEmbeddingModel(endpoint, key, model);
    }

    private static IEmbeddingModel CreateLocalEmbeddingModel()
    {
        var model = EmbedModelOverride ?? "nomic-embed-text";
        var provider = new LangChain.Providers.Ollama.OllamaProvider();
        var ollamaModel = new LangChain.Providers.Ollama.OllamaEmbeddingModel(provider, model);
        return new OllamaEmbeddingAdapter(ollamaModel);
    }

    private static string GetDefaultMockResponse()
    {
        return "[mock-response] This is a mock LLM response for testing. " +
               "To use real models, set TEST_USE_CLOUD_MODEL=true or TEST_USE_LOCAL_MODEL=true.";
    }
}
