#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Symbolic Knowledge Base Implementation
// Manages symbolic rules and executes MeTTa queries
// ==========================================================

namespace Ouroboros.Agent.NeuralSymbolic;

using System.Collections.Concurrent;
using Ouroboros.Tools.MeTTa;

/// <summary>
/// Implementation of symbolic knowledge base using MeTTa engine.
/// </summary>
public sealed class SymbolicKnowledgeBase : ISymbolicKnowledgeBase
{
    private readonly IMeTTaEngine _mettaEngine;
    private readonly ConcurrentDictionary<string, SymbolicRule> _rules = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SymbolicKnowledgeBase"/> class.
    /// </summary>
    /// <param name="mettaEngine">The MeTTa engine to use.</param>
    public SymbolicKnowledgeBase(IMeTTaEngine mettaEngine)
    {
        _mettaEngine = mettaEngine ?? throw new ArgumentNullException(nameof(mettaEngine));
    }

    /// <inheritdoc/>
    public int RuleCount => _rules.Count;

    /// <inheritdoc/>
    public async Task<Result<Tools.MeTTa.Unit, string>> AddRuleAsync(SymbolicRule rule, CancellationToken ct = default)
    {
        if (rule == null)
            return Result<Tools.MeTTa.Unit, string>.Failure("Rule cannot be null");

        try
        {
            // Add the rule to MeTTa engine
            var addResult = await _mettaEngine.AddFactAsync(rule.MeTTaRepresentation, ct);
            if (addResult.IsFailure)
                return Result<Tools.MeTTa.Unit, string>.Failure(addResult.Error);

            // Store in local dictionary
            _rules[rule.Name] = rule;

            return Result<Tools.MeTTa.Unit, string>.Success(Tools.MeTTa.Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Tools.MeTTa.Unit, string>.Failure($"Failed to add rule: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<List<SymbolicRule>, string>> QueryRulesAsync(string pattern, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            return Result<List<SymbolicRule>, string>.Failure("Pattern cannot be empty");

        try
        {
            // Simple pattern matching for now - match rules by name or description
            var patternLower = pattern.ToLowerInvariant();
            var matchingRules = _rules.Values
                .Where(r => r.Name.ToLowerInvariant().Contains(patternLower) ||
                           r.NaturalLanguageDescription.ToLowerInvariant().Contains(patternLower))
                .ToList();

            return Result<List<SymbolicRule>, string>.Success(matchingRules);
        }
        catch (Exception ex)
        {
            return Result<List<SymbolicRule>, string>.Failure($"Failed to query rules: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<string, string>> ExecuteMeTTaQueryAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Result<string, string>.Failure("Query cannot be empty");

        try
        {
            var result = await _mettaEngine.ExecuteQueryAsync(query, ct);
            return result;
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Failed to execute MeTTa query: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<List<string>, string>> InferAsync(string fact, int maxDepth = 5, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(fact))
            return Result<List<string>, string>.Failure("Fact cannot be empty");

        if (maxDepth <= 0)
            return Result<List<string>, string>.Failure("Max depth must be positive");

        try
        {
            var inferred = new List<string>();
            var visited = new HashSet<string> { fact };
            var queue = new Queue<(string currentFact, int depth)>();
            queue.Enqueue((fact, 0));

            while (queue.Count > 0 && inferred.Count < 100) // Limit results
            {
                var (currentFact, depth) = queue.Dequeue();
                
                if (depth >= maxDepth)
                    continue;

                // Query MeTTa for applicable rules
                var queryResult = await _mettaEngine.ExecuteQueryAsync($"(match &self {currentFact} $x)", ct);
                if (queryResult.IsSuccess && !string.IsNullOrWhiteSpace(queryResult.Value))
                {
                    // Parse results and add to inferred list
                    var results = ParseMeTTaResults(queryResult.Value);
                    foreach (var result in results)
                    {
                        if (visited.Add(result))
                        {
                            inferred.Add(result);
                            queue.Enqueue((result, depth + 1));
                        }
                    }
                }
            }

            return Result<List<string>, string>.Success(inferred);
        }
        catch (Exception ex)
        {
            return Result<List<string>, string>.Failure($"Failed to perform inference: {ex.Message}");
        }
    }

    private static List<string> ParseMeTTaResults(string results)
    {
        // Simple parsing - split by newlines and clean up
        return results.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(r => r.Trim())
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .ToList();
    }
}
