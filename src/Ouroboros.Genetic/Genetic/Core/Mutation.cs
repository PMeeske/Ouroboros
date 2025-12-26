// <copyright file="Mutation.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Genetic.Core;

using Ouroboros.Genetic.Abstractions;

/// <summary>
/// Implements mutation operations for genetic algorithms.
/// Mutation introduces random variations to maintain genetic diversity.
/// </summary>
public sealed class EvolutionMutation
{
    private readonly Random random;
    private readonly double mutationRate;

    /// <summary>
    /// Initializes a new instance of the <see cref="EvolutionMutation"/> class.
    /// </summary>
    /// <param name="mutationRate">The probability of mutation occurring (0.0 to 1.0).</param>
    /// <param name="seed">Optional seed for reproducible randomness.</param>
    public EvolutionMutation(double mutationRate = 0.1, int? seed = null)
    {
        if (mutationRate < 0.0 || mutationRate > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(mutationRate), "Mutation rate must be between 0.0 and 1.0");
        }

        this.mutationRate = mutationRate;
        this.random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    /// <summary>
    /// Applies mutation to a chromosome.
    /// </summary>
    /// <typeparam name="TChromosome">The type of chromosome.</typeparam>
    /// <param name="chromosome">The chromosome to mutate.</param>
    /// <param name="mutationFunc">Function that performs the actual mutation.</param>
    /// <returns>A Result containing the mutated chromosome or an error message.</returns>
    public Result<TChromosome> Mutate<TChromosome>(
        TChromosome chromosome,
        Func<TChromosome, Random, Result<TChromosome>> mutationFunc)
        where TChromosome : IChromosome
    {
        if (chromosome == null)
        {
            return Result<TChromosome>.Failure("Chromosome cannot be null");
        }

        // Check if mutation should occur
        if (this.random.NextDouble() > this.mutationRate)
        {
            // No mutation, return clone of original
            return Result<TChromosome>.Success((TChromosome)chromosome.Clone());
        }

        // Perform mutation
        return mutationFunc(chromosome, this.random);
    }

    /// <summary>
    /// Applies mutation to multiple chromosomes in a population.
    /// </summary>
    /// <typeparam name="TChromosome">The type of chromosome.</typeparam>
    /// <param name="population">The population to mutate.</param>
    /// <param name="mutationFunc">Function that performs the actual mutation.</param>
    /// <returns>A Result containing the mutated population or an error message.</returns>
    public async Task<Result<EvolutionPopulation<TChromosome>>> MutatePopulationAsync<TChromosome>(
        EvolutionPopulation<TChromosome> population,
        Func<TChromosome, Random, Result<TChromosome>> mutationFunc)
        where TChromosome : IChromosome
    {
        if (population == null)
        {
            return Result<EvolutionPopulation<TChromosome>>.Failure("Population cannot be null");
        }

        var mutatedChromosomes = new List<TChromosome>();

        foreach (var chromosome in population.Chromosomes)
        {
            var mutationResult = this.Mutate(chromosome, mutationFunc);
            if (mutationResult.IsFailure)
            {
                return Result<EvolutionPopulation<TChromosome>>.Failure($"Mutation failed: {mutationResult.Error}");
            }

            mutatedChromosomes.Add(mutationResult.Value);
        }

        var newPopulation = new EvolutionPopulation<TChromosome>(mutatedChromosomes);
        return Result<EvolutionPopulation<TChromosome>>.Success(newPopulation);
    }

    /// <summary>
    /// Gets the current mutation rate.
    /// </summary>
    public double MutationRate => this.mutationRate;
}
