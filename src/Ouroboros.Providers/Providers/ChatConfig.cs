#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using Microsoft.Extensions.Configuration;

namespace Ouroboros.Providers;

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
    OllamaCloud,
    /// <summary>
    /// LiteLLM proxy with OpenAI-compatible chat completions
    /// </summary>
    LiteLLM,
    /// <summary>
    /// GitHub Models API with OpenAI-compatible chat completions
    /// </summary>
    GitHubModels,
    /// <summary>
    /// Anthropic Claude API (native Messages API format)
    /// </summary>
    Anthropic
}

/// <summary>
/// Resolves optional remote chat configuration from environment variables and IConfiguration.
/// This keeps the public surface that the CLI expects without forcing callers
/// to set any secrets during local development.
/// </summary>
public static class ChatConfig
{
    private const string EndpointEnv = "CHAT_ENDPOINT";
    private const string ApiKeyEnv = "CHAT_API_KEY";
    private const string EndpointTypeEnv = "CHAT_ENDPOINT_TYPE";

    private static IConfiguration? _configuration;

    /// <summary>
    /// Sets the IConfiguration instance for resolving secrets from user secrets/appsettings.
    /// Call this early in application startup.
    /// </summary>
    public static void SetConfiguration(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public static (string? Endpoint, string? ApiKey, ChatEndpointType EndpointType) Resolve()
    {
        return ResolveWithOverrides(null, null, null);
    }

    public static (string? Endpoint, string? ApiKey, ChatEndpointType EndpointType) ResolveWithOverrides(
        string? endpointOverride = null,
        string? apiKeyOverride = null,
        string? endpointTypeOverride = null)
    {
        string? endpoint = endpointOverride ?? GetConfigValue(EndpointEnv);
        string? apiKey = apiKeyOverride ?? GetConfigValue(ApiKeyEnv);
        string? endpointTypeStr = endpointTypeOverride ?? GetConfigValue(EndpointTypeEnv);

        ChatEndpointType endpointType = ChatEndpointType.Auto;
        if (!string.IsNullOrWhiteSpace(endpointTypeStr) &&
            Enum.TryParse<ChatEndpointType>(endpointTypeStr, true, out ChatEndpointType parsedType))
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
        else if (!string.IsNullOrWhiteSpace(endpointTypeStr) &&
                 endpointTypeStr.Equals("litellm", StringComparison.OrdinalIgnoreCase))
        {
            endpointType = ChatEndpointType.LiteLLM;
        }
        else if (!string.IsNullOrWhiteSpace(endpointTypeStr) &&
                 (endpointTypeStr.Equals("github-models", StringComparison.OrdinalIgnoreCase) ||
                  endpointTypeStr.Equals("github", StringComparison.OrdinalIgnoreCase)))
        {
            endpointType = ChatEndpointType.GitHubModels;
        }
        else if (!string.IsNullOrWhiteSpace(endpointTypeStr) &&
                 (endpointTypeStr.Equals("anthropic", StringComparison.OrdinalIgnoreCase) ||
                  endpointTypeStr.Equals("claude", StringComparison.OrdinalIgnoreCase)))
        {
            endpointType = ChatEndpointType.Anthropic;
        }

        // Auto-detect endpoint type based on URL if type is Auto
        if (endpointType == ChatEndpointType.Auto && !string.IsNullOrWhiteSpace(endpoint))
        {
            if (endpoint.Contains("api.ollama.com", StringComparison.OrdinalIgnoreCase) ||
                endpoint.Contains("ollama.cloud", StringComparison.OrdinalIgnoreCase))
            {
                endpointType = ChatEndpointType.OllamaCloud;
            }
            else if (endpoint.Contains("litellm", StringComparison.OrdinalIgnoreCase) ||
                     endpoint.Contains("3asabc.de", StringComparison.OrdinalIgnoreCase))
            {
                endpointType = ChatEndpointType.LiteLLM;
            }
            else if (endpoint.Contains("models.inference.ai.azure.com", StringComparison.OrdinalIgnoreCase))
            {
                endpointType = ChatEndpointType.GitHubModels;
            }
            else if (endpoint.Contains("anthropic.com", StringComparison.OrdinalIgnoreCase))
            {
                endpointType = ChatEndpointType.Anthropic;
            }
            else
            {
                endpointType = ChatEndpointType.OpenAiCompatible;
            }
        }

        // Fallback to provider-specific API key from IConfiguration or environment variables
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = endpointType switch
            {
                ChatEndpointType.Anthropic => GetConfigValue("ANTHROPIC_API_KEY"),
                ChatEndpointType.GitHubModels => GetConfigValue("GITHUB_TOKEN")
                                                 ?? GetConfigValue("GITHUB_MODELS_TOKEN"),
                _ => null
            };
        }

        // For Anthropic, default endpoint if not specified
        if (endpointType == ChatEndpointType.Anthropic && string.IsNullOrWhiteSpace(endpoint))
        {
            endpoint = "https://api.anthropic.com";
        }

        return (string.IsNullOrWhiteSpace(endpoint) ? null : endpoint,
            string.IsNullOrWhiteSpace(apiKey) ? null : apiKey,
            endpointType);
    }

    /// <summary>
    /// Gets a configuration value from IConfiguration first (includes user secrets),
    /// then falls back to environment variables.
    /// </summary>
    private static string? GetConfigValue(string key)
    {
        // Try IConfiguration first (includes user secrets, appsettings, etc.)
        string? value = _configuration?[key];
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        // Fallback to environment variable
        return Environment.GetEnvironmentVariable(key);
    }
}
