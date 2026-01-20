// <copyright file="MutationTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Genetic;

using FluentAssertions;
using Ouroboros.Genetic.Core;
using Xunit;

/// <summary>
/// Tests for the Mutation class.
/// </summary>
[Trait("Category", "Unit")]
public class MutationTests
{
    [Fact]
    public void Constructor_AcceptsValidMutationRate()
    {
        // Act & Assert
        var mutation = new EvolutionMutation(0.1);
        mutation.Should().NotBeNull();
        mutation.MutationRate.Should().Be(0.1);
    }

    [Fact]
    public void Constructor_ThrowsForInvalidMutationRate()
    {
        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => new EvolutionMutation(-0.1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new EvolutionMutation(1.5));
    }

    [Fact]
    public void Mutate_ReturnsSuccessForValidChromosome()
    {
        // Arrange
        var chromosome = new SimpleChromosome(10.0, fitness: 0.5);
        var mutation = new EvolutionMutation(1.0, seed: 42); // Always mutate

        Func<SimpleChromosome, Random, Result<SimpleChromosome>> mutationFunc =
            (c, random) =>
            {
                var newValue = c.Value + random.NextDouble();
                return Result<SimpleChromosome>.Success(new SimpleChromosome(newValue));
            };

        // Act
        var result = mutation.Mutate(chromosome, mutationFunc);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Value.Should().NotBe(chromosome.Value); // Should be mutated
    }

    [Fact]
    public void Mutate_ReturnsFailureForNullChromosome()
    {
        // Arrange
        var mutation = new EvolutionMutation();

        Func<SimpleChromosome, Random, Result<SimpleChromosome>> mutationFunc =
            (c, random) => Result<SimpleChromosome>.Success(new SimpleChromosome(15.0));

        // Act
        var result = mutation.Mutate(null!, mutationFunc);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be null");
    }

    [Fact]
    public void Mutate_WithZeroRate_ReturnsClone()
    {
        // Arrange
        var chromosome = new SimpleChromosome(10.0, fitness: 0.5);
        var mutation = new EvolutionMutation(0.0); // Never mutate

        Func<SimpleChromosome, Random, Result<SimpleChromosome>> mutationFunc =
            (c, random) => Result<SimpleChromosome>.Success(new SimpleChromosome(999.0));

        // Act
        var result = mutation.Mutate(chromosome, mutationFunc);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Value.Should().Be(10.0); // Should be clone
    }

    [Fact]
    public void Mutate_ProducesReproducibleResultsWithSeed()
    {
        // Arrange
        var chromosome = new SimpleChromosome(10.0, fitness: 0.5);
        var mutation1 = new EvolutionMutation(1.0, seed: 42);
        var mutation2 = new EvolutionMutation(1.0, seed: 42);

        Func<SimpleChromosome, Random, Result<SimpleChromosome>> mutationFunc =
            (c, random) =>
            {
                var newValue = c.Value + random.NextDouble();
                return Result<SimpleChromosome>.Success(new SimpleChromosome(newValue));
            };

        // Act
        var result1 = mutation1.Mutate(chromosome, mutationFunc);
        var result2 = mutation2.Mutate(chromosome, mutationFunc);

        // Assert
        result1.Value.Value.Should().Be(result2.Value.Value);
    }

    [Fact]
    public async Task MutatePopulation_MutatesAllChromosomes()
    {
        // Arrange
        var chromosomes = new[]
        {
            new SimpleChromosome(1.0),
            new SimpleChromosome(2.0),
            new SimpleChromosome(3.0),
        };
        var population = new EvolutionPopulation<SimpleChromosome>(chromosomes);
        var mutation = new EvolutionMutation(1.0, seed: 42); // Always mutate

        Func<SimpleChromosome, Random, Result<SimpleChromosome>> mutationFunc =
            (c, random) =>
            {
                var newValue = c.Value + 10.0;
                return Result<SimpleChromosome>.Success(new SimpleChromosome(newValue));
            };

        // Act
        var result = await mutation.MutatePopulationAsync(population, mutationFunc);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Size.Should().Be(3);
        result.Value.Chromosomes.Should().AllSatisfy(c => c.Value.Should().BeGreaterThan(10.0));
    }

    [Fact]
    public async Task MutatePopulation_ReturnsFailureForNullPopulation()
    {
        // Arrange
        var mutation = new EvolutionMutation();

        Func<SimpleChromosome, Random, Result<SimpleChromosome>> mutationFunc =
            (c, random) => Result<SimpleChromosome>.Success(new SimpleChromosome(15.0));

        // Act
        var result = await mutation.MutatePopulationAsync<SimpleChromosome>(null!, mutationFunc);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot be null");
    }

    [Fact]
    public async Task MutatePopulation_PropagatesFailures()
    {
        // Arrange
        var chromosomes = new[] { new SimpleChromosome(1.0) };
        var population = new EvolutionPopulation<SimpleChromosome>(chromosomes);
        var mutation = new EvolutionMutation(1.0);

        Func<SimpleChromosome, Random, Result<SimpleChromosome>> failingFunc =
            (c, random) => Result<SimpleChromosome>.Failure("Mutation failed");

        // Act
        var result = await mutation.MutatePopulationAsync(population, failingFunc);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Mutation failed");
    }
}
