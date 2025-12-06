// <copyright file="IFitnessFunction.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Genetic.Abstractions;

/// <summary>
/// Defines a fitness function for evaluating chromosomes.
/// Follows functional programming principles with monadic error handling.
/// </summary>
/// <typeparam name="TChromosome">The type of chromosome to evaluate.</typeparam>
public interface IFitnessFunction<TChromosome>
    where TChromosome : IChromosome
{
    /// <summary>
    /// Evaluates the fitness of a chromosome.
    /// Higher scores indicate better fitness.
    /// Uses Result monad for error handling without exceptions.
    /// </summary>
    /// <param name="chromosome">The chromosome to evaluate.</param>
    /// <returns>A Result containing the fitness score or an error message.</returns>
    Task<Result<double>> EvaluateAsync(TChromosome chromosome);
}
