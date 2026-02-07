// <copyright file="OuroborosCliIntegration.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace Ouroboros.CLI;

using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ouroboros.Application.Integration;
using Ouroboros.Core.EmbodiedInteraction;
using Ouroboros.Providers;
using Ouroboros.Providers.Tapo;

/// <summary>
/// Integrates the full Ouroboros system into CLI commands.
/// Ensures all commands have access to the unified core by default.
/// </summary>
public static class OuroborosCliIntegration
{
    private static IServiceProvider? _serviceProvider;
    private static IOuroborosCore? _ouroborosCore;
    private static OuroborosTelemetry? _telemetry;

    /// <summary>
    /// Initializes the Ouroboros system for CLI usage.
    /// Should be called early in Program.cs startup.
    /// </summary>
    public static async Task<IServiceProvider> InitializeAsync(string[] args)
    {
        if (_serviceProvider != null)
        {
            return _serviceProvider;
        }

        var builder = Host.CreateDefaultBuilder(args);

        // Configure services
        builder.ConfigureServices((context, services) =>
        {
            // Load Ouroboros configuration
            var config = context.Configuration;
            var ouroborosConfig = OuroborosConfiguration.Load(config);

            // Register configuration
            services.AddSingleton(ouroborosConfig);

            // Add full Ouroboros system with monitoring
            services.AddOuroborosFullWithMonitoring();

            // Add Tapo embodiment if configured (lazy initialization - availability checked at runtime)
            var tapoServerAddress = config["Tapo:ServerAddress"];
            var tapoUsername = config["Tapo:Username"];
            var tapoPassword = config["Tapo:Password"];
            var tapoDevices = config.GetSection("Tapo:Devices").Get<List<TapoDevice>>();

            // Validate and register Tapo services based on configuration
            // Both RTSP cameras AND REST API can be used simultaneously
            var tapoConfigResult = ValidateTapoConfiguration(tapoServerAddress, tapoUsername, tapoPassword, tapoDevices);
            if (tapoConfigResult.IsValid)
            {
                // Register RTSP cameras if configured (for direct camera access)
                if (tapoConfigResult.UseRtsp)
                {
                    services.AddTapoRtspCameras(tapoDevices!, tapoUsername!, tapoPassword!);
                }

                // Register REST API client if configured (for lights, plugs, etc.)
                // Availability is checked at runtime, not at registration
                if (tapoConfigResult.UseRestApi)
                {
                    services.AddTapoRestClient(tapoServerAddress!);
                }

                // Register unified embodiment provider that can use both
                services.AddSingleton<TapoEmbodimentProvider>(sp =>
                {
                    var restClient = sp.GetService<TapoRestClient>();
                    var rtspFactory = sp.GetService<ITapoRtspClientFactory>();
                    var visionModel = sp.GetService<IVisionModel>();
                    var ttsModel = sp.GetService<ITtsModel>();
                    var visionConfig = sp.GetService<TapoVisionModelConfig>() ?? TapoVisionModelConfig.CreateDefault();
                    var logger = sp.GetService<ILogger<TapoEmbodimentProvider>>();

                    return new TapoEmbodimentProvider(
                        restClient, rtspFactory, "tapo", visionModel, ttsModel, visionConfig, logger,
                        username: tapoUsername, password: tapoPassword);
                });

                services.AddSingleton<IEmbodimentProvider>(sp => sp.GetRequiredService<TapoEmbodimentProvider>());
            }

            // Add logging
            services.AddLogging();

            // Register Ollama vision model for embodiment and multi-model swarm
            var ollamaEndpoint = config["Ollama:Endpoint"] ?? "http://localhost:11434";
            var visionModelName = config["Ollama:VisionModel"] ?? OllamaVisionModel.DefaultModel;
            services.AddSingleton<IVisionModel>(sp =>
            {
                var visionLogger = sp.GetService<ILogger<OllamaVisionModel>>();
                return new OllamaVisionModel(ollamaEndpoint, visionModelName, logger: visionLogger);
            });
        });

        var host = builder.Build();
        _serviceProvider = host.Services;

        // Resolve core components
        _ouroborosCore = _serviceProvider.GetService<IOuroborosCore>();
        _telemetry = _serviceProvider.GetService<OuroborosTelemetry>();

        // Auto-authenticate Tapo if configured
        await TryInitializeTapoAsync(host.Services.GetRequiredService<IConfiguration>());

        return _serviceProvider;
    }

