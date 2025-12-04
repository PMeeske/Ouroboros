using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;

namespace Ouroboros.Android.Services;

/// <summary>
/// Service for interacting with Ollama API with authentication support
/// </summary>
public class OllamaService
{
    private readonly HttpClient _httpClient;
    private string _baseUrl;
    private string? _apiKey;
    private string? _username;
    private string? _password;

    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaService"/> class.
    /// </summary>
    /// <param name="baseUrl">Base URL of the Ollama service</param>
    public OllamaService(string baseUrl = "http://localhost:11434")
    {
        _baseUrl = baseUrl;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };
    }

    /// <summary>
    /// Gets or sets the base URL
    /// </summary>
    public string BaseUrl
    {
        get => _baseUrl;
        set => _baseUrl = value;
    }

    /// <summary>
    /// Set API key authentication
    /// </summary>
    /// <param name="apiKey">The API key for bearer token authentication</param>
    public void SetApiKey(string? apiKey)
    {
        _apiKey = apiKey;
        UpdateAuthenticationHeaders();
    }

    /// <summary>
    /// Set basic authentication credentials
    /// </summary>
    /// <param name="username">Username</param>
    /// <param name="password">Password</param>
    public void SetBasicAuth(string? username, string? password)
    {
        _username = username;
        _password = password;
        UpdateAuthenticationHeaders();
    }

    /// <summary>
    /// Clear all authentication
    /// </summary>
    public void ClearAuthentication()
    {
        _apiKey = null;
        _username = null;
        _password = null;
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    private void UpdateAuthenticationHeaders()
    {
        _httpClient.DefaultRequestHeaders.Authorization = null;

        if (!string.IsNullOrEmpty(_apiKey))
        {
            // Bearer token authentication
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", _apiKey);
        }
        else if (!string.IsNullOrEmpty(_username) && !string.IsNullOrEmpty(_password))
        {
            // Basic authentication
            var credentials = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{_username}:{_password}"));
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Basic", credentials);
        }
    }

    /// <summary>
    /// List all available models
    /// </summary>
    /// <returns>List of model information</returns>
    public async Task<List<OllamaModel>> ListModelsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags");
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaTagsResponse>();
            return result?.Models ?? new List<OllamaModel>();
        }
        catch (Exception ex)
        {
            throw new OllamaException($"Failed to list models: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Pull a model from Ollama
    /// </summary>
    /// <param name="modelName">Name of the model to pull</param>
    /// <param name="progressHandler">Handler for progress updates</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the operation</returns>
    public async Task PullModelAsync(
        string modelName,
        Action<string>? progressHandler = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new { name = modelName };
            var content = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.PostAsync(
                $"{_baseUrl}/api/pull",
                content,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (!string.IsNullOrEmpty(line))
                {
                    progressHandler?.Invoke(line);
                }
            }
        }
        catch (Exception ex)
        {
            throw new OllamaException($"Failed to pull model: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Delete a model
    /// </summary>
    /// <param name="modelName">Name of the model to delete</param>
    /// <returns>Task representing the operation</returns>
    public async Task DeleteModelAsync(string modelName)
    {
        try
        {
            var request = new { name = modelName };
            var content = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json");

            var httpRequest = new HttpRequestMessage(HttpMethod.Delete, $"{_baseUrl}/api/delete")
            {
                Content = content
            };

            var response = await _httpClient.SendAsync(httpRequest);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            throw new OllamaException($"Failed to delete model: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Generate a response from a model
    /// </summary>
    /// <param name="model">Model name</param>
    /// <param name="prompt">The prompt</param>
    /// <param name="responseHandler">Handler for streaming response</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Complete response text</returns>
    public async Task<string> GenerateAsync(
        string model,
        string prompt,
        Action<string>? responseHandler = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var request = new
            {
                model = model,
                prompt = prompt,
                stream = true
            };

            var content = new StringContent(
                JsonSerializer.Serialize(request),
                Encoding.UTF8,
                "application/json");

            using var response = await _httpClient.PostAsync(
                $"{_baseUrl}/api/generate",
                content,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);

            var fullResponse = new StringBuilder();

            while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(cancellationToken);
                if (!string.IsNullOrEmpty(line))
                {
                    try
                    {
                        var jsonResponse = JsonSerializer.Deserialize<OllamaGenerateResponse>(line);
                        if (jsonResponse?.Response != null)
                        {
                            fullResponse.Append(jsonResponse.Response);
                            responseHandler?.Invoke(jsonResponse.Response);
                        }

                        if (jsonResponse?.Done == true)
                        {
                            break;
                        }
                    }
                    catch
                    {
                        // Skip malformed JSON lines
                    }
                }
            }

            return fullResponse.ToString();
        }
        catch (Exception ex)
        {
            throw new OllamaException($"Failed to generate response: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Test connection to Ollama service
    /// </summary>
    /// <returns>True if connection is successful</returns>
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// Ollama model information
/// </summary>
public class OllamaModel
{
    /// <summary>
    /// Gets or sets the model name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model size in bytes
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Gets or sets the model digest
    /// </summary>
    public string Digest { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the modified date
    /// </summary>
    public DateTime ModifiedAt { get; set; }

    /// <summary>
    /// Get formatted size string
    /// </summary>
    /// <returns>Human-readable size</returns>
    public string GetFormattedSize()
    {
        if (Size < 1024) return $"{Size} B";
        if (Size < 1024 * 1024) return $"{Size / 1024.0:F1} KB";
        if (Size < 1024 * 1024 * 1024) return $"{Size / (1024.0 * 1024):F1} MB";
        return $"{Size / (1024.0 * 1024 * 1024):F1} GB";
    }
}

/// <summary>
/// Response from Ollama tags API
/// </summary>
public class OllamaTagsResponse
{
    /// <summary>
    /// Gets or sets the list of models
    /// </summary>
    public List<OllamaModel> Models { get; set; } = new();
}

/// <summary>
/// Response from Ollama generate API
/// </summary>
public class OllamaGenerateResponse
{
    /// <summary>
    /// Gets or sets the model name
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Gets or sets the response text
    /// </summary>
    public string? Response { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether generation is complete
    /// </summary>
    public bool Done { get; set; }
}

/// <summary>
/// Exception thrown by Ollama service
/// </summary>
public class OllamaException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OllamaException"/> class.
    /// </summary>
    /// <param name="message">The exception message</param>
    /// <param name="innerException">The inner exception</param>
    public OllamaException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
