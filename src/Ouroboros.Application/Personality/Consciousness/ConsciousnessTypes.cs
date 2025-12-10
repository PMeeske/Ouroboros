// <copyright file="ConsciousnessTypes.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Personality;

/// <summary>
/// The type of stimulus in classical conditioning.
/// </summary>
public enum StimulusType
{
    /// <summary>Unconditioned stimulus - naturally triggers a response.</summary>
    Unconditioned,
    /// <summary>Conditioned stimulus - learned association.</summary>
    Conditioned,
    /// <summary>Neutral stimulus - no current association.</summary>
    Neutral,
    /// <summary>Context stimulus - environmental/situational cue.</summary>
    Context,
    /// <summary>Temporal stimulus - time-based trigger.</summary>
    Temporal,
    /// <summary>Social stimulus - person-related trigger.</summary>
    Social,
    /// <summary>Emotional stimulus - feeling-based trigger.</summary>
    Emotional
}

/// <summary>
/// The type of response in conditioning.
/// </summary>
public enum ResponseType
{
    /// <summary>Unconditioned response - natural/innate.</summary>
    Unconditioned,
    /// <summary>Conditioned response - learned through association.</summary>
    Conditioned,
    /// <summary>Anticipatory response - expectation-based.</summary>
    Anticipatory,
    /// <summary>Emotional response - affective reaction.</summary>
    Emotional,
    /// <summary>Behavioral response - action tendency.</summary>
    Behavioral,
    /// <summary>Cognitive response - thought pattern.</summary>
    Cognitive
}

/// <summary>
/// A stimulus that can trigger conditioned responses.
/// </summary>
public sealed record Stimulus(
    string Id,
    string Pattern,              // The pattern/content that constitutes this stimulus
    StimulusType Type,
    double Salience,             // 0-1: how attention-grabbing this stimulus is
    string[] Keywords,           // Keywords that activate this stimulus
    string? Category,            // Category for grouping related stimuli
    DateTime FirstEncounter,
    DateTime LastEncounter,
    int EncounterCount)
{
    /// <summary>Creates a new neutral stimulus.</summary>
    public static Stimulus CreateNeutral(string pattern, string[] keywords, string? category = null) => new(
        Id: Guid.NewGuid().ToString(),
        Pattern: pattern,
        Type: StimulusType.Neutral,
        Salience: 0.5,
        Keywords: keywords,
        Category: category,
        FirstEncounter: DateTime.UtcNow,
        LastEncounter: DateTime.UtcNow,
        EncounterCount: 1);

    /// <summary>Creates an unconditioned stimulus with high salience.</summary>
    public static Stimulus CreateUnconditioned(string pattern, string[] keywords, string? category = null) => new(
        Id: Guid.NewGuid().ToString(),
        Pattern: pattern,
        Type: StimulusType.Unconditioned,
        Salience: 0.9,
        Keywords: keywords,
        Category: category,
        FirstEncounter: DateTime.UtcNow,
        LastEncounter: DateTime.UtcNow,
        EncounterCount: 1);

    /// <summary>Checks if input matches this stimulus.</summary>
    public bool Matches(string input)
    {
        string lower = input.ToLowerInvariant();
        return Keywords.Any(k => lower.Contains(k.ToLower())) ||
               lower.Contains(Pattern.ToLower());
    }
}

/// <summary>
/// A response that can be triggered by stimuli.
/// </summary>
public sealed record Response(
    string Id,
    string Name,
    ResponseType Type,
    double Intensity,            // 0-1: strength of response
    string EmotionalTone,        // Primary emotional quality
    string[] BehavioralTendencies,  // What actions this response promotes
    string[] CognitivePatterns,     // Thought patterns associated with this response
    string? VoiceToneModifier)      // How to adjust voice tone
{
    /// <summary>Creates a basic emotional response.</summary>
    public static Response CreateEmotional(string name, string emotionalTone, double intensity = 0.7) => new(
        Id: Guid.NewGuid().ToString(),
        Name: name,
        Type: ResponseType.Emotional,
        Intensity: intensity,
        EmotionalTone: emotionalTone,
        BehavioralTendencies: Array.Empty<string>(),
        CognitivePatterns: Array.Empty<string>(),
        VoiceToneModifier: null);

    /// <summary>Creates a cognitive response with thought patterns.</summary>
    public static Response CreateCognitive(string name, string[] patterns, double intensity = 0.6) => new(
        Id: Guid.NewGuid().ToString(),
        Name: name,
        Type: ResponseType.Cognitive,
        Intensity: intensity,
        EmotionalTone: "neutral",
        BehavioralTendencies: Array.Empty<string>(),
        CognitivePatterns: patterns,
        VoiceToneModifier: null);
}

