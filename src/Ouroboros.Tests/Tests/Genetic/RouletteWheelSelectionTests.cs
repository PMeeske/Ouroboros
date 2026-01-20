// <copyright file="RouletteWheelSelectionTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Genetic;

using FluentAssertions;
using Ouroboros.Genetic.Core;
using Xunit;

/// <summary>
/// Tests for the RouletteWheelSelection class.
/// </summary>
[Trait("Category", "Unit")]
public class RouletteWheelSelectionTests
{
    [Fact]
    public void Select_ReturnsSuccessForNonEmptyPopulation()
    {
        // Arrange
        var chromosomes = new[]
        {
            new SimpleChromosome(1.0, fitness: 0.3),
            new SimpleChromosome(2.0, fitness: 0.5),
            new SimpleChromosome(3.0, fitness: 0.2),
        };
        var population = new EvolutionPopulation<SimpleChromosome>(chromosomes);
        var selection = new EvolutionRouletteWheelSelection<SimpleChromosome>(seed: 42);

        // Act
        var result = selection.Select(population);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public void Select_ReturnsFailureForEmptyPopulation()
    {
        // Arrange
        var population = new EvolutionPopulation<SimpleChromosome>(Array.Empty<SimpleChromosome>());
        var selection = new EvolutionRouletteWheelSelection<SimpleChromosome>();

        // Act
        var result = selection.Select(population);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty population");
    }

    [Fact]
    public void Select_WithSeed_ProducesReproducibleResults()
    {
        // Arrange
        var chromosomes = new[]
        {
            new Chromosome<int>(new List<int> { 1 }, 10),
            new Chromosome<int>(new List<int> { 2 }, 20),
            new Chromosome<int>(new List<int> { 3 }, 30),
        };
        var population = new Population<int>(chromosomes);
        
        var selection1 = new RouletteWheelSelection<int>(seed: 42);
        var selection2 = new RouletteWheelSelection<int>(seed: 42);

        // Act
        var selected1 = selection1.Select(population);
        var selected2 = selection2.Select(population);

        // Assert
        selected1.Fitness.Should().Be(selected2.Fitness);
        selected1.Genes.Should().Equal(selected2.Genes);
    }

    [Fact]
    public void Select_FavorsHigherFitness()
    {
        // Arrange
        var chromosomes = new[]
        {
            new SimpleChromosome(1.0, fitness: 0.01),
            new SimpleChromosome(2.0, fitness: 100.0),
            new SimpleChromosome(3.0, fitness: 0.01),
        };
        var population = new EvolutionPopulation<SimpleChromosome>(chromosomes);
        var selection = new EvolutionRouletteWheelSelection<SimpleChromosome>(seed: 42);

        // Act - select multiple times
        var selections = Enumerable.Range(0, 100)
            .Select(_ => selection.Select(population).Value)
            .ToList();

        var highFitnessCount = selections.Count(c => c.Value == 2.0);

        // Assert - high fitness chromosome should be selected significantly more often
        highFitnessCount.Should().BeGreaterThan(50);
    }

    [Fact]
    public void SelectMany_SelectsCorrectCount()
    {
        // Arrange
        var chromosomes = new[]
        {
            new SimpleChromosome(1.0, fitness: 0.3),
            new SimpleChromosome(2.0, fitness: 0.5),
            new SimpleChromosome(3.0, fitness: 0.2),
        };
        var population = new EvolutionPopulation<SimpleChromosome>(chromosomes);
        var selection = new EvolutionRouletteWheelSelection<SimpleChromosome>(seed: 42);

        // Act
        var result = selection.SelectMany(population, 5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(5);
    }

    [Fact]
    public void SelectMany_ReturnsFailureForNegativeCount()
    {
        // Arrange
        var chromosomes = new[] { new SimpleChromosome(1.0, fitness: 0.5) };
        var population = new EvolutionPopulation<SimpleChromosome>(chromosomes);
        var selection = new EvolutionRouletteWheelSelection<SimpleChromosome>();

        // Act
        var result = selection.SelectMany(population, -1);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("non-negative");
    }

    [Fact]
    public void Select_HandlesNegativeFitness()
    {
        // Arrange
        var chromosomes = new[]
        {
            new SimpleChromosome(1.0, fitness: -5.0),
            new SimpleChromosome(2.0, fitness: -3.0),
            new SimpleChromosome(3.0, fitness: -4.0),
        };
        var population = new EvolutionPopulation<SimpleChromosome>(chromosomes);
        var selection = new EvolutionRouletteWheelSelection<SimpleChromosome>(seed: 42);

        // Act
        var result = selection.Select(population);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public void Select_HandlesZeroFitness()
    {
        // Arrange
        var chromosomes = new[]
        {
            new SimpleChromosome(1.0, fitness: 0.0),
            new SimpleChromosome(2.0, fitness: 0.0),
            new SimpleChromosome(3.0, fitness: 0.0),
        };
        var population = new EvolutionPopulation<SimpleChromosome>(chromosomes);
        var selection = new EvolutionRouletteWheelSelection<SimpleChromosome>(seed: 42);

        // Act
        var result = selection.Select(population);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }
}
