// <copyright file="HttpMeTTaEngine.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Tools.MeTTa;

using System.Net.Http.Json;

/// <summary>
/// HTTP client for communicating with a Python-based MeTTa/Hyperon service.
/// </summary>
public sealed class HttpMeTTaEngine : IMeTTaEngine
{
    private readonly HttpClient client;
    private readonly string baseUrl;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="HttpMeTTaEngine"/> class.
    /// Creates a new HTTP-based MeTTa engine client.
    /// </summary>
    /// <param name="baseUrl">Base URL of the MeTTa/Hyperon HTTP service.</param>
    /// <param name="apiKey">Optional API key for authentication.</param>
    public HttpMeTTaEngine(string baseUrl, string? apiKey = null)
    {
        this.baseUrl = baseUrl.TrimEnd('/');
        this.client = new HttpClient
        {
            BaseAddress = new Uri(this.baseUrl),
            Timeout = TimeSpan.FromSeconds(30),
        };

        if (!string.IsNullOrEmpty(apiKey))
        {
            this.client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
    {
        try
        {
            var payload = new { query };
            HttpResponseMessage response = await this.client.PostAsJsonAsync("/query", payload, ct);

            if (!response.IsSuccessStatusCode)
            {
                return Result<string, string>.Failure($"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
            }

            QueryResponse? result = await response.Content.ReadFromJsonAsync<QueryResponse>(cancellationToken: ct);

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
            HttpResponseMessage response = await this.client.PostAsJsonAsync("/fact", payload, ct);

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
            HttpResponseMessage response = await this.client.PostAsJsonAsync("/rule", payload, ct);

            if (!response.IsSuccessStatusCode)
            {
                return Result<string, string>.Failure($"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
            }

            QueryResponse? result = await response.Content.ReadFromJsonAsync<QueryResponse>(cancellationToken: ct);

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
            HttpResponseMessage response = await this.client.PostAsJsonAsync("/verify", payload, ct);

            if (!response.IsSuccessStatusCode)
            {
                return Result<bool, string>.Failure($"HTTP {response.StatusCode}: {await response.Content.ReadAsStringAsync(ct)}");
            }

            VerifyResponse? result = await response.Content.ReadFromJsonAsync<VerifyResponse>(cancellationToken: ct);

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
            HttpResponseMessage response = await this.client.PostAsync("/reset", null, ct);

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
        if (this.disposed)
        {
            return;
        }

        this.client?.Dispose();
        this.disposed = true;
    }

    // Response DTOs
    private record QueryResponse(string? Result, string? Error);

    private record VerifyResponse(bool IsValid, string? Reason);
}
