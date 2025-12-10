// <copyright file="InterconnectedLearner.cs" company="PlaceholderCompany">
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
using Ouroboros.Tools;
using Ouroboros.Tools.MeTTa;

/// <summary>
/// Represents a single execution record (tool, skill, or pipeline).
/// </summary>
public sealed record ExecutionRecord(
    string Id,
    string ExecutionType,
    string Name,
    string Input,
    string Output,
    bool Success,
    TimeSpan Duration,
    DateTime Timestamp,
    Dictionary<string, string> Metadata);

/// <summary>
/// Represents a learned interconnection pattern between tools, skills, and goals.
/// </summary>
public sealed record InterconnectedPattern(
    string Id,
    string PatternType,
    string GoalDescription,
    List<string> ToolSequence,
    List<string> SkillSequence,
    double SuccessRate,
    int UsageCount,
    DateTime CreatedAt,
    DateTime LastUsed,
    float[] EmbeddingVector);

/// <summary>
/// Gene for evolving action sequences in the genetic algorithm.
/// </summary>
public sealed record ActionGene(string ActionType, string ActionName, double Priority);

/// <summary>
/// Intelligent interconnected learning system that bridges tools, skills, and pipelines
/// using LLM reasoning, genetic algorithm optimization, embeddings, and MeTTa symbolic reasoning.
///
/// This system:
/// 1. Records all tool, skill, and pipeline executions
/// 2. Uses LLM to analyze patterns and suggest optimizations
/// 3. Uses genetic algorithms to evolve optimal action sequences
/// 4. Uses embeddings to find semantically similar patterns
/// 5. Uses MeTTa for symbolic reasoning about relationships
/// 6. Learns and adapts over time to improve suggestions
/// </summary>
public sealed class InterconnectedLearner : IAsyncDisposable
{
    private readonly DynamicToolFactory _toolFactory;
    private readonly ISkillRegistry? _skillRegistry;
    private readonly IMeTTaEngine _mettaEngine;
    private readonly IEmbeddingModel? _embeddingModel;
    private readonly ToolAwareChatModel? _llm;

    // Execution history for pattern learning
    private readonly ConcurrentQueue<ExecutionRecord> _executionLog = new();
    private readonly ConcurrentDictionary<string, InterconnectedPattern> _patterns = new();
    private readonly ConcurrentDictionary<string, List<string>> _conceptGraph = new();

    // Statistics
    private int _totalToolExecutions;
    private int _totalSkillExecutions;
    private int _totalPipelineExecutions;
    private int _successfulExecutions;

    private const int MaxExecutionHistory = 1000;

    /// <summary>
    /// Initializes a new instance of the <see cref="InterconnectedLearner"/> class.
    /// </summary>
    public InterconnectedLearner(
        DynamicToolFactory toolFactory,
        ISkillRegistry? skillRegistry,
        IMeTTaEngine mettaEngine,
        IEmbeddingModel? embeddingModel = null,
        ToolAwareChatModel? llm = null)
    {
        _toolFactory = toolFactory ?? throw new ArgumentNullException(nameof(toolFactory));
        _skillRegistry = skillRegistry;
        _mettaEngine = mettaEngine ?? throw new ArgumentNullException(nameof(mettaEngine));
        _embeddingModel = embeddingModel;
        _llm = llm;
    }

    #region Execution Recording

    /// <summary>
    /// Records a tool execution for pattern learning.
    /// </summary>
    public async Task RecordToolExecutionAsync(
        string toolName,
        string input,
        string output,
        bool success,
        TimeSpan duration,
        CancellationToken ct = default)
    {
        var record = new ExecutionRecord(
            Id: Guid.NewGuid().ToString("N"),
            ExecutionType: "tool",
            Name: toolName,
            Input: input,
            Output: output,
            Success: success,
            Duration: duration,
            Timestamp: DateTime.UtcNow,
            Metadata: new Dictionary<string, string>());

        EnqueueExecution(record);
        Interlocked.Increment(ref _totalToolExecutions);
        if (success) Interlocked.Increment(ref _successfulExecutions);

        // Add to MeTTa knowledge base
        await AddToMeTTaKnowledgeAsync(record, ct);

        // Update concept graph
        UpdateConceptGraph(record);

        // Try to learn patterns periodically
        if (_executionLog.Count % 10 == 0)
        {
            await TryLearnPatternsAsync(ct);
        }
    }

