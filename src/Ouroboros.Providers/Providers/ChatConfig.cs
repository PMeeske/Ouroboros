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
    /// Native OpenAI API (api.openai.com)
    /// </summary>
    OpenAI,
    /// <summary>
    /// Ollama Cloud API format
    /// </summary>
    OllamaCloud,
    /// <summary>
    /// Local Ollama instance
    /// </summary>
    OllamaLocal,
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
    Anthropic,
    /// <summary>
    /// Azure OpenAI Service
    /// </summary>
    AzureOpenAI,
    /// <summary>
    /// Google AI (Gemini) API
    /// </summary>
    Google,
    /// <summary>
    /// Mistral AI API
    /// </summary>
    Mistral,
    /// <summary>
    /// DeepSeek API
    /// </summary>
    DeepSeek,
    /// <summary>
    /// Cohere API
    /// </summary>
    Cohere,
    /// <summary>
    /// Groq API (fast inference)
    /// </summary>
    Groq,
    /// <summary>
    /// Together AI API
    /// </summary>
    Together,
    /// <summary>
    /// Fireworks AI API
    /// </summary>
    Fireworks,
    /// <summary>
    /// Perplexity AI API
    /// </summary>
    Perplexity,
    /// <summary>
    /// Replicate API
    /// </summary>
    Replicate,
    /// <summary>
    /// HuggingFace Inference API
    /// </summary>
    HuggingFace
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
    /// Provider-specific API key environment variable names.
    /// </summary>
    private static readonly Dictionary<ChatEndpointType, string[]> ProviderApiKeyEnvVars = new()
    {
        [ChatEndpointType.Anthropic] = ["ANTHROPIC_API_KEY", "CLAUDE_API_KEY"],
        [ChatEndpointType.OpenAI] = ["OPENAI_API_KEY"],
        [ChatEndpointType.AzureOpenAI] = ["AZURE_OPENAI_API_KEY", "AZURE_OPENAI_KEY"],
        [ChatEndpointType.Google] = ["GOOGLE_API_KEY", "GEMINI_API_KEY"],
        [ChatEndpointType.Mistral] = ["MISTRAL_API_KEY"],
        [ChatEndpointType.DeepSeek] = ["DEEPSEEK_API_KEY"],
        [ChatEndpointType.Cohere] = ["COHERE_API_KEY", "CO_API_KEY"],
        [ChatEndpointType.Groq] = ["GROQ_API_KEY"],
        [ChatEndpointType.Together] = ["TOGETHER_API_KEY", "TOGETHERAI_API_KEY"],
        [ChatEndpointType.Fireworks] = ["FIREWORKS_API_KEY"],
        [ChatEndpointType.Perplexity] = ["PERPLEXITY_API_KEY", "PPLX_API_KEY"],
        [ChatEndpointType.Replicate] = ["REPLICATE_API_TOKEN", "REPLICATE_API_KEY"],
        [ChatEndpointType.HuggingFace] = ["HUGGINGFACE_API_KEY", "HF_TOKEN", "HF_API_KEY"],
        [ChatEndpointType.GitHubModels] = ["GITHUB_TOKEN", "GITHUB_MODELS_TOKEN", "GH_TOKEN"],
        [ChatEndpointType.OllamaCloud] = ["OLLAMA_API_KEY", "OLLAMA_CLOUD_API_KEY"],
    };

    /// <summary>
    /// Default endpoints for providers.
    /// </summary>
    private static readonly Dictionary<ChatEndpointType, string> DefaultEndpoints = new()
    {
        [ChatEndpointType.Anthropic] = "https://api.anthropic.com",
        [ChatEndpointType.OpenAI] = "https://api.openai.com",
        [ChatEndpointType.Google] = "https://generativelanguage.googleapis.com",
        [ChatEndpointType.Mistral] = "https://api.mistral.ai",
        [ChatEndpointType.DeepSeek] = "https://api.deepseek.com",
        [ChatEndpointType.Cohere] = "https://api.cohere.ai",
        [ChatEndpointType.Groq] = "https://api.groq.com/openai",
        [ChatEndpointType.Together] = "https://api.together.xyz",
        [ChatEndpointType.Fireworks] = "https://api.fireworks.ai/inference",
        [ChatEndpointType.Perplexity] = "https://api.perplexity.ai",
        [ChatEndpointType.Replicate] = "https://api.replicate.com",
        [ChatEndpointType.HuggingFace] = "https://api-inference.huggingface.co",
        [ChatEndpointType.GitHubModels] = "https://models.inference.ai.azure.com",
        [ChatEndpointType.OllamaLocal] = "http://localhost:11434",
    };

    /// <summary>
    /// Endpoint type aliases for more flexible configuration.
    /// </summary>
    private static readonly Dictionary<string, ChatEndpointType> EndpointTypeAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        // OpenAI
        ["openai"] = ChatEndpointType.OpenAI,
        ["gpt"] = ChatEndpointType.OpenAI,
        ["chatgpt"] = ChatEndpointType.OpenAI,
        ["openai-compatible"] = ChatEndpointType.OpenAiCompatible,
        ["openai_compatible"] = ChatEndpointType.OpenAiCompatible,

        // Anthropic
        ["anthropic"] = ChatEndpointType.Anthropic,
        ["claude"] = ChatEndpointType.Anthropic,

        // Ollama
        ["ollama"] = ChatEndpointType.OllamaLocal,
        ["ollama-local"] = ChatEndpointType.OllamaLocal,
        ["ollama_local"] = ChatEndpointType.OllamaLocal,
        ["ollama-cloud"] = ChatEndpointType.OllamaCloud,
        ["ollama_cloud"] = ChatEndpointType.OllamaCloud,

        // Azure
        ["azure"] = ChatEndpointType.AzureOpenAI,
        ["azure-openai"] = ChatEndpointType.AzureOpenAI,
        ["azure_openai"] = ChatEndpointType.AzureOpenAI,
        ["azureopenai"] = ChatEndpointType.AzureOpenAI,

        // Google
        ["google"] = ChatEndpointType.Google,
        ["gemini"] = ChatEndpointType.Google,
        ["palm"] = ChatEndpointType.Google,
        ["vertex"] = ChatEndpointType.Google,

        // Mistral
        ["mistral"] = ChatEndpointType.Mistral,
        ["mistralai"] = ChatEndpointType.Mistral,

        // DeepSeek
        ["deepseek"] = ChatEndpointType.DeepSeek,

        // Cohere
        ["cohere"] = ChatEndpointType.Cohere,
        ["command"] = ChatEndpointType.Cohere,

        // Groq
        ["groq"] = ChatEndpointType.Groq,

        // Together
        ["together"] = ChatEndpointType.Together,
        ["togetherai"] = ChatEndpointType.Together,
        ["together-ai"] = ChatEndpointType.Together,

        // Fireworks
        ["fireworks"] = ChatEndpointType.Fireworks,
        ["fireworksai"] = ChatEndpointType.Fireworks,

        // Perplexity
        ["perplexity"] = ChatEndpointType.Perplexity,
        ["pplx"] = ChatEndpointType.Perplexity,

        // Replicate
        ["replicate"] = ChatEndpointType.Replicate,

        // HuggingFace
        ["huggingface"] = ChatEndpointType.HuggingFace,
        ["hf"] = ChatEndpointType.HuggingFace,

        // GitHub Models
        ["github"] = ChatEndpointType.GitHubModels,
        ["github-models"] = ChatEndpointType.GitHubModels,
        ["github_models"] = ChatEndpointType.GitHubModels,
        ["gh-models"] = ChatEndpointType.GitHubModels,

        // LiteLLM
        ["litellm"] = ChatEndpointType.LiteLLM,
        ["lite-llm"] = ChatEndpointType.LiteLLM,
    };

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

        // Parse endpoint type from string
        ChatEndpointType endpointType = ParseEndpointType(endpointTypeStr);

        // Auto-detect endpoint type based on URL if type is Auto
        if (endpointType == ChatEndpointType.Auto && !string.IsNullOrWhiteSpace(endpoint))
        {
            endpointType = DetectEndpointTypeFromUrl(endpoint);
        }

        // Fallback to provider-specific API key from IConfiguration or environment variables
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            apiKey = GetProviderApiKey(endpointType);
        }

        // Set default endpoint if not specified and we know the provider
        if (string.IsNullOrWhiteSpace(endpoint) && DefaultEndpoints.TryGetValue(endpointType, out string? defaultEndpoint))
        {
            endpoint = defaultEndpoint;
        }

        return (string.IsNullOrWhiteSpace(endpoint) ? null : endpoint,
            string.IsNullOrWhiteSpace(apiKey) ? null : apiKey,
            endpointType);
    }

    /// <summary>
    /// Parse endpoint type string to enum, supporting aliases.
    /// </summary>
    private static ChatEndpointType ParseEndpointType(string? endpointTypeStr)
    {
        if (string.IsNullOrWhiteSpace(endpointTypeStr))
            return ChatEndpointType.Auto;

        // Try direct enum parse first
        if (Enum.TryParse<ChatEndpointType>(endpointTypeStr, true, out ChatEndpointType parsedType))
            return parsedType;

        // Try alias lookup
        if (EndpointTypeAliases.TryGetValue(endpointTypeStr, out ChatEndpointType aliasType))
            return aliasType;

        return ChatEndpointType.Auto;
    }

    /// <summary>
    /// Detect endpoint type from URL patterns.
    /// </summary>
    private static ChatEndpointType DetectEndpointTypeFromUrl(string endpoint)
    {
        string url = endpoint.ToLowerInvariant();

        // === ANTHROPIC ===
        if (url.Contains("anthropic.com") || url.Contains("claude.ai"))
            return ChatEndpointType.Anthropic;

        // === OPENAI ===
        if (url.Contains("api.openai.com"))
            return ChatEndpointType.OpenAI;

        // === AZURE OPENAI ===
        if (url.Contains(".openai.azure.com") || url.Contains("azure.com/openai"))
            return ChatEndpointType.AzureOpenAI;

        // === GOOGLE ===
        if (url.Contains("googleapis.com") ||
            url.Contains("generativelanguage.googleapis.com") ||
            url.Contains("aiplatform.googleapis.com") ||
            url.Contains("vertexai"))
            return ChatEndpointType.Google;

        // === MISTRAL ===
        if (url.Contains("mistral.ai") || url.Contains("api.mistral"))
            return ChatEndpointType.Mistral;

        // === DEEPSEEK ===
        if (url.Contains("deepseek.com") || url.Contains("api.deepseek"))
            return ChatEndpointType.DeepSeek;

        // === COHERE ===
        if (url.Contains("cohere.ai") || url.Contains("api.cohere"))
            return ChatEndpointType.Cohere;

        // === GROQ ===
        if (url.Contains("groq.com") || url.Contains("api.groq"))
            return ChatEndpointType.Groq;

        // === TOGETHER ===
        if (url.Contains("together.xyz") || url.Contains("api.together"))
            return ChatEndpointType.Together;

        // === FIREWORKS ===
        if (url.Contains("fireworks.ai") || url.Contains("api.fireworks"))
            return ChatEndpointType.Fireworks;

        // === PERPLEXITY ===
        if (url.Contains("perplexity.ai") || url.Contains("api.perplexity"))
            return ChatEndpointType.Perplexity;

        // === REPLICATE ===
        if (url.Contains("replicate.com") || url.Contains("api.replicate"))
            return ChatEndpointType.Replicate;

        // === HUGGINGFACE ===
        if (url.Contains("huggingface.co") ||
            url.Contains("hf.co") ||
            url.Contains("api-inference.huggingface"))
            return ChatEndpointType.HuggingFace;

        // === GITHUB MODELS ===
        if (url.Contains("models.inference.ai.azure.com") ||
            url.Contains("github.com/models"))
            return ChatEndpointType.GitHubModels;

        // === OLLAMA ===
        if (url.Contains("api.ollama.com") || url.Contains("ollama.cloud"))
            return ChatEndpointType.OllamaCloud;

        if (url.Contains("localhost") || url.Contains("127.0.0.1") || url.Contains("0.0.0.0"))
        {
            // Check for common Ollama port
            if (url.Contains(":11434"))
                return ChatEndpointType.OllamaLocal;
        }

        // === LITELLM ===
        if (url.Contains("litellm"))
            return ChatEndpointType.LiteLLM;

        // Default to OpenAI-compatible
        return ChatEndpointType.OpenAiCompatible;
    }

    /// <summary>
    /// Get API key for provider from known environment variables.
    /// </summary>
    private static string? GetProviderApiKey(ChatEndpointType endpointType)
    {
        if (!ProviderApiKeyEnvVars.TryGetValue(endpointType, out string[]? envVars))
            return null;

        foreach (string envVar in envVars)
        {
            string? value = GetConfigValue(envVar);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
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

    /// <summary>
    /// Get the default endpoint for a provider, if known.
    /// </summary>
    public static string? GetDefaultEndpoint(ChatEndpointType endpointType)
    {
        return DefaultEndpoints.TryGetValue(endpointType, out string? endpoint) ? endpoint : null;
    }

    /// <summary>
    /// Get all known endpoint type aliases.
    /// </summary>
    public static IReadOnlyDictionary<string, ChatEndpointType> GetAliases() => EndpointTypeAliases;
}
