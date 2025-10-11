using System.Net.Http.Json;
using System.Text.Json;

namespace LangChainPipeline.Tools.MeTTa;

/// <summary>
/// HTTP client for communicating with a Python-based MeTTa/Hyperon service.
/// </summary>
public sealed class HttpMeTTaEngine : IMeTTaEngine
{
    private readonly HttpClient _client;
    private readonly string _baseUrl;
    private bool _disposed;

    /// <summary>
    /// Creates a new HTTP-based MeTTa engine client.
    /// </summary>
    /// <param name="baseUrl">Base URL of the MeTTa/Hyperon HTTP service.</param>
    /// <param name="apiKey">Optional API key for authentication.</param>
    public HttpMeTTaEngine(string baseUrl, string? apiKey = null)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _client = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };

        if (!string.IsNullOrEmpty(apiKey))
        {
            _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
    {
        try
        {
            var payload = new { query };
            var response = await _client.PostAsJsonAsync("/query", payload, ct);

            if (!response.IsSuccessStatusCode)
            {
                return Result<string, string>.Failure($"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
            }

            var result = await response.Content.ReadFromJsonAsync<QueryResponse>(cancellationToken: ct);

            return result?.Result != null
                ? Result<string, string>.Success(result.Result)
                : Result<string, string>.Failure("Invalid response from server");
        }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"HTTP request failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Query execution failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<Unit, string>> AddFactAsync(string fact, CancellationToken ct = default)
    {
        try
        {
            var payload = new { fact };
            var response = await _client.PostAsJsonAsync("/fact", payload, ct);

            if (!response.IsSuccessStatusCode)
            {
                return Result<Unit, string>.Failure($"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
            }

            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (HttpRequestException ex)
        {
            return Result<Unit, string>.Failure($"HTTP request failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Failed to add fact: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<string, string>> ApplyRuleAsync(string rule, CancellationToken ct = default)
    {
        try
        {
            var payload = new { rule };
            var response = await _client.PostAsJsonAsync("/rule", payload, ct);

            if (!response.IsSuccessStatusCode)
            {
                return Result<string, string>.Failure($"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
            }

            var result = await response.Content.ReadFromJsonAsync<QueryResponse>(cancellationToken: ct);

            return result?.Result != null
                ? Result<string, string>.Success(result.Result)
                : Result<string, string>.Failure("Invalid response from server");
        }
        catch (HttpRequestException ex)
        {
            return Result<string, string>.Failure($"HTTP request failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Rule application failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool, string>> VerifyPlanAsync(string plan, CancellationToken ct = default)
    {
        try
        {
            var payload = new { plan };
            var response = await _client.PostAsJsonAsync("/verify", payload, ct);

            if (!response.IsSuccessStatusCode)
            {
                return Result<bool, string>.Failure($"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
            }

            var result = await response.Content.ReadFromJsonAsync<VerifyResponse>(cancellationToken: ct);

            return result != null
                ? Result<bool, string>.Success(result.IsValid)
                : Result<bool, string>.Failure("Invalid response from server");
        }
        catch (HttpRequestException ex)
        {
            return Result<bool, string>.Failure($"HTTP request failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result<bool, string>.Failure($"Plan verification failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<Unit, string>> ResetAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await _client.PostAsync("/reset", null, ct);

            if (!response.IsSuccessStatusCode)
            {
                return Result<Unit, string>.Failure($"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
            }

            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (HttpRequestException ex)
        {
            return Result<Unit, string>.Failure($"HTTP request failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Failed to reset: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;
        _client?.Dispose();
        _disposed = true;
    }

    // Response DTOs
    private record QueryResponse(string? Result, string? Error);
    private record VerifyResponse(bool IsValid, string? Reason);
}
