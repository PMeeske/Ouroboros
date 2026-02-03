// <copyright file="MockSkillRegistry.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Mocks;

using Ouroboros.Agent.MetaAI;

/// <summary>
/// Mock implementation of ISkillRegistry for testing purposes.
/// Provides in-memory skill storage with configurable behavior.
/// </summary>
public sealed class MockSkillRegistry : ISkillRegistry
{
    private readonly Dictionary<string, Skill> skills = new();
    private readonly Func<string, Dictionary<string, object>?, List<Skill>>? matchingFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockSkillRegistry"/> class.
    /// </summary>
    public MockSkillRegistry()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MockSkillRegistry"/> class with custom matching logic.
    /// </summary>
    /// <param name="matchingFactory">Factory function for finding matching skills.</param>
    public MockSkillRegistry(Func<string, Dictionary<string, object>?, List<Skill>> matchingFactory)
    {
        this.matchingFactory = matchingFactory;
    }

    /// <summary>
    /// Gets the number of registered skills.
    /// </summary>
    public int Count => this.skills.Count;

    /// <summary>
    /// Gets the last skill that was registered.
    /// </summary>
    public Skill? LastRegisteredSkill { get; private set; }

    /// <summary>
    /// Gets the number of times RegisterSkill was called.
    /// </summary>
    public int RegisterCallCount { get; private set; }

    /// <summary>
    /// Registers a new skill.
    /// </summary>
    public void RegisterSkill(Skill skill)
    {
        this.RegisterCallCount++;
        this.LastRegisteredSkill = skill;
        this.skills[skill.Name] = skill;
    }

    /// <summary>
    /// Registers a new skill asynchronously.
    /// </summary>
    public Task RegisterSkillAsync(Skill skill, CancellationToken ct = default)
    {
        this.RegisterSkill(skill);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Finds skills matching the given goal and context.
    /// </summary>
    public Task<List<Skill>> FindMatchingSkillsAsync(
        string goal,
        Dictionary<string, object>? context = null)
    {
        if (this.matchingFactory != null)
        {
            return Task.FromResult(this.matchingFactory(goal, context));
        }

        // Default: return skills whose description contains the goal (case-insensitive)
        var matches = this.skills.Values
            .Where(s => s.Description.Contains(goal, StringComparison.OrdinalIgnoreCase) ||
                        s.Name.Contains(goal, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(s => s.SuccessRate)
            .ToList();

        return Task.FromResult(matches);
    }

    /// <summary>
    /// Gets a skill by name.
    /// </summary>
    public Skill? GetSkill(string name)
    {
        this.skills.TryGetValue(name, out var skill);
        return skill;
    }

    /// <summary>
    /// Records skill execution and updates metrics.
    /// </summary>
    public void RecordSkillExecution(string name, bool success)
    {
        if (this.skills.TryGetValue(name, out var skill))
        {
            var newUsageCount = skill.UsageCount + 1;
            var newSuccessRate = success
                ? ((skill.SuccessRate * skill.UsageCount) + 1.0) / newUsageCount
                : (skill.SuccessRate * skill.UsageCount) / newUsageCount;

            var updatedSkill = skill with
            {
                SuccessRate = newSuccessRate,
                UsageCount = newUsageCount,
                LastUsed = DateTime.UtcNow
            };

            this.skills[name] = updatedSkill;
        }
    }

    /// <summary>
    /// Gets all registered skills.
    /// </summary>
    public IReadOnlyList<Skill> GetAllSkills()
    {
        return this.skills.Values.ToList();
    }

    /// <summary>
    /// Extracts and registers a skill from an execution result.
    /// </summary>
    public Task<Result<Skill, string>> ExtractSkillAsync(
        ExecutionResult execution,
        string skillName,
        string description)
    {
        if (!execution.Success)
        {
            return Task.FromResult(Result<Skill, string>.Failure("Cannot extract skill from failed execution"));
        }

        var skill = new Skill(
            Name: skillName,
            Description: description,
            Prerequisites: new List<string>(),
            Steps: execution.Plan.Steps,
            SuccessRate: 1.0,
            UsageCount: 0,
            CreatedAt: DateTime.UtcNow,
            LastUsed: DateTime.UtcNow);

        this.RegisterSkill(skill);
        return Task.FromResult(Result<Skill, string>.Success(skill));
    }

    /// <summary>
    /// Adds a pre-configured skill for testing.
    /// </summary>
    /// <param name="skill">Skill to add.</param>
    public void AddSkill(Skill skill)
    {
        this.skills[skill.Name] = skill;
    }
}