    /// <summary>
    /// Records a skill execution for pattern learning.
    /// </summary>
    public async Task RecordSkillExecutionAsync(
        string skillName,
        string input,
        string output,
        bool success,
        CancellationToken ct = default)
    {
        var record = new ExecutionRecord(
            Id: Guid.NewGuid().ToString("N"),
            ExecutionType: "skill",
            Name: skillName,
            Input: input,
            Output: output,
            Success: success,
            Duration: TimeSpan.Zero,
            Timestamp: DateTime.UtcNow,
            Metadata: new Dictionary<string, string>());

        EnqueueExecution(record);
        Interlocked.Increment(ref _totalSkillExecutions);
        if (success) Interlocked.Increment(ref _successfulExecutions);

        await AddToMeTTaKnowledgeAsync(record, ct);
        UpdateConceptGraph(record);

        if (_executionLog.Count % 10 == 0)
        {
            await TryLearnPatternsAsync(ct);
        }
    }

    /// <summary>
    /// Records a pipeline execution for pattern learning.
    /// </summary>
    public async Task RecordPipelineExecutionAsync(
        string pipelineName,
        string input,
        string output,
        bool success,
        TimeSpan duration,
        CancellationToken ct = default)
    {
        var record = new ExecutionRecord(
            Id: Guid.NewGuid().ToString("N"),
            ExecutionType: "pipeline",
            Name: pipelineName,
            Input: input,
            Output: output,
            Success: success,
            Duration: duration,
            Timestamp: DateTime.UtcNow,
            Metadata: new Dictionary<string, string>());

        EnqueueExecution(record);
        Interlocked.Increment(ref _totalPipelineExecutions);
        if (success) Interlocked.Increment(ref _successfulExecutions);

        await AddToMeTTaKnowledgeAsync(record, ct);
        UpdateConceptGraph(record);
    }

    private void EnqueueExecution(ExecutionRecord record)
    {
        _executionLog.Enqueue(record);

        // Prune old records to avoid memory bloat
        while (_executionLog.Count > MaxExecutionHistory && _executionLog.TryDequeue(out _))
        {
        }
    }

    #endregion

    #region MeTTa Symbolic Reasoning

    private async Task AddToMeTTaKnowledgeAsync(ExecutionRecord record, CancellationToken ct)
    {
        try
        {
            // Add execution fact
            string executionAtom = $"(execution {record.ExecutionType} \"{EscapeMeTTa(record.Name)}\" {(record.Success ? "success" : "failure")})";
            await _mettaEngine.AddFactAsync(executionAtom, ct);

            // Add tool/skill capability fact
            string capabilityAtom = $"(capability \"{EscapeMeTTa(record.Name)}\" \"{EscapeMeTTa(ExtractCapability(record.Input))}\")";
            await _mettaEngine.AddFactAsync(capabilityAtom, ct);

            // Add relationship facts based on input patterns
            var keywords = ExtractKeywords(record.Input);
            foreach (var keyword in keywords.Take(5)) // Limit to 5 most relevant
            {
                string relationAtom = $"(related-to \"{EscapeMeTTa(record.Name)}\" \"{EscapeMeTTa(keyword)}\")";
                await _mettaEngine.AddFactAsync(relationAtom, ct);
            }
        }
        catch
        {
            // MeTTa updates are best-effort
        }
    }

    /// <summary>
    /// Queries MeTTa to find tools/skills related to a goal.
    /// </summary>
    public async Task<List<string>> QueryRelatedActionsAsync(string goal, CancellationToken ct = default)
    {
        var results = new List<string>();

        try
        {
            // Query for tools/skills that can handle this goal
            string query = $@"!(match &self
                (and
                    (capability $name $cap)
                    (or
                        (contains ""{EscapeMeTTa(goal)}"" $cap)
                        (related-to $name ""{EscapeMeTTa(goal)}"")
                    )
                )
                $name)";

            var mettaResult = await _mettaEngine.ExecuteQueryAsync(query, ct);
            if (mettaResult.IsSuccess && !string.IsNullOrWhiteSpace(mettaResult.Value))
            {
                // Parse MeTTa results
                var matches = System.Text.RegularExpressions.Regex.Matches(
                    mettaResult.Value,
                    @"""([^""]+)""");

                foreach (System.Text.RegularExpressions.Match match in matches)
                {
                    if (match.Groups.Count > 1)
                    {
                        results.Add(match.Groups[1].Value);
                    }
                }
            }
        }
        catch
        {
            // Fallback to concept graph
        }

