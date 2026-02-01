// <copyright file="ToolCapabilityMatcher.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.WorldModel;

using System.Collections.Immutable;
using Ouroboros.Core.Monads;
using Ouroboros.Core.Steps;
using Ouroboros.Pipeline.Planning;
using Ouroboros.Tools;

/// <summary>
/// Represents a match between a tool and a goal with relevance scoring.
/// </summary>
/// <param name="ToolName">The name of the matched tool.</param>
/// <param name="RelevanceScore">Relevance score between 0.0 and 1.0.</param>
/// <param name="MatchedCapabilities">List of capabilities that matched the goal.</param>
public sealed record ToolMatch(
    string ToolName,
    double RelevanceScore,
    IReadOnlyList<string> MatchedCapabilities)
{
    /// <summary>
    /// Creates a tool match with no matched capabilities.
    /// </summary>
    /// <param name="toolName">The tool name.</param>
    /// <param name="score">The relevance score.</param>
    /// <returns>A new tool match.</returns>
    public static ToolMatch Create(string toolName, double score)
    {
        ArgumentNullException.ThrowIfNull(toolName);

        double clampedScore = Math.Clamp(score, 0.0, 1.0);
        return new ToolMatch(toolName, clampedScore, []);
    }

    /// <summary>
    /// Creates a tool match with matched capabilities.
    /// </summary>
    /// <param name="toolName">The tool name.</param>
    /// <param name="score">The relevance score.</param>
    /// <param name="capabilities">The matched capabilities.</param>
    /// <returns>A new tool match.</returns>
    public static ToolMatch Create(string toolName, double score, IEnumerable<string> capabilities)
    {
        ArgumentNullException.ThrowIfNull(toolName);
        ArgumentNullException.ThrowIfNull(capabilities);

        double clampedScore = Math.Clamp(score, 0.0, 1.0);
        return new ToolMatch(toolName, clampedScore, capabilities.ToList());
    }
}