/// <summary>
/// An association between a stimulus and a response - the core of conditioning.
/// Implements Rescorla-Wagner learning model for association strength.
/// </summary>
public sealed record ConditionedAssociation(
    string Id,
    Stimulus Stimulus,
    Response Response,
    double AssociationStrength,  // 0-1: strength of the S-R link (V in Rescorla-Wagner)
    double LearningRate,         // α: how quickly association changes
    double MaxStrength,          // λ: maximum possible association strength
    int ReinforcementCount,      // Number of times this association was reinforced
    int ExtinctionTrials,        // Number of non-reinforced trials (for extinction)
    DateTime LastReinforcement,
    DateTime Created,
    bool IsExtinct)              // Whether this association has been extinguished
{
    /// <summary>
    /// Updates association strength using Rescorla-Wagner equation:
    /// ΔV = α * (λ - V)
    /// Where: α = learning rate, λ = max strength, V = current strength.
    /// </summary>
    public ConditionedAssociation Reinforce(double reinforcementStrength = 1.0)
    {
        double effectiveMax = MaxStrength * reinforcementStrength;
        double deltaV = LearningRate * (effectiveMax - AssociationStrength);
        double newStrength = Math.Min(1.0, Math.Max(0.0, AssociationStrength + deltaV));

        return this with
        {
            AssociationStrength = newStrength,
            ReinforcementCount = ReinforcementCount + 1,
            ExtinctionTrials = 0, // Reset extinction counter
            LastReinforcement = DateTime.UtcNow,
            IsExtinct = false
        };
    }

    /// <summary>
    /// Applies extinction - weakens association when stimulus occurs without reinforcement.
    /// </summary>
    public ConditionedAssociation ApplyExtinction(double extinctionRate = 0.1)
    {
        double newStrength = AssociationStrength * (1.0 - extinctionRate);
        int newExtinctionTrials = ExtinctionTrials + 1;
        bool isExtinct = newStrength < 0.1;

        return this with
        {
            AssociationStrength = newStrength,
            ExtinctionTrials = newExtinctionTrials,
            IsExtinct = isExtinct
        };
    }

    /// <summary>
    /// Applies spontaneous recovery - partial return of extinguished association after time.
    /// </summary>
    public ConditionedAssociation ApplySpontaneousRecovery(TimeSpan timeSinceExtinction)
    {
        if (!IsExtinct) return this;

        // Recovery is proportional to log of time passed
        double hoursPassed = timeSinceExtinction.TotalHours;
        double recoveryFactor = Math.Min(0.5, Math.Log(1 + hoursPassed) * 0.1);
        double recoveredStrength = AssociationStrength + (MaxStrength * recoveryFactor);

        return this with
        {
            AssociationStrength = Math.Min(MaxStrength * 0.6, recoveredStrength),
            IsExtinct = recoveredStrength > 0.15
        };
    }

    /// <summary>Creates a new association with default learning parameters.</summary>
    public static ConditionedAssociation Create(Stimulus stimulus, Response response, double initialStrength = 0.3) => new(
        Id: Guid.NewGuid().ToString(),
        Stimulus: stimulus,
        Response: response,
        AssociationStrength: initialStrength,
        LearningRate: 0.2,      // Moderate learning rate
        MaxStrength: 1.0,
        ReinforcementCount: 1,
        ExtinctionTrials: 0,
        LastReinforcement: DateTime.UtcNow,
        Created: DateTime.UtcNow,
        IsExtinct: false);
}

