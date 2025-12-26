// <copyright file="GeneticOptimizationExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Examples;

using Ouroboros.Core.Monads;
using Ouroboros.Core.Steps;
using Ouroboros.Genetic.Abstractions;
using Ouroboros.Genetic.Core;
using Ouroboros.Genetic.Extensions;

/// <summary>
/// Demonstrates evolutionary optimization of pipeline configurations using genetic algorithms.
/// This example shows how to use the Genetic module to evolve optimal parameters for AI pipelines.
/// </summary>
public static class GeneticOptimizationExample
{
    /// <summary>
    /// Example 1: Optimize a simple numerical transformation.
    /// Evolves a multiplier to transform input into a target range.
    /// </summary>
    public static async Task OptimizeMultiplierExample()
    {
        Console.WriteLine("=== Example 1: Optimizing a Multiplier ===\n");

        // Define a fitness function that evaluates how close the output is to 100
        var fitnessFunction = new TargetValueFitnessFunction(targetOutput: 100);

        // Create a step factory that generates multiplication steps based on gene value
        Func<double, Step<double, double>> stepFactory = multiplier =>
            input => Task.FromResult(input * multiplier);

        // Define mutation: slightly adjust the multiplier
        Func<double, double> mutateGene = multiplier =>
        {
            var random = new Random();
            return multiplier + (random.NextDouble() - 0.5) * 2; // +/- 1
        };

        // Initial population: random multipliers
        var initialPopulation = new List<IChromosome<double>>
        {
            new Chromosome<double>(new List<double> { 2.0 }),
            new Chromosome<double>(new List<double> { 5.0 }),
            new Chromosome<double>(new List<double> { 10.0 }),
            new Chromosome<double>(new List<double> { 15.0 }),
            new Chromosome<double>(new List<double> { 20.0 }),
        };

        // Create an identity step and evolve it
        var evolvedStep = GeneticPipelineExtensions.Identity<double>()
            .Evolve(
                stepFactory,
                fitnessFunction,
                mutateGene,
                initialPopulation,
                generations: 50,
                mutationRate: 0.2,
                crossoverRate: 0.8,
                elitismRate: 0.1,
                seed: 42);

        // Test with input of 10 (should evolve multiplier close to 10)
        double testInput = 10.0;
        var result = await evolvedStep(testInput);

        result.Match(
            onSuccess: output =>
            {
                Console.WriteLine($"Input: {testInput}");
                Console.WriteLine($"Evolved Output: {output:F2}");
                Console.WriteLine($"Target: 100");
                Console.WriteLine($"Distance from target: {Math.Abs(output - 100):F2}\n");
            },
            onFailure: error => Console.WriteLine($"Error: {error}\n"));
    }

    /// <summary>
    /// Example 2: Optimize prompt template parameters.
    /// Evolves temperature and max_tokens parameters for an LLM.
    /// </summary>
    public static async Task OptimizePromptParametersExample()
    {
        Console.WriteLine("=== Example 2: Optimizing Prompt Parameters ===\n");

        // Configuration for LLM parameters
        var fitnessFunction = new PromptParameterFitnessFunction();

        // Step factory that creates a prompt generation step with specific parameters
        Func<PromptConfig, Step<string, string>> stepFactory = config =>
            async prompt =>
            {
                // Simulate LLM call with parameters
                await Task.Delay(10); // Simulate API call
                return $"[Temp={config.Temperature:F2}, MaxTokens={config.MaxTokens}] Response to: {prompt}";
            };

        // Mutation function for prompt configurations
        Func<PromptConfig, PromptConfig> mutateGene = config =>
        {
            var random = new Random();
            return new PromptConfig
            {
                Temperature = Math.Clamp(config.Temperature + (random.NextDouble() - 0.5) * 0.2, 0, 2),
                MaxTokens = Math.Max(10, config.MaxTokens + random.Next(-50, 51)),
            };
        };

        // Initial population of configurations
        var initialPopulation = new List<IChromosome<PromptConfig>>
        {
            new Chromosome<PromptConfig>(new List<PromptConfig> { new() { Temperature = 0.3, MaxTokens = 100 } }),
            new Chromosome<PromptConfig>(new List<PromptConfig> { new() { Temperature = 0.7, MaxTokens = 200 } }),
            new Chromosome<PromptConfig>(new List<PromptConfig> { new() { Temperature = 1.0, MaxTokens = 500 } }),
            new Chromosome<PromptConfig>(new List<PromptConfig> { new() { Temperature = 1.5, MaxTokens = 1000 } }),
        };

        // Evolve with metadata to see the best configuration
        var evolvedStep = GeneticPipelineExtensions.Identity<string>()
            .EvolveWithMetadata(
                stepFactory,
                fitnessFunction,
                mutateGene,
                initialPopulation,
                generations: 30,
                mutationRate: 0.15,
                seed: 42);

        var result = await evolvedStep("Explain quantum computing");

        result.Match(
            onSuccess: tuple =>
            {
                var (bestChromosome, output) = tuple;
                var bestConfig = bestChromosome.Genes.First();
                Console.WriteLine($"Best Configuration Found:");
                Console.WriteLine($"  Temperature: {bestConfig.Temperature:F2}");
                Console.WriteLine($"  MaxTokens: {bestConfig.MaxTokens}");
                Console.WriteLine($"  Fitness: {bestChromosome.Fitness:F2}");
                Console.WriteLine($"\nSample Output: {output}\n");
            },
            onFailure: error => Console.WriteLine($"Error: {error}\n"));
    }

