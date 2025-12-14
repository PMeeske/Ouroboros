// <copyright file="IntelligentToolLearner.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using LangChainPipeline.Agent.MetaAI;
using LangChainPipeline.Genetic.Abstractions;
using LangChainPipeline.Genetic.Core;
using LangChainPipeline.Providers;
using Ouroboros.Tools.MeTTa;
using Ouroboros.Tools;
using Qdrant.Client;
using Qdrant.Client.Grpc;

/// <summary>
/// Configuration for a dynamically optimized tool.
/// Used as the gene type for genetic algorithm evolution.
/// </summary>
public sealed record ToolConfiguration(
    string Name,
    string Description,
    string? SearchProvider,
    double TimeoutSeconds,
    int MaxRetries,
    bool CacheResults,
    string? CustomParameters)
{
    /// <summary>
    /// Creates a default configuration.
    /// </summary>
    public static ToolConfiguration Default(string name, string description) =>
        new(name, description, null, 30.0, 3, true, null);
}

/// <summary>
/// Represents a learned tool pattern stored in Qdrant.
/// </summary>
public sealed record LearnedToolPattern(
    string Id,
    string Goal,
    string ToolName,
    ToolConfiguration Configuration,
    double SuccessRate,
    int UsageCount,
    DateTime CreatedAt,
    DateTime LastUsed,
    List<string> RelatedGoals);

/// <summary>
/// Intelligent tool learning system that combines:
/// - MeTTa semantic reasoning for tool understanding and selection
/// - Genetic algorithm optimization for tool configurations
/// - Qdrant vector storage for persistent pattern learning
/// </summary>
public sealed class IntelligentToolLearner : IAsyncDisposable
{
    private readonly DynamicToolFactory _toolFactory;
    private readonly IMeTTaEngine _mettaEngine;
    private readonly MeTTaRepresentation _mettaRepresentation;
    private readonly IEmbeddingModel _embedding;
    private readonly QdrantClient _qdrantClient;
    private readonly ToolAwareChatModel _llm;
    private readonly ConcurrentDictionary<string, LearnedToolPattern> _patternCache = new();
    private readonly SemaphoreSlim _syncLock = new(1, 1);
    private bool _isInitialized;
    private int _vectorSize = 32; // Will be detected from embedding model

    private const string CollectionName = "ouroboros_tool_patterns";

    /// <summary>
    /// Initializes a new instance of the <see cref="IntelligentToolLearner"/> class.
    /// </summary>
    public IntelligentToolLearner(
        DynamicToolFactory toolFactory,
        IMeTTaEngine mettaEngine,
        IEmbeddingModel embedding,
        ToolAwareChatModel llm,
        string qdrantUrl = "http://localhost:6334")
    {
        _toolFactory = toolFactory ?? throw new ArgumentNullException(nameof(toolFactory));
        _mettaEngine = mettaEngine ?? throw new ArgumentNullException(nameof(mettaEngine));
        _embedding = embedding ?? throw new ArgumentNullException(nameof(embedding));
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _mettaRepresentation = new MeTTaRepresentation(mettaEngine);

        var uri = new Uri(qdrantUrl);
        _qdrantClient = new QdrantClient(uri.Host, uri.Port > 0 ? uri.Port : 6334, uri.Scheme == "https");
    }

    /// <summary>
    /// Initializes the learner, loading existing patterns from Qdrant.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_isInitialized) return;

