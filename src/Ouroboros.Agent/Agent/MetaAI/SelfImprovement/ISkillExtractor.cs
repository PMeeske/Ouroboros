#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Skill Extractor Interface
// Automatic extraction of reusable skills from successful executions
// ==========================================================

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Configuration for skill extraction behavior.
/// </summary>
public sealed record SkillExtractionConfig(
    double MinQualityThreshold = 0.8,
    int MinStepsForExtraction = 2,
    int MaxStepsPerSkill = 10,
    bool EnableAutoParameterization = true,
    bool EnableSkillVersioning = true);

/// <summary>
/// Interface for automatic skill extraction from successful executions.
/// Analyzes execution patterns and extracts reusable skills.
/// </summary>
public interface ISkillExtractor
{
    /// <summary>
    /// Extracts a skill from a successful execution.
    /// </summary>
    /// <param name="execution">The successful execution result</param>
    /// <param name="verification">The verification result with quality metrics</param>
    /// <param name="config">Optional extraction configuration</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Extracted skill or error message</returns>
    Task<Result<Skill, string>> ExtractSkillAsync(
        ExecutionResult execution,
        VerificationResult verification,
        SkillExtractionConfig? config = null,
        CancellationToken ct = default);

    /// <summary>
    /// Determines if a skill should be extracted from the given verification result.
    /// </summary>
    /// <param name="verification">The verification result to analyze</param>
    /// <param name="config">Optional extraction configuration</param>
    /// <returns>True if skill should be extracted, false otherwise</returns>
    Task<bool> ShouldExtractSkillAsync(
        VerificationResult verification,
        SkillExtractionConfig? config = null);

    /// <summary>
    /// Generates a descriptive name for the extracted skill using LLM.
    /// </summary>
    /// <param name="execution">The execution to analyze</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Generated skill name</returns>
    Task<string> GenerateSkillNameAsync(
        ExecutionResult execution,
        CancellationToken ct = default);

    /// <summary>
    /// Generates a description for the extracted skill using LLM.
    /// </summary>
    /// <param name="execution">The execution to analyze</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Generated skill description</returns>
    Task<string> GenerateSkillDescriptionAsync(
        ExecutionResult execution,
        CancellationToken ct = default);
}
