using System.Text.Json.Serialization;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Represents a Tapo device configuration.
/// </summary>
public sealed record TapoDevice
{
    /// <summary>
    /// Gets the name of the device.
    /// </summary>
    [JsonPropertyName("name")]
    public required string Name { get; init; }

    /// <summary>
    /// Gets the device type.
    /// </summary>
    [JsonPropertyName("device_type")]
    public required TapoDeviceType DeviceType { get; init; }

    /// <summary>
    /// Gets the IP address of the device.
    /// </summary>
    [JsonPropertyName("ip_addr")]
    public required string IpAddress { get; init; }
}

/// <summary>
/// Enumeration of supported Tapo device types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TapoDeviceType
{
    /// <summary>Light bulb (non-color).</summary>
    L510,
    
    /// <summary>Light bulb (non-color).</summary>
    L520,
    
    /// <summary>Light bulb (non-color).</summary>
    L610,
    
    /// <summary>Light bulb with customizable colors.</summary>
    L530,
    
    /// <summary>Light bulb with customizable colors.</summary>
    L535,
    
    /// <summary>Light bulb with customizable colors.</summary>
    L630,
    
    /// <summary>RGB light strip.</summary>
    L900,
    
    /// <summary>RGB light strip with individually colored segments.</summary>
    L920,
    
    /// <summary>RGB light strip with individually colored segments.</summary>
    L930,
    
    /// <summary>Smart plug.</summary>
    P100,
    
    /// <summary>Smart plug.</summary>
    P105,
    
    /// <summary>Smart plug with energy monitoring.</summary>
    P110,
    
    /// <summary>Smart plug with energy monitoring.</summary>
    P110M,
    
    /// <summary>Smart plug with energy monitoring.</summary>
    P115,
    
    /// <summary>Power strip.</summary>
    P300,
    
    /// <summary>Power strip.</summary>
    P304,
    
    /// <summary>Power strip.</summary>
    P304M,
    
    /// <summary>Power strip.</summary>
    P316
}

/// <summary>
/// Represents RGB color values.
/// </summary>
public sealed record Color
{
    /// <summary>
    /// Gets the red component (0-255).
    /// </summary>
    [JsonPropertyName("red")]
    public required byte Red { get; init; }

    /// <summary>
    /// Gets the green component (0-255).
    /// </summary>
    [JsonPropertyName("green")]
    public required byte Green { get; init; }

    /// <summary>
    /// Gets the blue component (0-255).
    /// </summary>
    [JsonPropertyName("blue")]
    public required byte Blue { get; init; }
}

/// <summary>
/// Preset lighting effects for RGB light strips.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LightingEffectPreset
{
    /// <summary>Aurora effect.</summary>
    Aurora,
    
    /// <summary>Bubbling cauldron effect.</summary>
    BubblingCauldron,
    
    /// <summary>Candy cane effect.</summary>
    CandyCane,
    
    /// <summary>Christmas effect.</summary>
    Christmas,
    
    /// <summary>Flicker effect.</summary>
    Flicker,
    
    /// <summary>Grandmas Christmas lights effect.</summary>
    GrandmasChristmasLights,
    
    /// <summary>Hanukkah effect.</summary>
    Hanukkah,
    
    /// <summary>Haunted mansion effect.</summary>
    HauntedMansion,
    
    /// <summary>Icicle effect.</summary>
    Icicle,
    
    /// <summary>Lightning effect.</summary>
    Lightning,
    
    /// <summary>Ocean effect.</summary>
    Ocean,
    
    /// <summary>Rainbow effect.</summary>
    Rainbow,
    
    /// <summary>Raindrop effect.</summary>
    Raindrop,
    
    /// <summary>Spring effect.</summary>
    Spring,
    
    /// <summary>Sunrise effect.</summary>
    Sunrise,
    
    /// <summary>Sunset effect.</summary>
    Sunset,
    
    /// <summary>Valentines effect.</summary>
    Valentines
}

/// <summary>
/// Interval type for energy data queries.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum EnergyDataInterval
{
    /// <summary>Hourly data.</summary>
    Hourly,
    
    /// <summary>Daily data.</summary>
    Daily,
    
    /// <summary>Monthly data.</summary>
    Monthly
}

/// <summary>
/// Represents the server-side configuration for the Tapo REST API server.
/// This is NOT used by the client but documents what the server requires.
/// The server configuration file needs tapo_credentials (email/password),
/// server_password, and device list.
/// </summary>
/// <remarks>
/// Example server configuration (server-side only, not for client use):
/// <code>
/// {
///   "tapo_credentials": {
///     "email": "your-tapo-account@example.com",
///     "password": "your-tapo-account-password"
///   },
///   "server_password": "your-api-server-password",
///   "devices": [
///     {
///       "name": "living-room-bulb",
///       "device_type": "L530",
///       "ip_addr": "192.168.1.100"
///     }
///   ]
/// }
/// </code>
/// </remarks>
public sealed record TapoServerConfig
{
    /// <summary>
    /// Gets the Tapo account credentials (server-side only).
    /// </summary>
    [JsonPropertyName("tapo_credentials")]
    public required TapoCredentials TapoCredentials { get; init; }

    /// <summary>
    /// Gets the server password used for client authentication.
    /// </summary>
    [JsonPropertyName("server_password")]
    public required string ServerPassword { get; init; }

    /// <summary>
    /// Gets the list of configured devices.
    /// </summary>
    [JsonPropertyName("devices")]
    public required List<TapoDevice> Devices { get; init; }
}

/// <summary>
/// Represents Tapo account credentials (server-side configuration only).
/// </summary>
public sealed record TapoCredentials
{
    /// <summary>
    /// Gets the Tapo account email address.
    /// </summary>
    [JsonPropertyName("email")]
    public required string Email { get; init; }

    /// <summary>
    /// Gets the Tapo account password.
    /// </summary>
    [JsonPropertyName("password")]
    public required string Password { get; init; }
}
