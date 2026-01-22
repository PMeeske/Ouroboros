// <copyright file="FailedEpisode.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Reflection;

/// <summary>
/// Represents a failed episode with detailed context for error analysis.
/// Immutable record following functional programming principles.
/// </summary>
/// <param name="Id">Unique identifier for this failed episode</param>
/// <param name="Timestamp">When the failure occurred</param>
/// <param name="Goal">The intended goal of the episode</param>
/// <param name="FailureReason">Description of why the episode failed</param>
/// <param name="ReasoningTrace">The reasoning trace data (can be PipelineBranch or other trace data)</param>
/// <param name="Context">Additional contextual information about the failure</param>
public sealed record FailedEpisode(
    Guid Id,
    DateTime Timestamp,
    string Goal,
    string FailureReason,
    object ReasoningTrace,
    IReadOnlyDictionary<string, object> Context);