        return results.Distinct().ToList();
    }

    #endregion

    #region LLM-Based Pattern Analysis

    /// <summary>
    /// Uses LLM to analyze execution patterns and suggest improvements.
    /// </summary>
    public async Task<string> AnalyzePatternsWithLLMAsync(CancellationToken ct = default)
    {
        if (_llm == null)
            return "LLM not available for pattern analysis.";

        var recentExecutions = _executionLog.ToArray().TakeLast(50).ToList();
        if (recentExecutions.Count < 5)
            return "Not enough execution data for pattern analysis.";

        var sb = new StringBuilder();
        sb.AppendLine("Recent execution history:");
        foreach (var exec in recentExecutions.TakeLast(20))
        {
            sb.AppendLine($"- [{exec.ExecutionType}] {exec.Name}: {(exec.Success ? "✓" : "✗")} ({exec.Duration.TotalMilliseconds:F0}ms)");
            sb.AppendLine($"  Input: {Truncate(exec.Input, 100)}");
        }

        string prompt = $@"Analyze this AI system's execution history and provide insights:

{sb}

Provide:
1. Patterns you notice (what tools/skills are used together)
2. Optimization suggestions (faster alternatives, better sequences)
3. Skills that could be created to automate common patterns
4. Any inefficiencies or issues

Be concise and actionable.";

        try
        {
            var response = await _llm.InnerModel.GenerateTextAsync(prompt, ct);
            return response;
        }
        catch (Exception ex)
        {
            return $"Pattern analysis failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Uses LLM to suggest the best action for a given goal.
    /// </summary>
    public async Task<(string ActionType, string ActionName, string Reasoning)?> SuggestActionWithLLMAsync(
        string goal,
        ToolRegistry availableTools,
        CancellationToken ct = default)
    {
        if (_llm == null)
            return null;

        // Gather context
        var toolNames = availableTools.All.Select(t => t.Name).ToList();
        var skillNames = _skillRegistry?.GetAllSkills().Select(s => s.Name).ToList() ?? new List<string>();
        var relatedFromMeTTa = await QueryRelatedActionsAsync(goal, ct);
        var similarPatterns = await FindSimilarPatternsAsync(goal, 3, ct);

        string prompt = $@"Goal: {goal}

Available Tools: {string.Join(", ", toolNames)}
Available Skills: {string.Join(", ", skillNames)}
MeTTa suggested: {string.Join(", ", relatedFromMeTTa)}
Similar past patterns: {string.Join("; ", similarPatterns.Select(p => $"{p.PatternType}: {string.Join("->", p.ToolSequence.Concat(p.SkillSequence))}"))}

What single action should be taken? Respond in JSON:
{{
  ""actionType"": ""tool"" or ""skill"" or ""pipeline"" or ""create_tool"" or ""create_skill"",
  ""actionName"": ""name of action to take"",
  ""reasoning"": ""why this is the best choice""
}}";

        try
        {
            var response = await _llm.InnerModel.GenerateTextAsync(prompt, ct);

            // Parse JSON
            var match = System.Text.RegularExpressions.Regex.Match(
                response,
                @"""actionType""\s*:\s*""([^""]+)"".*?""actionName""\s*:\s*""([^""]+)"".*?""reasoning""\s*:\s*""([^""]+)""",
                System.Text.RegularExpressions.RegexOptions.Singleline);

            if (match.Success)
            {
                return (match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value);
            }
        }
        catch
        {
            // Fallback
        }

        return null;
    }

    #endregion

    #region Embedding-Based Similarity

    /// <summary>
    /// Finds patterns semantically similar to the given goal using embeddings.
    /// </summary>
    public async Task<List<InterconnectedPattern>> FindSimilarPatternsAsync(
        string goal,
        int topK = 5,
        CancellationToken ct = default)
    {
        if (_embeddingModel == null || _patterns.IsEmpty)
            return new List<InterconnectedPattern>();

        try
        {
            // Get embedding for the goal
            var goalEmbedding = await _embeddingModel.CreateEmbeddingsAsync(goal, ct);

            // Compute cosine similarity with all patterns
            var similarities = new List<(InterconnectedPattern Pattern, double Similarity)>();

            foreach (var pattern in _patterns.Values)
            {
                if (pattern.EmbeddingVector.Length > 0)
                {
                    double similarity = CosineSimilarity(goalEmbedding, pattern.EmbeddingVector);
                    similarities.Add((pattern, similarity));
                }
            }

            return similarities
                .OrderByDescending(x => x.Similarity)
                .Take(topK)
                .Where(x => x.Similarity > 0.5) // Threshold
                .Select(x => x.Pattern)
                .ToList();
        }
        catch
        {
            return new List<InterconnectedPattern>();
        }
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length) return 0;

        double dotProduct = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        double denominator = Math.Sqrt(normA) * Math.Sqrt(normB);
        return denominator > 0 ? dotProduct / denominator : 0;
    }

    #endregion

    #region Genetic Algorithm Evolution

    /// <summary>
    /// Uses genetic algorithm to evolve optimal action sequences for a goal.
    /// </summary>
    public async Task<List<(string ActionType, string ActionName)>> EvolveOptimalSequenceAsync(
        string goal,
        ToolRegistry availableTools,
        int maxSteps = 5,
        CancellationToken ct = default)
    {
        if (_llm == null)
            return new List<(string, string)>();

        try
        {
            // Get available actions
            var toolNames = availableTools.All.Select(t => ("tool", t.Name)).ToList();
            var skillNames = (_skillRegistry?.GetAllSkills().Select(s => ("skill", s.Name)).ToList())
                ?? new List<(string, string)>();
            var allActions = toolNames.Concat(skillNames).ToList();

            if (allActions.Count == 0)
                return new List<(string, string)>();

            // Create initial population
            var initialPopulation = CreateActionSequencePopulation(allActions, maxSteps, 15);

            // Create fitness function
            var fitnessFunc = new ActionSequenceFitness(_llm, goal);

            // Create genetic algorithm
            var ga = new GeneticAlgorithm<ActionGene>(
                fitnessFunc,
                MutateActionGene,
                mutationRate: 0.2,
                crossoverRate: 0.7,
                elitismRate: 0.15);

            // Evolve for a few generations
            var result = await ga.EvolveAsync(initialPopulation, generations: 8);

            if (result.IsSuccess)
            {
                var bestChromosome = result.Value;
                return bestChromosome.Genes
                    .OrderByDescending(g => g.Priority)
                    .Take(maxSteps)
                    .Select(g => (g.ActionType, g.ActionName))
                    .ToList();
            }
        }
        catch
        {
            // Fallback
        }

        return new List<(string, string)>();
    }

    private List<IChromosome<ActionGene>> CreateActionSequencePopulation(
        List<(string Type, string Name)> availableActions,
        int maxSteps,
        int populationSize)
    {
        var random = new Random();
        var population = new List<IChromosome<ActionGene>>();

        for (int i = 0; i < populationSize; i++)
        {
            var genes = new List<ActionGene>();
            int numActions = random.Next(1, maxSteps + 1);

            for (int j = 0; j < numActions; j++)
            {
                var action = availableActions[random.Next(availableActions.Count)];
                genes.Add(new ActionGene(action.Type, action.Name, 1.0 - (j * 0.1)));
            }

            population.Add(new ActionSequenceChromosome(genes));
        }

        return population;
    }

    private static ActionGene MutateActionGene(ActionGene gene)
    {
        var random = new Random();

        // Mutate priority
        double newPriority = Math.Clamp(gene.Priority + (random.NextDouble() - 0.5) * 0.3, 0.0, 1.0);

        return gene with { Priority = newPriority };
    }

    #endregion

    #region Pattern Learning

    private void UpdateConceptGraph(ExecutionRecord record)
    {
        // Extract concepts from input
        var concepts = ExtractKeywords(record.Input);

        // Link action to concepts
        if (!_conceptGraph.ContainsKey(record.Name))
        {
            _conceptGraph[record.Name] = new List<string>();
        }

        foreach (var concept in concepts)
        {
            if (!_conceptGraph[record.Name].Contains(concept))
            {
                _conceptGraph[record.Name].Add(concept);
            }

            // Reverse link: concept -> action
            if (!_conceptGraph.ContainsKey(concept))
            {
                _conceptGraph[concept] = new List<string>();
            }

            if (!_conceptGraph[concept].Contains(record.Name))
            {
                _conceptGraph[concept].Add(record.Name);
            }
        }
    }

    private async Task TryLearnPatternsAsync(CancellationToken ct)
    {
        var recentExecutions = _executionLog.ToArray().TakeLast(20).ToList();
        if (recentExecutions.Count < 5) return;

        // Find sequences of successful executions
        var successfulSequence = new List<ExecutionRecord>();
        foreach (var exec in recentExecutions)
        {
            if (exec.Success)
            {
                successfulSequence.Add(exec);
            }
            else
            {
                if (successfulSequence.Count >= 2)
                {
                    await CreatePatternFromSequenceAsync(successfulSequence, ct);
                }

                successfulSequence.Clear();
            }
        }

        // Handle trailing successful sequence
        if (successfulSequence.Count >= 2)
        {
            await CreatePatternFromSequenceAsync(successfulSequence, ct);
        }
    }

    private async Task CreatePatternFromSequenceAsync(List<ExecutionRecord> sequence, CancellationToken ct)
    {
        if (sequence.Count < 2) return;

        // Generate description from inputs
        string goalDescription = string.Join(" -> ", sequence.Select(s => Truncate(s.Input, 50)));

        // Get embedding if available
        float[] embedding = Array.Empty<float>();
        if (_embeddingModel != null)
        {
            try
            {
                embedding = await _embeddingModel.CreateEmbeddingsAsync(goalDescription, ct);
            }
            catch
            {
                // Continue without embedding
            }
        }

        var pattern = new InterconnectedPattern(
            Id: Guid.NewGuid().ToString("N"),
            PatternType: DeterminePatternType(sequence),
            GoalDescription: goalDescription,
            ToolSequence: sequence.Where(s => s.ExecutionType == "tool").Select(s => s.Name).ToList(),
            SkillSequence: sequence.Where(s => s.ExecutionType == "skill").Select(s => s.Name).ToList(),
            SuccessRate: 1.0,
            UsageCount: 1,
            CreatedAt: DateTime.UtcNow,
            LastUsed: DateTime.UtcNow,
            EmbeddingVector: embedding);

        _patterns[pattern.Id] = pattern;
    }

    private static string DeterminePatternType(List<ExecutionRecord> sequence)
    {
        bool hasTools = sequence.Any(s => s.ExecutionType == "tool");
        bool hasSkills = sequence.Any(s => s.ExecutionType == "skill");
        bool hasPipelines = sequence.Any(s => s.ExecutionType == "pipeline");

        if (hasTools && hasSkills) return "hybrid";
        if (hasTools) return "tool-chain";
        if (hasSkills) return "skill-chain";
        if (hasPipelines) return "pipeline-chain";
        return "mixed";
    }

    #endregion

    #region Smart Suggestions

    /// <summary>
    /// Gets intelligent suggestions for accomplishing a goal using all available algorithms.
    /// </summary>
    public async Task<SmartSuggestion> SuggestForGoalAsync(
        string goal,
        ToolRegistry availableTools,
        CancellationToken ct = default)
    {
        var suggestion = new SmartSuggestion
        {
            Goal = goal,
            Timestamp = DateTime.UtcNow
        };

        // 1. MeTTa symbolic reasoning
        var mettaSuggestions = await QueryRelatedActionsAsync(goal, ct);
        suggestion.MeTTaSuggestions = mettaSuggestions;

        // 2. Embedding similarity search
        var similarPatterns = await FindSimilarPatternsAsync(goal, 3, ct);
        suggestion.SimilarPatterns = similarPatterns.Select(p => new PatternSummary(
            p.PatternType,
            p.GoalDescription,
            p.ToolSequence.Concat(p.SkillSequence).ToList(),
            p.SuccessRate)).ToList();

        // 3. LLM analysis
        var llmSuggestion = await SuggestActionWithLLMAsync(goal, availableTools, ct);
        if (llmSuggestion.HasValue)
        {
            suggestion.LLMSuggestion = new LLMActionSuggestion(
                llmSuggestion.Value.ActionType,
                llmSuggestion.Value.ActionName,
                llmSuggestion.Value.Reasoning);
        }

        // 4. Genetic algorithm optimization (for complex goals)
        if (goal.Length > 20 || goal.Contains(" and ") || goal.Contains(","))
        {
            var evolvedSequence = await EvolveOptimalSequenceAsync(goal, availableTools, 5, ct);
            suggestion.EvolvedSequence = evolvedSequence;
        }

        // 5. Concept graph traversal
        suggestion.RelatedConcepts = GetRelatedConcepts(goal);

        // 6. Compute confidence score
        suggestion.ConfidenceScore = ComputeConfidenceScore(suggestion);

        return suggestion;
    }

    private List<string> GetRelatedConcepts(string goal)
    {
        var keywords = ExtractKeywords(goal);
        var related = new HashSet<string>();

        foreach (var keyword in keywords)
        {
            if (_conceptGraph.TryGetValue(keyword, out var connections))
            {
                foreach (var connection in connections.Take(5))
                {
                    related.Add(connection);
                }
            }
        }

        return related.Take(10).ToList();
    }

    private static double ComputeConfidenceScore(SmartSuggestion suggestion)
    {
        double score = 0.0;

        // Weight different sources
        if (suggestion.MeTTaSuggestions.Count > 0) score += 0.2;
        if (suggestion.SimilarPatterns.Count > 0) score += 0.25 * Math.Min(suggestion.SimilarPatterns.Count, 3) / 3.0;
        if (suggestion.LLMSuggestion != null) score += 0.3;
        if (suggestion.EvolvedSequence.Count > 0) score += 0.15;
        if (suggestion.RelatedConcepts.Count > 0) score += 0.1;

        return Math.Min(score, 1.0);
    }

    #endregion

    #region Statistics

    /// <summary>
    /// Gets learning statistics.
    /// </summary>
    public LearningStats GetStats() => new(
        TotalToolExecutions: _totalToolExecutions,
        TotalSkillExecutions: _totalSkillExecutions,
        TotalPipelineExecutions: _totalPipelineExecutions,
        SuccessfulExecutions: _successfulExecutions,
        LearnedPatterns: _patterns.Count,
        ConceptGraphNodes: _conceptGraph.Count,
        ExecutionLogSize: _executionLog.Count);

    /// <summary>
    /// Gets all learned patterns.
    /// </summary>
    public IReadOnlyList<InterconnectedPattern> GetPatterns() =>
        _patterns.Values.OrderByDescending(p => p.SuccessRate * p.UsageCount).ToList();

    #endregion

    #region Helpers

    private static string EscapeMeTTa(string text) =>
        text.Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", string.Empty);

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength] + "...";

    private static string ExtractCapability(string input)
    {
        // Extract first verb or action phrase
        var words = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 0 ? words[0].ToLowerInvariant() : "unknown";
    }

    private static List<string> ExtractKeywords(string text)
    {
        // Simple keyword extraction - remove common words
        var stopWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "the", "a", "an", "is", "are", "was", "were", "be", "been", "being",
            "have", "has", "had", "do", "does", "did", "will", "would", "could",
            "should", "may", "might", "must", "shall", "can", "to", "of", "in",
            "for", "on", "with", "at", "by", "from", "as", "into", "through",
            "during", "before", "after", "above", "below", "between", "under",
            "again", "further", "then", "once", "here", "there", "when", "where",
            "why", "how", "all", "each", "few", "more", "most", "other", "some",
            "such", "no", "nor", "not", "only", "own", "same", "so", "than",
            "too", "very", "just", "and", "but", "if", "or", "because", "until",
            "while", "about", "against", "this", "that", "these", "those", "it"
        };

        return text.ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', '!', '?', ';', ':', '"', '\'', '(', ')', '[', ']' },
                   StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !stopWords.Contains(w))
            .Distinct()
            .Take(15)
            .ToList();
    }

    #endregion

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _executionLog.Clear();
        _patterns.Clear();
        _conceptGraph.Clear();
        return ValueTask.CompletedTask;
    }
}

