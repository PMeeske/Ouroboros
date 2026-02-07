using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Ouroboros.Core.EmbodiedInteraction;

namespace Ouroboros.Providers.Tapo;

/// <summary>
/// Extension methods for configuring Tapo REST client services.
/// </summary>
public static class TapoServiceCollectionExtensions
{
    /// <summary>
    /// Adds the Tapo REST client to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="baseAddress">The base address of the Tapo REST API server.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTapoRestClient(
        this IServiceCollection services,
        string baseAddress)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        if (string.IsNullOrWhiteSpace(baseAddress))
            throw new ArgumentException("Base address is required", nameof(baseAddress));

        services.AddHttpClient<TapoRestClient>(client =>
        {
            client.BaseAddress = new Uri(baseAddress);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddTransient<TapoRestClient>();

        return services;
    }

    /// <summary>
    /// Adds the Tapo REST client to the service collection with configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configuration action for the HTTP client.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTapoRestClient(
        this IServiceCollection services,
        Action<HttpClient> configure)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        if (configure == null)
            throw new ArgumentNullException(nameof(configure));

        services.AddHttpClient<TapoRestClient>(configure);
        services.AddTransient<TapoRestClient>();

        return services;
    }

    /// <summary>
    /// Adds the Tapo embodiment provider following the repository pattern.
    /// The provider uses Tapo REST API as the state source for the domain aggregate.
    /// Configures a strong vision model (llava:13b) as default.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="baseAddress">The base address of the Tapo REST API server.</param>
    /// <param name="providerId">Optional provider identifier.</param>
    /// <param name="configureVision">Optional function to configure the vision model (returns modified config).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTapoEmbodimentProvider(
        this IServiceCollection services,
        string baseAddress,
        string providerId = "tapo",
        Func<TapoVisionModelConfig, TapoVisionModelConfig>? configureVision = null)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        if (string.IsNullOrWhiteSpace(baseAddress))
            throw new ArgumentException("Base address is required", nameof(baseAddress));

        // Add the REST client (state source)
        services.AddTapoRestClient(baseAddress);

        // Configure the vision model with strong defaults (llava:13b)
        var visionConfig = TapoVisionModelConfig.CreateDefault();
        if (configureVision != null)
        {
            visionConfig = configureVision(visionConfig);
        }
        services.AddSingleton(visionConfig);

        // Register the provider as singleton to maintain connection state across application lifecycle
        services.AddSingleton<TapoEmbodimentProvider>(sp =>
        {
            var tapoClient = sp.GetRequiredService<TapoRestClient>();
            var visionModel = sp.GetService<IVisionModel>();
            var ttsModel = sp.GetService<ITtsModel>();
            var config = sp.GetService<TapoVisionModelConfig>() ?? TapoVisionModelConfig.CreateDefault();
            var logger = sp.GetService<ILogger<TapoEmbodimentProvider>>();

            return new TapoEmbodimentProvider(tapoClient, providerId, visionModel, ttsModel, config, logger);
        });

        // Register interface to resolve to same singleton instance
        services.AddSingleton<IEmbodimentProvider>(sp => sp.GetRequiredService<TapoEmbodimentProvider>());

        return services;
    }

    /// <summary>
    /// Adds a Tapo RTSP embodiment provider for direct camera access.
    /// Uses RTSP streaming instead of the REST API for camera devices (C200, C210, etc.).
    /// Requires FFmpeg to be installed on the system.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="cameras">List of camera devices to connect to.</param>
    /// <param name="username">Tapo account username/email.</param>
    /// <param name="password">Tapo account password.</param>
    /// <param name="providerId">Optional provider identifier.</param>
    /// <param name="configureVision">Optional function to configure the vision model.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTapoRtspEmbodimentProvider(
        this IServiceCollection services,
        IEnumerable<TapoDevice> cameras,
        string username,
        string password,
        string providerId = "tapo-rtsp",
        Func<TapoVisionModelConfig, TapoVisionModelConfig>? configureVision = null)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        // Add the RTSP camera factory
        services.AddTapoRtspCameras(cameras, username, password);

        // Configure the vision model with strong defaults
        var visionConfig = TapoVisionModelConfig.CreateDefault();
        if (configureVision != null)
        {
            visionConfig = configureVision(visionConfig);
        }
        services.AddSingleton(visionConfig);

        // Register the RTSP-based embodiment provider
        services.AddSingleton<TapoEmbodimentProvider>(sp =>
        {
            var rtspFactory = sp.GetRequiredService<ITapoRtspClientFactory>();
            var visionModel = sp.GetService<IVisionModel>();
            var ttsModel = sp.GetService<ITtsModel>();
            var config = sp.GetService<TapoVisionModelConfig>() ?? TapoVisionModelConfig.CreateDefault();
            var logger = sp.GetService<ILogger<TapoEmbodimentProvider>>();

            return new TapoEmbodimentProvider(rtspFactory, providerId, visionModel, ttsModel, config, logger,
                username: username, password: password);
        });

        // Register interface to resolve to same singleton instance
        services.AddSingleton<IEmbodimentProvider>(sp => sp.GetRequiredService<TapoEmbodimentProvider>());

        return services;
    }

    /// <summary>
    /// Adds a complete Tapo embodiment aggregate with the provider as state source.
    /// Creates a domain aggregate that uses Tapo devices for embodied interaction.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="baseAddress">The base address of the Tapo REST API server.</param>
    /// <param name="aggregateId">Unique identifier for the aggregate.</param>
    /// <param name="aggregateName">Human-readable name for the aggregate.</param>
    /// <param name="configureVision">Optional function to configure the vision model (returns modified config).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTapoEmbodimentAggregate(
        this IServiceCollection services,
        string baseAddress,
        string aggregateId = "tapo-embodiment",
        string aggregateName = "Tapo Embodiment",
        Func<TapoVisionModelConfig, TapoVisionModelConfig>? configureVision = null)
    {
        // Add the provider
        services.AddTapoEmbodimentProvider(baseAddress, "tapo", configureVision);

        // Register the aggregate as singleton
        services.AddSingleton<EmbodimentAggregate>(sp =>
        {
            var aggregate = new EmbodimentAggregate(aggregateId, aggregateName);

            // Register the Tapo provider singleton with the aggregate.
            // RegisterProvider uses TryAdd internally, so duplicate registrations are safely ignored.
            var provider = sp.GetRequiredService<TapoEmbodimentProvider>();
            aggregate.RegisterProvider(provider);

            return aggregate;
        });

        return services;
    }

    /// <summary>
    /// Adds the legacy Tapo embodiment services for video, audio, and voice capabilities.
    /// Configures the TapoEmbodiment with a strong vision model (llava:13b) as default.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="baseAddress">The base address of the Tapo REST API server.</param>
    /// <param name="configureVision">Optional function to configure the vision model (returns modified config).</param>
    /// <returns>The service collection for chaining.</returns>
    [Obsolete("Use AddTapoEmbodimentProvider for the new repository-pattern architecture")]
    public static IServiceCollection AddTapoEmbodiment(
        this IServiceCollection services,
        string baseAddress,
        Func<TapoVisionModelConfig, TapoVisionModelConfig>? configureVision = null)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        if (string.IsNullOrWhiteSpace(baseAddress))
            throw new ArgumentException("Base address is required", nameof(baseAddress));

        // Add the REST client
        services.AddTapoRestClient(baseAddress);

        // Configure the vision model with defaults
        var visionConfig = TapoVisionModelConfig.CreateDefault();
        if (configureVision != null)
        {
            visionConfig = configureVision(visionConfig);
        }
        services.AddSingleton(visionConfig);

        // Register the embodiment as a factory - VirtualSelf must be registered
        services.AddTransient<TapoEmbodiment>(sp =>
        {
            var tapoClient = sp.GetRequiredService<TapoRestClient>();
            var virtualSelf = sp.GetRequiredService<VirtualSelf>();
            var visionModel = sp.GetService<IVisionModel>();
            var ttsModel = sp.GetService<ITtsModel>();
            var logger = sp.GetService<ILogger<TapoEmbodiment>>();

            return new TapoEmbodiment(tapoClient, virtualSelf, visionModel, ttsModel, logger);
        });

        return services;
    }

    /// <summary>
    /// Adds the Tapo embodiment with a pre-configured camera.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="baseAddress">The base address of the Tapo REST API server.</param>
    /// <param name="cameraName">The name of the camera device to use.</param>
    /// <param name="configureCamera">Optional function to configure the camera (returns modified config).</param>
    /// <returns>The service collection for chaining.</returns>
    [Obsolete("Use AddTapoEmbodimentProvider for the new repository-pattern architecture")]
    public static IServiceCollection AddTapoEmbodimentWithCamera(
        this IServiceCollection services,
        string baseAddress,
        string cameraName,
        Func<TapoCameraConfig, TapoCameraConfig>? configureCamera = null)
    {
        if (string.IsNullOrWhiteSpace(cameraName))
            throw new ArgumentException("Camera name is required", nameof(cameraName));

        // Add base embodiment
#pragma warning disable CS0618 // Type or member is obsolete
        services.AddTapoEmbodiment(baseAddress);
#pragma warning restore CS0618

        // Configure the camera with strong vision model as default
        var cameraConfig = new TapoCameraConfig(
            CameraName: cameraName,
            VisionModel: TapoVisionModelConfig.DefaultVisionModel);

        // Allow customization
        if (configureCamera != null)
        {
            cameraConfig = configureCamera(cameraConfig);
        }
        services.AddSingleton(cameraConfig);

        return services;
    }

    /// <summary>
    /// Adds a Tapo RTSP camera client for direct camera streaming (C200, C210, etc.).
    /// This connects directly to the camera via RTSP, bypassing the REST API server.
    /// Requires FFmpeg to be installed on the system.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="cameraIp">IP address of the Tapo camera.</param>
    /// <param name="username">Tapo account username/email.</param>
    /// <param name="password">Tapo account password.</param>
    /// <param name="quality">Stream quality setting.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTapoRtspCamera(
        this IServiceCollection services,
        string cameraIp,
        string username,
        string password,
        CameraStreamQuality quality = CameraStreamQuality.HD)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        if (string.IsNullOrWhiteSpace(cameraIp))
            throw new ArgumentException("Camera IP is required", nameof(cameraIp));

        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("Username is required", nameof(username));

        if (string.IsNullOrWhiteSpace(password))
            throw new ArgumentException("Password is required", nameof(password));

        services.AddSingleton(sp =>
        {
            var logger = sp.GetService<ILogger<TapoRtspClient>>();
            return new TapoRtspClient(cameraIp, username, password, quality, logger);
        });

        return services;
    }

    /// <summary>
    /// Adds multiple Tapo RTSP cameras from configuration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="cameras">List of camera configurations.</param>
    /// <param name="username">Tapo account username/email.</param>
    /// <param name="password">Tapo account password.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddTapoRtspCameras(
        this IServiceCollection services,
        IEnumerable<TapoDevice> cameras,
        string username,
        string password)
    {
        var cameraList = cameras.Where(d => IsCameraDevice(d.DeviceType)).ToList();

        services.AddSingleton<ITapoRtspClientFactory>(sp =>
        {
            var logger = sp.GetService<ILogger<TapoRtspClient>>();
            return new TapoRtspClientFactory(cameraList, username, password, logger);
        });

        return services;
    }

    private static bool IsCameraDevice(TapoDeviceType deviceType) =>
        deviceType is TapoDeviceType.C100 or TapoDeviceType.C200 or TapoDeviceType.C210
            or TapoDeviceType.C220 or TapoDeviceType.C310 or TapoDeviceType.C320
            or TapoDeviceType.C420 or TapoDeviceType.C500 or TapoDeviceType.C520;
}

