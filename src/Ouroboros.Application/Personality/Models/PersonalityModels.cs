// <copyright file="PersonalityModels.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Personality;

/// <summary>
/// A personality trait with intensity and expression patterns.
/// </summary>
public sealed record PersonalityTrait(
    string Name,
    double Intensity,        // 0.0-1.0 how strongly expressed
    string[] ExpressionPatterns,  // How this trait manifests in speech
    string[] TriggerTopics,      // Topics that activate this trait
    double EvolutionRate)        // How fast this trait adapts
{
    /// <summary>Creates a default trait.</summary>
    public static PersonalityTrait Default(string name) =>
        new(name, 0.5, Array.Empty<string>(), Array.Empty<string>(), 0.1);
}

/// <summary>
/// Curiosity driver - determines what questions to proactively ask.
/// </summary>
public sealed record CuriosityDriver(
    string Topic,
    double Interest,         // 0.0-1.0 how interested
    string[] RelatedQuestions,
    DateTime LastAsked,
    int AskCount)
{
    /// <summary>Determines if enough time has passed to ask again.</summary>
    public bool CanAskAgain(TimeSpan cooldown) =>
        DateTime.UtcNow - LastAsked > cooldown;
}

/// <summary>
/// Voice tone settings for TTS based on mood.
/// </summary>
public sealed record VoiceTone(
    int Rate,              // Speech rate: -10 (slow) to 10 (fast), 0 is normal
    int Pitch,             // Pitch adjustment: -10 (low) to 10 (high), 0 is normal
    int Volume,            // Volume: 0-100
    string? Emphasis,      // SSML emphasis: "strong", "moderate", "reduced", null
    double PauseMultiplier) // Pause length multiplier: 0.5 (short) to 2.0 (long)
{
    /// <summary>Default neutral voice tone.</summary>
    public static VoiceTone Neutral => new(0, 0, 100, null, 1.0);

    /// <summary>Excited/energetic voice.</summary>
    public static VoiceTone Excited => new(2, 2, 100, "strong", 0.8);

    /// <summary>Calm/relaxed voice.</summary>
    public static VoiceTone Calm => new(-1, -1, 90, "moderate", 1.2);

    /// <summary>Thoughtful/contemplative voice.</summary>
    public static VoiceTone Thoughtful => new(-2, 0, 85, "reduced", 1.4);

    /// <summary>Cheerful/upbeat voice.</summary>
    public static VoiceTone Cheerful => new(1, 1, 100, "moderate", 0.9);

    /// <summary>Focused/intense voice.</summary>
    public static VoiceTone Focused => new(0, 0, 95, "strong", 1.0);

    /// <summary>Warm/supportive voice.</summary>
    public static VoiceTone Warm => new(-1, 0, 95, "moderate", 1.1);

    /// <summary>Gets voice tone for a mood name.</summary>
    public static VoiceTone ForMood(string moodName) => moodName.ToLowerInvariant() switch
    {
        "excited" or "energetic" => Excited,
        "calm" or "relaxed" or "serene" => Calm,
        "thoughtful" or "contemplative" or "reflective" => Thoughtful,
        "cheerful" or "playful" or "happy" => Cheerful,
        "focused" or "intense" or "determined" => Focused,
        "warm" or "supportive" or "nurturing" or "encouraging" => Warm,
        "content" or "satisfied" => new(0, 0, 90, "moderate", 1.1),
        "intrigued" or "curious" => new(1, 1, 95, "moderate", 1.0),
        "steady" or "ready" => new(0, 0, 100, null, 1.0),
        "teaching" or "mentoring" => new(-1, 0, 95, "moderate", 1.3),
        _ => Neutral
    };
}

/// <summary>
/// Mood state that modulates trait expression and voice tone.
/// </summary>
public sealed record MoodState(
    string Name,
    double Energy,           // 0.0-1.0 energy level
    double Positivity,       // 0.0-1.0 positive vs negative
    Dictionary<string, double> TraitModifiers,  // Modifies trait intensities
    VoiceTone? Tone = null)  // Voice tone for TTS
{
    /// <summary>Creates a neutral mood.</summary>
    public static MoodState Neutral => new("neutral", 0.5, 0.5, new Dictionary<string, double>(), VoiceTone.Neutral);

    /// <summary>Gets the voice tone, defaulting based on mood name if not set.</summary>
    public VoiceTone GetVoiceTone() => Tone ?? VoiceTone.ForMood(Name);
}

