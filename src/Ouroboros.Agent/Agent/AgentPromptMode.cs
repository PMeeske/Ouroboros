// <copyright file="AgentPromptMode.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Agent;

/// <summary>
/// Defines the different modes for agent prompt processing.
/// </summary>
public enum AgentPromptMode
{
    /// <summary>
    /// Standard mode - generates a normal response without self-reflection.
    /// </summary>
    Standard,

    /// <summary>
    /// Self-critique mode - implements Draft → Critique → Improve loop
    /// for iterative refinement of responses.
    /// </summary>
    SelfCritique,

    /// <summary>
    /// Ouroboros mode - full recursive self-improvement with depth limits
    /// for advanced iterative reasoning.
    /// </summary>
    Ouroboros,
}
