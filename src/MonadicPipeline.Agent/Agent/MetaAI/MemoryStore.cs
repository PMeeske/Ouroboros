// <copyright file="MemoryStore.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Agent.MetaAI;

using System.Collections.Concurrent;
using LangChain.Databases;

/// <summary>
/// Implementation of persistent memory store for continual learning.
/// Uses in-memory storage with optional vector similarity search.
/// </summary>
public sealed class MemoryStore : IMemoryStore
{
    private readonly ConcurrentDictionary<Guid, Experience> experiences = new();
    private readonly IEmbeddingModel? embedding;
    private readonly TrackedVectorStore? vectorStore;

    public MemoryStore(IEmbeddingModel? embedding = null, TrackedVectorStore? vectorStore = null)
    {
        this.embedding = embedding;
        this.vectorStore = vectorStore;
    }

    /// <summary>
    /// Stores an experience in long-term memory.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task StoreExperienceAsync(Experience experience, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(experience);

        this.experiences[experience.Id] = experience;

        // If vector store available, store for similarity search
        if (this.embedding != null && this.vectorStore != null)
        {
            var text = $"Goal: {experience.Goal}\nPlan: {string.Join(", ", experience.Plan.Steps.Select(s => s.Action))}\nQuality: {experience.Verification.QualityScore}";
            var embedding = await this.embedding.CreateEmbeddingsAsync(text, ct);

            var vector = new Vector
            {
                Id = experience.Id.ToString(),
                Text = text,
                Embedding = embedding,
                Metadata = new Dictionary<string, object>
                {
                    ["goal"] = experience.Goal,
                    ["quality"] = experience.Verification.QualityScore,
                    ["verified"] = experience.Verification.Verified,
                    ["timestamp"] = experience.Timestamp
                },
            };

            await this.vectorStore.AddAsync(new[] { vector }, ct);
        }
    }

    /// <summary>
    /// Retrieves relevant experiences based on similarity.
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
            var queryEmbedding = await this.embedding.CreateEmbeddingsAsync(query.Goal, ct);
            var similarDocs = await this.vectorStore.GetSimilarDocuments(
                this.embedding,
                query.Goal,
                amount: query.MaxResults);

            var experiences = new List<Experience>();
            foreach (var doc in similarDocs)
            {
                if (doc.Metadata?.TryGetValue("id", out var idObj) == true &&
                    Guid.TryParse(idObj?.ToString(), out var id) &&
                    this.experiences.TryGetValue(id, out var exp))
                {
                    experiences.Add(exp);
                }
            }

            return experiences;
        }

        // Fallback to keyword-based search
        var goalLower = query.Goal.ToLowerInvariant();
        var matches = this.experiences.Values
            .Where(exp => exp.Goal.ToLowerInvariant().Contains(goalLower))
            .OrderByDescending(exp => exp.Verification.QualityScore)
            .Take(query.MaxResults)
            .ToList();

        return await Task.FromResult(matches);
    }

    /// <summary>
    /// Gets memory statistics.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<MemoryStatistics> GetStatisticsAsync()
    {
        var experiences = this.experiences.Values.ToList();

        var totalCount = experiences.Count;
        var successCount = experiences.Count(e => e.Execution.Success);
        var failCount = totalCount - successCount;
        var avgQuality = experiences.Any()
            ? experiences.Average(e => e.Verification.QualityScore)
            : 0.0;

        var goalCounts = experiences
            .GroupBy(e => e.Goal)
            .ToDictionary(g => g.Key, g => g.Count());

        var stats = new MemoryStatistics(
            totalCount,
            successCount,
            failCount,
            avgQuality,
            goalCounts);

        return await Task.FromResult(stats);
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
            await this.vectorStore.ClearAsync(ct);
        }
    }

    /// <summary>
    /// Gets an experience by ID.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<Experience?> GetExperienceAsync(Guid id, CancellationToken ct = default)
    {
        this.experiences.TryGetValue(id, out var experience);
        return await Task.FromResult(experience);
    }
}
