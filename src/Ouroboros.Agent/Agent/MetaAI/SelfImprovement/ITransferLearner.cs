#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Transfer Learning Interface
// Domain adaptation and analogical reasoning
// ==========================================================

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents the result of a skill transfer attempt.
/// </summary>
public sealed record TransferResult(
    Skill AdaptedSkill,
    double TransferabilityScore,
    string SourceDomain,
    string TargetDomain,
    List<string> Adaptations,
    DateTime TransferredAt);

/// <summary>
/// Configuration for transfer learning behavior.
/// </summary>
public sealed record TransferLearningConfig(
    double MinTransferabilityThreshold = 0.5,
    int MaxAdaptationAttempts = 3,
    bool EnableAnalogicalReasoning = true,
    bool TrackTransferHistory = true);

/// <summary>
/// Interface for transfer learning capabilities.
/// Enables applying learned skills across different domains.
/// </summary>
public interface ITransferLearner
{
    /// <summary>
    /// Adapts a skill from one domain to another.
    /// </summary>
    /// <param name="sourceSkill">The skill to adapt</param>
    /// <param name="targetDomain">The target domain description</param>
    /// <param name="config">Optional configuration</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Adapted skill or error message</returns>
    Task<Result<TransferResult, string>> AdaptSkillToDomainAsync(
        Skill sourceSkill,
        string targetDomain,
        TransferLearningConfig? config = null,
        CancellationToken ct = default);

    /// <summary>
    /// Estimates how well a skill can transfer to a new domain.
    /// </summary>
    /// <param name="skill">The skill to evaluate</param>
    /// <param name="targetDomain">The target domain</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Transferability score (0-1)</returns>
    Task<double> EstimateTransferabilityAsync(
        Skill skill,
        string targetDomain,
        CancellationToken ct = default);

    /// <summary>
    /// Finds analogies between domains to guide transfer.
    /// </summary>
    /// <param name="sourceDomain">Source domain description</param>
    /// <param name="targetDomain">Target domain description</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of analogical mappings</returns>
    Task<List<(string source, string target, double confidence)>> FindAnalogiesAsync(
        string sourceDomain,
        string targetDomain,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the transfer history for a skill.
    /// </summary>
    /// <param name="skillName">Name of the skill</param>
    /// <returns>List of transfer attempts</returns>
    List<TransferResult> GetTransferHistory(string skillName);

    /// <summary>
    /// Validates if a transferred skill works in the target domain.
    /// </summary>
    /// <param name="transferResult">The transfer result to validate</param>
    /// <param name="success">Whether the validation was successful</param>
    void RecordTransferValidation(TransferResult transferResult, bool success);
}