#region Supporting Types

/// <summary>
/// Summary of a learned pattern.
/// </summary>
public sealed record PatternSummary(
    string PatternType,
    string GoalDescription,
    List<string> Actions,
    double SuccessRate);

/// <summary>
/// LLM action suggestion.
/// </summary>
public sealed record LLMActionSuggestion(
    string ActionType,
    string ActionName,
    string Reasoning);

/// <summary>
/// Learning statistics.
/// </summary>
public sealed record LearningStats(
    int TotalToolExecutions,
    int TotalSkillExecutions,
    int TotalPipelineExecutions,
    int SuccessfulExecutions,
    int LearnedPatterns,
    int ConceptGraphNodes,
    int ExecutionLogSize);

/// <summary>
/// Smart suggestion result combining multiple algorithms.
/// </summary>
public sealed class SmartSuggestion
{
    public string Goal { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public double ConfidenceScore { get; set; }

    public List<string> MeTTaSuggestions { get; set; } = new();
    public List<PatternSummary> SimilarPatterns { get; set; } = new();
    public LLMActionSuggestion? LLMSuggestion { get; set; }
    public List<(string ActionType, string ActionName)> EvolvedSequence { get; set; } = new();
    public List<string> RelatedConcepts { get; set; } = new();
}

#endregion

#region Genetic Algorithm Support Types

/// <summary>
/// Chromosome for action sequences.
/// </summary>
public sealed class ActionSequenceChromosome : IChromosome<ActionGene>
{
    public ActionSequenceChromosome(IReadOnlyList<ActionGene> genes, double fitness = 0.0)
    {
        Genes = genes;
        Fitness = fitness;
    }

