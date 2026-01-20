// <copyright file="GeneticPipelineIntegrationTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Genetic;

using FluentAssertions;
using Ouroboros.Genetic.Abstractions;
using Ouroboros.Genetic.Core;
using Ouroboros.Genetic.Extensions;
using Xunit;

/// <summary>
/// Integration tests for genetic algorithm pipeline extensions.
/// </summary>
[Trait("Category", "Integration")]
public class GeneticPipelineIntegrationTests
{
    [Fact]
    public async Task Evolve_IntegratesWithPipeline()
    {
        // Arrange
        var targetValue = 75.0;
        var fitnessFunction = new TargetValueFitnessFunction(targetValue);

        Func<SimpleChromosome, SimpleChromosome, double, Result<SimpleChromosome>> crossoverFunc =
            (p1, p2, ratio) =>
            {
                var newValue = p1.Value * ratio + p2.Value * (1 - ratio);
                return Result<SimpleChromosome>.Success(new SimpleChromosome(newValue));
            };

        Func<SimpleChromosome, Random, Result<SimpleChromosome>> mutationFunc =
            (c, random) =>
            {
                var delta = (random.NextDouble() - 0.5) * 8.0;
                return Result<SimpleChromosome>.Success(new SimpleChromosome(c.Value + delta));
            };

        var engine = new EvolutionEngine<SimpleChromosome>(
            fitnessFunction,
            crossoverFunc,
            mutationFunc,
            seed: 42);

        // Create a pipeline step that produces a population
        Step<int, EvolutionPopulation<SimpleChromosome>> createPopulation = size =>
        {
            var chromosomes = Enumerable.Range(0, size)
                .Select(i => new SimpleChromosome(i * 3.0))
                .ToList();
            return Task.FromResult(new EvolutionPopulation<SimpleChromosome>(chromosomes));
        };

        // Act - Use the Evolve extension method
        var pipeline = createPopulation.Evolve(engine, generations: 50);
        var result = await pipeline(30);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Value.Should().BeInRange(targetValue - 20, targetValue + 20);
    }

