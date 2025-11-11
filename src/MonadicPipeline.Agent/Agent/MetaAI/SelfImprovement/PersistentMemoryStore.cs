// <copyright file="PersistentMemoryStore.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Agent.MetaAI;

using System.Collections.Concurrent;
using LangChain.Databases;

/// <summary>
/// Memory type classification.
/// </summary>
public enum MemoryType
{
    /// <summary>Specific execution instances (recent experiences).</summary>
    Episodic,

    /// <summary>Generalized knowledge and patterns (consolidated).</summary>
    Semantic,
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
    private readonly ConcurrentDictionary<Guid, (Experience experience, MemoryType type, double importance)> experiences = new();
    private readonly IEmbeddingModel? embedding;
    private readonly TrackedVectorStore? vectorStore;
    private readonly PersistentMemoryConfig config;
    private DateTime lastConsolidation = DateTime.UtcNow;

    public PersistentMemoryStore(
        IEmbeddingModel? embedding = null,
        TrackedVectorStore? vectorStore = null,
        PersistentMemoryConfig? config = null)
    {
        this.embedding = embedding;
        this.vectorStore = vectorStore;
        this.config = config ?? new PersistentMemoryConfig(
            ConsolidationInterval: TimeSpan.FromHours(1));
    }

    /// <summary>
    /// Stores an experience in memory with automatic importance scoring.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task StoreExperienceAsync(Experience experience, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(experience);

        // Calculate importance score
        var importance = this.CalculateImportance(experience);

        // Store in short-term episodic memory initially
        this.experiences[experience.Id] = (experience, MemoryType.Episodic, importance);

        // Store in vector database if available
        if (this.embedding != null && this.vectorStore != null)
        {
            await this.StoreInVectorStoreAsync(experience, MemoryType.Episodic, ct);
        }

        // Check if consolidation is needed
        if (this.ShouldConsolidate())
        {
            await this.ConsolidateMemoriesAsync(ct);
        }

