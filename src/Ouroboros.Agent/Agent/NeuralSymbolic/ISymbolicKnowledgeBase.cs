#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Symbolic Knowledge Base Interface
// Manages symbolic rules and MeTTa reasoning
// ==========================================================

namespace Ouroboros.Agent.NeuralSymbolic;

/// <summary>
/// Interface for managing symbolic knowledge and executing MeTTa queries.
/// </summary>
public interface ISymbolicKnowledgeBase
{
    /// <summary>
    /// Adds a symbolic rule to the knowledge base.
    /// </summary>
    /// <param name="rule">The rule to add.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Success or error.</returns>
    Task<Result<Unit, string>> AddRuleAsync(SymbolicRule rule, CancellationToken ct = default);

    /// <summary>
    /// Queries rules matching a pattern.
    /// </summary>
    /// <param name="pattern">The pattern to match.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of matching rules.</returns>
    Task<Result<List<SymbolicRule>, string>> QueryRulesAsync(string pattern, CancellationToken ct = default);

    /// <summary>
    /// Executes a MeTTa query against the knowledge base.
    /// </summary>
    /// <param name="query">The MeTTa query.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Query result as string.</returns>
    Task<Result<string, string>> ExecuteMeTTaQueryAsync(string query, CancellationToken ct = default);

    /// <summary>
    /// Performs inference from a fact using rules in the knowledge base.
    /// </summary>
    /// <param name="fact">The starting fact.</param>
    /// <param name="maxDepth">Maximum inference depth.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of inferred facts.</returns>
    Task<Result<List<string>, string>> InferAsync(string fact, int maxDepth = 5, CancellationToken ct = default);

    /// <summary>
    /// Gets the number of rules in the knowledge base.
    /// </summary>
    int RuleCount { get; }
}
