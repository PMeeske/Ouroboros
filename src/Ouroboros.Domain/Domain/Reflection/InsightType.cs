// <copyright file="InsightType.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Reflection;

/// <summary>
/// Types of insights that can be derived from performance analysis.
/// </summary>
public enum InsightType
{
    /// <summary>
    /// Identifies an area of strong performance.
    /// </summary>
    Strength,

    /// <summary>
    /// Identifies an area of weak performance.
    /// </summary>
    Weakness,

    /// <summary>
    /// Identifies a performance bottleneck.
    /// </summary>
    Bottleneck,

    /// <summary>
    /// Identifies a recurring pattern in behavior.
    /// </summary>
    Pattern,

    /// <summary>
    /// Identifies an anomalous or unexpected behavior.
    /// </summary>
    Anomaly
}
