// <copyright file="ChromosomeTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Genetic;

using FluentAssertions;
using Ouroboros.Genetic.Core;
using Xunit;

/// <summary>
/// Tests for the Chromosome implementation.
/// </summary>
[Trait("Category", "Unit")]
public class ChromosomeTests
{
    [Fact]
    public void Constructor_WithValidGenes_CreatesChromosome()
    {
        // Arrange
        var genes = new List<int> { 1, 2, 3, 4, 5 };

        // Act
        var chromosome = new Chromosome<int>(genes);

        // Assert
        chromosome.Genes.Should().Equal(genes);
        chromosome.Fitness.Should().Be(0);
    }

    [Fact]
    public void Constructor_WithFitness_CreatesChromosomeWithFitness()
    {
        // Arrange
        var genes = new List<int> { 1, 2, 3 };
        double fitness = 42.5;

        // Act
        var chromosome = new Chromosome<int>(genes, fitness);

        // Assert
        chromosome.Genes.Should().Equal(genes);
        chromosome.Fitness.Should().Be(fitness);
    }

    [Fact]
    public void Constructor_WithNullGenes_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Chromosome<int>(null!));
    }

    [Fact]
    public void WithGenes_CreatesNewChromosomeWithUpdatedGenes()
    {
        // Arrange
        var originalGenes = new List<int> { 1, 2, 3 };
        var newGenes = new List<int> { 4, 5, 6 };
        var chromosome = new Chromosome<int>(originalGenes, 10);

        // Act
        var updated = chromosome.WithGenes(newGenes);

        // Assert
        updated.Genes.Should().Equal(newGenes);
        updated.Fitness.Should().Be(10); // Fitness should be preserved
        chromosome.Genes.Should().Equal(originalGenes); // Original should be unchanged
    }

    [Fact]
    public void WithFitness_CreatesNewChromosomeWithUpdatedFitness()
    {
        // Arrange
        var genes = new List<int> { 1, 2, 3 };
        var chromosome = new Chromosome<int>(genes, 10);

        // Act
        var updated = chromosome.WithFitness(20);

        // Assert
        updated.Fitness.Should().Be(20);
        updated.Genes.Should().Equal(genes); // Genes should be preserved
        chromosome.Fitness.Should().Be(10); // Original should be unchanged
    }

    [Fact]
    public void Chromosome_IsImmutable()
    {
        // Arrange
        var genes = new List<int> { 1, 2, 3 };
        var chromosome = new Chromosome<int>(genes, 10);

        // Act - modify original list
        genes.Add(4);

        // Assert - chromosome should be unaffected
        chromosome.Genes.Should().HaveCount(3);
        chromosome.Genes.Should().Equal(1, 2, 3);
    }
}
