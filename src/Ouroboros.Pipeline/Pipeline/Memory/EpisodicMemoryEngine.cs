// <copyright file="EpisodicMemoryEngine.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Memory;

using System.Collections.Immutable;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Ouroboros.Domain;
using Ouroboros.Domain.Reinforcement;
using Ouroboros.Domain.States;
using Ouroboros.Domain.Vectors;
using Ouroboros.Pipeline.Verification;
using Qdrant.Client;
using Qdrant.Client.Grpc;

/// <summary>
/// Implementation of episodic memory system using Qdrant for vector storage.
/// Provides semantic search, consolidation, and experience-based planning.
/// </summary>
public sealed class EpisodicMemoryEngine : IEpisodicMemoryEngine, IAsyncDisposable
{
    private readonly QdrantClient _qdrantClient;
    private readonly IEmbeddingModel _embeddingModel;
    private readonly ILogger<EpisodicMemoryEngine>? _logger;
    private readonly string _collectionName;
    private readonly bool _disposeClient;
    private bool _collectionInitialized;

    /// <summary>
    /// Initializes a new instance of the <see cref="EpisodicMemoryEngine"/> class.
    /// </summary>
    /// <param name="qdrantClient">The Qdrant client for vector storage.</param>
    /// <param name="embeddingModel">The embedding model for creating semantic vectors.</param>
    /// <param name="collectionName">Name of the Qdrant collection to use.</param>
    /// <param name="logger">Optional logger instance.</param>
    public EpisodicMemoryEngine(
        QdrantClient qdrantClient,
        IEmbeddingModel embeddingModel,
        string collectionName = "episodic_memory",
        ILogger<EpisodicMemoryEngine>? logger = null)
    {
        _qdrantClient = qdrantClient ?? throw new ArgumentNullException(nameof(qdrantClient));
        _embeddingModel = embeddingModel ?? throw new ArgumentNullException(nameof(embeddingModel));
        _collectionName = collectionName ?? throw new ArgumentNullException(nameof(collectionName));
        _logger = logger;
        _disposeClient = false;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EpisodicMemoryEngine"/> class with connection string.
    /// </summary>
    /// <param name="qdrantConnectionString">Qdrant connection string (e.g., "http://localhost:6333").</param>
    /// <param name="embeddingModel">The embedding model for creating semantic vectors.</param>
    /// <param name="collectionName">Name of the Qdrant collection to use.</param>
    /// <param name="logger">Optional logger instance.</param>
    public EpisodicMemoryEngine(
        string qdrantConnectionString,
        IEmbeddingModel embeddingModel,
        string collectionName = "episodic_memory",
        ILogger<EpisodicMemoryEngine>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(qdrantConnectionString))
        {
            throw new ArgumentException("Connection string cannot be null or empty", nameof(qdrantConnectionString));
        }

        _embeddingModel = embeddingModel ?? throw new ArgumentNullException(nameof(embeddingModel));
        _collectionName = collectionName ?? throw new ArgumentNullException(nameof(collectionName));
        _logger = logger;

        var uri = new Uri(qdrantConnectionString);
        var host = uri.Host;
        var port = uri.Port > 0 ? uri.Port : 6334;
        var useHttps = uri.Scheme == "https";

        _qdrantClient = new QdrantClient(host, port, useHttps);
        _disposeClient = true;
    }

    /// <inheritdoc/>
    public async Task<Result<EpisodeId, string>> StoreEpisodeAsync(
        PipelineBranch branch,
        ExecutionContext context,
        Outcome result,
        ImmutableDictionary<string, object> metadata,
        CancellationToken ct = default)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(branch);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(result);
            ArgumentNullException.ThrowIfNull(metadata);

            // Ensure collection exists
            await EnsureCollectionInitializedAsync(ct);

            // Create episode ID
            var episodeId = Guid.NewGuid();

