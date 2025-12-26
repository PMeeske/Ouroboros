using LangChain.Providers;
using LangChain.Providers.Ollama;
using Ouroboros.Domain;
using Ouroboros.Providers;
using IEmbeddingModel = Ouroboros.Domain.IEmbeddingModel;

namespace Ouroboros.Application.Services;

public static class ServiceFactory
{
    // Helper method to create the appropriate remote chat model based on endpoint type
    public static IChatCompletionModel CreateRemoteChatModel(string endpoint, string apiKey, string modelName, ChatRuntimeSettings? settings, ChatEndpointType endpointType)
    {
        return endpointType switch
        {
            ChatEndpointType.OllamaCloud => new OllamaCloudChatModel(endpoint, apiKey, modelName, settings),
            ChatEndpointType.LiteLLM => new LiteLLMChatModel(endpoint, apiKey, modelName, settings),
            ChatEndpointType.GitHubModels => new GitHubModelsChatModel(apiKey, modelName, endpoint, settings),
            ChatEndpointType.OpenAiCompatible => new HttpOpenAiCompatibleChatModel(endpoint, apiKey, modelName, settings),
            ChatEndpointType.Auto => new HttpOpenAiCompatibleChatModel(endpoint, apiKey, modelName, settings), // Default to OpenAI-compatible for auto
            _ => new HttpOpenAiCompatibleChatModel(endpoint, apiKey, modelName, settings)
        };
    }

    // Helper method to create the appropriate remote embedding model based on endpoint type
    public static IEmbeddingModel CreateEmbeddingModel(string? endpoint, string? apiKey, ChatEndpointType endpointType, string embedName, OllamaProvider provider)
    {
        if (!string.IsNullOrWhiteSpace(endpoint) && !string.IsNullOrWhiteSpace(apiKey))
        {
            return endpointType switch
            {
                ChatEndpointType.OllamaCloud => new OllamaCloudEmbeddingModel(endpoint, apiKey, embedName),
                ChatEndpointType.LiteLLM => new LiteLLMEmbeddingModel(endpoint, apiKey, embedName),
                _ => new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, embedName)) // Fall back to local for OpenAI-compatible (no standard embedding endpoint)
            };
        }
        return new OllamaEmbeddingAdapter(new OllamaEmbeddingModel(provider, embedName));
    }
}

