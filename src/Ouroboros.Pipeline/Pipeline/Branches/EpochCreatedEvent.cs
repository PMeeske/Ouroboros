// <copyright file="EpochCreatedEvent.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Domain.Events;

namespace Ouroboros.Pipeline.Branches;

/// <summary>
/// Event representing the creation of a new epoch snapshot in the global projection service.
/// Captures a point-in-time snapshot of the system's evolutionary state.
/// </summary>
/// <param name="Id">Unique identifier for this event.</param>
/// <param name="Epoch">The epoch snapshot that was created.</param>
/// <param name="Timestamp">When the epoch was created.</param>
public sealed record EpochCreatedEvent(
    Guid Id,
    EpochSnapshot Epoch,
    DateTime Timestamp) : PipelineEvent(Id, "EpochCreated", Timestamp)
{
    /// <summary>
    /// Creates an EpochCreatedEvent from an epoch snapshot.
    /// </summary>
    /// <param name="epoch">The epoch snapshot.</param>
    /// <returns>A new EpochCreatedEvent.</returns>
    public static EpochCreatedEvent FromEpoch(EpochSnapshot epoch)
    {
        return new EpochCreatedEvent(
            Guid.NewGuid(),
            epoch,
            epoch.CreatedAt);
    }
}