/// <summary>
/// Matches goals to available tools based on capability analysis.
/// Uses keyword/token matching to determine tool relevance for goal completion.
/// </summary>
public sealed class ToolCapabilityMatcher
{
    private static readonly char[] Separators = [' ', ',', '.', ';', ':', '-', '_', '/', '\\', '(', ')', '[', ']', '{', '}', '"', '\'', '\n', '\r', '\t'];

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for",
        "of", "with", "by", "from", "as", "is", "was", "are", "were", "been",
        "be", "have", "has", "had", "do", "does", "did", "will", "would", "could",
        "should", "may", "might", "must", "shall", "can", "need", "this", "that",
        "these", "those", "it", "its", "they", "them", "their", "we", "our", "you",
        "your", "i", "me", "my", "he", "she", "him", "her", "his", "all", "any",
        "some", "no", "not", "only", "just", "also", "very", "too", "so", "then"
    };

    private readonly ToolRegistry toolRegistry;

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolCapabilityMatcher"/> class.
    /// </summary>
    /// <param name="toolRegistry">The registry containing available tools.</param>
    /// <exception cref="ArgumentNullException">Thrown when toolRegistry is null.</exception>
    public ToolCapabilityMatcher(ToolRegistry toolRegistry)
    {
        ArgumentNullException.ThrowIfNull(toolRegistry);

        this.toolRegistry = toolRegistry;
    }

    /// <summary>
    /// Matches tools to a goal based on capability requirements.
    /// </summary>
    /// <param name="goal">The goal to match tools for.</param>
    /// <param name="minScore">Minimum relevance score threshold (0.0 to 1.0).</param>
    /// <returns>A Result containing ranked list of matching tools, or an error.</returns>
    public Result<IReadOnlyList<ToolMatch>, string> MatchToolsForGoal(Goal goal, double minScore = 0.0)
    {
        ArgumentNullException.ThrowIfNull(goal);

        if (minScore < 0.0 || minScore > 1.0)
        {
            return Result<IReadOnlyList<ToolMatch>, string>.Failure(
                $"Minimum score must be between 0.0 and 1.0, got {minScore}");
        }

        return this.MatchToolsForGoalDescription(goal.Description, minScore);
    }

    /// <summary>
    /// Matches tools to a goal description string.
    /// </summary>
    /// <param name="goalDescription">The goal description to match.</param>
    /// <param name="minScore">Minimum relevance score threshold (0.0 to 1.0).</param>
    /// <returns>A Result containing ranked list of matching tools, or an error.</returns>
    public Result<IReadOnlyList<ToolMatch>, string> MatchToolsForGoalDescription(string goalDescription, double minScore = 0.0)
    {
        ArgumentNullException.ThrowIfNull(goalDescription);

        if (string.IsNullOrWhiteSpace(goalDescription))
        {
            return Result<IReadOnlyList<ToolMatch>, string>.Failure(
                "Goal description cannot be empty or whitespace");
        }

        if (minScore < 0.0 || minScore > 1.0)
        {
            return Result<IReadOnlyList<ToolMatch>, string>.Failure(
                $"Minimum score must be between 0.0 and 1.0, got {minScore}");
        }

        IReadOnlyList<string> requiredCapabilities = this.GetRequiredCapabilities(goalDescription);
        List<ToolMatch> matches = new();

        foreach (ITool tool in this.toolRegistry.All)
        {
            ToolMatch match = this.ScoreAndMatchTool(tool, goalDescription, requiredCapabilities);

            if (match.RelevanceScore >= minScore)
            {
                matches.Add(match);
            }
        }

        // Sort by relevance score descending
        IReadOnlyList<ToolMatch> rankedMatches = matches
            .OrderByDescending(m => m.RelevanceScore)
            .ThenBy(m => m.ToolName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Result<IReadOnlyList<ToolMatch>, string>.Success(rankedMatches);
    }

    /// <summary>
    /// Scores the relevance of a tool for a given goal description.
    /// </summary>
    /// <param name="tool">The tool to score.</param>
    /// <param name="goalDescription">The goal description.</param>
    /// <returns>A relevance score between 0.0 and 1.0.</returns>
    public double ScoreToolRelevance(ITool tool, string goalDescription)
    {
        ArgumentNullException.ThrowIfNull(tool);
        ArgumentNullException.ThrowIfNull(goalDescription);

        IReadOnlyList<string> requiredCapabilities = this.GetRequiredCapabilities(goalDescription);
        return this.ScoreAndMatchTool(tool, goalDescription, requiredCapabilities).RelevanceScore;
    }

    /// <summary>
    /// Extracts required capabilities from a goal description.
    /// Performs tokenization and keyword extraction.
    /// </summary>
    /// <param name="goalDescription">The goal description to analyze.</param>
    /// <returns>List of capability keywords extracted from the description.</returns>
    public IReadOnlyList<string> GetRequiredCapabilities(string goalDescription)
    {
        ArgumentNullException.ThrowIfNull(goalDescription);

        if (string.IsNullOrWhiteSpace(goalDescription))
        {
            return [];
        }

        return ExtractKeywords(goalDescription);
    }

    /// <summary>
    /// Creates a step that matches tools for a goal asynchronously.
    /// </summary>
    /// <param name="minScore">Minimum relevance score threshold.</param>
    /// <returns>A step that transforms a goal into matching tools.</returns>
    public Step<Goal, Result<IReadOnlyList<ToolMatch>, string>> CreateMatchingStep(double minScore = 0.0)
    {
        return goal => Task.FromResult(this.MatchToolsForGoal(goal, minScore));
    }

    /// <summary>
    /// Creates a step that matches tools for a goal description asynchronously.
    /// </summary>
    /// <param name="minScore">Minimum relevance score threshold.</param>
    /// <returns>A step that transforms a goal description into matching tools.</returns>
    public Step<string, Result<IReadOnlyList<ToolMatch>, string>> CreateDescriptionMatchingStep(double minScore = 0.0)
    {
        return description => Task.FromResult(this.MatchToolsForGoalDescription(description, minScore));
    }

    /// <summary>
    /// Gets the best matching tool for a goal, if any tool meets the minimum score.
    /// </summary>
    /// <param name="goal">The goal to match.</param>
    /// <param name="minScore">Minimum relevance score threshold.</param>
    /// <returns>Option containing the best match if found.</returns>
    public Option<ToolMatch> GetBestMatch(Goal goal, double minScore = 0.1)
    {
        ArgumentNullException.ThrowIfNull(goal);

        Result<IReadOnlyList<ToolMatch>, string> result = this.MatchToolsForGoal(goal, minScore);

        if (result.IsSuccess && result.Value.Count > 0)
        {
            return Option<ToolMatch>.Some(result.Value[0]);
        }

        return Option<ToolMatch>.None();
    }

    /// <summary>
    /// Gets the best matching tool for a goal description, if any tool meets the minimum score.
    /// </summary>
    /// <param name="goalDescription">The goal description to match.</param>
    /// <param name="minScore">Minimum relevance score threshold.</param>
    /// <returns>Option containing the best match if found.</returns>
    public Option<ToolMatch> GetBestMatchForDescription(string goalDescription, double minScore = 0.1)
    {
        ArgumentNullException.ThrowIfNull(goalDescription);

        Result<IReadOnlyList<ToolMatch>, string> result = this.MatchToolsForGoalDescription(goalDescription, minScore);

        if (result.IsSuccess && result.Value.Count > 0)
        {
            return Option<ToolMatch>.Some(result.Value[0]);
        }

        return Option<ToolMatch>.None();
    }

    private static IReadOnlyList<string> ExtractKeywords(string text)
    {
        string[] tokens = text.Split(Separators, StringSplitOptions.RemoveEmptyEntries);

        return tokens
            .Select(t => t.ToLowerInvariant())
            .Where(t => t.Length >= 2)
            .Where(t => !StopWords.Contains(t))
            .Distinct()
            .ToList();
    }

    private static double CalculateJaccardSimilarity(IReadOnlyCollection<string> set1, IReadOnlyCollection<string> set2)
    {
        if (set1.Count == 0 || set2.Count == 0)
        {
            return 0.0;
        }

        HashSet<string> hashSet1 = new(set1, StringComparer.OrdinalIgnoreCase);
        HashSet<string> hashSet2 = new(set2, StringComparer.OrdinalIgnoreCase);

        int intersectionCount = hashSet1.Intersect(hashSet2, StringComparer.OrdinalIgnoreCase).Count();
        int unionCount = hashSet1.Union(hashSet2, StringComparer.OrdinalIgnoreCase).Count();

        return unionCount > 0 ? (double)intersectionCount / unionCount : 0.0;
    }

    private static IReadOnlyList<string> FindMatchedCapabilities(
        IReadOnlyCollection<string> goalKeywords,
        IReadOnlyCollection<string> toolKeywords)
    {
        HashSet<string> goalSet = new(goalKeywords, StringComparer.OrdinalIgnoreCase);
        HashSet<string> toolSet = new(toolKeywords, StringComparer.OrdinalIgnoreCase);

        return goalSet.Intersect(toolSet, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private ToolMatch ScoreAndMatchTool(ITool tool, string goalDescription, IReadOnlyList<string> goalKeywords)
    {
        // Extract keywords from tool name and description
        IReadOnlyList<string> toolNameKeywords = ExtractKeywords(tool.Name);
        IReadOnlyList<string> toolDescKeywords = ExtractKeywords(tool.Description);

        // Combine tool keywords
        HashSet<string> allToolKeywords = new(StringComparer.OrdinalIgnoreCase);
        foreach (string keyword in toolNameKeywords)
        {
            allToolKeywords.Add(keyword);
        }

        foreach (string keyword in toolDescKeywords)
        {
            allToolKeywords.Add(keyword);
        }

        // Calculate similarity scores
        double nameSimilarity = CalculateJaccardSimilarity(goalKeywords, toolNameKeywords);
        double descSimilarity = CalculateJaccardSimilarity(goalKeywords, toolDescKeywords);

        // Weight name matches higher than description matches
        // Name match: 40%, Description match: 60%
        double combinedScore = (nameSimilarity * 0.4) + (descSimilarity * 0.6);

        // Boost score if there's an exact name match in the goal
        if (goalDescription.Contains(tool.Name, StringComparison.OrdinalIgnoreCase))
        {
            combinedScore = Math.Min(1.0, combinedScore + 0.3);
        }

        // Find matched capabilities
        IReadOnlyList<string> matchedCapabilities = FindMatchedCapabilities(goalKeywords, allToolKeywords.ToList());

        return ToolMatch.Create(tool.Name, combinedScore, matchedCapabilities);
    }
}
