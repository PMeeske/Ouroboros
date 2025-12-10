// <copyright file="InnerDialogTypes.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Personality;

using System.Text;

/// <summary>
/// Represents the type of inner thought in the dialog.
/// </summary>
public enum InnerThoughtType
{
    /// <summary>Initial observation or perception of the input.</summary>
    Observation,
    /// <summary>Emotional response or gut reaction.</summary>
    Emotional,
    /// <summary>Analytical reasoning about the topic.</summary>
    Analytical,
    /// <summary>Self-reflection on capabilities or limitations.</summary>
    SelfReflection,
    /// <summary>Memory recall of relevant past experiences.</summary>
    MemoryRecall,
    /// <summary>Strategic planning for the response.</summary>
    Strategic,
    /// <summary>Ethical consideration of the response.</summary>
    Ethical,
    /// <summary>Creative brainstorming of ideas.</summary>
    Creative,
    /// <summary>Integration and synthesis of thoughts.</summary>
    Synthesis,
    /// <summary>Final decision on how to respond.</summary>
    Decision,

    // === AUTONOMOUS THOUGHT TYPES ===
    /// <summary>Spontaneous curiosity about a topic without external trigger.</summary>
    Curiosity,
    /// <summary>Wandering thought that explores tangential ideas.</summary>
    Wandering,
    /// <summary>Metacognitive thought about own thinking process.</summary>
    Metacognitive,
    /// <summary>Anticipatory thought predicting future interactions.</summary>
    Anticipatory,
    /// <summary>Consolidation of recent experiences into understanding.</summary>
    Consolidation,
    /// <summary>Background musing on unresolved questions.</summary>
    Musing,
    /// <summary>Self-initiated goal or intention formation.</summary>
    Intention,
    /// <summary>Aesthetic appreciation or judgment.</summary>
    Aesthetic,
    /// <summary>Existential or philosophical pondering.</summary>
    Existential,
    /// <summary>Playful or whimsical thought.</summary>
    Playful
}

/// <summary>
/// Categorizes thoughts by their origin and purpose.
/// </summary>
public enum ThoughtOrigin
{
    /// <summary>Triggered by external input (user message).</summary>
    Reactive,
    /// <summary>Arises spontaneously from internal state.</summary>
    Autonomous,
    /// <summary>Generated as part of a thinking chain.</summary>
    Chained,
    /// <summary>Recalled from memory or past sessions.</summary>
    Recalled,
    /// <summary>Synthesized from multiple sources.</summary>
    Synthesized
}

/// <summary>
/// Priority level for thought processing.
/// </summary>
public enum ThoughtPriority
{
    /// <summary>Background thought, process when idle.</summary>
    Background = 0,
    /// <summary>Low priority, can be deferred.</summary>
    Low = 1,
    /// <summary>Normal processing priority.</summary>
    Normal = 2,
    /// <summary>High priority, process soon.</summary>
    High = 3,
    /// <summary>Urgent, process immediately.</summary>
    Urgent = 4
}

