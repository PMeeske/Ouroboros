// <copyright file="TaskDetectionStrategy.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Providers.Routing;

/// <summary>
/// Strategy for detecting task types from prompts.
/// </summary>
public enum TaskDetectionStrategy
{
    /// <summary>
    /// Use keyword-based heuristics for fast detection.
    /// </summary>
    Heuristic,

    /// <summary>
    /// Use length and complexity-based rules.
    /// </summary>
    RuleBased,

    /// <summary>
    /// Combine multiple detection methods.
    /// </summary>
    Hybrid,
}
