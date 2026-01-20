// <copyright file="PopulationTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Genetic;

using FluentAssertions;
using Ouroboros.Genetic.Core;
using Xunit;

/// <summary>
/// Tests for the Population class.
/// </summary>
[Trait("Category", "Unit")]
public class PopulationTests
{
    [Fact]
    public void Constructor_CreatesPopulationWithChromosomes()
    {
        // Arrange
        var chromosomes = new[]
        {
            new SimpleChromosome(1.0),
            new SimpleChromosome(2.0),
            new SimpleChromosome(3.0),
        };

        // Act
        var population = new EvolutionPopulation<SimpleChromosome>(chromosomes);

        // Assert
        population.Size.Should().Be(3);
        population.Chromosomes.Should().HaveCount(3);
    }

    [Fact]
    public void Constructor_ThrowsOnNullChromosomes()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new EvolutionPopulation<SimpleChromosome>(null!));
    }

    [Fact]
    public void GetBest_ReturnsChromosomeWithHighestFitness()
    {
        // Arrange
        var chromosomes = new[]
        {
            new SimpleChromosome(1.0, fitness: 0.3),
            new SimpleChromosome(2.0, fitness: 0.9),
            new SimpleChromosome(3.0, fitness: 0.5),
        };
        var population = new EvolutionPopulation<SimpleChromosome>(chromosomes);

        // Act
        var best = population.GetBest();

        // Assert
        best.HasValue.Should().BeTrue();
        best.Value!.Value.Should().Be(2.0);
        best.Value.Fitness.Should().Be(0.9);
    }

    [Fact]
    public void GetBest_ReturnsNoneForEmptyPopulation()
    {
        // Arrange
        var population = new EvolutionPopulation<SimpleChromosome>(Array.Empty<SimpleChromosome>());

        // Act
        var best = population.GetBest();

        // Assert
        best.HasValue.Should().BeFalse();
    }

    [Fact]
    public void GetAverageFitness_CalculatesCorrectAverage()
    {
        // Arrange
        var chromosomes = new[]
        {
            new SimpleChromosome(1.0, fitness: 0.2),
            new SimpleChromosome(2.0, fitness: 0.4),
            new SimpleChromosome(3.0, fitness: 0.6),
        };
        var population = new EvolutionPopulation<SimpleChromosome>(chromosomes);

        // Act
        var average = population.GetAverageFitness();

        // Assert
        average.Should().BeApproximately(0.4, 0.0001);
    }

    [Fact]
    public void GetAverageFitness_ReturnsZeroForEmptyPopulation()
    {
        // Arrange
        var population = new EvolutionPopulation<SimpleChromosome>(Array.Empty<SimpleChromosome>());

        // Act
        var average = population.GetAverageFitness();

        // Assert
        average.Should().Be(0.0);
    }

    [Fact]
    public void Add_AddsChromosomeImmutably()
    {
        // Arrange
        var initial = new[] { new SimpleChromosome(1.0) };
        var population = new EvolutionPopulation<SimpleChromosome>(initial);
        var newChromosome = new SimpleChromosome(2.0);

        // Act
        var newPopulation = population.Add(newChromosome);

        // Assert
        newPopulation.Size.Should().Be(2);
        population.Size.Should().Be(1); // Original unchanged
        newPopulation.Chromosomes.Should().Contain(c => c.Value == 2.0);
    }

    [Fact]
    public void SortByFitness_SortsDescending()
    {
        // Arrange
        var chromosomes = new[]
        {
            new SimpleChromosome(1.0, fitness: 0.3),
            new SimpleChromosome(2.0, fitness: 0.9),
            new SimpleChromosome(3.0, fitness: 0.5),
        };
        var population = new EvolutionPopulation<SimpleChromosome>(chromosomes);

        // Act
        var sorted = population.SortByFitness();

        // Assert
        sorted.Chromosomes[0].Fitness.Should().Be(0.9);
        sorted.Chromosomes[1].Fitness.Should().Be(0.5);
        sorted.Chromosomes[2].Fitness.Should().Be(0.3);
    }

    [Fact]
    public void Take_TakesTopNChromosomes()
    {
        // Arrange
        var chromosomes = new[]
        {
            new SimpleChromosome(1.0, fitness: 0.3),
            new SimpleChromosome(2.0, fitness: 0.9),
            new SimpleChromosome(3.0, fitness: 0.5),
        };
        var population = new EvolutionPopulation<SimpleChromosome>(chromosomes);

        // Act
        var top2 = population.Take(2);

        // Assert
        top2.Size.Should().Be(2);
        top2.Chromosomes[0].Fitness.Should().Be(0.9);
        top2.Chromosomes[1].Fitness.Should().Be(0.5);
    }

    [Fact]
    public void Generation_ReturnsMaxGenerationOfChromosomes()
    {
        // Arrange
        var chromosomes = new[]
        {
            new SimpleChromosome(1.0, generation: 1),
            new SimpleChromosome(2.0, generation: 3),
            new SimpleChromosome(3.0, generation: 2),
        };
        var population = new EvolutionPopulation<SimpleChromosome>(chromosomes);

        // Act
        var generation = population.Generation;

        // Assert
        generation.Should().Be(3);
    }
}
