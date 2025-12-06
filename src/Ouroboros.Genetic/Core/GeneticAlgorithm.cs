// <copyright file="GeneticAlgorithm.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Genetic.Core;

using LangChainPipeline.Genetic.Abstractions;

/// <summary>
/// Standard genetic algorithm implementation using selection, crossover, and mutation.
/// Follows functional programming principles with immutable operations.
/// </summary>
/// <typeparam name="TGene">The type of gene in the chromosomes.</typeparam>
public sealed class GeneticAlgorithm<TGene> : IGeneticAlgorithm<TGene>
{
    private readonly RouletteWheelSelection<TGene> selection;
    private readonly UniformCrossover<TGene> crossover;
    private readonly Mutation<TGene> mutation;
    private readonly double elitismRate;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeneticAlgorithm{TGene}"/> class.
    /// </summary>
    /// <param name="fitnessFunction">The fitness function to evaluate chromosomes.</param>
    /// <param name="mutateGene">Function to mutate a single gene.</param>
    /// <param name="mutationRate">The probability of mutating each gene (default 0.01).</param>
    /// <param name="crossoverRate">The probability of performing crossover (default 0.8).</param>
    /// <param name="elitismRate">The proportion of top chromosomes to preserve (default 0.1).</param>
    /// <param name="seed">Optional seed for reproducibility.</param>
    public GeneticAlgorithm(
        IFitnessFunction<TGene> fitnessFunction,
        Func<TGene, TGene> mutateGene,
        double mutationRate = 0.01,
        double crossoverRate = 0.8,
        double elitismRate = 0.1,
        int? seed = null)
    {
        FitnessFunction = fitnessFunction ?? throw new ArgumentNullException(nameof(fitnessFunction));
        
        if (elitismRate < 0 || elitismRate > 1)
        {
            throw new ArgumentException("Elitism rate must be between 0 and 1", nameof(elitismRate));
        }

        this.elitismRate = elitismRate;
        this.selection = new RouletteWheelSelection<TGene>(seed);
        this.crossover = new UniformCrossover<TGene>(crossoverRate, seed);
        this.mutation = new Mutation<TGene>(mutationRate, mutateGene, seed);
    }

    /// <inheritdoc/>
    public IFitnessFunction<TGene> FitnessFunction { get; }

    /// <inheritdoc/>
    public async Task<Result<IChromosome<TGene>, string>> EvolveAsync(
        IReadOnlyList<IChromosome<TGene>> initialPopulation,
        int generations)
    {
        try
        {
            if (initialPopulation == null || initialPopulation.Count == 0)
            {
                return Result<IChromosome<TGene>, string>.Failure("Initial population cannot be empty");
            }

            if (generations < 1)
            {
                return Result<IChromosome<TGene>, string>.Failure("Number of generations must be at least 1");
            }

            var population = new Population<TGene>(initialPopulation);
            
            // Evaluate initial population
            population = await population.EvaluateAsync(FitnessFunction);

            for (int generation = 0; generation < generations; generation++)
            {
                population = await EvolveGenerationAsync(population);
            }

            return Result<IChromosome<TGene>, string>.Success(population.BestChromosome);
        }
        catch (Exception ex)
        {
            return Result<IChromosome<TGene>, string>.Failure($"Evolution failed: {ex.Message}");
        }
    }

    private async Task<Population<TGene>> EvolveGenerationAsync(Population<TGene> population)
    {
        var newChromosomes = new List<IChromosome<TGene>>();

        // Elitism: preserve the best chromosomes
        int eliteCount = (int)(population.Size * this.elitismRate);
        var elites = population.Chromosomes
            .OrderByDescending(c => c.Fitness)
            .Take(eliteCount)
            .ToList();
        newChromosomes.AddRange(elites);

        // Generate offspring to fill the rest of the population
        int offspringCount = population.Size - eliteCount;
        for (int i = 0; i < offspringCount / 2; i++)
        {
            // Selection
            var parent1 = this.selection.Select(population);
            var parent2 = this.selection.Select(population);

            // Crossover
            var (offspring1, offspring2) = this.crossover.Crossover(parent1, parent2);

            // Mutation
            offspring1 = this.mutation.Mutate(offspring1);
            offspring2 = this.mutation.Mutate(offspring2);

            newChromosomes.Add(offspring1);
            if (newChromosomes.Count < population.Size)
            {
                newChromosomes.Add(offspring2);
            }
        }

        // Handle odd population size
        if (newChromosomes.Count < population.Size)
        {
            var parent = this.selection.Select(population);
            var offspring = this.mutation.Mutate(parent);
            newChromosomes.Add(offspring);
        }

        var newPopulation = new Population<TGene>(newChromosomes);
        return await newPopulation.EvaluateAsync(FitnessFunction);
    }
}
