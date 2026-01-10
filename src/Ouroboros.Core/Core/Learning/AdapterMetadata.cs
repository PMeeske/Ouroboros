// <copyright file="AdapterMetadata.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Learning;

/// <summary>
/// Metadata for a trained adapter, stored in vector database.
/// </summary>
/// <param name="Id">Unique identifier for the adapter.</param>
/// <param name="TaskName">Name of the task this adapter is trained for.</param>
/// <param name="Config">Configuration used to create the adapter.</param>
/// <param name="BlobStoragePath">Path to the adapter weights in blob storage.</param>
/// <param name="CreatedAt">Timestamp when the adapter was created.</param>
/// <param name="LastTrainedAt">Timestamp of the last training session.</param>
/// <param name="TrainingExampleCount">Number of examples used for training.</param>
/// <param name="PerformanceScore">Optional performance score for the adapter.</param>
public sealed record AdapterMetadata(
    AdapterId Id,
    string TaskName,
    AdapterConfig Config,
    string BlobStoragePath,
    DateTime CreatedAt,
    DateTime LastTrainedAt,
    int TrainingExampleCount,
    double? PerformanceScore = null)
{
    /// <summary>
    /// Creates initial metadata for a newly created adapter.
    /// </summary>
    /// <param name="id">The adapter ID.</param>
    /// <param name="taskName">The task name.</param>
    /// <param name="config">The adapter configuration.</param>
    /// <param name="blobStoragePath">Path to blob storage.</param>
    /// <returns>New adapter metadata.</returns>
    public static AdapterMetadata Create(AdapterId id, string taskName, AdapterConfig config, string blobStoragePath)
    {
        var now = DateTime.UtcNow;
        return new AdapterMetadata(
            Id: id,
            TaskName: taskName,
            Config: config,
            BlobStoragePath: blobStoragePath,
            CreatedAt: now,
            LastTrainedAt: now,
            TrainingExampleCount: 0);
    }

    /// <summary>
    /// Updates the metadata after a training session.
    /// </summary>
    /// <param name="exampleCount">Number of examples trained on.</param>
    /// <param name="performanceScore">Optional performance score.</param>
    /// <returns>Updated metadata.</returns>
    public AdapterMetadata WithTraining(int exampleCount, double? performanceScore = null) =>
        this with
        {
            LastTrainedAt = DateTime.UtcNow,
            TrainingExampleCount = this.TrainingExampleCount + exampleCount,
            PerformanceScore = performanceScore ?? this.PerformanceScore,
        };
}
