# Genetic Algorithm Module

The Genetic Algorithm module (`Ouroboros.Genetic`) enables evolutionary optimization of monadic pipelines in Ouroboros. It provides a complete genetic algorithm implementation that can evolve parameters, configurations, or prompt variations to maximize a fitness function.

## Overview

This module treats "Emergence" as an optimization problem, allowing pipelines to "breed" optimal parameters through natural selection principles:

- **Selection**: Roulette wheel selection favors chromosomes with higher fitness
- **Crossover**: Uniform crossover creates offspring by mixing parent genes
- **Mutation**: Random mutations introduce variation
- **Elitism**: Best solutions are preserved across generations

## Architecture

### Abstractions (`Abstractions/`)

- **`IChromosome<TGene>`**: Represents a solution candidate with genes and fitness score
- **`IFitnessFunction<TGene>`**: Evaluates how good a solution is
- **`IGeneticAlgorithm<TGene>`**: Orchestrates the evolution process

### Core Components (`Core/`)

- **`Chromosome<TGene>`**: Immutable implementation of `IChromosome`
- **`Population<TGene>`**: Manages a collection of chromosomes
- **`RouletteWheelSelection<TGene>`**: Fitness-proportionate selection operator
- **`UniformCrossover<TGene>`**: Uniform crossover operator
- **`Mutation<TGene>`**: Mutation operator with configurable rate
- **`GeneticAlgorithm<TGene>`**: Complete GA implementation

### Pipeline Integration (`Steps/`, `Extensions/`)

- **`GeneticEvolutionStep<TIn, TOut, TGene>`**: Wraps pipeline steps with evolution
- **`GeneticPipelineExtensions`**: Fluent API for `.Evolve(...)` and `.EvolveWithMetadata(...)`

## Usage

### Basic Example: Optimizing a Multiplier

```csharp
using LangChainPipeline.Genetic.Core;
using LangChainPipeline.Genetic.Extensions;

// Define fitness function (minimize distance from target)
var fitnessFunction = new TargetFitnessFunction(target: 100);

// Step factory: creates a step from a gene (multiplier)
Func<double, Step<double, double>> stepFactory = multiplier =>
    input => Task.FromResult(input * multiplier);

// Mutation: slightly adjust the multiplier
Func<double, double> mutateGene = m => m + Random.Shared.NextDouble() * 2 - 1;

// Initial population
var initialPopulation = new List<IChromosome<double>>
{
    new Chromosome<double>(new List<double> { 2.0 }),
    new Chromosome<double>(new List<double> { 5.0 }),
    new Chromosome<double>(new List<double> { 10.0 }),
};

// Evolve the step
var evolvedStep = GeneticPipelineExtensions.Identity<double>()
    .Evolve(
        stepFactory,
        fitnessFunction,
        mutateGene,
        initialPopulation,
        generations: 50,
        mutationRate: 0.1,
        crossoverRate: 0.8,
        elitismRate: 0.1);

// Use the evolved step
var result = await evolvedStep(10.0);
// Result: ~100 (evolved multiplier ≈ 10)
```

### Optimizing LLM Parameters

```csharp
public class PromptConfig
{
    public double Temperature { get; set; }
    public int MaxTokens { get; set; }
}

// Fitness function evaluates response quality
var fitnessFunction = new ResponseQualityFitnessFunction();

// Step factory with LLM parameters
Func<PromptConfig, Step<string, string>> stepFactory = config =>
    async prompt =>
    {
        var llm = new ChatModel(temperature: config.Temperature, maxTokens: config.MaxTokens);
        return await llm.GenerateAsync(prompt);
    };

// Mutate configurations
Func<PromptConfig, PromptConfig> mutateGene = config => new()
{
    Temperature = Math.Clamp(config.Temperature + Random.Shared.NextDouble() * 0.2 - 0.1, 0, 2),
    MaxTokens = Math.Max(10, config.MaxTokens + Random.Shared.Next(-50, 51))
};

var initialPopulation = new List<IChromosome<PromptConfig>>
{
    new Chromosome<PromptConfig>(new List<PromptConfig> { new() { Temperature = 0.7, MaxTokens = 256 } }),
    new Chromosome<PromptConfig>(new List<PromptConfig> { new() { Temperature = 1.0, MaxTokens = 512 } }),
};

var evolvedStep = GeneticPipelineExtensions.Identity<string>()
    .EvolveWithMetadata(
        stepFactory,
        fitnessFunction,
        mutateGene,
        initialPopulation,
        generations: 30);

var result = await evolvedStep("Explain quantum computing");
Console.WriteLine($"Best Temperature: {result.Value.BestChromosome.Genes.First().Temperature}");
Console.WriteLine($"Best MaxTokens: {result.Value.BestChromosome.Genes.First().MaxTokens}");
Console.WriteLine($"Output: {result.Value.Output}");
```

