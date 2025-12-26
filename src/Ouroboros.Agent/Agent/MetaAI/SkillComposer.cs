#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Skill Composition - Combine skills into higher-level skills
// ==========================================================

namespace Ouroboros.Agent.MetaAI;

/// <summary>
/// Marker interface for composite skills.
/// </summary>
public interface ICompositeSkill
{
    List<string> ComponentSkills { get; }
}

/// <summary>
/// Configuration for skill composition.
/// </summary>
public sealed record SkillCompositionConfig(
    int MaxComponentSkills = 5,
    double MinComponentQuality = 0.7,
    bool AllowRecursiveComposition = false);

/// <summary>
/// Interface for skill composition capabilities.
/// </summary>
public interface ISkillComposer
{
    /// <summary>
    /// Composes multiple skills into a higher-level skill.
    /// </summary>
    Task<Result<Skill, string>> ComposeSkillsAsync(
        string compositeName,
        string description,
        List<string> componentSkillNames,
        SkillCompositionConfig? config = null,
        CancellationToken ct = default);

    /// <summary>
    /// Suggests skill compositions based on usage patterns.
    /// </summary>
    Task<List<(List<string> skills, double score)>> SuggestCompositionsAsync(
        int maxSuggestions = 5,
        CancellationToken ct = default);

    /// <summary>
    /// Decomposes a composite skill into its components.
    /// </summary>
    Result<List<Skill>, string> DecomposeSkill(string compositeName);
}

/// <summary>
/// Implementation of skill composition for creating higher-level skills.
/// </summary>
public sealed class SkillComposer : ISkillComposer
{
    private readonly ISkillRegistry _skills;
    private readonly IMemoryStore _memory;

    public SkillComposer(ISkillRegistry skills, IMemoryStore memory)
    {
        _skills = skills ?? throw new ArgumentNullException(nameof(skills));
        _memory = memory ?? throw new ArgumentNullException(nameof(memory));
    }

