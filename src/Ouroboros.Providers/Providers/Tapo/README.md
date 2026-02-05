# Tapo REST API Client

C# client for the [Tapo REST API](https://github.com/ClementNerma/tapo-rest) to control TP-Link Tapo smart home devices.

## Architecture Overview

This is a **client** for the Tapo REST API **server**, not a direct connection to Tapo devices:

```
┌─────────────┐         ┌──────────────────┐         ┌─────────────┐
│  C# Client  │  HTTP   │  Tapo REST API   │  Tapo   │   Tapo      │
│  (This)     │────────▶│  Server (Rust)   │ Protocol│   Devices   │
└─────────────┘         └──────────────────┘────────▶└─────────────┘
     Uses                  Requires email/             Physical
  server_password          password for Tapo          smart devices
```

## Prerequisites

### 1. Set up the Tapo REST API Server

First, you need to run the [Tapo REST API server](https://github.com/ClementNerma/tapo-rest):

```bash
docker run -it -v ./devices.json:/app/devices.json -p 8000:80 clementnerma/tapo-rest
```

### 2. Create Server Configuration

Create a `devices.json` file with your **Tapo account credentials** and devices:

```json
{
  "tapo_credentials": {
    "email": "your-tapo-account@example.com",
    "password": "your-tapo-account-password"
  },
  "server_password": "your-strong-api-password",
  "devices": [
    {
      "name": "living-room-bulb",
      "device_type": "L530",
      "ip_addr": "192.168.1.100"
    },
    {
      "name": "bedroom-plug",
      "device_type": "P110",
      "ip_addr": "192.168.1.101"
    }
  ]
}
```

**Important Notes:**
- `tapo_credentials.email` and `tapo_credentials.password`: Your Tapo app login credentials (configured on server)
- `server_password`: Password for the REST API itself (used by this client)
- The Tapo credentials stay on the server - the client never sees them

## Client Usage

### 1. Register the Service

```csharp
services.AddTapoRestClient("http://localhost:8000");
```

### 2. Authenticate with Server Password

```csharp
var client = serviceProvider.GetRequiredService<TapoRestClient>();

// Use the server_password from your devices.json, NOT your Tapo account password
var loginResult = await client.LoginAsync("your-strong-api-password");

if (loginResult.IsFailure)
{
    Console.WriteLine($"Login failed: {loginResult.Error}");
    return;
}

Console.WriteLine($"Authenticated! Session ID: {loginResult.Value}");
```

### 3. Control Devices

```csharp
// Turn on a color bulb
await client.ColorLightBulbs.TurnOnAsync("living-room-bulb");

// Set color to red
await client.ColorLightBulbs.SetColorAsync("living-room-bulb", 
    new Color { Red = 255, Green = 0, Blue = 0 });

// Control a smart plug
await client.EnergyPlugs.TurnOnAsync("bedroom-plug");

// Get energy usage
var usageResult = await client.EnergyPlugs.GetEnergyUsageAsync("bedroom-plug");
if (usageResult.IsSuccess)
{
    var usage = usageResult.Value;
    Console.WriteLine($"Energy usage: {usage}");
}
```

## Supported Devices

### Light Bulbs (Non-Color)
- **L510, L520, L610**: Basic on/off, brightness control
- Operations: `TurnOnAsync()`, `TurnOffAsync()`, `SetBrightnessAsync()`

### Color Light Bulbs
- **L530, L535, L630**: RGB color, hue/saturation, color temperature
- Operations: `SetColorAsync()`, `SetHueSaturationAsync()`, `SetColorTemperatureAsync()`

### RGB Light Strips
- **L900**: Basic RGB light strips
- **L920, L930**: RGBIC strips with individually colored segments and preset effects
- Operations: `SetLightingEffectAsync()` (for L920/L930)

### Smart Plugs
- **P100, P105**: Basic on/off control
- **P110, P110M, P115**: Energy monitoring plugs
- Operations: `GetEnergyUsageAsync()`, `GetCurrentPowerAsync()`, `GetHourlyEnergyDataAsync()`

### Power Strips
- **P300, P304, P304M, P316**: Multi-outlet power strips
- Operations: `GetChildDeviceListAsync()`

## Error Handling

All operations return `Result<T>` for monadic error handling:

```csharp
var result = await client.ColorLightBulbs.TurnOnAsync("living-room-bulb");

if (result.IsSuccess)
{
    Console.WriteLine("Light turned on successfully!");
}
else
{
    Console.WriteLine($"Failed to turn on light: {result.Error}");
}
```

## Session Management

```csharp
// Refresh device session if it expires
var refreshResult = await client.RefreshSessionAsync("living-room-bulb");

// Reload server configuration without restarting
var reloadResult = await client.ReloadConfigAsync();
```

## Complete Example

```csharp
using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Providers.Tapo;

var services = new ServiceCollection();
services.AddTapoRestClient("http://localhost:8000");
services.AddLogging();

var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<TapoRestClient>();

// Login with server password (NOT Tapo account password)
var loginResult = await client.LoginAsync("your-strong-api-password");
if (loginResult.IsFailure)
{
    Console.WriteLine($"Login failed: {loginResult.Error}");
    return;
}

// Get all configured devices
var devicesResult = await client.GetDevicesAsync();
if (devicesResult.IsSuccess)
{
    foreach (var device in devicesResult.Value)
    {
        Console.WriteLine($"Device: {device.Name} ({device.DeviceType})");
    }
}

// Control a color bulb
var bulb = "living-room-bulb";
await client.ColorLightBulbs.TurnOnAsync(bulb);
await client.ColorLightBulbs.SetBrightnessAsync(bulb, 80);
await client.ColorLightBulbs.SetColorAsync(bulb, 
    new Color { Red = 255, Green = 100, Blue = 50 });
```

## Security Notes

1. **Never commit credentials**: Keep your `devices.json` secure and never commit it to source control
2. **Use HTTPS in production**: The Tapo REST API server doesn't provide SSL, so use a reverse proxy (like Caddy) for HTTPS
3. **Strong server password**: Use a strong, unique password for `server_password`
4. **Network isolation**: Run the server on a trusted network or use VPN

## Troubleshooting

### "Not authenticated" errors
- Ensure you called `LoginAsync()` first with the correct `server_password`
- Check that the server is running and accessible

### "Device name not found" errors
- Verify the device name matches exactly what's in `devices.json`
- Call `GetDevicesAsync()` to see all configured devices

### Session timeout errors
- Call `RefreshSessionAsync(deviceName)` to refresh the device session
- The server maintains sessions with physical devices that can expire

## Embodiment Integration

The Tapo client can be used as an embodiment provider for AI agents, providing video, audio, and voice capabilities through Tapo smart cameras and devices.

### Architecture

The integration follows a repository-like pattern with a Domain Aggregate layer:

```
┌─────────────────────────────────┐
│     EmbodimentAggregate         │  Domain Aggregate
│  (Business Logic / State)       │
└─────────────┬───────────────────┘
              │
┌─────────────▼───────────────────┐
│   IEmbodimentProvider           │  Repository Interface
│   (Abstract State Source)       │
└─────────────┬───────────────────┘
              │
┌─────────────▼───────────────────┐
│   TapoEmbodimentProvider        │  Implementation
│   (Uses Tapo REST as State)     │
└─────────────┬───────────────────┘
              │
┌─────────────▼───────────────────┐
│   TapoRestClient                │  State Source
│   (Tapo REST API)               │
└─────────────────────────────────┘
```

### Quick Start

```csharp
using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Providers.Tapo;

var services = new ServiceCollection();

// Option 1: Register provider only
services.AddTapoEmbodimentProvider("http://localhost:8000");

// Option 2: Register complete aggregate (recommended)
services.AddTapoEmbodimentAggregate("http://localhost:8000");

var provider = services.BuildServiceProvider();
var aggregate = provider.GetRequiredService<EmbodimentAggregate>();

// Activate the embodiment
await aggregate.ActivateAsync();

// Subscribe to perceptions from cameras
aggregate.UnifiedPerceptions.Subscribe(perception =>
{
    Console.WriteLine($"Perception from {perception.SensorId}: {perception.Modality}");
});

// Execute actions (speak, control lights, etc.)
var action = ActuatorAction.Speak("Hello from my Tapo camera!");
await aggregate.ExecuteActionAsync("tapo:camera-speaker", action);
```

### Supported Capabilities

| Device Type | Capabilities |
|-------------|--------------|
| C100-C520 Cameras | VideoCapture, VisionAnalysis, AudioCapture, TwoWayAudio |
| L510-L930 Lights | LightingControl, ColorControl (L530+) |
| P100-P316 Plugs | PowerControl, EnergyMonitoring (P110+) |

### Vision Model Configuration

By default, the provider uses **llava:13b** as the vision model for camera analysis:

```csharp
// Use default (llava:13b - balanced speed/accuracy)
services.AddTapoEmbodimentProvider("http://localhost:8000");

// Configure custom vision model
services.AddTapoEmbodimentProvider("http://localhost:8000", configureVision: config =>
    TapoVisionModelConfig.CreateHighQuality()); // llava:34b
```

Available presets:
- `CreateDefault()` - llava:13b (balanced)
- `CreateLightweight()` - llava:7b (faster, less accurate)
- `CreateHighQuality()` - llava:34b (slower, more accurate)

### Domain Events

The aggregate emits domain events for all significant actions:

```csharp
aggregate.DomainEvents.Subscribe(evt =>
{
    switch (evt.EventType)
    {
        case EmbodimentDomainEventType.PerceptionReceived:
            Console.WriteLine("New perception received");
            break;
        case EmbodimentDomainEventType.MotionDetected:
            Console.WriteLine("Motion detected!");
            break;
        case EmbodimentDomainEventType.PersonDetected:
            Console.WriteLine("Person detected!");
            break;
    }
});
```

## References

- [Tapo REST API Server](https://github.com/ClementNerma/tapo-rest)
- [Unofficial Tapo API (Rust)](https://crates.io/crates/tapo)
