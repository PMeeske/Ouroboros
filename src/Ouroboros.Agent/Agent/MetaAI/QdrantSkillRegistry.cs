// <copyright file="QdrantSkillRegistry.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Qdrant-backed Skill Registry Implementation
// Stores skills in Qdrant vector database for persistence
// and semantic search capabilities
// ==========================================================

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Configuration for Qdrant skill storage.
/// </summary>
public sealed record QdrantSkillConfig(
    string ConnectionString = "http://localhost:6334",
    string CollectionName = "ouroboros_skills",
    bool AutoSave = true,
    int VectorSize = 1536);

/// <summary>
/// Qdrant-backed implementation of skill registry.
/// Stores skills directly in Qdrant for persistence and semantic search.
/// </summary>
public sealed class QdrantSkillRegistry : ISkillRegistry, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, Skill> _skillsCache = new();
    private readonly IEmbeddingModel? _embedding;
    private readonly QdrantClient _client;
    private readonly QdrantSkillConfig _config;
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private bool _isInitialized;
    private readonly bool _disposeClient;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public QdrantSkillRegistry(
        IEmbeddingModel? embedding = null,
        QdrantSkillConfig? config = null)
    {
        _embedding = embedding;
        _config = config ?? new QdrantSkillConfig();

        // Parse connection string to extract host and port
        var uri = new Uri(_config.ConnectionString);
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 6334; // Default to gRPC port
        var useHttps = uri.Scheme == "https";

        _client = new QdrantClient(host, port, useHttps);
        _disposeClient = true;
    }

    public QdrantSkillRegistry(
        QdrantClient client,
        IEmbeddingModel? embedding = null,
        QdrantSkillConfig? config = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _embedding = embedding;
        _config = config ?? new QdrantSkillConfig();
        _disposeClient = false;
    }

    /// <summary>
    /// Initializes the registry by loading persisted skills from Qdrant.
    /// Call this before using the registry.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_isInitialized) return;

        await EnsureCollectionExistsAsync(ct);
        await LoadSkillsFromQdrantAsync(ct);
        _isInitialized = true;
    }

    /// <summary>
    /// Registers a new skill and persists it to Qdrant.
    /// </summary>
    public void RegisterSkill(Skill skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        _skillsCache[skill.Name] = skill;

        if (_config.AutoSave)
        {
            // Fire and forget save (async in background)
            _ = SaveSkillToQdrantAsync(skill);
        }
    }

    /// <summary>
    /// Registers a skill asynchronously with immediate persistence.
    /// </summary>
    public async Task RegisterSkillAsync(Skill skill, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(skill);
        _skillsCache[skill.Name] = skill;
        await SaveSkillToQdrantAsync(skill, ct);
    }

    /// <summary>
    /// Minimum similarity score threshold for semantic skill matching.
    /// Skills with similarity below this value are considered unrelated.
    /// Qdrant scores typically range from 0 to 1 for cosine similarity.
    /// A threshold of 0.75+ ensures strong semantic match, not just topical relatedness.
    /// </summary>
    private const float MinimumSimilarityThreshold = 0.75f;

    /// <summary>
    /// Finds skills matching a goal using semantic similarity.
    /// </summary>
    public async Task<List<Skill>> FindMatchingSkillsAsync(
        string goal,
        Dictionary<string, object>? context = null)
    {
        if (string.IsNullOrWhiteSpace(goal))
            return new List<Skill>();

        // Use semantic search if embedding model is available
        if (_embedding != null)
        {
            try
            {
                float[] goalEmbedding = await _embedding.CreateEmbeddingsAsync(goal);

                var collectionExists = await _client.CollectionExistsAsync(_config.CollectionName);
                if (collectionExists)
                {
                    var searchResults = await _client.SearchAsync(
                        _config.CollectionName,
                        goalEmbedding,
                        limit: 10,
                        scoreThreshold: MinimumSimilarityThreshold);

                    var matchedSkills = new List<Skill>();
                    foreach (var result in searchResults)
                    {
                        if (result.Payload.TryGetValue("skill_name", out var nameValue) &&
                            _skillsCache.TryGetValue(nameValue.StringValue, out var skill))
                        {
                            matchedSkills.Add(skill);
                        }
                    }

                    if (matchedSkills.Count > 0)
                        return matchedSkills;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[WARN] Qdrant semantic search failed: {ex.Message}");
                // Fall back to keyword matching
            }
        }

        // Fallback: keyword matching on cached skills
        string goalLower = goal.ToLowerInvariant();
        return _skillsCache.Values
            .Where(s => s.Description.ToLowerInvariant().Contains(goalLower) ||
                       goalLower.Contains(s.Name.ToLowerInvariant()) ||
                       s.Prerequisites.Any(p => p.ToLowerInvariant().Contains(goalLower)))
            .OrderByDescending(s => s.SuccessRate)
            .ThenByDescending(s => s.UsageCount)
            .ToList();
    }

    /// <summary>
    /// Gets a skill by name.
    /// </summary>
    public Skill? GetSkill(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        _skillsCache.TryGetValue(name, out Skill? skill);
        return skill;
    }

    /// <summary>
    /// Records skill execution outcome and updates Qdrant.
    /// </summary>
    public void RecordSkillExecution(string name, bool success)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        _skillsCache.AddOrUpdate(
            name,
            _ => throw new InvalidOperationException($"Skill '{name}' not found"),
            (_, existing) =>
            {
                int newCount = existing.UsageCount + 1;
                double newSuccessRate = ((existing.SuccessRate * existing.UsageCount) + (success ? 1.0 : 0.0)) / newCount;

                return existing with
                {
                    UsageCount = newCount,
                    SuccessRate = newSuccessRate,
                    LastUsed = DateTime.UtcNow
                };
            });

        if (_config.AutoSave && _skillsCache.TryGetValue(name, out var updatedSkill))
        {
            _ = SaveSkillToQdrantAsync(updatedSkill);
        }
    }

    /// <summary>
    /// Gets all registered skills.
    /// </summary>
    public IReadOnlyList<Skill> GetAllSkills()
        => _skillsCache.Values.OrderByDescending(s => s.SuccessRate).ToList();

    /// <summary>
    /// Extracts and registers a skill from a successful execution.
    /// </summary>
    public async Task<Result<Skill, string>> ExtractSkillAsync(
        ExecutionResult execution,
        string skillName,
        string description)
    {
        if (execution == null)
            return Result<Skill, string>.Failure("Execution cannot be null");

        if (!execution.Success)
            return Result<Skill, string>.Failure("Cannot extract skill from failed execution");

        if (string.IsNullOrWhiteSpace(skillName))
            return Result<Skill, string>.Failure("Skill name cannot be empty");

        try
        {
            List<string> prerequisites = execution.Plan.Steps
                .Where(s => s.ConfidenceScore > 0.7)
                .Select(s => s.Action)
                .Distinct()
                .ToList();

            Skill skill = new Skill(
                skillName,
                description,
                prerequisites,
                execution.Plan.Steps,
                SuccessRate: 1.0,
                UsageCount: 0,
                CreatedAt: DateTime.UtcNow,
                LastUsed: DateTime.UtcNow);

            await RegisterSkillAsync(skill);

            return Result<Skill, string>.Success(skill);
        }
        catch (Exception ex)
        {
            return Result<Skill, string>.Failure($"Failed to extract skill: {ex.Message}");
        }
    }

    /// <summary>
    /// Deletes a skill by name from both cache and Qdrant.
    /// </summary>
    public async Task DeleteSkillAsync(string name, CancellationToken ct = default)
    {
        if (_skillsCache.TryRemove(name, out _))
        {
            try
            {
                var collectionExists = await _client.CollectionExistsAsync(_config.CollectionName, ct);
                if (collectionExists)
                {
                    // Delete by filter on skill_name
                    await _client.DeleteAsync(
                        _config.CollectionName,
                        new Filter
                        {
                            Must =
                            {
                                new Condition
                                {
                                    Field = new FieldCondition
                                    {
                                        Key = "skill_name",
                                        Match = new Match { Keyword = name }
                                    }
                                }
                            }
                        },
                        cancellationToken: ct);
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[WARN] Failed to delete skill '{name}' from Qdrant: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Gets statistics about the skill registry.
    /// </summary>
    public QdrantSkillRegistryStats GetStats()
    {
        var skills = _skillsCache.Values.ToList();
        return new QdrantSkillRegistryStats(
            TotalSkills: skills.Count,
            AverageSuccessRate: skills.Count > 0 ? skills.Average(s => s.SuccessRate) : 0,
            TotalExecutions: skills.Sum(s => s.UsageCount),
            MostUsedSkill: skills.OrderByDescending(s => s.UsageCount).FirstOrDefault()?.Name,
            MostSuccessfulSkill: skills.OrderByDescending(s => s.SuccessRate).FirstOrDefault()?.Name,
            ConnectionString: _config.ConnectionString,
            CollectionName: _config.CollectionName,
            IsConnected: _isInitialized);
    }

    private int? _detectedVectorSize;

    private async Task EnsureCollectionExistsAsync(CancellationToken ct = default)
    {
        try
        {
            var collectionExists = await _client.CollectionExistsAsync(_config.CollectionName, ct);
            if (!collectionExists)
            {
                // Detect vector size from embedding model if available
                int vectorSize = _config.VectorSize;
                if (_embedding != null)
                {
                    try
                    {
                        var sampleEmbedding = await _embedding.CreateEmbeddingsAsync("sample text for dimension detection", ct);
                        vectorSize = sampleEmbedding.Length;
                        _detectedVectorSize = vectorSize;
                        Console.WriteLine($"[qdrant] Detected embedding dimension: {vectorSize}");
                    }
                    catch
                    {
                        // Use default if detection fails
                    }
                }

                await _client.CreateCollectionAsync(
                    _config.CollectionName,
                    new VectorParams
                    {
                        Size = (ulong)vectorSize,
                        Distance = Distance.Cosine
                    },
                    cancellationToken: ct);

                Console.WriteLine($"[qdrant] Created skills collection: {_config.CollectionName}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] Failed to ensure Qdrant collection: {ex.Message}");
        }
    }

    private async Task SaveSkillToQdrantAsync(Skill skill, CancellationToken ct = default)
    {
        await _syncLock.WaitAsync(ct);
        try
        {
            // Create searchable text from skill
            string searchText = $"{skill.Name}: {skill.Description}. Prerequisites: {string.Join(", ", skill.Prerequisites)}. Steps: {string.Join(", ", skill.Steps.Select(s => s.Action))}";

            // Generate embedding
            float[] embedding;
            if (_embedding != null)
            {
                embedding = await _embedding.CreateEmbeddingsAsync(searchText, ct);
            }
            else
            {
                // Use a simple hash-based "embedding" as fallback (not for semantic search)
                int vectorSize = _detectedVectorSize ?? _config.VectorSize;
                embedding = GenerateFallbackEmbedding(searchText, vectorSize);
            }

            // Serialize skill data for storage
            var skillJson = JsonSerializer.Serialize(new SerializableSkillData(
                skill.Name,
                skill.Description,
                skill.Prerequisites,
                skill.Steps.Select(s => new SerializableStepData(s.Action, s.ExpectedOutcome, s.ConfidenceScore)).ToList(),
                skill.SuccessRate,
                skill.UsageCount,
                skill.CreatedAt,
                skill.LastUsed
            ), JsonOptions);

            var point = new PointStruct
            {
                Id = new PointId { Uuid = GenerateSkillId(skill.Name) },
                Vectors = embedding,
                Payload =
                {
                    ["skill_name"] = skill.Name,
                    ["description"] = skill.Description,
                    ["success_rate"] = skill.SuccessRate,
                    ["usage_count"] = skill.UsageCount,
                    ["created_at"] = skill.CreatedAt.ToString("O"),
                    ["last_used"] = skill.LastUsed.ToString("O"),
                    ["skill_data"] = skillJson,
                    ["type"] = "skill"
                }
            };

            await _client.UpsertAsync(_config.CollectionName, new[] { point }, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] Failed to save skill '{skill.Name}' to Qdrant: {ex.Message}");
        }
        finally
        {
            _syncLock.Release();
        }
    }

    private async Task LoadSkillsFromQdrantAsync(CancellationToken ct = default)
    {
        try
        {
            var collectionExists = await _client.CollectionExistsAsync(_config.CollectionName, ct);
            if (!collectionExists)
            {
                return;
            }

            // Scroll through all points in the collection using the proper API
            var scrollResult = await _client.ScrollAsync(
                _config.CollectionName,
                payloadSelector: true,
                limit: 1000,
                cancellationToken: ct);

            foreach (var point in scrollResult.Result)
            {
                try
                {
                    if (point.Payload.TryGetValue("skill_data", out var skillDataValue))
                    {
                        var skillData = JsonSerializer.Deserialize<SerializableSkillData>(
                            skillDataValue.StringValue, JsonOptions);

                        if (skillData != null)
                        {
                            var skill = new Skill(
                                skillData.Name,
                                skillData.Description,
                                skillData.Prerequisites,
                                skillData.Steps.Select(s => new PlanStep(
                                    s.Action,
                                    new Dictionary<string, object>(),
                                    s.ExpectedOutcome,
                                    s.ConfidenceScore)).ToList(),
                                skillData.SuccessRate,
                                skillData.UsageCount,
                                skillData.CreatedAt,
                                skillData.LastUsed);

                            _skillsCache[skill.Name] = skill;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[WARN] Failed to deserialize skill from Qdrant: {ex.Message}");
                }
            }

            if (_skillsCache.Count > 0)
            {
                Console.WriteLine($"[qdrant] Loaded {_skillsCache.Count} skills from {_config.CollectionName}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] Failed to load skills from Qdrant: {ex.Message}");
        }
    }

    private static string GenerateSkillId(string skillName)
    {
        // Create a deterministic UUID from the skill name
        using var md5 = System.Security.Cryptography.MD5.Create();
        var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes($"skill_{skillName}"));
        return new Guid(hash).ToString();
    }

    private static float[] GenerateFallbackEmbedding(string text, int size)
    {
        // Generate a simple hash-based vector as fallback when no embedding model is available
        // This is NOT for semantic search - just for storage
        var embedding = new float[size];
        var hash = text.GetHashCode();
        var random = new Random(hash);
        for (int i = 0; i < size; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1); // -1 to 1
        }
        // Normalize
        var magnitude = (float)Math.Sqrt(embedding.Sum(x => x * x));
        if (magnitude > 0)
        {
            for (int i = 0; i < size; i++)
                embedding[i] /= magnitude;
        }
        return embedding;
    }

    public async ValueTask DisposeAsync()
    {
        _syncLock.Dispose();
        if (_disposeClient)
        {
            _client.Dispose();
        }
        await Task.CompletedTask;
    }
}

/// <summary>
/// Serializable skill data for Qdrant storage.
/// </summary>
internal sealed record SerializableSkillData(
    string Name,
    string Description,
    List<string> Prerequisites,
    List<SerializableStepData> Steps,
    double SuccessRate,
    int UsageCount,
    DateTime CreatedAt,
    DateTime LastUsed);

/// <summary>
/// Serializable step data for Qdrant storage.
/// </summary>
internal sealed record SerializableStepData(
    string Action,
    string ExpectedOutcome,
    double ConfidenceScore);

/// <summary>
/// Statistics about the Qdrant skill registry.
/// </summary>
public sealed record QdrantSkillRegistryStats(
    int TotalSkills,
    double AverageSuccessRate,
    int TotalExecutions,
    string? MostUsedSkill,
    string? MostSuccessfulSkill,
    string ConnectionString,
    string CollectionName,
    bool IsConnected);
