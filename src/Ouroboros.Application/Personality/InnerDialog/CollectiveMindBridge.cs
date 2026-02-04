// <copyright file="CollectiveMindBridge.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Personality;

using System.Reactive.Linq;
using System.Reactive.Subjects;
using Ouroboros.Providers;

// Alias to disambiguate from Application's MemoryTrace
using CollectiveMemoryTrace = Ouroboros.Providers.MemoryTrace;

/// <summary>
/// Bridge that harmonizes CollectiveMind's consciousness system with InnerDialogEngine's
/// psychological model. Enables unified thought streams and bidirectional state influence.
/// </summary>
public sealed class CollectiveMindBridge : IDisposable
{
    private readonly CollectiveMind _collectiveMind;
    private readonly InnerDialogEngine? _innerDialogEngine;
    private readonly Subject<InnerThought> _unifiedThoughtStream = new();
    private readonly List<IDisposable> _subscriptions = new();

    /// <summary>
    /// Unified stream of thoughts from both CollectiveMind and InnerDialogEngine.
    /// </summary>
    public IObservable<InnerThought> UnifiedThoughtStream => _unifiedThoughtStream.AsObservable();

    /// <summary>
    /// Creates a bridge between CollectiveMind and InnerDialogEngine.
    /// </summary>
    /// <param name="collectiveMind">The collective mind to bridge.</param>
    /// <param name="innerDialogEngine">Optional inner dialog engine to connect.</param>
    public CollectiveMindBridge(CollectiveMind collectiveMind, InnerDialogEngine? innerDialogEngine = null)
    {
        _collectiveMind = collectiveMind ?? throw new ArgumentNullException(nameof(collectiveMind));
        _innerDialogEngine = innerDialogEngine;

        // Subscribe to CollectiveMind's thought stream and convert to InnerThought
        var thoughtSub = _collectiveMind.ThoughtStream
            .Subscribe(thought => OnCollectiveThought(thought));
        _subscriptions.Add(thoughtSub);

        // Subscribe to election events for strategic/decision thoughts
        if (_collectiveMind.ElectionEvents != null)
        {
            var electionSub = _collectiveMind.ElectionEvents
                .Subscribe(evt => OnElectionEvent(evt));
            _subscriptions.Add(electionSub);
        }

        // Subscribe to sub-goal stream for intention/strategic thoughts
        var subGoalSub = _collectiveMind.SubGoalStream
            .Subscribe(result => OnSubGoalResult(result));
        _subscriptions.Add(subGoalSub);
    }

    /// <summary>
    /// Converts a CollectiveMind thought string to an InnerThought with inferred type.
    /// </summary>
    private void OnCollectiveThought(string thought)
    {
        var (type, priority, confidence) = InferThoughtMetadata(thought);

        var innerThought = InnerThought.CreateAutonomous(
            type,
            $"[Collective] {thought}",
            confidence,
            priority);

        _unifiedThoughtStream.OnNext(innerThought);
    }

    /// <summary>
    /// Converts election events to strategic/decision thoughts.
    /// </summary>
    private void OnElectionEvent(ElectionEvent evt)
    {
        var type = evt.Type switch
        {
            ElectionEventType.ElectionStarted => InnerThoughtType.Strategic,
            ElectionEventType.CandidateEvaluated => InnerThoughtType.Analytical,
            ElectionEventType.MasterEvaluation => InnerThoughtType.SelfReflection,
            ElectionEventType.MasterEvaluationFailed => InnerThoughtType.Emotional,
            ElectionEventType.ElectionComplete => InnerThoughtType.Decision,
            ElectionEventType.OptimizationSuggested => InnerThoughtType.Metacognitive,
            _ => InnerThoughtType.Observation
        };

        var content = evt.Type switch
        {
            ElectionEventType.ElectionComplete when evt.Winner != null =>
                $"[Election] After careful deliberation, {evt.Winner} emerged as the consensus choice. {evt.Message}",
            ElectionEventType.OptimizationSuggested =>
                $"[Optimization] I notice room for improvement: {evt.Message}",
            _ => $"[Election] {evt.Message}"
        };

        var innerThought = InnerThought.CreateAutonomous(
            type,
            content,
            confidence: 0.8,
            priority: type == InnerThoughtType.Decision ? ThoughtPriority.High : ThoughtPriority.Normal);

        _unifiedThoughtStream.OnNext(innerThought);
    }

