// <copyright file="Observation.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Learning;

/// <summary>
/// Represents an observation made during a learning cycle.
/// </summary>
/// <param name="Content">Content of the observation.</param>
/// <param name="Timestamp">When the observation was made.</param>
/// <param name="PriorCertainty">Prior epistemic certainty before this observation.</param>
public record Observation(
    string Content,
    DateTime Timestamp,
    double PriorCertainty);