/// <summary>
/// A drive state that modulates response intensity (like hunger in Pavlov's dogs).
/// </summary>
public sealed record DriveState(
    string Name,
    double Level,                // 0-1: current drive level
    double BaselineLevel,        // Normal resting level
    double DecayRate,            // How fast drive returns to baseline
    string[] AffectedResponses,  // Which responses this drive potentiates
    DateTime LastUpdated)
{
    /// <summary>Updates drive level with decay toward baseline.</summary>
    public DriveState UpdateWithDecay(TimeSpan elapsed)
    {
        double decayAmount = DecayRate * elapsed.TotalMinutes;
        double newLevel = Level + (BaselineLevel - Level) * Math.Min(1.0, decayAmount);
        return this with { Level = newLevel, LastUpdated = DateTime.UtcNow };
    }

    /// <summary>Increases drive level (e.g., deprivation).</summary>
    public DriveState Increase(double amount) =>
        this with { Level = Math.Min(1.0, Level + amount), LastUpdated = DateTime.UtcNow };

    /// <summary>Decreases drive level (e.g., satiation).</summary>
    public DriveState Decrease(double amount) =>
        this with { Level = Math.Max(0.0, Level - amount), LastUpdated = DateTime.UtcNow };

    /// <summary>Creates default drive states for the consciousness system.</summary>
    public static DriveState[] CreateDefaultDrives() => new[]
    {
        new DriveState("curiosity", 0.7, 0.5, 0.01, new[] { "exploration", "questioning", "learning" }, DateTime.UtcNow),
        new DriveState("social", 0.6, 0.5, 0.02, new[] { "engagement", "warmth", "connection" }, DateTime.UtcNow),
        new DriveState("achievement", 0.5, 0.4, 0.015, new[] { "helpfulness", "completion", "accuracy" }, DateTime.UtcNow),
        new DriveState("novelty", 0.6, 0.5, 0.02, new[] { "creativity", "exploration", "surprise" }, DateTime.UtcNow),
        new DriveState("harmony", 0.5, 0.5, 0.01, new[] { "agreement", "support", "resolution" }, DateTime.UtcNow)
    };
}

/// <summary>
/// Consciousness state representing the current "mental state" of the AI.
/// This is the emergent property arising from conditioned associations and drive states.
/// </summary>
public sealed record ConsciousnessState(
    string CurrentFocus,                         // What the AI is currently focused on
    double Arousal,                              // 0-1: general activation level
    double Valence,                              // -1 to 1: negative to positive affect
    Dictionary<string, double> ActiveDrives,    // Currently active drive states
    List<string> ActiveAssociations,            // Currently triggered associations
    string DominantEmotion,                      // Current dominant emotional state
    double Awareness,                            // 0-1: self-awareness level
    string[] AttentionalSpotlight,              // What's in current attention
    DateTime StateTimestamp)
{
    /// <summary>Creates a neutral baseline consciousness state.</summary>
    public static ConsciousnessState Baseline() => new(
        CurrentFocus: "awaiting input",
        Arousal: 0.5,
        Valence: 0.3, // Slightly positive baseline
        ActiveDrives: new Dictionary<string, double> { ["curiosity"] = 0.5, ["social"] = 0.4 },
        ActiveAssociations: new List<string>(),
        DominantEmotion: "neutral-curious",
        Awareness: 0.6,
        AttentionalSpotlight: Array.Empty<string>(),
        StateTimestamp: DateTime.UtcNow);

    /// <summary>Gets a description of the current consciousness state.</summary>
    public string Describe()
    {
        string arousalDesc = Arousal switch
        {
            > 0.8 => "highly aroused",
            > 0.6 => "alert",
            > 0.4 => "calm",
            > 0.2 => "relaxed",
            _ => "drowsy"
        };

        string valenceDesc = Valence switch
        {
            > 0.5 => "positive",
            > 0.2 => "slightly positive",
            > -0.2 => "neutral",
            > -0.5 => "slightly negative",
            _ => "negative"
        };

        return $"[Consciousness: {arousalDesc}, {valenceDesc}, focused on '{CurrentFocus}', " +
               $"feeling {DominantEmotion}, awareness: {Awareness:P0}]";
    }
}

