// <copyright file="EpisodeId.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Memory;

/// <summary>
/// Strongly-typed identifier for an episode in the episodic memory system.
/// </summary>
/// <param name="Value">The unique identifier for this episode.</param>
public sealed record EpisodeId(Guid Value);
