namespace LangChainPipeline.Providers;

/// <summary>
/// Resolves optional remote chat configuration from environment variables.
/// This keeps the public surface that the CLI expects without forcing callers
/// to set any secrets during local development.
/// </summary>
public static class ChatConfig
{
    private const string EndpointEnv = "CHAT_ENDPOINT";
    private const string ApiKeyEnv = "CHAT_API_KEY";

    public static (string? Endpoint, string? ApiKey) Resolve()
    {
        string? endpoint = Environment.GetEnvironmentVariable(EndpointEnv);
        string? apiKey = Environment.GetEnvironmentVariable(ApiKeyEnv);
        return (string.IsNullOrWhiteSpace(endpoint) ? null : endpoint,
            string.IsNullOrWhiteSpace(apiKey) ? null : apiKey);
    }
}