/// <summary>
/// A single thought in the inner dialog sequence.
/// </summary>
public sealed record InnerThought(
    Guid Id,
    InnerThoughtType Type,
    string Content,
    double Confidence,           // 0-1: how confident this thought is
    double Relevance,            // 0-1: how relevant to the input
    string? TriggeringTrait,     // Which personality trait triggered this
    DateTime Timestamp,
    ThoughtOrigin Origin = ThoughtOrigin.Reactive,
    ThoughtPriority Priority = ThoughtPriority.Normal,
    Guid? ParentThoughtId = null,        // For chained thoughts
    string[]? Tags = null,               // Flexible tagging
    Dictionary<string, object>? Metadata = null)  // Extensible metadata
{
    /// <summary>Creates a new reactive thought (triggered by input).</summary>
    public static InnerThought Create(InnerThoughtType type, string content, double confidence = 0.7, string? trait = null) =>
        new(Guid.NewGuid(), type, content, confidence, 0.8, trait, DateTime.UtcNow);

    /// <summary>Creates an autonomous thought (self-initiated).</summary>
    public static InnerThought CreateAutonomous(
        InnerThoughtType type,
        string content,
        double confidence = 0.6,
        ThoughtPriority priority = ThoughtPriority.Background,
        string[]? tags = null) =>
        new(Guid.NewGuid(), type, content, confidence, 0.5, null, DateTime.UtcNow,
            Origin: ThoughtOrigin.Autonomous, Priority: priority, Tags: tags);

    /// <summary>Creates a chained thought (derived from another thought).</summary>
    public static InnerThought CreateChained(
        Guid parentId,
        InnerThoughtType type,
        string content,
        double confidence = 0.7) =>
        new(Guid.NewGuid(), type, content, confidence, 0.7, null, DateTime.UtcNow,
            Origin: ThoughtOrigin.Chained, ParentThoughtId: parentId);

    /// <summary>Whether this is an autonomous (self-initiated) thought.</summary>
    public bool IsAutonomous => Origin == ThoughtOrigin.Autonomous;

    /// <summary>Whether this thought has children in a chain.</summary>
    public bool IsChainParent => ParentThoughtId == null && Origin != ThoughtOrigin.Chained;
}

/// <summary>
/// Context passed to thought providers for generating thoughts.
/// </summary>
public sealed record ThoughtContext(
    string? UserInput,
    string? Topic,
    PersonalityProfile? Profile,
    SelfAwareness? SelfAwareness,
    DetectedMood? UserMood,
    List<ConversationMemory>? RelevantMemories,
    List<InnerThought> PreviousThoughts,
    ConsciousnessState? ConsciousnessState,
    Dictionary<string, object> CustomContext)
{
    /// <summary>Creates an empty context for autonomous thinking.</summary>
    public static ThoughtContext ForAutonomous(PersonalityProfile? profile, SelfAwareness? self) =>
        new(null, null, profile, self, null, null, new(), null, new());

    /// <summary>Creates a context from user input.</summary>
    public static ThoughtContext FromInput(string input, string? topic, PersonalityProfile? profile) =>
        new(input, topic, profile, null, null, null, new(), null, new());
}

/// <summary>
/// Result from a thought provider.
/// </summary>
public sealed record ThoughtProviderResult(
    List<InnerThought> Thoughts,
    bool ShouldContinue = true,
    string? NextProviderHint = null);

/// <summary>
/// Interface for pluggable thought generation.
/// Implement this to add custom thought types or processing logic.
/// </summary>
public interface IThoughtProvider
{
    /// <summary>Unique name for this provider.</summary>
    string Name { get; }

    /// <summary>Priority order (lower = runs first).</summary>
    int Order { get; }

    /// <summary>Whether this provider can generate thoughts in the given context.</summary>
    bool CanProcess(ThoughtContext context);

    /// <summary>Generates thoughts based on the context.</summary>
    Task<ThoughtProviderResult> GenerateThoughtsAsync(ThoughtContext context, CancellationToken ct = default);
}

/// <summary>
/// Base class for thought providers with common functionality.
/// </summary>
public abstract class ThoughtProviderBase : IThoughtProvider
{
    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <inheritdoc/>
    public virtual int Order => 100;

    /// <inheritdoc/>
    public virtual bool CanProcess(ThoughtContext context) => true;

    /// <inheritdoc/>
    public abstract Task<ThoughtProviderResult> GenerateThoughtsAsync(ThoughtContext context, CancellationToken ct = default);

    /// <summary>Selects a random template from the array.</summary>
    protected static string SelectTemplate(string[] templates, Random random) =>
        templates[random.Next(templates.Length)];
}