    [Fact]
    public async Task EvolveWith_CreatesAndUsesEngine()
    {
        // Arrange
        var targetValue = 60.0;
        var fitnessFunction = new TargetValueFitnessFunction(targetValue);

        Func<SimpleChromosome, SimpleChromosome, double, Result<SimpleChromosome>> crossoverFunc =
            (p1, p2, ratio) =>
            {
                var newValue = p1.Value * ratio + p2.Value * (1 - ratio);
                return Result<SimpleChromosome>.Success(new SimpleChromosome(newValue));
            };

        Func<SimpleChromosome, Random, Result<SimpleChromosome>> mutationFunc =
            (c, random) =>
            {
                var delta = (random.NextDouble() - 0.5) * 5.0;
                return Result<SimpleChromosome>.Success(new SimpleChromosome(c.Value + delta));
            };

        Step<int, EvolutionPopulation<SimpleChromosome>> createPopulation = size =>
        {
            var chromosomes = Enumerable.Range(0, size)
                .Select(i => new SimpleChromosome(i * 2.5))
                .ToList();
            return Task.FromResult(new EvolutionPopulation<SimpleChromosome>(chromosomes));
        };

        // Act - Use the EvolveWith extension method
        var pipeline = createPopulation.EvolveWith(
            fitnessFunction,
            crossoverFunc,
            mutationFunc,
            generations: 40,
            crossoverRate: 0.85,
            mutationRate: 0.15);

        var result = await pipeline(25);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task EvolvePopulation_ReturnsFullPopulation()
    {
        // Arrange
        var fitnessFunction = new TargetValueFitnessFunction(50.0);

        Func<SimpleChromosome, SimpleChromosome, double, Result<SimpleChromosome>> crossoverFunc =
            (p1, p2, ratio) =>
            {
                var newValue = p1.Value * ratio + p2.Value * (1 - ratio);
                return Result<SimpleChromosome>.Success(new SimpleChromosome(newValue));
            };

        Func<SimpleChromosome, Random, Result<SimpleChromosome>> mutationFunc =
            (c, random) =>
            {
                var delta = (random.NextDouble() - 0.5) * 10.0;
                return Result<SimpleChromosome>.Success(new SimpleChromosome(c.Value + delta));
            };

        var engine = new EvolutionEngine<SimpleChromosome>(
            fitnessFunction,
            crossoverFunc,
            mutationFunc,
            seed: 42);

        Step<int, EvolutionPopulation<SimpleChromosome>> createPopulation = size =>
        {
            var chromosomes = Enumerable.Range(0, size)
                .Select(i => new SimpleChromosome(i * 4.0))
                .ToList();
            return Task.FromResult(new EvolutionPopulation<SimpleChromosome>(chromosomes));
        };

        // Act - Use EvolvePopulation to get the entire population
        var pipeline = createPopulation.EvolvePopulation(engine, generations: 30);
        var result = await pipeline(20);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Size.Should().Be(20);
        result.Value.GetAverageFitness().Should().BeGreaterThan(0.0);
    }

    [Fact]
    public async Task UnwrapOrDefault_ExtractsValueFromResult()
    {
        // Arrange
        var fitnessFunction = new TargetValueFitnessFunction(40.0);

        Func<SimpleChromosome, SimpleChromosome, double, Result<SimpleChromosome>> crossoverFunc =
            (p1, p2, ratio) => Result<SimpleChromosome>.Success(new SimpleChromosome(15.0));

        Func<SimpleChromosome, Random, Result<SimpleChromosome>> mutationFunc =
            (c, random) => Result<SimpleChromosome>.Success(new SimpleChromosome(c.Value + 1));

        var engine = new EvolutionEngine<SimpleChromosome>(
            fitnessFunction,
            crossoverFunc,
            mutationFunc,
            seed: 42);

        Step<int, EvolutionPopulation<SimpleChromosome>> createPopulation = size =>
        {
            var chromosomes = new[] { new SimpleChromosome(10.0) };
            return Task.FromResult(new EvolutionPopulation<SimpleChromosome>(chromosomes));
        };

        var defaultChromosome = new SimpleChromosome(-999.0);

        // Act
        var pipeline = createPopulation
            .Evolve(engine, generations: 10)
            .UnwrapOrDefault(defaultChromosome);

        var result = await pipeline(1);

        // Assert
        result.Should().NotBeNull();
        result.Value.Should().NotBe(-999.0); // Should not be default
    }

    [Fact]
    public async Task MatchResult_TransformsResultCorrectly()
    {
        // Arrange
        var fitnessFunction = new TargetValueFitnessFunction(30.0);

        Func<SimpleChromosome, SimpleChromosome, double, Result<SimpleChromosome>> crossoverFunc =
            (p1, p2, ratio) => Result<SimpleChromosome>.Success(new SimpleChromosome(15.0));

        Func<SimpleChromosome, Random, Result<SimpleChromosome>> mutationFunc =
            (c, random) => Result<SimpleChromosome>.Success(new SimpleChromosome(c.Value + 1));

        var engine = new EvolutionEngine<SimpleChromosome>(
            fitnessFunction,
            crossoverFunc,
            mutationFunc,
            seed: 42);

        Step<int, EvolutionPopulation<SimpleChromosome>> createPopulation = size =>
        {
            var chromosomes = new[] { new SimpleChromosome(25.0) };
            return Task.FromResult(new EvolutionPopulation<SimpleChromosome>(chromosomes));
        };

        // Act
        var pipeline = createPopulation
            .Evolve(engine, generations: 5)
            .MatchResult(
                chromosome => $"Success: {chromosome.Value}",
                error => $"Failed: {error}");

        var result = await pipeline(1);

        // Assert
        result.Should().StartWith("Success:");
    }

    [Fact]
    public async Task CreateEvolveFromInputStep_WorksCorrectly()
    {
        // Arrange
        var targetValue = 55.0;
        var fitnessFunction = new TargetValueFitnessFunction(targetValue);

        Func<SimpleChromosome, SimpleChromosome, double, Result<SimpleChromosome>> crossoverFunc =
            (p1, p2, ratio) =>
            {
                var newValue = p1.Value * ratio + p2.Value * (1 - ratio);
                return Result<SimpleChromosome>.Success(new SimpleChromosome(newValue));
            };

        Func<SimpleChromosome, Random, Result<SimpleChromosome>> mutationFunc =
            (c, random) =>
            {
                var delta = (random.NextDouble() - 0.5) * 7.0;
                return Result<SimpleChromosome>.Success(new SimpleChromosome(c.Value + delta));
            };

        var engine = new EvolutionEngine<SimpleChromosome>(
            fitnessFunction,
            crossoverFunc,
            mutationFunc,
            seed: 42);

        Func<string, Result<EvolutionPopulation<SimpleChromosome>>> populationFactory = seed =>
        {
            var chromosomes = Enumerable.Range(0, 15)
                .Select(i => new SimpleChromosome(i * 3.5 + seed.Length))
                .ToList();
            return Result<EvolutionPopulation<SimpleChromosome>>.Success(new EvolutionPopulation<SimpleChromosome>(chromosomes));
        };

        // Act
        var step = GeneticEvolutionStep.CreateEvolveFromInputStep(populationFactory, engine, generations: 30);
        var result = await step("test input");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Value.Should().BeInRange(targetValue - 25, targetValue + 25);
    }

    [Fact]
    public async Task CompleteOptimizationPipeline_WorksEndToEnd()
    {
        // Arrange - Complete pipeline: input -> population -> evolve -> extract value -> format
        var targetValue = 88.0;
        var fitnessFunction = new TargetValueFitnessFunction(targetValue);

        Func<SimpleChromosome, SimpleChromosome, double, Result<SimpleChromosome>> crossoverFunc =
            (p1, p2, ratio) =>
            {
                var newValue = p1.Value * ratio + p2.Value * (1 - ratio);
                return Result<SimpleChromosome>.Success(new SimpleChromosome(newValue));
            };

        Func<SimpleChromosome, Random, Result<SimpleChromosome>> mutationFunc =
            (c, random) =>
            {
                var delta = (random.NextDouble() - 0.5) * 6.0;
                return Result<SimpleChromosome>.Success(new SimpleChromosome(c.Value + delta));
            };

        var engine = new EvolutionEngine<SimpleChromosome>(
            fitnessFunction,
            crossoverFunc,
            mutationFunc,
            seed: 42);

        // Create complete pipeline
        Step<string, int> parseInput = input => Task.FromResult(int.Parse(input));

        Step<int, EvolutionPopulation<SimpleChromosome>> createPopulation = size =>
        {
            var chromosomes = Enumerable.Range(0, size)
                .Select(i => new SimpleChromosome(i * 2.8))
                .ToList();
            return Task.FromResult(new EvolutionPopulation<SimpleChromosome>(chromosomes));
        };

        // Act - Chain the complete pipeline
        var completePipeline = parseInput
            .Then(createPopulation)
            .Evolve(engine, generations: 60)
            .MatchResult(
                chromosome => $"Optimized to {chromosome.Value:F2} with fitness {chromosome.Fitness:F4}",
                error => $"Optimization failed: {error}");

        var result = await completePipeline("35");

        // Assert
        result.Should().StartWith("Optimized to");
        result.Should().Contain("fitness");
    }
}
