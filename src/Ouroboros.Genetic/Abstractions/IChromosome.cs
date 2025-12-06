// <copyright file="IChromosome.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Genetic.Abstractions;

/// <summary>
/// Represents a chromosome in a genetic algorithm, encoding a potential solution.
/// Chromosomes can represent pipeline configurations, parameters, or prompt variations.
/// </summary>
/// <typeparam name="TGene">The type of gene in this chromosome.</typeparam>
public interface IChromosome<TGene>
{
    /// <summary>
    /// Gets the genes that make up this chromosome.
    /// </summary>
    IReadOnlyList<TGene> Genes { get; }

    /// <summary>
    /// Gets the fitness score of this chromosome.
    /// Higher values indicate better fitness.
    /// </summary>
    double Fitness { get; }

    /// <summary>
    /// Creates a new chromosome with the specified genes.
    /// </summary>
    /// <param name="genes">The genes for the new chromosome.</param>
    /// <returns>A new chromosome instance.</returns>
    IChromosome<TGene> WithGenes(IReadOnlyList<TGene> genes);

    /// <summary>
    /// Creates a new chromosome with the specified fitness score.
    /// </summary>
    /// <param name="fitness">The fitness score.</param>
    /// <returns>A new chromosome instance.</returns>
    IChromosome<TGene> WithFitness(double fitness);
}
