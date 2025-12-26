#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Skill Registry - Learn and store reusable skills
// ==========================================================

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Represents a learned skill that can be reused.
/// </summary>
public sealed record Skill(
    string Name,
    string Description,
    List<string> Prerequisites,
    List<PlanStep> Steps,
    double SuccessRate,
    int UsageCount,
    DateTime CreatedAt,
    DateTime LastUsed);

/// <summary>
/// Interface for skill acquisition and management.
/// Enables the system to learn and reuse successful patterns.
/// </summary>
public interface ISkillRegistry
{
    /// <summary>
    /// Registers a new skill learned from successful execution.
    /// </summary>
    /// <param name="skill">The skill to register</param>
    void RegisterSkill(Skill skill);

    /// <summary>
    /// Registers a new skill asynchronously with immediate persistence.
    /// Use this method when you need to ensure the skill is persisted before continuing.
    /// </summary>
    /// <param name="skill">The skill to register</param>
    /// <param name="ct">Cancellation token</param>
    Task RegisterSkillAsync(Skill skill, CancellationToken ct = default);

    /// <summary>
    /// Finds skills that match the given goal and context.
    /// </summary>
    /// <param name="goal">The goal to accomplish</param>
    /// <param name="context">Optional context information</param>
    /// <returns>List of matching skills sorted by relevance</returns>
    Task<List<Skill>> FindMatchingSkillsAsync(
        string goal,
        Dictionary<string, object>? context = null);

    /// <summary>
    /// Gets a skill by name.
    /// </summary>
    /// <param name="name">The skill name</param>
    /// <returns>The skill if found, null otherwise</returns>
    Skill? GetSkill(string name);

    /// <summary>
    /// Updates skill metrics after execution.
    /// </summary>
    /// <param name="name">The skill name</param>
    /// <param name="success">Whether the execution succeeded</param>
    void RecordSkillExecution(string name, bool success);

    /// <summary>
    /// Gets all registered skills.
    /// </summary>
    /// <returns>All skills in the registry</returns>
    IReadOnlyList<Skill> GetAllSkills();

    /// <summary>
    /// Extracts and registers a skill from a successful execution.
    /// </summary>
    /// <param name="execution">The successful execution result</param>
    /// <param name="skillName">The name for the new skill</param>
    /// <param name="description">Description of what the skill does</param>
    Task<Result<Skill, string>> ExtractSkillAsync(
        ExecutionResult execution,
        string skillName,
        string description);
}
