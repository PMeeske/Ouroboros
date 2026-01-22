// <copyright file="CognitiveProcessor.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Personality.Consciousness;

using Ouroboros.Agent.MetaAI.SelfModel;

/// <summary>
/// Integrates Pavlovian consciousness with Global Workspace Theory.
/// Implements the broadcast mechanism where conscious experiences compete for global attention.
/// </summary>
public sealed class CognitiveProcessor
{
    private readonly IGlobalWorkspace _globalWorkspace;
    private readonly PavlovianConsciousnessEngine _consciousness;
    private readonly CognitiveProcessorConfig _config;

    /// <summary>
    /// Initializes a new instance of the <see cref="CognitiveProcessor"/> class.
    /// </summary>
    public CognitiveProcessor(
        IGlobalWorkspace globalWorkspace,
        PavlovianConsciousnessEngine consciousness,
        CognitiveProcessorConfig? config = null)
    {
        ArgumentNullException.ThrowIfNull(globalWorkspace);
        ArgumentNullException.ThrowIfNull(consciousness);
        
        _globalWorkspace = globalWorkspace;
        _consciousness = consciousness;
        _config = config ?? CognitiveProcessorConfig.Default();
    }

    /// <summary>
    /// Processes input through consciousness and broadcasts salient experiences to global workspace.
    /// This is the core integration point between local processing and global broadcast.
    /// </summary>
    public ConsciousnessState ProcessAndBroadcast(string input, string? context = null)
    {
        ArgumentNullException.ThrowIfNull(input);
        
        // Process through consciousness engine
        ConsciousnessState state = _consciousness.ProcessInput(input, context);

        // Determine if this experience is salient enough for global broadcast
        double salience = CalculateSalience(state);

        if (salience >= _config.BroadcastThreshold)
        {
            BroadcastToWorkspace(state, input, salience);
        }

        // Update workspace with current focus
        UpdateWorkspaceFocus(state);

        return state;
    }

    /// <summary>
    /// Broadcasts a consciousness state to the global workspace.
    /// High-salience experiences become available to all cognitive processes.
    /// </summary>
    private void BroadcastToWorkspace(ConsciousnessState state, string input, double salience)
    {
        // Determine priority based on salience and arousal
        WorkspacePriority priority = DeterminePriority(salience, state.Arousal);

        // Create workspace item for the conscious experience
        string content = FormatConsciousExperience(state, input);
        
        var tags = new List<string>
        {
            "consciousness",
            state.DominantEmotion,
            $"arousal-{GetArousalLevel(state.Arousal)}"
        };

        // Add active association tags
        tags.AddRange(state.ActiveAssociations.Take(3).Select(a => $"assoc-{a.GetHashCode()}"));

        WorkspaceItem item = _globalWorkspace.AddItem(
            content,
            priority,
            "CognitiveProcessor",
            tags,
            lifetime: TimeSpan.FromMinutes(_config.ConsciousExperienceLifetimeMinutes));

        // If critical, broadcast immediately
        if (priority >= WorkspacePriority.High)
        {
            _globalWorkspace.BroadcastItem(item, $"High-salience conscious experience (salience: {salience:F2})");
        }
    }

    /// <summary>
    /// Updates workspace with current attentional focus.
    /// </summary>
    private void UpdateWorkspaceFocus(ConsciousnessState state)
    {
        if (state.AttentionalSpotlight.Length > 0)
        {
            string focusContent = $"Attention: {string.Join(", ", state.AttentionalSpotlight)}";
            
            _globalWorkspace.AddItem(
                focusContent,
                WorkspacePriority.Normal,
                "Attention",
                new List<string> { "attention", "focus" },
                lifetime: TimeSpan.FromSeconds(30));
        }
    }

    /// <summary>
    /// Calculates salience score for a consciousness state.
    /// Higher salience = more likely to be broadcast globally.
    /// </summary>
    private double CalculateSalience(ConsciousnessState state)
    {
        // Factors that contribute to salience:
        // 1. Arousal (high arousal = more salient)
        double arousalComponent = state.Arousal * 0.4;

        // 2. Extreme valence (very positive or negative)
        double valenceComponent = Math.Abs(state.Valence) * 0.2;

        // 3. Number of active associations (more associations = more salient)
        double associationComponent = Math.Min(1.0, state.ActiveAssociations.Count / 5.0) * 0.2;

        // 4. Awareness/meta-cognition level
        double awarenessComponent = state.Awareness * 0.2;

        return arousalComponent + valenceComponent + associationComponent + awarenessComponent;
    }

    /// <summary>
    /// Determines workspace priority based on salience and arousal.
    /// </summary>
    private WorkspacePriority DeterminePriority(double salience, double arousal)
    {
        // Critical: very high salience or extreme arousal
        if (salience > 0.8 || arousal > 0.9)
        {
            return WorkspacePriority.Critical;
        }

        // High: high salience or high arousal
        if (salience > 0.6 || arousal > 0.7)
        {
            return WorkspacePriority.High;
        }

        // Normal: moderate salience
        if (salience > 0.4)
        {
            return WorkspacePriority.Normal;
        }

        return WorkspacePriority.Low;
    }

