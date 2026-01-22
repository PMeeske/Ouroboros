// <copyright file="GeneticTestUtilities.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Genetic.Abstractions;

namespace Ouroboros.Tests.Shared;

/// <summary>
/// Test implementation of IChromosome for unit testing.
/// Represents a simple numeric chromosome.
/// </summary>
public sealed class SimpleChromosome : IChromosome
{
    public SimpleChromosome(double value, int generation = 0, double fitness = 0.0)
    {
        this.Id = Guid.NewGuid().ToString();
        this.Value = value;
        this.Generation = generation;
        this.Fitness = fitness;
    }

    private SimpleChromosome(string id, double value, int generation, double fitness)
    {
        this.Id = id;
        this.Value = value;
        this.Generation = generation;
        this.Fitness = fitness;
    }

    public string Id { get; }

    public double Value { get; }

    public int Generation { get; }

    public double Fitness { get; }

    public IChromosome Clone()
    {
        return new SimpleChromosome(this.Id, this.Value, this.Generation, this.Fitness);
    }

    public IChromosome WithFitness(double fitness)
    {
        return new SimpleChromosome(this.Id, this.Value, this.Generation, fitness);
    }
}

/// <summary>
/// Simple fitness function for testing that optimizes towards a target value.
/// </summary>
public sealed class TargetValueFitnessFunction : IEvolutionFitnessFunction<SimpleChromosome>
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
