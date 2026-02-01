// <copyright file="EpisodicMemoryArrows.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using Ouroboros.Core.Steps;
using Ouroboros.Domain.Events;
using Ouroboros.Domain.Reinforcement;
using Ouroboros.Domain.States;
using Qdrant.Client;

namespace Ouroboros.Pipeline.Memory;

/// <summary>
/// Provides arrow factory methods for episodic memory operations with explicit dependency parameterization.
/// This transforms the traditional constructor DI pattern to functional arrow composition.
/// </summary>
public static class EpisodicMemoryArrows
{
    /// <summary>
    /// Creates an arrow that stores an episode in memory with explicit dependencies.
    /// </summary>
    /// <param name="qdrantClient">The Qdrant client for vector storage.</param>
    /// <param name="embeddingModel">The embedding model for creating semantic vectors.</param>
    /// <param name="context">The execution context.</param>
    /// <param name="result">The outcome of the execution.</param>
    /// <param name="metadata">Additional metadata for the episode.</param>
    /// <param name="collectionName">Name of the Qdrant collection to use.</param>
    /// <returns>A step that stores the episode and updates the branch.</returns>
    public static Step<PipelineBranch, PipelineBranch> StoreEpisodeArrow(
        QdrantClient qdrantClient,
        IEmbeddingModel embeddingModel,
        ExecutionContext context,
        Outcome result,
        ImmutableDictionary<string, object> metadata,
        string collectionName = "episodic_memory")
        => async branch =>
        {
            try
            {
                var episodeResult = await StoreEpisodeInternalAsync(
                    qdrantClient,
                    embeddingModel,
                    branch,
                    context,
                    result,
                    metadata,
                    collectionName);

                if (episodeResult.IsSuccess)
                {
                    return branch.WithEvent(new EpisodeStoredEvent(
                        episodeResult.Value,
                        context.Goal,
                        result.Success,
                        DateTime.UtcNow));
                }

                return branch;
            }
            catch (Exception)
            {
                return branch; // Gracefully continue even if storage fails
            }
        };

    /// <summary>
    /// Creates a Result-safe arrow that stores an episode with comprehensive error handling.
    /// </summary>
    /// <param name="qdrantClient">The Qdrant client for vector storage.</param>
    /// <param name="embeddingModel">The embedding model for creating semantic vectors.</param>
    /// <param name="context">The execution context.</param>
    /// <param name="result">The outcome of the execution.</param>
    /// <param name="metadata">Additional metadata for the episode.</param>
    /// <param name="collectionName">Name of the Qdrant collection to use.</param>
    /// <returns>A Kleisli arrow that returns a Result with the updated branch or error.</returns>
    public static KleisliResult<PipelineBranch, PipelineBranch, string> SafeStoreEpisodeArrow(
        QdrantClient qdrantClient,
        IEmbeddingModel embeddingModel,
        ExecutionContext context,
        Outcome result,
        ImmutableDictionary<string, object> metadata,
        string collectionName = "episodic_memory")
        => async branch =>
        {
            try
            {
                var episodeResult = await StoreEpisodeInternalAsync(
                    qdrantClient,
                    embeddingModel,
                    branch,
                    context,
                    result,
                    metadata,
                    collectionName);

                return episodeResult.Match(
                    episodeId => Result<PipelineBranch, string>.Success(
                        branch.WithEvent(new EpisodeStoredEvent(
                            episodeId,
                            context.Goal,
                            result.Success,
                            DateTime.UtcNow))),
                    error => Result<PipelineBranch, string>.Failure(error));
            }
            catch (Exception ex)
            {
                return Result<PipelineBranch, string>.Failure($"Failed to store episode: {ex.Message}");
            }
        };

    /// <summary>
    /// Creates an arrow that retrieves similar episodes and adds them to the branch context.
    /// </summary>
    /// <param name="qdrantClient">The Qdrant client for vector storage.</param>
    /// <param name="embeddingModel">The embedding model for creating semantic vectors.</param>
    /// <param name="query">The query to search for similar episodes.</param>
    /// <param name="topK">Number of similar episodes to retrieve.</param>
    /// <param name="minSimilarity">Minimum similarity threshold.</param>
    /// <param name="collectionName">Name of the Qdrant collection to use.</param>
    /// <returns>A step that retrieves episodes and updates the branch with context.</returns>
    public static Step<PipelineBranch, PipelineBranch> RetrieveSimilarEpisodesArrow(
        QdrantClient qdrantClient,
        IEmbeddingModel embeddingModel,
        string query,
        int topK = 5,
        double minSimilarity = 0.7,
        string collectionName = "episodic_memory")
        => async branch =>
        {
            try
            {
                var episodesResult = await RetrieveSimilarEpisodesInternalAsync(
                    qdrantClient,
                    embeddingModel,
                    query,
                    topK,
                    minSimilarity,
                    collectionName);

                if (episodesResult.IsSuccess)
                {
                    return branch.WithEvent(new EpisodesRetrievedEvent(
                        episodesResult.Value.Count,
                        query,
                        DateTime.UtcNow));
                }

                return branch;
            }
            catch (Exception)
            {
                return branch; // Gracefully continue
            }
        };

