// <copyright file="ServiceCollectionExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Providers;

using LangChain.Providers.Ollama;
using Microsoft.Extensions.DependencyInjection;

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
            (string endpoint, string apiKey, ChatEndpointType endpointType) = ChatConfig.Resolve();
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
}
