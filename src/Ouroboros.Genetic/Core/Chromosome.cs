// <copyright file="Chromosome.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Genetic.Core;

using LangChainPipeline.Genetic.Abstractions;

/// <summary>
/// Default implementation of a chromosome for genetic algorithms.
/// Immutable structure following functional programming principles.
/// </summary>
/// <typeparam name="TGene">The type of gene in this chromosome.</typeparam>
public sealed record Chromosome<TGene> : IChromosome<TGene>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Chromosome{TGene}"/> class.
    /// </summary>
    /// <param name="genes">The genes that make up this chromosome.</param>
    /// <param name="fitness">The fitness score (default is 0).</param>
    public Chromosome(IReadOnlyList<TGene> genes, double fitness = 0)
    {
        Genes = genes?.ToList().AsReadOnly() ?? throw new ArgumentNullException(nameof(genes));
        Fitness = fitness;
    }

    /// <inheritdoc/>
    public IReadOnlyList<TGene> Genes { get; init; }

    /// <inheritdoc/>
    public double Fitness { get; init; }

    /// <inheritdoc/>
    public IChromosome<TGene> WithGenes(IReadOnlyList<TGene> genes)
        => this with { Genes = genes };

    /// <inheritdoc/>
    public IChromosome<TGene> WithFitness(double fitness)
        => this with { Fitness = fitness };
}