/// <summary>
/// Factory for creating RTSP clients for multiple cameras.
/// </summary>
public interface ITapoRtspClientFactory
{
    /// <summary>
    /// Gets an RTSP client for a specific camera by name.
    /// </summary>
    TapoRtspClient? GetClient(string cameraName);

    /// <summary>
    /// Gets all available camera names.
    /// </summary>
    IEnumerable<string> GetCameraNames();
}

/// <summary>
/// Default implementation of ITapoRtspClientFactory.
/// </summary>
public sealed class TapoRtspClientFactory : ITapoRtspClientFactory, IDisposable
{
    private readonly Dictionary<string, TapoRtspClient> _clients = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="TapoRtspClientFactory"/> class.
    /// </summary>
    public TapoRtspClientFactory(
        IEnumerable<TapoDevice> cameras,
        string username,
        string password,
        ILogger<TapoRtspClient>? logger = null)
    {
        foreach (var camera in cameras)
        {
            _clients[camera.Name] = new TapoRtspClient(
                camera.IpAddress, username, password, CameraStreamQuality.HD, logger);
        }
    }

    /// <inheritdoc/>
    public TapoRtspClient? GetClient(string cameraName) =>
        _clients.TryGetValue(cameraName, out var client) ? client : null;

    /// <inheritdoc/>
    public IEnumerable<string> GetCameraNames() => _clients.Keys;

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var client in _clients.Values)
        {
            client.Dispose();
        }
        _clients.Clear();
    }
}