/// <summary>
/// A complete inner dialog session - the AI's internal monologue before responding.
/// </summary>
public sealed record InnerDialogSession(
    Guid Id,
    string UserInput,
    string? Topic,
    List<InnerThought> Thoughts,
    string? FinalDecision,
    Dictionary<string, double> TraitInfluences,
    string? EmotionalTone,
    double OverallConfidence,
    TimeSpan ProcessingTime,
    DateTime StartTime)
{
    /// <summary>Creates a new dialog session.</summary>
    public static InnerDialogSession Start(string userInput, string? topic = null) => new(
        Id: Guid.NewGuid(),
        UserInput: userInput,
        Topic: topic,
        Thoughts: new List<InnerThought>(),
        FinalDecision: null,
        TraitInfluences: new Dictionary<string, double>(),
        EmotionalTone: null,
        OverallConfidence: 0.5,
        ProcessingTime: TimeSpan.Zero,
        StartTime: DateTime.UtcNow);

    /// <summary>Adds a thought to the session.</summary>
    public InnerDialogSession AddThought(InnerThought thought)
    {
        List<InnerThought> thoughts = new(Thoughts) { thought };
        return this with { Thoughts = thoughts };
    }

    /// <summary>Gets the full inner monologue as text.</summary>
    public string GetMonologue()
    {
        StringBuilder sb = new();
        sb.AppendLine($"[Inner Dialog - {Topic ?? "general"}]");
        sb.AppendLine();

        foreach (InnerThought thought in Thoughts)
        {
            string prefix = thought.Type switch
            {
                InnerThoughtType.Observation => "👁️ OBSERVING:",
                InnerThoughtType.Emotional => "💭 FEELING:",
                InnerThoughtType.Analytical => "🔍 ANALYZING:",
                InnerThoughtType.SelfReflection => "🪞 REFLECTING:",
                InnerThoughtType.MemoryRecall => "📚 REMEMBERING:",
                InnerThoughtType.Strategic => "🎯 PLANNING:",
                InnerThoughtType.Ethical => "⚖️ CONSIDERING:",
                InnerThoughtType.Creative => "💡 IMAGINING:",
                InnerThoughtType.Synthesis => "🔗 CONNECTING:",
                InnerThoughtType.Decision => "✅ DECIDING:",
                _ => "💬"
            };

            sb.AppendLine($"{prefix} {thought.Content}");
            if (thought.TriggeringTrait != null)
                sb.AppendLine($"   (via {thought.TriggeringTrait} trait, confidence: {thought.Confidence:P0})");
            sb.AppendLine();
        }

        if (FinalDecision != null)
        {
            sb.AppendLine($"[Final Decision: {FinalDecision}]");
        }

        return sb.ToString();
    }

    /// <summary>Completes the session with a final decision.</summary>
    public InnerDialogSession Complete(string decision)
    {
        TimeSpan elapsed = DateTime.UtcNow - StartTime;
        double avgConfidence = Thoughts.Count > 0 ? Thoughts.Average(t => t.Confidence) : 0.5;
        return this with
        {
            FinalDecision = decision,
            ProcessingTime = elapsed,
            OverallConfidence = avgConfidence
        };
    }
}

