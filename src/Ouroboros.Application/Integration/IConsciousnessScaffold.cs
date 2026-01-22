// <copyright file="IConsciousnessScaffold.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Integration;

using Ouroboros.Agent.MetaAI.SelfModel;
using Ouroboros.Core.Monads;
using Unit = Ouroboros.Core.Learning.Unit;

/// <summary>
/// Interface for the consciousness scaffold wrapping global workspace.
/// Provides metacognitive features and attention management for conscious-like processing.
/// </summary>
public interface IConsciousnessScaffold
{
    /// <summary>Gets the underlying global workspace.</summary>
    IGlobalWorkspace GlobalWorkspace { get; }

    /// <summary>
    /// Broadcasts content to the global workspace with high priority.
    /// Simulates bringing information into conscious awareness.
    /// </summary>
    /// <param name="content">The content to broadcast.</param>
    /// <param name="source">The source of the content.</param>
    /// <param name="tags">Optional tags for categorization.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the workspace item or error message.</returns>
    Task<Result<WorkspaceItem, string>> BroadcastToConsciousnessAsync(
        string content,
        string source,
        List<string>? tags = null,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves the current focus of attention from the workspace.
    /// Returns items with highest attention weights representing conscious focus.
    /// </summary>
    /// <param name="topK">Number of items to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing list of focused items or error message.</returns>
    Task<Result<List<WorkspaceItem>, string>> GetAttentionalFocusAsync(
        int topK = 5,
        CancellationToken ct = default);

    /// <summary>
    /// Performs metacognitive monitoring by analyzing workspace contents.
    /// Identifies patterns, conflicts, and opportunities for reflection.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing metacognitive insights or error message.</returns>
    Task<Result<MetacognitiveInsights, string>> MonitorMetacognitionAsync(
        CancellationToken ct = default);

    /// <summary>
    /// Integrates new information with current conscious state.
    /// Handles conflicts and updates attention based on relevance.
    /// </summary>
    /// <param name="newInfo">New information to integrate.</param>
    /// <param name="priority">Priority level for the new information.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or error message.</returns>
    Task<Result<Unit, string>> IntegrateInformationAsync(
        string newInfo,
        WorkspacePriority priority,
        CancellationToken ct = default);
}

/// <summary>
/// Represents insights from metacognitive monitoring.
/// </summary>
public sealed record MetacognitiveInsights(
    List<string> DetectedConflicts,
    List<string> IdentifiedPatterns,
    List<string> ReflectionOpportunities,
    double OverallCoherence,
    Dictionary<string, int> AttentionDistribution);
