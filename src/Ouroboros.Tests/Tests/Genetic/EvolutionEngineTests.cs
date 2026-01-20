// <copyright file="EvolutionEngineTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Genetic;

using FluentAssertions;
using Ouroboros.Genetic.Abstractions;
using Ouroboros.Genetic.Core;
using Xunit;

/// <summary>
/// Simple fitness function for testing that optimizes towards a target value.
/// </summary>
internal sealed class TargetValueFitnessFunction : IEvolutionFitnessFunction<SimpleChromosome>
{
    private readonly double targetValue;

    public TargetValueFitnessFunction(double targetValue)
    {
        this.targetValue = targetValue;
    }

    public Task<Result<double>> EvaluateAsync(SimpleChromosome chromosome)
    {
        // Fitness is inverse of distance from target (closer = better)
        var distance = Math.Abs(chromosome.Value - this.targetValue);
        var fitness = 1.0 / (1.0 + distance);
        return Task.FromResult(Result<double>.Success(fitness));
    }
}

/// <summary>
/// Tests for the EvolutionEngine class.
/// </summary>
[Trait("Category", "Unit")]
public class EvolutionEngineTests
{
    [Fact]
    public void Constructor_ThrowsForInvalidElitismRate()
    {
        // Arrange
        var fitnessFunction = new TargetValueFitnessFunction(50.0);

        Func<SimpleChromosome, SimpleChromosome, double, Result<SimpleChromosome>> crossoverFunc =
            (p1, p2, ratio) => Result<SimpleChromosome>.Success(new SimpleChromosome(15.0));

        Func<SimpleChromosome, Random, Result<SimpleChromosome>> mutationFunc =
            (c, random) => Result<SimpleChromosome>.Success(new SimpleChromosome(c.Value + 1));

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EvolutionEngine<SimpleChromosome>(
                fitnessFunction,
                crossoverFunc,
                mutationFunc,
                elitismRate: -0.1));

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new EvolutionEngine<SimpleChromosome>(
                fitnessFunction,
                crossoverFunc,
                mutationFunc,
                elitismRate: 1.5));
    }

    [Fact]
    public async Task EvolveAsync_EvolvesPopulationSuccessfully()
    {
        // Arrange
        var targetValue = 50.0;
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
                var delta = (random.NextDouble() - 0.5) * 10.0;
                return Result<SimpleChromosome>.Success(new SimpleChromosome(c.Value + delta));
            };

        var engine = new EvolutionEngine<SimpleChromosome>(
            fitnessFunction,
            crossoverFunc,
            mutationFunc,
            crossoverRate: 0.8,
            mutationRate: 0.2,
            elitismRate: 0.1,
            seed: 42);

        var initialChromosomes = Enumerable.Range(0, 20)
            .Select(i => new SimpleChromosome(i * 5.0))
            .ToList();
        var initialPopulation = new EvolutionPopulation<SimpleChromosome>(initialChromosomes);

        // Act
        var result = await engine.EvolveAsync(initialPopulation, generations: 50);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Size.Should().Be(20);

        var best = engine.GetBest(result.Value);
        best.HasValue.Should().BeTrue();

        // The evolved population should have improved fitness
        var initialBest = initialPopulation.GetBest();
        best.Value!.Fitness.Should().BeGreaterThan(initialBest.Value!.Fitness);
    }

    [Fact]
    public async Task EvolveAsync_ReturnsFailureForNullPopulation()
    {
        // Arrange
        var fitnessFunction = new TargetValueFitnessFunction(50.0);

        Func<SimpleChromosome, SimpleChromosome, double, Result<SimpleChromosome>> crossoverFunc =
            (p1, p2, ratio) => Result<SimpleChromosome>.Success(new SimpleChromosome(15.0));

        Func<SimpleChromosome, Random, Result<SimpleChromosome>> mutationFunc =
            (c, random) => Result<SimpleChromosome>.Success(new SimpleChromosome(c.Value + 1));

        var engine = new EvolutionEngine<SimpleChromosome>(
            fitnessFunction,
            crossoverFunc,
            mutationFunc);

        // Act
        var result = await engine.EvolveAsync(null!, generations: 10);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be null");
    }

    [Fact]
    public async Task EvolveAsync_ReturnsFailureForEmptyPopulation()
    {
        // Arrange
        var fitnessFunction = new TargetValueFitnessFunction(50.0);

        Func<SimpleChromosome, SimpleChromosome, double, Result<SimpleChromosome>> crossoverFunc =
            (p1, p2, ratio) => Result<SimpleChromosome>.Success(new SimpleChromosome(15.0));

        Func<SimpleChromosome, Random, Result<SimpleChromosome>> mutationFunc =
            (c, random) => Result<SimpleChromosome>.Success(new SimpleChromosome(c.Value + 1));

        var engine = new EvolutionEngine<SimpleChromosome>(
            fitnessFunction,
            crossoverFunc,
            mutationFunc);

        var emptyPopulation = new EvolutionPopulation<SimpleChromosome>(Array.Empty<SimpleChromosome>());

        // Act
        var result = await engine.EvolveAsync(emptyPopulation, generations: 10);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be empty");
    }

    [Fact]
    public async Task EvolveAsync_ReturnsFailureForNegativeGenerations()
    {
        // Arrange
        var fitnessFunction = new TargetValueFitnessFunction(50.0);

        Func<SimpleChromosome, SimpleChromosome, double, Result<SimpleChromosome>> crossoverFunc =
            (p1, p2, ratio) => Result<SimpleChromosome>.Success(new SimpleChromosome(15.0));

        Func<SimpleChromosome, Random, Result<SimpleChromosome>> mutationFunc =
            (c, random) => Result<SimpleChromosome>.Success(new SimpleChromosome(c.Value + 1));

        var engine = new EvolutionEngine<SimpleChromosome>(
            fitnessFunction,
            crossoverFunc,
            mutationFunc);

        var population = new EvolutionPopulation<SimpleChromosome>(new[] { new SimpleChromosome(10.0) });

        // Act
        var result = await engine.EvolveAsync(population, generations: -1);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("non-negative");
    }

    [Fact]
    public async Task EvolveAsync_WithZeroGenerations_ReturnsEvaluatedPopulation()
    {
        // Arrange
        var fitnessFunction = new TargetValueFitnessFunction(50.0);

        Func<SimpleChromosome, SimpleChromosome, double, Result<SimpleChromosome>> crossoverFunc =
            (p1, p2, ratio) => Result<SimpleChromosome>.Success(new SimpleChromosome(15.0));

        Func<SimpleChromosome, Random, Result<SimpleChromosome>> mutationFunc =
            (c, random) => Result<SimpleChromosome>.Success(new SimpleChromosome(c.Value + 1));

        var engine = new EvolutionEngine<SimpleChromosome>(
            fitnessFunction,
            crossoverFunc,
            mutationFunc);

        var population = new EvolutionPopulation<SimpleChromosome>(new[] { new SimpleChromosome(10.0) });

        // Act
        var result = await engine.EvolveAsync(population, generations: 0);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Size.Should().Be(1);
        result.Value.Chromosomes[0].Fitness.Should().BeGreaterThan(0.0);
    }

    [Fact]
    public void GetBest_ReturnsNoneForNullPopulation()
    {
        // Arrange
        var fitnessFunction = new TargetValueFitnessFunction(50.0);

        Func<SimpleChromosome, SimpleChromosome, double, Result<SimpleChromosome>> crossoverFunc =
            (p1, p2, ratio) => Result<SimpleChromosome>.Success(new SimpleChromosome(15.0));

        Func<SimpleChromosome, Random, Result<SimpleChromosome>> mutationFunc =
            (c, random) => Result<SimpleChromosome>.Success(new SimpleChromosome(c.Value + 1));

        var engine = new EvolutionEngine<SimpleChromosome>(
            fitnessFunction,
            crossoverFunc,
            mutationFunc);

        // Act
        var best = engine.GetBest(null!);

        // Assert
        best.HasValue.Should().BeFalse();
    }

    [Fact]
    public async Task EvolveAsync_ConvergesToTarget()
    {
        // Arrange - optimize towards value 100
        var targetValue = 100.0;
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

        var engine = new EvolutionEngine<SimpleChromosome>(
            fitnessFunction,
            crossoverFunc,
            mutationFunc,
            crossoverRate: 0.9,
            mutationRate: 0.15,
            elitismRate: 0.2,
            seed: 42);

        var initialChromosomes = Enumerable.Range(0, 50)
            .Select(i => new SimpleChromosome(i * 2.0))
            .ToList();
        var initialPopulation = new EvolutionPopulation<SimpleChromosome>(initialChromosomes);

        // Act
        var result = await engine.EvolveAsync(initialPopulation, generations: 100);

        // Assert
        result.IsSuccess.Should().BeTrue();

        var best = engine.GetBest(result.Value);
        best.HasValue.Should().BeTrue();

        // Best solution should be close to target
        best.Value!.Value.Should().BeInRange(targetValue - 10, targetValue + 10);
    }
}
