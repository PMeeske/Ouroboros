#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Persistent Memory Store - Long-term learning and experience
// ==========================================================

namespace LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Represents a learning experience stored in memory.
/// </summary>
public sealed record Experience(
    Guid Id,
    string Goal,
    Plan Plan,
    ExecutionResult Execution,
    VerificationResult Verification,
    DateTime Timestamp,
    Dictionary<string, object> Metadata);

/// <summary>
/// Memory query for retrieving relevant experiences.
/// </summary>
public sealed record MemoryQuery(
    string Goal,
    Dictionary<string, object>? Context,
    int MaxResults,
    double MinSimilarity);

/// <summary>
/// Interface for persistent memory storage and retrieval.
/// Enables continual learning from past executions.
/// </summary>
public interface IMemoryStore
{
    /// <summary>
    /// Stores an experience in long-term memory.
    /// </summary>
    /// <param name="experience">The experience to store</param>
    /// <param name="ct">Cancellation token</param>
    Task StoreExperienceAsync(Experience experience, CancellationToken ct = default);

    /// <summary>
    /// Retrieves relevant experiences for a goal.
    /// </summary>
    /// <param name="query">Memory query parameters</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of relevant experiences sorted by relevance</returns>
    Task<List<Experience>> RetrieveRelevantExperiencesAsync(
        MemoryQuery query,
        CancellationToken ct = default);

    /// <summary>
    /// Gets statistics about stored experiences.
    /// </summary>
    /// <returns>Memory statistics</returns>
    Task<MemoryStatistics> GetStatisticsAsync();

    /// <summary>
    /// Clears all experiences from memory.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    Task ClearAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets an experience by ID.
    /// </summary>
    /// <param name="id">Experience ID</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Experience if found, null otherwise</returns>
    Task<Experience?> GetExperienceAsync(Guid id, CancellationToken ct = default);
}

/// <summary>
/// Statistics about the memory store.
/// </summary>
public sealed record MemoryStatistics(
    int TotalExperiences,
    int SuccessfulExecutions,
    int FailedExecutions,
    double AverageQualityScore,
    Dictionary<string, int> GoalCounts);
