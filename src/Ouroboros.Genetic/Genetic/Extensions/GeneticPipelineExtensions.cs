// <copyright file="GeneticPipelineExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Genetic.Extensions;

using Ouroboros.Genetic.Abstractions;
using Ouroboros.Genetic.Core;

/// <summary>
/// Extension methods for integrating the evolution engine into pipeline composition.
/// Provides fluent API for evolutionary optimization using IEvolutionEngine.
/// Note: This file contains the Evolution API extensions (EvolutionPipelineExtensions class).
/// For root API genetic extensions, see the root Extensions/GeneticPipelineExtensions.cs file.
/// </summary>
public static class EvolutionPipelineExtensions
{
    /// <summary>
    /// Adds evolutionary optimization to a pipeline step.
    /// Evolves a population and returns the best chromosome.
    /// </summary>
    /// <typeparam name="TIn">The input type to the step.</typeparam>
    /// <typeparam name="TChromosome">The type of chromosome being evolved.</typeparam>
    /// <param name="step">The step that produces a population.</param>
    /// <param name="engine">The evolution engine to use.</param>
    /// <param name="generations">The number of generations to evolve.</param>
    /// <returns>A step that evolves the population and returns the best result.</returns>
    public static Step<TIn, Result<TChromosome>> Evolve<TIn, TChromosome>(
        this Step<TIn, EvolutionPopulation<TChromosome>> step,
        IEvolutionEngine<TChromosome> engine,
        int generations)
        where TChromosome : IChromosome
    {
        var evolutionStep = GeneticEvolutionStep.CreateEvolutionStep(engine, generations);
        return step.Then(evolutionStep);
    }

    /// <summary>
    /// Adds evolutionary optimization that returns the entire evolved population.
    /// </summary>
    /// <typeparam name="TIn">The input type to the step.</typeparam>
    /// <typeparam name="TChromosome">The type of chromosome being evolved.</typeparam>
    /// <param name="step">The step that produces a population.</param>
    /// <param name="engine">The evolution engine to use.</param>
    /// <param name="generations">The number of generations to evolve.</param>
    /// <returns>A step that evolves and returns the entire population.</returns>
    public static Step<TIn, Result<EvolutionPopulation<TChromosome>>> EvolvePopulation<TIn, TChromosome>(
        this Step<TIn, EvolutionPopulation<TChromosome>> step,
        IEvolutionEngine<TChromosome> engine,
        int generations)
        where TChromosome : IChromosome
    {
        var evolutionStep = GeneticEvolutionStep.CreatePopulationEvolutionStep(engine, generations);
        return step.Then(evolutionStep);
    }

    /// <summary>
    /// Adds evolutionary optimization to a pipeline with a custom fitness function.
    /// Creates an evolution engine and evolves the population.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TChromosome">The type of chromosome being evolved.</typeparam>
    /// <param name="step">The step that produces a population.</param>
    /// <param name="fitnessFunction">The fitness function to evaluate chromosomes.</param>
    /// <param name="crossoverFunc">Function to perform crossover.</param>
    /// <param name="mutationFunc">Function to perform mutation.</param>
    /// <param name="generations">The number of generations to evolve.</param>
    /// <param name="crossoverRate">The crossover rate (default 0.8).</param>
    /// <param name="mutationRate">The mutation rate (default 0.1).</param>
    /// <param name="elitismRate">The elitism rate (default 0.1).</param>
    /// <returns>A step that evolves the population and returns the best result.</returns>
    public static Step<TIn, Result<TChromosome>> EvolveWith<TIn, TChromosome>(
        this Step<TIn, EvolutionPopulation<TChromosome>> step,
        IEvolutionFitnessFunction<TChromosome> fitnessFunction,
        Func<TChromosome, TChromosome, double, Result<TChromosome>> crossoverFunc,
        Func<TChromosome, Random, Result<TChromosome>> mutationFunc,
        int generations,
        double crossoverRate = 0.8,
        double mutationRate = 0.1,
        double elitismRate = 0.1)
        where TChromosome : IChromosome
    {
        var engine = new EvolutionEngine<TChromosome>(
            fitnessFunction,
            crossoverFunc,
            mutationFunc,
            crossoverRate,
            mutationRate,
            elitismRate);

        return step.Evolve(engine, generations);
    }

    /// <summary>
    /// Maps the Result from an evolution step to extract the value or provide a default.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TChromosome">The type of chromosome.</typeparam>
    /// <param name="step">The step that produces a Result of chromosome.</param>
    /// <param name="defaultValue">The default value to use if evolution fails.</param>
    /// <returns>A step that extracts the chromosome or returns the default.</returns>
    public static Step<TIn, TChromosome> UnwrapOrDefault<TIn, TChromosome>(
        this Step<TIn, Result<TChromosome>> step,
        TChromosome defaultValue)
        where TChromosome : IChromosome
    {
        return step.Map(result => result.GetValueOrDefault(defaultValue));
    }

    /// <summary>
    /// Maps the Result from an evolution step using a match function.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TChromosome">The type of chromosome.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="step">The step that produces a Result of chromosome.</param>
    /// <param name="onSuccess">Function to apply on successful evolution.</param>
    /// <param name="onFailure">Function to apply on failed evolution.</param>
    /// <returns>A step that matches the result and transforms it.</returns>
    public static Step<TIn, TOut> MatchResult<TIn, TChromosome, TOut>(
        this Step<TIn, Result<TChromosome>> step,
        Func<TChromosome, TOut> onSuccess,
        Func<string, TOut> onFailure)
        where TChromosome : IChromosome
    {
        return step.Map(result => result.Match(onSuccess, onFailure));
    }
}
