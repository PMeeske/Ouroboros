using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Ouroboros.Core;
using Ouroboros.Core.Learning;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Client for interacting with the Tapo REST API server.
/// Provides type-safe access to Tapo smart devices (bulbs, plugs, strips).
/// Note: This client connects to a Tapo REST API server (https://github.com/ClementNerma/tapo-rest),
/// not directly to Tapo devices. The server handles device authentication using Tapo account credentials
/// configured on the server side. This client only needs the server_password to authenticate with the API.
/// </summary>
public sealed class TapoRestClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TapoRestClient>? _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private string? _sessionId;

    /// <summary>
    /// Initializes a new instance of the <see cref="TapoRestClient"/> class.
    /// </summary>
    /// <param name="httpClient">The HTTP client for API requests.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public TapoRestClient(HttpClient httpClient, ILogger<TapoRestClient>? logger = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        // Initialize device operation helpers
        LightBulbs = new TapoLightBulbOperations(_httpClient, _logger, _jsonOptions);
        ColorLightBulbs = new TapoColorLightBulbOperations(_httpClient, _logger, _jsonOptions);
        LightStrips = new TapoLightStripOperations(_httpClient, _logger, _jsonOptions);
        RgbicLightStrips = new TapoRgbicLightStripOperations(_httpClient, _logger, _jsonOptions);
        Plugs = new TapoPlugOperations(_httpClient, _logger, _jsonOptions);
        EnergyPlugs = new TapoEnergyPlugOperations(_httpClient, _logger, _jsonOptions);
        PowerStrips = new TapoPowerStripOperations(_httpClient, _logger, _jsonOptions);
    }

    /// <summary>
    /// Gets operations for standard light bulbs (L510, L520, L610).
    /// </summary>
    public TapoLightBulbOperations LightBulbs { get; }

    /// <summary>
    /// Gets operations for color light bulbs (L530, L535, L630).
    /// </summary>
    public TapoColorLightBulbOperations ColorLightBulbs { get; }

    /// <summary>
    /// Gets operations for RGB light strips (L900).
    /// </summary>
    public TapoLightStripOperations LightStrips { get; }

    /// <summary>
    /// Gets operations for RGBIC light strips (L920, L930).
    /// </summary>
    public TapoRgbicLightStripOperations RgbicLightStrips { get; }

    /// <summary>
    /// Gets operations for smart plugs (P100, P105).
    /// </summary>
    public TapoPlugOperations Plugs { get; }

    /// <summary>
    /// Gets operations for energy monitoring plugs (P110, P110M, P115).
    /// </summary>
    public TapoEnergyPlugOperations EnergyPlugs { get; }

    /// <summary>
    /// Gets operations for power strips (P300, P304, P304M, P316).
    /// </summary>
    public TapoPowerStripOperations PowerStrips { get; }

    /// <summary>
    /// Authenticates with the Tapo REST API server using the server password.
    /// Note: This is NOT the Tapo account email/password. Those credentials are configured
    /// on the server side. This password is the 'server_password' from the server's config file.
    /// </summary>
    /// <param name="password">Server password for authentication (server_password from config).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the session ID on success or error message on failure.</returns>
    public async Task<Result<string>> LoginAsync(string password, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(password))
            return Result<string>.Failure("Password is required");

        try
        {
            var loginRequest = new { password };
            var response = await _httpClient.PostAsJsonAsync("/login", loginRequest, _jsonOptions, ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                _logger?.LogError("Login failed with status {StatusCode}: {Error}", response.StatusCode, error);
                return Result<string>.Failure($"Login failed: {response.StatusCode}");
            }

            _sessionId = await response.Content.ReadAsStringAsync(ct);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _sessionId);

            _logger?.LogInformation("Successfully authenticated with Tapo REST API");
            return Result<string>.Success(_sessionId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "HTTP error during login");
            return Result<string>.Failure($"HTTP error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Unexpected error during login");
            return Result<string>.Failure($"Login failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the list of configured devices from the server.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing device list or error.</returns>
    public async Task<Result<List<TapoDevice>>> GetDevicesAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_sessionId))
            return Result<List<TapoDevice>>.Failure("Not authenticated. Call LoginAsync first.");

        try
        {
            var devices = await _httpClient.GetFromJsonAsync<List<TapoDevice>>("/devices", _jsonOptions, ct);
            return devices != null
                ? Result<List<TapoDevice>>.Success(devices)
                : Result<List<TapoDevice>>.Failure("No devices returned");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "HTTP error getting devices");
            return Result<List<TapoDevice>>.Failure($"HTTP error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting devices");
            return Result<List<TapoDevice>>.Failure($"Failed to get devices: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the list of available action routes from the server.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing action routes or error.</returns>
    public async Task<Result<List<string>>> GetActionsAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_sessionId))
            return Result<List<string>>.Failure("Not authenticated. Call LoginAsync first.");

        try
        {
            var actions = await _httpClient.GetFromJsonAsync<List<string>>("/actions", _jsonOptions, ct);
            return actions != null
                ? Result<List<string>>.Success(actions)
                : Result<List<string>>.Failure("No actions returned");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "HTTP error getting actions");
            return Result<List<string>>.Failure($"HTTP error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error getting actions");
            return Result<List<string>>.Failure($"Failed to get actions: {ex.Message}");
        }
    }

    /// <summary>
    /// Refreshes the session for a specific device.
    /// </summary>
    /// <param name="deviceName">Name of the device.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    public async Task<Result<Unit>> RefreshSessionAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_sessionId))
            return Result<Unit>.Failure("Not authenticated. Call LoginAsync first.");

        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<Unit>.Failure("Device name is required");

        try
        {
            var response = await _httpClient.GetAsync($"/refresh-session?device={Uri.EscapeDataString(deviceName)}", ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                _logger?.LogError("Session refresh failed for device {Device}: {Error}", deviceName, error);
                return Result<Unit>.Failure($"Session refresh failed: {response.StatusCode}");
            }

            _logger?.LogInformation("Session refreshed for device {Device}", deviceName);
            return Result<Unit>.Success(Unit.Value);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "HTTP error refreshing session for device {Device}", deviceName);
            return Result<Unit>.Failure($"HTTP error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error refreshing session for device {Device}", deviceName);
            return Result<Unit>.Failure($"Failed to refresh session: {ex.Message}");
        }
    }

    /// <summary>
    /// Reloads the server configuration file.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    public async Task<Result<Unit>> ReloadConfigAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_sessionId))
            return Result<Unit>.Failure("Not authenticated. Call LoginAsync first.");

        try
        {
            var response = await _httpClient.PostAsync("/reload-config", null, ct);

            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync(ct);
                _logger?.LogError("Config reload failed: {Error}", error);
                return Result<Unit>.Failure($"Config reload failed: {response.StatusCode}");
            }

            _logger?.LogInformation("Configuration reloaded successfully");
            return Result<Unit>.Success(Unit.Value);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger?.LogError(ex, "HTTP error reloading config");
            return Result<Unit>.Failure($"HTTP error: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error reloading config");
            return Result<Unit>.Failure($"Failed to reload config: {ex.Message}");
        }
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    public void Dispose()
    {
        // HttpClient is injected and should not be disposed here
        // It's managed by the DI container/HttpClientFactory
    }
}
