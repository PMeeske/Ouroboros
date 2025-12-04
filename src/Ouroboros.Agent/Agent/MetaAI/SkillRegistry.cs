#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Skill Registry Implementation
// Manages learned skills and pattern reuse
// ==========================================================

using System.Collections.Concurrent;

namespace LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Implementation of skill registry for learning and reusing successful patterns.
/// </summary>
public sealed class SkillRegistry : ISkillRegistry
{
    private readonly ConcurrentDictionary<string, Skill> _skills = new();
    private readonly IEmbeddingModel? _embedding;

    public SkillRegistry(IEmbeddingModel? embedding = null)
    {
        _embedding = embedding;
    }

    /// <summary>
    /// Registers a new skill.
    /// </summary>
    public void RegisterSkill(Skill skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        _skills[skill.Name] = skill;
    }

    /// <summary>
    /// Finds skills matching a goal.
    /// </summary>
    public async Task<List<Skill>> FindMatchingSkillsAsync(
        string goal,
        Dictionary<string, object>? context = null)
    {
        if (string.IsNullOrWhiteSpace(goal))
            return new List<Skill>();

        List<Skill> allSkills = _skills.Values.ToList();

        if (_embedding != null)
        {
            // Use semantic similarity if embedding model available
            float[] goalEmbedding = await _embedding.CreateEmbeddingsAsync(goal);

            List<(Skill skill, double score)> skillScores = new List<(Skill skill, double score)>();
            foreach (Skill? skill in allSkills)
            {
                float[] skillEmbedding = await _embedding.CreateEmbeddingsAsync(skill.Description);
                double similarity = CosineSimilarity(goalEmbedding, skillEmbedding);
                skillScores.Add((skill, similarity));
            }

            return skillScores
                .OrderByDescending(x => x.score)
                .Select(x => x.skill)
                .ToList();
        }
        else
        {
            // Use simple keyword matching
            string goalLower = goal.ToLowerInvariant();
            return allSkills
                .Where(s => s.Description.ToLowerInvariant().Contains(goalLower) ||
                           goalLower.Contains(s.Name.ToLowerInvariant()))
                .OrderByDescending(s => s.SuccessRate)
                .ThenByDescending(s => s.UsageCount)
                .ToList();
        }
    }

    /// <summary>
    /// Gets a skill by name.
    /// </summary>
    public Skill? GetSkill(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;

        _skills.TryGetValue(name, out Skill? skill);
        return skill;
    }

    /// <summary>
    /// Records skill execution outcome.
    /// </summary>
    public void RecordSkillExecution(string name, bool success)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        _skills.AddOrUpdate(
            name,
            _ => throw new InvalidOperationException($"Skill '{name}' not found"),
            (_, existing) =>
            {
                int newCount = existing.UsageCount + 1;
                double newSuccessRate = ((existing.SuccessRate * existing.UsageCount) + (success ? 1.0 : 0.0)) / newCount;

                return existing with
                {
                    UsageCount = newCount,
                    SuccessRate = newSuccessRate,
                    LastUsed = DateTime.UtcNow
                };
            });
    }

    /// <summary>
    /// Gets all registered skills.
    /// </summary>
    public IReadOnlyList<Skill> GetAllSkills()
        => _skills.Values.OrderByDescending(s => s.SuccessRate).ToList();

    /// <summary>
    /// Extracts a skill from successful execution.
    /// </summary>
    public async Task<Result<Skill, string>> ExtractSkillAsync(
        ExecutionResult execution,
        string skillName,
        string description)
    {
        if (execution == null)
            return Result<Skill, string>.Failure("Execution cannot be null");

        if (!execution.Success)
            return Result<Skill, string>.Failure("Cannot extract skill from failed execution");

        if (string.IsNullOrWhiteSpace(skillName))
            return Result<Skill, string>.Failure("Skill name cannot be empty");

        try
        {
            // Extract prerequisites from plan context
            List<string> prerequisites = execution.Plan.Steps
                .Where(s => s.ConfidenceScore > 0.7)
                .Select(s => s.Action)
                .Distinct()
                .ToList();

            // Create skill from successful execution steps
            Skill skill = new Skill(
                skillName,
                description,
                prerequisites,
                execution.Plan.Steps,
                SuccessRate: 1.0,
                UsageCount: 0,
                CreatedAt: DateTime.UtcNow,
                LastUsed: DateTime.UtcNow);

            RegisterSkill(skill);

            return await Task.FromResult(Result<Skill, string>.Success(skill));
        }
        catch (Exception ex)
        {
            return Result<Skill, string>.Failure($"Failed to extract skill: {ex.Message}");
        }
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            return 0;

        double dotProduct = 0;
        double magnitudeA = 0;
        double magnitudeB = 0;

        for (int i = 0; i < a.Length; i++)
        {
            dotProduct += a[i] * b[i];
            magnitudeA += a[i] * a[i];
            magnitudeB += b[i] * b[i];
        }

        if (magnitudeA == 0 || magnitudeB == 0)
            return 0;

        return dotProduct / (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
    }
}
