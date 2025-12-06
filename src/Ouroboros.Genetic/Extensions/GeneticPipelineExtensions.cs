// <copyright file="GeneticPipelineExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Genetic.Extensions;

using LangChainPipeline.Genetic.Abstractions;
using LangChainPipeline.Genetic.Core;
using LangChainPipeline.Genetic.Steps;

/// <summary>
/// Extension methods for integrating genetic algorithms into monadic pipelines.
/// Provides fluent API for evolutionary optimization of pipeline steps.
/// </summary>
public static class GeneticPipelineExtensions
{
    /// <summary>
    /// Evolves a pipeline step using a genetic algorithm to optimize its configuration.
    /// </summary>
    /// <typeparam name="TIn">The input type for the step.</typeparam>
    /// <typeparam name="TOut">The output type for the step.</typeparam>
    /// <typeparam name="TGene">The type of gene encoding the parameters.</typeparam>
    /// <param name="step">The step to evolve (typically identity or a base step).</param>
    /// <param name="stepFactory">Function that creates a step from a gene configuration.</param>
    /// <param name="fitnessFunction">Function to evaluate the fitness of a configuration.</param>
    /// <param name="mutateGene">Function to mutate a single gene.</param>
    /// <param name="initialPopulation">The initial population of configurations.</param>
    /// <param name="generations">The number of generations to evolve.</param>
    /// <param name="mutationRate">The probability of mutating each gene (default 0.01).</param>
    /// <param name="crossoverRate">The probability of performing crossover (default 0.8).</param>
    /// <param name="elitismRate">The proportion of top configurations to preserve (default 0.1).</param>
    /// <param name="seed">Optional seed for reproducibility.</param>
    /// <returns>A step that applies the evolved best configuration.</returns>
    public static Step<TIn, Result<TOut, string>> Evolve<TIn, TOut, TGene>(
        this Step<TIn, TIn> step,
        Func<TGene, Step<TIn, TOut>> stepFactory,
        IFitnessFunction<TGene> fitnessFunction,
        Func<TGene, TGene> mutateGene,
        IReadOnlyList<IChromosome<TGene>> initialPopulation,
        int generations,
        double mutationRate = 0.01,
        double crossoverRate = 0.8,
        double elitismRate = 0.1,
        int? seed = null)
    {
        var algorithm = new GeneticAlgorithm<TGene>(
            fitnessFunction,
            mutateGene,
            mutationRate,
            crossoverRate,
            elitismRate,
            seed);

        var evolutionStep = new GeneticEvolutionStep<TIn, TOut, TGene>(algorithm, stepFactory);
        
        return async input =>
        {
            // First apply the base step (often identity)
            var baseOutput = await step(input);
            
            // Then apply the evolved step
            var evolvedStep = evolutionStep.CreateEvolvedStep(initialPopulation, generations);
            return await evolvedStep(baseOutput);
        };
    }

    /// <summary>
    /// Evolves a pipeline step and returns both the best configuration and the output.
    /// </summary>
    /// <typeparam name="TIn">The input type for the step.</typeparam>
    /// <typeparam name="TOut">The output type for the step.</typeparam>
    /// <typeparam name="TGene">The type of gene encoding the parameters.</typeparam>
    /// <param name="step">The step to evolve (typically identity or a base step).</param>
    /// <param name="stepFactory">Function that creates a step from a gene configuration.</param>
    /// <param name="fitnessFunction">Function to evaluate the fitness of a configuration.</param>
    /// <param name="mutateGene">Function to mutate a single gene.</param>
    /// <param name="initialPopulation">The initial population of configurations.</param>
    /// <param name="generations">The number of generations to evolve.</param>
    /// <param name="mutationRate">The probability of mutating each gene (default 0.01).</param>
    /// <param name="crossoverRate">The probability of performing crossover (default 0.8).</param>
    /// <param name="elitismRate">The proportion of top configurations to preserve (default 0.1).</param>
    /// <param name="seed">Optional seed for reproducibility.</param>
    /// <returns>A step that returns the best chromosome and the output.</returns>
    public static Step<TIn, Result<(IChromosome<TGene> BestChromosome, TOut Output), string>> EvolveWithMetadata<TIn, TOut, TGene>(
        this Step<TIn, TIn> step,
        Func<TGene, Step<TIn, TOut>> stepFactory,
        IFitnessFunction<TGene> fitnessFunction,
        Func<TGene, TGene> mutateGene,
        IReadOnlyList<IChromosome<TGene>> initialPopulation,
        int generations,
        double mutationRate = 0.01,
        double crossoverRate = 0.8,
        double elitismRate = 0.1,
        int? seed = null)
    {
        var algorithm = new GeneticAlgorithm<TGene>(
            fitnessFunction,
            mutateGene,
            mutationRate,
            crossoverRate,
            elitismRate,
            seed);

        var evolutionStep = new GeneticEvolutionStep<TIn, TOut, TGene>(algorithm, stepFactory);
        
        return async input =>
        {
            // First apply the base step (often identity)
            var baseOutput = await step(input);
            
            // Then apply the evolved step with metadata
            var evolvedStep = evolutionStep.CreateEvolvedStepWithMetadata(initialPopulation, generations);
            return await evolvedStep(baseOutput);
        };
    }

    /// <summary>
    /// Creates an identity step that can be used as a starting point for evolution.
    /// </summary>
    /// <typeparam name="T">The input/output type.</typeparam>
    /// <returns>An identity step.</returns>
    public static Step<T, T> Identity<T>()
    {
        return input => Task.FromResult(input);
    }
}
