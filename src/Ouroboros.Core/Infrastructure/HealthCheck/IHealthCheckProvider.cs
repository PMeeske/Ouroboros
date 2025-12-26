// <copyright file="IHealthCheckProvider.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Infrastructure.HealthCheck;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Represents a health check provider that can verify the status of a specific component.
/// </summary>
public interface IHealthCheckProvider
{
    /// <summary>
    /// Gets the name of the component being checked.
    /// </summary>
    string ComponentName { get; }

    /// <summary>
    /// Checks the health of the component.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Health check result.</returns>
    Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default);
}