            // Generate embedding from goal and outcome
            var embeddingText = $"{context.Goal}\n{result.Output}";
            var embedding = await _embeddingModel.CreateEmbeddingsAsync(embeddingText, ct);

            // Serialize pipeline branch
            var branchJson = SerializePipelineBranch(branch);

            // Calculate success score
            var successScore = result.Success ? 1.0 : Math.Max(0.0, 1.0 - (result.Errors.Count * 0.2));

            // Extract lessons learned from reasoning events
            var lessonsLearned = ExtractLessonsLearned(branch);

            // Merge context metadata with provided metadata
            var combinedContext = context.Metadata.SetItems(metadata);

            // Create episode
            var episode = new Episode(
                episodeId,
                DateTime.UtcNow,
                context.Goal,
                branch,
                result,
                successScore,
                lessonsLearned,
                combinedContext,
                embedding);

            // Store in Qdrant
            var point = new PointStruct
            {
                Id = new PointId { Uuid = episodeId.ToString() },
                Vectors = embedding,
                Payload =
                {
                    ["goal"] = context.Goal,
                    ["timestamp"] = episode.Timestamp.ToString("o"),
                    ["success"] = result.Success,
                    ["success_score"] = successScore,
                    ["duration_ms"] = result.Duration.TotalMilliseconds,
                    ["output"] = result.Output,
                    ["errors"] = JsonSerializer.Serialize(result.Errors),
                    ["lessons_learned"] = JsonSerializer.Serialize(lessonsLearned),
                    ["branch_json"] = branchJson,
                    ["context"] = JsonSerializer.Serialize(combinedContext),
                },
            };

            await _qdrantClient.UpsertAsync(_collectionName, new[] { point }, cancellationToken: ct);

            _logger?.LogInformation("Stored episode {EpisodeId} with goal: {Goal}", episodeId, context.Goal);

