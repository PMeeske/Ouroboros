// <copyright file="SimpleChromosomeTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests.Genetic;

using FluentAssertions;
using Ouroboros.Genetic.Abstractions;
using Ouroboros.Genetic.Core;
using Ouroboros.Tests.Shared;
using Xunit;

/// <summary>
/// Tests for the SimpleChromosome implementation.
/// </summary>
[Trait("Category", "Unit")]
public class SimpleChromosomeTests
{
    [Fact]
    public void Constructor_CreatesChromosomeWithValues()
    {
        // Arrange & Act
        var chromosome = new SimpleChromosome(42.0, 1, 0.5);

        // Assert
        chromosome.Value.Should().Be(42.0);
        chromosome.Generation.Should().Be(1);
        chromosome.Fitness.Should().Be(0.5);
        chromosome.Id.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Clone_CreatesEqualChromosome()
    {
        // Arrange
        var original = new SimpleChromosome(10.0, 2, 0.8);

        // Act
        var clone = (SimpleChromosome)original.Clone();

        // Assert
        clone.Id.Should().Be(original.Id);
        clone.Value.Should().Be(original.Value);
        clone.Generation.Should().Be(original.Generation);
        clone.Fitness.Should().Be(original.Fitness);
    }

    [Fact]
    public void WithFitness_UpdatesFitnessImmutably()
    {
        // Arrange
        var original = new SimpleChromosome(5.0, 1, 0.3);

        // Act
        var updated = (SimpleChromosome)original.WithFitness(0.9);

        // Assert
        updated.Fitness.Should().Be(0.9);
        updated.Value.Should().Be(original.Value);
        updated.Generation.Should().Be(original.Generation);
        original.Fitness.Should().Be(0.3); // Original unchanged
    }
}
