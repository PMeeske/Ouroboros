using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ouroboros.Core;
using Ouroboros.Core.Learning;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Provides operations for Tapo light bulb devices (L510, L520, L610).
/// </summary>
public sealed class TapoLightBulbOperations : TapoDeviceOperationsBase
{
    internal TapoLightBulbOperations(
        HttpClient httpClient,
        ILogger? logger,
        JsonSerializerOptions jsonOptions,
        Func<string?> getSessionId)
        : base(httpClient, logger, jsonOptions, getSessionId)
    {
    }

    /// <summary>
    /// Turns the light bulb on.
    /// </summary>
    /// <param name="deviceName">Name of the device.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<Result<Unit>> TurnOnAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<Unit>.Failure("Device name is required");

        return await ExecuteActionAsync("l510/on", deviceName, ct);
    }

    /// <summary>
    /// Turns the light bulb off.
    /// </summary>
    /// <param name="deviceName">Name of the device.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<Result<Unit>> TurnOffAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<Unit>.Failure("Device name is required");

        return await ExecuteActionAsync("l510/off", deviceName, ct);
    }

    /// <summary>
    /// Sets the brightness level of the light bulb.
    /// </summary>
    /// <param name="deviceName">Name of the device.</param>
    /// <param name="level">Brightness level (0-100).</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<Result<Unit>> SetBrightnessAsync(string deviceName, byte level, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<Unit>.Failure("Device name is required");

        if (level > 100)
            return Result<Unit>.Failure("Brightness level must be between 0 and 100");

        return await ExecuteActionAsync($"l510/set-brightness?level={level}", deviceName, ct);
    }

    /// <summary>
    /// Gets device information.
    /// </summary>
    /// <param name="deviceName">Name of the device.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<Result<JsonDocument>> GetDeviceInfoAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<JsonDocument>.Failure("Device name is required");

        return await GetJsonResponseAsync("l510/get-device-info", deviceName, ct);
    }

    /// <summary>
    /// Gets device usage information.
    /// </summary>
    /// <param name="deviceName">Name of the device.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<Result<JsonDocument>> GetDeviceUsageAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<JsonDocument>.Failure("Device name is required");

        return await GetJsonResponseAsync("l510/get-device-usage", deviceName, ct);
    }
}

/// <summary>
/// Provides operations for Tapo color light bulb devices (L530, L535, L630).
/// </summary>
public sealed class TapoColorLightBulbOperations : TapoDeviceOperationsBase
{
    internal TapoColorLightBulbOperations(
        HttpClient httpClient,
        ILogger? logger,
        JsonSerializerOptions jsonOptions,
        Func<string?> getSessionId)
        : base(httpClient, logger, jsonOptions, getSessionId)
    {
    }

    /// <summary>
    /// Turns the light bulb on.
    /// </summary>
    public async Task<Result<Unit>> TurnOnAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<Unit>.Failure("Device name is required");

        return await ExecuteActionAsync("l530/on", deviceName, ct);
    }

    /// <summary>
    /// Turns the light bulb off.
    /// </summary>
    public async Task<Result<Unit>> TurnOffAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<Unit>.Failure("Device name is required");

        return await ExecuteActionAsync("l530/off", deviceName, ct);
    }

    /// <summary>
    /// Sets the brightness level.
    /// </summary>
    public async Task<Result<Unit>> SetBrightnessAsync(string deviceName, byte level, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<Unit>.Failure("Device name is required");

        if (level > 100)
            return Result<Unit>.Failure("Brightness level must be between 0 and 100");

        return await ExecuteActionAsync($"l530/set-brightness?level={level}", deviceName, ct);
    }

    /// <summary>
    /// Sets the color using RGB values.
    /// </summary>
    public async Task<Result<Unit>> SetColorAsync(string deviceName, Color color, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<Unit>.Failure("Device name is required");

        return await ExecuteActionAsync(
            $"l530/set-color?color.red={color.Red}&color.green={color.Green}&color.blue={color.Blue}",
            deviceName,
            ct);
    }

    /// <summary>
    /// Sets the hue and saturation.
    /// </summary>
    public async Task<Result<Unit>> SetHueSaturationAsync(string deviceName, ushort hue, byte saturation, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<Unit>.Failure("Device name is required");

        if (hue > 360)
            return Result<Unit>.Failure("Hue must be between 0 and 360");

        if (saturation > 100)
            return Result<Unit>.Failure("Saturation must be between 0 and 100");

        return await ExecuteActionAsync($"l530/set-hue-saturation?hue={hue}&saturation={saturation}", deviceName, ct);
    }

    /// <summary>
    /// Sets the color temperature.
    /// </summary>
    public async Task<Result<Unit>> SetColorTemperatureAsync(string deviceName, ushort temperature, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<Unit>.Failure("Device name is required");

        return await ExecuteActionAsync($"l530/set-color-temperature?color_temperature={temperature}", deviceName, ct);
    }

    /// <summary>
    /// Gets device information.
    /// </summary>
    public async Task<Result<JsonDocument>> GetDeviceInfoAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<JsonDocument>.Failure("Device name is required");

        return await GetJsonResponseAsync("l530/get-device-info", deviceName, ct);
    }

    /// <summary>
    /// Gets device usage information.
    /// </summary>
    public async Task<Result<JsonDocument>> GetDeviceUsageAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<JsonDocument>.Failure("Device name is required");

        return await GetJsonResponseAsync("l530/get-device-usage", deviceName, ct);
    }
}