    /// <summary>
    /// Initializes Tapo embodiment - either via REST API authentication or RTSP connection.
    /// REST API availability is checked before attempting connection.
    /// </summary>
    private static async Task TryInitializeTapoAsync(IConfiguration config)
    {
        var tapoProvider = _serviceProvider?.GetService<TapoEmbodimentProvider>();
        if (tapoProvider == null)
        {
            return; // Tapo not configured
        }

        // Initialize both RTSP cameras AND REST API devices (both can be active simultaneously)
        var hasRtsp = tapoProvider.RtspClientFactory != null;
        var hasRestApi = tapoProvider.RestClient != null;

        if (!hasRtsp && !hasRestApi)
        {
            Console.WriteLine("[Tapo] No RTSP cameras or REST API configured");
            return;
        }

        // First initialize RTSP cameras (direct camera access)
        if (hasRtsp)
        {
            await InitializeRtspCamerasAsync(tapoProvider);
        }

        // Then initialize REST API (smart plugs/lights control)
        if (hasRestApi)
        {
            await InitializeRestApiAsync(tapoProvider, config);
        }

        // Now connect the provider (initializes both RTSP and REST if configured)
        if (!tapoProvider.IsConnected)
        {
            var connectResult = await tapoProvider.ConnectAsync();
            if (connectResult.IsFailure)
            {
                Console.WriteLine($"[Tapo] Connection failed: {connectResult.Error}");
            }
            else
            {
                Console.WriteLine("[Tapo] Embodiment provider connected successfully");
            }
        }
    }

