// <copyright file="SimpleChromosomeTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Tests.Genetic;

using FluentAssertions;
using LangChainPipeline.Genetic.Abstractions;
using LangChainPipeline.Genetic.Core;
using Xunit;

/// <summary>
/// Test implementation of IChromosome for unit testing.
/// Represents a simple numeric chromosome.
/// </summary>
internal sealed class SimpleChromosome : IChromosome<double>
{
    public SimpleChromosome(double value, int generation = 0, double fitness = 0.0)
        : this(new[] { value }, generation, fitness)
    {
    }

    public SimpleChromosome(IReadOnlyList<double> genes, int generation = 0, double fitness = 0.0)
    {
        this.Genes = genes;
        this.Generation = generation;
        this.Fitness = fitness;
    }

    public IReadOnlyList<double> Genes { get; }

    public double Value => this.Genes[0];

    public int Generation { get; }

    public double Fitness { get; }

    public IChromosome<double> WithGenes(IReadOnlyList<double> genes)
    {
        return new SimpleChromosome(genes, this.Generation, this.Fitness);
    }

    public IChromosome<double> WithFitness(double fitness)
    {
        return new SimpleChromosome(this.Genes, this.Generation, fitness);
    }

    public SimpleChromosome WithValue(double value)
    {
        return new SimpleChromosome(new[] { value }, this.Generation, this.Fitness);
    }

    public SimpleChromosome WithGeneration(int generation)
    {
        return new SimpleChromosome(this.Genes, generation, this.Fitness);
    }
}

/// <summary>
/// Tests for the SimpleChromosome implementation.
/// </summary>
public class SimpleChromosomeTests
{
    [Fact]
    public void Constructor_CreatesChromosomeWithValues()
    {
        // Arrange & Act
        var chromosome = new SimpleChromosome(42.0, 1, 0.5);

        // Assert
        chromosome.Value.Should().Be(42.0);
        chromosome.Generation.Should().Be(1);
        chromosome.Fitness.Should().Be(0.5);
        chromosome.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void WithGenes_CreatesChromosomeWithNewGenes()
    {
        // Arrange
        var original = new SimpleChromosome(10.0, 2, 0.8);

        // Act
        var updated = (SimpleChromosome)original.WithGenes(new[] { 20.0 });

        // Assert
        updated.Value.Should().Be(20.0);
        updated.Generation.Should().Be(original.Generation);
        updated.Fitness.Should().Be(original.Fitness);
    }

    [Fact]
    public void WithFitness_UpdatesFitnessImmutably()
    {
        // Arrange
        var original = new SimpleChromosome(5.0, 1, 0.3);

        // Act
        var updated = (SimpleChromosome)original.WithFitness(0.9);

        // Assert
        updated.Fitness.Should().Be(0.9);
        updated.Value.Should().Be(original.Value);
        updated.Generation.Should().Be(original.Generation);
        original.Fitness.Should().Be(0.3); // Original unchanged
    }
}