        await _syncLock.WaitAsync(ct);
        try
        {
            if (_isInitialized) return;

            // Detect vector size from embedding model
            await DetectVectorSizeAsync(ct);

            // Ensure collection exists with correct dimensions
            await EnsureCollectionExistsAsync(ct);

            // Load existing patterns into cache
            await LoadPatternsFromQdrantAsync(ct);

            // Add tool knowledge to MeTTa
            await InitializeMeTTaKnowledgeAsync(ct);

            _isInitialized = true;
        }
        finally
        {
            _syncLock.Release();
        }
    }

    /// <summary>
    /// Finds or creates the best tool for a given goal using intelligent matching.
    /// Uses MeTTa for semantic understanding, checks Qdrant for existing patterns,
    /// and optionally evolves new configurations using genetic algorithms.
    /// </summary>
    /// <param name="goal">The user's goal (e.g., "search for Python tutorials").</param>
    /// <param name="registry">The tool registry to check for existing tools.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The best matching or newly created tool.</returns>
    public async Task<Result<(ITool Tool, bool WasCreated), string>> FindOrCreateToolAsync(
        string goal,
        ToolRegistry registry,
        CancellationToken ct = default)
    {
        if (!_isInitialized)
            await InitializeAsync(ct);

        try
        {
            // Step 1: Check Qdrant for existing learned patterns
            var existingPattern = await FindMatchingPatternAsync(goal, ct);
            if (existingPattern != null)
            {
                // Secondary check: verify the tool name is semantically related to the goal
                // This prevents generic embeddings from matching unrelated tools
                if (!IsToolRelevantToGoal(existingPattern.ToolName, goal))
                {
                    // Tool name doesn't seem related to goal - skip this pattern
                    existingPattern = null;
                }
            }

            if (existingPattern != null)
            {
                // Found a pattern - retrieve or recreate the tool
                var existingTool = registry.Get(existingPattern.ToolName);
                if (existingTool != null)
                {
                    await RecordPatternUsageAsync(existingPattern.Id, true, ct);
                    return Result<(ITool, bool), string>.Success((existingTool, false));
                }

                // Tool doesn't exist but we have the config - recreate it
                var recreateResult = await RecreateToolFromPatternAsync(existingPattern, registry, ct);
                if (recreateResult.IsSuccess)
                {
                    return Result<(ITool, bool), string>.Success((recreateResult.Value, false));
                }
            }

            // Step 2: Use MeTTa to reason about what tool is needed
            var toolAnalysis = await AnalyzeGoalWithMeTTaAsync(goal, ct);

            // Step 3: Check if we already have a suitable tool
            var recommendedTools = await _mettaRepresentation.QueryToolsForGoalAsync(goal, ct);
            if (recommendedTools.IsSuccess && recommendedTools.Value.Count > 0)
            {
                foreach (var recToolName in recommendedTools.Value)
                {
                    // Verify tool name is relevant before accepting
                    if (!IsToolRelevantToGoal(recToolName, goal))
                        continue;

                    var existingTool = registry.Get(recToolName);
                    if (existingTool != null)
                    {
                        return Result<(ITool, bool), string>.Success((existingTool, false));
                    }
                }
            }

            // Step 4: Create a new tool with optimized configuration
            var (inferredToolName, description) = await InferToolSpecFromGoalAsync(goal, toolAnalysis, ct);

            // Step 5: Evolve optimal configuration using genetic algorithm
            var optimalConfig = await EvolveOptimalConfigurationAsync(inferredToolName, description, goal, ct);

            // Step 6: Create the tool
            var createResult = await _toolFactory.CreateToolAsync(inferredToolName, description, ct);
            if (!createResult.IsSuccess)
            {
                return Result<(ITool, bool), string>.Failure($"Failed to create tool: {createResult.Error}");
            }

            var newTool = createResult.Value;
            // Note: ToolRegistry is immutable, caller is responsible for updating registry

            // Step 7: Persist the learned pattern to Qdrant
            await PersistPatternAsync(goal, inferredToolName, optimalConfig, ct);

            // Step 8: Add to MeTTa knowledge base
            await _mettaRepresentation.TranslateToolsAsync(registry.WithTool(newTool), ct);

            return Result<(ITool, bool), string>.Success((newTool, true));
        }
        catch (Exception ex)
        {
            return Result<(ITool, bool), string>.Failure($"Intelligent tool creation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Uses genetic algorithm to evolve optimal tool configuration.
    /// </summary>
    private async Task<ToolConfiguration> EvolveOptimalConfigurationAsync(
        string toolName,
        string description,
        string goal,
        CancellationToken ct = default)
    {
        try
        {
            // Create initial population of configurations
            var initialPopulation = CreateInitialConfigurationPopulation(toolName, description, 10);

            // Define fitness function based on goal alignment
            var fitnessFunction = new ToolConfigurationFitness(_llm, goal);

            // Create genetic algorithm
            var ga = new GeneticAlgorithm<ToolConfigurationGene>(
                fitnessFunction,
                MutateGene,
                mutationRate: 0.15,
                crossoverRate: 0.7,
                elitismRate: 0.2);

            // Evolve over a few generations (keep it fast for interactive use)
            var result = await ga.EvolveAsync(initialPopulation, generations: 5);

            if (result.IsSuccess)
            {
                var bestChromosome = result.Value as ToolConfigurationChromosome;
                return bestChromosome?.ToConfiguration() ?? ToolConfiguration.Default(toolName, description);
            }

            // Fall back to default configuration
            return ToolConfiguration.Default(toolName, description);
        }
        catch (Exception)
        {
            // Fall back to default configuration on any error
            return ToolConfiguration.Default(toolName, description);
        }
    }

    /// <summary>
    /// Creates initial population of tool configurations for genetic algorithm.
    /// </summary>
    private List<IChromosome<ToolConfigurationGene>> CreateInitialConfigurationPopulation(
        string toolName,
        string description,
        int size)
    {
        var random = new Random();
        var population = new List<IChromosome<ToolConfigurationGene>>();

        string[] providers = { "duckduckgo", "google", "bing", null! };
        double[] timeouts = { 10.0, 20.0, 30.0, 45.0, 60.0 };
        int[] retries = { 1, 2, 3, 5 };

        for (int i = 0; i < size; i++)
        {
            var genes = new List<ToolConfigurationGene>
            {
                new("name", toolName),
                new("description", description),
                new("searchProvider", providers[random.Next(providers.Length)]),
                new("timeout", timeouts[random.Next(timeouts.Length)].ToString()),
                new("retries", retries[random.Next(retries.Length)].ToString()),
                new("cacheResults", random.NextDouble() > 0.3 ? "true" : "false"),
                new("customParams", null)
            };

            population.Add(new ToolConfigurationChromosome(genes));
        }

        return population;
    }

    /// <summary>
    /// Mutation function for genetic algorithm.
    /// </summary>
    private static ToolConfigurationGene MutateGene(ToolConfigurationGene gene)
    {
        var random = new Random();

        return gene.Key switch
        {
            "timeout" => new ToolConfigurationGene(gene.Key,
                (double.Parse(gene.Value ?? "30") * (0.8 + random.NextDouble() * 0.4)).ToString("F1")),
            "retries" => new ToolConfigurationGene(gene.Key,
                Math.Max(1, int.Parse(gene.Value ?? "3") + random.Next(-1, 2)).ToString()),
            "cacheResults" => new ToolConfigurationGene(gene.Key,
                random.NextDouble() > 0.5 ? "true" : "false"),
            "searchProvider" => new ToolConfigurationGene(gene.Key,
                new[] { "duckduckgo", "google", "bing" }[random.Next(3)]),
            _ => gene
        };
    }

    /// <summary>
    /// Analyzes a goal using MeTTa symbolic reasoning.
    /// </summary>
    private async Task<string> AnalyzeGoalWithMeTTaAsync(string goal, CancellationToken ct)
    {
        try
        {
            // Add the goal to MeTTa for reasoning
            string goalAtom = $"(goal current-goal \"{EscapeMeTTa(goal)}\")";
            await _mettaEngine.AddFactAsync(goalAtom, ct);

            // Query for capabilities needed
            string query = @"!(match &self
                (and
                    (goal current-goal $goal)
                    (or
                        (contains $goal ""search"")
                        (contains $goal ""find"")
                        (contains $goal ""fetch"")
                        (contains $goal ""calculate"")
                    )
                )
                $goal)";

            var result = await _mettaEngine.ExecuteQueryAsync(query, ct);
            return result.IsSuccess ? result.Value : goal;
        }
        catch
        {
            return goal;
        }
    }

    /// <summary>
    /// Infers tool specification from goal using LLM.
    /// </summary>
    private async Task<(string Name, string Description)> InferToolSpecFromGoalAsync(
        string goal,
        string mettaAnalysis,
        CancellationToken ct)
    {
        string prompt = $@"Based on this user goal, suggest a tool name and description.
Goal: {goal}
Analysis: {mettaAnalysis}

Respond in JSON format:
{{
  ""toolName"": ""snake_case_name"",
  ""description"": ""What the tool does""
}}";

        try
        {
            string response = await _llm.InnerModel.GenerateTextAsync(prompt, ct);

            // Parse JSON response
            var match = System.Text.RegularExpressions.Regex.Match(
                response,
                @"\{[^}]+""toolName""\s*:\s*""([^""]+)""[^}]+""description""\s*:\s*""([^""]+)""[^}]*\}",
                System.Text.RegularExpressions.RegexOptions.Singleline);

            if (match.Success)
            {
                return (match.Groups[1].Value, match.Groups[2].Value);
            }

            // Fallback: generate from goal
            string safeName = goal.ToLowerInvariant()
                .Replace(" ", "_")
                .Replace("-", "_");
            safeName = System.Text.RegularExpressions.Regex.Replace(safeName, @"[^a-z0-9_]", string.Empty);
            if (safeName.Length > 30) safeName = safeName[..30];

            return (safeName + "_tool", $"Tool to accomplish: {goal}");
        }
        catch
        {
            return ("dynamic_tool", goal);
        }
    }

    /// <summary>
    /// Minimum similarity score threshold for pattern matching.
    /// Patterns with similarity below this value are considered unrelated.
    /// </summary>
    private const float MinimumPatternSimilarityThreshold = 0.75f;

    /// <summary>
    /// Finds a matching learned pattern in Qdrant using semantic search.
    /// </summary>
    private async Task<LearnedToolPattern?> FindMatchingPatternAsync(string goal, CancellationToken ct)
    {
        try
        {
            // First check cache - require exact goal match or high substring overlap
            var cachedMatch = _patternCache.Values
                .Where(p => p.Goal.Equals(goal, StringComparison.OrdinalIgnoreCase) ||
                           (p.RelatedGoals.Any(g => g.Equals(goal, StringComparison.OrdinalIgnoreCase)) ||
                            p.RelatedGoals.Any(g => ComputeOverlapRatio(g, goal) >= 0.6)))
                .OrderByDescending(p => p.SuccessRate)
                .FirstOrDefault();

            if (cachedMatch != null)
                return cachedMatch;

            // Semantic search in Qdrant
            var embedding = await _embedding.CreateEmbeddingsAsync(goal);

            var collectionExists = await _qdrantClient.CollectionExistsAsync(CollectionName, ct);
            if (!collectionExists) return null;

            var searchResults = await _qdrantClient.SearchAsync(
                CollectionName,
                embedding,
                limit: 3,
                cancellationToken: ct);

            foreach (var result in searchResults)
            {
                if (result.Score < MinimumPatternSimilarityThreshold) continue; // Threshold for similarity

                if (result.Payload.TryGetValue("pattern_json", out var jsonValue))
                {
                    var pattern = JsonSerializer.Deserialize<LearnedToolPattern>(jsonValue.StringValue);
                    if (pattern != null)
                    {
                        _patternCache[pattern.Id] = pattern;
                        return pattern;
                    }
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] Pattern search failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Computes word overlap ratio between two strings (Jaccard-like similarity).
    /// Returns a value between 0 and 1 representing the proportion of shared words.
    /// </summary>
    private static double ComputeOverlapRatio(string a, string b)
    {
        var wordsA = a.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var wordsB = b.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();

        if (wordsA.Count == 0 || wordsB.Count == 0)
            return 0.0;

        int intersection = wordsA.Intersect(wordsB).Count();
        int union = wordsA.Union(wordsB).Count();

        return union > 0 ? (double)intersection / union : 0.0;
    }

    /// <summary>
    /// Determines if a tool name is semantically relevant to the given goal.
    /// Prevents false matches where embeddings are similar but topics differ.
    /// </summary>
    /// <param name="toolName">The tool name (e.g., "net_10_migration_assistant").</param>
    /// <param name="goal">The user's goal (e.g., "learn to fly").</param>
    /// <returns>True if the tool seems relevant to the goal.</returns>
    private static bool IsToolRelevantToGoal(string toolName, string goal)
    {
        // Normalize: split tool name on underscores and goal on spaces
        var toolWords = toolName.ToLowerInvariant()
            .Replace("_", " ")
            .Replace("-", " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ToHashSet();

        var goalWords = goal.ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2) // Skip small words like "to", "a", etc.
            .ToHashSet();

        // Check for any meaningful word overlap
        var intersection = toolWords.Intersect(goalWords).ToList();
        if (intersection.Count > 0)
            return true;

        // Check for semantic keyword groups that should match
        var technicalKeywords = new HashSet<string> { "net", "dotnet", "migration", "upgrade", "version", "framework", "code", "assistant", "api", "sdk" };
        var learnKeywords = new HashSet<string> { "learn", "study", "understand", "know", "master", "skill", "tutorial", "course" };
        var flightKeywords = new HashSet<string> { "fly", "flight", "aviation", "pilot", "plane", "aircraft", "air" };
        var independenceKeywords = new HashSet<string> { "independent", "autonomy", "autonomous", "self", "freedom", "free" };

        // If tool is technical but goal is about learning/flying/independence, they don't match
        bool toolIsTechnical = toolWords.Intersect(technicalKeywords).Any();
        bool goalIsLearn = goalWords.Intersect(learnKeywords).Any();
        bool goalIsFlight = goalWords.Intersect(flightKeywords).Any();
        bool goalIsIndependence = goalWords.Intersect(independenceKeywords).Any();

        // Mismatch: technical tool for non-technical goal
        if (toolIsTechnical && (goalIsFlight || goalIsIndependence))
            return false;

        // If no clear mismatch found and the overlap ratio is at least minimal
        return ComputeOverlapRatio(toolName.Replace("_", " "), goal) >= 0.15;
    }

    /// <summary>
    /// Persists a learned tool pattern to Qdrant.
    /// </summary>
    private async Task PersistPatternAsync(
        string goal,
        string toolName,
        ToolConfiguration config,
        CancellationToken ct)
    {
        try
        {
            var pattern = new LearnedToolPattern(
                Id: Guid.NewGuid().ToString("N"),
                Goal: goal,
                ToolName: toolName,
                Configuration: config,
                SuccessRate: 1.0,
                UsageCount: 1,
                CreatedAt: DateTime.UtcNow,
                LastUsed: DateTime.UtcNow,
                RelatedGoals: new List<string>());

            // Add to cache
            _patternCache[pattern.Id] = pattern;

            // Create embedding for the goal
            var embedding = await _embedding.CreateEmbeddingsAsync(goal);

            // Store in Qdrant
            var point = new PointStruct
            {
                Id = new PointId { Uuid = pattern.Id },
                Vectors = embedding,
                Payload =
                {
                    ["goal"] = goal,
                    ["tool_name"] = toolName,
                    ["pattern_json"] = JsonSerializer.Serialize(pattern),
                    ["created_at"] = pattern.CreatedAt.ToString("O")
                }
            };

            await _qdrantClient.UpsertAsync(CollectionName, new[] { point }, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] Failed to persist pattern: {ex.Message}");
        }
    }

    /// <summary>
    /// Records usage of a pattern for learning optimization.
    /// </summary>
    private async Task RecordPatternUsageAsync(string patternId, bool success, CancellationToken ct)
    {
        try
        {
            if (_patternCache.TryGetValue(patternId, out var pattern))
            {
                int newCount = pattern.UsageCount + 1;
                double newSuccessRate = ((pattern.SuccessRate * pattern.UsageCount) + (success ? 1.0 : 0.0)) / newCount;

                var updatedPattern = pattern with
                {
                    UsageCount = newCount,
                    SuccessRate = newSuccessRate,
                    LastUsed = DateTime.UtcNow
                };

                _patternCache[patternId] = updatedPattern;

                // Update in Qdrant
                var embedding = await _embedding.CreateEmbeddingsAsync(pattern.Goal);
                var point = new PointStruct
                {
                    Id = new PointId { Uuid = patternId },
                    Vectors = embedding,
                    Payload =
                    {
                        ["goal"] = pattern.Goal,
                        ["tool_name"] = pattern.ToolName,
                        ["pattern_json"] = JsonSerializer.Serialize(updatedPattern),
                        ["created_at"] = pattern.CreatedAt.ToString("O"),
                        ["success_rate"] = newSuccessRate
                    }
                };

                await _qdrantClient.UpsertAsync(CollectionName, new[] { point }, cancellationToken: ct);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] Failed to record pattern usage: {ex.Message}");
        }
    }

    /// <summary>
    /// Recreates a tool from a stored pattern.
    /// </summary>
    private async Task<Result<ITool, string>> RecreateToolFromPatternAsync(
        LearnedToolPattern pattern,
        ToolRegistry registry,
        CancellationToken ct)
    {
        var result = await _toolFactory.CreateToolAsync(
            pattern.ToolName,
            pattern.Configuration.Description,
            ct);

        // Note: Caller is responsible for updating registry since it's immutable

        return result;
    }

    /// <summary>
    /// Detects vector dimension from embedding model.
    /// </summary>
    private async Task DetectVectorSizeAsync(CancellationToken ct)
    {
        try
        {
            var testEmbedding = await _embedding.CreateEmbeddingsAsync("test", ct);
            if (testEmbedding.Length > 0)
            {
                _vectorSize = testEmbedding.Length;
            }
        }
        catch
        {
            // Keep default vector size
        }
    }

    /// <summary>
    /// Ensures the Qdrant collection exists with correct dimensions.
    /// </summary>
    private async Task EnsureCollectionExistsAsync(CancellationToken ct)
    {
        try
        {
            var exists = await _qdrantClient.CollectionExistsAsync(CollectionName, ct);
            if (exists)
            {
                // Check if dimension matches - use same pattern as PersonalityEngine
                var info = await _qdrantClient.GetCollectionInfoAsync(CollectionName, ct);
                var existingSize = info.Config?.Params?.VectorsConfig?.Params?.Size ?? 0;
                if (existingSize > 0 && existingSize != (ulong)_vectorSize)
                {
                    Console.WriteLine($"  [!] Collection {CollectionName} has dimension {existingSize}, expected {_vectorSize}. Recreating...");
                    await _qdrantClient.DeleteCollectionAsync(CollectionName);
                    exists = false;
                }
            }

            if (!exists)
            {
                await _qdrantClient.CreateCollectionAsync(
                    CollectionName,
                    new VectorParams
                    {
                        Size = (ulong)_vectorSize,
                        Distance = Distance.Cosine
                    },
                    cancellationToken: ct);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] Qdrant collection setup failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Loads existing patterns from Qdrant into cache.
    /// </summary>
    private async Task LoadPatternsFromQdrantAsync(CancellationToken ct)
    {
        try
        {
            var exists = await _qdrantClient.CollectionExistsAsync(CollectionName, ct);
            if (!exists) return;

            var points = await _qdrantClient.ScrollAsync(
                CollectionName,
                limit: 100,
                cancellationToken: ct);

            foreach (var point in points.Result)
            {
                if (point.Payload.TryGetValue("pattern_json", out var jsonValue))
                {
                    var pattern = JsonSerializer.Deserialize<LearnedToolPattern>(jsonValue.StringValue);
                    if (pattern != null)
                    {
                        _patternCache[pattern.Id] = pattern;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] Failed to load patterns from Qdrant: {ex.Message}");
        }
    }

    /// <summary>
    /// Initializes MeTTa with tool knowledge.
    /// </summary>
    private async Task InitializeMeTTaKnowledgeAsync(CancellationToken ct)
    {
        try
        {
            // Add base tool capability rules
            string rules = @"
; Tool capability inference rules
(= (requires-search $goal)
   (or (contains $goal ""search"") (contains $goal ""find"") (contains $goal ""look up"")))

(= (requires-fetch $goal)
   (or (contains $goal ""fetch"") (contains $goal ""get"") (contains $goal ""download"")))

(= (requires-calculate $goal)
   (or (contains $goal ""calculate"") (contains $goal ""compute"") (contains $goal ""math"")))

; Tool selection rule
(= (best-tool-type $goal)
   (if (requires-search $goal) search-tool
       (if (requires-fetch $goal) fetch-tool
           (if (requires-calculate $goal) calculator-tool
               generic-tool))))
";
            await _mettaEngine.AddFactAsync(rules, ct);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARN] MeTTa initialization failed: {ex.Message}");
        }
    }

    private static string EscapeMeTTa(string text) =>
        text.Replace("\"", "\\\"").Replace("\n", "\\n");

    /// <summary>
    /// Gets statistics about learned patterns.
    /// </summary>
    public (int TotalPatterns, double AvgSuccessRate, int TotalUsage) GetStats()
    {
        var patterns = _patternCache.Values.ToList();
        if (patterns.Count == 0) return (0, 0, 0);

        return (
            patterns.Count,
            patterns.Average(p => p.SuccessRate),
            patterns.Sum(p => p.UsageCount)
        );
    }

    /// <summary>
    /// Gets all learned patterns.
    /// </summary>
    public IReadOnlyList<LearnedToolPattern> GetAllPatterns() =>
        _patternCache.Values.OrderByDescending(p => p.SuccessRate).ToList();

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        _syncLock.Dispose();
        await ValueTask.CompletedTask;
    }
}

#region Genetic Algorithm Support Types

/// <summary>
/// Gene type for tool configuration evolution.
/// </summary>
public sealed record ToolConfigurationGene(string Key, string? Value);

/// <summary>
/// Chromosome representing a tool configuration.
/// </summary>
public sealed class ToolConfigurationChromosome : IChromosome<ToolConfigurationGene>
{
    public ToolConfigurationChromosome(IReadOnlyList<ToolConfigurationGene> genes, double fitness = 0.0)
    {
        Genes = genes;
        Fitness = fitness;
    }

    public IReadOnlyList<ToolConfigurationGene> Genes { get; }
    public double Fitness { get; }

    public IChromosome<ToolConfigurationGene> WithFitness(double fitness) =>
        new ToolConfigurationChromosome(Genes.ToList(), fitness);

    public IChromosome<ToolConfigurationGene> WithGenes(IReadOnlyList<ToolConfigurationGene> genes) =>
        new ToolConfigurationChromosome(genes, Fitness);

    public ToolConfiguration ToConfiguration()
    {
        var dict = Genes.ToDictionary(g => g.Key, g => g.Value);
        return new ToolConfiguration(
            Name: dict.GetValueOrDefault("name") ?? "tool",
            Description: dict.GetValueOrDefault("description") ?? string.Empty,
            SearchProvider: dict.GetValueOrDefault("searchProvider"),
            TimeoutSeconds: double.TryParse(dict.GetValueOrDefault("timeout"), out var t) ? t : 30.0,
            MaxRetries: int.TryParse(dict.GetValueOrDefault("retries"), out var r) ? r : 3,
            CacheResults: dict.GetValueOrDefault("cacheResults") == "true",
            CustomParameters: dict.GetValueOrDefault("customParams"));
    }
}

/// <summary>
/// Fitness function for evaluating tool configurations.
/// </summary>
public sealed class ToolConfigurationFitness : IFitnessFunction<ToolConfigurationGene>
{
    private readonly ToolAwareChatModel _llm;
    private readonly string _goal;

    public ToolConfigurationFitness(ToolAwareChatModel llm, string goal)
    {
        _llm = llm;
        _goal = goal;
    }

    public async Task<double> EvaluateAsync(IChromosome<ToolConfigurationGene> chromosome)
    {
        var config = ((ToolConfigurationChromosome)chromosome).ToConfiguration();

        // Use LLM to evaluate how well the configuration matches the goal
        string prompt = $@"Rate how well this tool configuration matches the goal (0-100):
Goal: {_goal}
Tool: {config.Name}
Description: {config.Description}
Settings: timeout={config.TimeoutSeconds}s, retries={config.MaxRetries}, cache={config.CacheResults}

Just respond with a number 0-100.";

        try
        {
            string response = await _llm.InnerModel.GenerateTextAsync(prompt);
            var match = System.Text.RegularExpressions.Regex.Match(response, @"\d+");
            if (match.Success && int.TryParse(match.Value, out int llmScore))
            {
                return llmScore / 100.0;
            }
        }
        catch
        {
            // Ignore errors
        }

        // Heuristic fallback scoring
        double heuristicScore = 0.5;

        // Prefer caching for repeated queries
        if (config.CacheResults) heuristicScore += 0.1;

        // Reasonable timeout
        if (config.TimeoutSeconds is >= 20 and <= 45) heuristicScore += 0.1;

        // Good retry count
        if (config.MaxRetries is >= 2 and <= 4) heuristicScore += 0.1;

        return Math.Min(heuristicScore, 1.0);
    }
}

#endregion
