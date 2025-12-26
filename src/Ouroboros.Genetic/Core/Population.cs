// <copyright file="Population.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Genetic.Core;

using Ouroboros.Genetic.Abstractions;

/// <summary>
/// Represents a population of chromosomes in a genetic algorithm.
/// Provides methods for population management and evolution.
/// </summary>
/// <typeparam name="TGene">The type of gene in the chromosomes.</typeparam>
public sealed class Population<TGene>
{
    private readonly List<IChromosome<TGene>> chromosomes;

    /// <summary>
    /// Initializes a new instance of the <see cref="Population{TGene}"/> class.
    /// </summary>
    /// <param name="chromosomes">The initial chromosomes in the population.</param>
    public Population(IEnumerable<IChromosome<TGene>> chromosomes)
    {
        this.chromosomes = chromosomes?.ToList() ?? throw new ArgumentNullException(nameof(chromosomes));
        if (this.chromosomes.Count == 0)
        {
            throw new ArgumentException("Population must contain at least one chromosome", nameof(chromosomes));
        }
    }

    /// <summary>
    /// Gets the chromosomes in this population.
    /// </summary>
    public IReadOnlyList<IChromosome<TGene>> Chromosomes => this.chromosomes.AsReadOnly();

    /// <summary>
    /// Gets the size of the population.
    /// </summary>
    public int Size => this.chromosomes.Count;

    /// <summary>
    /// Gets the best chromosome in the population (highest fitness).
    /// </summary>
    public IChromosome<TGene> BestChromosome
        => this.chromosomes.OrderByDescending(c => c.Fitness).First();

    /// <summary>
    /// Gets the average fitness of the population.
    /// </summary>
    public double AverageFitness
        => this.chromosomes.Average(c => c.Fitness);

    /// <summary>
    /// Creates a new population with updated chromosomes.
    /// </summary>
    /// <param name="newChromosomes">The new chromosomes for the population.</param>
    /// <returns>A new population instance.</returns>
    public Population<TGene> WithChromosomes(IEnumerable<IChromosome<TGene>> newChromosomes)
        => new(newChromosomes);

    /// <summary>
    /// Evaluates all chromosomes in the population using the provided fitness function.
    /// </summary>
    /// <param name="fitnessFunction">The fitness function to use.</param>
    /// <returns>A task representing a new population with evaluated chromosomes.</returns>
    public async Task<Population<TGene>> EvaluateAsync(IFitnessFunction<TGene> fitnessFunction)
    {
        var evaluatedChromosomes = new List<IChromosome<TGene>>();
        
        foreach (var chromosome in this.chromosomes)
        {
            double fitness = await fitnessFunction.EvaluateAsync(chromosome);
            evaluatedChromosomes.Add(chromosome.WithFitness(fitness));
        }
        
        return new Population<TGene>(evaluatedChromosomes);
    }
}
