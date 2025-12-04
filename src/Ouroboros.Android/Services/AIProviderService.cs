namespace Ouroboros.Android.Services;

/// <summary>
/// Supported AI providers
/// </summary>
public enum AIProvider
{
    /// <summary>
    /// Local Ollama instance
    /// </summary>
    Ollama,

    /// <summary>
    /// OpenAI (GPT-3.5, GPT-4, etc.)
    /// </summary>
    OpenAI,

    /// <summary>
    /// Anthropic (Claude)
    /// </summary>
    Anthropic,

    /// <summary>
    /// Google AI (Gemini, PaLM)
    /// </summary>
    Google,

    /// <summary>
    /// Meta (LLaMA via hosted API)
    /// </summary>
    Meta,

    /// <summary>
    /// Cohere
    /// </summary>
    Cohere,

    /// <summary>
    /// Mistral AI
    /// </summary>
    Mistral,

    /// <summary>
    /// Hugging Face Inference API
    /// </summary>
    HuggingFace,

    /// <summary>
    /// Azure OpenAI Service
    /// </summary>
    AzureOpenAI
}

/// <summary>
/// Configuration for an AI provider
/// </summary>
public class AIProviderConfig
{
    /// <summary>
    /// Gets or sets the provider type
    /// </summary>
    public AIProvider Provider { get; set; }

    /// <summary>
    /// Gets or sets the API endpoint
    /// </summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the API key
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the organization ID (for providers that support it)
    /// </summary>
    public string? OrganizationId { get; set; }

    /// <summary>
    /// Gets or sets the project ID (for Google Cloud)
    /// </summary>
    public string? ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the region (for Azure)
    /// </summary>
    public string? Region { get; set; }

    /// <summary>
    /// Gets or sets the deployment name (for Azure OpenAI)
    /// </summary>
    public string? DeploymentName { get; set; }

    /// <summary>
    /// Gets or sets the default model to use
    /// </summary>
    public string? DefaultModel { get; set; }

    /// <summary>
    /// Gets or sets whether this provider is enabled
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets custom headers
    /// </summary>
    public Dictionary<string, string> CustomHeaders { get; set; } = new();

    /// <summary>
    /// Gets or sets the timeout in seconds
    /// </summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Gets or sets the max tokens for generation
    /// </summary>
    public int? MaxTokens { get; set; }

    /// <summary>
    /// Gets or sets the temperature for generation
    /// </summary>
    public double? Temperature { get; set; }

    /// <summary>
    /// Get default configuration for a provider
    /// </summary>
    public static AIProviderConfig GetDefault(AIProvider provider)
    {
        return provider switch
        {
            AIProvider.Ollama => new AIProviderConfig
            {
                Provider = AIProvider.Ollama,
                Endpoint = "http://localhost:11434",
                DefaultModel = "tinyllama"
            },
            AIProvider.OpenAI => new AIProviderConfig
            {
                Provider = AIProvider.OpenAI,
                Endpoint = "https://api.openai.com/v1",
                DefaultModel = "gpt-3.5-turbo"
            },
            AIProvider.Anthropic => new AIProviderConfig
            {
                Provider = AIProvider.Anthropic,
                Endpoint = "https://api.anthropic.com/v1",
                DefaultModel = "claude-3-haiku-20240307"
            },
            AIProvider.Google => new AIProviderConfig
            {
                Provider = AIProvider.Google,
                Endpoint = "https://generativelanguage.googleapis.com/v1",
                DefaultModel = "gemini-pro"
            },
            AIProvider.Meta => new AIProviderConfig
            {
                Provider = AIProvider.Meta,
                Endpoint = "https://api.together.xyz/v1",
                DefaultModel = "meta-llama/Llama-2-7b-chat-hf"
            },
            AIProvider.Cohere => new AIProviderConfig
            {
                Provider = AIProvider.Cohere,
                Endpoint = "https://api.cohere.ai/v1",
                DefaultModel = "command"
            },
            AIProvider.Mistral => new AIProviderConfig
            {
                Provider = AIProvider.Mistral,
                Endpoint = "https://api.mistral.ai/v1",
                DefaultModel = "mistral-tiny"
            },
            AIProvider.HuggingFace => new AIProviderConfig
            {
                Provider = AIProvider.HuggingFace,
                Endpoint = "https://api-inference.huggingface.co",
                DefaultModel = "mistralai/Mistral-7B-Instruct-v0.1"
            },
            AIProvider.AzureOpenAI => new AIProviderConfig
            {
                Provider = AIProvider.AzureOpenAI,
                Endpoint = "https://{resource-name}.openai.azure.com",
                DefaultModel = "gpt-35-turbo",
                Region = "eastus"
            },
            _ => throw new ArgumentException($"Unknown provider: {provider}")
        };
    }

