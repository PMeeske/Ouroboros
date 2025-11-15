#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Memory Store Implementation
// Persistent long-term learning storage
// ==========================================================

using System.Collections.Concurrent;
using LangChain.Databases;

namespace LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Implementation of persistent memory store for continual learning.
/// Uses in-memory storage with optional vector similarity search.
/// </summary>
public sealed class MemoryStore : IMemoryStore
{
    private readonly ConcurrentDictionary<Guid, Experience> _experiences = new();
    private readonly IEmbeddingModel? _embedding;
    private readonly TrackedVectorStore? _vectorStore;

    public MemoryStore(IEmbeddingModel? embedding = null, TrackedVectorStore? vectorStore = null)
    {
        _embedding = embedding;
        _vectorStore = vectorStore;
    }

    /// <summary>
    /// Stores an experience in long-term memory.
    /// </summary>
    public async Task StoreExperienceAsync(Experience experience, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(experience);

        _experiences[experience.Id] = experience;

        // If vector store available, store for similarity search
        if (_embedding != null && _vectorStore != null)
        {
            var text = $"Goal: {experience.Goal}\nPlan: {string.Join(", ", experience.Plan.Steps.Select(s => s.Action))}\nQuality: {experience.Verification.QualityScore}";
            var embedding = await _embedding.CreateEmbeddingsAsync(text, ct);

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
                }
            };

            await _vectorStore.AddAsync(new[] { vector }, ct);
        }
    }

    /// <summary>
    /// Retrieves relevant experiences based on similarity.
    /// </summary>
    public async Task<List<Experience>> RetrieveRelevantExperiencesAsync(
        MemoryQuery query,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        // If vector store available, use semantic search
        if (_embedding != null && _vectorStore != null)
        {
            var queryEmbedding = await _embedding.CreateEmbeddingsAsync(query.Goal, ct);
            var similarDocs = await _vectorStore.GetSimilarDocuments(
                _embedding,
                query.Goal,
                amount: query.MaxResults);

            var experiences = new List<Experience>();
            foreach (var doc in similarDocs)
            {
                if (doc.Metadata?.TryGetValue("id", out var idObj) == true &&
                    Guid.TryParse(idObj?.ToString(), out var id) &&
                    _experiences.TryGetValue(id, out var exp))
                {
                    experiences.Add(exp);
                }
            }

            return experiences;
        }

        // Fallback to keyword-based search
        var goalLower = query.Goal.ToLowerInvariant();
        var matches = _experiences.Values
            .Where(exp => exp.Goal.ToLowerInvariant().Contains(goalLower))
            .OrderByDescending(exp => exp.Verification.QualityScore)
            .Take(query.MaxResults)
            .ToList();

        return await Task.FromResult(matches);
    }

    /// <summary>
    /// Gets memory statistics.
    /// </summary>
    public async Task<MemoryStatistics> GetStatisticsAsync()
    {
        var experiences = _experiences.Values.ToList();

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
    public async Task ClearAsync(CancellationToken ct = default)
    {
        _experiences.Clear();

        if (_vectorStore != null)
        {
            await _vectorStore.ClearAsync(ct);
        }
    }

    /// <summary>
    /// Gets an experience by ID.
    /// </summary>
    public async Task<Experience?> GetExperienceAsync(Guid id, CancellationToken ct = default)
    {
        _experiences.TryGetValue(id, out var experience);
        return await Task.FromResult(experience);
    }
}
