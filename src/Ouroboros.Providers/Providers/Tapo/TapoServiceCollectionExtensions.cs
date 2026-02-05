using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
}
