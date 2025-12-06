// <copyright file="RouletteWheelSelection.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Genetic.Core;

using LangChainPipeline.Genetic.Abstractions;

/// <summary>
/// Implements roulette wheel selection (fitness-proportionate selection).
/// Chromosomes with higher fitness have a higher probability of being selected.
/// </summary>
/// <typeparam name="TGene">The type of gene in the chromosomes.</typeparam>
public sealed class RouletteWheelSelection<TGene>
{
    private readonly Random random;

    /// <summary>
    /// Initializes a new instance of the <see cref="RouletteWheelSelection{TGene}"/> class.
    /// </summary>
    /// <param name="seed">Optional seed for reproducibility.</param>
    public RouletteWheelSelection(int? seed = null)
    {
        this.random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>
    /// Selects a chromosome from the population using roulette wheel selection.
    /// </summary>
    /// <param name="population">The population to select from.</param>
    /// <returns>The selected chromosome.</returns>
    public IChromosome<TGene> Select(Population<TGene> population)
    {
        if (population.Size == 0)
        {
            throw new ArgumentException("Cannot select from an empty population", nameof(population));
        }

        // Handle negative fitness by shifting all values to be positive
        double minFitness = population.Chromosomes.Min(c => c.Fitness);
        double offset = minFitness < 0 ? Math.Abs(minFitness) + 1 : 0;
        
        double totalFitness = population.Chromosomes.Sum(c => c.Fitness + offset);
        
        // Handle zero total fitness by selecting randomly
        if (totalFitness <= 0)
        {
            int index = this.random.Next(population.Size);
            return population.Chromosomes[index];
        }

        double randomValue = this.random.NextDouble() * totalFitness;
        double cumulativeFitness = 0;

        foreach (var chromosome in population.Chromosomes)
        {
            cumulativeFitness += chromosome.Fitness + offset;
            if (cumulativeFitness >= randomValue)
            {
                return chromosome;
            }
        }

        // Fallback to last chromosome (should rarely happen due to floating point precision)
        return population.Chromosomes[^1];
    }

    /// <summary>
    /// Selects multiple chromosomes from the population.
    /// </summary>
    /// <param name="population">The population to select from.</param>
    /// <param name="count">The number of chromosomes to select.</param>
    /// <returns>A list of selected chromosomes.</returns>
    public IReadOnlyList<IChromosome<TGene>> SelectMany(Population<TGene> population, int count)
    {
        if (count < 0)
        {
            throw new ArgumentException("Count must be non-negative", nameof(count));
        }

        var selected = new List<IChromosome<TGene>>(count);
        for (int i = 0; i < count; i++)
        {
            selected.Add(Select(population));
        }

        return selected;
    }
}
