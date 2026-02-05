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

        // Register the provider as IEmbodimentProvider
        services.AddTransient<IEmbodimentProvider, TapoEmbodimentProvider>(sp =>
        {
            var tapoClient = sp.GetRequiredService<TapoRestClient>();
            var visionModel = sp.GetService<IVisionModel>();
            var ttsModel = sp.GetService<ITtsModel>();
            var config = sp.GetService<TapoVisionModelConfig>() ?? TapoVisionModelConfig.CreateDefault();
            var logger = sp.GetService<ILogger<TapoEmbodimentProvider>>();

            return new TapoEmbodimentProvider(tapoClient, providerId, visionModel, ttsModel, config, logger);
        });

        // Also register concrete type for direct access
        services.AddTransient<TapoEmbodimentProvider>(sp =>
        {
            var tapoClient = sp.GetRequiredService<TapoRestClient>();
            var visionModel = sp.GetService<IVisionModel>();
            var ttsModel = sp.GetService<ITtsModel>();
            var config = sp.GetService<TapoVisionModelConfig>() ?? TapoVisionModelConfig.CreateDefault();
            var logger = sp.GetService<ILogger<TapoEmbodimentProvider>>();

            return new TapoEmbodimentProvider(tapoClient, providerId, visionModel, ttsModel, config, logger);
        });

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

        // Register the aggregate
        services.AddSingleton<EmbodimentAggregate>(sp =>
        {
            var aggregate = new EmbodimentAggregate(aggregateId, aggregateName);
            
            // Register the Tapo provider with the aggregate
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

        // Register the embodiment as a factory to allow proper dependency resolution
        services.AddTransient<TapoEmbodiment>(sp =>
        {
            var tapoClient = sp.GetRequiredService<TapoRestClient>();
            var virtualSelf = sp.GetService<VirtualSelf>() ?? new VirtualSelf("TapoEmbodiment");
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
}
