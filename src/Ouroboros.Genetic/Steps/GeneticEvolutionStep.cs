// <copyright file="GeneticEvolutionStep.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Genetic.Steps;

using LangChainPipeline.Genetic.Abstractions;
using LangChainPipeline.Genetic.Core;

/// <summary>
/// A pipeline step that uses genetic algorithms to evolve and optimize parameters or configurations.
/// This enables evolutionary optimization of monadic pipeline operations.
/// </summary>
/// <typeparam name="TIn">The input type for the pipeline step.</typeparam>
/// <typeparam name="TOut">The output type for the pipeline step.</typeparam>
/// <typeparam name="TGene">The type of gene encoding the parameters to optimize.</typeparam>
public sealed class GeneticEvolutionStep<TIn, TOut, TGene>
{
    private readonly IGeneticAlgorithm<TGene> algorithm;
    private readonly Func<TGene, Step<TIn, TOut>> stepFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeneticEvolutionStep{TIn, TOut, TGene}"/> class.
    /// </summary>
    /// <param name="algorithm">The genetic algorithm to use for evolution.</param>
    /// <param name="stepFactory">Function that creates a pipeline step from a gene configuration.</param>
    public GeneticEvolutionStep(
        IGeneticAlgorithm<TGene> algorithm,
        Func<TGene, Step<TIn, TOut>> stepFactory)
    {
        this.algorithm = algorithm ?? throw new ArgumentNullException(nameof(algorithm));
        this.stepFactory = stepFactory ?? throw new ArgumentNullException(nameof(stepFactory));
    }

    /// <summary>
    /// Creates a step that evolves the best configuration and applies it to the input.
    /// </summary>
    /// <param name="initialPopulation">The initial population of gene configurations.</param>
    /// <param name="generations">The number of generations to evolve.</param>
    /// <returns>An evolved step that applies the best configuration.</returns>
    public Step<TIn, Result<TOut, string>> CreateEvolvedStep(
        IReadOnlyList<IChromosome<TGene>> initialPopulation,
        int generations)
    {
        return async input =>
        {
            // Evolve to find the best configuration
            var evolutionResult = await this.algorithm.EvolveAsync(initialPopulation, generations);
            
            if (evolutionResult.IsFailure)
            {
                return Result<TOut, string>.Failure(evolutionResult.Error);
            }

            // Extract the best gene configuration
            var bestChromosome = evolutionResult.Value;
            var bestGene = bestChromosome.Genes.FirstOrDefault();
            
            if (bestGene == null)
            {
                return Result<TOut, string>.Failure("Best chromosome has no genes");
            }

            // Create and execute the step with the best configuration
            var optimizedStep = this.stepFactory(bestGene);
            
            try
            {
                var output = await optimizedStep(input);
                return Result<TOut, string>.Success(output);
            }
            catch (Exception ex)
            {
                return Result<TOut, string>.Failure($"Optimized step execution failed: {ex.Message}");
            }
        };
    }

    /// <summary>
    /// Creates a step that evolves and returns both the best chromosome and the output.
    /// </summary>
    /// <param name="initialPopulation">The initial population of gene configurations.</param>
    /// <param name="generations">The number of generations to evolve.</param>
    /// <returns>A step that returns the best chromosome and the output.</returns>
    public Step<TIn, Result<(IChromosome<TGene> BestChromosome, TOut Output), string>> CreateEvolvedStepWithMetadata(
        IReadOnlyList<IChromosome<TGene>> initialPopulation,
        int generations)
    {
        return async input =>
        {
            // Evolve to find the best configuration
            var evolutionResult = await this.algorithm.EvolveAsync(initialPopulation, generations);
            
            if (evolutionResult.IsFailure)
            {
                return Result<(IChromosome<TGene>, TOut), string>.Failure(evolutionResult.Error);
            }

            // Extract the best gene configuration
            var bestChromosome = evolutionResult.Value;
            var bestGene = bestChromosome.Genes.FirstOrDefault();
            
            if (bestGene == null)
            {
                return Result<(IChromosome<TGene>, TOut), string>.Failure("Best chromosome has no genes");
            }

            // Create and execute the step with the best configuration
            var optimizedStep = this.stepFactory(bestGene);
            
            try
            {
                var output = await optimizedStep(input);
                return Result<(IChromosome<TGene>, TOut), string>.Success((bestChromosome, output));
            }
            catch (Exception ex)
            {
                return Result<(IChromosome<TGene>, TOut), string>.Failure($"Optimized step execution failed: {ex.Message}");
            }
        };
    }
}