    /// <summary>
    /// Creates an arrow that retrieves similar episodes and returns them for use in planning.
    /// </summary>
    /// <param name="qdrantClient">The Qdrant client for vector storage.</param>
    /// <param name="embeddingModel">The embedding model for creating semantic vectors.</param>
    /// <param name="collectionName">Name of the Qdrant collection to use.</param>
    /// <returns>A function that creates arrows for any query.</returns>
    public static Func<string, int, Step<PipelineBranch, (PipelineBranch, ImmutableList<Episode>)>> CreateEpisodeRetriever(
        QdrantClient qdrantClient,
        IEmbeddingModel embeddingModel,
        string collectionName = "episodic_memory")
    {
        return (query, topK) => async branch =>
        {
            var episodesResult = await RetrieveSimilarEpisodesInternalAsync(
                qdrantClient,
                embeddingModel,
                query,
                topK,
                0.7,
                collectionName);

            var episodes = episodesResult.IsSuccess
                ? episodesResult.Value
                : ImmutableList<Episode>.Empty;

            var updatedBranch = branch.WithEvent(new EpisodesRetrievedEvent(
                episodes.Count,
                query,
                DateTime.UtcNow));

            return (updatedBranch, episodes);
        };
    }

    /// <summary>
    /// Creates an arrow that plans with experience by retrieving similar episodes.
    /// </summary>
    /// <param name="qdrantClient">The Qdrant client for vector storage.</param>
    /// <param name="embeddingModel">The embedding model for creating semantic vectors.</param>
    /// <param name="goal">The goal to plan for.</param>
    /// <param name="topK">Number of similar episodes to retrieve.</param>
    /// <param name="collectionName">Name of the Qdrant collection to use.</param>
    /// <returns>A step that generates a plan based on relevant episodes.</returns>
    public static Step<PipelineBranch, (PipelineBranch, Verification.Plan?)> PlanWithExperienceArrow(
        QdrantClient qdrantClient,
        IEmbeddingModel embeddingModel,
        string goal,
        int topK = 5,
        string collectionName = "episodic_memory")
        => async branch =>
        {
            try
            {
                var episodesResult = await RetrieveSimilarEpisodesInternalAsync(
                    qdrantClient,
                    embeddingModel,
                    goal,
                    topK,
                    0.7,
                    collectionName);

                if (episodesResult.IsSuccess)
                {
                    var plan = GeneratePlanFromEpisodes(goal, episodesResult.Value);
                    return (branch, plan);
                }

                return (branch, null);
            }
            catch (Exception)
            {
                return (branch, null);
            }
        };

    /// <summary>
    /// Creates a pre-configured episodic memory system.
    /// </summary>
    /// <param name="qdrantClient">The Qdrant client for vector storage.</param>
    /// <param name="embeddingModel">The embedding model for creating semantic vectors.</param>
    /// <param name="collectionName">Name of the Qdrant collection to use.</param>
    /// <returns>An object with pre-configured arrow factories.</returns>
    public static EpisodicMemorySystem CreateConfiguredMemorySystem(
        QdrantClient qdrantClient,
        IEmbeddingModel embeddingModel,
        string collectionName = "episodic_memory")
    {
        return new EpisodicMemorySystem(qdrantClient, embeddingModel, collectionName);
    }

    // Internal helper methods

    private static async Task<Result<EpisodeId, string>> StoreEpisodeInternalAsync(
        QdrantClient qdrantClient,
        IEmbeddingModel embeddingModel,
        PipelineBranch branch,
        ExecutionContext context,
        Outcome result,
        ImmutableDictionary<string, object> metadata,
        string collectionName,
        CancellationToken ct = default)
    {
        // Reuse the logic from EpisodicMemoryEngine but without the class instance
        var engine = new EpisodicMemoryEngine(qdrantClient, embeddingModel, collectionName);
        return await engine.StoreEpisodeAsync(branch, context, result, metadata, ct);
    }

    private static async Task<Result<ImmutableList<Episode>, string>> RetrieveSimilarEpisodesInternalAsync(
        QdrantClient qdrantClient,
        IEmbeddingModel embeddingModel,
        string query,
        int topK,
        double minSimilarity,
        string collectionName,
        CancellationToken ct = default)
    {
        var engine = new EpisodicMemoryEngine(qdrantClient, embeddingModel, collectionName);
        return await engine.RetrieveSimilarEpisodesAsync(query, topK, minSimilarity, ct);
    }

