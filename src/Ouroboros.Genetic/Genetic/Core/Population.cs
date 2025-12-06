// <copyright file="Population.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Genetic.Core;

using LangChainPipeline.Genetic.Abstractions;

/// <summary>
/// Represents a population of chromosomes in the evolution engine.
/// Implements immutable collection pattern following functional programming principles.
/// </summary>
/// <typeparam name="TChromosome">The type of chromosome in the population.</typeparam>
public sealed class EvolutionPopulation<TChromosome>
    where TChromosome : IChromosome
{
    private readonly IReadOnlyList<TChromosome> chromosomes;

    /// <summary>
    /// Initializes a new instance of the <see cref="EvolutionPopulation{TChromosome}"/> class.
    /// </summary>
    /// <param name="chromosomes">The collection of chromosomes.</param>
    public EvolutionPopulation(IEnumerable<TChromosome> chromosomes)
    {
        this.chromosomes = chromosomes?.ToList() ?? throw new ArgumentNullException(nameof(chromosomes));
    }

    /// <summary>
    /// Gets the number of chromosomes in the population.
    /// </summary>
    public int Size => this.chromosomes.Count;

    /// <summary>
    /// Gets the generation number (the maximum generation of all chromosomes).
    /// </summary>
    public int Generation => this.chromosomes.Any() ? this.chromosomes.Max(c => c.Generation) : 0;

    /// <summary>
    /// Gets all chromosomes in the population.
    /// </summary>
    public IReadOnlyList<TChromosome> Chromosomes => this.chromosomes;

    /// <summary>
    /// Gets the best chromosome in the population (highest fitness).
    /// </summary>
    /// <returns>An Option containing the best chromosome, or None if population is empty.</returns>
    public Option<TChromosome> GetBest()
    {
        if (!this.chromosomes.Any())
        {
            return Option<TChromosome>.None();
        }

        return Option<TChromosome>.Some(this.chromosomes.MaxBy(c => c.Fitness)!);
    }

    /// <summary>
    /// Gets the average fitness of the population.
    /// </summary>
    /// <returns>The average fitness score.</returns>
    public double GetAverageFitness()
    {
        return this.chromosomes.Any() ? this.chromosomes.Average(c => c.Fitness) : 0.0;
    }

    /// <summary>
    /// Creates a new population with the specified chromosomes (immutable update).
    /// </summary>
    /// <param name="newChromosomes">The new collection of chromosomes.</param>
    /// <returns>A new EvolutionPopulation instance.</returns>
    public EvolutionPopulation<TChromosome> WithChromosomes(IEnumerable<TChromosome> newChromosomes)
    {
        return new EvolutionPopulation<TChromosome>(newChromosomes);
    }

    /// <summary>
    /// Adds a chromosome to the population (immutable operation).
    /// </summary>
    /// <param name="chromosome">The chromosome to add.</param>
    /// <returns>A new EvolutionPopulation with the added chromosome.</returns>
    public EvolutionPopulation<TChromosome> Add(TChromosome chromosome)
    {
        var newList = this.chromosomes.Append(chromosome);
        return new EvolutionPopulation<TChromosome>(newList);
    }

    /// <summary>
    /// Sorts the population by fitness in descending order.
    /// </summary>
    /// <returns>A new sorted EvolutionPopulation.</returns>
    public EvolutionPopulation<TChromosome> SortByFitness()
    {
        var sorted = this.chromosomes.OrderByDescending(c => c.Fitness);
        return new EvolutionPopulation<TChromosome>(sorted);
    }

    /// <summary>
    /// Takes the top N chromosomes by fitness.
    /// </summary>
    /// <param name="count">The number of chromosomes to take.</param>
    /// <returns>A new EvolutionPopulation with the top chromosomes.</returns>
    public EvolutionPopulation<TChromosome> Take(int count)
    {
        var topChromosomes = this.chromosomes
            .OrderByDescending(c => c.Fitness)
            .Take(count);
        return new EvolutionPopulation<TChromosome>(topChromosomes);
    }
}
