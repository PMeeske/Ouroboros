// <copyright file="DistinctionConsolidationService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Services;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Ouroboros.Core.DistinctionLearning;

/// <summary>
/// Background service that periodically consolidates distinctions.
/// - Dissolves low-fitness distinctions
/// - Merges related distinctions
/// - Cleans up old dissolved archives
/// </summary>
public sealed class DistinctionConsolidationService : BackgroundService
{
    private readonly IDistinctionLearner _learner;
    private readonly IDistinctionWeightStorage _storage;
    private readonly DistinctionStorageConfig _config;
    private readonly ILogger<DistinctionConsolidationService> _logger;
    private readonly TimeSpan _consolidationInterval;
    private readonly TimeSpan _errorRecoveryDelay;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistinctionConsolidationService"/> class.
    /// </summary>
    public DistinctionConsolidationService(
        IDistinctionLearner learner,
        IDistinctionWeightStorage storage,
        DistinctionStorageConfig config,
        ILogger<DistinctionConsolidationService> logger,
        TimeSpan? consolidationInterval = null,
        TimeSpan? errorRecoveryDelay = null)
    {
        _learner = learner ?? throw new ArgumentNullException(nameof(learner));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _consolidationInterval = consolidationInterval ?? TimeSpan.FromMinutes(10);
        _errorRecoveryDelay = errorRecoveryDelay ?? TimeSpan.FromMinutes(1);
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Distinction consolidation service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ConsolidateAsync(stoppingToken);
                await Task.Delay(_consolidationInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Consolidation cycle failed");
                await Task.Delay(_errorRecoveryDelay, stoppingToken);
            }
        }

        _logger.LogInformation("Distinction consolidation service stopped");
    }

    private async Task ConsolidateAsync(CancellationToken ct)
    {
        _logger.LogInformation("Starting distinction consolidation cycle");

        // 1. Get current state
        Result<List<DistinctionWeightMetadata>, string> listResult = null!;
        const int maxRetries = 3;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                listResult = await _storage.ListWeightsAsync(ct);
                break;
            }
            catch (Exception ex)
            {
                if (attempt == maxRetries - 1)
                {
                    _logger.LogError(ex, "Failed to list weights after {MaxRetries} attempts", maxRetries);
                    return;
                }
                _logger.LogWarning(ex, "Failed to list weights, attempt {Attempt}, retrying in 1s", attempt + 1);
                await Task.Delay(TimeSpan.FromSeconds(1), ct);
            }
        }

        if (listResult.IsFailure)
        {
            _logger.LogWarning("Failed to list weights: {Error}", listResult.Error);
            return;
        }

        var weights = listResult.Value;

        // 2. Dissolve low-fitness distinctions
        var lowFitness = weights
            .Where(w => !w.IsDissolved && w.Fitness < DistinctionLearningConstants.DefaultFitnessThreshold)
            .ToList();

        foreach (var w in lowFitness)
        {
            var dissolveResult = await _storage.DissolveWeightsAsync(w.Path, ct);
            if (dissolveResult.IsSuccess)
            {
                _logger.LogDebug("Dissolved low-fitness distinction {Id} (fitness: {Fitness:F2})", w.Id, w.Fitness);
            }
            else
            {
                _logger.LogWarning("Failed to dissolve {Id}: {Error}", w.Id, dissolveResult.Error);
            }
        }

        // 3. Clean up old dissolved archives
        var oldDissolved = weights
            .Where(w => w.IsDissolved &&
                        (DateTime.UtcNow - w.CreatedAt) > _config.DissolvedRetentionPeriod)
            .ToList();

        foreach (var w in oldDissolved)
        {
            try
            {
                if (File.Exists(w.Path))
                {
                    File.Delete(w.Path);
                    _logger.LogDebug("Cleaned up old dissolved distinction {Id}", w.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete old dissolved file {Path}", w.Path);
            }
        }

        // 4. Check storage limits
        var sizeResult = await _storage.GetTotalStorageSizeAsync(ct);
        if (sizeResult.IsSuccess && sizeResult.Value > _config.MaxTotalStorageBytes)
        {
            _logger.LogWarning(
                "Distinction storage exceeds limit: {Size} bytes > {Limit} bytes",
                sizeResult.Value,
                _config.MaxTotalStorageBytes);

            // Dissolve oldest low-value distinctions to free space
            await FreeStorageSpaceAsync(weights, ct);
        }

        _logger.LogInformation(
            "Consolidation complete: {Dissolved} dissolved, {Cleaned} cleaned up",
            lowFitness.Count,
            oldDissolved.Count);
    }

    private async Task FreeStorageSpaceAsync(List<DistinctionWeightMetadata> weights, CancellationToken ct)
    {
        // Find lowest fitness non-dissolved weights
        var toDissolve = weights
            .Where(w => !w.IsDissolved)
            .OrderBy(w => w.Fitness)
            .ThenBy(w => w.CreatedAt)
            .Take(10)
            .ToList();

        foreach (var w in toDissolve)
        {
            var dissolveResult = await _storage.DissolveWeightsAsync(w.Path, ct);
            if (dissolveResult.IsSuccess)
            {
                _logger.LogInformation("Dissolved {Id} to free storage space (fitness: {Fitness:F2})", w.Id, w.Fitness);
            }
        }
    }
}
