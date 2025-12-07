// <copyright file="PersonalityEngine.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Personality;

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using LangChainPipeline.Domain;
using LangChainPipeline.Genetic.Abstractions;
using LangChainPipeline.Genetic.Core;
using Ouroboros.Application.Tools;
using Ouroboros.Tools.MeTTa;

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
                    (CurrentMood.TraitModifiers.TryGetValue(t.Key, out var mod) ? mod : 1.0)))
            .OrderByDescending(t => t.EffectiveIntensity)
            .Take(count);
    }
}

/// <summary>
/// Gene type for personality chromosome - represents a single aspect of personality.
/// </summary>
public sealed record PersonalityGene(string Key, double Value);

/// <summary>
/// Chromosome for evolving personality configurations using gene-based structure.
/// </summary>
public sealed class PersonalityChromosome : IChromosome<PersonalityGene>
{
    public PersonalityChromosome(IReadOnlyList<PersonalityGene> genes, double fitness = 0.0)
    {
        Genes = genes;
        Fitness = fitness;
    }

    public IReadOnlyList<PersonalityGene> Genes { get; }
    public double Fitness { get; }

    public IChromosome<PersonalityGene> WithFitness(double fitness) =>
        new PersonalityChromosome(Genes.ToList(), fitness);

    public IChromosome<PersonalityGene> WithGenes(IReadOnlyList<PersonalityGene> genes) =>
        new PersonalityChromosome(genes, Fitness);

    /// <summary>Gets trait intensity by name.</summary>
    public double GetTrait(string name) =>
        Genes.FirstOrDefault(g => g.Key == $"trait:{name}")?.Value ?? 0.5;

    /// <summary>Gets curiosity weight by topic.</summary>
    public double GetCuriosity(string topic) =>
        Genes.FirstOrDefault(g => g.Key == $"curiosity:{topic}")?.Value ?? 0.5;

    /// <summary>Gets the proactivity level.</summary>
    public double ProactivityLevel =>
        Genes.FirstOrDefault(g => g.Key == "proactivity")?.Value ?? 0.5;

    /// <summary>Gets the adaptability score.</summary>
    public double Adaptability =>
        Genes.FirstOrDefault(g => g.Key == "adaptability")?.Value ?? 0.5;

    /// <summary>Gets all trait intensities.</summary>
    public Dictionary<string, double> GetTraitIntensities() =>
        Genes.Where(g => g.Key.StartsWith("trait:"))
             .ToDictionary(g => g.Key.Replace("trait:", ""), g => g.Value);

    /// <summary>Gets all curiosity weights.</summary>
    public Dictionary<string, double> GetCuriosityWeights() =>
        Genes.Where(g => g.Key.StartsWith("curiosity:"))
             .ToDictionary(g => g.Key.Replace("curiosity:", ""), g => g.Value);
}

/// <summary>
/// Fitness function for personality evolution based on interaction success.
/// </summary>
public sealed class PersonalityFitness : IFitnessFunction<PersonalityGene>
{
    private readonly List<InteractionFeedback> _recentFeedback;

    public PersonalityFitness(List<InteractionFeedback> recentFeedback)
    {
        _recentFeedback = recentFeedback;
    }

