// <copyright file="UniformCrossoverTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Genetic;

using FluentAssertions;
using Ouroboros.Genetic.Core;
using Xunit;

/// <summary>
/// Tests for the UniformCrossover class.
/// </summary>
[Trait("Category", "Unit")]
public class UniformCrossoverTests
{
    [Fact]
    public void Constructor_AcceptsValidCrossoverRate()
    {
        // Act & Assert
        var crossover = new EvolutionCrossover(0.8);
        crossover.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_ThrowsForInvalidCrossoverRate()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new EvolutionCrossover(-0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EvolutionCrossover(1.5));
    }

    [Fact]
    public void Crossover_ReturnsSuccessForValidParents()
    {
        // Arrange
        var parent1 = new SimpleChromosome(10.0, fitness: 0.5);
        var parent2 = new SimpleChromosome(20.0, fitness: 0.5);
        var crossover = new EvolutionCrossover(1.0, seed: 42); // Always crossover

        Func<SimpleChromosome, SimpleChromosome, double, Result<SimpleChromosome>> crossoverFunc =
            (p1, p2, ratio) =>
            {
                var newValue = p1.Value * ratio + p2.Value * (1 - ratio);
                return Result<SimpleChromosome>.Success(new SimpleChromosome(newValue));
            };

        // Act
        var result = crossover.Crossover(parent1, parent2, crossoverFunc);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public void Crossover_ReturnsFailureForNullParents()
    {
        // Arrange
        var parent = new SimpleChromosome(10.0);
        var crossover = new EvolutionCrossover();

        Func<SimpleChromosome, SimpleChromosome, double, Result<SimpleChromosome>> crossoverFunc =
            (p1, p2, ratio) => Result<SimpleChromosome>.Success(new SimpleChromosome(15.0));

        // Act
        var result1 = crossover.Crossover(null!, parent, crossoverFunc);
        var result2 = crossover.Crossover(parent, null!, crossoverFunc);

        // Assert
        result1.IsFailure.Should().BeTrue();
        result1.Error.Should().Contain("cannot be null");
        result2.IsFailure.Should().BeTrue();
        result2.Error.Should().Contain("cannot be null");
    }

    [Fact]
    public void Crossover_WithZeroRate_ReturnsClone()
    {
        // Arrange
        var parent1 = new SimpleChromosome(10.0, fitness: 0.5);
        var parent2 = new SimpleChromosome(20.0, fitness: 0.5);
        var crossover = new EvolutionCrossover(0.0); // Never crossover

        Func<SimpleChromosome, SimpleChromosome, double, Result<SimpleChromosome>> crossoverFunc =
            (p1, p2, ratio) => Result<SimpleChromosome>.Success(new SimpleChromosome(999.0));

        // Act
        var result = crossover.Crossover(parent1, parent2, crossoverFunc);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(10.0); // Should be clone of parent1
    }

    [Fact]
    public void Crossover_ProducesReproducibleResultsWithSeed()
    {
        // Arrange
        var parent1 = new SimpleChromosome(10.0, fitness: 0.5);
        var parent2 = new SimpleChromosome(20.0, fitness: 0.5);
        var crossover1 = new EvolutionCrossover(0.8, seed: 42);
        var crossover2 = new EvolutionCrossover(0.8, seed: 42);

        Func<SimpleChromosome, SimpleChromosome, double, Result<SimpleChromosome>> crossoverFunc =
            (p1, p2, ratio) =>
            {
                var newValue = p1.Value * ratio + p2.Value * (1 - ratio);
                return Result<SimpleChromosome>.Success(new SimpleChromosome(newValue));
            };

        // Act
        var result1 = crossover1.Crossover(parent1, parent2, crossoverFunc);
        var result2 = crossover2.Crossover(parent1, parent2, crossoverFunc);

        // Assert
        result1.Value.Value.Should().Be(result2.Value.Value);
    }

    [Fact]
    public void CrossoverPair_ReturnsTwoOffspring()
    {
        // Arrange
        var parent1 = new SimpleChromosome(10.0, fitness: 0.5);
        var parent2 = new SimpleChromosome(20.0, fitness: 0.5);
        var crossover = new EvolutionCrossover(1.0, seed: 42); // Always crossover

        Func<SimpleChromosome, SimpleChromosome, double, Result<SimpleChromosome>> crossoverFunc =
            (p1, p2, ratio) =>
            {
                var newValue = p1.Value * ratio + p2.Value * (1 - ratio);
                return Result<SimpleChromosome>.Success(new SimpleChromosome(newValue));
            };

        // Act
        var result = crossover.CrossoverPair(parent1, parent2, crossoverFunc);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Offspring1.Should().NotBeNull();
        result.Value.Offspring2.Should().NotBeNull();
    }

    [Fact]
    public void CrossoverPair_PropagatesFailures()
    {
        // Arrange
        var parent1 = new SimpleChromosome(10.0);
        var parent2 = new SimpleChromosome(20.0);
        var crossover = new EvolutionCrossover(1.0);

        Func<SimpleChromosome, SimpleChromosome, double, Result<SimpleChromosome>> failingFunc =
            (p1, p2, ratio) => Result<SimpleChromosome>.Failure("Crossover failed");

        // Act
        var result = crossover.CrossoverPair(parent1, parent2, failingFunc);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Crossover failed");
    }
}
