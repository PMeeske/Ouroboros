// <copyright file="SkillRegistry.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Agent.MetaAI;

using System.Collections.Concurrent;

/// <summary>
/// Implementation of skill registry for learning and reusing successful patterns.
/// </summary>
public sealed class SkillRegistry : ISkillRegistry
{
    private readonly ConcurrentDictionary<string, Skill> skills = new();
    private readonly IEmbeddingModel? embedding;

    public SkillRegistry(IEmbeddingModel? embedding = null)
    {
        this.embedding = embedding;
    }

    /// <summary>
    /// Registers a new skill.
    /// </summary>
    public void RegisterSkill(Skill skill)
    {
        ArgumentNullException.ThrowIfNull(skill);
        this.skills[skill.Name] = skill;
    }

    /// <summary>
    /// Finds skills matching a goal.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<List<Skill>> FindMatchingSkillsAsync(
        string goal,
        Dictionary<string, object>? context = null)
    {
        if (string.IsNullOrWhiteSpace(goal))
        {
            return new List<Skill>();
        }

        var allSkills = this.skills.Values.ToList();

        if (this.embedding != null)
        {
            // Use semantic similarity if embedding model available
            var goalEmbedding = await this.embedding.CreateEmbeddingsAsync(goal);

            var skillScores = new List<(Skill skill, double score)>();
            foreach (var skill in allSkills)
            {
                var skillEmbedding = await this.embedding.CreateEmbeddingsAsync(skill.Description);
                var similarity = CosineSimilarity(goalEmbedding, skillEmbedding);
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
            var goalLower = goal.ToLowerInvariant();
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
    /// <returns></returns>
    public Skill? GetSkill(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        this.skills.TryGetValue(name, out var skill);
        return skill;
    }

    /// <summary>
    /// Records skill execution outcome.
    /// </summary>
    public void RecordSkillExecution(string name, bool success)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        this.skills.AddOrUpdate(
            name,
            _ => throw new InvalidOperationException($"Skill '{name}' not found"),
            (_, existing) =>
            {
                var newCount = existing.UsageCount + 1;
                var newSuccessRate = ((existing.SuccessRate * existing.UsageCount) + (success ? 1.0 : 0.0)) / newCount;

                return existing with
                {
                    UsageCount = newCount,
                    SuccessRate = newSuccessRate,
                    LastUsed = DateTime.UtcNow,
                };
            });
    }

    /// <summary>
    /// Gets all registered skills.
    /// </summary>
    /// <returns></returns>
    public IReadOnlyList<Skill> GetAllSkills()
        => this.skills.Values.OrderByDescending(s => s.SuccessRate).ToList();

    /// <summary>
    /// Extracts a skill from successful execution.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public async Task<Result<Skill, string>> ExtractSkillAsync(
        ExecutionResult execution,
        string skillName,
        string description)
    {
        if (execution == null)
        {
            return Result<Skill, string>.Failure("Execution cannot be null");
        }

        if (!execution.Success)
        {
            return Result<Skill, string>.Failure("Cannot extract skill from failed execution");
        }

        if (string.IsNullOrWhiteSpace(skillName))
        {
            return Result<Skill, string>.Failure("Skill name cannot be empty");
        }

        try
        {
            // Extract prerequisites from plan context
            var prerequisites = execution.Plan.Steps
                .Where(s => s.ConfidenceScore > 0.7)
                .Select(s => s.Action)
                .Distinct()
                .ToList();

            // Create skill from successful execution steps
            var skill = new Skill(
                skillName,
                description,
                prerequisites,
                execution.Plan.Steps,
                SuccessRate: 1.0,
                UsageCount: 0,
                CreatedAt: DateTime.UtcNow,
                LastUsed: DateTime.UtcNow);

            this.RegisterSkill(skill);

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
        {
            return 0;
        }

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
        {
            return 0;
        }

        return dotProduct / (Math.Sqrt(magnitudeA) * Math.Sqrt(magnitudeB));
    }
}
