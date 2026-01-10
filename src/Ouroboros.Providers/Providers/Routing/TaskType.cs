// <copyright file="TaskType.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Providers.Routing;

/// <summary>
/// Types of tasks that can be detected for intelligent routing.
/// </summary>
public enum TaskType
{
    /// <summary>
    /// Simple, short queries or general conversation.
    /// </summary>
    Simple,

    /// <summary>
    /// Reasoning tasks requiring logical inference, chain-of-thought, or analysis.
    /// </summary>
    Reasoning,

    /// <summary>
    /// Planning tasks requiring strategy, decomposition, or multi-step approaches.
    /// </summary>
    Planning,

    /// <summary>
    /// Code generation, implementation, or programming tasks.
    /// </summary>
    Coding,

    /// <summary>
    /// Unknown or unclear task type (use default routing).
    /// </summary>
    Unknown,
}
