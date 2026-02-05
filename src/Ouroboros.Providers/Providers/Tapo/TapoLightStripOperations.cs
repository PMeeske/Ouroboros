using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ouroboros.Core;
using Ouroboros.Core.Learning;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Provides operations for Tapo RGB light strip devices (L900).
/// </summary>
public sealed class TapoLightStripOperations : TapoDeviceOperationsBase
{
    internal TapoLightStripOperations(
        HttpClient httpClient,
        ILogger? logger,
        JsonSerializerOptions jsonOptions,
        Func<string?> getSessionId)
        : base(httpClient, logger, jsonOptions, getSessionId)
    {
    }

    /// <summary>
    /// Turns the light strip on.
    /// </summary>
    public async Task<Result<Unit>> TurnOnAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<Unit>.Failure("Device name is required");

        return await ExecuteActionAsync("l900/on", deviceName, ct);
    }

    /// <summary>
    /// Turns the light strip off.
    /// </summary>
    public async Task<Result<Unit>> TurnOffAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<Unit>.Failure("Device name is required");

        return await ExecuteActionAsync("l900/off", deviceName, ct);
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

        return await ExecuteActionAsync($"l900/set-brightness?level={level}", deviceName, ct);
    }

    /// <summary>
    /// Sets the color using RGB values.
    /// </summary>
    public async Task<Result<Unit>> SetColorAsync(string deviceName, Color color, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<Unit>.Failure("Device name is required");

        return await ExecuteActionAsync(
            $"l900/set-color?color.red={color.Red}&color.green={color.Green}&color.blue={color.Blue}",
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

        return await ExecuteActionAsync($"l900/set-hue-saturation?hue={hue}&saturation={saturation}", deviceName, ct);
    }

    /// <summary>
    /// Sets the color temperature.
    /// </summary>
    public async Task<Result<Unit>> SetColorTemperatureAsync(string deviceName, ushort temperature, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<Unit>.Failure("Device name is required");

        return await ExecuteActionAsync($"l900/set-color-temperature?color_temperature={temperature}", deviceName, ct);
    }

    /// <summary>
    /// Gets device information.
    /// </summary>
    public async Task<Result<JsonDocument>> GetDeviceInfoAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<JsonDocument>.Failure("Device name is required");

        return await GetJsonResponseAsync("l900/get-device-info", deviceName, ct);
    }

    /// <summary>
    /// Gets device usage information.
    /// </summary>
    public async Task<Result<JsonDocument>> GetDeviceUsageAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<JsonDocument>.Failure("Device name is required");

        return await GetJsonResponseAsync("l900/get-device-usage", deviceName, ct);
    }
}

/// <summary>
/// Provides operations for Tapo RGBIC light strip devices with individually colored segments (L920, L930).
/// </summary>
public sealed class TapoRgbicLightStripOperations : TapoDeviceOperationsBase
{
    internal TapoRgbicLightStripOperations(
        HttpClient httpClient,
        ILogger? logger,
        JsonSerializerOptions jsonOptions,
        Func<string?> getSessionId)
        : base(httpClient, logger, jsonOptions, getSessionId)
    {
    }

    /// <summary>
    /// Turns the light strip on.
    /// </summary>
    public async Task<Result<Unit>> TurnOnAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<Unit>.Failure("Device name is required");

        return await ExecuteActionAsync("l920/on", deviceName, ct);
    }

    /// <summary>
    /// Turns the light strip off.
    /// </summary>
    public async Task<Result<Unit>> TurnOffAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<Unit>.Failure("Device name is required");

        return await ExecuteActionAsync("l920/off", deviceName, ct);
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

        return await ExecuteActionAsync($"l920/set-brightness?level={level}", deviceName, ct);
    }

    /// <summary>
    /// Sets the color using RGB values.
    /// </summary>
    public async Task<Result<Unit>> SetColorAsync(string deviceName, Color color, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<Unit>.Failure("Device name is required");

        return await ExecuteActionAsync(
            $"l920/set-color?color.red={color.Red}&color.green={color.Green}&color.blue={color.Blue}",
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

        return await ExecuteActionAsync($"l920/set-hue-saturation?hue={hue}&saturation={saturation}", deviceName, ct);
    }

    /// <summary>
    /// Sets the color temperature.
    /// </summary>
    public async Task<Result<Unit>> SetColorTemperatureAsync(string deviceName, ushort temperature, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<Unit>.Failure("Device name is required");

        return await ExecuteActionAsync($"l920/set-color-temperature?color_temperature={temperature}", deviceName, ct);
    }

    /// <summary>
    /// Sets a preset lighting effect.
    /// </summary>
    public async Task<Result<Unit>> SetLightingEffectAsync(string deviceName, LightingEffectPreset effect, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<Unit>.Failure("Device name is required");

        return await ExecuteActionAsync($"l920/set-lighting-effect?lighting_effect={effect}", deviceName, ct);
    }

    /// <summary>
    /// Gets device information.
    /// </summary>
    public async Task<Result<JsonDocument>> GetDeviceInfoAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<JsonDocument>.Failure("Device name is required");

        return await GetJsonResponseAsync("l920/get-device-info", deviceName, ct);
    }

    /// <summary>
    /// Gets device usage information.
    /// </summary>
    public async Task<Result<JsonDocument>> GetDeviceUsageAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<JsonDocument>.Failure("Device name is required");

        return await GetJsonResponseAsync("l920/get-device-usage", deviceName, ct);
    }
}