### Multi-Parameter Optimization

```csharp
public class PipelineParams
{
    public int WeightX { get; set; }
    public int WeightY { get; set; }
    public int Bias { get; set; }
}

var fitnessFunction = new MultiParamFitnessFunction();

Func<PipelineParams, Step<(int x, int y), int>> stepFactory = p =>
    input => Task.FromResult(input.x * p.WeightX + input.y * p.WeightY + p.Bias);

Func<PipelineParams, PipelineParams> mutateGene = p => new()
{
    WeightX = p.WeightX + Random.Shared.Next(-2, 3),
    WeightY = p.WeightY + Random.Shared.Next(-2, 3),
    Bias = p.Bias + Random.Shared.Next(-5, 6)
};

// ... (similar evolution setup)
```

## Configuration Parameters

### Genetic Algorithm Parameters

- **`mutationRate`** (default: 0.01): Probability of mutating each gene (0.0-1.0)
  - Lower values: More stable convergence
  - Higher values: More exploration, slower convergence

- **`crossoverRate`** (default: 0.8): Probability of performing crossover (0.0-1.0)
  - Higher values: More offspring diversity
  - Lower values: More parent preservation

- **`elitismRate`** (default: 0.1): Proportion of top chromosomes to preserve (0.0-1.0)
  - Higher values: Better preservation of good solutions
  - Lower values: More exploration

- **`generations`**: Number of evolution cycles
  - More generations: Better optimization, longer runtime
  - Typical range: 20-100 depending on problem complexity

- **`seed`** (optional): Random seed for reproducibility

### Population Size

The size of `initialPopulation` determines the population size:
- Smaller (4-10): Faster but may miss good solutions
- Larger (20-50): Better exploration but slower
- Recommended: 10-20 for most problems

## Fitness Function Design

A good fitness function should:

1. **Return higher values for better solutions**
2. **Be deterministic** (same input → same fitness)
3. **Provide gradients** (small changes → small fitness changes)
4. **Be efficient** (evaluated many times)

### Example Fitness Functions

```csharp
// Target-based fitness
public class TargetFitnessFunction : IFitnessFunction<double>
{
    private readonly double target;
    
    public async Task<double> EvaluateAsync(IChromosome<double> chromosome)
    {
        double value = chromosome.Genes.First();
        double result = SimulateStep(value);
        return -Math.Abs(result - target); // Negative distance
    }
}

// Test-based fitness
public class TestPassFitnessFunction : IFitnessFunction<Config>
{
    private readonly IEnumerable<TestCase> tests;
    
    public async Task<double> EvaluateAsync(IChromosome<Config> chromosome)
    {
        var config = chromosome.Genes.First();
        int passed = 0;
        
        foreach (var test in tests)
        {
            var result = await RunWithConfig(config, test.Input);
            if (result == test.Expected) passed++;
        }
        
        return passed; // More passing tests = higher fitness
    }
}

// Composite fitness
public class CompositeFitnessFunction : IFitnessFunction<Params>
{
    public async Task<double> EvaluateAsync(IChromosome<Params> chromosome)
    {
        var p = chromosome.Genes.First();
        
        double accuracy = await MeasureAccuracy(p);
        double speed = await MeasureSpeed(p);
        double cost = await MeasureCost(p);
        
        // Weighted combination
        return 0.5 * accuracy + 0.3 * speed - 0.2 * cost;
    }
}
```

## Best Practices

### 1. Gene Representation

Choose gene types that are easy to mutate and combine:

```csharp
// ✅ Good: Simple numeric parameters
Chromosome<double>

// ✅ Good: Configuration objects
Chromosome<ConfigStruct>

// ⚠️ Caution: Complex nested objects (harder to mutate meaningfully)
Chromosome<ComplexNestedConfig>
```

