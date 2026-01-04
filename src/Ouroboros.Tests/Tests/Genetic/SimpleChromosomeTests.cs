// <copyright file="SimpleChromosomeTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Genetic;

using FluentAssertions;
using Ouroboros.Genetic.Abstractions;
using Ouroboros.Genetic.Core;
using Xunit;

/// <summary>
/// Test implementation of IChromosome for unit testing.
/// Represents a simple numeric chromosome.
/// </summary>
internal sealed class SimpleChromosome : IChromosome
{
    public SimpleChromosome(double value, int generation = 0, double fitness = 0.0)
    {
        this.Id = Guid.NewGuid().ToString();
        this.Value = value;
        this.Generation = generation;
        this.Fitness = fitness;
    }

    private SimpleChromosome(string id, double value, int generation, double fitness)
    {
        this.Id = id;
        this.Value = value;
        this.Generation = generation;
        this.Fitness = fitness;
    }

    public string Id { get; }

    public double Value { get; }

    public int Generation { get; }

    public double Fitness { get; }

    public IChromosome Clone()
    {
        return new SimpleChromosome(this.Id, this.Value, this.Generation, this.Fitness);
    }

    public IChromosome WithFitness(double fitness)
    {
        return new SimpleChromosome(this.Id, this.Value, this.Generation, fitness);
    }

    public SimpleChromosome WithValue(double value)
    {
        return new SimpleChromosome(this.Id, value, this.Generation, this.Fitness);
    }

    public SimpleChromosome WithGeneration(int generation)
    {
        return new SimpleChromosome(this.Id, this.Value, generation, this.Fitness);
    }
}

/// <summary>
/// Tests for the SimpleChromosome implementation.
/// </summary>
[Trait("Category", "Unit")]
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
    public void Clone_CreatesEqualChromosome()
    {
        // Arrange
        var original = new SimpleChromosome(10.0, 2, 0.8);

        // Act
        var clone = (SimpleChromosome)original.Clone();

        // Assert
        clone.Id.Should().Be(original.Id);
        clone.Value.Should().Be(original.Value);
        clone.Generation.Should().Be(original.Generation);
        clone.Fitness.Should().Be(original.Fitness);
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
