// <copyright file="ServiceCollectionExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Providers;

using LangChain.Providers.Ollama;
using LangChainPipeline.Core.Configuration;
using LangChainPipeline.Domain.Vectors;
using LangChainPipeline.Providers.SpeechToText;
using LangChainPipeline.Providers.TextToSpeech;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Dependency injection helpers for registering chat and embedding models.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register an interchangeable chat + embedding stack that prefers remote OpenAI-compatible
    /// endpoints when configured, falling back to local Ollama with deterministic embeddings otherwise.
    /// </summary>
    /// <returns></returns>
    public static IServiceCollection AddInterchangeableLlm(this IServiceCollection services, string? model = null, string? embed = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        model = string.IsNullOrWhiteSpace(model) ? "llama3" : model;
        embed = string.IsNullOrWhiteSpace(embed) ? "nomic-embed-text" : embed;

        services.AddSingleton<OllamaProvider>();

        services.AddSingleton<IChatCompletionModel>(sp =>
        {
            (string? endpoint, string? apiKey, ChatEndpointType endpointType) = ChatConfig.Resolve();
            if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey))
            {
                try
                {
                    return endpointType switch
                    {
                        ChatEndpointType.OllamaCloud => new OllamaCloudChatModel(endpoint!, apiKey!, model!),
                        ChatEndpointType.OpenAiCompatible => new HttpOpenAiCompatibleChatModel(endpoint!, apiKey!, model!),
                        ChatEndpointType.Auto => new HttpOpenAiCompatibleChatModel(endpoint!, apiKey!, model!),
                        _ => new HttpOpenAiCompatibleChatModel(endpoint!, apiKey!, model!),
                    };
                }
                catch
                {
                    // Ignore and fall back to local Ollama below.
                }
            }

            OllamaProvider provider = sp.GetRequiredService<OllamaProvider>();
            OllamaChatModel chat = new OllamaChatModel(provider, model!);
            try
            {
                string n = (model ?? string.Empty).ToLowerInvariant();
                if (n.StartsWith("deepseek-coder:33b"))
                {
                    chat.Settings = OllamaPresets.DeepSeekCoder33B;
                }
                else if (n.StartsWith("llama3"))
                {
                    chat.Settings = OllamaPresets.Llama3General;
                }
                else if (n.StartsWith("deepseek-r1:32") || n.Contains("32b"))
                {
                    chat.Settings = OllamaPresets.DeepSeekR1_32B_Reason;
                }
                else if (n.StartsWith("deepseek-r1:14") || n.Contains("14b"))
                {
                    chat.Settings = OllamaPresets.DeepSeekR1_14B_Reason;
                }
                else if (n.Contains("mistral") && (n.Contains("7b") || !n.Contains("large")))
                {
                    chat.Settings = OllamaPresets.Mistral7BGeneral;
                }
                else if (n.StartsWith("qwen2.5") || n.Contains("qwen"))
                {
                    chat.Settings = OllamaPresets.Qwen25_7B_General;
                }
                else if (n.StartsWith("phi3") || n.Contains("phi-3"))
                {
                    chat.Settings = OllamaPresets.Phi3MiniGeneral;
                }
            }
            catch
            {
                // Best-effort mapping; if detection fails we keep provider defaults.
            }

            return new OllamaChatAdapter(chat);
        });

        services.AddSingleton<IEmbeddingModel>(sp =>
        {
            OllamaProvider provider = sp.GetRequiredService<OllamaProvider>();
            return new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, embed!));
        });

        services.AddSingleton<ToolRegistry>();
        services.AddSingleton(sp =>
        {
            ToolRegistry registry = sp.GetRequiredService<ToolRegistry>();
            IChatCompletionModel chat = sp.GetRequiredService<IChatCompletionModel>();
            return new ToolAwareChatModel(chat, registry);
        });

        return services;
    }

    /// <summary>
    /// Register vector store based on configuration.
    /// Supports InMemory (default), Qdrant, and extensible to other backends.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Optional explicit configuration. If null, reads from IOptions.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVectorStore(this IServiceCollection services, VectorStoreConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Register the factory
        services.AddSingleton<VectorStoreFactory>(sp =>
        {
            var config = configuration ?? sp.GetService<IOptions<VectorStoreConfiguration>>()?.Value ?? new VectorStoreConfiguration();
            var logger = sp.GetService<ILogger<VectorStoreFactory>>();
            return new VectorStoreFactory(config, logger);
        });

        // Register IVectorStore using the factory
        services.AddSingleton<IVectorStore>(sp =>
        {
            var factory = sp.GetRequiredService<VectorStoreFactory>();
            return factory.Create();
        });

        return services;
    }

    /// <summary>
    /// Register vector store with explicit type selection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="storeType">Type of store: "InMemory", "Qdrant".</param>
    /// <param name="connectionString">Connection string for external stores.</param>
    /// <param name="collectionName">Collection/index name.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddVectorStore(
        this IServiceCollection services,
        string storeType,
        string? connectionString = null,
        string collectionName = "pipeline_vectors")
    {
        var config = new VectorStoreConfiguration
        {
            Type = storeType,
            ConnectionString = connectionString,
            DefaultCollection = collectionName
        };

        return services.AddVectorStore(config);
    }

    /// <summary>
    /// Register OpenAI Whisper speech-to-text service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="apiKey">OpenAI API key. If null, reads from OPENAI_API_KEY environment variable.</param>
    /// <param name="model">Whisper model to use (default: whisper-1).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddWhisperSpeechToText(
        this IServiceCollection services,
        string? apiKey = null,
        string model = "whisper-1")
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ISpeechToTextService>(sp =>
        {
            var key = apiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                ?? throw new InvalidOperationException("OpenAI API key required. Set OPENAI_API_KEY or pass apiKey parameter.");
            return new WhisperSpeechToTextService(key, model: model);
        });

        return services;
    }

    /// <summary>
    /// Register local Whisper speech-to-text service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="modelSize">Model size: tiny, base, small, medium, large.</param>
    /// <param name="whisperPath">Optional path to whisper executable.</param>
    /// <param name="modelPath">Optional path to model file.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddLocalWhisperSpeechToText(
        this IServiceCollection services,
        string modelSize = "base",
        string? whisperPath = null,
        string? modelPath = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ISpeechToTextService>(sp =>
            new LocalWhisperService(whisperPath, modelPath, modelSize));

        return services;
    }

    /// <summary>
    /// Register a custom speech-to-text service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="service">The speech-to-text service instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSpeechToText(
        this IServiceCollection services,
        ISpeechToTextService service)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(service);

        services.AddSingleton(service);
        return services;
    }

    /// <summary>
    /// Register OpenAI text-to-speech service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="apiKey">OpenAI API key. If null, reads from OPENAI_API_KEY environment variable.</param>
    /// <param name="model">TTS model to use: "tts-1" (faster) or "tts-1-hd" (higher quality).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddOpenAiTextToSpeech(
        this IServiceCollection services,
        string? apiKey = null,
        string model = "tts-1")
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ITextToSpeechService>(sp =>
        {
            string key = apiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                ?? throw new InvalidOperationException("OpenAI API key required. Set OPENAI_API_KEY or pass apiKey parameter.");
            return new OpenAiTextToSpeechService(key, model: model);
        });

        return services;
    }

    /// <summary>
    /// Register a custom text-to-speech service.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="service">The text-to-speech service instance.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTextToSpeech(
        this IServiceCollection services,
        ITextToSpeechService service)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(service);

        services.AddSingleton(service);
        return services;
    }

    /// <summary>
    /// Register both speech-to-text and text-to-speech services for bidirectional audio support.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="apiKey">OpenAI API key. If null, reads from OPENAI_API_KEY environment variable.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBidirectionalSpeech(
        this IServiceCollection services,
        string? apiKey = null)
    {
        return services
            .AddWhisperSpeechToText(apiKey)
            .AddOpenAiTextToSpeech(apiKey);
    }
}