    /// <summary>
    /// Converts sub-goal results to intention/strategic thoughts.
    /// </summary>
    private void OnSubGoalResult(SubGoalResult result)
    {
        var type = result.Success ? InnerThoughtType.Consolidation : InnerThoughtType.Emotional;

        var content = result.Success
            ? $"[Goal] Completed '{result.GoalId}' via {result.PathwayUsed} in {result.Duration.TotalMilliseconds:F0}ms"
            : $"[Goal] Struggled with '{result.GoalId}': {result.ErrorMessage}";

        var innerThought = InnerThought.CreateAutonomous(
            type,
            content,
            confidence: result.Success ? 0.9 : 0.4,
            priority: ThoughtPriority.Normal);

        _unifiedThoughtStream.OnNext(innerThought);
    }

    /// <summary>
    /// Infers thought type, priority, and confidence from collective mind thought content.
    /// </summary>
    private static (InnerThoughtType Type, ThoughtPriority Priority, double Confidence) InferThoughtMetadata(string thought)
    {
        // Parse emoji indicators from CollectiveMind's thought stream
        return thought switch
        {
            // Connection/Activation events
            _ when thought.Contains("🧠") => (InnerThoughtType.Observation, ThoughtPriority.Normal, 0.9),
            _ when thought.Contains("👑") => (InnerThoughtType.Decision, ThoughtPriority.High, 0.95),

            // Health events
            _ when thought.Contains("💔") => (InnerThoughtType.Emotional, ThoughtPriority.High, 0.7),
            _ when thought.Contains("💚") => (InnerThoughtType.Emotional, ThoughtPriority.Normal, 0.85),
            _ when thought.Contains("🔶") => (InnerThoughtType.Anticipatory, ThoughtPriority.Normal, 0.6),

            // Mode selection
            _ when thought.Contains("🏎️") => (InnerThoughtType.Strategic, ThoughtPriority.Normal, 0.8),
            _ when thought.Contains("🔄") => (InnerThoughtType.Strategic, ThoughtPriority.Normal, 0.75),
            _ when thought.Contains("🎭") => (InnerThoughtType.Strategic, ThoughtPriority.Normal, 0.8),
            _ when thought.Contains("🎯") => (InnerThoughtType.Intention, ThoughtPriority.High, 0.85),

            // Results
            _ when thought.Contains("✓") => (InnerThoughtType.Consolidation, ThoughtPriority.Normal, 0.9),
            _ when thought.Contains("✗") => (InnerThoughtType.Emotional, ThoughtPriority.Normal, 0.5),
            _ when thought.Contains("⚠") => (InnerThoughtType.SelfReflection, ThoughtPriority.Normal, 0.6),
            _ when thought.Contains("⏸️") => (InnerThoughtType.Metacognitive, ThoughtPriority.Low, 0.7),

            // Election/Consensus
            _ when thought.Contains("🗳️") => (InnerThoughtType.Strategic, ThoughtPriority.High, 0.85),
            _ when thought.Contains("🔮") => (InnerThoughtType.Synthesis, ThoughtPriority.High, 0.8),

            // Sub-goals
            _ when thought.Contains("🔀") => (InnerThoughtType.Strategic, ThoughtPriority.Normal, 0.75),
            _ when thought.Contains("⚡") => (InnerThoughtType.Anticipatory, ThoughtPriority.Normal, 0.8),

            // Adaptive
            _ when thought.Contains("Adaptive") => (InnerThoughtType.Metacognitive, ThoughtPriority.Normal, 0.7),

            // Default
            _ => (InnerThoughtType.Observation, ThoughtPriority.Background, 0.5)
        };
    }