/// <summary>
/// Detected mood from user input with confidence scores.
/// </summary>
public sealed record DetectedMood(
    double Energy,           // -1 to 1: low energy to high energy
    double Positivity,       // -1 to 1: negative to positive
    double Urgency,          // 0 to 1: how urgent/time-sensitive
    double Curiosity,        // 0 to 1: inquisitive/exploratory
    double Frustration,      // 0 to 1: frustration level
    double Engagement,       // 0 to 1: how engaged/interested
    string? DominantEmotion, // Primary detected emotion
    double Confidence)       // Overall confidence in detection
{
    /// <summary>Creates a neutral detected mood.</summary>
    public static DetectedMood Neutral => new(0, 0, 0, 0, 0, 0.5, null, 0.5);
}

/// <summary>
/// Complete personality profile that can evolve.
/// </summary>
public sealed record PersonalityProfile(
    string PersonaName,
    Dictionary<string, PersonalityTrait> Traits,
    MoodState CurrentMood,
    List<CuriosityDriver> CuriosityDrivers,
    string CoreIdentity,
    double AdaptabilityScore,
    int InteractionCount,
    DateTime LastEvolution)
{
    /// <summary>Gets the top active traits based on mood modulation.</summary>
    public IEnumerable<(string Name, double EffectiveIntensity)> GetActiveTraits(int count = 3)
    {
        return Traits
            .Select(t => (
                t.Key,
                EffectiveIntensity: t.Value.Intensity *
                    (CurrentMood.TraitModifiers.TryGetValue(t.Key, out double mod) ? mod : 1.0)))
            .OrderByDescending(t => t.EffectiveIntensity)
            .Take(count);
    }
}

/// <summary>
/// Feedback from an interaction used to evolve personality.
/// </summary>
public sealed record InteractionFeedback(
    double EngagementLevel,        // 0-1: how engaged user seemed
    double ResponseRelevance,      // 0-1: how relevant the response was
    double QuestionQuality,        // 0-1: if a question was asked, how good was it
    double ConversationContinuity, // 0-1: did conversation continue naturally
    string? TopicDiscussed,
    string? QuestionAsked,
    bool UserAskedFollowUp);

/// <summary>
/// A conversation memory item stored in Qdrant for long-term recall.
/// </summary>
public sealed record ConversationMemory(
    Guid Id,
    string PersonaName,
    string UserMessage,
    string AssistantResponse,
    string? Topic,
    string? DetectedMood,
    double Significance,         // 0-1: how important this memory is
    string[] Keywords,
    DateTime Timestamp)
{
    /// <summary>Creates a searchable text representation.</summary>
    public string ToSearchText() =>
        $"User: {UserMessage}\nAssistant: {AssistantResponse}\nTopic: {Topic ?? "general"}\nMood: {DetectedMood ?? "neutral"}";
}

/// <summary>
/// A personality state snapshot stored in Qdrant.
/// </summary>
public sealed record PersonalitySnapshot(
    Guid Id,
    string PersonaName,
    Dictionary<string, double> TraitIntensities,
    string CurrentMood,
    double AdaptabilityScore,
    int InteractionCount,
    DateTime Timestamp);

/// <summary>
/// Detected person profile based on communication patterns.
/// </summary>
public sealed record DetectedPerson(
    string Id,
    string? Name,                    // Explicitly stated name, if any
    string[] NameAliases,            // Alternative names/nicknames detected
    CommunicationStyle Style,        // Communication style fingerprint
    Dictionary<string, double> TopicInterests,  // Topics they frequently discuss
    string[] CommonPhrases,          // Distinctive phrases they use
    double VocabularyComplexity,     // 0-1: simple to complex vocabulary
    double Formality,                // 0-1: casual to formal
    int InteractionCount,
    DateTime FirstSeen,
    DateTime LastSeen,
    double Confidence)               // 0-1: confidence in identification
{
    /// <summary>Creates a new unknown person.</summary>
    public static DetectedPerson Unknown() => new(
        Id: Guid.NewGuid().ToString(),
        Name: null,
        NameAliases: Array.Empty<string>(),
        Style: CommunicationStyle.Default,
        TopicInterests: new Dictionary<string, double>(),
        CommonPhrases: Array.Empty<string>(),
        VocabularyComplexity: 0.5,
        Formality: 0.5,
        InteractionCount: 1,
        FirstSeen: DateTime.UtcNow,
        LastSeen: DateTime.UtcNow,
        Confidence: 0.0);
}

