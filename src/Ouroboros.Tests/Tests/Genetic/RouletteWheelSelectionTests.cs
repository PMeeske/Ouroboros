// <copyright file="RouletteWheelSelectionTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Tests.Genetic;

using FluentAssertions;
using LangChainPipeline.Genetic.Abstractions;
using LangChainPipeline.Genetic.Core;
using Xunit;

/// <summary>
/// Tests for the RouletteWheelSelection class.
/// </summary>
public class RouletteWheelSelectionTests
{
    [Fact]
    public void Select_FromPopulation_ReturnsChromosome()
    {
        // Arrange
        var chromosomes = new List<IChromosome<int>>
        {
            new Chromosome<int>(new List<int> { 1 }, 10),
            new Chromosome<int>(new List<int> { 2 }, 20),
            new Chromosome<int>(new List<int> { 3 }, 30),
        };
        var population = new Population<int>(chromosomes);
        var selection = new RouletteWheelSelection<int>(seed: 42);

        // Act
        var selected = selection.Select(population);

        // Assert
        selected.Should().NotBeNull();
        population.Chromosomes.Should().Contain(selected);
    }

    [Fact]
    public void Select_WithSeed_ProducesReproducibleResults()
    {
        // Arrange
        var chromosomes = new List<IChromosome<int>>
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
        // Arrange - one chromosome with much higher fitness
        var chromosomes = new List<IChromosome<int>>
        {
            new Chromosome<int>(new List<int> { 1 }, 1),
            new Chromosome<int>(new List<int> { 2 }, 1),
            new Chromosome<int>(new List<int> { 3 }, 98), // Much higher fitness
        };
        var population = new Population<int>(chromosomes);
        var selection = new RouletteWheelSelection<int>(seed: 42);

        // Act - select many times
        var selections = new List<IChromosome<int>>();
        for (int i = 0; i < 100; i++)
        {
            selections.Add(selection.Select(population));
        }

        // Assert - high fitness chromosome should be selected more often
        var highFitnessCount = selections.Count(c => c.Fitness == 98);
        highFitnessCount.Should().BeGreaterThan(50); // Should be selected more than half the time
    }

    [Fact]
    public void Select_WithNegativeFitness_HandlesCorrectly()
    {
        // Arrange
        var chromosomes = new List<IChromosome<int>>
        {
            new Chromosome<int>(new List<int> { 1 }, -10),
            new Chromosome<int>(new List<int> { 2 }, -5),
            new Chromosome<int>(new List<int> { 3 }, 0),
        };
        var population = new Population<int>(chromosomes);
        var selection = new RouletteWheelSelection<int>(seed: 42);

        // Act
        var selected = selection.Select(population);

        // Assert
        selected.Should().NotBeNull();
        population.Chromosomes.Should().Contain(selected);
    }

    [Fact]
    public void SelectMany_ReturnsCorrectCount()
    {
        // Arrange
        var chromosomes = new List<IChromosome<int>>
        {
            new Chromosome<int>(new List<int> { 1 }, 10),
            new Chromosome<int>(new List<int> { 2 }, 20),
        };
        var population = new Population<int>(chromosomes);
        var selection = new RouletteWheelSelection<int>();

        // Act
        var selected = selection.SelectMany(population, 5);

        // Assert
        selected.Should().HaveCount(5);
    }

    [Fact]
    public void SelectMany_WithZeroCount_ReturnsEmptyList()
    {
        // Arrange
        var chromosomes = new List<IChromosome<int>>
        {
            new Chromosome<int>(new List<int> { 1 }, 10),
        };
        var population = new Population<int>(chromosomes);
        var selection = new RouletteWheelSelection<int>();

        // Act
        var selected = selection.SelectMany(population, 0);

        // Assert
        selected.Should().BeEmpty();
    }

    [Fact]
    public void SelectMany_WithNegativeCount_ThrowsArgumentException()
    {
        // Arrange
        var chromosomes = new List<IChromosome<int>>
        {
            new Chromosome<int>(new List<int> { 1 }, 10),
        };
        var population = new Population<int>(chromosomes);
        var selection = new RouletteWheelSelection<int>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => selection.SelectMany(population, -1));
    }
}
