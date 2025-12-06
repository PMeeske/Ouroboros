// <copyright file="UniformCrossoverTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Tests.Genetic;

using FluentAssertions;
using LangChainPipeline.Genetic.Core;
using Xunit;

/// <summary>
/// Tests for the UniformCrossover class.
/// </summary>
public class UniformCrossoverTests
{
    [Fact]
    public void Constructor_WithValidRate_CreatesInstance()
    {
        // Act
        var crossover = new UniformCrossover<int>(0.8);

        // Assert
        crossover.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithInvalidRate_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new UniformCrossover<int>(-0.1));
        Assert.Throws<ArgumentException>(() => new UniformCrossover<int>(1.1));
    }

    [Fact]
    public void Crossover_WithSameLengthParents_ProducesOffspring()
    {
        // Arrange
        var parent1 = new Chromosome<int>(new List<int> { 1, 2, 3, 4, 5 });
        var parent2 = new Chromosome<int>(new List<int> { 6, 7, 8, 9, 10 });
        var crossover = new UniformCrossover<int>(1.0, seed: 42); // 100% crossover rate

        // Act
        var (offspring1, offspring2) = crossover.Crossover(parent1, parent2);

        // Assert
        offspring1.Genes.Should().HaveCount(5);
        offspring2.Genes.Should().HaveCount(5);
        
        // Each gene should come from one of the parents
        for (int i = 0; i < 5; i++)
        {
            (parent1.Genes[i] == offspring1.Genes[i] || parent2.Genes[i] == offspring1.Genes[i]).Should().BeTrue();
            (parent1.Genes[i] == offspring2.Genes[i] || parent2.Genes[i] == offspring2.Genes[i]).Should().BeTrue();
        }
    }

    [Fact]
    public void Crossover_WithDifferentLengthParents_ThrowsArgumentException()
    {
        // Arrange
        var parent1 = new Chromosome<int>(new List<int> { 1, 2, 3 });
        var parent2 = new Chromosome<int>(new List<int> { 4, 5 });
        var crossover = new UniformCrossover<int>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => crossover.Crossover(parent1, parent2));
    }

    [Fact]
    public void Crossover_WithZeroRate_ReturnsParentCopies()
    {
        // Arrange
        var parent1 = new Chromosome<int>(new List<int> { 1, 2, 3 });
        var parent2 = new Chromosome<int>(new List<int> { 4, 5, 6 });
        var crossover = new UniformCrossover<int>(0.0, seed: 42); // 0% crossover rate

        // Act
        var (offspring1, offspring2) = crossover.Crossover(parent1, parent2);

        // Assert
        offspring1.Genes.Should().Equal(parent1.Genes);
        offspring2.Genes.Should().Equal(parent2.Genes);
    }

    [Fact]
    public void Crossover_WithSeed_ProducesReproducibleResults()
    {
        // Arrange
        var parent1 = new Chromosome<int>(new List<int> { 1, 2, 3, 4, 5 });
        var parent2 = new Chromosome<int>(new List<int> { 6, 7, 8, 9, 10 });
        
        var crossover1 = new UniformCrossover<int>(1.0, seed: 42);
        var crossover2 = new UniformCrossover<int>(1.0, seed: 42);

        // Act
        var (offspring1a, offspring2a) = crossover1.Crossover(parent1, parent2);
        var (offspring1b, offspring2b) = crossover2.Crossover(parent1, parent2);

        // Assert
        offspring1a.Genes.Should().Equal(offspring1b.Genes);
        offspring2a.Genes.Should().Equal(offspring2b.Genes);
    }

    [Fact]
    public void Crossover_CreatesGeneticDiversity()
    {
        // Arrange
        var parent1 = new Chromosome<int>(new List<int> { 1, 1, 1, 1, 1 });
        var parent2 = new Chromosome<int>(new List<int> { 2, 2, 2, 2, 2 });
        var crossover = new UniformCrossover<int>(1.0, seed: 42);

        // Act
        var (offspring1, offspring2) = crossover.Crossover(parent1, parent2);

        // Assert - offspring should have a mix of genes
        var hasOnes = offspring1.Genes.Any(g => g == 1);
        var hasTwos = offspring1.Genes.Any(g => g == 2);
        
        hasOnes.Should().BeTrue();
        hasTwos.Should().BeTrue();
    }
}