    /// <summary>
    /// Get display name for provider
    /// </summary>
    public string GetDisplayName()
    {
        return Provider switch
        {
            AIProvider.Ollama => "Ollama (Local)",
            AIProvider.OpenAI => "OpenAI",
            AIProvider.Anthropic => "Anthropic (Claude)",
            AIProvider.Google => "Google AI (Gemini)",
            AIProvider.Meta => "Meta (LLaMA)",
            AIProvider.Cohere => "Cohere",
            AIProvider.Mistral => "Mistral AI",
            AIProvider.HuggingFace => "Hugging Face",
            AIProvider.AzureOpenAI => "Azure OpenAI",
            _ => Provider.ToString()
        };
    }

    /// <summary>
    /// Validate the configuration
    /// </summary>
    public (bool isValid, string? error) Validate()
    {
        if (string.IsNullOrWhiteSpace(Endpoint))
        {
            return (false, "Endpoint is required");
        }

        if (Provider != AIProvider.Ollama && string.IsNullOrWhiteSpace(ApiKey))
        {
            return (false, "API key is required for this provider");
        }

        if (Provider == AIProvider.AzureOpenAI && string.IsNullOrWhiteSpace(DeploymentName))
        {
            return (false, "Deployment name is required for Azure OpenAI");
        }

        if (Provider == AIProvider.Google && string.IsNullOrWhiteSpace(ProjectId))
        {
            return (false, "Project ID is required for Google AI");
        }

        return (true, null);
    }
}

/// <summary>
/// Service for managing AI provider configurations
/// </summary>
public class AIProviderService
{
    private const string ConfigKeyPrefix = "ai_provider_config_";
    private const string ActiveProviderKey = "active_ai_provider";

    /// <summary>
    /// Get all configured providers
    /// </summary>
    public List<AIProviderConfig> GetAllProviders()
    {
        var providers = new List<AIProviderConfig>();

        foreach (AIProvider provider in Enum.GetValues(typeof(AIProvider)))
        {
            var config = GetProviderConfig(provider);
            if (config != null)
            {
                providers.Add(config);
            }
        }

        return providers;
    }

    /// <summary>
    /// Get configuration for a specific provider
    /// </summary>
    public AIProviderConfig? GetProviderConfig(AIProvider provider)
    {
        var key = $"{ConfigKeyPrefix}{provider}";
        var json = Preferences.Get(key, string.Empty);

        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<AIProviderConfig>(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Save provider configuration
    /// </summary>
    public void SaveProviderConfig(AIProviderConfig config)
    {
        var key = $"{ConfigKeyPrefix}{config.Provider}";
        var json = System.Text.Json.JsonSerializer.Serialize(config);
        Preferences.Set(key, json);
    }

    /// <summary>
    /// Get the active provider
    /// </summary>
    public AIProvider GetActiveProvider()
    {
        var activeStr = Preferences.Get(ActiveProviderKey, AIProvider.Ollama.ToString());
        return Enum.TryParse<AIProvider>(activeStr, out var provider) 
            ? provider 
            : AIProvider.Ollama;
    }

    /// <summary>
    /// Set the active provider
    /// </summary>
    public void SetActiveProvider(AIProvider provider)
    {
        Preferences.Set(ActiveProviderKey, provider.ToString());
    }

    /// <summary>
    /// Get active provider configuration
    /// </summary>
    public AIProviderConfig GetActiveProviderConfig()
    {
        var activeProvider = GetActiveProvider();
        var config = GetProviderConfig(activeProvider);
        
        return config ?? AIProviderConfig.GetDefault(activeProvider);
    }

    /// <summary>
    /// Delete provider configuration
    /// </summary>
    public void DeleteProviderConfig(AIProvider provider)
    {
        var key = $"{ConfigKeyPrefix}{provider}";
        Preferences.Remove(key);
    }

    /// <summary>
    /// Reset to defaults
    /// </summary>
    public void ResetToDefaults()
    {
        foreach (AIProvider provider in Enum.GetValues(typeof(AIProvider)))
        {
            DeleteProviderConfig(provider);
        }
        SetActiveProvider(AIProvider.Ollama);
    }
}