    /// <summary>
    /// Composes multiple skills into a higher-level skill.
    /// </summary>
    public async Task<Result<Skill, string>> ComposeSkillsAsync(
        string compositeName,
        string description,
        List<string> componentSkillNames,
        SkillCompositionConfig? config = null,
        CancellationToken ct = default)
    {
        config ??= new SkillCompositionConfig();

        if (string.IsNullOrWhiteSpace(compositeName))
            return Result<Skill, string>.Failure("Composite name cannot be empty");

        if (componentSkillNames.Count == 0)
            return Result<Skill, string>.Failure("Must provide at least one component skill");

        if (componentSkillNames.Count > config.MaxComponentSkills)
            return Result<Skill, string>.Failure($"Too many component skills (max: {config.MaxComponentSkills})");

        try
        {
            // Retrieve all component skills
            List<Skill> componentSkills = new List<Skill>();
            foreach (string skillName in componentSkillNames)
            {
                Skill? skill = _skills.GetSkill(skillName);
                if (skill == null)
                    return Result<Skill, string>.Failure($"Component skill '{skillName}' not found");

                if (skill.SuccessRate < config.MinComponentQuality)
                    return Result<Skill, string>.Failure($"Component skill '{skillName}' quality too low ({skill.SuccessRate:P0})");

                // Check for recursive composition - use metadata to track
                if (!config.AllowRecursiveComposition && skill.Prerequisites.Contains("__composite__"))
                    return Result<Skill, string>.Failure($"Recursive composition not allowed: '{skillName}' is already composite");

                componentSkills.Add(skill);
            }

            // Compose steps from all component skills
            List<PlanStep> composedSteps = new List<PlanStep>();
            foreach (Skill skill in componentSkills)
            {
                composedSteps.AddRange(skill.Steps);
            }

            // Calculate composite success rate
            double avgSuccessRate = componentSkills.Average(s => s.SuccessRate);

            // Create composite skill with special prerequisite marker and component list
            List<string> prerequisites = new List<string>(componentSkillNames) { "__composite__" };

            Skill compositeSkill = new Skill(
                compositeName,
                description,
                prerequisites,
                composedSteps,
                avgSuccessRate,
                UsageCount: 0,
                DateTime.UtcNow,
                DateTime.UtcNow);

            // Register the composite skill
            _skills.RegisterSkill(compositeSkill);

            return await Task.FromResult(Result<Skill, string>.Success(compositeSkill));
        }
        catch (Exception ex)
        {
            return Result<Skill, string>.Failure($"Skill composition failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Suggests skill compositions based on usage patterns.
    /// </summary>
    public async Task<List<(List<string> skills, double score)>> SuggestCompositionsAsync(
        int maxSuggestions = 5,
        CancellationToken ct = default)
    {
        List<(List<string> skills, double score)> suggestions = new List<(List<string> skills, double score)>();

        try
        {
            // Get all experiences
            MemoryQuery query = new MemoryQuery(
                Goal: "",
                Context: null,
                MaxResults: 100,
                MinSimilarity: 0.0);

            List<Experience> experiences = await _memory.RetrieveRelevantExperiencesAsync(query, ct);

            // Analyze which skills are used together
            Dictionary<string, int> skillCombinations = new Dictionary<string, int>();

            foreach (Experience? exp in experiences.Where(e => e.Verification.Verified))
            {
                List<string> usedSkills = ExtractUsedSkills(exp);

                if (usedSkills.Count >= 2)
                {
                    string combo = string.Join("|", usedSkills.OrderBy(s => s));
                    skillCombinations[combo] = skillCombinations.GetValueOrDefault(combo, 0) + 1;
                }
            }

            // Sort by frequency and create suggestions
            List<KeyValuePair<string, int>> topCombos = skillCombinations
                .OrderByDescending(kv => kv.Value)
                .Take(maxSuggestions)
                .ToList();

            foreach ((string combo, int count) in topCombos)
            {
                List<string> skills = combo.Split('|').ToList();
                double score = (double)count / experiences.Count;
                suggestions.Add((skills, score));
            }
        }
        catch
        {
            // Return empty suggestions on error
        }

        return suggestions;
    }

    /// <summary>
    /// Decomposes a composite skill into its components.
    /// </summary>
    public Result<List<Skill>, string> DecomposeSkill(string compositeName)
    {
        if (string.IsNullOrWhiteSpace(compositeName))
            return Result<List<Skill>, string>.Failure("Composite name cannot be empty");

        Skill? skill = _skills.GetSkill(compositeName);
        if (skill == null)
            return Result<List<Skill>, string>.Failure($"Skill '{compositeName}' not found");

        // Check if it's composite by looking for the marker in prerequisites
        if (!skill.Prerequisites.Contains("__composite__"))
            return Result<List<Skill>, string>.Failure($"Skill '{compositeName}' is not a composite skill");

        // Component names are in prerequisites (excluding the marker)
        List<string> componentNames = skill.Prerequisites.Where(p => p != "__composite__").ToList();
        List<Skill> components = new List<Skill>();

        foreach (string? componentName in componentNames)
        {
            Skill? component = _skills.GetSkill(componentName);
            if (component != null)
            {
                components.Add(component);
            }
        }

        return Result<List<Skill>, string>.Success(components);
    }

    private List<string> ExtractUsedSkills(Experience experience)
    {
        List<string> usedSkills = new List<string>();

        // Check which registered skills were used in the plan
        IReadOnlyList<Skill> allSkills = _skills.GetAllSkills();

        foreach (Skill skill in allSkills)
        {
            HashSet<string> skillActions = skill.Steps.Select(s => s.Action).ToHashSet();
            HashSet<string> planActions = experience.Plan.Steps.Select(s => s.Action).ToHashSet();

            // If skill actions are subset of plan actions, the skill was likely used
            if (skillActions.IsSubsetOf(planActions))
            {
                usedSkills.Add(skill.Name);
            }
        }

        return usedSkills;
    }
}