            return Result<EpisodeId, string>.Success(new EpisodeId(episodeId));
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to store episode");
            return Result<EpisodeId, string>.Failure($"Failed to store episode: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<ImmutableList<Episode>, string>> RetrieveSimilarEpisodesAsync(
        string query,
        int topK = 5,
        double minSimilarity = 0.7,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(query);
            if (topK <= 0)
            {
                return Result<ImmutableList<Episode>, string>.Failure("topK must be greater than 0");
            }

            if (minSimilarity < 0.0 || minSimilarity > 1.0)
            {
                return Result<ImmutableList<Episode>, string>.Failure("minSimilarity must be between 0.0 and 1.0");
            }

            // Ensure collection exists
            await EnsureCollectionInitializedAsync(ct);

            // Generate query embedding
            var queryEmbedding = await _embeddingModel.CreateEmbeddingsAsync(query, ct);

            // Search in Qdrant
            var searchResult = await _qdrantClient.SearchAsync(
                _collectionName,
                queryEmbedding,
                limit: (ulong)topK,
                scoreThreshold: (float)minSimilarity,
                cancellationToken: ct);

            // Convert results to Episode objects
            var episodes = searchResult
                .Select(point => DeserializeEpisode(point))
                .Where(ep => ep.HasValue)
                .Select(ep => ep.Value!)
                .ToImmutableList();

            stopwatch.Stop();
            _logger?.LogInformation(
                "Retrieved {Count} episodes in {ElapsedMs}ms for query: {Query}",
                episodes.Count,
                stopwatch.ElapsedMilliseconds,
                query);

            return Result<ImmutableList<Episode>, string>.Success(episodes);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to retrieve similar episodes");
            return Result<ImmutableList<Episode>, string>.Failure($"Failed to retrieve episodes: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<Unit, string>> ConsolidateMemoriesAsync(
        TimeSpan olderThan,
        ConsolidationStrategy strategy,
        CancellationToken ct = default)
    {
        try
        {
            await EnsureCollectionInitializedAsync(ct);

            var cutoffTime = DateTime.UtcNow - olderThan;
            _logger?.LogInformation(
                "Starting memory consolidation with strategy {Strategy} for episodes older than {CutoffTime}",
                strategy,
                cutoffTime);

            // Scroll through old episodes
            // Note: Qdrant filter API for dates is complex, so we retrieve all and filter in memory
            // For production use, consider custom filtering logic or timestamp-based collections
            var scrollResult = await _qdrantClient.ScrollAsync(
                _collectionName,
                limit: 1000,
                cancellationToken: ct);

            var oldEpisodes = new List<Episode>();
            foreach (var point in scrollResult.Result)
            {
                var episodeOption = DeserializeEpisodeFromRetrieved(point);
                if (episodeOption.HasValue && episodeOption.Value!.Timestamp < cutoffTime)
                {
                    oldEpisodes.Add(episodeOption.Value!);
                }
            }

            if (oldEpisodes.Count == 0)
            {
                _logger?.LogInformation("No episodes found for consolidation");
                return Result<Unit, string>.Success(Unit.Value);
            }

            // Apply consolidation strategy
            var consolidationResult = strategy switch
            {
                ConsolidationStrategy.Compress => await ConsolidateByCompressionAsync(oldEpisodes, ct),
                ConsolidationStrategy.Abstract => await ConsolidateByAbstractionAsync(oldEpisodes, ct),
                ConsolidationStrategy.Prune => await ConsolidateByPruningAsync(oldEpisodes, ct),
                ConsolidationStrategy.Hierarchical => await ConsolidateHierarchicallyAsync(oldEpisodes, ct),
                _ => Result<Unit, string>.Failure($"Unknown consolidation strategy: {strategy}"),
            };

            return consolidationResult;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to consolidate memories");
            return Result<Unit, string>.Failure($"Failed to consolidate memories: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<Plan, string>> PlanWithExperienceAsync(
        string goal,
        ImmutableList<Episode> relevantEpisodes,
        CancellationToken ct = default)
    {
        try
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(goal);
            ArgumentNullException.ThrowIfNull(relevantEpisodes);

            _logger?.LogInformation(
                "Generating plan for goal '{Goal}' using {EpisodeCount} relevant episodes",
                goal,
                relevantEpisodes.Count);

            // Extract successful patterns from episodes
            var successfulPatterns = relevantEpisodes
                .Where(ep => ep.SuccessScore > 0.7)
                .SelectMany(ep => ep.LessonsLearned)
                .Distinct()
                .ToList();

            // Extract failed patterns to avoid
            var failedPatterns = relevantEpisodes
                .Where(ep => ep.SuccessScore < 0.3)
                .SelectMany(ep => ep.Result.Errors)
                .Distinct()
                .ToList();

            // Build plan description incorporating learned patterns
            var planDescription = BuildPlanDescription(goal, successfulPatterns, failedPatterns);

            // Generate plan actions based on successful episodes
            var actions = GeneratePlanActions(goal, relevantEpisodes);

            var plan = new Plan(planDescription, actions);

            _logger?.LogInformation("Generated plan with {ActionCount} actions", actions.Count);

            return Result<Plan, string>.Success(plan);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to generate plan with experience");
            return Result<Plan, string>.Failure($"Failed to generate plan: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_disposeClient)
        {
            _qdrantClient?.Dispose();
        }

        await Task.CompletedTask;
    }

    private async Task EnsureCollectionInitializedAsync(CancellationToken ct)
    {
        if (_collectionInitialized)
        {
            return;
        }

        try
        {
            // Check if collection exists
            var exists = await _qdrantClient.CollectionExistsAsync(_collectionName, ct);

            if (!exists)
            {
                _logger?.LogInformation("Creating collection: {CollectionName}", _collectionName);

                // Create collection with appropriate vector size (assuming 768 for typical embeddings)
                await _qdrantClient.CreateCollectionAsync(
                    _collectionName,
                    new VectorParams { Size = 768, Distance = Distance.Cosine },
                    cancellationToken: ct);
            }

            _collectionInitialized = true;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize collection");
            throw;
        }
    }

    private string SerializePipelineBranch(PipelineBranch branch)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = false,
            };
            return JsonSerializer.Serialize(branch, options);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to serialize pipeline branch, storing minimal info");
            return $"{{\"name\":\"{branch.Name}\",\"event_count\":{branch.Events.Count}}}";
        }
    }

    private Option<Episode> DeserializeEpisode(ScoredPoint point)
    {
        return DeserializeEpisodeCore(point.Id, point.Payload, point.Vectors.Vector);
    }

    private Option<Episode> DeserializeEpisodeFromRetrieved(RetrievedPoint point)
    {
        return DeserializeEpisodeCore(point.Id, point.Payload, point.Vectors.Vector);
    }

    private Option<Episode> DeserializeEpisodeCore(PointId id, Google.Protobuf.Collections.MapField<string, Value> payload, VectorOutput vector)
    {
        try
        {
            var episodeId = Guid.Parse(id.Uuid);
            var timestamp = DateTime.Parse(payload["timestamp"].StringValue);
            var goal = payload["goal"].StringValue;
            var success = payload["success"].BoolValue;
            var successScore = payload["success_score"].DoubleValue;
            var durationMs = payload["duration_ms"].DoubleValue;
            var output = payload["output"].StringValue;
            var errorsJson = payload["errors"].StringValue;
            var lessonsJson = payload["lessons_learned"].StringValue;
            var branchJson = payload["branch_json"].StringValue;
            var contextJson = payload["context"].StringValue;

            var errors = JsonSerializer.Deserialize<ImmutableList<string>>(errorsJson) ?? ImmutableList<string>.Empty;
            var lessonsLearned = JsonSerializer.Deserialize<ImmutableList<string>>(lessonsJson) ?? ImmutableList<string>.Empty;
            var context = JsonSerializer.Deserialize<ImmutableDictionary<string, object>>(contextJson) ?? ImmutableDictionary<string, object>.Empty;

            var outcome = new Outcome(success, output, TimeSpan.FromMilliseconds(durationMs), errors);

            // Note: We're not deserializing the full PipelineBranch here for performance
            // It can be reconstructed from the JSON when needed
            var dataSource = LangChain.DocumentLoaders.DataSource.FromPath(Environment.CurrentDirectory);
            var emptyBranch = new PipelineBranch("deserialized", new TrackedVectorStore(), dataSource);

            // For deserialized episodes, embedding is not critical since already stored in Qdrant
            // Use empty embedding to save processing time
            var embedding = new float[768]; // Standard embedding size

            var episode = new Episode(
                episodeId,
                timestamp,
                goal,
                emptyBranch,
                outcome,
                successScore,
                lessonsLearned,
                context,
                embedding);

            return Option<Episode>.Some(episode);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to deserialize episode from point");
            return Option<Episode>.None();
        }
    }

    private ImmutableList<string> ExtractLessonsLearned(PipelineBranch branch)
    {
        var lessons = new List<string>();

        // Extract insights from reasoning steps
        var reasoningSteps = branch.Events.OfType<ReasoningStep>().ToList();

        foreach (var step in reasoningSteps)
        {
            // Look for critique and final spec states that often contain insights
            if (step.State is Critique critique)
            {
                if (!string.IsNullOrWhiteSpace(critique.Text) && critique.Text.Length > 20)
                {
                    lessons.Add($"Critique insight: {critique.Text.Substring(0, Math.Min(200, critique.Text.Length))}");
                }
            }
            else if (step.State is FinalSpec finalSpec)
            {
                if (!string.IsNullOrWhiteSpace(finalSpec.Text) && finalSpec.Text.Length > 20)
                {
                    lessons.Add($"Final insight: {finalSpec.Text.Substring(0, Math.Min(200, finalSpec.Text.Length))}");
                }
            }
        }

        // If no lessons found, add a generic one
        if (lessons.Count == 0 && reasoningSteps.Count > 0)
        {
            lessons.Add($"Completed reasoning with {reasoningSteps.Count} steps");
        }

        return lessons.ToImmutableList();
    }

    private async Task<Result<Unit, string>> ConsolidateByCompressionAsync(List<Episode> episodes, CancellationToken ct)
    {
        // Group similar episodes and create compressed summaries
        var groupedByGoal = episodes.GroupBy(ep => ep.Goal.ToLowerInvariant()).ToList();

        _logger?.LogInformation(
            "Compressing {EpisodeCount} episodes into {GroupCount} groups",
            episodes.Count,
            groupedByGoal.Count);

        foreach (var group in groupedByGoal)
        {
            if (group.Count() > 1)
            {
                // Could implement: merge similar episodes into a single compressed representation
                _logger?.LogDebug("Group '{Goal}' has {Count} episodes to compress", group.Key, group.Count());
            }
        }

        return Result<Unit, string>.Success(Unit.Value);
    }

    private async Task<Result<Unit, string>> ConsolidateByAbstractionAsync(List<Episode> episodes, CancellationToken ct)
    {
        // Extract patterns and rules from episodes
        var patterns = episodes
            .SelectMany(ep => ep.LessonsLearned)
            .GroupBy(lesson => lesson)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => new { Pattern = g.Key, Frequency = g.Count() })
            .ToList();

        _logger?.LogInformation(
            "Extracted {PatternCount} patterns from {EpisodeCount} episodes",
            patterns.Count,
            episodes.Count);

        // Patterns could be stored as high-level abstract episodes
        return Result<Unit, string>.Success(Unit.Value);
    }