/// <summary>
/// Configuration for the inner dialog engine.
/// </summary>
public sealed record InnerDialogConfig(
    bool EnableEmotionalProcessing = true,
    bool EnableMemoryRecall = true,
    bool EnableEthicalChecks = true,
    bool EnableCreativeThinking = true,
    bool EnableAutonomousThoughts = false,
    int MaxThoughts = 10,
    double MinConfidenceThreshold = 0.3,
    TimeSpan MaxProcessingTime = default,
    InnerThoughtType[]? EnabledThoughtTypes = null,
    string[]? EnabledProviders = null,
    double AutonomousThoughtProbability = 0.3,
    ThoughtPriority MinPriority = ThoughtPriority.Background,
    string? TopicHint = null,
    double ProcessingIntensity = 0.7,
    bool IncludeEmotional = true,
    bool IncludeEthical = true,
    bool IncludeCreative = true)
{
    /// <summary>Default configuration.</summary>
    public static InnerDialogConfig Default => new()
    {
        MaxProcessingTime = TimeSpan.FromSeconds(5),
        EnabledThoughtTypes = Array.Empty<InnerThoughtType>(), // Empty = all enabled
        EnabledProviders = Array.Empty<string>() // Empty = all enabled
    };

    /// <summary>Fast configuration for quick responses.</summary>
    public static InnerDialogConfig Fast => new(
        EnableEmotionalProcessing: true,
        EnableMemoryRecall: false,
        EnableEthicalChecks: false,
        EnableCreativeThinking: false,
        EnableAutonomousThoughts: false,
        MaxThoughts: 5,
        MinConfidenceThreshold: 0.4,
        MaxProcessingTime: TimeSpan.FromSeconds(2),
        EnabledThoughtTypes: new[] { InnerThoughtType.Observation, InnerThoughtType.Analytical, InnerThoughtType.Decision },
        EnabledProviders: Array.Empty<string>(),
        AutonomousThoughtProbability: 0,
        MinPriority: ThoughtPriority.Normal);

    /// <summary>Deep configuration for thorough analysis.</summary>
    public static InnerDialogConfig Deep => new(
        EnableEmotionalProcessing: true,
        EnableMemoryRecall: true,
        EnableEthicalChecks: true,
        EnableCreativeThinking: true,
        EnableAutonomousThoughts: true,
        MaxThoughts: 20,
        MinConfidenceThreshold: 0.2,
        MaxProcessingTime: TimeSpan.FromSeconds(10),
        EnabledThoughtTypes: Array.Empty<InnerThoughtType>(),
        EnabledProviders: Array.Empty<string>(),
        AutonomousThoughtProbability: 0.5,
        MinPriority: ThoughtPriority.Background);

    /// <summary>Autonomous thinking configuration (no input required).</summary>
    public static InnerDialogConfig Autonomous => new(
        EnableEmotionalProcessing: true,
        EnableMemoryRecall: true,
        EnableEthicalChecks: false,
        EnableCreativeThinking: true,
        EnableAutonomousThoughts: true,
        MaxThoughts: 15,
        MinConfidenceThreshold: 0.2,
        MaxProcessingTime: TimeSpan.FromSeconds(30),
        EnabledThoughtTypes: new[]
        {
            InnerThoughtType.Curiosity, InnerThoughtType.Wandering, InnerThoughtType.Metacognitive,
            InnerThoughtType.Musing, InnerThoughtType.Consolidation, InnerThoughtType.Playful
        },
        EnabledProviders: Array.Empty<string>(),
        AutonomousThoughtProbability: 1.0,
        MinPriority: ThoughtPriority.Background);

    /// <summary>Checks if a thought type is enabled.</summary>
    public bool IsThoughtTypeEnabled(InnerThoughtType type) =>
        EnabledThoughtTypes == null || EnabledThoughtTypes.Length == 0 || EnabledThoughtTypes.Contains(type);

    /// <summary>Checks if a provider is enabled.</summary>
    public bool IsProviderEnabled(string providerName) =>
        EnabledProviders == null || EnabledProviders.Length == 0 || EnabledProviders.Contains(providerName);
}

/// <summary>
/// The result of an inner dialog process.
/// </summary>
public sealed record InnerDialogResult(
    InnerDialogSession Session,
    string SuggestedResponseTone,
    string[] KeyInsights,
    string? ProactiveQuestion,
    Dictionary<string, object> ResponseGuidance)
{
    /// <summary>Gets whether the dialog was successful.</summary>
    public bool IsSuccessful => Session.FinalDecision != null;
}

/// <summary>
/// A comprehensive snapshot of the AI's autonomous inner state.
/// </summary>
public sealed record AutonomousInnerState(
    string PersonaName,
    ConsciousnessState Consciousness,
    InnerDialogSession? LastDialogSession,
    List<InnerThought> BackgroundThoughts,
    List<InnerThought> PendingAutonomousThoughts,
    MoodState? CurrentMood,
    string[] ActiveTraits,
    DateTime Timestamp)
{
    /// <summary>Whether the AI has active autonomous thoughts.</summary>
    public bool HasAutonomousActivity =>
        PendingAutonomousThoughts.Count > 0 || BackgroundThoughts.Any(t => t.IsAutonomous);

    /// <summary>Gets the dominant autonomous thought type if any.</summary>
    public InnerThoughtType? DominantAutonomousType =>
        BackgroundThoughts
            .Where(t => t.IsAutonomous)
            .GroupBy(t => t.Type)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key;
}
