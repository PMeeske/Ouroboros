#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Persistent Memory Store Implementation
// Enhanced memory with persistence, consolidation, and forgetting
// ==========================================================

using System.Collections.Concurrent;
using LangChain.Databases;

namespace LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Memory type classification.
/// </summary>
public enum MemoryType
{
    /// <summary>Specific execution instances (recent experiences)</summary>
    Episodic,

    /// <summary>Generalized knowledge and patterns (consolidated)</summary>
    Semantic
}

/// <summary>
/// Configuration for persistent memory behavior.
/// </summary>
public sealed record PersistentMemoryConfig(
    int ShortTermCapacity = 100,
    int LongTermCapacity = 1000,
    double ConsolidationThreshold = 0.7,
    TimeSpan ConsolidationInterval = default,
    bool EnableForgetting = true,
    double ForgettingThreshold = 0.3);

/// <summary>
/// Enhanced memory store with persistence, consolidation, and intelligent forgetting.
/// Implements short-term → long-term memory transfer and episodic/semantic separation.
/// </summary>
public sealed class PersistentMemoryStore : IMemoryStore
{
    private readonly ConcurrentDictionary<Guid, (Experience experience, MemoryType type, double importance)> _experiences = new();
    private readonly IEmbeddingModel? _embedding;
    private readonly TrackedVectorStore? _vectorStore;
    private readonly PersistentMemoryConfig _config;
    private DateTime _lastConsolidation = DateTime.UtcNow;

    public PersistentMemoryStore(
        IEmbeddingModel? embedding = null,
        TrackedVectorStore? vectorStore = null,
        PersistentMemoryConfig? config = null)
    {
        _embedding = embedding;
        _vectorStore = vectorStore;
        _config = config ?? new PersistentMemoryConfig(
            ConsolidationInterval: TimeSpan.FromHours(1));
    }

    /// <summary>
    /// Stores an experience in memory with automatic importance scoring.
    /// </summary>
    public async Task StoreExperienceAsync(Experience experience, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(experience);

        // Calculate importance score
        double importance = CalculateImportance(experience);

        // Store in short-term episodic memory initially
        _experiences[experience.Id] = (experience, MemoryType.Episodic, importance);

        // Store in vector database if available
        if (_embedding != null && _vectorStore != null)
        {
            await StoreInVectorStoreAsync(experience, MemoryType.Episodic, ct);
        }

        // Check if consolidation is needed
        if (ShouldConsolidate())
        {
            await ConsolidateMemoriesAsync(ct);
        }

        // Check if forgetting is needed
        if (_config.EnableForgetting && _experiences.Count > _config.LongTermCapacity)
        {
            await ForgetLowImportanceMemoriesAsync();
        }
    }

    /// <summary>
    /// Retrieves relevant experiences based on similarity and recency.
    /// </summary>
    public async Task<List<Experience>> RetrieveRelevantExperiencesAsync(
        MemoryQuery query,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // If vector store available, use semantic search
        if (_embedding != null && _vectorStore != null)
        {
            return await RetrieveViaSimilarityAsync(query, ct);
        }

        // Fallback to simple filtering
        List<Experience> experiences = _experiences.Values
            .Where(e => e.experience.Verification.Verified)
            .OrderByDescending(e => e.importance)
            .Take(query.MaxResults)
            .Select(e => e.experience)
            .ToList();

        return experiences;
    }

    /// <summary>
    /// Gets memory statistics.
    /// </summary>
    public Task<MemoryStatistics> GetStatisticsAsync()
    {
        List<Experience> experiences = _experiences.Values.Select(v => v.experience).ToList();

        MemoryStatistics stats = new MemoryStatistics(
            TotalExperiences: experiences.Count,
            SuccessfulExecutions: experiences.Count(e => e.Verification.Verified),
            FailedExecutions: experiences.Count(e => !e.Verification.Verified),
            AverageQualityScore: experiences.Any()
                ? experiences.Average(e => e.Verification.QualityScore)
                : 0.0,
            GoalCounts: experiences
                .GroupBy(e => e.Goal)
                .ToDictionary(g => g.Key, g => g.Count()));

        return Task.FromResult(stats);
    }

