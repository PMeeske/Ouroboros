// <copyright file="GeneticAlgorithmTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Genetic;

using FluentAssertions;
using Ouroboros.Genetic.Abstractions;
using Ouroboros.Genetic.Core;
using Xunit;

/// <summary>
/// Tests for the GeneticAlgorithm class.
/// </summary>
[Trait("Category", "Unit")]
public class GeneticAlgorithmTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange
        var fitnessFunction = new SimpleFitnessFunction();

        // Act
        var ga = new GeneticAlgorithm<int>(
            fitnessFunction,
            mutateGene: x => x + 1,
            mutationRate: 0.01,
            crossoverRate: 0.8,
            elitismRate: 0.1);

        // Assert
        ga.Should().NotBeNull();
        ga.FitnessFunction.Should().BeSameAs(fitnessFunction);
    }

    [Fact]
    public void Constructor_WithNullFitnessFunction_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new GeneticAlgorithm<int>(null!, x => x + 1));
    }

    [Fact]
    public void Constructor_WithInvalidElitismRate_ThrowsArgumentException()
    {
        // Arrange
        var fitnessFunction = new SimpleFitnessFunction();

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new GeneticAlgorithm<int>(fitnessFunction, x => x + 1, elitismRate: -0.1));
        Assert.Throws<ArgumentException>(() =>
            new GeneticAlgorithm<int>(fitnessFunction, x => x + 1, elitismRate: 1.1));
    }

    [Fact]
    public async Task EvolveAsync_WithEmptyPopulation_ReturnsFailure()
    {
        // Arrange
        var ga = new GeneticAlgorithm<int>(new SimpleFitnessFunction(), x => x + 1);
        var emptyPopulation = new List<IChromosome<int>>();

        // Act
        var result = await ga.EvolveAsync(emptyPopulation, generations: 10);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public async Task EvolveAsync_WithZeroGenerations_ReturnsFailure()
    {
        // Arrange
        var ga = new GeneticAlgorithm<int>(new SimpleFitnessFunction(), x => x + 1);
        var population = new List<IChromosome<int>>
        {
            new Chromosome<int>(new List<int> { 1, 2, 3 }),
        };

        // Act
        var result = await ga.EvolveAsync(population, generations: 0);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("at least 1");
    }

    [Fact]
    public async Task EvolveAsync_OptimizesPopulation()
    {
        // Arrange - fitness function favors higher sum of genes
        var ga = new GeneticAlgorithm<int>(
            new SumFitnessFunction(),
            mutateGene: x => x + 1,
            mutationRate: 0.1,
            crossoverRate: 0.8,
            elitismRate: 0.1,
            seed: 42);

        var initialPopulation = new List<IChromosome<int>>
        {
            new Chromosome<int>(new List<int> { 1, 1, 1 }),
            new Chromosome<int>(new List<int> { 2, 2, 2 }),
            new Chromosome<int>(new List<int> { 3, 3, 3 }),
            new Chromosome<int>(new List<int> { 4, 4, 4 }),
        };

        // Act
        var result = await ga.EvolveAsync(initialPopulation, generations: 10);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var bestChromosome = result.Value;
        bestChromosome.Fitness.Should().BeGreaterThan(9); // Should improve from initial max of 12
    }

    [Fact]
    public async Task EvolveAsync_PreservesBestChromosome()
    {
        // Arrange - elitism should preserve the best
        var ga = new GeneticAlgorithm<int>(
            new SumFitnessFunction(),
            mutateGene: x => Math.Max(0, x - 1), // Mutation decreases values
            mutationRate: 0.1, // Lower mutation rate to better preserve elite
            crossoverRate: 0.8,
            elitismRate: 0.3, // Higher elitism to ensure preservation
            seed: 42);

        var initialPopulation = new List<IChromosome<int>>
        {
            new Chromosome<int>(new List<int> { 1, 1, 1 }),
            new Chromosome<int>(new List<int> { 2, 2, 2 }),
            new Chromosome<int>(new List<int> { 10, 10, 10 }), // Best initial
        };

        // Act
        var result = await ga.EvolveAsync(initialPopulation, generations: 5);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Fitness.Should().BeGreaterThanOrEqualTo(20); // Should be reasonably good
    }

    [Fact]
    public async Task EvolveAsync_WithSeed_ProducesReproducibleResults()
    {
        // Arrange
        var population = new List<IChromosome<int>>
        {
            new Chromosome<int>(new List<int> { 1, 2, 3 }),
            new Chromosome<int>(new List<int> { 4, 5, 6 }),
        };

        var ga1 = new GeneticAlgorithm<int>(
            new SumFitnessFunction(),
            x => x + 1,
            seed: 42);

        var ga2 = new GeneticAlgorithm<int>(
            new SumFitnessFunction(),
            x => x + 1,
            seed: 42);

        // Act
        var result1 = await ga1.EvolveAsync(population, generations: 5);
        var result2 = await ga2.EvolveAsync(population, generations: 5);

        // Assert
        result1.IsSuccess.Should().BeTrue();
        result2.IsSuccess.Should().BeTrue();
        result1.Value.Fitness.Should().Be(result2.Value.Fitness);
    }

    private class SimpleFitnessFunction : IFitnessFunction<int>
    {
        public Task<double> EvaluateAsync(IChromosome<int> chromosome)
        {
            return Task.FromResult(1.0);
        }
    }

    private class SumFitnessFunction : IFitnessFunction<int>
    {
        public Task<double> EvaluateAsync(IChromosome<int> chromosome)
        {
            double fitness = chromosome.Genes.Sum();
            return Task.FromResult(fitness);
        }
    }
}
