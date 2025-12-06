// <copyright file="GeneticEvolutionStep.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Genetic.Extensions;

using LangChainPipeline.Genetic.Abstractions;
using LangChainPipeline.Genetic.Core;

/// <summary>
/// Provides genetic evolution as a composable pipeline step.
/// Integrates genetic algorithms into the monadic Step architecture.
/// </summary>
public static class GeneticEvolutionStep
{
    /// <summary>
    /// Creates a genetic evolution step that evolves a population and returns the best result.
    /// This fits into the existing Step{TIn, TOut} architecture.
    /// </summary>
    /// <typeparam name="TChromosome">The type of chromosome being evolved.</typeparam>
    /// <param name="engine">The evolution engine to use.</param>
    /// <param name="generations">The number of generations to evolve.</param>
    /// <returns>A Step that takes a population and returns the best chromosome wrapped in Result.</returns>
    public static Step<Population<TChromosome>, Result<TChromosome>> CreateEvolutionStep<TChromosome>(
        IEvolutionEngine<TChromosome> engine,
        int generations)
        where TChromosome : IChromosome
    {
        return async initialPopulation =>
        {
            if (initialPopulation == null)
            {
                return Result<TChromosome>.Failure("Initial population cannot be null");
            }

            // Evolve the population
            var evolutionResult = await engine.EvolveAsync(initialPopulation, generations);

            if (evolutionResult.IsFailure)
            {
                return Result<TChromosome>.Failure(evolutionResult.Error);
            }

            // Get the best chromosome
            var bestOption = engine.GetBest(evolutionResult.Value);

            if (bestOption.HasValue)
            {
                return Result<TChromosome>.Success(bestOption.Value!);
            }

            return Result<TChromosome>.Failure("No best chromosome found in evolved population");
        };
    }

    /// <summary>
    /// Creates a genetic evolution step that returns the entire evolved population.
    /// </summary>
    /// <typeparam name="TChromosome">The type of chromosome being evolved.</typeparam>
    /// <param name="engine">The evolution engine to use.</param>
    /// <param name="generations">The number of generations to evolve.</param>
    /// <returns>A Step that takes a population and returns the evolved population wrapped in Result.</returns>
    public static Step<Population<TChromosome>, Result<Population<TChromosome>>> CreatePopulationEvolutionStep<TChromosome>(
        IEvolutionEngine<TChromosome> engine,
        int generations)
        where TChromosome : IChromosome
    {
        return async initialPopulation =>
        {
            if (initialPopulation == null)
            {
                return Result<Population<TChromosome>>.Failure("Initial population cannot be null");
            }

            return await engine.EvolveAsync(initialPopulation, generations);
        };
    }

    /// <summary>
    /// Creates a step that converts input to a population, evolves it, and returns the best result.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TChromosome">The type of chromosome being evolved.</typeparam>
    /// <param name="populationFactory">Function to create initial population from input.</param>
    /// <param name="engine">The evolution engine to use.</param>
    /// <param name="generations">The number of generations to evolve.</param>
    /// <returns>A Step that takes input, evolves a population, and returns the best chromosome.</returns>
    public static Step<TIn, Result<TChromosome>> CreateEvolveFromInputStep<TIn, TChromosome>(
        Func<TIn, Result<Population<TChromosome>>> populationFactory,
        IEvolutionEngine<TChromosome> engine,
        int generations)
        where TChromosome : IChromosome
    {
        return async input =>
        {
            // Create initial population
            var populationResult = populationFactory(input);
            if (populationResult.IsFailure)
            {
                return Result<TChromosome>.Failure($"Population creation failed: {populationResult.Error}");
            }

            // Evolve
            var evolutionResult = await engine.EvolveAsync(populationResult.Value, generations);
            if (evolutionResult.IsFailure)
            {
                return Result<TChromosome>.Failure(evolutionResult.Error);
            }

            // Get best
            var bestOption = engine.GetBest(evolutionResult.Value);

            if (bestOption.HasValue)
            {
                return Result<TChromosome>.Success(bestOption.Value!);
            }

            return Result<TChromosome>.Failure("No best chromosome found");
        };
    }
}
