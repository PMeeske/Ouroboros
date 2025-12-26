// <copyright file="SkillBasedDslExtension.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

// ==========================================================
// Skill-Based DSL Extension
// Dynamically exposes learned skills as DSL pipeline tokens
// ==========================================================

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Extends the DSL pipeline with dynamically learned skills from research and experience.
/// Skills are automatically converted to executable pipeline steps.
/// </summary>
public sealed class SkillBasedDslExtension
{
    private readonly ISkillRegistry _skillRegistry;
    private readonly IChatCompletionModel _model;
    private readonly Dictionary<string, DynamicSkillToken> _skillTokens = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes the skill-based DSL extension.
    /// </summary>
    public SkillBasedDslExtension(ISkillRegistry skillRegistry, IChatCompletionModel model)
    {
        _skillRegistry = skillRegistry ?? throw new ArgumentNullException(nameof(skillRegistry));
        _model = model ?? throw new ArgumentNullException(nameof(model));
    }

    /// <summary>
    /// Refreshes the available DSL tokens from the skill registry.
    /// Call this after new skills are learned.
    /// </summary>
    public void RefreshSkillTokens()
    {
        _skillTokens.Clear();

        foreach (Skill skill in _skillRegistry.GetAllSkills())
        {
            string tokenName = $"UseSkill_{SanitizeTokenName(skill.Name)}";
            _skillTokens[tokenName] = new DynamicSkillToken(skill, _model);
        }
    }

    /// <summary>
    /// Gets all available skill-based DSL tokens.
    /// </summary>
    public IReadOnlyDictionary<string, DynamicSkillToken> SkillTokens => _skillTokens;

    /// <summary>
    /// Gets suggested skill tokens for a given goal.
    /// </summary>
    public async Task<List<SkillSuggestion>> SuggestSkillsForGoalAsync(string goal, int maxSuggestions = 5)
    {
        List<Skill> matchingSkills = await _skillRegistry.FindMatchingSkillsAsync(goal);

        return matchingSkills
            .Take(maxSuggestions)
            .Select(s => new SkillSuggestion(
                TokenName: $"UseSkill_{SanitizeTokenName(s.Name)}",
                Skill: s,
                RelevanceScore: s.SuccessRate,
                UsageExample: GenerateUsageExample(s)))
            .ToList();
    }

    /// <summary>
    /// Tries to resolve a skill token to an executable skill execution.
    /// </summary>
    public bool TryResolveSkillToken(
        string tokenName,
        string? args,
        out Func<string, Task<string>>? executor)
    {
        executor = null;

        if (!tokenName.StartsWith("UseSkill_", StringComparison.OrdinalIgnoreCase))
            return false;

        if (!_skillTokens.TryGetValue(tokenName, out DynamicSkillToken? skillToken))
        {
            // Try to find by skill name directly
            string skillName = tokenName.Replace("UseSkill_", string.Empty);
            Skill? skill = _skillRegistry.GetSkill(skillName);
            if (skill == null)
                return false;

            skillToken = new DynamicSkillToken(skill, _model);
        }

        executor = input => skillToken.ExecuteAsync(input, args);
        return true;
    }

    /// <summary>
    /// Generates DSL help text for all available skill tokens.
    /// </summary>
    public string GenerateSkillTokenHelp()
    {
        if (_skillTokens.Count == 0)
        {
            return "No learned skills available yet. Skills are extracted from successful research analysis.";
        }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== Learned Skill Tokens (Dynamically Generated) ===\n");

        foreach (var (tokenName, skillToken) in _skillTokens.OrderBy(x => x.Key))
        {
            Skill skill = skillToken.Skill;
            sb.AppendLine($"  {tokenName}");
            sb.AppendLine($"    Description: {skill.Description}");
            sb.AppendLine($"    Success Rate: {skill.SuccessRate:P0} | Uses: {skill.UsageCount}");
            if (skill.Prerequisites.Count > 0)
            {
                sb.AppendLine($"    Prerequisites: {string.Join(", ", skill.Prerequisites)}");
            }

            sb.AppendLine($"    Steps: {skill.Steps.Count} operations");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Creates a composite DSL pipeline from multiple skills.
    /// </summary>
    public string GenerateSkillPipeline(IEnumerable<string> skillNames)
    {
        var parts = new List<string>();

        foreach (string name in skillNames)
        {
            string tokenName = name.StartsWith("UseSkill_", StringComparison.OrdinalIgnoreCase)
                ? name
                : $"UseSkill_{SanitizeTokenName(name)}";

            if (_skillTokens.ContainsKey(tokenName))
            {
                parts.Add(tokenName);
            }
        }

        return string.Join(" | ", parts);
    }

    private static string SanitizeTokenName(string name)
    {
        return new string(name
            .Replace(" ", "_")
            .Replace("-", "_")
            .Where(c => char.IsLetterOrDigit(c) || c == '_')
            .ToArray());
    }

    private static string GenerateUsageExample(Skill skill)
    {
        string tokenName = $"UseSkill_{SanitizeTokenName(skill.Name)}";
        return $"SetPrompt \"your query\" | {tokenName} | UseOutput";
    }
}

/// <summary>
/// A dynamically created DSL token from a learned skill.
/// </summary>
public sealed class DynamicSkillToken
{
    private readonly IChatCompletionModel _model;

    /// <summary>
    /// Initializes a dynamic skill token.
    /// </summary>
    public DynamicSkillToken(Skill skill, IChatCompletionModel model)
    {
        Skill = skill ?? throw new ArgumentNullException(nameof(skill));
        _model = model ?? throw new ArgumentNullException(nameof(model));
    }

    /// <summary>
    /// Gets the underlying skill.
    /// </summary>
    public Skill Skill { get; }

    /// <summary>
    /// Executes this skill with the given input.
    /// </summary>
    public async Task<string> ExecuteAsync(string input, string? args)
    {
        // Execute each step in the skill
        var context = new Dictionary<string, string>
        {
            ["input"] = input,
            ["args"] = args ?? string.Empty,
        };

        string currentOutput = input;

        foreach (PlanStep planStep in Skill.Steps)
        {
            string stepPrompt = $@"Execute this step: {planStep.Action}

Input: {currentOutput}
Expected output: {planStep.ExpectedOutcome}

Perform this step and return the result.";

            string stepResult = await _model.GenerateTextAsync(stepPrompt);
            currentOutput = stepResult;
        }

        return currentOutput;
    }
}

/// <summary>
/// Represents a skill suggestion for DSL autocompletion.
/// </summary>
public sealed record SkillSuggestion(
    string TokenName,
    Skill Skill,
    double RelevanceScore,
    string UsageExample);