    public IReadOnlyList<ActionGene> Genes { get; }
    public double Fitness { get; }

    public IChromosome<ActionGene> WithFitness(double fitness) =>
        new ActionSequenceChromosome(Genes.ToList(), fitness);

    public IChromosome<ActionGene> WithGenes(IReadOnlyList<ActionGene> genes) =>
        new ActionSequenceChromosome(genes, Fitness);
}

/// <summary>
/// Fitness function for action sequences using LLM evaluation.
/// </summary>
public sealed class ActionSequenceFitness : IFitnessFunction<ActionGene>
{
    private readonly ToolAwareChatModel _llm;
    private readonly string _goal;

    public ActionSequenceFitness(ToolAwareChatModel llm, string goal)
    {
        _llm = llm;
        _goal = goal;
    }

    public async Task<double> EvaluateAsync(IChromosome<ActionGene> chromosome)
    {
        var actions = chromosome.Genes
            .OrderByDescending(g => g.Priority)
            .Select(g => $"{g.ActionType}:{g.ActionName}")
            .ToList();

        string sequence = string.Join(" -> ", actions);

        string prompt = $@"Rate how well this action sequence would accomplish the goal (0-100):
Goal: {_goal}
Sequence: {sequence}

Consider: relevance, efficiency, completeness.
Just respond with a number 0-100.";

        try
        {
            string response = await _llm.InnerModel.GenerateTextAsync(prompt);
            var match = System.Text.RegularExpressions.Regex.Match(response, @"\d+");
            if (match.Success && int.TryParse(match.Value, out int score))
            {
                // Normalize and add bonus for shorter sequences
                double normalizedScore = score / 100.0;
                double lengthBonus = Math.Max(0, (5 - actions.Count) * 0.05);
                return Math.Min(normalizedScore + lengthBonus, 1.0);
            }
        }
        catch
        {
            // Ignore errors
        }

        // Heuristic fallback
        return 0.3 + (chromosome.Genes.Count > 0 ? 0.2 : 0.0);
    }
}

#endregion
