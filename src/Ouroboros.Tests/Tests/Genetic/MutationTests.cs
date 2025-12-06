// <copyright file="MutationTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Tests.Genetic;

using FluentAssertions;
using LangChainPipeline.Genetic.Core;
using Xunit;

/// <summary>
/// Tests for the Mutation class.
/// </summary>
public class MutationTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var mutation = new Mutation<int>(0.1, x => x + 1);

        // Assert
        mutation.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithInvalidRate_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => new Mutation<int>(-0.1, x => x + 1));
        Assert.Throws<ArgumentException>(() => new Mutation<int>(1.1, x => x + 1));
    }

    [Fact]
    public void Constructor_WithNullMutateGene_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Mutation<int>(0.1, null!));
    }

    [Fact]
    public void Mutate_WithZeroRate_ReturnsUnchangedChromosome()
    {
        // Arrange
        var chromosome = new Chromosome<int>(new List<int> { 1, 2, 3, 4, 5 });
        var mutation = new Mutation<int>(0.0, x => x + 100, seed: 42);

        // Act
        var mutated = mutation.Mutate(chromosome);

        // Assert
        mutated.Genes.Should().Equal(chromosome.Genes);
    }

    [Fact]
    public void Mutate_WithHighRate_MutatesGenes()
    {
        // Arrange
        var chromosome = new Chromosome<int>(new List<int> { 1, 2, 3, 4, 5 });
        var mutation = new Mutation<int>(1.0, x => x + 10, seed: 42); // 100% mutation rate

        // Act
        var mutated = mutation.Mutate(chromosome);

        // Assert
        mutated.Genes.Should().Equal(11, 12, 13, 14, 15);
    }

    [Fact]
    public void Mutate_WithSeed_ProducesReproducibleResults()
    {
        // Arrange
        var chromosome = new Chromosome<int>(new List<int> { 1, 2, 3, 4, 5 });
        var mutation1 = new Mutation<int>(0.5, x => x * 2, seed: 42);
        var mutation2 = new Mutation<int>(0.5, x => x * 2, seed: 42);

        // Act
        var mutated1 = mutation1.Mutate(chromosome);
        var mutated2 = mutation2.Mutate(chromosome);

        // Assert
        mutated1.Genes.Should().Equal(mutated2.Genes);
    }

    [Fact]
    public void Mutate_PreservesChromosomeStructure()
    {
        // Arrange
        var chromosome = new Chromosome<int>(new List<int> { 1, 2, 3 });
        var mutation = new Mutation<int>(0.5, x => x + 1);

        // Act
        var mutated = mutation.Mutate(chromosome);

        // Assert
        mutated.Genes.Should().HaveCount(chromosome.Genes.Count);
        mutated.Should().NotBeSameAs(chromosome); // Should be a new instance
    }

    [Fact]
    public void MutatePopulation_MutatesAllChromosomes()
    {
        // Arrange
        var chromosomes = new List<Chromosome<int>>
        {
            new Chromosome<int>(new List<int> { 1, 2, 3 }),
            new Chromosome<int>(new List<int> { 4, 5, 6 }),
        };
        var population = new Population<int>(chromosomes);
        var mutation = new Mutation<int>(1.0, x => x + 10, seed: 42); // 100% mutation

        // Act
        var mutatedPopulation = mutation.MutatePopulation(population);

        // Assert
        mutatedPopulation.Size.Should().Be(2);
        mutatedPopulation.Chromosomes[0].Genes.Should().Equal(11, 12, 13);
        mutatedPopulation.Chromosomes[1].Genes.Should().Equal(14, 15, 16);
    }

    [Fact]
    public void MutatePopulation_CreatesNewPopulation()
    {
        // Arrange
        var chromosomes = new List<Chromosome<int>>
        {
            new Chromosome<int>(new List<int> { 1, 2, 3 }),
        };
        var population = new Population<int>(chromosomes);
        var mutation = new Mutation<int>(0.5, x => x + 1);

        // Act
        var mutatedPopulation = mutation.MutatePopulation(population);

        // Assert
        mutatedPopulation.Should().NotBeSameAs(population);
        population.Chromosomes[0].Genes.Should().Equal(1, 2, 3); // Original unchanged
    }
}
