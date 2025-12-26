// <copyright file="IFitnessFunction.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Genetic.Abstractions;

/// <summary>
/// Evaluates the fitness of a chromosome for the genetic algorithm.
/// Fitness functions can evaluate based on test results, user satisfaction, or other metrics.
/// </summary>
/// <typeparam name="TGene">The type of gene in the chromosomes being evaluated.</typeparam>
public interface IFitnessFunction<TGene>
{
    /// <summary>
    /// Evaluates the fitness of a chromosome asynchronously.
    /// </summary>
    /// <param name="chromosome">The chromosome to evaluate.</param>
    /// <returns>A task representing the fitness score (higher is better).</returns>
    Task<double> EvaluateAsync(IChromosome<TGene> chromosome);
}
