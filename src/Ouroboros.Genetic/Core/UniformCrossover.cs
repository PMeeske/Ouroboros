// <copyright file="UniformCrossover.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Genetic.Core;

using LangChainPipeline.Genetic.Abstractions;

/// <summary>
/// Implements uniform crossover for genetic algorithms.
/// Each gene is randomly selected from one of the two parents.
/// </summary>
/// <typeparam name="TGene">The type of gene in the chromosomes.</typeparam>
public sealed class UniformCrossover<TGene>
{
    private readonly Random random;
    private readonly double crossoverRate;

    /// <summary>
    /// Initializes a new instance of the <see cref="UniformCrossover{TGene}"/> class.
    /// </summary>
    /// <param name="crossoverRate">The probability of performing crossover (0.0 to 1.0).</param>
    /// <param name="seed">Optional seed for reproducibility.</param>
    public UniformCrossover(double crossoverRate = 0.8, int? seed = null)
    {
        if (crossoverRate < 0 || crossoverRate > 1)
        {
            throw new ArgumentException("Crossover rate must be between 0 and 1", nameof(crossoverRate));
        }

        this.crossoverRate = crossoverRate;
        this.random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>
    /// Performs crossover between two parent chromosomes.
    /// </summary>
    /// <param name="parent1">The first parent chromosome.</param>
    /// <param name="parent2">The second parent chromosome.</param>
    /// <returns>Two offspring chromosomes.</returns>
    public (IChromosome<TGene> offspring1, IChromosome<TGene> offspring2) Crossover(
        IChromosome<TGene> parent1,
        IChromosome<TGene> parent2)
    {
        if (parent1.Genes.Count != parent2.Genes.Count)
        {
            throw new ArgumentException("Parents must have the same number of genes");
        }

        // Decide whether to perform crossover
        if (this.random.NextDouble() > this.crossoverRate)
        {
            // Return copies of parents without crossover
            return (parent1, parent2);
        }

        int geneCount = parent1.Genes.Count;
        var genes1 = new List<TGene>(geneCount);
        var genes2 = new List<TGene>(geneCount);

        // Uniform crossover: randomly select each gene from either parent
        for (int i = 0; i < geneCount; i++)
        {
            if (this.random.NextDouble() < 0.5)
            {
                genes1.Add(parent1.Genes[i]);
                genes2.Add(parent2.Genes[i]);
            }
            else
            {
                genes1.Add(parent2.Genes[i]);
                genes2.Add(parent1.Genes[i]);
            }
        }

        var offspring1 = parent1.WithGenes(genes1);
        var offspring2 = parent2.WithGenes(genes2);

        return (offspring1, offspring2);
    }
}
