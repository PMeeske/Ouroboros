using LangChain.Providers.Ollama;
using Microsoft.Extensions.DependencyInjection;

namespace LangChainPipeline.Providers;

/// <summary>
/// Dependency injection helpers for registering chat and embedding models.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Register an interchangeable chat + embedding stack that prefers remote OpenAI-compatible
    /// endpoints when configured, falling back to local Ollama with deterministic embeddings otherwise.
    /// </summary>
    public static IServiceCollection AddInterchangeableLlm(this IServiceCollection services, string? model = null, string? embed = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        model = string.IsNullOrWhiteSpace(model) ? "llama3" : model;
        embed = string.IsNullOrWhiteSpace(embed) ? "nomic-embed-text" : embed;

        services.AddSingleton<OllamaProvider>();

        services.AddSingleton<IChatCompletionModel>(sp =>
        {
            var (endpoint, apiKey, endpointType) = ChatConfig.Resolve();
            if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey))
            {
                try
                {
                    return endpointType switch
                    {
                        ChatEndpointType.OllamaCloud => new OllamaCloudChatModel(endpoint!, apiKey!, model!),
                        ChatEndpointType.OpenAiCompatible => new HttpOpenAiCompatibleChatModel(endpoint!, apiKey!, model!),
                        ChatEndpointType.Auto => new HttpOpenAiCompatibleChatModel(endpoint!, apiKey!, model!),
                        _ => new HttpOpenAiCompatibleChatModel(endpoint!, apiKey!, model!)
                    };
                }
                catch
                {
                    // Ignore and fall back to local Ollama below.
                }
            }

            var provider = sp.GetRequiredService<OllamaProvider>();
            var chat = new OllamaChatModel(provider, model!);
            if (string.Equals(model, "deepseek-coder:33b", StringComparison.OrdinalIgnoreCase))
            {
                chat.Settings = OllamaPresets.DeepSeekCoder33B;
            }
            return new OllamaChatAdapter(chat);
        });

        services.AddSingleton<IEmbeddingModel>(sp =>
        {
            var provider = sp.GetRequiredService<OllamaProvider>();
            return new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, embed!));
        });

        services.AddSingleton<ToolRegistry>();
        services.AddSingleton(sp =>
        {
            var registry = sp.GetRequiredService<ToolRegistry>();
            var chat = sp.GetRequiredService<IChatCompletionModel>();
            return new ToolAwareChatModel(chat, registry);
        });

        return services;
    }
}