    /// <summary>
    /// Clears all experiences from memory.
    /// </summary>
    public async Task ClearAsync(CancellationToken ct = default)
    {
        _experiences.Clear();

        if (_vectorStore != null)
        {
            // Note: TrackedVectorStore doesn't have a Clear method
            // In a real implementation with Qdrant, we would clear the collection
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Gets an experience by ID.
    /// </summary>
    public Task<Experience?> GetExperienceAsync(Guid id, CancellationToken ct = default)
    {
        bool found = _experiences.TryGetValue(id, out (Experience experience, MemoryType type, double importance) entry);
        return Task.FromResult(found ? entry.experience : null);
    }

    /// <summary>
    /// Gets experiences by memory type.
    /// </summary>
    public List<Experience> GetExperiencesByType(MemoryType type)
    {
        return _experiences.Values
            .Where(e => e.type == type)
            .Select(e => e.experience)
            .ToList();
    }

    /// <summary>
    /// Calculates importance score for an experience.
    /// Based on quality, recency, and uniqueness.
    /// </summary>
    private double CalculateImportance(Experience experience)
    {
        // Base importance from quality
        double qualityScore = experience.Verification.QualityScore;

        // Recency bonus (newer memories are more important initially)
        double recencyHours = (DateTime.UtcNow - experience.Timestamp).TotalHours;
        double recencyBonus = Math.Max(0, 1.0 - (recencyHours / 24.0)); // Decays over 24 hours

        // Success bonus
        double successBonus = experience.Verification.Verified ? 0.2 : 0.0;

        // Combined importance (weighted average)
        double importance = (qualityScore * 0.5) + (recencyBonus * 0.3) + successBonus;

        return Math.Clamp(importance, 0.0, 1.0);
    }

    /// <summary>
    /// Determines if consolidation should occur.
    /// </summary>
    private bool ShouldConsolidate()
    {
        // Check time-based consolidation
        if ((DateTime.UtcNow - _lastConsolidation) < _config.ConsolidationInterval)
            return false;

        // Check capacity-based consolidation
        int episodicCount = _experiences.Values.Count(e => e.type == MemoryType.Episodic);
        return episodicCount > _config.ShortTermCapacity;
    }

    /// <summary>
    /// Consolidates short-term episodic memories into long-term semantic memories.
    /// </summary>
    private async Task ConsolidateMemoriesAsync(CancellationToken ct = default)
    {
        _lastConsolidation = DateTime.UtcNow;

        // Find high-importance episodic memories to consolidate
        List<(Experience experience, MemoryType type, double importance)> toConsolidate = _experiences.Values
            .Where(e => e.type == MemoryType.Episodic && e.importance >= _config.ConsolidationThreshold)
            .OrderByDescending(e => e.importance)
            .Take(_config.ShortTermCapacity / 2)
            .ToList();

        foreach ((Experience experience, MemoryType _, double importance) in toConsolidate)
        {
            // Mark as semantic (long-term)
            _experiences[experience.Id] = (experience, MemoryType.Semantic, importance);

            // Update in vector store if available
            if (_embedding != null && _vectorStore != null)
            {
                await StoreInVectorStoreAsync(experience, MemoryType.Semantic, ct);
            }
        }
    }

    /// <summary>
    /// Removes low-importance memories to prevent unbounded growth.
    /// </summary>
    private async Task ForgetLowImportanceMemoriesAsync()
    {
        List<(Experience experience, MemoryType type, double importance)> toForget = _experiences.Values
            .Where(e => e.importance < _config.ForgettingThreshold)
            .OrderBy(e => e.importance)
            .Take(_experiences.Count - _config.LongTermCapacity)
            .ToList();

        foreach ((Experience experience, MemoryType _, double _) in toForget)
        {
            _experiences.TryRemove(experience.Id, out _);
        }

        await Task.CompletedTask;
    }

    /// <summary>
    /// Stores experience in vector database.
    /// </summary>
    private async Task StoreInVectorStoreAsync(
        Experience experience,
        MemoryType type,
        CancellationToken ct)
    {
        if (_embedding == null || _vectorStore == null)
            return;

        string text = $"[{type}] Goal: {experience.Goal}\n" +
                   $"Plan: {string.Join(", ", experience.Plan.Steps.Select(s => s.Action))}\n" +
                   $"Quality: {experience.Verification.QualityScore:P0}\n" +
                   $"Verified: {experience.Verification.Verified}";

        float[] embedding = await _embedding.CreateEmbeddingsAsync(text, ct);

        Vector vector = new Vector
        {
            Id = experience.Id.ToString(),
            Text = text,
            Embedding = embedding,
            Metadata = new Dictionary<string, object>
            {
                ["id"] = experience.Id.ToString(),
                ["goal"] = experience.Goal,
                ["quality"] = experience.Verification.QualityScore,
                ["verified"] = experience.Verification.Verified,
                ["timestamp"] = experience.Timestamp,
                ["memory_type"] = type.ToString()
            }
        };

        await _vectorStore.AddAsync(new[] { vector }, ct);
    }

    /// <summary>
    /// Retrieves experiences using vector similarity search.
    /// </summary>
    private async Task<List<Experience>> RetrieveViaSimilarityAsync(
        MemoryQuery query,
        CancellationToken ct)
    {
        if (_embedding == null || _vectorStore == null)
            return new List<Experience>();

        try
        {
            float[] queryEmbedding = await _embedding.CreateEmbeddingsAsync(query.Goal, ct);

            IReadOnlyCollection<LangChain.DocumentLoaders.Document> searchResults = await _vectorStore.GetSimilarDocuments(
                _embedding,
                query.Goal,
                amount: query.MaxResults);

            List<Experience> experiences = new List<Experience>();
            foreach (LangChain.DocumentLoaders.Document doc in searchResults)
            {
                if (doc.Metadata?.TryGetValue("id", out object? idObj) == true &&
                    Guid.TryParse(idObj?.ToString(), out Guid id) &&
                    _experiences.TryGetValue(id, out (Experience experience, MemoryType type, double importance) entry))
                {
                    experiences.Add(entry.experience);
                }
            }

            return experiences;
        }
        catch
        {
            // Fallback to simple retrieval
            return _experiences.Values
                .Select(e => e.experience)
                .Take(query.MaxResults)
                .ToList();
        }
    }
}
