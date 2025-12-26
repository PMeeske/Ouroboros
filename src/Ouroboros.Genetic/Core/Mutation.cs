// <copyright file="Mutation.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Genetic.Core;

using Ouroboros.Genetic.Abstractions;

/// <summary>
/// Implements mutation for genetic algorithms.
/// Randomly modifies genes to introduce variation in the population.
/// </summary>
/// <typeparam name="TGene">The type of gene in the chromosomes.</typeparam>
public sealed class Mutation<TGene>
{
    private readonly Random random;
    private readonly double mutationRate;
    private readonly Func<TGene, TGene> mutateGene;

    /// <summary>
    /// Initializes a new instance of the <see cref="Mutation{TGene}"/> class.
    /// </summary>
    /// <param name="mutationRate">The probability of mutating each gene (0.0 to 1.0).</param>
    /// <param name="mutateGene">Function that mutates a single gene.</param>
    /// <param name="seed">Optional seed for reproducibility.</param>
    public Mutation(
        double mutationRate,
        Func<TGene, TGene> mutateGene,
        int? seed = null)
    {
        if (mutationRate < 0 || mutationRate > 1)
        {
            throw new ArgumentException("Mutation rate must be between 0 and 1", nameof(mutationRate));
        }

        this.mutationRate = mutationRate;
        this.mutateGene = mutateGene ?? throw new ArgumentNullException(nameof(mutateGene));
        this.random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>
    /// Mutates a chromosome by randomly modifying its genes.
    /// </summary>
    /// <param name="chromosome">The chromosome to mutate.</param>
    /// <returns>A mutated chromosome.</returns>
    public IChromosome<TGene> Mutate(IChromosome<TGene> chromosome)
    {
        var mutatedGenes = new List<TGene>(chromosome.Genes.Count);

        foreach (var gene in chromosome.Genes)
        {
            if (this.random.NextDouble() < this.mutationRate)
            {
                mutatedGenes.Add(this.mutateGene(gene));
            }
            else
            {
                mutatedGenes.Add(gene);
            }
        }

        return chromosome.WithGenes(mutatedGenes);
    }

    /// <summary>
    /// Mutates multiple chromosomes in a population.
    /// </summary>
    /// <param name="population">The population to mutate.</param>
    /// <returns>A new population with mutated chromosomes.</returns>
    public Population<TGene> MutatePopulation(Population<TGene> population)
    {
        var mutatedChromosomes = population.Chromosomes
            .Select(Mutate)
            .ToList();

        return new Population<TGene>(mutatedChromosomes);
    }
}