### 2. Mutation Functions

Design mutations that explore the solution space effectively:

```csharp
// ✅ Good: Bounded random adjustments
mutateGene: value => Math.Clamp(value + Random.Shared.NextDouble() * 0.2 - 0.1, 0, 1)

// ⚠️ Caution: Unbounded mutations
mutateGene: value => value * Random.Shared.NextDouble() * 100

// ✅ Good: Type-appropriate mutations
mutateGene: (int value) => value + Random.Shared.Next(-5, 6)
```

### 3. Monitoring Evolution

Use `EvolveWithMetadata` to track progress:

```csharp
var result = await step.EvolveWithMetadata(...);

if (result.IsSuccess)
{
    var (bestChromosome, output) = result.Value;
    
    Console.WriteLine($"Best Fitness: {bestChromosome.Fitness}");
    Console.WriteLine($"Best Configuration: {bestChromosome.Genes.First()}");
}
```

### 4. Reproducibility

Use seeds for debugging and testing:

```csharp
var ga = new GeneticAlgorithm<T>(
    fitnessFunction,
    mutateGene,
    seed: 42); // Reproducible results
```

## Performance Considerations

### Parallelization

Fitness evaluation is the bottleneck. Consider parallel evaluation:

```csharp
public async Task<Population<T>> EvaluateParallelAsync(Population<T> population)
{
    var evaluationTasks = population.Chromosomes
        .Select(async c =>
        {
            var fitness = await fitnessFunction.EvaluateAsync(c);
            return c.WithFitness(fitness);
        });
    
    var evaluated = await Task.WhenAll(evaluationTasks);
    return new Population<T>(evaluated);
}
```

### Early Stopping

Stop when fitness plateaus:

```csharp
double previousBest = double.NegativeInfinity;
int noImprovementCount = 0;

for (int gen = 0; gen < maxGenerations; gen++)
{
    population = await EvolveGeneration(population);
    double currentBest = population.BestChromosome.Fitness;
    
    if (Math.Abs(currentBest - previousBest) < 0.001)
    {
        noImprovementCount++;
        if (noImprovementCount >= 10) break; // Early stop
    }
    else
    {
        noImprovementCount = 0;
    }
    
    previousBest = currentBest;
}
```

## Testing

The module includes comprehensive tests (49 tests, 100% passing):

```bash
dotnet test --filter "FullyQualifiedName~Genetic"
```

Test coverage includes:
- Chromosome immutability and operations
- Population management
- Selection, crossover, and mutation operators
- Full genetic algorithm evolution
- Pipeline integration and fluent API

## Examples

See `src/Ouroboros.Examples/Examples/GeneticOptimizationExample.cs` for complete working examples:

```bash
# Run all examples
cd src/Ouroboros.Examples
dotnet run
```

## Integration with Existing Pipelines

The Genetic module integrates seamlessly with existing Ouroboros pipelines:

```csharp
using LangChainPipeline.Core.Steps;
using LangChainPipeline.Genetic.Extensions;

// Existing pipeline
var pipeline = Step.Pure<string>()
    .Bind(PreprocessInput)
    .Bind(/* Evolved step here */)
    .Map(FormatOutput);

// Add evolution to optimize a specific step
var optimizedPipeline = Step.Pure<string>()
    .Bind(PreprocessInput)
    .Bind(async input =>
    {
        var evolvedStep = GeneticPipelineExtensions.Identity<string>()
            .Evolve(stepFactory, fitnessFunction, mutateGene, initialPop, 50);
        return await evolvedStep(input);
    })
    .Map(FormatOutput);
```

## Future Enhancements

Potential areas for extension:

1. **Additional Selection Methods**: Tournament selection, rank selection
2. **Adaptive Parameters**: Dynamic mutation/crossover rates
3. **Multi-Objective Optimization**: Pareto frontier optimization
4. **Island Model**: Parallel sub-populations with migration
5. **Hybrid Approaches**: Combine with gradient descent or reinforcement learning

## References

- Holland, J. H. (1992). Adaptation in Natural and Artificial Systems
- Goldberg, D. E. (1989). Genetic Algorithms in Search, Optimization, and Machine Learning
- Mitchell, M. (1998). An Introduction to Genetic Algorithms
