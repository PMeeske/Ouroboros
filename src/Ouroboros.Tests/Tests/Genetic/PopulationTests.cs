// <copyright file="PopulationTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Tests.Genetic;

using FluentAssertions;
using LangChainPipeline.Genetic.Abstractions;
using LangChainPipeline.Genetic.Core;
using Xunit;

/// <summary>
/// Tests for the Population class.
/// </summary>
public class PopulationTests
{
    [Fact]
    public void Constructor_WithValidChromosomes_CreatesPopulation()
    {
        // Arrange
        var chromosomes = new List<IChromosome<int>>
        {
            new Chromosome<int>(new List<int> { 1, 2 }, 10),
            new Chromosome<int>(new List<int> { 3, 4 }, 20),
        };

        // Act
        var population = new Population<int>(chromosomes);

        // Assert
        population.Chromosomes.Should().HaveCount(2);
        population.Size.Should().Be(2);
    }

    [Fact]
    public void Constructor_WithEmptyList_ThrowsArgumentException()
    {
        // Arrange
        var emptyList = new List<IChromosome<int>>();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new Population<int>(emptyList));
    }

    [Fact]
    public void Constructor_WithNull_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new Population<int>(null!));
    }

    [Fact]
    public void BestChromosome_ReturnsChromosomeWithHighestFitness()
    {
        // Arrange
        var chromosomes = new List<IChromosome<int>>
        {
            new Chromosome<int>(new List<int> { 1 }, 10),
            new Chromosome<int>(new List<int> { 2 }, 50),
            new Chromosome<int>(new List<int> { 3 }, 30),
        };
        var population = new Population<int>(chromosomes);

        // Act
        var best = population.BestChromosome;

        // Assert
        best.Fitness.Should().Be(50);
        best.Genes.Should().Equal(2);
    }

    [Fact]
    public void AverageFitness_ReturnsCorrectAverage()
    {
        // Arrange
        var chromosomes = new List<IChromosome<int>>
        {
            new Chromosome<int>(new List<int> { 1 }, 10),
            new Chromosome<int>(new List<int> { 2 }, 20),
            new Chromosome<int>(new List<int> { 3 }, 30),
        };
        var population = new Population<int>(chromosomes);

        // Act
        var average = population.AverageFitness;

        // Assert
        average.Should().Be(20);
    }

    [Fact]
    public void WithChromosomes_CreatesNewPopulation()
    {
        // Arrange
        var original = new List<IChromosome<int>>
        {
            new Chromosome<int>(new List<int> { 1 }, 10),
        };
        var population = new Population<int>(original);
        
        var newChromosomes = new List<IChromosome<int>>
        {
            new Chromosome<int>(new List<int> { 2 }, 20),
            new Chromosome<int>(new List<int> { 3 }, 30),
        };

        // Act
        var newPopulation = population.WithChromosomes(newChromosomes);

        // Assert
        newPopulation.Size.Should().Be(2);
        newPopulation.BestChromosome.Fitness.Should().Be(30);
        population.Size.Should().Be(1); // Original unchanged
    }

    [Fact]
    public async Task EvaluateAsync_EvaluatesAllChromosomes()
    {
        // Arrange
        var chromosomes = new List<IChromosome<int>>
        {
            new Chromosome<int>(new List<int> { 1, 2 }),
            new Chromosome<int>(new List<int> { 3, 4 }),
        };
        var population = new Population<int>(chromosomes);
        
        var fitnessFunction = new TestFitnessFunction();

        // Act
        var evaluated = await population.EvaluateAsync(fitnessFunction);

        // Assert
        evaluated.Chromosomes[0].Fitness.Should().Be(3); // 1 + 2
        evaluated.Chromosomes[1].Fitness.Should().Be(7); // 3 + 4
    }

    private class TestFitnessFunction : IFitnessFunction<int>
    {
        public Task<double> EvaluateAsync(IChromosome<int> chromosome)
        {
            // Fitness is the sum of all genes
            double fitness = chromosome.Genes.Sum();
            return Task.FromResult(fitness);
        }
    }
}
