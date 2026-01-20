// <copyright file="GeneticPipelineExtensionsTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Genetic;

using FluentAssertions;
using Ouroboros.Core.Monads;
using Ouroboros.Core.Steps;
using Ouroboros.Genetic.Abstractions;
using Ouroboros.Genetic.Core;
using Ouroboros.Genetic.Extensions;
using Xunit;

/// <summary>
/// Tests for the GeneticPipelineExtensions class.
/// </summary>
[Trait("Category", "Unit")]
public class GeneticPipelineExtensionsTests
{
    [Fact]
    public void Identity_ReturnsIdentityStep()
    {
        // Act
        var identityStep = GeneticPipelineExtensions.Identity<int>();

        // Assert
        identityStep.Should().NotBeNull();
    }

    [Fact]
    public async Task Identity_PreservesInput()
    {
        // Arrange
        var identityStep = GeneticPipelineExtensions.Identity<int>();
        int input = 42;

        // Act
        var output = await identityStep(input);

        // Assert
        output.Should().Be(input);
    }

    [Fact]
    public async Task Evolve_OptimizesStepConfiguration()
    {
        // Arrange - evolve a multiplier parameter
        var identityStep = GeneticPipelineExtensions.Identity<int>();
        
        // Step factory that creates a multiplication step based on gene
        Func<int, Step<int, int>> stepFactory = multiplier =>
            input => Task.FromResult(input * multiplier);

        // Fitness function that favors results closer to 100
        var fitnessFunction = new TargetFitnessFunction(target: 100);

        // Mutate by randomly adjusting multiplier
        Func<int, int> mutateGene = multiplier =>
        {
            var random = new Random();
            return multiplier + random.Next(-1, 2); // -1, 0, or 1
        };

        // Initial population of multipliers
        var initialPopulation = new List<IChromosome<int>>
        {
            new Chromosome<int>(new List<int> { 2 }),
            new Chromosome<int>(new List<int> { 5 }),
            new Chromosome<int>(new List<int> { 10 }),
            new Chromosome<int>(new List<int> { 20 }),
        };

        // Act - evolve with input of 10 (should find multiplier close to 10)
        var evolvedStep = identityStep.Evolve(
            stepFactory,
            fitnessFunction,
            mutateGene,
            initialPopulation,
            generations: 20,
            seed: 42);

        var result = await evolvedStep(10);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThanOrEqualTo(90); // Should be close to 100
    }

    [Fact]
    public async Task EvolveWithMetadata_ReturnsBestChromosomeAndOutput()
    {
        // Arrange
        var identityStep = GeneticPipelineExtensions.Identity<string>();
        
        // Step factory that appends a suffix
        Func<string, Step<string, string>> stepFactory = suffix =>
            input => Task.FromResult(input + suffix);

        // Fitness function based on output length
        var fitnessFunction = new LengthFitnessFunction();

        // Initial population of suffixes
        var initialPopulation = new List<IChromosome<string>>
        {
            new Chromosome<string>(new List<string> { "!" }),
            new Chromosome<string>(new List<string> { "!!" }),
            new Chromosome<string>(new List<string> { "!!!" }),
        };

        // Act
        var evolvedStep = identityStep.EvolveWithMetadata(
            stepFactory,
            fitnessFunction,
            suffix => suffix + "!",
            initialPopulation,
            generations: 5,
            seed: 42);

        var result = await evolvedStep("Hello");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Output.Should().StartWith("Hello");
        result.Value.BestChromosome.Should().NotBeNull();
        result.Value.BestChromosome.Fitness.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Evolve_HandlesComplexScenarios()
    {
        // Arrange - optimize a two-parameter transformation
        var identityStep = GeneticPipelineExtensions.Identity<(int x, int y)>();
        
        // Step factory using both genes
        Func<(int a, int b), Step<(int x, int y), int>> stepFactory = genes =>
            input => Task.FromResult(input.x * genes.a + input.y * genes.b);

        // Fitness function that wants result close to 50
        var fitnessFunction = new TupleFitnessFunction();

        // Mutate tuple genes
        Func<(int a, int b), (int a, int b)> mutateGene = genes =>
        {
            var random = new Random();
            return (genes.a + random.Next(-1, 2), genes.b + random.Next(-1, 2));
        };

        // Initial population
        var initialPopulation = new List<IChromosome<(int, int)>>
        {
            new Chromosome<(int, int)>(new List<(int, int)> { (1, 1) }),
            new Chromosome<(int, int)>(new List<(int, int)> { (2, 2) }),
            new Chromosome<(int, int)>(new List<(int, int)> { (3, 3) }),
        };

        // Act
        var evolvedStep = identityStep.Evolve(
            stepFactory,
            fitnessFunction,
            mutateGene,
            initialPopulation,
            generations: 10,
            seed: 42);

        var result = await evolvedStep((10, 5));

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThan(0);
    }

    private class TargetFitnessFunction : IFitnessFunction<int>
    {
        private readonly int target;

        public TargetFitnessFunction(int target)
        {
            this.target = target;
        }

        public Task<double> EvaluateAsync(IChromosome<int> chromosome)
        {
            // For testing, we simulate evaluation by computing with input 10
            // In real scenarios, this would involve running the actual step
            int multiplier = chromosome.Genes.FirstOrDefault();
            int result = 10 * multiplier;
            double fitness = -Math.Abs(result - this.target); // Negative distance from target
            return Task.FromResult(fitness);
        }
    }

    private class LengthFitnessFunction : IFitnessFunction<string>
    {
        public Task<double> EvaluateAsync(IChromosome<string> chromosome)
        {
            // Fitness based on length of the suffix
            string suffix = chromosome.Genes.FirstOrDefault() ?? string.Empty;
            double fitness = suffix.Length;
            return Task.FromResult(fitness);
        }
    }

    private class TupleFitnessFunction : IFitnessFunction<(int a, int b)>
    {
        public Task<double> EvaluateAsync(IChromosome<(int a, int b)> chromosome)
        {
            // Simple fitness based on sum
            var gene = chromosome.Genes.FirstOrDefault();
            double fitness = gene.a + gene.b;
            return Task.FromResult(fitness);
        }
    }
}
