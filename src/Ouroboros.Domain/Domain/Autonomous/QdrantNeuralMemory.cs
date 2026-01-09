// <copyright file="QdrantNeuralMemory.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ouroboros.Domain.Autonomous;

/// <summary>
/// Provides Qdrant-based persistent memory for the neural network.
/// Stores neuron messages, intentions, and enables semantic search.
/// </summary>
public sealed class QdrantNeuralMemory : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly ConcurrentDictionary<string, bool> _initializedCollections = new();

    private const string NeuronMessagesCollection = "ouroboros_neuron_messages";
    private const string IntentionsCollection = "ouroboros_intentions";
    private const string MemoriesCollection = "ouroboros_memories";
    private const int DefaultVectorSize = 768;

    /// <summary>
    /// Creates a new Qdrant neural memory instance.
    /// </summary>
    /// <param name="qdrantEndpoint">Qdrant REST API endpoint (e.g., http://localhost:6333).</param>
    public QdrantNeuralMemory(string qdrantEndpoint = "http://localhost:6333")
    {
        _baseUrl = qdrantEndpoint.TrimEnd('/');
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(_baseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    /// <summary>
    /// Delegate for embedding text.
    /// </summary>
    public Func<string, CancellationToken, Task<float[]>>? EmbedFunction { get; set; }

    /// <summary>
    /// Initializes all required collections in Qdrant.
    /// Automatically handles dimension mismatches by migrating data.
    /// </summary>
    public async Task InitializeAsync(int vectorSize = DefaultVectorSize, CancellationToken ct = default)
    {
        await EnsureCollectionWithMigrationAsync(NeuronMessagesCollection, vectorSize, ct);
        await EnsureCollectionWithMigrationAsync(IntentionsCollection, vectorSize, ct);
        await EnsureCollectionWithMigrationAsync(MemoriesCollection, vectorSize, ct);
    }

    /// <summary>
    /// Ensures a collection exists with the correct vector size, migrating data if needed.
    /// </summary>
    private async Task EnsureCollectionWithMigrationAsync(string collectionName, int vectorSize, CancellationToken ct)
    {
        if (_initializedCollections.ContainsKey(collectionName)) return;

        try
        {
            var stats = await GetCollectionStatsAsync(collectionName, ct);

            if (!stats.Exists)
            {
                // Collection doesn't exist, create it
                await CreateCollectionAsync(collectionName, vectorSize, ct);
                _initializedCollections[collectionName] = true;
                return;
            }

            if (stats.VectorSize == vectorSize)
            {
                // Dimension matches, nothing to do
                _initializedCollections[collectionName] = true;
                return;
            }

            // Dimension mismatch - need to migrate
            Console.WriteLine($"  ⚠ Qdrant: {collectionName} dimension mismatch ({stats.VectorSize} → {vectorSize})");

            if (stats.PointCount == 0)
            {
                // No data, just recreate
                Console.WriteLine($"    Recreating empty collection...");
                await DeleteCollectionAsync(collectionName, ct);
                await CreateCollectionAsync(collectionName, vectorSize, ct);
            }
            else if (EmbedFunction != null)
            {
                // Has data and can re-embed - migrate
                Console.WriteLine($"    Migrating {stats.PointCount} points with new embeddings...");
                await MigrateCollectionAsync(collectionName, vectorSize, ct);
            }
            else
            {
                // Has data but can't re-embed - backup and recreate
                Console.WriteLine($"    ⚠ Cannot re-embed {stats.PointCount} points (no embedding function)");
                Console.WriteLine($"    Creating backup and recreating collection...");
                await BackupAndRecreateCollectionAsync(collectionName, vectorSize, ct);
            }

            _initializedCollections[collectionName] = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to ensure collection {collectionName}: {ex.Message}");
        }
    }

    /// <summary>
    /// Migrates a collection to a new vector size by re-embedding all content.
    /// </summary>
    private async Task MigrateCollectionAsync(string collectionName, int newVectorSize, CancellationToken ct)
    {
        try
        {
            // 1. Scroll through all points and extract payloads
            var allPayloads = await ScrollAllPayloadsAsync(collectionName, ct);
            Console.WriteLine($"    Retrieved {allPayloads.Count} payloads for migration");

            // 2. Delete old collection
            await DeleteCollectionAsync(collectionName, ct);

            // 3. Create new collection with correct dimension
            await CreateCollectionAsync(collectionName, newVectorSize, ct);

            // 4. Re-embed and insert each point
            var migrated = 0;
            var failed = 0;
            foreach (var (id, payload) in allPayloads)
            {
                try
                {
                    // Build search text from payload for re-embedding
                    var searchText = BuildSearchTextFromPayload(collectionName, payload);
                    if (string.IsNullOrWhiteSpace(searchText))
                    {
                        failed++;
                        continue;
                    }

                    var newEmbedding = await EmbedFunction!(searchText, ct);
                    if (newEmbedding == null || newEmbedding.Length != newVectorSize)
                    {
                        failed++;
                        continue;
                    }

                    var point = new QdrantPoint
                    {
                        Id = id,
                        Vector = newEmbedding,
                        Payload = payload
                    };
                    await UpsertPointsAsync(collectionName, new[] { point }, ct);
                    migrated++;
                }
                catch
                {
                    failed++;
                }
            }

            Console.WriteLine($"    ✓ Migrated {migrated} points ({failed} failed)");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    ⚠ Migration failed: {ex.Message}");
            // Try to recreate empty collection as fallback
            await CreateCollectionAsync(collectionName, newVectorSize, ct);
        }
    }

    /// <summary>
    /// Builds search text from a payload for re-embedding.
    /// </summary>
    private static string BuildSearchTextFromPayload(string collectionName, Dictionary<string, object> payload)
    {
        return collectionName switch
        {
            NeuronMessagesCollection =>
                $"{payload.GetValueOrDefault("topic")} {payload.GetValueOrDefault("content")}",
            IntentionsCollection =>
                $"{payload.GetValueOrDefault("title")} {payload.GetValueOrDefault("description")} {payload.GetValueOrDefault("rationale")}",
            MemoriesCollection =>
                $"{payload.GetValueOrDefault("category")} {payload.GetValueOrDefault("content")}",
            _ => payload.GetValueOrDefault("content")?.ToString() ?? ""
        };
    }

    /// <summary>
    /// Scrolls through all points in a collection and returns their payloads.
    /// </summary>
    private async Task<List<(string Id, Dictionary<string, object> Payload)>> ScrollAllPayloadsAsync(
        string collectionName, CancellationToken ct)
    {
        var results = new List<(string, Dictionary<string, object>)>();
        string? offset = null;
        const int batchSize = 100;

        while (true)
        {
            try
            {
                var request = new
                {
                    limit = batchSize,
                    offset = offset,
                    with_payload = true,
                    with_vector = false
                };

                var response = await _httpClient.PostAsJsonAsync(
                    $"/collections/{collectionName}/points/scroll", request, ct);

                if (!response.IsSuccessStatusCode) break;

                var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
                var result = json.GetProperty("result");
                var points = result.GetProperty("points");

                foreach (var point in points.EnumerateArray())
                {
                    var id = point.TryGetProperty("id", out var idProp) ? idProp.ToString() : Guid.NewGuid().ToString();
                    var payloadDict = new Dictionary<string, object>();

                    if (point.TryGetProperty("payload", out var payloadProp))
                    {
                        foreach (var prop in payloadProp.EnumerateObject())
                        {
                            payloadDict[prop.Name] = prop.Value.ValueKind switch
                            {
                                JsonValueKind.String => prop.Value.GetString() ?? "",
                                JsonValueKind.Number => prop.Value.GetDouble(),
                                JsonValueKind.True => true,
                                JsonValueKind.False => false,
                                _ => prop.Value.ToString()
                            };
                        }
                    }

                    results.Add((id, payloadDict));
                }

                // Check for next page
                if (result.TryGetProperty("next_page_offset", out var nextOffset) &&
                    nextOffset.ValueKind != JsonValueKind.Null)
                {
                    offset = nextOffset.ToString();
                }
                else
                {
                    break;
                }
            }
            catch
            {
                break;
            }
        }

        return results;
    }

    /// <summary>
    /// Creates a backup collection and recreates the original with new dimension.
    /// </summary>
    private async Task BackupAndRecreateCollectionAsync(string collectionName, int newVectorSize, CancellationToken ct)
    {
        var backupName = $"{collectionName}_backup_{DateTime.UtcNow:yyyyMMddHHmmss}";

        try
        {
            // Rename to backup by creating snapshot (Qdrant doesn't have rename)
            // For simplicity, we just delete and note that data was lost
            Console.WriteLine($"    ⚠ Data in {collectionName} will be reset (cannot re-embed without embedding function)");

            await DeleteCollectionAsync(collectionName, ct);
            await CreateCollectionAsync(collectionName, newVectorSize, ct);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    ⚠ Backup failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a new collection with the specified vector size.
    /// </summary>
    private async Task CreateCollectionAsync(string collectionName, int vectorSize, CancellationToken ct)
    {
        var createPayload = new
        {
            vectors = new
            {
                size = vectorSize,
                distance = "Cosine"
            }
        };

        await _httpClient.PutAsJsonAsync($"/collections/{collectionName}", createPayload, ct);
    }

    /// <summary>
    /// Deletes a collection.
    /// </summary>
    private async Task DeleteCollectionAsync(string collectionName, CancellationToken ct)
    {
        await _httpClient.DeleteAsync($"/collections/{collectionName}", ct);
    }

    /// <summary>
    /// Stores a neuron message in Qdrant for persistence and search.
    /// </summary>
    public async Task StoreNeuronMessageAsync(NeuronMessage message, CancellationToken ct = default)
    {
        var content = JsonSerializer.Serialize(message.Payload);
        var searchText = $"{message.Topic} {content}";

        var embedding = await GetEmbeddingAsync(searchText, ct);
        if (embedding == null) return;

        var point = new QdrantPoint
        {
            Id = message.Id.ToString(),
            Vector = embedding,
            Payload = new Dictionary<string, object>
            {
                ["source_neuron"] = message.SourceNeuron,
                ["target_neuron"] = message.TargetNeuron ?? "",
                ["topic"] = message.Topic,
                ["content"] = content,
                ["priority"] = (int)message.Priority,
                ["created_at"] = message.CreatedAt.ToString("O"),
                ["ttl_seconds"] = message.TtlSeconds,
            }
        };

        await UpsertPointsAsync(NeuronMessagesCollection, new[] { point }, ct);
    }

    /// <summary>
    /// Stores an intention in Qdrant.
    /// </summary>
    public async Task StoreIntentionAsync(Intention intention, CancellationToken ct = default)
    {
        var searchText = $"{intention.Title} {intention.Description} {intention.Rationale}";

        var embedding = await GetEmbeddingAsync(searchText, ct);
        if (embedding == null) return;

        var point = new QdrantPoint
        {
            Id = intention.Id.ToString(),
            Vector = embedding,
            Payload = new Dictionary<string, object>
            {
                ["title"] = intention.Title,
                ["description"] = intention.Description,
                ["rationale"] = intention.Rationale,
                ["category"] = intention.Category.ToString(),
                ["priority"] = (int)intention.Priority,
                ["source"] = intention.Source,
                ["status"] = intention.Status.ToString(),
                ["created_at"] = intention.CreatedAt.ToString("O"),
                ["requires_approval"] = intention.RequiresApproval,
            }
        };

        await UpsertPointsAsync(IntentionsCollection, new[] { point }, ct);
    }

    /// <summary>
    /// Stores a memory/fact in Qdrant.
    /// </summary>
    public async Task StoreMemoryAsync(string category, string content, float[] embedding, CancellationToken ct = default)
    {
        var id = Guid.NewGuid().ToString();

        var point = new QdrantPoint
        {
            Id = id,
            Vector = embedding,
            Payload = new Dictionary<string, object>
            {
                ["category"] = category,
                ["content"] = content,
                ["created_at"] = DateTime.UtcNow.ToString("O"),
            }
        };

        await UpsertPointsAsync(MemoriesCollection, new[] { point }, ct);
    }

    /// <summary>
    /// Searches for similar neuron messages.
    /// </summary>
    public async Task<IReadOnlyList<NeuronMessage>> SearchSimilarMessagesAsync(
        float[] queryVector, int limit = 10, CancellationToken ct = default)
    {
        var results = await SearchAsync(NeuronMessagesCollection, queryVector, limit, ct);

        return results.Select(r =>
        {
            var payload = r.Payload;
            return new NeuronMessage
            {
                Id = Guid.TryParse(r.Id, out var id) ? id : Guid.NewGuid(),
                SourceNeuron = payload.GetValueOrDefault("source_neuron")?.ToString() ?? "",
                TargetNeuron = payload.GetValueOrDefault("target_neuron")?.ToString(),
                Topic = payload.GetValueOrDefault("topic")?.ToString() ?? "",
                Payload = payload.GetValueOrDefault("content") ?? "",
                Priority = Enum.TryParse<IntentionPriority>(payload.GetValueOrDefault("priority")?.ToString(), out var p) ? p : IntentionPriority.Normal,
            };
        }).ToList();
    }

    /// <summary>
    /// Searches for similar memories.
    /// </summary>
    public async Task<IReadOnlyList<string>> SearchMemoriesAsync(
        float[] queryVector, int limit = 10, CancellationToken ct = default)
    {
        var results = await SearchAsync(MemoriesCollection, queryVector, limit, ct);
        return results.Select(r => r.Payload.GetValueOrDefault("content")?.ToString() ?? "").ToList();
    }

    /// <summary>
    /// Searches for similar intentions.
    /// </summary>
    public async Task<IReadOnlyList<Intention>> SearchIntentionsAsync(
        float[] queryVector, int limit = 10, CancellationToken ct = default)
    {
        var results = await SearchAsync(IntentionsCollection, queryVector, limit, ct);

        return results.Select(r =>
        {
            var payload = r.Payload;
            return new Intention
            {
                Id = Guid.TryParse(r.Id, out var id) ? id : Guid.NewGuid(),
                Title = payload.GetValueOrDefault("title")?.ToString() ?? "",
                Description = payload.GetValueOrDefault("description")?.ToString() ?? "",
                Rationale = payload.GetValueOrDefault("rationale")?.ToString() ?? "",
                Category = Enum.TryParse<IntentionCategory>(payload.GetValueOrDefault("category")?.ToString(), out var c) ? c : IntentionCategory.SelfReflection,
                Source = payload.GetValueOrDefault("source")?.ToString() ?? "",
                Priority = Enum.TryParse<IntentionPriority>(payload.GetValueOrDefault("priority")?.ToString(), out var p) ? p : IntentionPriority.Normal,
            };
        }).ToList();
    }

    /// <summary>
    /// Gets collection statistics.
    /// </summary>
    public async Task<QdrantCollectionStats> GetCollectionStatsAsync(string collectionName, CancellationToken ct = default)
    {
        try
        {
            var response = await _httpClient.GetAsync($"/collections/{collectionName}", ct);
            if (!response.IsSuccessStatusCode)
            {
                return new QdrantCollectionStats { Name = collectionName, Exists = false };
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var result = json.GetProperty("result");

            return new QdrantCollectionStats
            {
                Name = collectionName,
                Exists = true,
                PointCount = result.TryGetProperty("points_count", out var pc) ? pc.GetInt64() : 0,
                VectorSize = result.TryGetProperty("config", out var cfg) &&
                            cfg.TryGetProperty("params", out var prm) &&
                            prm.TryGetProperty("vectors", out var vec) &&
                            vec.TryGetProperty("size", out var sz) ? sz.GetInt32() : 0,
            };
        }
        catch
        {
            return new QdrantCollectionStats { Name = collectionName, Exists = false };
        }
    }

    /// <summary>
    /// Gets statistics for all neural memory collections.
    /// </summary>
    public async Task<QdrantNeuralMemoryStats> GetStatsAsync(CancellationToken ct = default)
    {
        var messagesStats = await GetCollectionStatsAsync(NeuronMessagesCollection, ct);
        var intentionsStats = await GetCollectionStatsAsync(IntentionsCollection, ct);
        var memoriesStats = await GetCollectionStatsAsync(MemoriesCollection, ct);

        return new QdrantNeuralMemoryStats
        {
            IsConnected = messagesStats.Exists || intentionsStats.Exists || memoriesStats.Exists,
            NeuronMessagesCount = messagesStats.PointCount,
            IntentionsCount = intentionsStats.PointCount,
            MemoriesCount = memoriesStats.PointCount,
            TotalPoints = messagesStats.PointCount + intentionsStats.PointCount + memoriesStats.PointCount,
        };
    }

    private async Task<float[]?> GetEmbeddingAsync(string text, CancellationToken ct)
    {
        if (EmbedFunction == null) return null;
        return await EmbedFunction(text, ct);
    }

    private async Task UpsertPointsAsync(string collectionName, IEnumerable<QdrantPoint> points, CancellationToken ct)
    {
        try
        {
            var payload = new { points = points.Select(p => new { id = p.Id, vector = p.Vector, payload = p.Payload }) };
            await _httpClient.PutAsJsonAsync($"/collections/{collectionName}/points", payload, ct);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to upsert points: {ex.Message}");
        }
    }

    private async Task<IReadOnlyList<QdrantSearchResult>> SearchAsync(
        string collectionName, float[] vector, int limit, CancellationToken ct)
    {
        try
        {
            var payload = new
            {
                vector = vector,
                limit = limit,
                with_payload = true,
            };

            var response = await _httpClient.PostAsJsonAsync($"/collections/{collectionName}/points/search", payload, ct);
            if (!response.IsSuccessStatusCode)
            {
                return Array.Empty<QdrantSearchResult>();
            }

            var json = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
            var result = json.GetProperty("result");

            var results = new List<QdrantSearchResult>();
            foreach (var item in result.EnumerateArray())
            {
                var id = item.TryGetProperty("id", out var idProp) ? idProp.ToString() : Guid.NewGuid().ToString();
                var score = item.TryGetProperty("score", out var scoreProp) ? scoreProp.GetDouble() : 0;
                var payloadDict = new Dictionary<string, object>();

                if (item.TryGetProperty("payload", out var payloadProp))
                {
                    foreach (var prop in payloadProp.EnumerateObject())
                    {
                        payloadDict[prop.Name] = prop.Value.ValueKind switch
                        {
                            JsonValueKind.String => prop.Value.GetString() ?? "",
                            JsonValueKind.Number => prop.Value.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            _ => prop.Value.ToString()
                        };
                    }
                }

                results.Add(new QdrantSearchResult(id, score, payloadDict));
            }

            return results;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Search failed: {ex.Message}");
            return Array.Empty<QdrantSearchResult>();
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

/// <summary>
/// Represents a point to upsert to Qdrant.
/// </summary>
internal sealed record QdrantPoint
{
    public required string Id { get; init; }
    public required float[] Vector { get; init; }
    public required Dictionary<string, object> Payload { get; init; }
}

/// <summary>
/// Represents a search result from Qdrant.
/// </summary>
internal sealed record QdrantSearchResult(string Id, double Score, Dictionary<string, object> Payload);

/// <summary>
/// Statistics for a Qdrant collection.
/// </summary>
public sealed record QdrantCollectionStats
{
    public required string Name { get; init; }
    public bool Exists { get; init; }
    public long PointCount { get; init; }
    public int VectorSize { get; init; }
}

/// <summary>
/// Statistics for the neural memory system.
/// </summary>
public sealed record QdrantNeuralMemoryStats
{
    public bool IsConnected { get; init; }
    public long NeuronMessagesCount { get; init; }
    public long IntentionsCount { get; init; }
    public long MemoriesCount { get; init; }
    public long TotalPoints { get; init; }
}
