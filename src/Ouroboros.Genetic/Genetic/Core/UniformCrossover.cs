// <copyright file="UniformCrossover.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Genetic.Core;

using LangChainPipeline.Genetic.Abstractions;

/// <summary>
/// Implements uniform crossover for genetic algorithms.
/// Each gene has an equal probability of coming from either parent.
/// This is a generic implementation that works with any chromosome type implementing ICrossoverable.
/// </summary>
public sealed class EvolutionCrossover
{
    private readonly Random random;
    private readonly double crossoverRate;

    /// <summary>
    /// Initializes a new instance of the <see cref="EvolutionCrossover"/> class.
    /// </summary>
    /// <param name="crossoverRate">The probability of crossover occurring (0.0 to 1.0).</param>
    /// <param name="seed">Optional seed for reproducible randomness.</param>
    public EvolutionCrossover(double crossoverRate = 0.8, int? seed = null)
    {
        if (crossoverRate < 0.0 || crossoverRate > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(crossoverRate), "Crossover rate must be between 0.0 and 1.0");
        }

        this.crossoverRate = crossoverRate;
        this.random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>
    /// Performs uniform crossover between two parents.
    /// </summary>
    /// <typeparam name="TChromosome">The type of chromosome.</typeparam>
    /// <param name="parent1">The first parent.</param>
    /// <param name="parent2">The second parent.</param>
    /// <param name="crossoverFunc">Function that performs the actual crossover given a mixing ratio.</param>
    /// <returns>A Result containing the offspring or an error message.</returns>
    public Result<TChromosome> Crossover<TChromosome>(
        TChromosome parent1,
        TChromosome parent2,
        Func<TChromosome, TChromosome, double, Result<TChromosome>> crossoverFunc)
        where TChromosome : IChromosome
    {
        if (parent1 == null || parent2 == null)
        {
            return Result<TChromosome>.Failure("Parents cannot be null");
        }

        // Check if crossover should occur
        if (this.random.NextDouble() > this.crossoverRate)
        {
            // No crossover, return clone of first parent
            return Result<TChromosome>.Success((TChromosome)parent1.Clone());
        }

        // Perform crossover with uniform mixing ratio (0.5 = equal contribution from both parents)
        return crossoverFunc(parent1, parent2, 0.5);
    }

    /// <summary>
    /// Performs crossover between two parents to produce two offspring.
    /// </summary>
    /// <typeparam name="TChromosome">The type of chromosome.</typeparam>
    /// <param name="parent1">The first parent.</param>
    /// <param name="parent2">The second parent.</param>
    /// <param name="crossoverFunc">Function that performs the actual crossover.</param>
    /// <returns>A Result containing both offspring or an error message.</returns>
    public Result<(TChromosome Offspring1, TChromosome Offspring2)> CrossoverPair<TChromosome>(
        TChromosome parent1,
        TChromosome parent2,
        Func<TChromosome, TChromosome, double, Result<TChromosome>> crossoverFunc)
        where TChromosome : IChromosome
    {
        var offspring1Result = this.Crossover(parent1, parent2, crossoverFunc);
        if (offspring1Result.IsFailure)
        {
            return Result<(TChromosome, TChromosome)>.Failure(offspring1Result.Error);
        }

        var offspring2Result = this.Crossover(parent2, parent1, crossoverFunc);
        if (offspring2Result.IsFailure)
        {
            return Result<(TChromosome, TChromosome)>.Failure(offspring2Result.Error);
        }

        return Result<(TChromosome, TChromosome)>.Success((offspring1Result.Value, offspring2Result.Value));
    }
}
