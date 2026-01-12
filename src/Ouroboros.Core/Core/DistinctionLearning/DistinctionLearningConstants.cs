// <copyright file="DistinctionLearningConstants.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.DistinctionLearning;

/// <summary>
/// Constants for distinction learning configuration.
/// </summary>
public static class DistinctionLearningConstants
{
    /// <summary>
    /// Default fitness threshold below which distinctions are dissolved.
    /// </summary>
    public const double DefaultFitnessThreshold = 0.3;

    /// <summary>
    /// Number of cycles between automatic dissolution runs.
    /// </summary>
    public const int DissolutionCycleInterval = 10;
}