    /// <summary>
    /// Formats a conscious experience for workspace broadcast.
    /// </summary>
    private string FormatConsciousExperience(ConsciousnessState state, string input)
    {
        var parts = new List<string>
        {
            $"Focus: {state.CurrentFocus}",
            $"Emotion: {state.DominantEmotion}",
            $"Arousal: {state.Arousal:F2}",
            $"Valence: {state.Valence:F2}"
        };

        if (state.AttentionalSpotlight.Length > 0)
        {
            parts.Add($"Spotlight: {string.Join(", ", state.AttentionalSpotlight)}");
        }

        if (state.ActiveAssociations.Count > 0)
        {
            parts.Add($"Associations: {state.ActiveAssociations.Count} active");
        }

        return $"[{string.Join(" | ", parts)}]";
    }

    /// <summary>
    /// Gets a categorical arousal level description.
    /// </summary>
    private string GetArousalLevel(double arousal)
    {
        return arousal switch
        {
            > 0.8 => "very-high",
            > 0.6 => "high",
            > 0.4 => "medium",
            > 0.2 => "low",
            _ => "very-low"
        };
    }

    /// <summary>
    /// Retrieves relevant context from global workspace to inform consciousness processing.
    /// Implements the "reading" side of global workspace - accessing broadcast information.
    /// </summary>
    public List<WorkspaceItem> GetRelevantContext(ConsciousnessState state, int maxItems = 5)
    {
        // Build query tags from current consciousness state
        var queryTags = new List<string> { state.DominantEmotion };

        // Add drive-based tags
        var topDrives = state.ActiveDrives
            .OrderByDescending(kvp => kvp.Value)
            .Take(2)
            .Select(kvp => kvp.Key);
        queryTags.AddRange(topDrives);

        // Search workspace for relevant items
        List<WorkspaceItem> relevantItems = _globalWorkspace.SearchByTags(queryTags);

        return relevantItems.Take(maxItems).ToList();
    }

    /// <summary>
    /// Applies reinforcement learning based on workspace feedback.
    /// If high-priority items in workspace align with consciousness state, reinforce.
    /// </summary>
    public void IntegrateWorkspaceFeedback()
    {
        List<WorkspaceItem> highPriorityItems = _globalWorkspace.GetHighPriorityItems();

        foreach (WorkspaceItem item in highPriorityItems.Take(3))
        {
            // If workspace item is related to consciousness, this validates the response
            if (item.Source == "CognitiveProcessor" && item.Tags.Contains("consciousness"))
            {
                // Extract associated content for reinforcement
                // This creates a feedback loop: successful broadcasts strengthen associations
                var associatedTags = item.Tags
                    .Where(t => t.StartsWith("assoc-"))
                    .ToList();

                if (associatedTags.Any())
                {
                    // Reinforce current consciousness patterns
                    // In real system, would map back to specific associations
                    double reinforcementStrength = (int)item.Priority / 3.0;
                    // Note: Actual reinforcement would need stimulus context
                    // This is a simplified demonstration
                }
            }
        }
    }

    /// <summary>
    /// Gets statistics about cognitive processing integration.
    /// </summary>
    public CognitiveProcessingStats GetStatistics()
    {
        WorkspaceStatistics workspaceStats = _globalWorkspace.GetStatistics();
        ConsciousnessState consciousnessState = _consciousness.CurrentState;

        int consciousItemsInWorkspace = _globalWorkspace
            .SearchByTags(new List<string> { "consciousness" })
            .Count;

        return new CognitiveProcessingStats(
            TotalWorkspaceItems: workspaceStats.TotalItems,
            ConsciousExperiencesInWorkspace: consciousItemsInWorkspace,
            CurrentArousal: consciousnessState.Arousal,
            CurrentValence: consciousnessState.Valence,
            CurrentAwareness: consciousnessState.Awareness,
            ActiveAssociations: consciousnessState.ActiveAssociations.Count,
            WorkspaceAverageAttention: workspaceStats.AverageAttentionWeight);
    }
}

/// <summary>
/// Configuration for cognitive processor.
/// </summary>
public sealed record CognitiveProcessorConfig(
    double BroadcastThreshold,
    double ConsciousExperienceLifetimeMinutes)
{
    /// <summary>
    /// Gets the default configuration.
    /// </summary>
    public static CognitiveProcessorConfig Default() => new(
        BroadcastThreshold: 0.5,        // Only broadcast moderately salient experiences
        ConsciousExperienceLifetimeMinutes: 5.0);  // Conscious experiences decay after 5 minutes
}

/// <summary>
/// Statistics about cognitive processing.
/// </summary>
public sealed record CognitiveProcessingStats(
    int TotalWorkspaceItems,
    int ConsciousExperiencesInWorkspace,
    double CurrentArousal,
    double CurrentValence,
    double CurrentAwareness,
    int ActiveAssociations,
    double WorkspaceAverageAttention);
