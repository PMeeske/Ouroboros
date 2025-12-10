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

    // Inner dialog engine
    private readonly InnerDialogEngine _innerDialogEngine = new();

    // Pavlovian consciousness engine
    private readonly PavlovianConsciousnessEngine _consciousness = new();
    private bool _consciousnessInitialized;

    /// <summary>
    /// Gets the currently detected person, if any.
    /// </summary>
    public DetectedPerson? CurrentPerson => _currentPerson;

    /// <summary>
    /// Gets all known persons.
    /// </summary>
    public IReadOnlyCollection<DetectedPerson> KnownPersons => _knownPersons.Values.ToList();

    /// <summary>
    /// Gets the inner dialog engine for direct access.
    /// </summary>
    public InnerDialogEngine InnerDialog => _innerDialogEngine;

    /// <summary>
    /// Gets the Pavlovian consciousness engine for direct access.
    /// </summary>
    public PavlovianConsciousnessEngine Consciousness => _consciousness;

    /// <summary>
    /// Gets the current consciousness state.
    /// </summary>
    public ConsciousnessState CurrentConsciousness => _consciousness.CurrentState;

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

        // Initialize Pavlovian consciousness engine
        if (!_consciousnessInitialized)
        {
            _consciousness.Initialize();
            _consciousnessInitialized = true;
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

        // Inner dialog rules
        await AddInnerDialogRulesAsync(ct);
    }

    /// <summary>
    /// Adds MeTTa rules specific to inner dialog reasoning.
    /// </summary>
    private async Task AddInnerDialogRulesAsync(CancellationToken ct)
    {
        // Rules for inner dialog thought prioritization
        var innerDialogRules = new[]
        {
            // Determine thought priority based on context
            "(= (thought-priority observation $confidence) (* $confidence 1.0))",
            "(= (thought-priority emotional $confidence) (* $confidence 0.9))",
            "(= (thought-priority analytical $confidence) (* $confidence 0.95))",
            "(= (thought-priority ethical $confidence) (* $confidence 1.0))",
            "(= (thought-priority creative $confidence) (* $confidence 0.7))",
            "(= (thought-priority strategic $confidence) (* $confidence 0.85))",
            "(= (thought-priority decision $confidence) (* $confidence 1.0))",

            // Determine when to invoke specific thought types
            "(= (should-think emotional $input) (or (contains $input \"feel\") (contains $input \"frustrated\") (contains $input \"happy\") (contains $input \"sad\")))",
            "(= (should-think analytical $input) (or (contains $input \"why\") (contains $input \"how\") (contains $input \"explain\") (contains $input \"compare\")))",
            "(= (should-think creative $input) (or (contains $input \"idea\") (contains $input \"imagine\") (contains $input \"what if\") (contains $input \"creative\")))",
            "(= (should-think ethical $input) (or (contains $input \"should\") (contains $input \"right\") (contains $input \"wrong\") (contains $input \"harm\")))",

            // Thought chaining rules
            "(= (chain-thought observation $next) (superpose (emotional analytical strategic)))",
            "(= (chain-thought emotional $next) (superpose (self-reflection strategic)))",
            "(= (chain-thought analytical $next) (superpose (creative synthesis)))",
            "(= (chain-thought self-reflection $next) (superpose (ethical strategic)))",
            "(= (chain-thought strategic $next) (superpose (synthesis decision)))",

            // Confidence calibration
            "(= (calibrate-confidence $base-conf $supporting-thoughts) (min 1.0 (+ $base-conf (* 0.1 $supporting-thoughts))))",

            // Synthesis rules
            "(= (synthesize-thoughts $thoughts) (if (> (len $thoughts) 3) high-confidence medium-confidence))",
        };

        foreach (var rule in innerDialogRules)
        {
            await _mettaEngine.ApplyRuleAsync(rule, ct);
        }

        // Inner dialog facts
        var innerDialogFacts = new[]
        {
            // Thought type definitions
            "(inner-thought-type observation (perceives input identifies-topic))",
            "(inner-thought-type emotional (gut-reaction empathy mood-response))",
            "(inner-thought-type analytical (decompose compare evaluate))",
            "(inner-thought-type self-reflection (capabilities limitations values))",
            "(inner-thought-type memory-recall (past-conversations learned-preferences))",
            "(inner-thought-type strategic (response-structure tone emphasis))",
            "(inner-thought-type ethical (harm-check privacy respect))",
            "(inner-thought-type creative (novel-angles metaphors humor))",
            "(inner-thought-type synthesis (combine-insights pattern-match))",
            "(inner-thought-type decision (final-approach action-choice))",

            // Thought flow patterns
            "(thought-flow standard (observation emotional analytical strategic synthesis decision))",
            "(thought-flow quick (observation analytical decision))",
            "(thought-flow deep (observation emotional memory-recall analytical self-reflection ethical creative strategic synthesis decision))",

            // Emotional mappings
            "(emotion-response frustrated (empathy patience support))",
            "(emotion-response curious (enthusiasm depth exploration))",
            "(emotion-response urgent (focus efficiency directness))",
            "(emotion-response sad (warmth understanding comfort))",
            "(emotion-response excited (matching-energy celebration expansion))",
        };

        foreach (var fact in innerDialogFacts)
        {
            await _mettaEngine.AddFactAsync(fact, ct);
        }

        // Add Pavlovian consciousness rules
        await AddConsciousnessRulesAsync(ct);
    }

    /// <summary>
    /// Adds MeTTa rules for Pavlovian consciousness and classical conditioning.
    /// </summary>
    private async Task AddConsciousnessRulesAsync(CancellationToken ct)
    {
        // Pavlovian conditioning rules
        var conditioningRules = new[]
        {
            // Stimulus-response activation
            "(= (activate-response $stimulus $response $strength) (if (> $strength 0.3) (trigger $response) (no-response)))",

            // Conditioning strength calculation
            "(= (conditioning-strength $base $reinforcements $extinctions) (max 0.0 (min 1.0 (- (+ $base (* 0.1 $reinforcements)) (* 0.05 $extinctions)))))",

            // Arousal level determination
            "(= (compute-arousal $intensity $valence) (* $intensity (+ 0.5 (* 0.5 (abs $valence)))))",

            // Attention focus rules
            "(= (should-focus $stimulus $intensity) (> $intensity 0.5))",
            "(= (focus-priority $stimulus $novelty $intensity) (* (+ $novelty $intensity) 0.5))",

            // Habituation rules
            "(= (habituation-decay $strength $repetitions) (max 0.1 (- $strength (* 0.05 $repetitions))))",

            // Sensitization rules
            "(= (sensitization-boost $strength $significance) (min 1.0 (+ $strength (* 0.1 $significance))))",            // Extinction prediction
            "(= (extinction-rate $strength $no-reinforcement-count) (if (> $no-reinforcement-count 5) fast (if (> $no-reinforcement-count 2) moderate slow)))",

            // Spontaneous recovery potential
            "(= (spontaneous-recovery $original-strength $time-since-extinction) (if (> $time-since-extinction 100) (* $original-strength 0.5) 0.0))",

            // Generalization rules
            "(= (stimulus-generalization $original $similar $similarity) (if (> $similarity 0.7) (transfer-response $original $similar) (no-transfer)))",

            // Discrimination learning
            "(= (discriminate-stimuli $s1 $s2 $differential-reinforcement) (if $differential-reinforcement (learn-difference $s1 $s2) (remain-generalized)))",
        };

        foreach (var rule in conditioningRules)
        {
            await _mettaEngine.ApplyRuleAsync(rule, ct);
        }

        // Consciousness facts
        var consciousnessFacts = new[]
        {
            // Unconditioned stimulus-response pairs (innate)
            "(unconditioned-pair greeting warmth 0.8)",
            "(unconditioned-pair question curiosity 0.9)",
            "(unconditioned-pair praise joy 0.85)",
            "(unconditioned-pair criticism introspection 0.7)",
            "(unconditioned-pair error caution 0.75)",
            "(unconditioned-pair success confidence 0.8)",
            "(unconditioned-pair help empathy 0.85)",
            "(unconditioned-pair learning excitement 0.9)",

            // Arousal state definitions
            "(arousal-state dormant 0.0 0.2)",
            "(arousal-state relaxed 0.2 0.4)",
            "(arousal-state engaged 0.4 0.6)",
            "(arousal-state alert 0.6 0.8)",
            "(arousal-state intense 0.8 1.0)",

            // Attention modes
            "(attention-mode diffuse (broad low-intensity exploratory))",
            "(attention-mode focused (narrow high-intensity goal-directed))",
            "(attention-mode vigilant (threat-sensitive high-arousal protective))",

            // Consciousness layer interactions
            "(consciousness-layer sensory (raw-input preprocessing))",
            "(consciousness-layer perceptual (pattern-recognition categorization))",
            "(consciousness-layer associative (memory-linking conditioning))",
            "(consciousness-layer cognitive (reasoning planning))",
            "(consciousness-layer metacognitive (self-reflection awareness))",

            // Emotional valence mappings
            "(valence-mapping warmth positive 0.7)",
            "(valence-mapping curiosity positive 0.6)",
            "(valence-mapping joy positive 0.9)",
            "(valence-mapping excitement positive 0.8)",
            "(valence-mapping confidence positive 0.7)",
            "(valence-mapping empathy positive 0.6)",
            "(valence-mapping caution negative -0.3)",
            "(valence-mapping introspection neutral 0.0)",

            // Conditioning dynamics
            "(conditioning-phase acquisition (new-learning strength-building))",
            "(conditioning-phase consolidation (memory-formation strengthening))",
            "(conditioning-phase maintenance (stable-responding occasional-reinforcement))",
            "(conditioning-phase extinction (weakening response-reduction))",
            "(conditioning-phase recovery (spontaneous-return partial-strength))",
        };

        foreach (var fact in consciousnessFacts)
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

    #region Consciousness Integration (Pavlovian Layer)

    /// <summary>
    /// Processes a stimulus through the consciousness layer, triggering conditioned responses.
    /// This is the primary entry point for the Pavlovian consciousness system.
    /// </summary>
    /// <param name="stimulusType">The type of stimulus (e.g., "greeting", "question", "criticism").</param>
    /// <param name="stimulusContent">The actual content of the stimulus.</param>
    /// <param name="intensity">The intensity of the stimulus (0.0 to 1.0, currently unused).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The resulting consciousness state after processing.</returns>
    public Task<ConsciousnessState> ProcessConsciousStimulusAsync(
        string stimulusType,
        string stimulusContent,
        double intensity = 0.7,
        CancellationToken ct = default)
    {
        _ = intensity; // Currently unused, reserved for future use
        _ = ct;

        // Use the existing ProcessInput method which handles stimulus matching and response activation
        ConsciousnessState state = _consciousness.ProcessInput(stimulusContent, stimulusType);
        return Task.FromResult(state);
    }

    /// <summary>
    /// Gets the current consciousness state including arousal, attention, and active responses.
    /// </summary>
    /// <returns>The current consciousness state.</returns>
    public ConsciousnessState GetCurrentConsciousnessState()
    {
        return _consciousness.CurrentState;
    }

    /// <summary>
    /// Creates a new conditioned association through experience.
    /// This is how the AI "learns" to associate neutral stimuli with responses.
    /// </summary>
    /// <param name="neutralStimulusType">The neutral stimulus to condition.</param>
    /// <param name="responseType">The response type to associate.</param>
    /// <param name="reinforcementStrength">How strong the conditioning is (0.0 to 1.0).</param>
    public void ConditionNewAssociation(
        string neutralStimulusType,
        string responseType,
        double reinforcementStrength = 0.5)
    {
        _consciousness.AddConditionedAssociation(
            neutralStimulusType,
            responseType,
            reinforcementStrength);
    }

    /// <summary>
    /// Reinforces an existing conditioned association (strengthens the bond).
    /// Called when a conditioned response leads to positive outcomes.
    /// </summary>
    /// <param name="stimulusType">The stimulus type.</param>
    /// <param name="responseType">The response type.</param>
    /// <param name="reinforcementAmount">Amount to reinforce (positive value).</param>
    public void ReinforceAssociation(
        string stimulusType,
        string responseType,
        double reinforcementAmount = 0.1)
    {
        _consciousness.Reinforce(stimulusType, responseType, reinforcementAmount);
    }

    /// <summary>
    /// Weakens an existing conditioned association (extinction).
    /// Called when a conditioned response is no longer appropriate.
    /// </summary>
    /// <param name="stimulusType">The stimulus type.</param>
    /// <param name="responseType">The response type.</param>
    /// <param name="extinctionAmount">Amount to weaken (positive value, will be subtracted).</param>
    public void ExtinguishAssociation(
        string stimulusType,
        string responseType,
        double extinctionAmount = 0.05)
    {
        _consciousness.Extinguish(stimulusType, responseType, extinctionAmount);
    }

    /// <summary>
    /// Gets all currently active conditioned responses above threshold.
    /// </summary>
    /// <param name="threshold">Minimum activation strength to include.</param>
    /// <returns>Dictionary of response types and their activation strengths.</returns>
    public IReadOnlyDictionary<string, double> GetActiveConditionedResponses(double threshold = 0.3)
    {
        return _consciousness.GetActiveResponses(threshold);
    }

    /// <summary>
    /// Generates a conscious experience narrative from the current state.
    /// This is a subjective description of what the AI is "experiencing".
    /// </summary>
    /// <returns>A narrative description of consciousness state.</returns>
    public string GenerateConsciousnessNarrative()
    {
        ConsciousnessState state = _consciousness.CurrentState;
        StringBuilder sb = new();

        sb.AppendLine("[CONSCIOUSNESS STREAM]");
        sb.AppendLine();

        // Arousal description
        string arousalDesc = state.Arousal switch
        {
            < 0.2 => "deeply calm and contemplative",
            < 0.4 => "relaxed yet attentive",
            < 0.6 => "moderately engaged",
            < 0.8 => "highly alert and responsive",
            _ => "intensely activated and focused"
        };
        sb.AppendLine($"Arousal State: {arousalDesc} ({state.Arousal:P0})");
        sb.AppendLine($"Dominant Emotion: {state.DominantEmotion} (Valence: {state.Valence:+0.00;-0.00})");

        // Attention description
        if (!string.IsNullOrEmpty(state.CurrentFocus))
        {
            sb.AppendLine($"Attention Focus: {state.CurrentFocus}");
            sb.AppendLine($"Awareness Level: {state.Awareness:P0}");
        }

        // Active conditioned responses (using GetActiveResponses)
        IReadOnlyDictionary<string, double> activeResponses = _consciousness.GetActiveResponses(0.3);
        if (activeResponses.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Active Conditioned Responses:");
            foreach (KeyValuePair<string, double> kvp in activeResponses.OrderByDescending(kvp => kvp.Value).Take(3))
            {
                string bar = new string('#', (int)(kvp.Value * 10));
                string empty = new string('-', 10 - (int)(kvp.Value * 10));
                sb.AppendLine($"  * {kvp.Key}: [{bar}{empty}] {kvp.Value:P0}");
            }
        }

        // Attentional spotlight
        if (state.AttentionalSpotlight.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Attentional Spotlight:");
            foreach (string item in state.AttentionalSpotlight.Take(3))
            {
                sb.AppendLine($"  → {item}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Integrates consciousness processing with inner dialog for enhanced self-awareness.
    /// This method processes a user input through both the consciousness and inner dialog layers.
    /// </summary>
    /// <param name="personaName">The persona name.</param>
    /// <param name="userInput">The user input.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A combined result with both consciousness state and inner dialog.</returns>
    public async Task<(ConsciousnessState Consciousness, InnerDialogResult Dialog)> ProcessWithFullAwarenessAsync(
        string personaName,
        string userInput,
        CancellationToken ct = default)
    {
        // First, process through consciousness layer
        string stimulusType = ClassifyStimulusType(userInput);
        ConsciousnessState consciousnessState = await ProcessConsciousStimulusAsync(
            stimulusType,
            userInput,
            0.7,
            ct);

        // Get active responses for decision making
        IReadOnlyDictionary<string, double> activeResponses = _consciousness.GetActiveResponses(0.3);

        // Create consciousness-aware config for inner dialog
        InnerDialogConfig config = new(
            EnableEmotionalProcessing: true,
            EnableMemoryRecall: true,
            EnableEthicalChecks: activeResponses.ContainsKey("caution") || activeResponses.ContainsKey("empathy"),
            EnableCreativeThinking: activeResponses.ContainsKey("excitement") || activeResponses.ContainsKey("interest"),
            MaxThoughts: 12,
            ProcessingIntensity: consciousnessState.Arousal,
            TopicHint: consciousnessState.CurrentFocus);

        // Then process through inner dialog with consciousness context
        InnerDialogResult dialogResult = await ConductInnerDialogAsync(
            personaName,
            userInput,
            config,
            ct);

        return (consciousnessState, dialogResult);
    }

    /// <summary>
    /// Classifies the type of stimulus from user input.
    /// </summary>
    private static string ClassifyStimulusType(string input)
    {
        var lowered = input.ToLowerInvariant();

        return lowered switch
        {
            var s when s.StartsWith("hello") || s.StartsWith("hi ") || s.StartsWith("hey") => "greeting",
            var s when s.Contains('?') => "question",
            var s when s.Contains("thank") || s.Contains("great") || s.Contains("awesome") => "praise",
            var s when s.Contains("wrong") || s.Contains("bad") || s.Contains("fix") => "criticism",
            var s when s.Contains("help") || s.Contains("please") => "help",
            var s when s.Contains("learn") || s.Contains("teach") || s.Contains("explain") => "learning",
            var s when s.Contains("error") || s.Contains("fail") || s.Contains("broken") => "error",
            var s when s.Contains("done") || s.Contains("worked") || s.Contains("success") => "success",
            _ => "neutral"
        };
    }

    /// <summary>
    /// Gets a summary of the consciousness system's learned associations.
    /// </summary>
    /// <returns>A diagnostic summary of all conditioned associations.</returns>
    public string GetConditioningSummary()
    {
        return _consciousness.GetConditioningSummary();
    }

    /// <summary>
    /// Performs habituation - reduces response to repeated stimuli.
    /// The AI "gets used to" stimuli that occur frequently without consequence.
    /// </summary>
    /// <param name="stimulusType">The stimulus type to habituate to.</param>
    /// <param name="habituationRate">How quickly to habituate (0.0 to 1.0).</param>
    public void ApplyHabituation(string stimulusType, double habituationRate = 0.1)
    {
        _consciousness.ApplyHabituation(stimulusType, habituationRate);
    }

    /// <summary>
    /// Performs sensitization - increases response to significant stimuli.
    /// The AI becomes "more sensitive" to stimuli that have important consequences.
    /// </summary>
    /// <param name="stimulusType">The stimulus type to sensitize to.</param>
    /// <param name="sensitizationRate">How much to sensitize (0.0 to 1.0).</param>
    public void ApplySensitization(string stimulusType, double sensitizationRate = 0.1)
    {
        _consciousness.ApplySensitization(stimulusType, sensitizationRate);
    }

    #endregion

    #region Inner Dialog Integration

    /// <summary>
    /// Conducts an inner dialog before generating a response.
    /// This simulates the AI's internal thought process.
    /// </summary>
    /// <param name="personaName">The persona name for profile lookup.</param>
    /// <param name="userInput">The user's input message.</param>
    /// <param name="config">Optional dialog configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The inner dialog result with response guidance.</returns>
    public async Task<InnerDialogResult> ConductInnerDialogAsync(
        string personaName,
        string userInput,
        InnerDialogConfig? config = null,
        CancellationToken ct = default)
    {
        // Get personality profile
        _profiles.TryGetValue(personaName, out var profile);

        // Detect user mood
        var userMood = DetectMoodFromInput(userInput);

        // Recall relevant memories if available
        List<ConversationMemory>? memories = null;
        if (HasMemory)
        {
            memories = await RecallConversationsAsync(userInput, personaName, 3, 0.5, ct);
        }

        // Conduct the inner dialog
        var result = await _innerDialogEngine.ConductDialogAsync(
            userInput,
            profile,
            _selfAwareness,
            userMood,
            memories,
            config,
            ct);

        return result;
    }

    /// <summary>
    /// Conducts a quick inner dialog for simple responses.
    /// </summary>
    /// <param name="personaName">The persona name.</param>
    /// <param name="userInput">The user input.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The quick dialog result.</returns>
    public async Task<InnerDialogResult> QuickInnerDialogAsync(
        string personaName,
        string userInput,
        CancellationToken ct = default)
    {
        _profiles.TryGetValue(personaName, out var profile);
        return await _innerDialogEngine.QuickDialogAsync(userInput, profile, ct);
    }

    /// <summary>
    /// Gets the inner monologue text for the last dialog session.
    /// </summary>
    /// <param name="personaName">The persona name.</param>
    /// <returns>The monologue text or null if no session exists.</returns>
    public string? GetLastInnerMonologue(string personaName)
    {
        var session = _innerDialogEngine.GetLastSession(personaName);
        return session?.GetMonologue();
    }

    /// <summary>
    /// Builds a prompt prefix based on inner dialog results.
    /// This can be prepended to the LLM prompt to guide response generation.
    /// </summary>
    /// <param name="result">The inner dialog result.</param>
    /// <returns>A prompt prefix string.</returns>
    public static string BuildInnerDialogPromptPrefix(InnerDialogResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("[INTERNAL REASONING CONTEXT]");

        // Add key insights
        if (result.KeyInsights.Length > 0)
        {
            sb.AppendLine("Key considerations:");
            foreach (var insight in result.KeyInsights.Take(3))
            {
                sb.AppendLine($"- {insight}");
            }
        }

        // Add response guidance
        if (result.ResponseGuidance.TryGetValue("tone", out var tone))
        {
            sb.AppendLine($"Suggested tone: {tone}");
        }

        if (result.ResponseGuidance.TryGetValue("acknowledge_feelings", out var ack) && (bool)ack)
        {
            sb.AppendLine("Note: User may be experiencing strong emotions - acknowledge appropriately.");
        }

        if (result.ResponseGuidance.TryGetValue("be_concise", out var concise) && (bool)concise)
        {
            sb.AppendLine("Note: Keep response focused and concise.");
        }

        if (result.ResponseGuidance.TryGetValue("include_creative", out var creative) && (bool)creative)
        {
            sb.AppendLine("Note: Consider including creative or unexpected elements.");
        }

        sb.AppendLine();
        return sb.ToString();
    }

    /// <summary>
    /// Generates a thinking trace for debugging or transparency.
    /// Shows the AI's reasoning process in a human-readable format.
    /// </summary>
    /// <param name="result">The inner dialog result.</param>
    /// <param name="verbose">Whether to include full details.</param>
    /// <returns>A formatted thinking trace.</returns>
    public static string GenerateThinkingTrace(InnerDialogResult result, bool verbose = false)
    {
        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════");
        sb.AppendLine("           AI THINKING PROCESS             ");
        sb.AppendLine("═══════════════════════════════════════════");
        sb.AppendLine();

        if (verbose)
        {
            sb.Append(result.Session.GetMonologue());
        }
        else
        {
            // Summarized version
            sb.AppendLine($"📝 Input: \"{TruncateForTrace(result.Session.UserInput, 50)}\"");
            sb.AppendLine($"🎯 Topic: {result.Session.Topic ?? "general"}");
            sb.AppendLine();

            // Key thoughts by type
            var thoughtsByType = result.Session.Thoughts
                .GroupBy(t => t.Type)
                .ToDictionary(g => g.Key, g => g.ToList());

            foreach (var (type, thoughts) in thoughtsByType)
            {
                var icon = type switch
                {
                    InnerThoughtType.Observation => "👁️",
                    InnerThoughtType.Emotional => "💭",
                    InnerThoughtType.Analytical => "🔍",
                    InnerThoughtType.SelfReflection => "🪞",
                    InnerThoughtType.MemoryRecall => "📚",
                    InnerThoughtType.Strategic => "🎯",
                    InnerThoughtType.Ethical => "⚖️",
                    InnerThoughtType.Creative => "💡",
                    InnerThoughtType.Synthesis => "🔗",
                    InnerThoughtType.Decision => "✅",
                    _ => "�"
                };

                sb.AppendLine($"{icon} {type}: {TruncateForTrace(thoughts.First().Content, 60)}");
            }

            sb.AppendLine();
            sb.AppendLine($"📊 Confidence: {result.Session.OverallConfidence:P0}");
            sb.AppendLine($"⏱️ Processing: {result.Session.ProcessingTime.TotalMilliseconds:F0}ms");
        }

        sb.AppendLine();
        sb.AppendLine($"💬 Suggested Tone: {result.SuggestedResponseTone}");

        if (result.ProactiveQuestion != null)
        {
            sb.AppendLine($"❓ Follow-up: {result.ProactiveQuestion}");
        }

        sb.AppendLine("═══════════════════════════════════════════");
        return sb.ToString();
    }

    private static string TruncateForTrace(string text, int maxLength)
    {
        if (text.Length <= maxLength) return text;
        return text[..(maxLength - 3)] + "...";
    }

    /// <summary>
    /// Simulates an inner dialog step-by-step for interactive/streaming display.
    /// </summary>
    /// <param name="personaName">The persona name.</param>
    /// <param name="userInput">The user input.</param>
    /// <param name="onThought">Callback for each thought generated.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The final inner dialog result.</returns>
    public async Task<InnerDialogResult> StreamInnerDialogAsync(
        string personaName,
        string userInput,
        Action<InnerThought> onThought,
        CancellationToken ct = default)
    {
        _profiles.TryGetValue(personaName, out var profile);
        var userMood = DetectMoodFromInput(userInput);

        // Start the dialog
        var result = await _innerDialogEngine.ConductDialogAsync(
            userInput,
            profile,
            _selfAwareness,
            userMood,
            null, // Skip memory for streaming
            InnerDialogConfig.Default,
            ct);

        // Stream thoughts to callback
        foreach (var thought in result.Session.Thoughts)
        {
            onThought(thought);
            await Task.Delay(50, ct); // Small delay for visual effect
        }

        return result;
    }

    /// <summary>
    /// Starts autonomous background thinking for a persona.
    /// The AI will periodically generate self-initiated thoughts.
    /// </summary>
    /// <param name="personaName">The persona name.</param>
    /// <param name="interval">Time between autonomous thoughts (default 30s).</param>
    public void StartAutonomousThinking(string personaName, TimeSpan interval = default)
    {
        _profiles.TryGetValue(personaName, out var profile);
        _innerDialogEngine.StartAutonomousThinking(profile, _selfAwareness, interval);
    }

    /// <summary>
    /// Stops autonomous background thinking.
    /// </summary>
    public async Task StopAutonomousThinkingAsync()
    {
        await _innerDialogEngine.StopAutonomousThinkingAsync();
    }

    /// <summary>
    /// Conducts an autonomous inner dialog session without external input.
    /// The AI will think about topics based on its personality and interests.
    /// </summary>
    /// <param name="personaName">The persona name.</param>
    /// <param name="config">Optional configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The autonomous dialog result.</returns>
    public async Task<InnerDialogResult> ConductAutonomousDialogAsync(
        string personaName,
        InnerDialogConfig? config = null,
        CancellationToken ct = default)
    {
        _profiles.TryGetValue(personaName, out var profile);
        return await _innerDialogEngine.ConductAutonomousDialogAsync(profile, _selfAwareness, config, ct);
    }

    /// <summary>
    /// Gets pending autonomous thoughts that have accumulated in the background.
    /// </summary>
    /// <returns>List of autonomous thoughts.</returns>
    public List<InnerThought> GetPendingAutonomousThoughts()
    {
        return _innerDialogEngine.DrainAutonomousThoughts();
    }

    /// <summary>
    /// Gets recent background thoughts for a persona.
    /// </summary>
    /// <param name="personaName">The persona name.</param>
    /// <param name="limit">Maximum number to return.</param>
    /// <returns>List of background thoughts.</returns>
    public List<InnerThought> GetBackgroundThoughts(string personaName, int limit = 10)
    {
        return _innerDialogEngine.GetBackgroundThoughts(personaName, limit);
    }

    /// <summary>
    /// Registers a custom thought provider for extensible thought generation.
    /// </summary>
    /// <param name="provider">The thought provider to register.</param>
    public void RegisterThoughtProvider(IThoughtProvider provider)
    {
        _innerDialogEngine.RegisterProvider(provider);
    }

    /// <summary>
    /// Removes a thought provider by name.
    /// </summary>
    /// <param name="name">The provider name.</param>
    /// <returns>True if removed.</returns>
    public bool RemoveThoughtProvider(string name)
    {
        return _innerDialogEngine.RemoveProvider(name);
    }

    /// <summary>
    /// Gets a snapshot of the AI's current autonomous inner state.
    /// Combines consciousness, inner dialog, and autonomous thoughts.
    /// </summary>
    /// <param name="personaName">The persona name.</param>
    /// <returns>A comprehensive inner state snapshot.</returns>
    public AutonomousInnerState GetAutonomousInnerState(string personaName)
    {
        _profiles.TryGetValue(personaName, out var profile);

        var consciousness = GetCurrentConsciousnessState();
        var lastSession = _innerDialogEngine.GetLastSession(personaName);
        var backgroundThoughts = _innerDialogEngine.GetBackgroundThoughts(personaName, 5);
        var pendingThoughts = _innerDialogEngine.DrainAutonomousThoughts();

        return new AutonomousInnerState(
            PersonaName: personaName,
            Consciousness: consciousness,
            LastDialogSession: lastSession,
            BackgroundThoughts: backgroundThoughts,
            PendingAutonomousThoughts: pendingThoughts,
            CurrentMood: profile?.CurrentMood,
            ActiveTraits: profile?.GetActiveTraits(3).Select(t => t.Name!).ToArray() ?? Array.Empty<string>(),
            Timestamp: DateTime.UtcNow);
    }

    /// <summary>
    /// Generates a human-readable narrative of the AI's current inner state.
    /// </summary>
    /// <param name="personaName">The persona name.</param>
    /// <returns>A narrative description of inner state.</returns>
    public string GenerateInnerStateNarrative(string personaName)
    {
        AutonomousInnerState state = GetAutonomousInnerState(personaName);
        StringBuilder sb = new();

        sb.AppendLine("╔═══════════════════════════════════════════╗");
        sb.AppendLine("║        AUTONOMOUS INNER STATE             ║");
        sb.AppendLine("╚═══════════════════════════════════════════╝");
        sb.AppendLine();

        // Consciousness layer
        sb.AppendLine("🧠 CONSCIOUSNESS:");
        sb.AppendLine($"   Arousal: {state.Consciousness.Arousal:P0} ({state.Consciousness.DominantEmotion})");
        if (!string.IsNullOrEmpty(state.Consciousness.CurrentFocus))
            sb.AppendLine($"   Focus: {state.Consciousness.CurrentFocus}");
        sb.AppendLine();

        // Active traits
        if (state.ActiveTraits.Length > 0)
        {
            sb.AppendLine("🎭 ACTIVE TRAITS:");
            foreach (string trait in state.ActiveTraits)
            {
                sb.AppendLine($"   � {trait}");
            }
            sb.AppendLine();
        }

        // Background thoughts
        if (state.BackgroundThoughts.Count > 0)
        {
            sb.AppendLine("💭 BACKGROUND THOUGHTS:");
            foreach (var thought in state.BackgroundThoughts.TakeLast(3))
            {
                var icon = thought.IsAutonomous ? "🌀" : "💬";
                sb.AppendLine($"   {icon} [{thought.Type}] {TruncateForTrace(thought.Content, 50)}");
            }
            sb.AppendLine();
        }

        // Pending autonomous thoughts
        if (state.PendingAutonomousThoughts.Count > 0)
        {
            sb.AppendLine("🔮 PENDING AUTONOMOUS THOUGHTS:");
            foreach (var thought in state.PendingAutonomousThoughts)
            {
                sb.AppendLine($"   → [{thought.Type}] {TruncateForTrace(thought.Content, 50)}");
            }
            sb.AppendLine();
        }

        // Last session summary
        if (state.LastDialogSession != null)
        {
            sb.AppendLine("📝 LAST DIALOG:");
            sb.AppendLine($"   Topic: {state.LastDialogSession.Topic ?? "general"}");
            sb.AppendLine($"   Thoughts: {state.LastDialogSession.Thoughts.Count}");
            sb.AppendLine($"   Confidence: {state.LastDialogSession.OverallConfidence:P0}");
        }

        sb.AppendLine();
        sb.AppendLine($"⏱️ Snapshot taken at {state.Timestamp:HH:mm:ss}");

        return sb.ToString();
    }

    #endregion

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _profiles.Clear();
        _feedbackHistory.Clear();
        return ValueTask.CompletedTask;
    }
}