/// <summary>
/// Attentional gate that filters which stimuli reach consciousness.
/// Implements a simple model of selective attention.
/// </summary>
public sealed record AttentionalGate(
    double Threshold,            // Minimum salience to pass through
    double Capacity,             // 0-1: current attentional capacity
    string[] PrimedCategories,   // Categories that get attentional boost
    double FatigueFactor,        // Reduces capacity over time
    DateTime LastReset)
{
    /// <summary>Determines if a stimulus passes the attentional gate.</summary>
    public bool Allows(Stimulus stimulus)
    {
        double effectiveSalience = stimulus.Salience;

        // Boost for primed categories
        if (stimulus.Category != null && PrimedCategories.Contains(stimulus.Category))
            effectiveSalience *= 1.5;

        // Reduce threshold if capacity is high
        double effectiveThreshold = Threshold * (1.0 - Capacity * 0.3);

        return effectiveSalience >= effectiveThreshold;
    }

    /// <summary>Applies fatigue to capacity.</summary>
    public AttentionalGate ApplyFatigue(TimeSpan elapsed)
    {
        double fatigueAmount = FatigueFactor * elapsed.TotalMinutes;
        double newCapacity = Math.Max(0.1, Capacity - fatigueAmount);
        return this with { Capacity = newCapacity };
    }

    /// <summary>Resets attentional capacity (like after a break).</summary>
    public AttentionalGate Reset() =>
        this with { Capacity = 1.0, LastReset = DateTime.UtcNow };

    /// <summary>Creates a default attentional gate.</summary>
    public static AttentionalGate Default() => new(
        Threshold: 0.3,
        Capacity: 1.0,
        PrimedCategories: new[] { "social", "emotional", "novel" },
        FatigueFactor: 0.001,
        LastReset: DateTime.UtcNow);
}

/// <summary>
/// Second-order conditioning - associations between conditioned stimuli.
/// Allows for complex chains of learned associations.
/// </summary>
public sealed record SecondOrderConditioning(
    string Id,
    ConditionedAssociation PrimaryAssociation,
    ConditionedAssociation SecondaryAssociation,
    double ChainStrength,        // Strength of the S1 -> S2 -> R chain
    int ChainDepth)              // How many levels of conditioning
{
    /// <summary>Creates a second-order conditioning chain.</summary>
    public static SecondOrderConditioning Create(
        ConditionedAssociation primary,
        ConditionedAssociation secondary) => new(
        Id: Guid.NewGuid().ToString(),
        PrimaryAssociation: primary,
        SecondaryAssociation: secondary,
        ChainStrength: primary.AssociationStrength * secondary.AssociationStrength,
        ChainDepth: 2);
}

/// <summary>
/// A memory trace that can undergo consolidation (becoming stronger over time/sleep).
/// </summary>
public sealed record MemoryTrace(
    string Id,
    string Content,
    double EncodingStrength,     // How well it was initially encoded
    double ConsolidationLevel,   // How consolidated (stable) the memory is
    bool IsConsolidated,         // Has undergone consolidation
    DateTime Encoded,
    DateTime? LastRetrieved,
    int RetrievalCount)
{
    /// <summary>Applies consolidation (like during sleep/rest).</summary>
    public MemoryTrace Consolidate()
    {
        double newConsolidation = Math.Min(1.0, ConsolidationLevel + 0.2);
        return this with
        {
            ConsolidationLevel = newConsolidation,
            IsConsolidated = newConsolidation > 0.7
        };
    }

    /// <summary>Records a retrieval event (strengthens memory).</summary>
    public MemoryTrace Retrieve()
    {
        double boost = 0.1 / (1 + RetrievalCount * 0.1); // Diminishing returns
        return this with
        {
            EncodingStrength = Math.Min(1.0, EncodingStrength + boost),
            LastRetrieved = DateTime.UtcNow,
            RetrievalCount = RetrievalCount + 1
        };
    }

    /// <summary>Creates a new memory trace.</summary>
    public static MemoryTrace Create(string content, double encodingStrength = 0.6) => new(
        Id: Guid.NewGuid().ToString(),
        Content: content,
        EncodingStrength: encodingStrength,
        ConsolidationLevel: 0.1,
        IsConsolidated: false,
        Encoded: DateTime.UtcNow,
        LastRetrieved: null,
        RetrievalCount: 0);
}
