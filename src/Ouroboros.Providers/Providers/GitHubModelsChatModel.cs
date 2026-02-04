#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
namespace Ouroboros.Providers;

/// <summary>
/// HTTP client for GitHub Models API that provides access to various AI models
/// (GPT-4o, o1-preview, Claude 3.5 Sonnet, Llama 3.1, Mistral, etc.) through
/// an OpenAI-compatible API using GitHub Personal Access Token authentication.
/// Includes Polly exponential backoff retry policy to handle rate limiting.
/// </summary>
public sealed class GitHubModelsChatModel : OpenAiCompatibleChatModelBase
{
    private const string DefaultEndpoint = "https://models.inference.ai.azure.com";

    /// <summary>
    /// Initializes a new instance of the <see cref="GitHubModelsChatModel"/> class.
    /// </summary>
    /// <param name="githubToken">GitHub Personal Access Token for authentication.</param>
    /// <param name="model">Model name (e.g., gpt-4o, o1-preview, claude-3-5-sonnet, llama-3.1-70b-instruct, mistral-large).</param>
    /// <param name="endpoint">Optional endpoint URL (defaults to https://models.inference.ai.azure.com).</param>
    /// <param name="settings">Optional runtime settings for temperature, max tokens, etc.</param>
    /// <param name="costTracker">Optional cost tracker for monitoring usage and costs.</param>
    public GitHubModelsChatModel(string githubToken, string model, string? endpoint = null, ChatRuntimeSettings? settings = null, LlmCostTracker? costTracker = null)
        : base(
            endpoint: !string.IsNullOrWhiteSpace(endpoint)
                ? endpoint
                : Environment.GetEnvironmentVariable("GITHUB_MODELS_ENDPOINT") ?? DefaultEndpoint,
            apiKey: githubToken ?? throw new ArgumentException("GitHub token is required", nameof(githubToken)),
            model: model ?? throw new ArgumentException("Model name is required", nameof(model)),
            providerName: "GitHubModelsChatModel",
            settings: settings,
            costTracker: costTracker)
    {
    }

    /// <summary>
    /// Factory method to create a GitHubModelsChatModel from environment variables.
    /// Uses MODEL_TOKEN, GITHUB_TOKEN, or GITHUB_MODELS_TOKEN for authentication (in that order).
    /// </summary>
    /// <param name="model">Model name.</param>
    /// <param name="settings">Optional runtime settings.</param>
    /// <returns>A configured GitHubModelsChatModel instance.</returns>
    public static GitHubModelsChatModel FromEnvironment(string model, ChatRuntimeSettings? settings = null)
    {
        string? token = Environment.GetEnvironmentVariable("MODEL_TOKEN")
                       ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN") 
                       ?? Environment.GetEnvironmentVariable("GITHUB_MODELS_TOKEN");
        
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                "GitHub token not found. Set MODEL_TOKEN, GITHUB_TOKEN, or GITHUB_MODELS_TOKEN environment variable.");
        }

        string? endpoint = Environment.GetEnvironmentVariable("GITHUB_MODELS_ENDPOINT");
        return new GitHubModelsChatModel(token, model, endpoint, settings);
    }

    /// <inheritdoc/>
    protected override string GetFallbackMessage(string prompt)
    {
        return $"[github-models-fallback] {prompt}";
    }
}
