#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Persistent Skill Registry Implementation
// Stores skills to disk/vector store for persistence across restarts
// ==========================================================

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;
using LangChain.Databases;
using LangChain.DocumentLoaders;

namespace LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Configuration for persistent skill storage.
/// </summary>
public sealed record PersistentSkillConfig(
    string StoragePath = "skills.json",
    bool UseVectorStore = true,
    string CollectionName = "ouroboros_skills",
    bool AutoSave = true);

/// <summary>
/// Serializable skill format for JSON persistence.
/// </summary>
internal sealed record SerializableSkill(
    string Name,
    string Description,
    List<string> Prerequisites,
    List<SerializablePlanStep> Steps,
    double SuccessRate,
    int UsageCount,
    DateTime CreatedAt,
    DateTime LastUsed);

/// <summary>
/// Serializable plan step for JSON persistence.
/// </summary>
internal sealed record SerializablePlanStep(
    string Action,
    Dictionary<string, object> Parameters,
    string ExpectedOutcome,
    double ConfidenceScore);

/// <summary>
/// Persistent implementation of skill registry that saves skills to disk and optionally to a vector store.
/// Enables skill persistence across application restarts and semantic skill search via embeddings.
/// </summary>
public sealed class PersistentSkillRegistry : ISkillRegistry, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, Skill> _skills = new();
    private readonly IEmbeddingModel? _embedding;
    private readonly TrackedVectorStore? _vectorStore;
    private readonly PersistentSkillConfig _config;
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private bool _isDirty;
    private bool _isInitialized;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public PersistentSkillRegistry(
        IEmbeddingModel? embedding = null,
        TrackedVectorStore? vectorStore = null,
        PersistentSkillConfig? config = null)
    {
        _embedding = embedding;
        _vectorStore = vectorStore;
        _config = config ?? new PersistentSkillConfig();
    }

    /// <summary>
    /// Initializes the registry by loading persisted skills.
    /// Call this before using the registry.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_isInitialized) return;

        await LoadSkillsAsync(ct);
        _isInitialized = true;
    }

    /// <summary>
    /// Registers a new skill and persists it.
    /// </summary>
    public void RegisterSkill(Skill skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        _skills[skill.Name] = skill;
        _isDirty = true;

        if (_config.AutoSave)
        {
            // Fire and forget save (async in background)
            _ = SaveSkillsAsync();
        }

        // Add to vector store for semantic search
        if (_embedding != null && _vectorStore != null)
        {
            _ = AddToVectorStoreAsync(skill);
        }
    }

    /// <summary>
    /// Registers a skill asynchronously with immediate persistence.
    /// </summary>
    public async Task RegisterSkillAsync(Skill skill, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(skill);
        _skills[skill.Name] = skill;
        _isDirty = true;

        await SaveSkillsAsync(ct);

        if (_embedding != null && _vectorStore != null)
        {
            await AddToVectorStoreAsync(skill, ct);
        }
    }

    /// <summary>
    /// Finds skills matching a goal using semantic similarity if available.
    /// </summary>
    public async Task<List<Skill>> FindMatchingSkillsAsync(
        string goal,
        Dictionary<string, object>? context = null)
    {
        if (string.IsNullOrWhiteSpace(goal))
            return new List<Skill>();

        List<Skill> allSkills = _skills.Values.ToList();

        // Use vector store for semantic search if available
        if (_embedding != null && _vectorStore != null && _vectorStore.GetAll().Any())
        {
            try
            {
                float[] goalEmbedding = await _embedding.CreateEmbeddingsAsync(goal);
                var similarDocs = await _vectorStore.GetSimilarDocumentsAsync(goalEmbedding, Math.Min(10, allSkills.Count));

                var matchedSkills = new List<Skill>();
                foreach (var doc in similarDocs)
                {
                    if (doc.Metadata?.TryGetValue("skill_name", out var nameObj) == true && 
                        nameObj is string name &&
                        _skills.TryGetValue(name, out var skill))
                    {
                        matchedSkills.Add(skill);
                    }
                }

                if (matchedSkills.Count > 0)
                    return matchedSkills;
            }
            catch
            {
                // Fall back to keyword matching if vector search fails
            }
        }

        // Fallback: embedding-based similarity on cached embeddings
        if (_embedding != null)
        {
            float[] goalEmbedding = await _embedding.CreateEmbeddingsAsync(goal);

            var skillScores = new List<(Skill skill, double score)>();
            foreach (var skill in allSkills)
            {
                float[] skillEmbedding = await _embedding.CreateEmbeddingsAsync(skill.Description);
                double similarity = CosineSimilarity(goalEmbedding, skillEmbedding);
                skillScores.Add((skill, similarity));
            }

            return skillScores
                .OrderByDescending(x => x.score)
                .Select(x => x.skill)
                .ToList();
        }

        // Final fallback: keyword matching
        string goalLower = goal.ToLowerInvariant();
        return allSkills
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

        _skills.TryGetValue(name, out Skill? skill);
        return skill;
    }

    /// <summary>
    /// Records skill execution outcome and updates persistence.
    /// </summary>
    public void RecordSkillExecution(string name, bool success)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        _skills.AddOrUpdate(
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

        _isDirty = true;
        if (_config.AutoSave)
        {
            _ = SaveSkillsAsync();
        }
    }

    /// <summary>
    /// Gets all registered skills.
    /// </summary>
    public IReadOnlyList<Skill> GetAllSkills()
        => _skills.Values.OrderByDescending(s => s.SuccessRate).ToList();

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
    /// Forces an immediate save of all skills to disk.
    /// </summary>
    public async Task SaveSkillsAsync(CancellationToken ct = default)
    {
        if (!_isDirty) return;

        await _saveLock.WaitAsync(ct);
        try
        {
            var serializableSkills = _skills.Values.Select(ToSerializable).ToList();
            string json = JsonSerializer.Serialize(serializableSkills, JsonOptions);
            
            string fullPath = Path.GetFullPath(_config.StoragePath);
            string? directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await File.WriteAllTextAsync(fullPath, json, ct);
            _isDirty = false;
        }
        finally
        {
            _saveLock.Release();
        }
    }

    /// <summary>
    /// Loads skills from disk.
    /// </summary>
    public async Task LoadSkillsAsync(CancellationToken ct = default)
    {
        string fullPath = Path.GetFullPath(_config.StoragePath);
        if (!File.Exists(fullPath))
            return;

        try
        {
            string json = await File.ReadAllTextAsync(fullPath, ct);
            var serializableSkills = JsonSerializer.Deserialize<List<SerializableSkill>>(json, JsonOptions);

            if (serializableSkills != null)
            {
                foreach (var ss in serializableSkills)
                {
                    var skill = FromSerializable(ss);
                    _skills[skill.Name] = skill;

                    // Add to vector store if available
                    if (_embedding != null && _vectorStore != null)
                    {
                        await AddToVectorStoreAsync(skill, ct);
                    }
                }
            }
        }
        catch (JsonException ex)
        {
            // Log but continue - corrupted file shouldn't crash the app
            Console.Error.WriteLine($"[WARN] Failed to load skills from {fullPath}: {ex.Message}");
        }
    }

    /// <summary>
    /// Deletes a skill by name.
    /// </summary>
    public async Task DeleteSkillAsync(string name, CancellationToken ct = default)
    {
        if (_skills.TryRemove(name, out _))
        {
            _isDirty = true;
            await SaveSkillsAsync(ct);
        }
    }

    /// <summary>
    /// Gets statistics about the skill registry.
    /// </summary>
    public SkillRegistryStats GetStats()
    {
        var skills = _skills.Values.ToList();
        return new SkillRegistryStats(
            TotalSkills: skills.Count,
            AverageSuccessRate: skills.Count > 0 ? skills.Average(s => s.SuccessRate) : 0,
            TotalExecutions: skills.Sum(s => s.UsageCount),
            MostUsedSkill: skills.OrderByDescending(s => s.UsageCount).FirstOrDefault()?.Name,
            MostSuccessfulSkill: skills.OrderByDescending(s => s.SuccessRate).FirstOrDefault()?.Name,
            StoragePath: Path.GetFullPath(_config.StoragePath),
            IsPersisted: File.Exists(_config.StoragePath));
    }

    private async Task AddToVectorStoreAsync(Skill skill, CancellationToken ct = default)
    {
        if (_embedding == null || _vectorStore == null) return;

        try
        {
            // Create searchable text from skill
            string searchText = $"{skill.Name}: {skill.Description}. Steps: {string.Join(", ", skill.Steps.Select(s => s.Action))}";
            float[] embedding = await _embedding.CreateEmbeddingsAsync(searchText, ct);

            var metadata = new Dictionary<string, object>
            {
                ["skill_name"] = skill.Name,
                ["description"] = skill.Description,
                ["success_rate"] = skill.SuccessRate,
                ["usage_count"] = skill.UsageCount,
                ["created_at"] = skill.CreatedAt.ToString("O"),
                ["type"] = "skill"
            };

            var vector = new Vector
            {
                Id = $"skill_{skill.Name}",
                Text = searchText,
                Embedding = embedding,
                Metadata = metadata!
            };

            await _vectorStore.AddAsync(new[] { vector }, ct);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] Failed to add skill '{skill.Name}' to vector store: {ex.Message}");
        }
    }

    private static SerializableSkill ToSerializable(Skill skill) => new(
        skill.Name,
        skill.Description,
        skill.Prerequisites,
        skill.Steps.Select(s => new SerializablePlanStep(
            s.Action,
            s.Parameters,
            s.ExpectedOutcome,
            s.ConfidenceScore)).ToList(),
        skill.SuccessRate,
        skill.UsageCount,
        skill.CreatedAt,
        skill.LastUsed);

    private static Skill FromSerializable(SerializableSkill ss) => new(
        ss.Name,
        ss.Description,
        ss.Prerequisites,
        ss.Steps.Select(s => new PlanStep(
            s.Action,
            s.Parameters,
            s.ExpectedOutcome,
            s.ConfidenceScore)).ToList(),
        ss.SuccessRate,
        ss.UsageCount,
        ss.CreatedAt,
        ss.LastUsed);

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        double dotProduct = 0, magnitudeA = 0, magnitudeB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        if (magnitudeA == 0 || magnitudeB == 0) return 0;
        return dotProduct / (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
    }

    public async ValueTask DisposeAsync()
    {
        if (_isDirty)
        {
            await SaveSkillsAsync();
        }
        _saveLock.Dispose();
    }
}

/// <summary>
/// Statistics about the skill registry.
/// </summary>
public sealed record SkillRegistryStats(
    int TotalSkills,
    double AverageSuccessRate,
    int TotalExecutions,
    string? MostUsedSkill,
    string? MostSuccessfulSkill,
    string StoragePath,
    bool IsPersisted);
