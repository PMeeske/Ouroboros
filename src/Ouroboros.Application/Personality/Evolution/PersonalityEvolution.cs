// <copyright file="PersonalityEvolution.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Personality;

using Ouroboros.Genetic.Abstractions;

/// <summary>
/// Gene type for personality chromosome - represents a single aspect of personality.
/// </summary>
public sealed record PersonalityGene(string Key, double Value);

/// <summary>
/// Chromosome for evolving personality configurations using gene-based structure.
/// </summary>
public sealed class PersonalityChromosome : IChromosome<PersonalityGene>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PersonalityChromosome"/> class.
    /// </summary>
    public PersonalityChromosome(IReadOnlyList<PersonalityGene> genes, double fitness = 0.0)
    {
        Genes = genes;
        Fitness = fitness;
    }

    /// <inheritdoc/>
    public IReadOnlyList<PersonalityGene> Genes { get; }

    /// <inheritdoc/>
    public double Fitness { get; }

    /// <inheritdoc/>
    public IChromosome<PersonalityGene> WithFitness(double fitness) =>
        new PersonalityChromosome(Genes.ToList(), fitness);

    /// <inheritdoc/>
    public IChromosome<PersonalityGene> WithGenes(IReadOnlyList<PersonalityGene> genes) =>
        new PersonalityChromosome(genes, Fitness);

    /// <summary>Gets trait intensity by name.</summary>
    public double GetTrait(string name) =>
        Genes.FirstOrDefault(g => g.Key == $"trait:{name}")?.Value ?? 0.5;

    /// <summary>Gets curiosity weight by topic.</summary>
    public double GetCuriosity(string topic) =>
        Genes.FirstOrDefault(g => g.Key == $"curiosity:{topic}")?.Value ?? 0.5;

    /// <summary>Gets the proactivity level.</summary>
    public double ProactivityLevel =>
        Genes.FirstOrDefault(g => g.Key == "proactivity")?.Value ?? 0.5;

    /// <summary>Gets the adaptability score.</summary>
    public double Adaptability =>
        Genes.FirstOrDefault(g => g.Key == "adaptability")?.Value ?? 0.5;

    /// <summary>Gets all trait intensities.</summary>
    public Dictionary<string, double> GetTraitIntensities() =>
        Genes.Where(g => g.Key.StartsWith("trait:"))
             .ToDictionary(g => g.Key.Replace("trait:", ""), g => g.Value);

    /// <summary>Gets all curiosity weights.</summary>
    public Dictionary<string, double> GetCuriosityWeights() =>
        Genes.Where(g => g.Key.StartsWith("curiosity:"))
             .ToDictionary(g => g.Key.Replace("curiosity:", ""), g => g.Value);
}

/// <summary>
/// Fitness function for personality evolution based on interaction success.
/// </summary>
public sealed class PersonalityFitness : IFitnessFunction<PersonalityGene>
{
    private readonly List<InteractionFeedback> _recentFeedback;

    /// <summary>
    /// Initializes a new instance of the <see cref="PersonalityFitness"/> class.
    /// </summary>
    public PersonalityFitness(List<InteractionFeedback> recentFeedback)
    {
        _recentFeedback = recentFeedback;
    }

    /// <inheritdoc/>
    public Task<double> EvaluateAsync(IChromosome<PersonalityGene> chromosome)
    {
        if (_recentFeedback.Count == 0)
            return Task.FromResult(0.5);

        PersonalityChromosome pc = (PersonalityChromosome)chromosome;

        double engagementScore = _recentFeedback.Average(f => f.EngagementLevel);
        double relevanceScore = _recentFeedback.Average(f => f.ResponseRelevance);
        double questionScore = _recentFeedback.Average(f => f.QuestionQuality);
        double continuityScore = _recentFeedback.Average(f => f.ConversationContinuity);

        // Weight proactivity more if questions led to good engagement
        double proactivityBonus = pc.ProactivityLevel * questionScore;

        double fitness = (engagementScore * 0.3 +
                relevanceScore * 0.25 +
                questionScore * 0.2 +
                continuityScore * 0.15 +
                proactivityBonus * 0.1);

        return Task.FromResult(fitness);
    }
}
