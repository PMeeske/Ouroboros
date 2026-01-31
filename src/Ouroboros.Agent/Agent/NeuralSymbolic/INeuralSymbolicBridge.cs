#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Neural-Symbolic Bridge Interface
// Bridges neural (LLM) and symbolic (MeTTa) reasoning
// ==========================================================

namespace Ouroboros.Agent.NeuralSymbolic;

using Ouroboros.Agent.MetaAI;

/// <summary>
/// Interface for bridging neural and symbolic reasoning systems.
/// Enables rule extraction, hybrid reasoning, and logical consistency checking.
/// </summary>
public interface INeuralSymbolicBridge
{
    /// <summary>
    /// Extracts symbolic rules from learned skills.
    /// </summary>
    /// <param name="skill">The skill to extract rules from.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>List of extracted symbolic rules.</returns>
    Task<Result<List<SymbolicRule>, string>> ExtractRulesFromSkillAsync(
        Skill skill,
        CancellationToken ct = default);

    /// <summary>
    /// Converts natural language to MeTTa expressions.
    /// </summary>
    /// <param name="naturalLanguage">The natural language description.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Parsed MeTTa expression.</returns>
    Task<Result<MeTTaExpression, string>> NaturalLanguageToMeTTaAsync(
        string naturalLanguage,
        CancellationToken ct = default);

    /// <summary>
    /// Converts MeTTa expressions to natural language explanation.
    /// </summary>
    /// <param name="expression">The MeTTa expression.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Natural language explanation.</returns>
    Task<Result<string, string>> MeTTaToNaturalLanguageAsync(
        MeTTaExpression expression,
        CancellationToken ct = default);

    /// <summary>
    /// Performs symbolic reasoning with neural fallback.
    /// </summary>
    /// <param name="query">The reasoning query.</param>
    /// <param name="mode">The reasoning mode to use.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Reasoning result with steps and confidence.</returns>
    Task<Result<ReasoningResult, string>> HybridReasonAsync(
        string query,
        ReasoningMode mode = ReasoningMode.SymbolicFirst,
        CancellationToken ct = default);

    /// <summary>
    /// Checks logical consistency of a hypothesis.
    /// </summary>
    /// <param name="hypothesis">The hypothesis to check.</param>
    /// <param name="knowledgeBase">The symbolic knowledge base to check against.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Consistency report with conflicts and suggestions.</returns>
    Task<Result<ConsistencyReport, string>> CheckConsistencyAsync(
        MetaAI.Hypothesis hypothesis,
        IReadOnlyList<SymbolicRule> knowledgeBase,
        CancellationToken ct = default);

    /// <summary>
    /// Grounds neural embeddings in symbolic concepts.
    /// </summary>
    /// <param name="conceptDescription">Description of the concept.</param>
    /// <param name="embedding">Neural embedding vector.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Grounded concept with symbolic representation.</returns>
    Task<Result<GroundedConcept, string>> GroundConceptAsync(
        string conceptDescription,
        float[] embedding,
        CancellationToken ct = default);
}