    /// <summary>
    /// Example 3: Optimize a multi-parameter transformation pipeline.
    /// Shows how to evolve complex parameter combinations.
    /// </summary>
    public static async Task OptimizeComplexPipelineExample()
    {
        Console.WriteLine("=== Example 3: Optimizing Complex Pipeline Parameters ===\n");

        // Multi-parameter configuration
        var fitnessFunction = new ComplexPipelineFitnessFunction();

        // Step factory with multiple parameters
        Func<PipelineConfig, Step<(int x, int y), int>> stepFactory = config =>
            input => Task.FromResult(
                (input.x * config.WeightX + input.y * config.WeightY + config.Bias) / config.Divisor);

        // Mutation for complex configurations
        Func<PipelineConfig, PipelineConfig> mutateGene = config =>
        {
            var random = new Random();
            return new PipelineConfig
            {
                WeightX = config.WeightX + random.Next(-2, 3),
                WeightY = config.WeightY + random.Next(-2, 3),
                Bias = config.Bias + random.Next(-5, 6),
                Divisor = Math.Max(1, config.Divisor + random.Next(-1, 2)),
            };
        };

        // Initial population
        var initialPopulation = new List<IChromosome<PipelineConfig>>
        {
            new Chromosome<PipelineConfig>(new List<PipelineConfig> { new() { WeightX = 1, WeightY = 1, Bias = 0, Divisor = 2 } }),
            new Chromosome<PipelineConfig>(new List<PipelineConfig> { new() { WeightX = 2, WeightY = 3, Bias = 5, Divisor = 1 } }),
            new Chromosome<PipelineConfig>(new List<PipelineConfig> { new() { WeightX = 3, WeightY = 2, Bias = -5, Divisor = 3 } }),
            new Chromosome<PipelineConfig>(new List<PipelineConfig> { new() { WeightX = 4, WeightY = 1, Bias = 10, Divisor = 2 } }),
        };

        var evolvedStep = GeneticPipelineExtensions.Identity<(int x, int y)>()
            .EvolveWithMetadata(
                stepFactory,
                fitnessFunction,
                mutateGene,
                initialPopulation,
                generations: 40,
                mutationRate: 0.1,
                crossoverRate: 0.9,
                seed: 42);

        var result = await evolvedStep((10, 20));

        result.Match(
            onSuccess: tuple =>
            {
                var (bestChromosome, output) = tuple;
                var bestConfig = bestChromosome.Genes.First();
                Console.WriteLine($"Best Pipeline Configuration:");
                Console.WriteLine($"  WeightX: {bestConfig.WeightX}");
                Console.WriteLine($"  WeightY: {bestConfig.WeightY}");
                Console.WriteLine($"  Bias: {bestConfig.Bias}");
                Console.WriteLine($"  Divisor: {bestConfig.Divisor}");
                Console.WriteLine($"  Fitness: {bestChromosome.Fitness:F2}");
                Console.WriteLine($"\nOutput for (10, 20): {output}\n");
            },
            onFailure: error => Console.WriteLine($"Error: {error}\n"));
    }

    /// <summary>
    /// Runs all examples.
    /// </summary>
    public static async Task RunAllExamples()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════════╗");
        Console.WriteLine("║    Genetic Algorithm Pipeline Optimization Examples     ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════════╝\n");

        await OptimizeMultiplierExample();
        await OptimizePromptParametersExample();
        await OptimizeComplexPipelineExample();

        Console.WriteLine("All examples completed successfully!");
    }

    // Helper classes for examples

    private class TargetValueFitnessFunction : IFitnessFunction<double>
    {
        private readonly double target;

        public TargetValueFitnessFunction(double targetOutput)
        {
            this.target = targetOutput;
        }

        public Task<double> EvaluateAsync(IChromosome<double> chromosome)
        {
            // Simulate evaluation with input of 10
            double multiplier = chromosome.Genes.FirstOrDefault();
            double output = 10.0 * multiplier;
            double fitness = -Math.Abs(output - this.target); // Closer to target = higher fitness
            return Task.FromResult(fitness);
        }
    }

    private class PromptConfig
    {
        public double Temperature { get; set; }
        public int MaxTokens { get; set; }
    }

    private class PromptParameterFitnessFunction : IFitnessFunction<PromptConfig>
    {
        public Task<double> EvaluateAsync(IChromosome<PromptConfig> chromosome)
        {
            var config = chromosome.Genes.FirstOrDefault() ?? new PromptConfig();
            
            // Fitness based on reasonable parameter ranges
            // Prefer moderate temperature and token count
            double tempScore = -Math.Abs(config.Temperature - 0.7) * 10; // Prefer around 0.7
            double tokenScore = -Math.Abs(config.MaxTokens - 300) / 10.0; // Prefer around 300
            
            return Task.FromResult(tempScore + tokenScore);
        }
    }

    private class PipelineConfig
    {
        public int WeightX { get; set; }
        public int WeightY { get; set; }
        public int Bias { get; set; }
        public int Divisor { get; set; }
    }

    private class ComplexPipelineFitnessFunction : IFitnessFunction<PipelineConfig>
    {
        public Task<double> EvaluateAsync(IChromosome<PipelineConfig> chromosome)
        {
            var config = chromosome.Genes.FirstOrDefault() ?? new PipelineConfig();
            
            // Simulate evaluation with test input (10, 20)
            int output = (10 * config.WeightX + 20 * config.WeightY + config.Bias) / config.Divisor;
            
            // Target output is around 50
            double fitness = -Math.Abs(output - 50);
            return Task.FromResult(fitness);
        }
    }
}
