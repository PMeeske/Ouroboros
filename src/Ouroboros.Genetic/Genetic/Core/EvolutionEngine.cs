// <copyright file="EvolutionEngine.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Genetic.Core;

using LangChainPipeline.Genetic.Abstractions;

/// <summary>
/// Implements the main evolution engine for genetic algorithms.
/// Orchestrates selection, crossover, mutation, and fitness evaluation.
/// Follows functional programming principles with monadic error handling.
/// </summary>
/// <typeparam name="TChromosome">The type of chromosome being evolved.</typeparam>
public sealed class EvolutionEngine<TChromosome> : IEvolutionEngine<TChromosome>
    where TChromosome : IChromosome
{
    private readonly IEvolutionFitnessFunction<TChromosome> fitnessFunction;
    private readonly EvolutionRouletteWheelSelection<TChromosome> selection;
    private readonly EvolutionCrossover crossover;
    private readonly EvolutionMutation mutation;
    private readonly Func<TChromosome, TChromosome, double, Result<TChromosome>> crossoverFunc;
    private readonly Func<TChromosome, Random, Result<TChromosome>> mutationFunc;
    private readonly double elitismRate;

    /// <summary>
    /// Initializes a new instance of the <see cref="EvolutionEngine{TChromosome}"/> class.
    /// </summary>
    /// <param name="fitnessFunction">The fitness function to evaluate chromosomes.</param>
    /// <param name="crossoverFunc">Function to perform crossover between two chromosomes.</param>
    /// <param name="mutationFunc">Function to perform mutation on a chromosome.</param>
    /// <param name="crossoverRate">The probability of crossover occurring (0.0 to 1.0).</param>
    /// <param name="mutationRate">The probability of mutation occurring (0.0 to 1.0).</param>
    /// <param name="elitismRate">The proportion of best chromosomes to preserve (0.0 to 1.0).</param>
    /// <param name="seed">Optional seed for reproducible randomness.</param>
    public EvolutionEngine(
        IEvolutionFitnessFunction<TChromosome> fitnessFunction,
        Func<TChromosome, TChromosome, double, Result<TChromosome>> crossoverFunc,
        Func<TChromosome, Random, Result<TChromosome>> mutationFunc,
        double crossoverRate = 0.8,
        double mutationRate = 0.1,
        double elitismRate = 0.1,
        int? seed = null)
    {
        if (elitismRate < 0.0 || elitismRate > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(elitismRate), "Elitism rate must be between 0.0 and 1.0");
        }

        this.fitnessFunction = fitnessFunction ?? throw new ArgumentNullException(nameof(fitnessFunction));
        this.crossoverFunc = crossoverFunc ?? throw new ArgumentNullException(nameof(crossoverFunc));
        this.mutationFunc = mutationFunc ?? throw new ArgumentNullException(nameof(mutationFunc));
        this.selection = new EvolutionRouletteWheelSelection<TChromosome>(seed);
        this.crossover = new EvolutionCrossover(crossoverRate, seed);
        this.mutation = new EvolutionMutation(mutationRate, seed);
        this.elitismRate = elitismRate;
    }

    /// <inheritdoc/>
    public async Task<Result<EvolutionPopulation<TChromosome>>> EvolveAsync(
        EvolutionPopulation<TChromosome> initialPopulation,
        int generations)
    {
        if (initialPopulation == null)
        {
            return Result<EvolutionPopulation<TChromosome>>.Failure("Initial population cannot be null");
        }

        if (generations < 0)
        {
            return Result<EvolutionPopulation<TChromosome>>.Failure("Generations must be non-negative");
        }

        if (initialPopulation.Size == 0)
        {
            return Result<EvolutionPopulation<TChromosome>>.Failure("Initial population cannot be empty");
        }

        var currentPopulation = initialPopulation;

        // Evaluate initial population fitness
        var evaluatedPopulation = await this.EvaluatePopulationAsync(currentPopulation);
        if (evaluatedPopulation.IsFailure)
        {
            return Result<EvolutionPopulation<TChromosome>>.Failure($"Initial fitness evaluation failed: {evaluatedPopulation.Error}");
        }

        currentPopulation = evaluatedPopulation.Value;

        // Evolve for specified generations
        for (int generation = 0; generation < generations; generation++)
        {
            var nextGenResult = await this.EvolveGenerationAsync(currentPopulation, generation + 1);
            if (nextGenResult.IsFailure)
            {
                return Result<EvolutionPopulation<TChromosome>>.Failure($"Evolution failed at generation {generation + 1}: {nextGenResult.Error}");
            }

            currentPopulation = nextGenResult.Value;
        }

        return Result<EvolutionPopulation<TChromosome>>.Success(currentPopulation);
    }

    /// <inheritdoc/>
    public Option<TChromosome> GetBest(EvolutionPopulation<TChromosome> population)
    {
        return population?.GetBest() ?? Option<TChromosome>.None();
    }

    /// <summary>
    /// Evolves a single generation.
    /// </summary>
    private async Task<Result<EvolutionPopulation<TChromosome>>> EvolveGenerationAsync(
        EvolutionPopulation<TChromosome> currentPopulation,
        int generationNumber)
    {
        var eliteCount = (int)(currentPopulation.Size * this.elitismRate);
        var newPopulationSize = currentPopulation.Size - eliteCount;

        // Preserve elite chromosomes
        var sortedPopulation = currentPopulation.SortByFitness();
        var elites = sortedPopulation.Take(eliteCount).Chromosomes.ToList();

        // Generate new offspring
        var offspring = new List<TChromosome>();

        while (offspring.Count < newPopulationSize)
        {
            // Select parents
            var parent1Result = this.selection.Select(currentPopulation);
            if (parent1Result.IsFailure)
            {
                return Result<EvolutionPopulation<TChromosome>>.Failure($"Parent selection failed: {parent1Result.Error}");
            }

            var parent2Result = this.selection.Select(currentPopulation);
            if (parent2Result.IsFailure)
            {
                return Result<EvolutionPopulation<TChromosome>>.Failure($"Parent selection failed: {parent2Result.Error}");
            }

            // Crossover
            var crossoverResult = this.crossover.Crossover(parent1Result.Value, parent2Result.Value, this.crossoverFunc);
            if (crossoverResult.IsFailure)
            {
                return Result<EvolutionPopulation<TChromosome>>.Failure($"Crossover failed: {crossoverResult.Error}");
            }

            // Mutation
            var mutationResult = this.mutation.Mutate(crossoverResult.Value, this.mutationFunc);
            if (mutationResult.IsFailure)
            {
                return Result<EvolutionPopulation<TChromosome>>.Failure($"Mutation failed: {mutationResult.Error}");
            }

            offspring.Add(mutationResult.Value);
        }

        // Combine elites and offspring
        var newChromosomes = elites.Concat(offspring);
        var newPopulation = new EvolutionPopulation<TChromosome>(newChromosomes);

        // Evaluate fitness of new population
        return await this.EvaluatePopulationAsync(newPopulation);
    }

    /// <summary>
    /// Evaluates the fitness of all chromosomes in a population.
    /// </summary>
    private async Task<Result<EvolutionPopulation<TChromosome>>> EvaluatePopulationAsync(
        EvolutionPopulation<TChromosome> population)
    {
        var evaluatedChromosomes = new List<TChromosome>();

        foreach (var chromosome in population.Chromosomes)
        {
            var fitnessResult = await this.fitnessFunction.EvaluateAsync(chromosome);
            if (fitnessResult.IsFailure)
            {
                return Result<EvolutionPopulation<TChromosome>>.Failure($"Fitness evaluation failed for chromosome {chromosome.Id}: {fitnessResult.Error}");
            }

            var updatedChromosome = (TChromosome)chromosome.WithFitness(fitnessResult.Value);
            evaluatedChromosomes.Add(updatedChromosome);
        }

        return Result<EvolutionPopulation<TChromosome>>.Success(new EvolutionPopulation<TChromosome>(evaluatedChromosomes));
    }
}
