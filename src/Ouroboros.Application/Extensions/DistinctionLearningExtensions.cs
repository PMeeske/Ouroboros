// <copyright file="DistinctionLearningExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Extensions;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Ouroboros.Application.Middleware;
using Ouroboros.Application.Personality.Consciousness;
using Ouroboros.Application.Services;
using Ouroboros.Core.DistinctionLearning;
using Ouroboros.Domain.DistinctionLearning;
using Ouroboros.Domain.Learning;

/// <summary>
/// Extension methods for registering distinction learning services.
/// </summary>
public static class DistinctionLearningExtensions
{
    /// <summary>
    /// Adds full distinction learning capabilities to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="options">Optional configuration options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDistinctionLearning(
        this IServiceCollection services,
        DistinctionLearningOptions? options = null)
    {
        options ??= DistinctionLearningOptions.Default;

        // Core services
        services.AddSingleton<ConsciousnessDream>();
        services.AddSingleton<IDistinctionLearner, DistinctionLearner>();

        // Storage
        var storageConfig = options.StorageConfig ?? DistinctionStorageConfig.Default;
        services.AddSingleton(storageConfig);
        services.AddSingleton<IDistinctionWeightStorage>(sp =>
            new FileSystemDistinctionWeightStorage(
                storageConfig,
                sp.GetService<Microsoft.Extensions.Logging.ILogger<FileSystemDistinctionWeightStorage>>()));

        // PEFT integration
        services.AddSingleton<DistinctionPeftAdapter>();

        // Pipeline middleware
        if (options.EnablePipelineIntegration)
        {
            services.AddSingleton<DistinctionLearningMiddleware>();
        }

        // Background consolidation
        if (options.EnableBackgroundConsolidation)
        {
            services.AddSingleton<IHostedService>(sp =>
                new DistinctionConsolidationService(
                    sp.GetRequiredService<IDistinctionLearner>(),
                    sp.GetRequiredService<IDistinctionWeightStorage>(),
                    storageConfig,
                    sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<DistinctionConsolidationService>>(),
                    options.ConsolidationInterval));
        }

        return services;
    }
}

/// <summary>
/// Options for configuring distinction learning.
/// </summary>
public sealed record DistinctionLearningOptions
{
    /// <summary>
    /// Gets the storage configuration.
    /// </summary>
    public DistinctionStorageConfig? StorageConfig { get; init; }

    /// <summary>
    /// Gets a value indicating whether to enable pipeline integration.
    /// </summary>
    public bool EnablePipelineIntegration { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether to enable background consolidation.
    /// </summary>
    public bool EnableBackgroundConsolidation { get; init; } = true;

    /// <summary>
    /// Gets the consolidation interval.
    /// </summary>
    public TimeSpan ConsolidationInterval { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// Gets the default options.
    /// </summary>
    public static DistinctionLearningOptions Default => new();
}