    private static Verification.Plan GeneratePlanFromEpisodes(string goal, ImmutableList<Episode> episodes)
    {
        // Extract successful patterns
        var successfulPatterns = episodes
            .Where(ep => ep.SuccessScore > 0.7)
            .SelectMany(ep => ep.LessonsLearned)
            .Distinct()
            .ToList();

        // Extract failed patterns
        var failedPatterns = episodes
            .Where(ep => ep.SuccessScore < 0.3)
            .SelectMany(ep => ep.Result.Errors)
            .Distinct()
            .ToList();

        var description = BuildPlanDescription(goal, successfulPatterns, failedPatterns);
        var actions = GeneratePlanActions(goal, episodes);

        return new Verification.Plan(description, actions);
    }

    private static string BuildPlanDescription(string goal, List<string> successPatterns, List<string> failurePatterns)
    {
        var description = $"Plan for: {goal}\n\n";

        if (successPatterns.Count > 0)
        {
            description += "Successful patterns to follow:\n";
            description += string.Join("\n", successPatterns.Take(3).Select(p => $"- {p}"));
            description += "\n\n";
        }

        if (failurePatterns.Count > 0)
        {
            description += "Patterns to avoid:\n";
            description += string.Join("\n", failurePatterns.Take(3).Select(p => $"- {p}"));
        }

        return description;
    }

    private static ImmutableList<Verification.PlanAction> GeneratePlanActions(string goal, ImmutableList<Episode> episodes)
    {
        var actions = new List<Verification.PlanAction>();
        var successfulEpisodes = episodes.Where(ep => ep.SuccessScore > 0.7).ToList();

        if (successfulEpisodes.Count > 0)
        {
            actions.Add(new Verification.ToolAction("analysis", goal));
            actions.Add(new Verification.ToolAction("execution", goal));
            actions.Add(new Verification.ToolAction("validation", "verify results"));
        }
        else
        {
            actions.Add(new Verification.ToolAction("reasoning", goal));
        }

        return actions.ToImmutableList();
    }
}

/// <summary>
/// Pre-configured episodic memory system with arrow factories.
/// </summary>
public sealed class EpisodicMemorySystem
{
    private readonly QdrantClient _qdrantClient;
    private readonly IEmbeddingModel _embeddingModel;
    private readonly string _collectionName;

    internal EpisodicMemorySystem(QdrantClient qdrantClient, IEmbeddingModel embeddingModel, string collectionName)
    {
        _qdrantClient = qdrantClient;
        _embeddingModel = embeddingModel;
        _collectionName = collectionName;
    }

    /// <summary>
    /// Creates an arrow to store an episode.
    /// </summary>
    public Step<PipelineBranch, PipelineBranch> StoreEpisode(
        ExecutionContext context,
        Outcome result,
        ImmutableDictionary<string, object> metadata)
        => EpisodicMemoryArrows.StoreEpisodeArrow(
            _qdrantClient,
            _embeddingModel,
            context,
            result,
            metadata,
            _collectionName);

    /// <summary>
    /// Creates an arrow to retrieve similar episodes.
    /// </summary>
    public Step<PipelineBranch, PipelineBranch> RetrieveSimilarEpisodes(
        string query,
        int topK = 5,
        double minSimilarity = 0.7)
        => EpisodicMemoryArrows.RetrieveSimilarEpisodesArrow(
            _qdrantClient,
            _embeddingModel,
            query,
            topK,
            minSimilarity,
            _collectionName);

    /// <summary>
    /// Creates an arrow to plan with experience.
    /// </summary>
    public Step<PipelineBranch, (PipelineBranch, Verification.Plan?)> PlanWithExperience(
        string goal,
        int topK = 5)
        => EpisodicMemoryArrows.PlanWithExperienceArrow(
            _qdrantClient,
            _embeddingModel,
            goal,
            topK,
            _collectionName);
}

/// <summary>
/// Event indicating that an episode was stored.
/// </summary>
public sealed record EpisodeStoredEvent(
    Guid Id,
    EpisodeId EpisodeId,
    string Goal,
    bool Success,
    DateTime Timestamp) : PipelineEvent(Id, "EpisodeStored", Timestamp)
{
    /// <summary>
    /// Creates a new EpisodeStoredEvent with auto-generated ID and current timestamp.
    /// </summary>
    public EpisodeStoredEvent(EpisodeId episodeId, string goal, bool success, DateTime timestamp)
        : this(Guid.NewGuid(), episodeId, goal, success, timestamp)
    {
    }
}

/// <summary>
/// Event indicating that episodes were retrieved.
/// </summary>
public sealed record EpisodesRetrievedEvent(
    Guid Id,
    int Count,
    string Query,
    DateTime Timestamp) : PipelineEvent(Id, "EpisodesRetrieved", Timestamp)
{
    /// <summary>
    /// Creates a new EpisodesRetrievedEvent with auto-generated ID and current timestamp.
    /// </summary>
    public EpisodesRetrievedEvent(int count, string query, DateTime timestamp)
        : this(Guid.NewGuid(), count, query, timestamp)
    {
    }
}