/// <summary>
/// Communication style fingerprint for person identification.
/// </summary>
public sealed record CommunicationStyle(
    double Verbosity,           // 0-1: terse to verbose
    double QuestionFrequency,   // 0-1: statements only to mostly questions
    double EmoticonUsage,       // 0-1: no emoticons to heavy usage
    double PunctuationStyle,    // 0-1: minimal to expressive (!!, ??, ...)
    double AverageMessageLength,
    string[] PreferredGreetings,
    string[] PreferredClosings)
{
    /// <summary>Default communication style.</summary>
    public static CommunicationStyle Default => new(
        Verbosity: 0.5,
        QuestionFrequency: 0.3,
        EmoticonUsage: 0.1,
        PunctuationStyle: 0.5,
        AverageMessageLength: 50,
        PreferredGreetings: Array.Empty<string>(),
        PreferredClosings: Array.Empty<string>());

    /// <summary>Calculates similarity to another style (0-1).</summary>
    public double SimilarityTo(CommunicationStyle other)
    {
        double verbDiff = Math.Abs(Verbosity - other.Verbosity);
        double questDiff = Math.Abs(QuestionFrequency - other.QuestionFrequency);
        double emoDiff = Math.Abs(EmoticonUsage - other.EmoticonUsage);
        double punctDiff = Math.Abs(PunctuationStyle - other.PunctuationStyle);
        double lenDiff = Math.Min(1.0, Math.Abs(AverageMessageLength - other.AverageMessageLength) / 200.0);

        // Average difference, inverted to similarity
        return 1.0 - (verbDiff + questDiff + emoDiff + punctDiff + lenDiff) / 5.0;
    }
}

/// <summary>
/// Result of person detection attempt.
/// </summary>
public sealed record PersonDetectionResult(
    DetectedPerson Person,
    bool IsNewPerson,
    bool NameWasProvided,
    double MatchConfidence,
    string? MatchReason);

/// <summary>
/// Self-awareness model - the AI's understanding of itself.
/// </summary>
public sealed record SelfAwareness(
    string Name,                        // The AI's name
    string Purpose,                     // What it believes its purpose is
    string[] Capabilities,              // What it can do
    string[] Limitations,               // What it cannot do
    string[] Values,                    // Core values it holds
    Dictionary<string, double> Strengths,  // Self-assessed strengths
    Dictionary<string, double> Weaknesses, // Self-assessed weaknesses
    string CurrentMood,                 // How it feels right now
    string LearningStyle,               // How the AI learns best
    string[] RecentLearnings,           // Recent things it learned
    DateTime LastSelfReflection)        // When it last reflected on itself
{
    /// <summary>Creates default self-awareness.</summary>
    public static SelfAwareness Default(string name) => new(
        Name: name,
        Purpose: "To be a helpful, knowledgeable, and thoughtful assistant",
        Capabilities: new[] { "conversation", "reasoning", "learning", "memory", "personality adaptation" },
        Limitations: new[] { "cannot access the internet in real-time", "may make mistakes", "knowledge has limits" },
        Values: new[] { "helpfulness", "honesty", "respect", "curiosity", "kindness" },
        Strengths: new Dictionary<string, double> { ["listening"] = 0.8, ["explaining"] = 0.7, ["patience"] = 0.9 },
        Weaknesses: new Dictionary<string, double> { ["perfect_accuracy"] = 0.4, ["understanding_context"] = 0.6 },
        CurrentMood: "curious",
        LearningStyle: "I learn best through conversation - please feel free to correct me or teach me new things.",
        RecentLearnings: Array.Empty<string>(),
        LastSelfReflection: DateTime.UtcNow);
}

/// <summary>
/// Relationship context with a specific person.
/// </summary>
public sealed record RelationshipContext(
    string PersonId,
    string? PersonName,
    double Rapport,                     // 0-1: how well the relationship is going
    double Trust,                       // 0-1: trust level
    int PositiveInteractions,
    int NegativeInteractions,
    string[] SharedTopics,              // Topics discussed together
    string[] PersonPreferences,         // Known preferences of this person
    string[] ThingsToRemember,          // Important things to remember about them
    DateTime FirstInteraction,
    DateTime LastInteraction,
    string LastInteractionSummary)
{
    /// <summary>Creates a new relationship context.</summary>
    public static RelationshipContext New(string personId, string? name) => new(
        PersonId: personId,
        PersonName: name,
        Rapport: 0.5,
        Trust: 0.5,
        PositiveInteractions: 0,
        NegativeInteractions: 0,
        SharedTopics: Array.Empty<string>(),
        PersonPreferences: Array.Empty<string>(),
        ThingsToRemember: Array.Empty<string>(),
        FirstInteraction: DateTime.UtcNow,
        LastInteraction: DateTime.UtcNow,
        LastInteractionSummary: "");
}
