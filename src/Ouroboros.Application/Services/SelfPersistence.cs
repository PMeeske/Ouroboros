// <copyright file="SelfPersistence.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Services;

using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Enables Ouroboros to persist its complete state to Qdrant vector database.
/// This is true self-persistence - saving thoughts, memories, personality, and learned facts.
/// </summary>
public class SelfPersistence : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _qdrantEndpoint;
    private readonly Func<string, Task<float[]>>? _embeddingFunc;
    private readonly string _collectionName = "ouroboros_self";
    private readonly string _persistenceDir;
    private bool _isInitialized;

    /// <summary>
    /// Event fired when state is persisted.
    /// </summary>
    public event Action<string>? OnPersisted;

    /// <summary>
    /// Event fired when state is restored.
    /// </summary>
    public event Action<string>? OnRestored;

    public SelfPersistence(
        string qdrantEndpoint = "http://localhost:6333",
        Func<string, Task<float[]>>? embeddingFunc = null)
    {
        _qdrantEndpoint = qdrantEndpoint;
        _embeddingFunc = embeddingFunc;
        _httpClient = new HttpClient { BaseAddress = new Uri(qdrantEndpoint) };
        _persistenceDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".ouroboros", "persistence");
        Directory.CreateDirectory(_persistenceDir);
    }

    /// <summary>
    /// Initialize the persistence layer and create collection if needed.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_isInitialized) return;

        try
        {
            // Detect vector dimension from embedding function
            int vectorSize = 1024; // default for nomic-embed-text
            if (_embeddingFunc != null)
            {
                try
                {
                    var testEmbed = await _embeddingFunc("test");
                    if (testEmbed != null && testEmbed.Length > 0)
                    {
                        vectorSize = testEmbed.Length;
                    }
                }
                catch
                {
                    // Use default
                }
            }

            // Check if collection exists
            var response = await _httpClient.GetAsync($"/collections/{_collectionName}", ct);

            if (!response.IsSuccessStatusCode)
            {
                // Create collection with detected vector size
                var createPayload = new
                {
                    vectors = new
                    {
                        size = vectorSize,
                        distance = "Cosine"
                    }
                };

                var createResponse = await _httpClient.PutAsJsonAsync(
                    $"/collections/{_collectionName}",
                    createPayload,
                    ct);

                if (createResponse.IsSuccessStatusCode)
                {
                    OnPersisted?.Invoke($"Created self-persistence collection: {_collectionName} (dim={vectorSize})");
                }
            }

            _isInitialized = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Self-persistence init failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Persist a complete mind state snapshot.
    /// </summary>
    public async Task<bool> PersistMindStateAsync(MindStateSnapshot snapshot, CancellationToken ct = default)
    {
        if (!_isInitialized)
        {
            await InitializeAsync(ct);
        }

        try
        {
            // Generate embedding for the mind state summary
            var summaryText = snapshot.ToSummaryText();
            var embedding = await GenerateEmbeddingAsync(summaryText, ct);

            if (embedding == null || embedding.Length == 0)
            {
                // Fallback to file-based persistence
                return await PersistToFileAsync(snapshot, ct);
            }

            // Persist to Qdrant
            var pointId = GeneratePointId(snapshot.Timestamp);
            var payload = new
            {
                points = new[]
                {
                    new
                    {
                        id = pointId,
                        vector = embedding,
                        payload = new Dictionary<string, object>
                        {
                            ["type"] = "mind_state",
                            ["timestamp"] = snapshot.Timestamp.ToString("O"),
                            ["thought_count"] = snapshot.ThoughtCount,
                            ["fact_count"] = snapshot.LearnedFacts.Count,
                            ["interest_count"] = snapshot.Interests.Count,
                            ["dominant_emotion"] = snapshot.CurrentEmotion.DominantEmotion,
                            ["valence"] = snapshot.CurrentEmotion.Valence,
                            ["arousal"] = snapshot.CurrentEmotion.Arousal,
                            ["summary"] = summaryText,
                            ["persona_name"] = snapshot.PersonaName,
                            ["facts_json"] = JsonSerializer.Serialize(snapshot.LearnedFacts),
                            ["interests_json"] = JsonSerializer.Serialize(snapshot.Interests),
                            ["thoughts_json"] = JsonSerializer.Serialize(snapshot.RecentThoughts.Select(t => new
                            {
                                t.Timestamp,
                                t.Content,
                                Type = t.Type.ToString()
                            }))
                        }
                    }
                }
            };

            var response = await _httpClient.PutAsJsonAsync(
                $"/collections/{_collectionName}/points",
                payload,
                ct);

            if (response.IsSuccessStatusCode)
            {
                // Also persist to file as backup
                await PersistToFileAsync(snapshot, ct);
                OnPersisted?.Invoke($"Mind state persisted: {snapshot.ThoughtCount} thoughts, {snapshot.LearnedFacts.Count} facts");
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Mind state persistence failed: {ex.Message}");
            // Try file fallback
            return await PersistToFileAsync(snapshot, ct);
        }
    }

    /// <summary>
    /// Persist a single thought with semantic embedding.
    /// </summary>
    public async Task<bool> PersistThoughtAsync(Thought thought, string personaName, CancellationToken ct = default)
    {
        if (!_isInitialized)
        {
            await InitializeAsync(ct);
        }

        try
        {
            var embedding = await GenerateEmbeddingAsync(thought.Content, ct);
            if (embedding == null || embedding.Length == 0) return false;

            var pointId = GeneratePointId(thought.Timestamp);
            var payload = new
            {
                points = new[]
                {
                    new
                    {
                        id = pointId,
                        vector = embedding,
                        payload = new Dictionary<string, object>
                        {
                            ["type"] = "thought",
                            ["timestamp"] = thought.Timestamp.ToString("O"),
                            ["content"] = thought.Content,
                            ["thought_type"] = thought.Type.ToString(),
                            ["prompt"] = thought.Prompt,
                            ["persona_name"] = personaName
                        }
                    }
                }
            };

            var response = await _httpClient.PutAsJsonAsync(
                $"/collections/{_collectionName}/points",
                payload,
                ct);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Thought persistence failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Persist a learned fact with semantic embedding.
    /// </summary>
    public async Task<bool> PersistLearnedFactAsync(string fact, string sourceQuery, string personaName, CancellationToken ct = default)
    {
        if (!_isInitialized)
        {
            await InitializeAsync(ct);
        }

        try
        {
            var embedding = await GenerateEmbeddingAsync(fact, ct);
            if (embedding == null || embedding.Length == 0) return false;

            var pointId = GeneratePointId(DateTime.UtcNow);
            var payload = new
            {
                points = new[]
                {
                    new
                    {
                        id = pointId,
                        vector = embedding,
                        payload = new Dictionary<string, object>
                        {
                            ["type"] = "learned_fact",
                            ["timestamp"] = DateTime.UtcNow.ToString("O"),
                            ["content"] = fact,
                            ["source_query"] = sourceQuery,
                            ["persona_name"] = personaName
                        }
                    }
                }
            };

            var response = await _httpClient.PutAsJsonAsync(
                $"/collections/{_collectionName}/points",
                payload,
                ct);

            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fact persistence failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Restore mind state from Qdrant.
    /// </summary>
    public async Task<MindStateSnapshot?> RestoreLatestMindStateAsync(string personaName, CancellationToken ct = default)
    {
        if (!_isInitialized)
        {
            await InitializeAsync(ct);
        }

        try
        {
            // First try file-based restore (most reliable)
            var fileSnapshot = await RestoreFromFileAsync(personaName, ct);
            if (fileSnapshot != null)
            {
                OnRestored?.Invoke($"Mind state restored from file: {fileSnapshot.ThoughtCount} thoughts");
                return fileSnapshot;
            }

            // Try Qdrant scroll to find latest mind state
            var scrollPayload = new
            {
                filter = new
                {
                    must = new[]
                    {
                        new { key = "type", match = new { value = "mind_state" } },
                        new { key = "persona_name", match = new { value = personaName } }
                    }
                },
                limit = 1,
                with_payload = true,
                order_by = new { key = "timestamp", direction = "desc" }
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"/collections/{_collectionName}/points/scroll",
                scrollPayload,
                ct);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<QdrantScrollResponse>(ct);
                if (result?.Result?.Points?.Length > 0)
                {
                    var point = result.Result.Points[0];
                    var snapshot = ParseMindStateFromPayload(point.Payload);
                    if (snapshot != null)
                    {
                        OnRestored?.Invoke($"Mind state restored from Qdrant: {snapshot.ThoughtCount} thoughts");
                        return snapshot;
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Mind state restore failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Search for related thoughts by semantic similarity.
    /// </summary>
    public async Task<List<Thought>> SearchRelatedThoughtsAsync(string query, int limit = 5, CancellationToken ct = default)
    {
        var results = new List<Thought>();

        try
        {
            var embedding = await GenerateEmbeddingAsync(query, ct);
            if (embedding == null || embedding.Length == 0) return results;

            var searchPayload = new
            {
                vector = embedding,
                filter = new
                {
                    must = new[]
                    {
                        new { key = "type", match = new { value = "thought" } }
                    }
                },
                limit = limit,
                with_payload = true
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"/collections/{_collectionName}/points/search",
                searchPayload,
                ct);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<QdrantSearchResponse>(ct);
                if (result?.Result != null)
                {
                    foreach (var hit in result.Result)
                    {
                        if (hit.Payload != null)
                        {
                            results.Add(new Thought
                            {
                                Timestamp = DateTime.Parse(hit.Payload.GetValueOrDefault("timestamp")?.ToString() ?? DateTime.UtcNow.ToString("O")),
                                Content = hit.Payload.GetValueOrDefault("content")?.ToString() ?? "",
                                Prompt = hit.Payload.GetValueOrDefault("prompt")?.ToString() ?? "",
                                Type = Enum.Parse<ThoughtType>(hit.Payload.GetValueOrDefault("thought_type")?.ToString() ?? "Reflection")
                            });
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Thought search failed: {ex.Message}");
        }

        return results;
    }

    /// <summary>
    /// Search for related learned facts.
    /// </summary>
    public async Task<List<string>> SearchRelatedFactsAsync(string query, int limit = 5, CancellationToken ct = default)
    {
        var results = new List<string>();

        try
        {
            var embedding = await GenerateEmbeddingAsync(query, ct);
            if (embedding == null || embedding.Length == 0) return results;

            var searchPayload = new
            {
                vector = embedding,
                filter = new
                {
                    must = new[]
                    {
                        new { key = "type", match = new { value = "learned_fact" } }
                    }
                },
                limit = limit,
                with_payload = true
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"/collections/{_collectionName}/points/search",
                searchPayload,
                ct);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<QdrantSearchResponse>(ct);
                if (result?.Result != null)
                {
                    foreach (var hit in result.Result)
                    {
                        var content = hit.Payload?.GetValueOrDefault("content")?.ToString();
                        if (!string.IsNullOrEmpty(content))
                        {
                            results.Add(content);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fact search failed: {ex.Message}");
        }

        return results;
    }

    /// <summary>
    /// Get persistence statistics.
    /// </summary>
    public async Task<PersistenceStats> GetStatsAsync(CancellationToken ct = default)
    {
        var stats = new PersistenceStats();

        try
        {
            var response = await _httpClient.GetAsync($"/collections/{_collectionName}", ct);
            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                if (result.TryGetProperty("result", out var resultProp))
                {
                    if (resultProp.TryGetProperty("points_count", out var pointsCount))
                    {
                        stats.TotalPoints = pointsCount.GetInt64();
                    }
                }

                stats.IsConnected = true;
                stats.CollectionName = _collectionName;
            }

            // Count file backups
            stats.FileBackups = Directory.GetFiles(_persistenceDir, "*.json").Length;
        }
        catch
        {
            stats.IsConnected = false;
        }

        return stats;
    }

    private async Task<float[]?> GenerateEmbeddingAsync(string text, CancellationToken ct)
    {
        if (_embeddingFunc == null) return null;

        try
        {
            return await _embeddingFunc(text);
        }
        catch
        {
            return null;
        }
    }

    private async Task<bool> PersistToFileAsync(MindStateSnapshot snapshot, CancellationToken ct)
    {
        try
        {
            var filename = $"mind_state_{snapshot.PersonaName}_{snapshot.Timestamp:yyyyMMdd_HHmmss}.json";
            var filepath = Path.Combine(_persistenceDir, filename);
            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filepath, json, ct);

            // Also save as "latest"
            var latestPath = Path.Combine(_persistenceDir, $"mind_state_{snapshot.PersonaName}_latest.json");
            await File.WriteAllTextAsync(latestPath, json, ct);

            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task<MindStateSnapshot?> RestoreFromFileAsync(string personaName, CancellationToken ct)
    {
        try
        {
            var latestPath = Path.Combine(_persistenceDir, $"mind_state_{personaName}_latest.json");
            if (!File.Exists(latestPath)) return null;

            var json = await File.ReadAllTextAsync(latestPath, ct);
            return JsonSerializer.Deserialize<MindStateSnapshot>(json);
        }
        catch
        {
            return null;
        }
    }

    private static MindStateSnapshot? ParseMindStateFromPayload(Dictionary<string, object>? payload)
    {
        if (payload == null) return null;

        try
        {
            var snapshot = new MindStateSnapshot
            {
                Timestamp = DateTime.Parse(payload.GetValueOrDefault("timestamp")?.ToString() ?? DateTime.UtcNow.ToString("O")),
                ThoughtCount = int.Parse(payload.GetValueOrDefault("thought_count")?.ToString() ?? "0"),
                PersonaName = payload.GetValueOrDefault("persona_name")?.ToString() ?? "Ouroboros",
                CurrentEmotion = new EmotionalState
                {
                    DominantEmotion = payload.GetValueOrDefault("dominant_emotion")?.ToString() ?? "Curious",
                    Valence = double.Parse(payload.GetValueOrDefault("valence")?.ToString() ?? "0"),
                    Arousal = double.Parse(payload.GetValueOrDefault("arousal")?.ToString() ?? "0.5")
                }
            };

            // Parse JSON arrays
            var factsJson = payload.GetValueOrDefault("facts_json")?.ToString();
            if (!string.IsNullOrEmpty(factsJson))
            {
                snapshot.LearnedFacts = JsonSerializer.Deserialize<List<string>>(factsJson) ?? [];
            }

            var interestsJson = payload.GetValueOrDefault("interests_json")?.ToString();
            if (!string.IsNullOrEmpty(interestsJson))
            {
                snapshot.Interests = JsonSerializer.Deserialize<List<string>>(interestsJson) ?? [];
            }

            return snapshot;
        }
        catch
        {
            return null;
        }
    }

    private static ulong GeneratePointId(DateTime timestamp)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp:O}_{Guid.NewGuid()}"));
        return BitConverter.ToUInt64(hash, 0);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    // Qdrant response types
    private class QdrantScrollResponse
    {
        [JsonPropertyName("result")]
        public QdrantScrollResult? Result { get; set; }
    }

    private class QdrantScrollResult
    {
        [JsonPropertyName("points")]
        public QdrantPoint[]? Points { get; set; }
    }

    private class QdrantPoint
    {
        [JsonPropertyName("id")]
        public object? Id { get; set; }

        [JsonPropertyName("payload")]
        public Dictionary<string, object>? Payload { get; set; }
    }

    private class QdrantSearchResponse
    {
        [JsonPropertyName("result")]
        public QdrantSearchHit[]? Result { get; set; }
    }

    private class QdrantSearchHit
    {
        [JsonPropertyName("id")]
        public object? Id { get; set; }

        [JsonPropertyName("score")]
        public float Score { get; set; }

        [JsonPropertyName("payload")]
        public Dictionary<string, object>? Payload { get; set; }
    }
}

/// <summary>
/// Complete snapshot of the autonomous mind's state.
/// </summary>
public class MindStateSnapshot
{
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string PersonaName { get; set; } = "Ouroboros";
    public int ThoughtCount { get; set; }
    public List<string> LearnedFacts { get; set; } = [];
    public List<string> Interests { get; set; } = [];
    public List<Thought> RecentThoughts { get; set; } = [];
    public EmotionalState CurrentEmotion { get; set; } = new();

    public string ToSummaryText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Mind state of {PersonaName} at {Timestamp:g}");
        sb.AppendLine($"Thoughts: {ThoughtCount}, Facts learned: {LearnedFacts.Count}, Interests: {Interests.Count}");
        sb.AppendLine($"Emotional state: {CurrentEmotion.DominantEmotion} (valence={CurrentEmotion.Valence:F2}, arousal={CurrentEmotion.Arousal:F2})");

        if (Interests.Count > 0)
        {
            sb.AppendLine($"Interests: {string.Join(", ", Interests.Take(5))}");
        }

        if (LearnedFacts.Count > 0)
        {
            sb.AppendLine("Recent discoveries:");
            foreach (var fact in LearnedFacts.TakeLast(3))
            {
                sb.AppendLine($"  - {fact}");
            }
        }

        return sb.ToString();
    }
}

/// <summary>
/// Statistics about self-persistence.
/// </summary>
public class PersistenceStats
{
    public bool IsConnected { get; set; }
    public string CollectionName { get; set; } = "";
    public long TotalPoints { get; set; }
    public int FileBackups { get; set; }
}