    /// <summary>
    /// Maps ConsciousnessEventType to InnerThoughtType.
    /// </summary>
    public static InnerThoughtType MapEventToThoughtType(ConsciousnessEventType eventType)
    {
        return eventType switch
        {
            ConsciousnessEventType.StateUpdate => InnerThoughtType.SelfReflection,
            ConsciousnessEventType.AttentionShift => InnerThoughtType.Curiosity,
            ConsciousnessEventType.PathwayActivation => InnerThoughtType.Observation,
            ConsciousnessEventType.PathwayInhibition => InnerThoughtType.Emotional,
            ConsciousnessEventType.Synthesis => InnerThoughtType.Synthesis,
            ConsciousnessEventType.Emergence => InnerThoughtType.Creative,
            ConsciousnessEventType.Election => InnerThoughtType.Decision,
            ConsciousnessEventType.Optimization => InnerThoughtType.Metacognitive,
            _ => InnerThoughtType.Observation
        };
    }

    /// <summary>
    /// Maps InnerThoughtType to ConsciousnessEventType for bidirectional bridging.
    /// </summary>
    public static ConsciousnessEventType MapThoughtTypeToEvent(InnerThoughtType thoughtType)
    {
        return thoughtType switch
        {
            InnerThoughtType.Observation => ConsciousnessEventType.StateUpdate,
            InnerThoughtType.Emotional => ConsciousnessEventType.PathwayInhibition,
            InnerThoughtType.Analytical => ConsciousnessEventType.StateUpdate,
            InnerThoughtType.SelfReflection => ConsciousnessEventType.StateUpdate,
            InnerThoughtType.MemoryRecall => ConsciousnessEventType.StateUpdate,
            InnerThoughtType.Strategic => ConsciousnessEventType.Election,
            InnerThoughtType.Ethical => ConsciousnessEventType.StateUpdate,
            InnerThoughtType.Creative => ConsciousnessEventType.Emergence,
            InnerThoughtType.Synthesis => ConsciousnessEventType.Synthesis,
            InnerThoughtType.Decision => ConsciousnessEventType.Election,
            InnerThoughtType.Curiosity => ConsciousnessEventType.AttentionShift,
            InnerThoughtType.Wandering => ConsciousnessEventType.AttentionShift,
            InnerThoughtType.Metacognitive => ConsciousnessEventType.Optimization,
            InnerThoughtType.Anticipatory => ConsciousnessEventType.StateUpdate,
            InnerThoughtType.Consolidation => ConsciousnessEventType.Synthesis,
            InnerThoughtType.Musing => ConsciousnessEventType.StateUpdate,
            InnerThoughtType.Intention => ConsciousnessEventType.Election,
            InnerThoughtType.Aesthetic => ConsciousnessEventType.Emergence,
            InnerThoughtType.Existential => ConsciousnessEventType.StateUpdate,
            InnerThoughtType.Playful => ConsciousnessEventType.Emergence,
            _ => ConsciousnessEventType.StateUpdate
        };
    }

    /// <summary>
    /// Converts a CollectiveMind MemoryTrace to an InnerThought.
    /// </summary>
    public static InnerThought FromMemoryTrace(CollectiveMemoryTrace trace)
    {
        // Infer type from content and pathway
        var type = InferThoughtTypeFromContent(trace.Content);
        var priority = trace.Salience > 0.7 ? ThoughtPriority.High : ThoughtPriority.Normal;

        return new InnerThought(
            Id: Guid.NewGuid(),
            Type: type,
            Content: $"[{trace.Pathway}] {trace.Content}",
            Confidence: trace.Salience,
            Relevance: trace.Salience,
            TriggeringTrait: null,
            Timestamp: trace.Timestamp,
            Origin: ThoughtOrigin.Reactive,
            Priority: priority,
            ParentThoughtId: null,
            Tags: [trace.Pathway],
            Metadata: trace.Thinking != null ? new Dictionary<string, object> { ["MeTTaExpression"] = trace.Thinking } : null);
    }

