using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ouroboros.Core;
using Ouroboros.Core.Learning;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Base class providing shared HTTP operation helpers for Tapo device operations.
/// </summary>
public abstract class TapoDeviceOperationsBase
{
    protected readonly HttpClient HttpClient;
    protected readonly ILogger? Logger;
    protected readonly JsonSerializerOptions JsonOptions;
    private readonly Func<string?> _getSessionId;

    protected TapoDeviceOperationsBase(
        HttpClient httpClient,
        ILogger? logger,
        JsonSerializerOptions jsonOptions,
        Func<string?> getSessionId)
    {
        HttpClient = httpClient;
        Logger = logger;
        JsonOptions = jsonOptions;
        _getSessionId = getSessionId;
    }

    /// <summary>
    /// Executes an action that doesn't return data.
    /// </summary>
    protected async Task<Result<Unit>> ExecuteActionAsync(string action, string deviceName, CancellationToken ct)
    {
        try
        {
            var separator = action.Contains('?') ? '&' : '?';
            var url = $"/actions/{action}{separator}device={Uri.EscapeDataString(deviceName)}";
            
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            AddAuthorizationHeader(request);
            
            var response = await HttpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                Logger?.LogError("Action {Action} failed for device {Device}: {Error}", action, deviceName, error);
                return Result<Unit>.Failure($"Action failed: {response.StatusCode}");
            }

            return Result<Unit>.Success(Unit.Value);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            Logger?.LogError(ex, "HTTP error executing action {Action} for device {Device}", action, deviceName);
            return Result<Unit>.Failure($"HTTP error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error executing action {Action} for device {Device}", action, deviceName);
            return Result<Unit>.Failure($"Action failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Executes an action that returns JSON data.
    /// </summary>
    protected async Task<Result<JsonDocument>> GetJsonResponseAsync(string action, string deviceName, CancellationToken ct)
    {
        try
        {
            var separator = action.Contains('?') ? '&' : '?';
            var url = $"/actions/{action}{separator}device={Uri.EscapeDataString(deviceName)}";
            
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            AddAuthorizationHeader(request);
            
            var response = await HttpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                Logger?.LogError("Action {Action} failed for device {Device}: {Error}", action, deviceName, error);
                return Result<JsonDocument>.Failure($"Action failed: {response.StatusCode}");
            }

            var json = await response.Content.ReadFromJsonAsync<JsonDocument>(JsonOptions, ct);
            return json != null
                ? Result<JsonDocument>.Success(json)
                : Result<JsonDocument>.Failure("Empty response");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            Logger?.LogError(ex, "HTTP error executing action {Action} for device {Device}", action, deviceName);
            return Result<JsonDocument>.Failure($"HTTP error: {ex.Message}");
        }
        catch (Exception ex)
        {
            Logger?.LogError(ex, "Error executing action {Action} for device {Device}", action, deviceName);
            return Result<JsonDocument>.Failure($"Action failed: {ex.Message}");
        }
    }

    private void AddAuthorizationHeader(HttpRequestMessage request)
    {
        var sessionId = _getSessionId();
        if (!string.IsNullOrWhiteSpace(sessionId))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionId);
        }
    }
}
