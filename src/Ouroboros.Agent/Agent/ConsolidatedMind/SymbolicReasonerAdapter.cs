// <copyright file="SymbolicReasonerAdapter.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using System.Text.RegularExpressions;
using Ouroboros.Agent.NeuralSymbolic;
using Ouroboros.Tools.MeTTa;

namespace Ouroboros.Agent.ConsolidatedMind;

/// <summary>
/// Adapts the symbolic reasoning engine (MeTTa/Hyperon) to the IChatCompletionModel interface,
/// allowing it to serve as a specialist within the ConsolidatedMind.
///
/// This enables symbolic reasoning as the ultimate fallback when all LLM-based
/// specialists are unavailable. While responses may be less fluent than LLM output,
/// they provide deterministic, logic-based answers that don't require external services.
/// </summary>
public sealed class SymbolicReasonerAdapter : IChatCompletionModel
{
    private readonly INeuralSymbolicBridge? _bridge;
    private readonly IMeTTaEngine? _engine;

    /// <summary>
    /// Initializes a new instance of the <see cref="SymbolicReasonerAdapter"/> class.
    /// Uses the neural-symbolic bridge for hybrid reasoning with symbolic-only mode.
    /// </summary>
    /// <param name="bridge">The neural-symbolic bridge to use for reasoning.</param>
    public SymbolicReasonerAdapter(INeuralSymbolicBridge bridge)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        _engine = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SymbolicReasonerAdapter"/> class.
    /// Uses the MeTTa engine directly for symbolic reasoning.
    /// </summary>
    /// <param name="engine">The MeTTa engine to use for reasoning.</param>
    public SymbolicReasonerAdapter(IMeTTaEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _bridge = null;
    }

    /// <inheritdoc/>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        // Normalize prompt to prevent null reference exceptions
        prompt ??= string.Empty;

        try
        {
            // Strategy 1: Use bridge with SymbolicOnly mode if available
            if (_bridge != null)
            {
                var result = await _bridge.HybridReasonAsync(prompt, ReasoningMode.SymbolicOnly, ct);

                if (result.IsSuccess)
                {
                    return FormatSymbolicResponse(result.Value.Answer, result.Value.Steps);
                }

                // Bridge failed, but we must return something as the ultimate fallback
                return FormatLimitedResponse(prompt, "Symbolic reasoning bridge unavailable");
            }

            // Strategy 2: Use engine directly if available
            if (_engine != null)
            {
                var query = ExtractQueryFromPrompt(prompt);
                var result = await _engine.ExecuteQueryAsync(query, ct);

                if (result.IsSuccess)
                {
                    return FormatSymbolicResponse(result.Value, null);
                }

                // Engine failed, return limited response
                return FormatLimitedResponse(prompt, "Symbolic reasoning engine could not process query");
            }

            // Should never reach here due to constructor validation, but handle gracefully
            return FormatLimitedResponse(prompt, "No symbolic reasoning engine configured");
        }
        catch (Exception ex)
        {
            // As the ultimate fallback, we NEVER throw - always return something
            return FormatLimitedResponse(prompt, $"Symbolic reasoning encountered an error: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts key terms from a natural language prompt and constructs a MeTTa query.
    /// This is a simplified heuristic approach for direct engine usage.
    /// </summary>
    private static string ExtractQueryFromPrompt(string prompt)
    {
        // Remove common question words and punctuation
        var cleaned = Regex.Replace(prompt, @"^(what|how|why|when|where|who|which|can|is|are|do|does)\s+", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"[?!.,;:]", "");

        // Extract key terms (words longer than 3 characters)
        var terms = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 3)
            .Take(5) // Limit to first 5 key terms
            .ToList();

        if (terms.Count == 0)
        {
            // Fallback to a generic query
            return "(query (concept general))";
        }

        // Construct a simple MeTTa query
        var queryTerms = string.Join(" ", terms.Select(t => $"(concept {t.ToLowerInvariant()})"));
        return $"(query {queryTerms})";
    }

    /// <summary>
    /// Formats a symbolic reasoning response with clear indication of its source.
    /// </summary>
    private static string FormatSymbolicResponse(string answer, List<Agent.NeuralSymbolic.ReasoningStep>? steps)
    {
        var formatted = new System.Text.StringBuilder();
        formatted.Append("[Symbolic Reasoning]\n\n");
        formatted.Append(answer);

        if (steps != null && steps.Count > 0)
        {
            formatted.Append("\n\nReasoning Steps:");
            foreach (var step in steps)
            {
                formatted.Append($"\n{step.StepNumber}. {step.Description}");
                if (!string.IsNullOrEmpty(step.RuleApplied))
                {
                    formatted.Append($" (Rule: {step.RuleApplied})");
                }
            }
        }

        return formatted.ToString();
    }

    /// <summary>
    /// Formats a limited/degraded response when symbolic reasoning cannot be performed.
    /// Still provides acknowledgment and context rather than failing completely.
    /// </summary>
    private static string FormatLimitedResponse(string prompt, string reason)
    {
        return $@"[Symbolic Reasoning - Limited Mode]

I apologize, but I'm currently operating with limited reasoning capabilities.

Your query: {(prompt.Length > 200 ? prompt[..200] + "..." : prompt)}

Status: {reason}

As the ultimate fallback system, I can acknowledge your request but cannot provide a complete symbolic reasoning analysis at this time. This typically occurs when:
- All neural language models are unavailable
- Symbolic reasoning engine is not properly configured
- The query format is not compatible with symbolic reasoning

Please try again later or check your system configuration.";
    }
}