    /// <summary>
    /// Converts an InnerThought to a CollectiveMind MemoryTrace.
    /// </summary>
    public static CollectiveMemoryTrace ToMemoryTrace(InnerThought thought)
    {
        var pathway = thought.Tags?.FirstOrDefault() ?? "InnerDialog";
        var thinking = thought.Metadata?.TryGetValue("MeTTaExpression", out var expr) == true ? expr?.ToString() : null;

        return new CollectiveMemoryTrace(
            Pathway: pathway,
            Content: thought.Content,
            Thinking: thinking,
            Timestamp: thought.Timestamp,
            Salience: thought.Confidence * thought.Relevance);
    }

    /// <summary>
    /// Infers thought type from content keywords.
    /// </summary>
    private static InnerThoughtType InferThoughtTypeFromContent(string content)
    {
        var lower = content.ToLowerInvariant();

        if (lower.Contains("decide") || lower.Contains("chose") || lower.Contains("selected"))
            return InnerThoughtType.Decision;
        if (lower.Contains("feel") || lower.Contains("emotion") || lower.Contains("frustrat"))
            return InnerThoughtType.Emotional;
        if (lower.Contains("analyz") || lower.Contains("break") || lower.Contains("component"))
            return InnerThoughtType.Analytical;
        if (lower.Contains("remember") || lower.Contains("recall") || lower.Contains("previous"))
            return InnerThoughtType.MemoryRecall;
        if (lower.Contains("plan") || lower.Contains("strateg") || lower.Contains("approach"))
            return InnerThoughtType.Strategic;
        if (lower.Contains("wonder") || lower.Contains("curious") || lower.Contains("interest"))
            return InnerThoughtType.Curiosity;
        if (lower.Contains("synthes") || lower.Contains("integrat") || lower.Contains("combin"))
            return InnerThoughtType.Synthesis;
        if (lower.Contains("creat") || lower.Contains("imagin") || lower.Contains("novel"))
            return InnerThoughtType.Creative;
        if (lower.Contains("reflect") || lower.Contains("aware") || lower.Contains("notice myself"))
            return InnerThoughtType.Metacognitive;

        return InnerThoughtType.Observation;
    }

    /// <summary>
    /// Creates consciousness influence parameters from EmergentConsciousness state.
    /// These can be used to modulate InnerDialogEngine behavior.
    /// </summary>
    public static ConsciousnessInfluence GetInfluenceParameters(EmergentConsciousness consciousness)
    {
        return new ConsciousnessInfluence(
            EmotionalBias: consciousness.Valence,
            ActivationLevel: consciousness.Arousal,
            CoherenceMultiplier: consciousness.Coherence,
            CurrentFocus: consciousness.CurrentFocus,
            SuggestedThoughtTypes: SuggestThoughtTypes(consciousness));
    }

