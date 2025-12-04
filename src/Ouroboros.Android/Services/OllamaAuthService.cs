namespace Ouroboros.Android.Services;

/// <summary>
/// Service for securely storing and retrieving Ollama credentials
/// </summary>
public class OllamaAuthService
{
    private const string ApiKeyKey = "ollama_api_key";
    private const string UsernameKey = "ollama_username";
    private const string PasswordKey = "ollama_password";
    private const string AuthTypeKey = "ollama_auth_type";

    /// <summary>
    /// Authentication type
    /// </summary>
    public enum AuthType
    {
        /// <summary>
        /// No authentication
        /// </summary>
        None,

        /// <summary>
        /// API key (Bearer token) authentication
        /// </summary>
        ApiKey,

        /// <summary>
        /// Basic authentication (username/password)
        /// </summary>
        Basic
    }

    /// <summary>
    /// Save API key authentication
    /// </summary>
    /// <param name="apiKey">The API key</param>
    /// <returns>Task representing the operation</returns>
    public async Task SaveApiKeyAsync(string apiKey)
    {
        try
        {
            await SecureStorage.SetAsync(ApiKeyKey, apiKey);
            await SecureStorage.SetAsync(AuthTypeKey, AuthType.ApiKey.ToString());
            
            // Clear basic auth if it was set
            SecureStorage.Remove(UsernameKey);
            SecureStorage.Remove(PasswordKey);
        }
        catch (Exception ex)
        {
            throw new OllamaAuthException($"Failed to save API key: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Save basic authentication credentials
    /// </summary>
    /// <param name="username">Username</param>
    /// <param name="password">Password</param>
    /// <returns>Task representing the operation</returns>
    public async Task SaveBasicAuthAsync(string username, string password)
    {
        try
        {
            await SecureStorage.SetAsync(UsernameKey, username);
            await SecureStorage.SetAsync(PasswordKey, password);
            await SecureStorage.SetAsync(AuthTypeKey, AuthType.Basic.ToString());
            
            // Clear API key if it was set
            SecureStorage.Remove(ApiKeyKey);
        }
        catch (Exception ex)
        {
            throw new OllamaAuthException($"Failed to save credentials: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Get the current authentication type
    /// </summary>
    /// <returns>The authentication type</returns>
    public async Task<AuthType> GetAuthTypeAsync()
    {
        try
        {
            var authTypeStr = await SecureStorage.GetAsync(AuthTypeKey);
            
            if (string.IsNullOrEmpty(authTypeStr))
            {
                return AuthType.None;
            }

            return Enum.TryParse<AuthType>(authTypeStr, out var authType) 
                ? authType 
                : AuthType.None;
        }
        catch
        {
            return AuthType.None;
        }
    }

    /// <summary>
    /// Get stored API key
    /// </summary>
    /// <returns>The API key or null if not set</returns>
    public async Task<string?> GetApiKeyAsync()
    {
        try
        {
            var authType = await GetAuthTypeAsync();
            
            if (authType != AuthType.ApiKey)
            {
                return null;
            }

            return await SecureStorage.GetAsync(ApiKeyKey);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get stored basic auth credentials
    /// </summary>
    /// <returns>Tuple of username and password, or null if not set</returns>
    public async Task<(string? username, string? password)?> GetBasicAuthAsync()
    {
        try
        {
            var authType = await GetAuthTypeAsync();
            
            if (authType != AuthType.Basic)
            {
                return null;
            }

            var username = await SecureStorage.GetAsync(UsernameKey);
            var password = await SecureStorage.GetAsync(PasswordKey);

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                return null;
            }

            return (username, password);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Clear all stored authentication
    /// </summary>
    public void ClearAuthentication()
    {
        SecureStorage.Remove(ApiKeyKey);
        SecureStorage.Remove(UsernameKey);
        SecureStorage.Remove(PasswordKey);
        SecureStorage.Remove(AuthTypeKey);
    }

    /// <summary>
    /// Apply stored authentication to an Ollama service
    /// </summary>
    /// <param name="ollamaService">The Ollama service to configure</param>
    /// <returns>Task representing the operation</returns>
    public async Task ApplyAuthenticationAsync(OllamaService ollamaService)
    {
        var authType = await GetAuthTypeAsync();

        switch (authType)
        {
            case AuthType.ApiKey:
                var apiKey = await GetApiKeyAsync();
                if (!string.IsNullOrEmpty(apiKey))
                {
                    ollamaService.SetApiKey(apiKey);
                }
                break;

            case AuthType.Basic:
                var basicAuth = await GetBasicAuthAsync();
                if (basicAuth.HasValue)
                {
                    ollamaService.SetBasicAuth(basicAuth.Value.username, basicAuth.Value.password);
                }
                break;

            case AuthType.None:
            default:
                ollamaService.ClearAuthentication();
                break;
        }
    }

    /// <summary>
    /// Check if authentication is configured
    /// </summary>
    /// <returns>True if authentication is configured</returns>
    public async Task<bool> IsAuthenticationConfiguredAsync()
    {
        var authType = await GetAuthTypeAsync();
        return authType != AuthType.None;
    }

    /// <summary>
    /// Get authentication status string
    /// </summary>
    /// <returns>Human-readable authentication status</returns>
    public async Task<string> GetAuthenticationStatusAsync()
    {
        var authType = await GetAuthTypeAsync();

        return authType switch
        {
            AuthType.ApiKey => "✓ API Key configured",
            AuthType.Basic => "✓ Basic Auth configured",
            AuthType.None => "No authentication configured",
            _ => "Unknown authentication status"
        };
    }
}

/// <summary>
/// Exception thrown by authentication service
/// </summary>
public class OllamaAuthException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaAuthException"/> class.
    /// </summary>
    /// <param name="message">The exception message</param>
    /// <param name="innerException">The inner exception</param>
    public OllamaAuthException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
