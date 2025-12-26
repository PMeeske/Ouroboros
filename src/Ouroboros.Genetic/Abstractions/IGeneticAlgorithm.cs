// <copyright file="IGeneticAlgorithm.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Genetic.Abstractions;

/// <summary>
/// Represents a genetic algorithm for evolutionary optimization.
/// </summary>
/// <typeparam name="TGene">The type of gene in the chromosomes.</typeparam>
public interface IGeneticAlgorithm<TGene>
{
    /// <summary>
    /// Evolves a population over multiple generations.
    /// </summary>
    /// <param name="initialPopulation">The initial population of chromosomes.</param>
    /// <param name="generations">The number of generations to evolve.</param>
    /// <returns>A task representing the best chromosome found.</returns>
    Task<Result<IChromosome<TGene>, string>> EvolveAsync(
        IReadOnlyList<IChromosome<TGene>> initialPopulation,
        int generations);

    /// <summary>
    /// Gets the fitness function used by this algorithm.
    /// </summary>
    IFitnessFunction<TGene> FitnessFunction { get; }
}