    /// <summary>
    /// Initializes RTSP cameras and displays available cameras.
    /// </summary>
    private static Task InitializeRtspCamerasAsync(TapoEmbodimentProvider tapoProvider)
    {
        Console.WriteLine("[Tapo] RTSP camera support enabled");
        var cameraNames = tapoProvider.RtspClientFactory!.GetCameraNames().ToList();
        Console.WriteLine($"[Tapo] {cameraNames.Count} camera(s) registered:");

        // Display each camera (non-blocking, just log)
        foreach (var cameraName in cameraNames)
        {
            var rtspClient = tapoProvider.RtspClientFactory.GetClient(cameraName);
            if (rtspClient != null)
            {
                Console.WriteLine($"[Tapo]   - {cameraName} @ {rtspClient.CameraIp}");
            }
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Initializes REST API connection with availability check.
    /// </summary>
    private static async Task InitializeRestApiAsync(TapoEmbodimentProvider tapoProvider, IConfiguration config)
    {
        var serverPassword = config["Tapo:ServerPassword"];
        var serverAddress = config["Tapo:ServerAddress"];

        if (string.IsNullOrEmpty(serverPassword))
        {
            Console.WriteLine("[Tapo] REST API configured but no server password set (Tapo:ServerPassword)");
            return;
        }

        // Check if REST API server is reachable before attempting authentication
        var isAvailable = await CheckRestApiAvailabilityAsync(serverAddress);
        if (!isAvailable)
        {
            Console.WriteLine($"[Tapo] REST API server not available at {serverAddress} (this is optional)");
            return;
        }

        var authResult = await tapoProvider.AuthenticateAsync(serverPassword);
        if (authResult.IsSuccess)
        {
            Console.WriteLine("[Tapo] Connected to Tapo REST API");
        }
        else
        {
            Console.WriteLine($"[Tapo] REST API authentication failed: {authResult.Error}");
        }
    }

    /// <summary>
    /// Checks if the Tapo REST API server is reachable.
    /// </summary>
    private static async Task<bool> CheckRestApiAvailabilityAsync(string? serverAddress)
    {
        if (string.IsNullOrEmpty(serverAddress))
            return false;

        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var response = await httpClient.GetAsync(serverAddress);
            // Any response means the server is running (even 401/403)
            return true;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException)
        {
            return false; // Timeout
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the Ouroboros core instance.
    /// Initializes system if not already done.
    /// </summary>
    public static IOuroborosCore? GetCore()
    {
        return _ouroborosCore;
    }

    /// <summary>
    /// Gets the telemetry instance.
    /// </summary>
    public static OuroborosTelemetry? GetTelemetry()
    {
        return _telemetry;
    }

    /// <summary>
    /// Gets the service provider for dependency resolution.
    /// </summary>
    public static IServiceProvider? GetServiceProvider()
    {
        return _serviceProvider;
    }

    /// <summary>
    /// Checks if Ouroboros system is initialized.
    /// </summary>
    public static bool IsInitialized => _serviceProvider != null;

    /// <summary>
    /// Result of Tapo configuration validation.
    /// </summary>
    private sealed record TapoConfigValidationResult(
        bool IsValid,
        bool UseRtsp,
        bool UseRestApi,
        string? ValidationMessage);

    /// <summary>
    /// Validates Tapo configuration and determines which mode to use.
    /// </summary>
    private static TapoConfigValidationResult ValidateTapoConfiguration(
        string? serverAddress,
        string? username,
        string? password,
        List<TapoDevice>? devices)
    {
        // Check for RTSP mode (direct camera access)
        var hasCredentials = !string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password);
        var hasDevices = devices?.Count > 0;
        var hasCameraDevices = devices?.Any(d => IsCameraDevice(d.DeviceType)) ?? false;

        if (hasCredentials && hasDevices && hasCameraDevices)
        {
            // Validate device configurations
            foreach (var device in devices!)
            {
                if (string.IsNullOrWhiteSpace(device.Name))
                {
                    return new TapoConfigValidationResult(false, false, false,
                        "Device name is required for all Tapo devices");
                }

                if (string.IsNullOrWhiteSpace(device.IpAddress) || device.IpAddress == "192.168.1.1")
                {
                    Console.WriteLine($"[Tapo] Warning: Device '{device.Name}' has placeholder IP address. Update appsettings.json with actual camera IP.");
                }
            }

            // Enable REST API alongside RTSP if server address is configured and valid
            var useRestApi = !string.IsNullOrEmpty(serverAddress)
                && Uri.TryCreate(serverAddress, UriKind.Absolute, out var rtspUri)
                && (rtspUri.Scheme == "http" || rtspUri.Scheme == "https");

            return new TapoConfigValidationResult(true, true, useRestApi, null);
        }

        // Check for REST API mode (no cameras, just smart home devices)
        if (!string.IsNullOrEmpty(serverAddress))
        {
            // Validate URL format
            if (!Uri.TryCreate(serverAddress, UriKind.Absolute, out var uri) ||
                (uri.Scheme != "http" && uri.Scheme != "https"))
            {
                return new TapoConfigValidationResult(false, false, false,
                    $"Invalid Tapo server address: {serverAddress}");
            }

            return new TapoConfigValidationResult(true, false, true, null);
        }

        // No Tapo configuration
        return new TapoConfigValidationResult(false, false, false, null);
    }

    /// <summary>
    /// Checks if a device type is a camera.
    /// </summary>
    private static bool IsCameraDevice(TapoDeviceType deviceType) =>
        deviceType is TapoDeviceType.C100 or TapoDeviceType.C200 or TapoDeviceType.C210
            or TapoDeviceType.C220 or TapoDeviceType.C310 or TapoDeviceType.C320
            or TapoDeviceType.C420 or TapoDeviceType.C500 or TapoDeviceType.C520;

    /// <summary>
    /// Executes a goal using the Ouroboros system if initialized.
    /// Falls back gracefully if system is not initialized.
    /// </summary>
    public static async Task<bool> TryExecuteGoalAsync(
        string goal,
        ExecutionConfig? config = null,
        Action<ExecutionResult>? onSuccess = null,
        Action<string>? onError = null)
    {
        if (_ouroborosCore == null)
        {
            return false; // Not initialized, fall back to regular command handling
        }

        config ??= ExecutionConfig.Default;
        _telemetry?.RecordGoalExecution(true, TimeSpan.Zero);

        var result = await _ouroborosCore.ExecuteGoalAsync(goal, config);

        result.Match(
            success =>
            {
                _telemetry?.RecordGoalExecution(true, success.Duration);
                onSuccess?.Invoke(success);
            },
            error =>
            {
                _telemetry?.RecordError("goal_execution", "execution_failed");
                onError?.Invoke(error);
            });

        return true;
    }

    /// <summary>
    /// Performs reasoning using the Ouroboros system if initialized.
    /// </summary>
    public static async Task<bool> TryReasonAsync(
        string query,
        ReasoningConfig? config = null,
        Action<ReasoningResult>? onSuccess = null,
        Action<string>? onError = null)
    {
        if (_ouroborosCore == null)
        {
            return false;
        }

        config ??= ReasoningConfig.Default;
        var startTime = DateTime.UtcNow;

        var result = await _ouroborosCore.ReasonAboutAsync(query, config);

        var duration = DateTime.UtcNow - startTime;
        _telemetry?.RecordReasoningQuery(
            duration,
            config.UseSymbolicReasoning,
            config.UseCausalInference,
            config.UseAbduction);

        result.Match(
            onSuccess ?? (_ => { }),
            onError ?? (_ => { }));

        return true;
    }

    /// <summary>
    /// Records telemetry for CLI operations.
    /// </summary>
    public static void RecordCliOperation(string operation, bool success, TimeSpan duration)
    {
        _telemetry?.RecordGoalExecution(success, duration, new Dictionary<string, object>
        {
            ["operation"] = operation,
            ["cli"] = true
        });
    }

    /// <summary>
    /// Gets health status of the Ouroboros system.
    /// </summary>
    public static string GetHealthStatus()
    {
        if (_ouroborosCore == null)
        {
            return "Not initialized";
        }

        var status = new System.Text.StringBuilder();
        status.AppendLine("Ouroboros System Status:");
        status.AppendLine($"  Episodic Memory: {(_ouroborosCore.EpisodicMemory != null ? "✓" : "✗")}");
        status.AppendLine($"  MeTTa Reasoning: {(_ouroborosCore.MeTTaReasoning != null ? "✓" : "✗")}");
        status.AppendLine($"  Hierarchical Planner: {(_ouroborosCore.HierarchicalPlanner != null ? "✓" : "✗")}");
        status.AppendLine($"  Causal Reasoning: {(_ouroborosCore.CausalReasoning != null ? "✓" : "✗")}");
        status.AppendLine($"  Consciousness: {(_ouroborosCore.Consciousness != null ? "✓" : "✗")}");
        status.AppendLine($"  Reflection: {(_ouroborosCore.Reflection != null ? "✓" : "✗")}");

        return status.ToString();
    }

    /// <summary>
    /// Ensures Ouroboros is initialized before command execution.
    /// Call this at the start of any CLI command that should integrate with Ouroboros.
    /// </summary>
    public static async Task EnsureInitializedAsync(string[] args)
    {
        if (!IsInitialized)
        {
            try
            {
                await InitializeAsync(args);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] Could not initialize Ouroboros system: {ex.Message}");
                Console.WriteLine("[INFO] Commands will run in standalone mode");
            }
        }
    }

    /// <summary>
    /// Broadcasts information to consciousness if available.
    /// Used to make the system aware of CLI activities.
    /// </summary>
    public static async Task BroadcastToConsciousnessAsync(string content, string source)
    {
        if (_ouroborosCore?.Consciousness != null)
        {
            await _ouroborosCore.Consciousness.BroadcastToConsciousnessAsync(
                content,
                source);
        }
    }
}

/// <summary>
/// Extension methods for integrating Ouroboros into CLI commands.
/// </summary>
public static class CommandIntegrationExtensions
{
    /// <summary>
    /// Wraps a CLI command with Ouroboros integration.
    /// Ensures telemetry and consciousness integration.
    /// </summary>
    public static async Task WithOuroborosIntegrationAsync(
        this Task commandTask,
        string commandName,
        string description)
    {
        var startTime = DateTime.UtcNow;
        var success = false;

        try
        {
            // Broadcast command start to consciousness
            await OuroborosCliIntegration.BroadcastToConsciousnessAsync(
                $"Starting CLI command: {commandName}",
                "CLI");

            await commandTask;
            success = true;

            // Broadcast completion
            await OuroborosCliIntegration.BroadcastToConsciousnessAsync(
                $"Completed CLI command: {commandName}",
                "CLI");
        }
        finally
        {
            var duration = DateTime.UtcNow - startTime;
            OuroborosCliIntegration.RecordCliOperation(commandName, success, duration);
        }
    }

    /// <summary>
    /// Wraps a CLI command with Ouroboros integration and returns result.
    /// </summary>
    public static async Task<T> WithOuroborosIntegrationAsync<T>(
        this Task<T> commandTask,
        string commandName,
        string description)
    {
        var startTime = DateTime.UtcNow;
        var success = false;
        T result;

        try
        {
            await OuroborosCliIntegration.BroadcastToConsciousnessAsync(
                $"Starting CLI command: {commandName} - {description}",
                "CLI");

            result = await commandTask;
            success = true;

            await OuroborosCliIntegration.BroadcastToConsciousnessAsync(
                $"Completed CLI command: {commandName}",
                "CLI");

            return result;
        }
        finally
        {
            var duration = DateTime.UtcNow - startTime;
            OuroborosCliIntegration.RecordCliOperation(commandName, success, duration);
        }
    }
}
