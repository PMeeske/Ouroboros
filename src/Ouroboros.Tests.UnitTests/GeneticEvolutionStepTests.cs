// <copyright file="GeneticEvolutionStepTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Genetic.Abstractions;
using Ouroboros.Genetic.Core;
using Ouroboros.Genetic.Steps;
using Xunit;

namespace Ouroboros.Tests.UnitTests;

/// <summary>
/// Tests for the GeneticEvolutionStep class.
/// </summary>
[Trait("Category", "Unit")]
public class GeneticEvolutionStepTests
{
    /// <summary>
    /// Tests that CreateEvolvedStep evolves a population and applies the best configuration.
    /// </summary>
    [Fact]
    public async Task CreateEvolvedStep_ShouldEvolveAndApplyBestConfiguration()
    {
        // Arrange
        var fitnessFunction = new SimpleFitnessFunction();
        var algorithm = new GeneticAlgorithm<double>(
            fitnessFunction,
            mutateGene: gene => gene + (Random.Shared.NextDouble() - 0.5) * 0.1,
            mutationRate: 0.1,
            crossoverRate: 0.8,
            elitismRate: 0.2,
            seed: 42);

        // Step factory: creates a step that multiplies input by the gene value
        Func<double, Step<int, int>> stepFactory = gene => async input =>
        {
            await Task.Yield();
            return (int)(input * gene);
        };

        var evolutionStep = new GeneticEvolutionStep<int, int, double>(algorithm, stepFactory);

        // Create initial population with chromosomes containing multipliers
        var initialPopulation = new List<IChromosome<double>>
        {
            new Chromosome<double>(new[] { 1.0 }),
            new Chromosome<double>(new[] { 2.0 }),
            new Chromosome<double>(new[] { 3.0 }),
            new Chromosome<double>(new[] { 4.0 }),
            new Chromosome<double>(new[] { 5.0 }),
        };

        var evolvedStep = evolutionStep.CreateEvolvedStep(initialPopulation, generations: 5);

        // Act
        var result = await evolvedStep(10);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThan(0); // The evolved step should produce a positive value
    }

    /// <summary>
    /// Tests that CreateEvolvedStepWithMetadata returns both the chromosome and output.
    /// </summary>
    [Fact]
    public async Task CreateEvolvedStepWithMetadata_ShouldReturnChromosomeAndOutput()
    {
        // Arrange
        var fitnessFunction = new SimpleFitnessFunction();
        var algorithm = new GeneticAlgorithm<double>(
            fitnessFunction,
            mutateGene: gene => gene + (Random.Shared.NextDouble() - 0.5) * 0.1,
            mutationRate: 0.1,
            crossoverRate: 0.8,
            elitismRate: 0.2,
            seed: 42);

        Func<double, Step<int, int>> stepFactory = gene => async input =>
        {
            await Task.Yield();
            return (int)(input * gene);
        };

        var evolutionStep = new GeneticEvolutionStep<int, int, double>(algorithm, stepFactory);

        var initialPopulation = new List<IChromosome<double>>
        {
            new Chromosome<double>(new[] { 1.0 }),
            new Chromosome<double>(new[] { 2.0 }),
            new Chromosome<double>(new[] { 3.0 }),
        };

        var evolvedStep = evolutionStep.CreateEvolvedStepWithMetadata(initialPopulation, generations: 3);

        // Act
        var result = await evolvedStep(10);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.BestChromosome.Should().NotBeNull();
        result.Value.BestChromosome.Genes.Should().NotBeEmpty();
        result.Value.Output.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Tests that CreateEvolvedStep fails gracefully with empty population.
    /// </summary>
    [Fact]
    public async Task CreateEvolvedStep_ShouldFailWithEmptyPopulation()
    {
        // Arrange
        var fitnessFunction = new SimpleFitnessFunction();
        var algorithm = new GeneticAlgorithm<double>(
            fitnessFunction,
            mutateGene: gene => gene,
            seed: 42);

        Func<double, Step<int, int>> stepFactory = gene => async input =>
        {
            await Task.Yield();
            return input;
        };

        var evolutionStep = new GeneticEvolutionStep<int, int, double>(algorithm, stepFactory);
        var emptyPopulation = new List<IChromosome<double>>();

        var evolvedStep = evolutionStep.CreateEvolvedStep(emptyPopulation, generations: 5);

        // Act
        var result = await evolvedStep(10);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    /// <summary>
    /// Tests that CreateEvolvedStep handles step execution exceptions.
    /// </summary>
    [Fact]
    public async Task CreateEvolvedStep_ShouldHandleStepExecutionException()
    {
        // Arrange
        var fitnessFunction = new SimpleFitnessFunction();
        var algorithm = new GeneticAlgorithm<double>(
            fitnessFunction,
            mutateGene: gene => gene,
            seed: 42);

        // Step factory that throws an exception
        Func<double, Step<int, int>> stepFactory = gene => async input =>
        {
            await Task.Yield();
            throw new InvalidOperationException("Step execution failed");
        };

        var evolutionStep = new GeneticEvolutionStep<int, int, double>(algorithm, stepFactory);

        var initialPopulation = new List<IChromosome<double>>
        {
            new Chromosome<double>(new[] { 1.0 }),
            new Chromosome<double>(new[] { 2.0 }),
        };

        var evolvedStep = evolutionStep.CreateEvolvedStep(initialPopulation, generations: 1);

        // Act
        var result = await evolvedStep(10);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Optimized step execution failed");
    }

    /// <summary>
    /// Tests constructor throws on null algorithm.
    /// </summary>
    [Fact]
    public void Constructor_ShouldThrowOnNullAlgorithm()
    {
        // Arrange
        Func<double, Step<int, int>> stepFactory = gene => async input =>
        {
            await Task.Yield();
            return input;
        };

        // Act & Assert
        var act = () => new GeneticEvolutionStep<int, int, double>(null!, stepFactory);
        act.Should().Throw<ArgumentNullException>().WithParameterName("algorithm");
    }

    /// <summary>
    /// Tests constructor throws on null step factory.
    /// </summary>
    [Fact]
    public void Constructor_ShouldThrowOnNullStepFactory()
    {
        // Arrange
        var fitnessFunction = new SimpleFitnessFunction();
        var algorithm = new GeneticAlgorithm<double>(
            fitnessFunction,
            mutateGene: gene => gene,
            seed: 42);

        // Act & Assert
        var act = () => new GeneticEvolutionStep<int, int, double>(algorithm, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("stepFactory");
    }

    /// <summary>
    /// A simple fitness function that prefers higher gene values.
    /// </summary>
    private class SimpleFitnessFunction : IFitnessFunction<double>
    {
        public Task<double> EvaluateAsync(IChromosome<double> chromosome)
        {
            // Prefer genes closer to 5.0
            var gene = chromosome.Genes.FirstOrDefault();
            var distance = Math.Abs(gene - 5.0);
            var fitness = 10.0 - distance; // Max fitness at gene = 5.0
            return Task.FromResult(Math.Max(0, fitness));
        }
    }
}
