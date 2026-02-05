using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ouroboros.Core;
using Ouroboros.Core.Learning;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Provides operations for Tapo smart plug devices (P100, P105).
/// </summary>
public sealed class TapoPlugOperations : TapoDeviceOperationsBase
{
    internal TapoPlugOperations(
        HttpClient httpClient,
        ILogger? logger,
        JsonSerializerOptions jsonOptions,
        Func<string?> getSessionId)
        : base(httpClient, logger, jsonOptions, getSessionId)
    {
    }

    /// <summary>
    /// Turns the plug on.
    /// </summary>
    public async Task<Result<Unit>> TurnOnAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<Unit>.Failure("Device name is required");

        return await ExecuteActionAsync("p100/on", deviceName, ct);
    }

    /// <summary>
    /// Turns the plug off.
    /// </summary>
    public async Task<Result<Unit>> TurnOffAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<Unit>.Failure("Device name is required");

        return await ExecuteActionAsync("p100/off", deviceName, ct);
    }

    /// <summary>
    /// Gets device information.
    /// </summary>
    public async Task<Result<JsonDocument>> GetDeviceInfoAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<JsonDocument>.Failure("Device name is required");

        return await GetJsonResponseAsync("p100/get-device-info", deviceName, ct);
    }

    /// <summary>
    /// Gets device usage information.
    /// </summary>
    public async Task<Result<JsonDocument>> GetDeviceUsageAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<JsonDocument>.Failure("Device name is required");

        return await GetJsonResponseAsync("p100/get-device-usage", deviceName, ct);
    }


}

/// <summary>
/// Provides operations for Tapo energy monitoring plug devices (P110, P110M, P115).
/// </summary>
public sealed class TapoEnergyPlugOperations : TapoDeviceOperationsBase
{
    internal TapoEnergyPlugOperations(
        HttpClient httpClient,
        ILogger? logger,
        JsonSerializerOptions jsonOptions,
        Func<string?> getSessionId)
        : base(httpClient, logger, jsonOptions, getSessionId)
    {
    }

    /// <summary>
    /// Turns the plug on.
    /// </summary>
    public async Task<Result<Unit>> TurnOnAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<Unit>.Failure("Device name is required");

        return await ExecuteActionAsync("p110/on", deviceName, ct);
    }

    /// <summary>
    /// Turns the plug off.
    /// </summary>
    public async Task<Result<Unit>> TurnOffAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<Unit>.Failure("Device name is required");

        return await ExecuteActionAsync("p110/off", deviceName, ct);
    }

    /// <summary>
    /// Gets device information.
    /// </summary>
    public async Task<Result<JsonDocument>> GetDeviceInfoAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<JsonDocument>.Failure("Device name is required");

        return await GetJsonResponseAsync("p110/get-device-info", deviceName, ct);
    }

    /// <summary>
    /// Gets device usage information.
    /// </summary>
    public async Task<Result<JsonDocument>> GetDeviceUsageAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<JsonDocument>.Failure("Device name is required");

        return await GetJsonResponseAsync("p110/get-device-usage", deviceName, ct);
    }

    /// <summary>
    /// Gets energy usage information.
    /// </summary>
    public async Task<Result<JsonDocument>> GetEnergyUsageAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<JsonDocument>.Failure("Device name is required");

        return await GetJsonResponseAsync("p110/get-energy-usage", deviceName, ct);
    }

    /// <summary>
    /// Gets current power consumption.
    /// </summary>
    public async Task<Result<JsonDocument>> GetCurrentPowerAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<JsonDocument>.Failure("Device name is required");

        return await GetJsonResponseAsync("p110/get-current-power", deviceName, ct);
    }

    /// <summary>
    /// Gets hourly energy data between two dates.
    /// </summary>
    public async Task<Result<JsonDocument>> GetHourlyEnergyDataAsync(
        string deviceName,
        DateOnly startDate,
        DateOnly? endDate = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<JsonDocument>.Failure("Device name is required");

        var endDateParam = endDate.HasValue ? $"&end_date={endDate.Value:yyyy-MM-dd}" : string.Empty;
        return await GetJsonResponseAsync(
            $"p110/get-hourly-energy-data?start_date={startDate:yyyy-MM-dd}{endDateParam}",
            deviceName,
            ct);
    }

    /// <summary>
    /// Gets daily energy data from a start date.
    /// </summary>
    public async Task<Result<JsonDocument>> GetDailyEnergyDataAsync(
        string deviceName,
        DateOnly startDate,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<JsonDocument>.Failure("Device name is required");

        return await GetJsonResponseAsync(
            $"p110/get-daily-energy-data?start_date={startDate:yyyy-MM-dd}",
            deviceName,
            ct);
    }

    /// <summary>
    /// Gets monthly energy data from a start date.
    /// </summary>
    public async Task<Result<JsonDocument>> GetMonthlyEnergyDataAsync(
        string deviceName,
        DateOnly startDate,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<JsonDocument>.Failure("Device name is required");

        return await GetJsonResponseAsync(
            $"p110/get-monthly-energy-data?start_date={startDate:yyyy-MM-dd}",
            deviceName,
            ct);
    }


}

/// <summary>
/// Provides operations for Tapo power strip devices (P300, P304, P304M, P316).
/// </summary>
public sealed class TapoPowerStripOperations : TapoDeviceOperationsBase
{
    internal TapoPowerStripOperations(
        HttpClient httpClient,
        ILogger? logger,
        JsonSerializerOptions jsonOptions,
        Func<string?> getSessionId)
        : base(httpClient, logger, jsonOptions, getSessionId)
    {
    }

    /// <summary>
    /// Gets device information.
    /// </summary>
    public async Task<Result<JsonDocument>> GetDeviceInfoAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<JsonDocument>.Failure("Device name is required");

        return await GetJsonResponseAsync("p300/get-device-info", deviceName, ct);
    }

    /// <summary>
    /// Gets the list of child devices (plugs) on the power strip.
    /// </summary>
    public async Task<Result<JsonDocument>> GetChildDeviceListAsync(string deviceName, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return Result<JsonDocument>.Failure("Device name is required");

        return await GetJsonResponseAsync("p300/get-child-device-list", deviceName, ct);
    }

}
