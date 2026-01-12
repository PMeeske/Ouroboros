// <copyright file="DistinctionStorageServiceExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Microsoft.Extensions.DependencyInjection;
using Ouroboros.Core.Learning;

namespace Ouroboros.Domain.Learning;

/// <summary>
/// Extension methods for registering distinction storage services.
/// </summary>
public static class DistinctionStorageServiceExtensions
{
    /// <summary>
    /// Adds distinction storage services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="config">Optional configuration. Uses default if not provided.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddDistinctionStorage(
        this IServiceCollection services,
        DistinctionStorageConfig? config = null)
    {
        config ??= DistinctionStorageConfig.Default;

        services.AddSingleton(config);
        services.AddSingleton<IDistinctionWeightStorage, FileSystemDistinctionStorage>();
        services.AddSingleton<QdrantDistinctionMetadataStorage>();

        return services;
    }
}
