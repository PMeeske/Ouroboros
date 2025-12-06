// <copyright file="IChromosome.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Genetic.Abstractions;

/// <summary>
/// Represents an evolving solution in a genetic algorithm.
/// A chromosome encodes a potential solution (e.g., a prompt, parameters, configuration).
/// </summary>
public interface IChromosome
{
    /// <summary>
    /// Gets the unique identifier for this chromosome.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the fitness score of this chromosome.
    /// Higher values indicate better fitness.
    /// </summary>
    double Fitness { get; }

    /// <summary>
    /// Gets the generation number when this chromosome was created.
    /// </summary>
    int Generation { get; }

    /// <summary>
    /// Creates a deep copy of this chromosome.
    /// </summary>
    /// <returns>A new chromosome instance with the same genetic information.</returns>
    IChromosome Clone();

    /// <summary>
    /// Updates the fitness score of this chromosome.
    /// Returns a new chromosome instance with the updated fitness (immutable pattern).
    /// </summary>
    /// <param name="fitness">The new fitness score.</param>
    /// <returns>A new chromosome with updated fitness.</returns>
    IChromosome WithFitness(double fitness);
}