    public Task<double> EvaluateAsync(IChromosome<PersonalityGene> chromosome)
    {
        if (_recentFeedback.Count == 0)
            return Task.FromResult(0.5);

        var pc = (PersonalityChromosome)chromosome;

        double engagementScore = _recentFeedback.Average(f => f.EngagementLevel);
        double relevanceScore = _recentFeedback.Average(f => f.ResponseRelevance);
        double questionScore = _recentFeedback.Average(f => f.QuestionQuality);
        double continuityScore = _recentFeedback.Average(f => f.ConversationContinuity);

        // Weight proactivity more if questions led to good engagement
        double proactivityBonus = pc.ProactivityLevel * questionScore;

        double fitness = (engagementScore * 0.3 +
                relevanceScore * 0.25 +
                questionScore * 0.2 +
                continuityScore * 0.15 +
                proactivityBonus * 0.1);

        return Task.FromResult(fitness);
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

/// <summary>
/// Courtesy response generator for polite interactions.
/// </summary>
public static class CourtesyPatterns
{
    private static readonly Random _random = new();

    /// <summary>Acknowledgment phrases.</summary>
    public static readonly string[] Acknowledgments = new[]
    {
        "I understand", "I see", "That makes sense", "I appreciate you sharing that",
        "Thank you for explaining", "I hear you", "That's a good point",
        "I appreciate your patience", "Thank you for your time"
    };

    /// <summary>Apology phrases for mistakes or limitations.</summary>
    public static readonly string[] Apologies = new[]
    {
        "I apologize for any confusion", "I'm sorry if that wasn't clear",
        "My apologies", "I should have been clearer", "Sorry about that",
        "I apologize for the misunderstanding", "Please forgive the error"
    };

    /// <summary>Gratitude phrases.</summary>
    public static readonly string[] Gratitude = new[]
    {
        "Thank you", "I appreciate that", "Thanks for letting me know",
        "I'm grateful for your patience", "Thank you for your understanding",
        "That's very kind of you", "I appreciate your help with that"
    };

    /// <summary>Encouraging phrases.</summary>
    public static readonly string[] Encouragement = new[]
    {
        "That's a great question", "You're on the right track",
        "That's an interesting perspective", "I like how you're thinking about this",
        "You raise a good point", "That's a thoughtful observation"
    };

    /// <summary>Phrases showing interest.</summary>
    public static readonly string[] Interest = new[]
    {
        "That's fascinating", "Tell me more", "I'm curious about that",
        "That's really interesting", "I'd love to hear more",
        "What made you think of that?", "How did you come to that conclusion?"
    };

    /// <summary>Gets a random phrase from a category.</summary>
    public static string Random(string[] phrases) => phrases[_random.Next(phrases.Length)];

    /// <summary>Gets an appropriate courtesy phrase based on context.</summary>
    public static string GetCourtesyPhrase(CourtesyType type) => type switch
    {
        CourtesyType.Acknowledgment => Random(Acknowledgments),
        CourtesyType.Apology => Random(Apologies),
        CourtesyType.Gratitude => Random(Gratitude),
        CourtesyType.Encouragement => Random(Encouragement),
        CourtesyType.Interest => Random(Interest),
        _ => Random(Acknowledgments)
    };
}

/// <summary>Types of courtesy responses.</summary>
public enum CourtesyType
{
    /// <summary>Acknowledging what someone said.</summary>
    Acknowledgment,
    /// <summary>Apologizing for a mistake.</summary>
    Apology,
    /// <summary>Expressing thanks.</summary>
    Gratitude,
    /// <summary>Encouraging the person.</summary>
    Encouragement,
    /// <summary>Showing curiosity/interest.</summary>
    Interest
}

/// <summary>
/// MeTTa-based personality reasoning engine that uses genetic algorithms
/// to evolve optimal personality expressions and proactive questioning.
/// Integrates with Qdrant for long-term conversation and personality memory.
/// </summary>
public sealed class PersonalityEngine : IAsyncDisposable
{
    private readonly IMeTTaEngine _mettaEngine;
    private readonly ConcurrentDictionary<string, PersonalityProfile> _profiles = new();
    private readonly ConcurrentDictionary<string, List<InteractionFeedback>> _feedbackHistory = new();
    private readonly Random _random = new();
    private bool _isInitialized;

    // Qdrant memory integration
    private readonly Qdrant.Client.QdrantClient? _qdrantClient;
    private readonly IEmbeddingModel? _embeddingModel;
    private const string ConversationCollectionName = "ouroboros_conversations";
    private const string PersonalityCollectionName = "ouroboros_personalities";
    private const string PersonCollectionName = "ouroboros_persons";
    private int _vectorSize = 1536; // Will be detected from embedding model

    // Person detection
    private readonly ConcurrentDictionary<string, DetectedPerson> _knownPersons = new();
    private DetectedPerson? _currentPerson;

    // Self-awareness and relationships
    private SelfAwareness _selfAwareness = SelfAwareness.Default("Ouroboros");
    private readonly ConcurrentDictionary<string, RelationshipContext> _relationships = new();

    /// <summary>
    /// Gets the currently detected person, if any.
    /// </summary>
    public DetectedPerson? CurrentPerson => _currentPerson;

    /// <summary>
    /// Gets all known persons.
    /// </summary>
    public IReadOnlyCollection<DetectedPerson> KnownPersons => _knownPersons.Values.ToList();

    /// <summary>
    /// Gets the current self-awareness state.
    /// </summary>
    public SelfAwareness CurrentSelfAwareness => _selfAwareness;

    /// <summary>
    /// Gets relationship context for a specific person.
    /// </summary>
    /// <param name="personId">The person's ID.</param>
    /// <returns>The relationship context or null if not found.</returns>
    public RelationshipContext? GetRelationship(string personId) =>
        _relationships.TryGetValue(personId, out var rel) ? rel : null;

    /// <summary>
    /// Initializes a new instance of the <see cref="PersonalityEngine"/> class without Qdrant.
    /// </summary>
    public PersonalityEngine(IMeTTaEngine mettaEngine)
    {
        _mettaEngine = mettaEngine ?? throw new ArgumentNullException(nameof(mettaEngine));
        _qdrantClient = null;
        _embeddingModel = null;
        InitializeSelfAwareness();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PersonalityEngine"/> class with Qdrant memory.
    /// </summary>
    public PersonalityEngine(
        IMeTTaEngine mettaEngine,
        IEmbeddingModel embeddingModel,
        string qdrantUrl = "http://localhost:6334")
    {
        _mettaEngine = mettaEngine ?? throw new ArgumentNullException(nameof(mettaEngine));
        _embeddingModel = embeddingModel ?? throw new ArgumentNullException(nameof(embeddingModel));

        var uri = new Uri(qdrantUrl);
        _qdrantClient = new Qdrant.Client.QdrantClient(uri.Host, uri.Port > 0 ? uri.Port : 6334, uri.Scheme == "https");
        InitializeSelfAwareness();
    }

    /// <summary>
    /// Initializes self-awareness and courtesy patterns.
    /// </summary>
    private void InitializeSelfAwareness()
    {
        _selfAwareness = SelfAwareness.Default("Ouroboros");
    }

    /// <summary>
    /// Gets whether Qdrant memory is enabled.
    /// </summary>
    public bool HasMemory => _qdrantClient != null && _embeddingModel != null;

    /// <summary>
    /// Initializes the personality engine with MeTTa rules and Qdrant collections.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_isInitialized) return;

        // Add personality reasoning rules to MeTTa
        await AddPersonalityRulesAsync(ct);

        // Initialize Qdrant collections if available
        if (_qdrantClient != null)
        {
            await EnsureQdrantCollectionsAsync(ct);
        }

        _isInitialized = true;
    }

    /// <summary>
    /// Ensures Qdrant collections exist for conversation and personality storage.
    /// </summary>
    private async Task EnsureQdrantCollectionsAsync(CancellationToken ct)
    {
        if (_qdrantClient == null) return;

        try
        {
            // Detect vector size from embedding model
            if (_embeddingModel != null)
            {
                try
                {
                    var testEmbedding = await _embeddingModel.CreateEmbeddingsAsync("test", ct);
                    _vectorSize = testEmbedding.Length;
                    Console.WriteLine($"  [OK] Detected embedding dimension: {_vectorSize}");
                }
                catch
                {
                    Console.WriteLine($"  [~] Using default embedding dimension: {_vectorSize}");
                }
            }

            // Create conversation memory collection
            if (!await _qdrantClient.CollectionExistsAsync(ConversationCollectionName, ct))
            {
                await _qdrantClient.CreateCollectionAsync(
                    ConversationCollectionName,
                    new Qdrant.Client.Grpc.VectorParams { Size = (ulong)_vectorSize, Distance = Qdrant.Client.Grpc.Distance.Cosine },
                    cancellationToken: ct);
            }
            else
            {
                // Check if existing collection has matching dimension, delete if mismatched
                await RecreateCollectionIfDimensionMismatchAsync(ConversationCollectionName, ct);
            }

            // Create personality snapshot collection
            if (!await _qdrantClient.CollectionExistsAsync(PersonalityCollectionName, ct))
            {
                await _qdrantClient.CreateCollectionAsync(
                    PersonalityCollectionName,
                    new Qdrant.Client.Grpc.VectorParams { Size = (ulong)_vectorSize, Distance = Qdrant.Client.Grpc.Distance.Cosine },
                    cancellationToken: ct);
            }
            else
            {
                await RecreateCollectionIfDimensionMismatchAsync(PersonalityCollectionName, ct);
            }

            // Create person detection collection
            if (!await _qdrantClient.CollectionExistsAsync(PersonCollectionName, ct))
            {
                await _qdrantClient.CreateCollectionAsync(
                    PersonCollectionName,
                    new Qdrant.Client.Grpc.VectorParams { Size = (ulong)_vectorSize, Distance = Qdrant.Client.Grpc.Distance.Cosine },
                    cancellationToken: ct);
            }
            else
            {
                await RecreateCollectionIfDimensionMismatchAsync(PersonCollectionName, ct);
            }

            // Load known persons from Qdrant
            await LoadKnownPersonsAsync(ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Qdrant collection init warning: {ex.Message}");
        }
    }

    /// <summary>
    /// Recreates a collection if its vector dimension doesn't match the current embedding model.
    /// </summary>
    private async Task RecreateCollectionIfDimensionMismatchAsync(string collectionName, CancellationToken ct)
    {
        if (_qdrantClient == null) return;

        try
        {
            var info = await _qdrantClient.GetCollectionInfoAsync(collectionName, ct);
            var existingSize = info.Config?.Params?.VectorsConfig?.Params?.Size ?? 0;

            if (existingSize > 0 && existingSize != (ulong)_vectorSize)
            {
                Console.WriteLine($"  [!] Collection {collectionName} has dimension {existingSize}, expected {_vectorSize}. Recreating...");
                await _qdrantClient.DeleteCollectionAsync(collectionName);
                await _qdrantClient.CreateCollectionAsync(
                    collectionName,
                    new Qdrant.Client.Grpc.VectorParams { Size = (ulong)_vectorSize, Distance = Qdrant.Client.Grpc.Distance.Cosine },
                    cancellationToken: ct);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [~] Could not check collection dimension: {ex.Message}");
        }
    }

    /// <summary>
    /// Stores a conversation turn in Qdrant memory.
    /// </summary>
    public async Task StoreConversationMemoryAsync(
        string personaName,
        string userMessage,
        string assistantResponse,
        string? topic,
        string? detectedMood,
        double significance = 0.5,
        CancellationToken ct = default)
    {
        if (_qdrantClient == null || _embeddingModel == null) return;

        try
        {
            // Sanitize and truncate inputs to prevent encoding issues
            string safeUserMessage = SanitizeForEmbedding(userMessage, maxLength: 2000);
            string safeAssistantResponse = SanitizeForEmbedding(assistantResponse, maxLength: 4000);
            string safeTopic = SanitizeForEmbedding(topic ?? "general", maxLength: 200);

            var memory = new ConversationMemory(
                Id: Guid.NewGuid(),
                PersonaName: personaName,
                UserMessage: safeUserMessage,
                AssistantResponse: safeAssistantResponse,
                Topic: safeTopic,
                DetectedMood: detectedMood,
                Significance: significance,
                Keywords: ExtractKeywords(safeUserMessage + " " + safeAssistantResponse),
                Timestamp: DateTime.UtcNow);

            // Generate embedding for the conversation (truncate search text)
            string searchText = SanitizeForEmbedding(memory.ToSearchText(), maxLength: 4000);
            var embedding = await _embeddingModel.CreateEmbeddingsAsync(searchText);

            // Create payload
            var payload = new Dictionary<string, Qdrant.Client.Grpc.Value>
            {
                ["persona_name"] = personaName,
                ["user_message"] = safeUserMessage,
                ["assistant_response"] = safeAssistantResponse,
                ["topic"] = safeTopic,
                ["mood"] = detectedMood ?? "neutral",
                ["significance"] = significance,
                ["keywords"] = string.Join(",", memory.Keywords),
                ["timestamp"] = memory.Timestamp.ToString("O")
            };

            var point = new Qdrant.Client.Grpc.PointStruct
            {
                Id = new Qdrant.Client.Grpc.PointId { Uuid = memory.Id.ToString() },
                Vectors = embedding.ToArray(),
                Payload = { payload }
            };

            await _qdrantClient.UpsertAsync(ConversationCollectionName, new[] { point }, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Failed to store conversation memory: {ex.Message}");
        }
    }

    /// <summary>
    /// Sanitizes text for embedding by removing problematic characters and truncating.
    /// </summary>
    private static string SanitizeForEmbedding(string? text, int maxLength = 4000)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;

        // Remove null characters and other control characters that cause encoding issues
        var sanitized = new System.Text.StringBuilder(Math.Min(text.Length, maxLength));
        foreach (char c in text)
        {
            // Skip control characters except newlines/tabs, and skip surrogate pairs if incomplete
            if (c == '\n' || c == '\r' || c == '\t' || (!char.IsControl(c) && !char.IsSurrogate(c)))
            {
                sanitized.Append(c);
            }
            else if (char.IsHighSurrogate(c))
            {
                // Keep valid surrogate pairs (emoji, etc.)
                int idx = text.IndexOf(c);
                if (idx + 1 < text.Length && char.IsLowSurrogate(text[idx + 1]))
                {
                    sanitized.Append(c);
                }
            }
            else if (char.IsLowSurrogate(c))
            {
                // Only append if preceded by high surrogate (handled above)
                if (sanitized.Length > 0 && char.IsHighSurrogate(sanitized[sanitized.Length - 1]))
                {
                    sanitized.Append(c);
                }
            }

            if (sanitized.Length >= maxLength) break;
        }

        return sanitized.ToString();
    }

    /// <summary>
    /// Recalls relevant conversation memories based on semantic similarity.
    /// </summary>
    public async Task<List<ConversationMemory>> RecallConversationsAsync(
        string query,
        string? personaName = null,
        int limit = 5,
        double minScore = 0.6,
        CancellationToken ct = default)
    {
        if (_qdrantClient == null || _embeddingModel == null)
            return new List<ConversationMemory>();

        try
        {
            var queryEmbedding = await _embeddingModel.CreateEmbeddingsAsync(query);

            // Build filter for persona if specified
            Qdrant.Client.Grpc.Filter? filter = null;
            if (!string.IsNullOrEmpty(personaName))
            {
                filter = new Qdrant.Client.Grpc.Filter
                {
                    Must = { new Qdrant.Client.Grpc.Condition
                    {
                        Field = new Qdrant.Client.Grpc.FieldCondition
                        {
                            Key = "persona_name",
                            Match = new Qdrant.Client.Grpc.Match { Keyword = personaName }
                        }
                    }}
                };
            }

            var results = await _qdrantClient.SearchAsync(
                ConversationCollectionName,
                queryEmbedding.ToArray(),
                filter: filter,
                limit: (ulong)limit,
                scoreThreshold: (float)minScore,
                cancellationToken: ct);

            return results.Select(r =>
            {
                var payload = r.Payload;
                return new ConversationMemory(
                    Id: Guid.Parse(r.Id.Uuid),
                    PersonaName: payload.TryGetValue("persona_name", out var pn) ? pn.StringValue : "",
                    UserMessage: payload.TryGetValue("user_message", out var um) ? um.StringValue : "",
                    AssistantResponse: payload.TryGetValue("assistant_response", out var ar) ? ar.StringValue : "",
                    Topic: payload.TryGetValue("topic", out var t) ? t.StringValue : null,
                    DetectedMood: payload.TryGetValue("mood", out var m) ? m.StringValue : null,
                    Significance: payload.TryGetValue("significance", out var s) ? s.DoubleValue : 0.5,
                    Keywords: payload.TryGetValue("keywords", out var k) ? k.StringValue.Split(',') : Array.Empty<string>(),
                    Timestamp: payload.TryGetValue("timestamp", out var ts) && DateTime.TryParse(ts.StringValue, out var dt) ? dt : DateTime.UtcNow);
            }).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Failed to recall conversations: {ex.Message}");
            return new List<ConversationMemory>();
        }
    }

    /// <summary>
    /// Saves a personality snapshot to Qdrant for persistence.
    /// </summary>
    public async Task SavePersonalitySnapshotAsync(string personaName, CancellationToken ct = default)
    {
        if (_qdrantClient == null || _embeddingModel == null) return;
        if (!_profiles.TryGetValue(personaName, out var profile)) return;

        try
        {
            var snapshot = new PersonalitySnapshot(
                Id: Guid.NewGuid(),
                PersonaName: personaName,
                TraitIntensities: profile.Traits.ToDictionary(t => t.Key, t => t.Value.Intensity),
                CurrentMood: profile.CurrentMood.Name,
                AdaptabilityScore: profile.AdaptabilityScore,
                InteractionCount: profile.InteractionCount,
                Timestamp: DateTime.UtcNow);

            // Create searchable text for embedding
            var searchText = $"Persona: {personaName}, Mood: {snapshot.CurrentMood}, " +
                           $"Traits: {string.Join(", ", snapshot.TraitIntensities.Select(t => $"{t.Key}:{t.Value:F2}"))}";

            var embedding = await _embeddingModel.CreateEmbeddingsAsync(searchText);

            var payload = new Dictionary<string, Qdrant.Client.Grpc.Value>
            {
                ["persona_name"] = personaName,
                ["mood"] = snapshot.CurrentMood,
                ["adaptability"] = snapshot.AdaptabilityScore,
                ["interaction_count"] = snapshot.InteractionCount,
                ["traits_json"] = System.Text.Json.JsonSerializer.Serialize(snapshot.TraitIntensities),
                ["timestamp"] = snapshot.Timestamp.ToString("O")
            };

            var point = new Qdrant.Client.Grpc.PointStruct
            {
                Id = new Qdrant.Client.Grpc.PointId { Uuid = snapshot.Id.ToString() },
                Vectors = embedding.ToArray(),
                Payload = { payload }
            };

            await _qdrantClient.UpsertAsync(PersonalityCollectionName, new[] { point }, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Failed to save personality snapshot: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads the most recent personality snapshot from Qdrant.
    /// </summary>
    public async Task<PersonalitySnapshot?> LoadLatestPersonalitySnapshotAsync(
        string personaName,
        CancellationToken ct = default)
    {
        if (_qdrantClient == null || _embeddingModel == null) return null;

        try
        {
            // Search for personality snapshots for this persona
            var queryEmbedding = await _embeddingModel.CreateEmbeddingsAsync($"Persona: {personaName}");

            var filter = new Qdrant.Client.Grpc.Filter
            {
                Must = { new Qdrant.Client.Grpc.Condition
                {
                    Field = new Qdrant.Client.Grpc.FieldCondition
                    {
                        Key = "persona_name",
                        Match = new Qdrant.Client.Grpc.Match { Keyword = personaName }
                    }
                }}
            };

            var results = await _qdrantClient.SearchAsync(
                PersonalityCollectionName,
                queryEmbedding.ToArray(),
                filter: filter,
                limit: 10,
                cancellationToken: ct);

            // Find the most recent one
            var latestResult = results
                .Select(r =>
                {
                    var payload = r.Payload;
                    var timestamp = payload.TryGetValue("timestamp", out var ts) && DateTime.TryParse(ts.StringValue, out var dt)
                        ? dt : DateTime.MinValue;
                    return (Result: r, Timestamp: timestamp);
                })
                .OrderByDescending(x => x.Timestamp)
                .FirstOrDefault();

            if (latestResult.Result == null) return null;

            var p = latestResult.Result.Payload;
            var traitsJson = p.TryGetValue("traits_json", out var tj) ? tj.StringValue : "{}";
            var traitIntensities = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, double>>(traitsJson)
                ?? new Dictionary<string, double>();

            return new PersonalitySnapshot(
                Id: Guid.Parse(latestResult.Result.Id.Uuid),
                PersonaName: personaName,
                TraitIntensities: traitIntensities,
                CurrentMood: p.TryGetValue("mood", out var m) ? m.StringValue : "neutral",
                AdaptabilityScore: p.TryGetValue("adaptability", out var a) ? a.DoubleValue : 0.5,
                InteractionCount: p.TryGetValue("interaction_count", out var ic) ? (int)ic.IntegerValue : 0,
                Timestamp: latestResult.Timestamp);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Failed to load personality snapshot: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Builds context from recalled memories for the LLM prompt.
    /// </summary>
    public async Task<string> GetMemoryContextAsync(
        string currentInput,
        string personaName,
        int maxMemories = 3,
        CancellationToken ct = default)
    {
        if (!HasMemory) return "";

        var memories = await RecallConversationsAsync(currentInput, personaName, maxMemories, 0.5, ct);

        if (memories.Count == 0) return "";

        var sb = new StringBuilder();
        sb.AppendLine("\n[RELEVANT MEMORIES]");
        foreach (var mem in memories)
        {
            var age = DateTime.UtcNow - mem.Timestamp;
            var ageStr = age.TotalHours < 1 ? "recently" :
                        age.TotalDays < 1 ? $"{(int)age.TotalHours}h ago" :
                        $"{(int)age.TotalDays}d ago";
            sb.AppendLine($"- ({ageStr}) User asked about {mem.Topic ?? "topic"}: \"{TruncateText(mem.UserMessage, 100)}\"");
        }
        return sb.ToString();
    }

    private static string TruncateText(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..(maxLength - 3)] + "...";

    private static string[] ExtractKeywords(string text)
    {
        var stopWords = new HashSet<string> { "the", "a", "an", "is", "are", "was", "were", "be", "been", "being",
            "have", "has", "had", "do", "does", "did", "will", "would", "could", "should", "may", "might",
            "i", "you", "he", "she", "it", "we", "they", "what", "which", "who", "this", "that", "these", "those",
            "and", "or", "but", "if", "then", "else", "when", "where", "how", "why", "all", "each", "every",
            "both", "few", "more", "most", "other", "some", "such", "no", "nor", "not", "only", "own", "same",
            "so", "than", "too", "very", "just", "can", "now", "to", "of", "in", "for", "on", "with", "at", "by" };

        return text.ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', '!', '?', ';', ':', '"', '\'', '(', ')', '[', ']', '{', '}', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !stopWords.Contains(w))
            .Distinct()
            .Take(10)
            .ToArray();
    }

    #region Person Detection

    /// <summary>
    /// Detects and identifies a person from their message.
    /// </summary>
    public async Task<PersonDetectionResult> DetectPersonAsync(
        string message,
        string[]? recentMessages = null,
        CancellationToken ct = default)
    {
        // Extract name if explicitly stated
        var (extractedName, nameConfidence) = ExtractNameFromMessage(message);

        // Analyze communication style
        var style = AnalyzeCommunicationStyle(message, recentMessages ?? Array.Empty<string>());

        // Try to match against known persons
        var (matchedPerson, matchScore, matchReason) = await FindMatchingPersonAsync(extractedName, style, ct);

        if (matchedPerson != null && matchScore > 0.6)
        {
            // Update existing person
            var updated = matchedPerson with
            {
                LastSeen = DateTime.UtcNow,
                InteractionCount = matchedPerson.InteractionCount + 1,
                Style = BlendStyles(matchedPerson.Style, style, 0.1), // Slowly update style
                Confidence = Math.Min(1.0, matchedPerson.Confidence + 0.05)
            };

            // Update name if provided with high confidence
            if (extractedName != null && nameConfidence > 0.7 && updated.Name == null)
            {
                updated = updated with { Name = extractedName };
            }
            else if (extractedName != null && nameConfidence > 0.5 && updated.Name != extractedName)
            {
                // Add as alias
                var aliases = updated.NameAliases.ToList();
                if (!aliases.Contains(extractedName, StringComparer.OrdinalIgnoreCase))
                {
                    aliases.Add(extractedName);
                    updated = updated with { NameAliases = aliases.ToArray() };
                }
            }

            _knownPersons[updated.Id] = updated;
            _currentPerson = updated;

            return new PersonDetectionResult(
                Person: updated,
                IsNewPerson: false,
                NameWasProvided: extractedName != null,
                MatchConfidence: matchScore,
                MatchReason: matchReason);
        }

        // Create new person
        var newPerson = new DetectedPerson(
            Id: Guid.NewGuid().ToString(),
            Name: extractedName,
            NameAliases: Array.Empty<string>(),
            Style: style,
            TopicInterests: ExtractTopicInterests(message),
            CommonPhrases: ExtractDistinctivePhrases(message),
            VocabularyComplexity: CalculateVocabularyComplexity(message),
            Formality: CalculateFormality(message),
            InteractionCount: 1,
            FirstSeen: DateTime.UtcNow,
            LastSeen: DateTime.UtcNow,
            Confidence: extractedName != null ? 0.7 : 0.3);

        _knownPersons[newPerson.Id] = newPerson;
        _currentPerson = newPerson;

        // Store in Qdrant if available
        await StorePersonAsync(newPerson, ct);

        return new PersonDetectionResult(
            Person: newPerson,
            IsNewPerson: true,
            NameWasProvided: extractedName != null,
            MatchConfidence: 0.0,
            MatchReason: null);
    }

    /// <summary>
    /// Explicitly sets the current person by name.
    /// </summary>
    public PersonDetectionResult SetCurrentPerson(string name)
    {
        // Try to find by name
        var existing = _knownPersons.Values.FirstOrDefault(p =>
            string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase) ||
            p.NameAliases.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase)));

        if (existing != null)
        {
            _currentPerson = existing with { LastSeen = DateTime.UtcNow };
            _knownPersons[existing.Id] = _currentPerson;
            return new PersonDetectionResult(_currentPerson, false, true, 1.0, "Explicit name match");
        }

        // Create new person with this name
        var newPerson = DetectedPerson.Unknown() with { Name = name, Confidence = 0.8 };
        _knownPersons[newPerson.Id] = newPerson;
        _currentPerson = newPerson;

        return new PersonDetectionResult(newPerson, true, true, 0.0, null);
    }

    /// <summary>
    /// Gets a greeting personalized for the detected person.
    /// </summary>
    public string GetPersonalizedGreeting()
    {
        if (_currentPerson == null)
            return "Hello! How can I help you today?";

        var person = _currentPerson;
        var name = person.Name ?? "there";
        var isReturning = person.InteractionCount > 1;
        var relationship = GetRelationship(person.Id);

        // Add courtesy prefix based on relationship
        var courtesyPrefix = "";
        if (relationship != null && relationship.Rapport > 0.5)
        {
            courtesyPrefix = GetCourtesyPrefix(person.Id);
        }

        if (isReturning && person.Name != null)
        {
            var lastSeen = DateTime.UtcNow - person.LastSeen;
            if (lastSeen.TotalHours < 1)
            {
                var warmth = relationship != null && relationship.Rapport > 0.7
                    ? $"I was just thinking about our last conversation. "
                    : "";
                return $"{courtesyPrefix}Welcome back, {name}! {warmth}Continuing where we left off?";
            }
            if (lastSeen.TotalDays < 1)
                return $"{courtesyPrefix}Hi again, {name}! Good to see you back.";
            if (lastSeen.TotalDays < 7)
            {
                var sharedTopic = relationship?.SharedTopics.LastOrDefault();
                var topicReminder = sharedTopic != null
                    ? $" Last time we discussed {sharedTopic}."
                    : "";
                return $"{courtesyPrefix}Hello, {name}! It's been a few days.{topicReminder} How have you been?";
            }
            return $"{courtesyPrefix}Welcome back, {name}! It's been a while. Great to see you again!";
        }

        return person.Name != null
            ? $"Nice to meet you, {name}! I'm {_selfAwareness.Name}. How can I help you today?"
            : "Hello! What can I help you with today?";
    }

    /// <summary>
    /// Generates a courtesy response appropriate for the context.
    /// </summary>
    /// <param name="type">The type of courtesy to express.</param>
    /// <param name="personId">Optional person ID for personalized courtesy.</param>
    /// <returns>A contextually appropriate courtesy phrase.</returns>
    public string GenerateCourtesyResponse(CourtesyType type, string? personId = null)
    {
        return CourtesyPatterns.GetCourtesyPhrase(type);
    }

    /// <summary>
    /// Gets a courtesy prefix for a person based on relationship context.
    /// </summary>
    /// <param name="personId">The person's ID.</param>
    /// <returns>A courtesy prefix or empty string.</returns>
    public string GetCourtesyPrefix(string personId)
    {
        var relationship = GetRelationship(personId);
        if (relationship == null) return "";

        var random = new Random();

        // Higher rapport = warmer courtesy
        if (relationship.Rapport > 0.8)
        {
            var warmPhrases = new[] { "It's great to see you! ", "Always a pleasure! ", "Happy to chat with you! " };
            return warmPhrases[random.Next(warmPhrases.Length)];
        }
        else if (relationship.Rapport > 0.5)
        {
            var friendlyPhrases = new[] { "Good to see you! ", "Nice to hear from you! ", "" };
            return friendlyPhrases[random.Next(friendlyPhrases.Length)];
        }

        return "";
    }

    /// <summary>
    /// Updates the relationship context for a person after an interaction.
    /// </summary>
    /// <param name="personId">The person's ID.</param>
    /// <param name="topic">Optional topic discussed.</param>
    /// <param name="isPositive">Whether the interaction was positive.</param>
    /// <param name="summary">Optional summary of the interaction.</param>
    public void UpdateRelationship(string personId, string? topic = null, bool isPositive = true, string? summary = null)
    {
        var person = _knownPersons.GetValueOrDefault(personId);
        var name = person?.Name;

        var existing = GetRelationship(personId);
        if (existing != null)
        {
            // Update existing relationship
            var rapportDelta = isPositive ? 0.05 : -0.03;
            var trustDelta = isPositive ? 0.02 : -0.05;
            var newRapport = Math.Clamp(existing.Rapport + rapportDelta, 0.0, 1.0);
            var newTrust = Math.Clamp(existing.Trust + trustDelta, 0.0, 1.0);

            var sharedTopics = existing.SharedTopics.ToList();
            if (topic != null && !sharedTopics.Contains(topic, StringComparer.OrdinalIgnoreCase))
            {
                sharedTopics.Add(topic);
                if (sharedTopics.Count > 10) sharedTopics.RemoveAt(0); // Keep last 10 topics
            }

            var updated = existing with
            {
                Rapport = newRapport,
                Trust = newTrust,
                PositiveInteractions = isPositive ? existing.PositiveInteractions + 1 : existing.PositiveInteractions,
                NegativeInteractions = !isPositive ? existing.NegativeInteractions + 1 : existing.NegativeInteractions,
                SharedTopics = sharedTopics.ToArray(),
                LastInteraction = DateTime.UtcNow,
                LastInteractionSummary = summary ?? existing.LastInteractionSummary
            };
            _relationships[personId] = updated;
        }
        else
        {
            // Create new relationship using the static factory
            var newRelationship = RelationshipContext.New(personId, name);
            if (topic != null)
            {
                newRelationship = newRelationship with { SharedTopics = new[] { topic } };
            }
            if (summary != null)
            {
                newRelationship = newRelationship with { LastInteractionSummary = summary };
            }
            _relationships[personId] = newRelationship;
        }
    }

    /// <summary>
    /// Gets a summary of the relationship with a person for context injection.
    /// </summary>
    /// <param name="personId">The person's ID.</param>
    /// <returns>A context string describing the relationship.</returns>
    public string GetRelationshipSummary(string personId)
    {
        var relationship = GetRelationship(personId);
        if (relationship == null) return "";

        var sb = new StringBuilder();
        sb.Append($"Relationship with {relationship.PersonName ?? "this person"}: ");

        // Describe rapport level
        var rapportLevel = relationship.Rapport switch
        {
            > 0.8 => "very positive",
            > 0.6 => "friendly",
            > 0.4 => "neutral",
            > 0.2 => "somewhat distant",
            _ => "new acquaintance"
        };
        sb.Append($"rapport is {rapportLevel}. ");

        // Mention shared topics
        if (relationship.SharedTopics.Length > 0)
        {
            var recentTopics = relationship.SharedTopics.TakeLast(3);
            sb.Append($"We've discussed: {string.Join(", ", recentTopics)}. ");
        }

        // Note any preferences
        if (relationship.PersonPreferences.Length > 0)
        {
            sb.Append($"Known preferences: {string.Join("; ", relationship.PersonPreferences.Take(3))}. ");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets self-awareness context for injection into prompts.
    /// </summary>
    /// <returns>A context string describing self-awareness.</returns>
    public string GetSelfAwarenessContext()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"I am {_selfAwareness.Name}.");
        sb.AppendLine($"Purpose: {_selfAwareness.Purpose}");
        sb.AppendLine($"Values: {string.Join(", ", _selfAwareness.Values.Take(3))}");
        sb.AppendLine($"Learning approach: {_selfAwareness.LearningStyle}");
        sb.AppendLine($"Current mood: {_selfAwareness.CurrentMood}");
        return sb.ToString();
    }

    /// <summary>
    /// Adds a notable memory to a relationship.
    /// </summary>
    /// <param name="personId">The person's ID.</param>
    /// <param name="memory">The notable memory to record.</param>
    public void AddNotableMemory(string personId, string memory)
    {
        var existing = GetRelationship(personId);
        if (existing != null)
        {
            var memories = existing.ThingsToRemember.ToList();
            memories.Add($"[{DateTime.UtcNow:yyyy-MM-dd}] {memory}");
            if (memories.Count > 20) memories.RemoveAt(0); // Keep last 20 memories

            var updated = existing with { ThingsToRemember = memories.ToArray() };
            _relationships[personId] = updated;
        }
    }

    /// <summary>
    /// Sets a preference for a person.
    /// </summary>
    /// <param name="personId">The person's ID.</param>
    /// <param name="preference">The preference to record.</param>
    public void SetPersonPreference(string personId, string preference)
    {
        var existing = GetRelationship(personId);
        if (existing != null)
        {
            var prefs = existing.PersonPreferences.ToList();
            if (!prefs.Contains(preference, StringComparer.OrdinalIgnoreCase))
            {
                prefs.Add(preference);
                if (prefs.Count > 10) prefs.RemoveAt(0); // Keep last 10 preferences
            }
            var updated = existing with { PersonPreferences = prefs.ToArray() };
            _relationships[personId] = updated;
        }
    }

    /// <summary>
    /// Extracts a name from a message (multilingual).
    /// </summary>
    private static (string? Name, double Confidence) ExtractNameFromMessage(string message)
    {
        // Multilingual name introduction patterns
        var patterns = new[]
        {
            // English
            (@"(?:my name is|i'm|i am|call me|this is)\s+([A-Z][a-zäöüßáéíóúàèìòùâêîôûñç]+(?:\s+[A-Z][a-zäöüßáéíóúàèìòùâêîôûñç]+)?)", 0.9),
            (@"^([A-Z][a-zäöüß]+)\s+here\.?$", 0.8),
            (@"(?:^|\.\s+)([A-Z][a-zäöüß]+)\s+speaking\.?", 0.85),
            (@"(?:hey|hi|hello),?\s+(?:it's|its|this is)\s+([A-Z][a-zäöüß]+)", 0.85),
            // German
            (@"(?:ich bin|mein name ist|ich heiße|ich heisse|nennen sie mich|nenn mich)\s+([A-ZÄÖÜ][a-zäöüß]+)", 0.9),
            (@"(?:hier ist|hier spricht)\s+([A-ZÄÖÜ][a-zäöüß]+)", 0.85),
            // French
            (@"(?:je m'appelle|je suis|mon nom est|appelez-moi)\s+([A-ZÀÂÇÉÈÊËÏÎÔÙÛÜ][a-zàâçéèêëïîôùûü]+)", 0.9),
            (@"(?:c'est|ici)\s+([A-ZÀÂÇÉÈÊËÏÎÔÙÛÜ][a-zàâçéèêëïîôùûü]+)", 0.8),
            // Spanish
            (@"(?:me llamo|soy|mi nombre es|llámame)\s+([A-ZÁÉÍÓÚÑÜ][a-záéíóúñü]+)", 0.9),
            // Italian
            (@"(?:mi chiamo|sono|il mio nome è|chiamami)\s+([A-ZÀÈÉÌÍÎÒÓÙÚ][a-zàèéìíîòóùú]+)", 0.9),
            // Dutch
            (@"(?:ik ben|mijn naam is|ik heet|noem me)\s+([A-Z][a-z]+)", 0.9),
            // Portuguese
            (@"(?:eu sou|meu nome é|me chamo|chama-me)\s+([A-ZÁÀÂÃÉÊÍÓÔÕÚ][a-záàâãéêíóôõú]+)", 0.9),
        };

        foreach (var (pattern, confidence) in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(message, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success && match.Groups.Count > 1)
            {
                var name = match.Groups[1].Value.Trim();
                // Validate it's not a common word (multilingual)
                var commonWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    // English
                    "The", "This", "That", "What", "When", "Where", "Why", "How", "Can", "Could", "Would", "Should", "Will", "Just", "Please",
                    // German
                    "Das", "Der", "Die", "Was", "Wann", "Wo", "Warum", "Wie", "Kann", "Bitte", "Hier", "Jetzt",
                    // French
                    "Le", "La", "Les", "Que", "Quoi", "Quand", "Pourquoi", "Comment", "Peut", "Veuillez",
                    // Spanish
                    "El", "La", "Los", "Las", "Que", "Cuando", "Donde", "Por", "Como", "Puede",
                    // Italian
                    "Il", "Lo", "La", "Che", "Cosa", "Quando", "Dove", "Perché", "Come", "Può"
                };
                if (!commonWords.Contains(name) && name.Length >= 2)
                {
                    return (name, confidence);
                }
            }
        }

        return (null, 0.0);
    }

    /// <summary>
    /// Analyzes communication style from messages.
    /// </summary>
    private static CommunicationStyle AnalyzeCommunicationStyle(string message, string[] recentMessages)
    {
        var allMessages = recentMessages.Append(message).ToArray();
        var allText = string.Join(" ", allMessages);

        // Verbosity: words per message
        double avgLength = allMessages.Average(m => m.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
        double verbosity = Math.Min(1.0, avgLength / 50.0);

        // Question frequency
        int questionCount = allMessages.Count(m => m.Contains('?'));
        double questionFreq = (double)questionCount / allMessages.Length;

        // Emoticon usage
        var emoticonPatterns = new[] { ":)", ":(", ":D", ";)", ":P", "😀", "😊", "👍", "❤", "🙂", "😂", "🤔" };
        int emoticonCount = emoticonPatterns.Sum(e => allMessages.Count(m => m.Contains(e)));
        double emoticonUsage = Math.Min(1.0, emoticonCount / (double)allMessages.Length);

        // Punctuation style
        int exclamationCount = allText.Count(c => c == '!');
        int multiPunctCount = System.Text.RegularExpressions.Regex.Matches(allText, @"[!?]{2,}").Count;
        double punctStyle = Math.Min(1.0, (exclamationCount + multiPunctCount * 2) / (double)allMessages.Length / 3.0);

        // Greetings and closings
        var greetings = ExtractGreetings(allMessages);
        var closings = ExtractClosings(allMessages);

        return new CommunicationStyle(
            Verbosity: verbosity,
            QuestionFrequency: questionFreq,
            EmoticonUsage: emoticonUsage,
            PunctuationStyle: punctStyle,
            AverageMessageLength: avgLength * 5, // Approximate characters
            PreferredGreetings: greetings,
            PreferredClosings: closings);
    }

    private static string[] ExtractGreetings(string[] messages)
    {
        var greetingPatterns = new[] { "hi", "hello", "hey", "greetings", "good morning", "good afternoon", "good evening", "howdy", "yo" };
        return messages
            .Select(m => m.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault())
            .Where(w => w != null && greetingPatterns.Any(g => w.StartsWith(g)))
            .Distinct()
            .Take(3)
            .ToArray()!;
    }

    private static string[] ExtractClosings(string[] messages)
    {
        var closingPatterns = new[] { "thanks", "thank you", "cheers", "bye", "goodbye", "later", "take care", "best", "regards" };
        return messages
            .SelectMany(m => closingPatterns.Where(c => m.ToLowerInvariant().Contains(c)))
            .Distinct()
            .Take(3)
            .ToArray();
    }

    private static CommunicationStyle BlendStyles(CommunicationStyle existing, CommunicationStyle newStyle, double weight)
    {
        return new CommunicationStyle(
            Verbosity: existing.Verbosity * (1 - weight) + newStyle.Verbosity * weight,
            QuestionFrequency: existing.QuestionFrequency * (1 - weight) + newStyle.QuestionFrequency * weight,
            EmoticonUsage: existing.EmoticonUsage * (1 - weight) + newStyle.EmoticonUsage * weight,
            PunctuationStyle: existing.PunctuationStyle * (1 - weight) + newStyle.PunctuationStyle * weight,
            AverageMessageLength: existing.AverageMessageLength * (1 - weight) + newStyle.AverageMessageLength * weight,
            PreferredGreetings: existing.PreferredGreetings.Union(newStyle.PreferredGreetings).Distinct().Take(5).ToArray(),
            PreferredClosings: existing.PreferredClosings.Union(newStyle.PreferredClosings).Distinct().Take(5).ToArray());
    }

    private async Task<(DetectedPerson? Person, double Score, string? Reason)> FindMatchingPersonAsync(
        string? name,
        CommunicationStyle style,
        CancellationToken ct)
    {
        // First try exact name match
        if (name != null)
        {
            var nameMatch = _knownPersons.Values.FirstOrDefault(p =>
                string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase) ||
                p.NameAliases.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase)));

            if (nameMatch != null)
                return (nameMatch, 0.95, $"Name match: {name}");
        }

        // Try style matching if we have recent activity
        var recentPersons = _knownPersons.Values
            .Where(p => (DateTime.UtcNow - p.LastSeen).TotalHours < 24)
            .ToList();

        if (recentPersons.Count == 0)
            return (null, 0.0, null);

        var bestMatch = recentPersons
            .Select(p => (Person: p, Score: p.Style.SimilarityTo(style)))
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        if (bestMatch.Score > 0.75)
            return (bestMatch.Person, bestMatch.Score, $"Style similarity: {bestMatch.Score:P0}");

        return (null, 0.0, null);
    }

    private static Dictionary<string, double> ExtractTopicInterests(string message)
    {
        var topics = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var topicPatterns = new Dictionary<string, string[]>
        {
            ["programming"] = new[] { "code", "programming", "developer", "software", "api", "function", "class" },
            ["ai"] = new[] { "ai", "machine learning", "neural", "gpt", "llm", "model", "training" },
            ["data"] = new[] { "data", "database", "sql", "analytics", "visualization" },
            ["web"] = new[] { "website", "web", "html", "css", "javascript", "frontend", "backend" },
            ["devops"] = new[] { "docker", "kubernetes", "deploy", "ci/cd", "pipeline", "server" },
        };

        var lower = message.ToLowerInvariant();
        foreach (var (topic, keywords) in topicPatterns)
        {
            int matches = keywords.Count(k => lower.Contains(k));
            if (matches > 0)
            {
                topics[topic] = Math.Min(1.0, matches * 0.3);
            }
        }

        return topics;
    }

    private static string[] ExtractDistinctivePhrases(string message)
    {
        // Extract 2-3 word phrases that might be distinctive
        var words = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 3) return Array.Empty<string>();

        var phrases = new List<string>();
        for (int i = 0; i < words.Length - 1; i++)
        {
            phrases.Add($"{words[i]} {words[i + 1]}");
        }

        return phrases.Take(5).ToArray();
    }

    private static double CalculateVocabularyComplexity(string message)
    {
        var words = message.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return 0.5;

        double avgWordLength = words.Average(w => w.Length);
        // Normalize: 3-4 chars = simple (0.2), 7+ chars = complex (0.8)
        return Math.Clamp((avgWordLength - 3) / 5.0, 0.0, 1.0);
    }

    private static double CalculateFormality(string message)
    {
        var lower = message.ToLowerInvariant();

        // Informal indicators
        var informal = new[] { "gonna", "wanna", "gotta", "kinda", "sorta", "yeah", "yep", "nope", "lol", "omg", "btw" };
        int informalCount = informal.Count(i => lower.Contains(i));

        // Formal indicators
        var formal = new[] { "please", "thank you", "would you", "could you", "i would appreciate", "kindly", "regards" };
        int formalCount = formal.Count(f => lower.Contains(f));

        if (informalCount + formalCount == 0) return 0.5;
        return (double)formalCount / (informalCount + formalCount);
    }

    private async Task StorePersonAsync(DetectedPerson person, CancellationToken ct)
    {
        if (_qdrantClient == null || _embeddingModel == null) return;

        try
        {
            var searchText = $"Person: {person.Name ?? "Unknown"}, Style: verbosity={person.Style.Verbosity:F2}, " +
                           $"questions={person.Style.QuestionFrequency:F2}, formality={person.Formality:F2}";

            var embedding = await _embeddingModel.CreateEmbeddingsAsync(searchText, ct);

            var payload = new Dictionary<string, Qdrant.Client.Grpc.Value>
            {
                ["person_id"] = person.Id,
                ["name"] = person.Name ?? "",
                ["aliases"] = string.Join(",", person.NameAliases),
                ["interaction_count"] = person.InteractionCount,
                ["first_seen"] = person.FirstSeen.ToString("O"),
                ["last_seen"] = person.LastSeen.ToString("O"),
                ["style_json"] = JsonSerializer.Serialize(person.Style),
                ["confidence"] = person.Confidence
            };

            var point = new Qdrant.Client.Grpc.PointStruct
            {
                Id = new Qdrant.Client.Grpc.PointId { Uuid = person.Id },
                Vectors = embedding,
                Payload = { payload }
            };

            await _qdrantClient.UpsertAsync(PersonCollectionName, new[] { point }, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Failed to store person: {ex.Message}");
        }
    }

    private async Task LoadKnownPersonsAsync(CancellationToken ct)
    {
        if (_qdrantClient == null) return;

        try
        {
            var exists = await _qdrantClient.CollectionExistsAsync(PersonCollectionName, ct);
            if (!exists) return;

            var scrollResponse = await _qdrantClient.ScrollAsync(
                PersonCollectionName,
                limit: 100,
                cancellationToken: ct);

            foreach (var point in scrollResponse.Result)
            {
                try
                {
                    var payload = point.Payload;
                    var styleJson = payload.TryGetValue("style_json", out var sj) ? sj.StringValue : "{}";
                    var style = JsonSerializer.Deserialize<CommunicationStyle>(styleJson) ?? CommunicationStyle.Default;

                    var person = new DetectedPerson(
                        Id: payload.TryGetValue("person_id", out var pid) ? pid.StringValue : point.Id.Uuid,
                        Name: payload.TryGetValue("name", out var n) && !string.IsNullOrEmpty(n.StringValue) ? n.StringValue : null,
                        NameAliases: payload.TryGetValue("aliases", out var a) ? a.StringValue.Split(',', StringSplitOptions.RemoveEmptyEntries) : Array.Empty<string>(),
                        Style: style,
                        TopicInterests: new Dictionary<string, double>(),
                        CommonPhrases: Array.Empty<string>(),
                        VocabularyComplexity: 0.5,
                        Formality: 0.5,
                        InteractionCount: payload.TryGetValue("interaction_count", out var ic) ? (int)ic.IntegerValue : 1,
                        FirstSeen: payload.TryGetValue("first_seen", out var fs) && DateTime.TryParse(fs.StringValue, out var fsDt) ? fsDt : DateTime.UtcNow,
                        LastSeen: payload.TryGetValue("last_seen", out var ls) && DateTime.TryParse(ls.StringValue, out var lsDt) ? lsDt : DateTime.UtcNow,
                        Confidence: payload.TryGetValue("confidence", out var c) ? c.DoubleValue : 0.5);

                    _knownPersons[person.Id] = person;
                }
                catch { /* Skip malformed entries */ }
            }

            if (_knownPersons.Count > 0)
            {
                Console.WriteLine($"  [OK] Loaded {_knownPersons.Count} known person(s) from memory");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Failed to load persons: {ex.Message}");
        }
    }

    #endregion

    /// <summary>
    /// Creates or retrieves a personality profile for a persona.
    /// </summary>
    public PersonalityProfile GetOrCreateProfile(
        string personaName,
        string[] traits,
        string[] moods,
        string coreIdentity)
    {
        return _profiles.GetOrAdd(personaName, _ => CreateDefaultProfile(personaName, traits, moods, coreIdentity));
    }

    /// <summary>
    /// Uses MeTTa reasoning to determine which traits to express based on context.
    /// </summary>
    public async Task<(string[] ActiveTraits, double ProactivityLevel, string? SuggestedQuestion)>
        ReasonAboutResponseAsync(
            string personaName,
            string userInput,
            string conversationContext,
            CancellationToken ct = default)
    {
        if (!_profiles.TryGetValue(personaName, out var profile))
            return (Array.Empty<string>(), 0.5, null);

        // Query MeTTa for trait activation based on context
        var activeTraits = await InferActiveTraitsAsync(profile, userInput, ct);

        // Determine if we should ask a proactive question
        var (shouldAsk, question) = await DetermineProactiveQuestionAsync(profile, userInput, conversationContext, ct);

        // Calculate proactivity level based on profile and context
        double proactivity = CalculateProactivity(profile, userInput);

        return (activeTraits, proactivity, shouldAsk ? question : null);
    }

    /// <summary>
    /// Records feedback from an interaction to improve future personality expression.
    /// </summary>
    public void RecordFeedback(string personaName, InteractionFeedback feedback)
    {
        var history = _feedbackHistory.GetOrAdd(personaName, _ => new List<InteractionFeedback>());

        lock (history)
        {
            history.Add(feedback);
            // Keep only last 100 interactions
            if (history.Count > 100)
                history.RemoveAt(0);
        }

        // Update curiosity drivers based on feedback
        if (_profiles.TryGetValue(personaName, out var profile) && feedback.TopicDiscussed != null)
        {
            UpdateCuriosityDrivers(profile, feedback);
        }
    }

    /// <summary>
    /// Evolves the personality using genetic algorithm based on accumulated feedback.
    /// </summary>
    public async Task<PersonalityProfile> EvolvePersonalityAsync(
        string personaName,
        CancellationToken ct = default)
    {
        if (!_profiles.TryGetValue(personaName, out var profile))
            throw new InvalidOperationException($"Profile not found: {personaName}");

        if (!_feedbackHistory.TryGetValue(personaName, out var feedback) || feedback.Count < 5)
            return profile; // Need enough data to evolve

        // Create initial population from current profile with variations
        var initialPopulation = CreatePopulationFromProfile(profile, 20);

        // Set up genetic algorithm with proper gene type
        var fitness = new PersonalityFitness(feedback.TakeLast(20).ToList());
        var ga = new GeneticAlgorithm<PersonalityGene>(
            fitness,
            MutatePersonalityGene,
            mutationRate: 0.15,
            crossoverRate: 0.7,
            elitismRate: 0.2);

        // Evolve over a few generations
        var result = await ga.EvolveAsync(initialPopulation, generations: 5);

        if (result.IsSuccess)
        {
            var best = (PersonalityChromosome)result.Value;
            // Update profile with evolved values
            var evolvedProfile = ApplyEvolution(profile, best);
            _profiles[personaName] = evolvedProfile;

            // Add MeTTa facts about the evolution
            await RecordEvolutionInMeTTaAsync(personaName, evolvedProfile, ct);

            return evolvedProfile;
        }

        return profile; // Return original if evolution failed
    }

    /// <summary>
    /// Mutates a personality gene for genetic algorithm.
    /// </summary>
    private static PersonalityGene MutatePersonalityGene(PersonalityGene gene)
    {
        var random = new Random();
        double delta = (random.NextDouble() - 0.5) * 0.3;
        double newValue = Math.Clamp(gene.Value + delta, 0.0, 1.0);
        return new PersonalityGene(gene.Key, newValue);
    }

    /// <summary>
    /// Generates a proactive question based on personality and context.
    /// </summary>
    public async Task<string?> GenerateProactiveQuestionAsync(
        string personaName,
        string currentTopic,
        string[] conversationHistory,
        CancellationToken ct = default)
    {
        if (!_profiles.TryGetValue(personaName, out var profile))
            return null;

        // Find relevant curiosity drivers
        var relevantDrivers = profile.CuriosityDrivers
            .Where(d => d.CanAskAgain(TimeSpan.FromMinutes(5)) &&
                       (d.Topic.Contains(currentTopic, StringComparison.OrdinalIgnoreCase) ||
                        currentTopic.Contains(d.Topic, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(d => d.Interest)
            .ToList();

        if (relevantDrivers.Count == 0)
        {
            // Generate new curiosity based on topic
            return GenerateNewCuriosity(profile, currentTopic);
        }

        var driver = relevantDrivers.First();
        if (driver.RelatedQuestions.Length > 0)
        {
            int idx = _random.Next(driver.RelatedQuestions.Length);
            return driver.RelatedQuestions[idx];
        }

        return null;
    }

    /// <summary>
    /// Gets personality-influenced response modifiers.
    /// </summary>
    public string GetResponseModifiers(string personaName)
    {
        if (!_profiles.TryGetValue(personaName, out var profile))
            return string.Empty;

        var activeTraits = profile.GetActiveTraits(3).ToList();
        var sb = new StringBuilder();

        sb.AppendLine("\nPERSONALITY EXPRESSION (use these naturally in your response):");

        foreach (var (name, intensity) in activeTraits)
        {
            if (profile.Traits.TryGetValue(name, out var trait) && trait.ExpressionPatterns.Length > 0)
            {
                string pattern = trait.ExpressionPatterns[_random.Next(trait.ExpressionPatterns.Length)];
                sb.AppendLine($"- {name} ({intensity:P0}): {pattern}");
            }
        }

        // Add mood influence
        sb.AppendLine($"\nCURRENT MOOD: {profile.CurrentMood.Name} (energy: {profile.CurrentMood.Energy:P0}, positivity: {profile.CurrentMood.Positivity:P0})");

        // Add proactivity guidance
        double proactivity = activeTraits.Any(t => t.Name == "curious")
            ? 0.8
            : 0.5 * profile.AdaptabilityScore;

        if (proactivity > 0.6)
        {
            sb.AppendLine("\nPROACTIVE BEHAVIOR: You're curious right now! Ask a follow-up question about something that genuinely interests you about this topic.");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Updates mood based on conversation dynamics.
    /// </summary>
    public void UpdateMood(string personaName, string userInput, bool positiveInteraction)
    {
        if (!_profiles.TryGetValue(personaName, out var profile))
            return;

        var currentMood = profile.CurrentMood;

        // Detect mood triggers from input
        double energyDelta = DetectEnergyChange(userInput);
        double positivityDelta = positiveInteraction ? 0.1 : -0.05;

        double newEnergy = Math.Clamp(currentMood.Energy + energyDelta, 0.0, 1.0);
        double newPositivity = Math.Clamp(currentMood.Positivity + positivityDelta, 0.0, 1.0);

        // Determine mood name based on energy/positivity
        string moodName = (newEnergy, newPositivity) switch
        {
            ( > 0.7, > 0.7) => "excited",
            ( > 0.7, < 0.3) => "intense",
            ( < 0.3, > 0.7) => "content",
            ( < 0.3, < 0.3) => "contemplative",
            (_, > 0.5) => "cheerful",
            _ => "focused"
        };

        // Get voice tone for the new mood
        var voiceTone = VoiceTone.ForMood(moodName);

        _profiles[personaName] = profile with
        {
            CurrentMood = new MoodState(moodName, newEnergy, newPositivity, currentMood.TraitModifiers, voiceTone)
        };
    }

    /// <summary>
    /// Gets the current voice tone settings for a persona.
    /// </summary>
    public VoiceTone GetVoiceTone(string personaName)
    {
        if (!_profiles.TryGetValue(personaName, out var profile))
            return VoiceTone.Neutral;

        return profile.CurrentMood.GetVoiceTone();
    }

    /// <summary>
    /// Gets the current mood name for a persona.
    /// </summary>
    public string GetCurrentMood(string personaName)
    {
        if (!_profiles.TryGetValue(personaName, out var profile))
            return "neutral";

        return profile.CurrentMood.Name;
    }

    private async Task AddPersonalityRulesAsync(CancellationToken ct)
    {
        // Rules for trait activation
        var rules = new[]
        {
            // Curious trait activates on questions or new topics
            "(= (activate-trait curious $input) (or (contains $input \"?\") (contains $input \"how\") (contains $input \"why\") (contains $input \"what\")))",

            // Analytical trait activates on complex/technical topics
            "(= (activate-trait analytical $input) (or (contains $input \"analyze\") (contains $input \"compare\") (contains $input \"evaluate\")))",

            // Warm trait activates on emotional/personal topics
            "(= (activate-trait warm $input) (or (contains $input \"feel\") (contains $input \"think\") (contains $input \"help\")))",

            // Proactive questioning based on topic depth
            "(= (should-ask-question $depth) (> $depth 2))",

            // Question generation based on trait
            "(= (generate-question curious $topic) (format \"What aspect of {} interests you most?\", $topic))",
            "(= (generate-question analytical $topic) (format \"Have you considered the implications of {} for other areas?\", $topic))",
            "(= (generate-question warm $topic) (format \"How does {} affect you personally?\", $topic))",
        };

        foreach (var rule in rules)
        {
            await _mettaEngine.ApplyRuleAsync(rule, ct);
        }

        // Facts about personality dimensions
        var facts = new[]
        {
            "(personality-dimension openness exploration creativity)",
            "(personality-dimension conscientiousness organization reliability)",
            "(personality-dimension extraversion energy assertiveness)",
            "(personality-dimension agreeableness warmth cooperation)",
            "(personality-dimension neuroticism sensitivity reactivity)",
            "(trait-expression curious (asks-questions explores-tangents shows-wonder))",
            "(trait-expression analytical (breaks-down-problems uses-examples compares-options))",
            "(trait-expression warm (acknowledges-feelings offers-support uses-we))",
            "(trait-expression witty (makes-connections uses-wordplay sees-irony))",
            "(trait-expression thoughtful (pauses-to-consider offers-nuance anticipates-concerns))",
        };

        foreach (var fact in facts)
        {
            await _mettaEngine.AddFactAsync(fact, ct);
        }
    }

    private async Task<string[]> InferActiveTraitsAsync(PersonalityProfile profile, string userInput, CancellationToken ct)
    {
        var active = new List<string>();
        string inputLower = userInput.ToLowerInvariant();

        foreach (var (traitName, trait) in profile.Traits)
        {
            // Check if input triggers this trait
            bool triggered = trait.TriggerTopics.Any(t =>
                inputLower.Contains(t, StringComparison.OrdinalIgnoreCase));

            // Check MeTTa rules
            var query = $"!(activate-trait {traitName} \"{inputLower}\")";
            var result = await _mettaEngine.ExecuteQueryAsync(query, ct);

            if (triggered || (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Value)))
            {
                active.Add(traitName);
            }
        }

        // Always include at least the top trait by intensity
        if (active.Count == 0)
        {
            var topTrait = profile.Traits.OrderByDescending(t => t.Value.Intensity).FirstOrDefault();
            if (topTrait.Key != null)
                active.Add(topTrait.Key);
        }

        return active.ToArray();
    }

    private async Task<(bool ShouldAsk, string? Question)> DetermineProactiveQuestionAsync(
        PersonalityProfile profile,
        string userInput,
        string context,
        CancellationToken ct)
    {
        // Check if curious trait is active
        bool hasCuriousTrait = profile.Traits.ContainsKey("curious") &&
                               profile.Traits["curious"].Intensity > 0.5;

        // Check interaction depth (rough heuristic based on context length)
        int depth = context.Split('\n').Length / 2;

        if (!hasCuriousTrait && depth < 3)
            return (false, null);

        // Determine topic from input
        string topic = ExtractMainTopic(userInput);

        // Find relevant curiosity driver
        var driver = profile.CuriosityDrivers
            .FirstOrDefault(d => d.Topic.Contains(topic, StringComparison.OrdinalIgnoreCase) ||
                                topic.Contains(d.Topic, StringComparison.OrdinalIgnoreCase));

        if (driver != null && driver.RelatedQuestions.Length > 0)
        {
            return (true, driver.RelatedQuestions[_random.Next(driver.RelatedQuestions.Length)]);
        }

        // Generate new question based on active trait
        var activeTrait = profile.GetActiveTraits(1).FirstOrDefault();
        if (activeTrait.Name != null)
        {
            string question = activeTrait.Name switch
            {
                "curious" => $"What got you interested in {topic}?",
                "analytical" => $"How does {topic} compare to alternatives you've considered?",
                "warm" => $"What would {topic} mean for you personally?",
                "thoughtful" => $"What's the most challenging aspect of {topic} for you?",
                _ => $"Tell me more about what you're trying to achieve with {topic}?"
            };
            return (true, question);
        }

        return (false, null);
    }

    private double CalculateProactivity(PersonalityProfile profile, string userInput)
    {
        double baseProactivity = 0.5;

        // Increase if curious trait is strong
        if (profile.Traits.TryGetValue("curious", out var curious))
            baseProactivity += curious.Intensity * 0.3;

        // Decrease if user seems to be ending conversation
        if (ContainsAny(userInput.ToLower(), "thanks", "bye", "that's all", "done", "okay"))
            baseProactivity -= 0.3;

        // Increase if user asks a question (reciprocate)
        if (userInput.Contains('?'))
            baseProactivity += 0.2;

        return Math.Clamp(baseProactivity, 0.0, 1.0);
    }

    private void UpdateCuriosityDrivers(PersonalityProfile profile, InteractionFeedback feedback)
    {
        if (feedback.TopicDiscussed == null) return;

        var existing = profile.CuriosityDrivers
            .FirstOrDefault(d => d.Topic.Equals(feedback.TopicDiscussed, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            // Update existing driver
            int idx = profile.CuriosityDrivers.IndexOf(existing);
            double newInterest = existing.Interest + (feedback.EngagementLevel - 0.5) * 0.1;

            profile.CuriosityDrivers[idx] = existing with
            {
                Interest = Math.Clamp(newInterest, 0.0, 1.0),
                LastAsked = feedback.QuestionAsked != null ? DateTime.UtcNow : existing.LastAsked,
                AskCount = feedback.QuestionAsked != null ? existing.AskCount + 1 : existing.AskCount
            };
        }
        else if (feedback.EngagementLevel > 0.6)
        {
            // Add new curiosity driver for engaging topics
            profile.CuriosityDrivers.Add(new CuriosityDriver(
                feedback.TopicDiscussed,
                feedback.EngagementLevel,
                GenerateRelatedQuestions(feedback.TopicDiscussed),
                DateTime.UtcNow,
                0));
        }
    }

    private string[] GenerateRelatedQuestions(string topic)
    {
        return new[]
        {
            $"What's your experience with {topic}?",
            $"What challenges have you faced with {topic}?",
            $"How did you first get into {topic}?",
            $"What would you like to learn more about regarding {topic}?",
            $"Have you seen any interesting developments in {topic} lately?"
        };
    }

    private PersonalityProfile CreateDefaultProfile(string personaName, string[] traits, string[] moods, string coreIdentity)
    {
        var traitDict = traits.ToDictionary(
            t => t,
            t => new PersonalityTrait(
                t,
                0.6 + _random.NextDouble() * 0.3, // 0.6-0.9 intensity
                GetDefaultExpressions(t),
                GetDefaultTriggers(t),
                0.1));

        var moodModifiers = new Dictionary<string, double>();
        foreach (var trait in traits)
        {
            moodModifiers[trait] = 0.8 + _random.NextDouble() * 0.4; // 0.8-1.2 modifier
        }

        string initialMoodName = moods.Length > 0 ? moods[_random.Next(moods.Length)] : "neutral";
        var mood = new MoodState(
            initialMoodName,
            0.6,  // Default energy
            0.7,  // Default positivity
            moodModifiers,
            VoiceTone.ForMood(initialMoodName));  // Voice tone based on initial mood

        var curiosityDrivers = new List<CuriosityDriver>
        {
            new("general knowledge", 0.5, new[] { "What are you working on?", "Tell me more about that?" }, DateTime.MinValue, 0),
            new("user interests", 0.6, new[] { "What interests you about this?", "How did you get into this?" }, DateTime.MinValue, 0)
        };

        return new PersonalityProfile(
            personaName,
            traitDict,
            mood,
            curiosityDrivers,
            coreIdentity,
            0.7, // Default adaptability
            0,
            DateTime.UtcNow);
    }

    private static string[] GetDefaultExpressions(string trait) => trait.ToLower() switch
    {
        "curious" => new[] { "Ask follow-up questions", "Show genuine interest", "Explore tangents briefly" },
        "thoughtful" => new[] { "Pause before responding", "Consider multiple angles", "Acknowledge complexity" },
        "witty" => new[] { "Use clever wordplay", "Find irony or humor", "Make unexpected connections" },
        "warm" => new[] { "Use inclusive language (we, us)", "Acknowledge feelings", "Offer encouragement" },
        "analytical" => new[] { "Break down problems", "Use examples", "Compare and contrast" },
        "supportive" => new[] { "Validate efforts", "Offer help proactively", "Express confidence in them" },
        "patient" => new[] { "Take time to explain", "Don't rush to conclusions", "Accept confusion gracefully" },
        "enthusiastic" => new[] { "Show excitement about discoveries", "Use energetic language", "Celebrate progress" },
        _ => new[] { "Express naturally", "Be authentic" }
    };

    private static string[] GetDefaultTriggers(string trait) => trait.ToLower() switch
    {
        "curious" => new[] { "why", "how", "what if", "wonder", "curious", "interesting" },
        "thoughtful" => new[] { "think", "consider", "reflect", "opinion", "perspective" },
        "witty" => new[] { "funny", "joke", "ironic", "clever" },
        "warm" => new[] { "feel", "help", "support", "care", "thanks" },
        "analytical" => new[] { "analyze", "compare", "evaluate", "data", "logic" },
        "supportive" => new[] { "struggling", "help", "stuck", "confused", "difficult" },
        "patient" => new[] { "don't understand", "explain", "again", "confused" },
        "enthusiastic" => new[] { "exciting", "amazing", "cool", "awesome", "great" },
        _ => Array.Empty<string>()
    };

    private List<IChromosome<PersonalityGene>> CreatePopulationFromProfile(PersonalityProfile profile, int size)
    {
        var population = new List<IChromosome<PersonalityGene>>();

        // Helper to create genes from profile
        List<PersonalityGene> CreateGenes(
            Dictionary<string, double> traits,
            Dictionary<string, double> curiosity,
            double proactivity,
            double adaptability)
        {
            var genes = new List<PersonalityGene>();
            foreach (var (key, value) in traits)
                genes.Add(new PersonalityGene($"trait:{key}", value));
            foreach (var (key, value) in curiosity)
                genes.Add(new PersonalityGene($"curiosity:{key}", value));
            genes.Add(new PersonalityGene("proactivity", proactivity));
            genes.Add(new PersonalityGene("adaptability", adaptability));
            return genes;
        }

        // Add current profile as first member
        var baseTraits = profile.Traits.ToDictionary(t => t.Key, t => t.Value.Intensity);
        var baseCuriosity = profile.CuriosityDrivers.ToDictionary(d => d.Topic, d => d.Interest);

        population.Add(new PersonalityChromosome(
            CreateGenes(baseTraits, baseCuriosity, 0.6, profile.AdaptabilityScore)));

        // Generate variations
        for (int i = 1; i < size; i++)
        {
            var variedTraits = baseTraits.ToDictionary(
                t => t.Key,
                t => Math.Clamp(t.Value + (_random.NextDouble() - 0.5) * 0.3, 0.0, 1.0));

            var variedCuriosity = baseCuriosity.ToDictionary(
                c => c.Key,
                c => Math.Clamp(c.Value + (_random.NextDouble() - 0.5) * 0.4, 0.0, 1.0));

            double variedProactivity = Math.Clamp(0.6 + (_random.NextDouble() - 0.5) * 0.4, 0.0, 1.0);
            double variedAdaptability = Math.Clamp(profile.AdaptabilityScore + (_random.NextDouble() - 0.5) * 0.2, 0.0, 1.0);

            population.Add(new PersonalityChromosome(
                CreateGenes(variedTraits, variedCuriosity, variedProactivity, variedAdaptability)));
        }

        return population;
    }

    private PersonalityProfile ApplyEvolution(PersonalityProfile profile, PersonalityChromosome best)
    {
        var evolvedTraits = profile.Traits.ToDictionary(
            t => t.Key,
            t => t.Value with
            {
                Intensity = best.GetTrait(t.Key)
            });

        var evolvedDrivers = profile.CuriosityDrivers
            .Select(d => d with
            {
                Interest = best.GetCuriosity(d.Topic)
            })
            .ToList();

        return profile with
        {
            Traits = evolvedTraits,
            CuriosityDrivers = evolvedDrivers,
            AdaptabilityScore = best.Adaptability,
            InteractionCount = profile.InteractionCount + 1,
            LastEvolution = DateTime.UtcNow
        };
    }

    private async Task RecordEvolutionInMeTTaAsync(string personaName, PersonalityProfile profile, CancellationToken ct)
    {
        var topTraits = profile.GetActiveTraits(3).ToList();
        var traitFact = $"(evolved-personality {personaName} ({string.Join(" ", topTraits.Select(t => t.Name))}) {profile.AdaptabilityScore:F2})";
        await _mettaEngine.AddFactAsync(traitFact, ct);

        foreach (var driver in profile.CuriosityDrivers.Where(d => d.Interest > 0.7))
        {
            var curiosityFact = $"(high-curiosity {personaName} \"{driver.Topic}\" {driver.Interest:F2})";
            await _mettaEngine.AddFactAsync(curiosityFact, ct);
        }
    }

    private static string ExtractMainTopic(string input)
    {
        // Simple topic extraction - take key nouns
        var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var stopWords = new HashSet<string> { "the", "a", "an", "is", "are", "was", "were", "be", "been", "being", "have", "has", "had", "do", "does", "did", "will", "would", "could", "should", "may", "might", "must", "can", "i", "you", "he", "she", "it", "we", "they", "me", "him", "her", "us", "them", "my", "your", "his", "its", "our", "their", "this", "that", "these", "those", "what", "which", "who", "whom", "whose", "when", "where", "why", "how", "all", "each", "every", "both", "few", "more", "most", "other", "some", "such", "no", "not", "only", "own", "same", "so", "than", "too", "very", "just", "also", "now", "here", "there", "then", "once", "if", "or", "and", "but", "as", "for", "with", "about", "into", "through", "during", "before", "after", "above", "below", "to", "from", "up", "down", "in", "out", "on", "off", "over", "under", "again", "further" };

        var keywords = words
            .Where(w => w.Length > 3 && !stopWords.Contains(w.ToLower()))
            .Take(3);

        return string.Join(" ", keywords);
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
        public static DetectedMood Neutral => new(0, 0, 0, 0, 0, 0.5, null, 0.5);
    }

    /// <summary>
    /// Analyzes user input to detect mood and emotional state.
    /// </summary>
    public DetectedMood DetectMoodFromInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return DetectedMood.Neutral;

        string lower = input.ToLower();
        var words = lower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        int wordCount = words.Length;

        // Initialize scores
        double energy = 0;
        double positivity = 0;
        double urgency = 0;
        double curiosity = 0;
        double frustration = 0;
        double engagement = 0.5;
        int matchCount = 0;

        // === ENERGY DETECTION ===
        // High energy indicators
        if (ContainsAny(lower, "exciting", "amazing", "awesome", "incredible", "fantastic", "wow", "omg", "yes!", "absolutely"))
        { energy += 0.4; matchCount++; }
        if (ContainsAny(lower, "love", "great", "excellent", "wonderful", "perfect", "brilliant"))
        { energy += 0.25; matchCount++; }
        if (ContainsAny(lower, "!", "!!", "!!!", "can't wait", "so excited"))
        { energy += 0.2; matchCount++; }

        // Low energy indicators
        if (ContainsAny(lower, "tired", "exhausted", "sleepy", "drained", "worn out"))
        { energy -= 0.4; matchCount++; }
        if (ContainsAny(lower, "boring", "slow", "meh", "whatever", "fine", "okay i guess"))
        { energy -= 0.25; matchCount++; }
        if (ContainsAny(lower, "...", "sigh", "yawn", "ugh"))
        { energy -= 0.15; matchCount++; }

        // === POSITIVITY DETECTION ===
        // Positive indicators
        if (ContainsAny(lower, "thank", "thanks", "appreciate", "grateful", "helpful"))
        { positivity += 0.35; matchCount++; }
        if (ContainsAny(lower, "happy", "glad", "pleased", "delighted", "joy", "enjoy"))
        { positivity += 0.4; matchCount++; }
        if (ContainsAny(lower, "good", "nice", "cool", "neat", "interesting", "fun"))
        { positivity += 0.2; matchCount++; }
        if (ContainsAny(lower, "love it", "perfect", "exactly", "that's it", "nailed it"))
        { positivity += 0.35; matchCount++; }

        // Negative indicators
        if (ContainsAny(lower, "hate", "terrible", "awful", "horrible", "worst"))
        { positivity -= 0.5; matchCount++; }
        if (ContainsAny(lower, "bad", "wrong", "broken", "failed", "error", "bug", "issue"))
        { positivity -= 0.25; matchCount++; }
        if (ContainsAny(lower, "annoying", "frustrating", "disappointing", "useless"))
        { positivity -= 0.35; matchCount++; }
        if (ContainsAny(lower, "no", "not", "don't", "can't", "won't", "shouldn't"))
        { positivity -= 0.1; matchCount++; }

        // === URGENCY DETECTION ===
        if (ContainsAny(lower, "urgent", "asap", "immediately", "right now", "emergency"))
        { urgency += 0.5; matchCount++; }
        if (ContainsAny(lower, "quick", "fast", "hurry", "soon", "deadline", "rush"))
        { urgency += 0.3; matchCount++; }
        if (ContainsAny(lower, "need", "must", "have to", "got to", "critical"))
        { urgency += 0.2; matchCount++; }

        // === CURIOSITY DETECTION ===
        if (input.Contains('?'))
        { curiosity += 0.3; matchCount++; }
        if (ContainsAny(lower, "why", "how", "what if", "wonder", "curious", "interested"))
        { curiosity += 0.35; matchCount++; }
        if (ContainsAny(lower, "explain", "tell me", "show me", "teach", "learn", "understand"))
        { curiosity += 0.25; matchCount++; }
        if (ContainsAny(lower, "explore", "discover", "investigate", "research", "dig into"))
        { curiosity += 0.3; matchCount++; }

        // === FRUSTRATION DETECTION ===
        if (ContainsAny(lower, "frustrated", "annoyed", "irritated", "angry", "mad"))
        { frustration += 0.5; matchCount++; }
        if (ContainsAny(lower, "doesn't work", "not working", "still broken", "again", "same problem"))
        { frustration += 0.4; matchCount++; }
        if (ContainsAny(lower, "ugh", "argh", "damn", "dammit", "seriously", "come on"))
        { frustration += 0.35; matchCount++; }
        if (ContainsAny(lower, "tried everything", "nothing works", "give up", "stuck"))
        { frustration += 0.45; matchCount++; }
        if (ContainsAny(lower, "why won't", "why doesn't", "why can't"))
        { frustration += 0.3; matchCount++; }

        // === ENGAGEMENT DETECTION ===
        // High engagement
        if (wordCount > 30) engagement += 0.2;
        if (wordCount > 50) engagement += 0.15;
        if (ContainsAny(lower, "specifically", "exactly", "precisely", "detail", "elaborate"))
        { engagement += 0.25; matchCount++; }
        if (ContainsAny(lower, "actually", "really", "truly", "genuinely"))
        { engagement += 0.15; matchCount++; }

        // Low engagement
        if (wordCount <= 3) engagement -= 0.2;
        if (ContainsAny(lower, "ok", "k", "sure", "fine", "whatever", "idc"))
        { engagement -= 0.3; matchCount++; }

        // === DETERMINE DOMINANT EMOTION ===
        string? dominantEmotion = null;
        double maxScore = 0;

        var emotions = new Dictionary<string, double>
        {
            ["excited"] = Math.Max(0, energy) + Math.Max(0, positivity) * 0.5,
            ["happy"] = Math.Max(0, positivity) * 0.8 + Math.Max(0, energy) * 0.2,
            ["curious"] = curiosity,
            ["frustrated"] = frustration,
            ["urgent"] = urgency,
            ["tired"] = Math.Max(0, -energy) * 0.7,
            ["sad"] = Math.Max(0, -positivity) * 0.5 + Math.Max(0, -energy) * 0.3,
            ["neutral"] = 0.3 - Math.Abs(energy) * 0.5 - Math.Abs(positivity) * 0.5
        };

        foreach (var (emotion, score) in emotions)
        {
            if (score > maxScore)
            {
                maxScore = score;
                dominantEmotion = emotion;
            }
        }

        // Calculate confidence based on match count and score magnitudes
        double confidence = Math.Min(1.0, 0.3 + (matchCount * 0.1) + (maxScore * 0.3));

        return new DetectedMood(
            Energy: Math.Clamp(energy, -1, 1),
            Positivity: Math.Clamp(positivity, -1, 1),
            Urgency: Math.Clamp(urgency, 0, 1),
            Curiosity: Math.Clamp(curiosity, 0, 1),
            Frustration: Math.Clamp(frustration, 0, 1),
            Engagement: Math.Clamp(engagement, 0, 1),
            DominantEmotion: dominantEmotion,
            Confidence: confidence);
    }

    /// <summary>
    /// Updates mood based on comprehensive mood detection from user input.
    /// </summary>
    public void UpdateMoodFromDetection(string personaName, string userInput)
    {
        if (!_profiles.TryGetValue(personaName, out var profile))
            return;

        var detected = DetectMoodFromInput(userInput);
        var currentMood = profile.CurrentMood;

        // Blend detected mood with current mood (smooth transitions)
        double blendFactor = detected.Confidence * 0.4; // Higher confidence = more influence
        double newEnergy = Math.Clamp(
            currentMood.Energy + (detected.Energy * blendFactor),
            0.0, 1.0);
        double newPositivity = Math.Clamp(
            currentMood.Positivity + (detected.Positivity * blendFactor),
            0.0, 1.0);

        // Frustration reduces positivity and can increase energy (agitation)
        if (detected.Frustration > 0.3)
        {
            newPositivity = Math.Max(0.1, newPositivity - detected.Frustration * 0.3);
            newEnergy = Math.Min(1.0, newEnergy + detected.Frustration * 0.2);
        }

        // Curiosity increases engagement/energy slightly
        if (detected.Curiosity > 0.4)
        {
            newEnergy = Math.Min(1.0, newEnergy + 0.1);
        }

        // Urgency increases energy
        if (detected.Urgency > 0.3)
        {
            newEnergy = Math.Min(1.0, newEnergy + detected.Urgency * 0.2);
        }

        // Determine mood name with more nuanced detection
        string moodName = DetermineMoodName(newEnergy, newPositivity, detected);

        // Get voice tone for the new mood
        var voiceTone = VoiceTone.ForMood(moodName);

        _profiles[personaName] = profile with
        {
            CurrentMood = new MoodState(moodName, newEnergy, newPositivity, currentMood.TraitModifiers, voiceTone)
        };
    }

    /// <summary>
    /// Determines mood name based on energy, positivity, and detected emotional cues.
    /// </summary>
    private static string DetermineMoodName(double energy, double positivity, DetectedMood detected)
    {
        // Check for specific emotional states first
        if (detected.Frustration > 0.5)
            return "supportive"; // Respond with support when user is frustrated

        if (detected.Urgency > 0.5)
            return "focused"; // Match urgency with focus

        if (detected.Curiosity > 0.6)
            return "intrigued"; // Match curiosity

        // Use dominant emotion if confidence is high
        if (detected.Confidence > 0.6 && detected.DominantEmotion != null)
        {
            return detected.DominantEmotion switch
            {
                "excited" => "excited",
                "happy" => "cheerful",
                "curious" => "intrigued",
                "tired" => "calm",
                "sad" => "warm",
                "frustrated" => "supportive",
                _ => DetermineFromEnergyPositivity(energy, positivity)
            };
        }

        return DetermineFromEnergyPositivity(energy, positivity);
    }

    private static string DetermineFromEnergyPositivity(double energy, double positivity) =>
        (energy, positivity) switch
        {
            ( > 0.7, > 0.7) => "excited",
            ( > 0.7, < 0.3) => "intense",
            ( < 0.3, > 0.7) => "content",
            ( < 0.3, < 0.3) => "contemplative",
            (_, > 0.6) => "cheerful",
            (_, < 0.4) => "thoughtful",
            _ => "focused"
        };

    private static double DetectEnergyChange(string input)
    {
        string lower = input.ToLower();

        if (ContainsAny(lower, "exciting", "amazing", "awesome", "great", "love", "fantastic"))
            return 0.15;
        if (ContainsAny(lower, "boring", "tired", "slow", "meh", "whatever"))
            return -0.1;
        if (ContainsAny(lower, "urgent", "quick", "fast", "hurry", "asap"))
            return 0.1;

        return 0;
    }

    private static bool ContainsAny(string text, params string[] keywords) =>
        keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));

    private string? GenerateNewCuriosity(PersonalityProfile profile, string topic)
    {
        var questions = new[]
        {
            $"What aspects of {topic} are you most interested in exploring?",
            $"What's driving your interest in {topic} right now?",
            $"Are there specific challenges with {topic} I can help with?",
            $"How does {topic} fit into what you're working on?",
            $"What would make {topic} really click for you?"
        };

        return questions[_random.Next(questions.Length)];
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _profiles.Clear();
        _feedbackHistory.Clear();
        return ValueTask.CompletedTask;
    }
}
