// <copyright file="IEvolutionEngine.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Genetic.Abstractions;

using Ouroboros.Genetic.Core;

/// <summary>
/// Defines the main evolution engine for genetic algorithms.
/// Orchestrates the evolution process across generations.
/// </summary>
/// <typeparam name="TChromosome">The type of chromosome being evolved.</typeparam>
public interface IEvolutionEngine<TChromosome>
    where TChromosome : IChromosome
{
    /// <summary>
    /// Evolves a population for the specified number of generations.
    /// Uses Result monad for error handling without exceptions.
    /// </summary>
    /// <param name="initialPopulation">The initial population to evolve.</param>
    /// <param name="generations">The number of generations to evolve.</param>
    /// <returns>A Result containing the final population or an error message.</returns>
    Task<Result<EvolutionPopulation<TChromosome>>> EvolveAsync(
        EvolutionPopulation<TChromosome> initialPopulation,
        int generations);

    /// <summary>
    /// Gets the best chromosome from the current population.
    /// </summary>
    /// <param name="population">The population to search.</param>
    /// <returns>An Option containing the best chromosome, or None if population is empty.</returns>
    Option<TChromosome> GetBest(EvolutionPopulation<TChromosome> population);
}
