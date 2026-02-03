// <copyright file="MockMemoryStore.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Mocks;

using Ouroboros.Agent.MetaAI;

/// <summary>
/// Mock implementation of IMemoryStore for testing purposes.
/// Provides in-memory storage with configurable behavior.
/// </summary>
public sealed class MockMemoryStore : IMemoryStore
{
    private readonly Dictionary<Guid, Experience> experiences = new();
    private readonly Func<MemoryQuery, List<Experience>>? retrievalFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockMemoryStore"/> class.
    /// </summary>
    public MockMemoryStore()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MockMemoryStore"/> class with custom retrieval logic.
    /// </summary>
    /// <param name="retrievalFactory">Factory function for retrieving experiences.</param>
    public MockMemoryStore(Func<MemoryQuery, List<Experience>> retrievalFactory)
    {
        this.retrievalFactory = retrievalFactory;
    }

    /// <summary>
    /// Gets the number of stored experiences.
    /// </summary>
    public int Count => this.experiences.Count;

    /// <summary>
    /// Gets the last experience that was stored.
    /// </summary>
    public Experience? LastStoredExperience { get; private set; }

    /// <summary>
    /// Gets the number of times StoreExperienceAsync was called.
    /// </summary>
    public int StoreCallCount { get; private set; }

    /// <summary>
    /// Gets the number of times RetrieveRelevantExperiencesAsync was called.
    /// </summary>
    public int RetrieveCallCount { get; private set; }

    /// <summary>
    /// Stores an experience in memory.
    /// </summary>
    public Task StoreExperienceAsync(Experience experience, CancellationToken ct = default)
    {
        this.StoreCallCount++;
        this.LastStoredExperience = experience;
        this.experiences[experience.Id] = experience;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Retrieves relevant experiences based on query.
    /// </summary>
    public Task<List<Experience>> RetrieveRelevantExperiencesAsync(
        MemoryQuery query,
        CancellationToken ct = default)
    {
        this.RetrieveCallCount++;

        if (this.retrievalFactory != null)
        {
            return Task.FromResult(this.retrievalFactory(query));
        }

        // Default: return all experiences matching the goal (case-insensitive partial match)
        var results = this.experiences.Values
            .Where(e => e.Goal.Contains(query.Goal, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(e => e.Timestamp)
            .Take(query.MaxResults)
            .ToList();

        return Task.FromResult(results);
    }

    /// <summary>
    /// Gets statistics about stored experiences.
    /// </summary>
    public Task<MemoryStatistics> GetStatisticsAsync()
    {
        var successful = this.experiences.Values.Count(e => e.Execution.Success);
        var failed = this.experiences.Values.Count(e => !e.Execution.Success);
        var avgQuality = this.experiences.Values.Any()
            ? this.experiences.Values.Average(e => e.Verification.QualityScore)
            : 0.0;

        var goalCounts = this.experiences.Values
            .GroupBy(e => e.Goal)
            .ToDictionary(g => g.Key, g => g.Count());

        var stats = new MemoryStatistics(
            this.experiences.Count,
            successful,
            failed,
            avgQuality,
            goalCounts);

        return Task.FromResult(stats);
    }

    /// <summary>
    /// Clears all stored experiences.
    /// </summary>
    public Task ClearAsync(CancellationToken ct = default)
    {
        this.experiences.Clear();
        this.LastStoredExperience = null;
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets an experience by ID.
    /// </summary>
    public Task<Experience?> GetExperienceAsync(Guid id, CancellationToken ct = default)
    {
        this.experiences.TryGetValue(id, out var experience);
        return Task.FromResult(experience);
    }

    /// <summary>
    /// Adds a pre-configured experience for testing.
    /// </summary>
    /// <param name="experience">Experience to add.</param>
    public void AddExperience(Experience experience)
    {
        this.experiences[experience.Id] = experience;
    }
}