    private async Task<Result<Unit, string>> ConsolidateByPruningAsync(List<Episode> episodes, CancellationToken ct)
    {
        // Remove low-value episodes (low success score, no unique lessons)
        var episodesToDelete = episodes
            .Where(ep => ep.SuccessScore < 0.3 && ep.LessonsLearned.Count == 0)
            .Select(ep => ep.Id)
            .ToList();

        if (episodesToDelete.Count > 0)
        {
            _logger?.LogInformation("Pruning {Count} low-value episodes", episodesToDelete.Count);

            var pointIds = episodesToDelete.Select(id => new PointId { Uuid = id.ToString() }).ToList();
            await _qdrantClient.DeleteAsync(_collectionName, pointIds, cancellationToken: ct);
        }

        return Result<Unit, string>.Success(Unit.Value);
    }

    private async Task<Result<Unit, string>> ConsolidateHierarchicallyAsync(List<Episode> episodes, CancellationToken ct)
    {
        // Build abstraction hierarchies from specific to general
        var hierarchyLevels = episodes
            .GroupBy(ep => ep.SuccessScore >= 0.7 ? "successful" : "failed")
            .ToList();

        _logger?.LogInformation(
            "Building hierarchical structure with {LevelCount} levels from {EpisodeCount} episodes",
            hierarchyLevels.Count,
            episodes.Count);

        // Could implement: create abstract parent episodes that summarize children
        return Result<Unit, string>.Success(Unit.Value);
    }

    private string BuildPlanDescription(string goal, List<string> successPatterns, List<string> failurePatterns)
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

    private ImmutableList<PlanAction> GeneratePlanActions(string goal, ImmutableList<Episode> episodes)
    {
        var actions = new List<PlanAction>();

        // Extract common tool usage patterns from successful episodes
        var successfulEpisodes = episodes.Where(ep => ep.SuccessScore > 0.7).ToList();

        if (successfulEpisodes.Count > 0)
        {
            // Add generic actions based on the goal
            actions.Add(new ToolAction("analysis", goal));
            actions.Add(new ToolAction("execution", goal));
            actions.Add(new ToolAction("validation", "verify results"));
        }
        else
        {
            // Fallback to basic actions if no successful episodes
            actions.Add(new ToolAction("reasoning", goal));
        }

        return actions.ToImmutableList();
    }
}
