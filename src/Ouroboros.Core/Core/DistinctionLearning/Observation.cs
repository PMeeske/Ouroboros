// <copyright file="Observation.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.DistinctionLearning;

/// <summary>
/// Represents an observation from which distinctions can be learned.
/// </summary>
public sealed record Observation(
    string Content,
    DateTime Timestamp,
    double PriorCertainty,
    Dictionary<string, object> Context);
