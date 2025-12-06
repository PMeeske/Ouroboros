// <copyright file="RouletteWheelSelection.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Genetic.Core;

using LangChainPipeline.Genetic.Abstractions;

/// <summary>
/// Implements roulette wheel selection (fitness-proportionate selection) for the evolution engine.
/// Chromosomes with higher fitness have higher probability of being selected.
/// </summary>
/// <typeparam name="TChromosome">The type of chromosome to select.</typeparam>
public sealed class EvolutionRouletteWheelSelection<TChromosome>
    where TChromosome : IChromosome
{
    private readonly Random random;

    /// <summary>
    /// Initializes a new instance of the <see cref="EvolutionRouletteWheelSelection{TChromosome}"/> class.
    /// </summary>
    /// <param name="seed">Optional seed for reproducible randomness.</param>
    public EvolutionRouletteWheelSelection(int? seed = null)
    {
        this.random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>
    /// Selects a chromosome from the population using roulette wheel selection.
    /// Uses Result monad for error handling.
    /// </summary>
    /// <param name="population">The population to select from.</param>
    /// <returns>A Result containing the selected chromosome or an error message.</returns>
    public Result<TChromosome> Select(EvolutionPopulation<TChromosome> population)
    {
        if (population.Size == 0)
        {
            return Result<TChromosome>.Failure("Cannot select from empty population");
        }

        // Handle negative fitness by shifting all values to positive
        var minFitness = population.Chromosomes.Min(c => c.Fitness);
        var offset = minFitness < 0 ? Math.Abs(minFitness) + 1 : 0;

        var totalFitness = population.Chromosomes.Sum(c => c.Fitness + offset);

        if (totalFitness <= 0)
        {
            // If all fitness values are zero or negative after offset, select randomly
            var randomIndex = this.random.Next(population.Size);
            return Result<TChromosome>.Success(population.Chromosomes[randomIndex]);
        }

        var spinValue = this.random.NextDouble() * totalFitness;
        var currentSum = 0.0;

        foreach (var chromosome in population.Chromosomes)
        {
            currentSum += chromosome.Fitness + offset;
            if (currentSum >= spinValue)
            {
                return Result<TChromosome>.Success(chromosome);
            }
        }

        // Fallback to last chromosome (should rarely happen due to floating point precision)
        return Result<TChromosome>.Success(population.Chromosomes[^1]);
    }

    /// <summary>
    /// Selects multiple chromosomes from the population.
    /// </summary>
    /// <param name="population">The population to select from.</param>
    /// <param name="count">The number of chromosomes to select.</param>
    /// <returns>A Result containing the list of selected chromosomes or an error message.</returns>
    public Result<List<TChromosome>> SelectMany(EvolutionPopulation<TChromosome> population, int count)
    {
        if (count < 0)
        {
            return Result<List<TChromosome>>.Failure("Count must be non-negative");
        }

        var selected = new List<TChromosome>();

        for (int i = 0; i < count; i++)
        {
            var selectionResult = this.Select(population);
            if (selectionResult.IsFailure)
            {
                return Result<List<TChromosome>>.Failure(selectionResult.Error);
            }

            selected.Add(selectionResult.Value);
        }

        return Result<List<TChromosome>>.Success(selected);
    }
}