        // Check if forgetting is needed
        if (this.config.EnableForgetting && this.experiences.Count > this.config.LongTermCapacity)
        {
            await this.ForgetLowImportanceMemoriesAsync();
        }
    }

    /// <summary>
    /// Retrieves relevant experiences based on similarity and recency.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<List<Experience>> RetrieveRelevantExperiencesAsync(
        MemoryQuery query,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // If vector store available, use semantic search
        if (this.embedding != null && this.vectorStore != null)
        {
            return await this.RetrieveViaSimilarityAsync(query, ct);
        }

        // Fallback to simple filtering
        var experiences = this.experiences.Values
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
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public Task<MemoryStatistics> GetStatisticsAsync()
    {
        var experiences = this.experiences.Values.Select(v => v.experience).ToList();

        var stats = new MemoryStatistics(
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
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task ClearAsync(CancellationToken ct = default)
    {
        this.experiences.Clear();

        if (this.vectorStore != null)
        {
            // Note: TrackedVectorStore doesn't have a Clear method
            // In a real implementation with Qdrant, we would clear the collection
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Gets an experience by ID.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public Task<Experience?> GetExperienceAsync(Guid id, CancellationToken ct = default)
    {
        var found = this.experiences.TryGetValue(id, out var entry);
        return Task.FromResult(found ? entry.experience : null);
    }

    /// <summary>
    /// Gets experiences by memory type.
    /// </summary>
    /// <returns></returns>
    public List<Experience> GetExperiencesByType(MemoryType type)
    {
        return this.experiences.Values
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
        var qualityScore = experience.Verification.QualityScore;

        // Recency bonus (newer memories are more important initially)
        var recencyHours = (DateTime.UtcNow - experience.Timestamp).TotalHours;
        var recencyBonus = Math.Max(0, 1.0 - (recencyHours / 24.0)); // Decays over 24 hours

        // Success bonus
        var successBonus = experience.Verification.Verified ? 0.2 : 0.0;

        // Combined importance (weighted average)
        var importance = (qualityScore * 0.5) + (recencyBonus * 0.3) + successBonus;

        return Math.Clamp(importance, 0.0, 1.0);
    }

    /// <summary>
    /// Determines if consolidation should occur.
    /// </summary>
    private bool ShouldConsolidate()
    {
        // Check time-based consolidation
        if ((DateTime.UtcNow - this.lastConsolidation) < this.config.ConsolidationInterval)
        {
            return false;
        }

        // Check capacity-based consolidation
        var episodicCount = this.experiences.Values.Count(e => e.type == MemoryType.Episodic);
        return episodicCount > this.config.ShortTermCapacity;
    }

    /// <summary>
    /// Consolidates short-term episodic memories into long-term semantic memories.
    /// </summary>
    private async Task ConsolidateMemoriesAsync(CancellationToken ct = default)
    {
        this.lastConsolidation = DateTime.UtcNow;

        // Find high-importance episodic memories to consolidate
        var toConsolidate = this.experiences.Values
            .Where(e => e.type == MemoryType.Episodic && e.importance >= this.config.ConsolidationThreshold)
            .OrderByDescending(e => e.importance)
            .Take(this.config.ShortTermCapacity / 2)
            .ToList();

        foreach (var (experience, _, importance) in toConsolidate)
        {
            // Mark as semantic (long-term)
            this.experiences[experience.Id] = (experience, MemoryType.Semantic, importance);

            // Update in vector store if available
            if (this.embedding != null && this.vectorStore != null)
            {
                await this.StoreInVectorStoreAsync(experience, MemoryType.Semantic, ct);
            }
        }
    }

    /// <summary>
    /// Removes low-importance memories to prevent unbounded growth.
    /// </summary>
    private async Task ForgetLowImportanceMemoriesAsync()
    {
        var toForget = this.experiences.Values
            .Where(e => e.importance < this.config.ForgettingThreshold)
            .OrderBy(e => e.importance)
            .Take(this.experiences.Count - this.config.LongTermCapacity)
            .ToList();

        foreach (var (experience, _, _) in toForget)
        {
            this.experiences.TryRemove(experience.Id, out _);
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
        if (this.embedding == null || this.vectorStore == null)
        {
            return;
        }

        var text = $"[{type}] Goal: {experience.Goal}\n" +
                   $"Plan: {string.Join(", ", experience.Plan.Steps.Select(s => s.Action))}\n" +
                   $"Quality: {experience.Verification.QualityScore:P0}\n" +
                   $"Verified: {experience.Verification.Verified}";

        var embedding = await this.embedding.CreateEmbeddingsAsync(text, ct);

        var vector = new Vector
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
            },
        };

        await this.vectorStore.AddAsync(new[] { vector }, ct);
    }

    /// <summary>
    /// Retrieves experiences using vector similarity search.
    /// </summary>
    private async Task<List<Experience>> RetrieveViaSimilarityAsync(
        MemoryQuery query,
        CancellationToken ct)
    {
        if (this.embedding == null || this.vectorStore == null)
        {
            return new List<Experience>();
        }

        try
        {
            var queryEmbedding = await this.embedding.CreateEmbeddingsAsync(query.Goal, ct);

            var searchResults = await this.vectorStore.GetSimilarDocuments(
                this.embedding,
                query.Goal,
                amount: query.MaxResults);

            var experiences = new List<Experience>();
            foreach (var doc in searchResults)
            {
                if (doc.Metadata?.TryGetValue("id", out var idObj) == true &&
                    Guid.TryParse(idObj?.ToString(), out var id) &&
                    this.experiences.TryGetValue(id, out var entry))
                {
                    experiences.Add(entry.experience);
                }
            }

            return experiences;
        }
        catch
        {
            // Fallback to simple retrieval
            return this.experiences.Values
                .Select(e => e.experience)
                .Take(query.MaxResults)
                .ToList();
        }
    }
}
