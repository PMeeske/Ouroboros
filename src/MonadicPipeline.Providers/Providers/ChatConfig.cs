namespace LangChainPipeline.Providers;

/// <summary>
/// Represents the type of remote chat endpoint being used.
/// </summary>
public enum ChatEndpointType
{
    /// <summary>
    /// Auto-detect endpoint type based on URL or use OpenAI-compatible as default
    /// </summary>
    Auto,
    /// <summary>
    /// OpenAI-compatible API (original behavior)
    /// </summary>
    OpenAiCompatible,
    /// <summary>
    /// Ollama Cloud API format
    /// </summary>
    OllamaCloud
}

/// <summary>
/// Resolves optional remote chat configuration from environment variables.
/// This keeps the public surface that the CLI expects without forcing callers
/// to set any secrets during local development.
/// </summary>
public static class ChatConfig
{
    private const string EndpointEnv = "CHAT_ENDPOINT";
    private const string ApiKeyEnv = "CHAT_API_KEY";
    private const string EndpointTypeEnv = "CHAT_ENDPOINT_TYPE";

    public static (string? Endpoint, string? ApiKey, ChatEndpointType EndpointType) Resolve()
    {
        return ResolveWithOverrides(null, null, null);
    }

    public static (string? Endpoint, string? ApiKey, ChatEndpointType EndpointType) ResolveWithOverrides(
        string? endpointOverride = null,
        string? apiKeyOverride = null,
        string? endpointTypeOverride = null)
    {
        string? endpoint = endpointOverride ?? Environment.GetEnvironmentVariable(EndpointEnv);
        string? apiKey = apiKeyOverride ?? Environment.GetEnvironmentVariable(ApiKeyEnv);
        string? endpointTypeStr = endpointTypeOverride ?? Environment.GetEnvironmentVariable(EndpointTypeEnv);

        var endpointType = ChatEndpointType.Auto;
        if (!string.IsNullOrWhiteSpace(endpointTypeStr) &&
            Enum.TryParse<ChatEndpointType>(endpointTypeStr, true, out var parsedType))
        {
            endpointType = parsedType;
        }
        else if (!string.IsNullOrWhiteSpace(endpointTypeStr) &&
                 endpointTypeStr.Equals("openai", StringComparison.OrdinalIgnoreCase))
        {
            endpointType = ChatEndpointType.OpenAiCompatible;
        }
        else if (!string.IsNullOrWhiteSpace(endpointTypeStr) &&
                 endpointTypeStr.Equals("ollama-cloud", StringComparison.OrdinalIgnoreCase))
        {
            endpointType = ChatEndpointType.OllamaCloud;
        }

        // Auto-detect Ollama Cloud based on endpoint URL if type is Auto
        if (endpointType == ChatEndpointType.Auto && !string.IsNullOrWhiteSpace(endpoint))
        {
            if (endpoint.Contains("api.ollama.com", StringComparison.OrdinalIgnoreCase) ||
                endpoint.Contains("ollama.cloud", StringComparison.OrdinalIgnoreCase))
            {
                endpointType = ChatEndpointType.OllamaCloud;
            }
            else
            {
                endpointType = ChatEndpointType.OpenAiCompatible;
            }
        }

        return (string.IsNullOrWhiteSpace(endpoint) ? null : endpoint,
            string.IsNullOrWhiteSpace(apiKey) ? null : apiKey,
            endpointType);
    }
}