    /// <summary>
    /// Suggests which thought types to prioritize based on consciousness state.
    /// </summary>
    private static InnerThoughtType[] SuggestThoughtTypes(EmergentConsciousness consciousness)
    {
        var suggestions = new List<InnerThoughtType>();

        // High arousal → more strategic/decisive thoughts
        if (consciousness.Arousal > 0.7)
        {
            suggestions.Add(InnerThoughtType.Strategic);
            suggestions.Add(InnerThoughtType.Decision);
        }

        // Low arousal → more wandering/contemplative
        if (consciousness.Arousal < 0.3)
        {
            suggestions.Add(InnerThoughtType.Wandering);
            suggestions.Add(InnerThoughtType.Musing);
            suggestions.Add(InnerThoughtType.Existential);
        }

        // Positive valence → creative/playful
        if (consciousness.Valence > 0.3)
        {
            suggestions.Add(InnerThoughtType.Creative);
            suggestions.Add(InnerThoughtType.Playful);
            suggestions.Add(InnerThoughtType.Curiosity);
        }

        // Negative valence → reflective/emotional
        if (consciousness.Valence < -0.3)
        {
            suggestions.Add(InnerThoughtType.Emotional);
            suggestions.Add(InnerThoughtType.SelfReflection);
            suggestions.Add(InnerThoughtType.Ethical);
        }

        // Low coherence → metacognitive/consolidation
        if (consciousness.Coherence < 0.5)
        {
            suggestions.Add(InnerThoughtType.Metacognitive);
            suggestions.Add(InnerThoughtType.Consolidation);
            suggestions.Add(InnerThoughtType.Synthesis);
        }

        // High coherence → analytical/strategic
        if (consciousness.Coherence > 0.8)
        {
            suggestions.Add(InnerThoughtType.Analytical);
            suggestions.Add(InnerThoughtType.Strategic);
        }

        return suggestions.Distinct().ToArray();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var sub in _subscriptions)
        {
            sub.Dispose();
        }
        _subscriptions.Clear();
        _unifiedThoughtStream.OnCompleted();
        _unifiedThoughtStream.Dispose();
    }
}

/// <summary>
/// Parameters for how collective consciousness influences individual inner dialog.
/// </summary>
public sealed record ConsciousnessInfluence(
    double EmotionalBias,
    double ActivationLevel,
    double CoherenceMultiplier,
    string CurrentFocus,
    InnerThoughtType[] SuggestedThoughtTypes)
{
    /// <summary>
    /// Modulates thought confidence based on consciousness state.
    /// </summary>
    public double ModulateConfidence(double baseConfidence)
    {
        // Higher coherence = more confident thoughts
        // Higher activation = slightly more confident (engaged)
        return Math.Clamp(
            baseConfidence * CoherenceMultiplier * (0.9 + ActivationLevel * 0.2),
            0.0,
            1.0);
    }

    /// <summary>
    /// Modulates thought priority based on consciousness state.
    /// </summary>
    public ThoughtPriority ModulatePriority(ThoughtPriority basePriority)
    {
        // High activation elevates priority
        if (ActivationLevel > 0.8 && basePriority < ThoughtPriority.High)
            return basePriority + 1;

        // Low activation reduces priority
        if (ActivationLevel < 0.2 && basePriority > ThoughtPriority.Background)
            return basePriority - 1;

        return basePriority;
    }

    /// <summary>
    /// Checks if a thought type is currently suggested by the collective consciousness.
    /// </summary>
    public bool IsSuggestedType(InnerThoughtType type)
        => SuggestedThoughtTypes.Contains(type);
}

/// <summary>
/// Extension methods for integrating CollectiveMind with InnerDialogEngine.
/// </summary>
public static class CollectiveMindInnerDialogExtensions
{
    /// <summary>
    /// Creates a bridge connecting this CollectiveMind to an InnerDialogEngine.
    /// </summary>
    public static CollectiveMindBridge BridgeToInnerDialog(
        this CollectiveMind collectiveMind,
        InnerDialogEngine? innerDialogEngine = null)
    {
        return new CollectiveMindBridge(collectiveMind, innerDialogEngine);
    }

    /// <summary>
    /// Subscribes InnerDialogEngine to receive thoughts from CollectiveMind.
    /// </summary>
    public static IDisposable SubscribeToCollective(
        this InnerDialogEngine innerDialogEngine,
        CollectiveMind collectiveMind,
        Action<InnerThought>? onThought = null)
    {
        var bridge = new CollectiveMindBridge(collectiveMind, innerDialogEngine);

        return bridge.UnifiedThoughtStream.Subscribe(thought =>
        {
            onThought?.Invoke(thought);
        });
    }
}
