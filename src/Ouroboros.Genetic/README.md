# Ouroboros.Genetic

A functional programming-based genetic algorithm module for the Ouroboros pipeline system. This module enables evolutionary optimization of agents and pipeline steps using monadic composition patterns.

## Overview

Ouroboros.Genetic provides primitives for implementing genetic algorithms that integrate seamlessly with the existing `Step<TIn, TOut>` architecture. All operations follow functional programming principles with:

- **Monadic Error Handling**: Uses `Result<T>` monad (no exceptions)
- **Immutability**: All data structures are immutable
- **Composability**: Integrates via Kleisli arrow composition
- **Type Safety**: Leverages C# type system for compile-time guarantees

## Core Abstractions

### IChromosome
Interface for an evolving solution (e.g., prompts, parameters, configurations):
```csharp
public interface IChromosome
{
    string Id { get; }
    double Fitness { get; }
    int Generation { get; }
    IChromosome Clone();
    IChromosome WithFitness(double fitness);
}
```

### IFitnessFunction<T>
Interface for evaluating chromosomes:
```csharp
public interface IFitnessFunction<TChromosome>
{
    Task<Result<double>> EvaluateAsync(TChromosome chromosome);
}
```

### IEvolutionEngine
Interface for the main evolution loop:
```csharp
public interface IEvolutionEngine<TChromosome>
{
    Task<Result<Population<TChromosome>>> EvolveAsync(
        Population<TChromosome> initialPopulation,
        int generations);
    
    Option<TChromosome> GetBest(Population<TChromosome> population);
}
```

## Core Implementations

- **Population**: Manages collections of chromosomes with immutable operations
- **RouletteWheelSelection**: Fitness-proportionate selection strategy
- **UniformCrossover**: Standard crossover with configurable rate
- **Mutation**: Random mutation with configurable rate
- **EvolutionEngine**: Main orchestrator implementing the complete GA loop

## Fluent API Usage

### Basic Evolution
```csharp
Step<int, Population<MyChromosome>> createPopulation = size => 
    Task.FromResult(new Population<MyChromosome>(/* ... */));

var pipeline = createPopulation.Evolve(evolutionEngine, generations: 50);
var result = await pipeline(populationSize);
```

### Evolution with Custom Parameters
```csharp
var pipeline = createPopulation.EvolveWith(
    fitnessFunction,
    crossoverFunc,
    mutationFunc,
    generations: 100,
    crossoverRate: 0.8,
    mutationRate: 0.1,
    elitismRate: 0.15);
```

### Complete Pipeline Example
```csharp
// Define fitness function
var fitnessFunction = new MyFitnessFunction(targetValue);

// Define genetic operators
Func<MyChromosome, MyChromosome, double, Result<MyChromosome>> crossoverFunc =
    (p1, p2, ratio) => /* crossover logic */;

Func<MyChromosome, Random, Result<MyChromosome>> mutationFunc =
    (c, random) => /* mutation logic */;

// Create engine
var engine = new EvolutionEngine<MyChromosome>(
    fitnessFunction,
    crossoverFunc,
    mutationFunc,
    crossoverRate: 0.8,
    mutationRate: 0.1,
    elitismRate: 0.1,
    seed: 42);

// Build and execute pipeline
var result = await Step.Pure<string>()
    .Map(int.Parse)
    .Then(size => CreatePopulation(size))
    .Evolve(engine, generations: 50)
    .MatchResult(
        chromosome => $"Best: {chromosome.Value}",
        error => $"Failed: {error}")("10");
```

## Extension Methods

### Evolve
Evolves a population and returns the best chromosome:
```csharp
Step<TIn, Result<TChromosome>> Evolve<TIn, TChromosome>(
    this Step<TIn, Population<TChromosome>> step,
    IEvolutionEngine<TChromosome> engine,
    int generations)
```

### EvolvePopulation
Evolves and returns the entire population:
```csharp
Step<TIn, Result<Population<TChromosome>>> EvolvePopulation<TIn, TChromosome>(
    this Step<TIn, Population<TChromosome>> step,
    IEvolutionEngine<TChromosome> engine,
    int generations)
```

### EvolveWith
Creates an engine and evolves in one step:
```csharp
Step<TIn, Result<TChromosome>> EvolveWith<TIn, TChromosome>(
    this Step<TIn, Population<TChromosome>> step,
    IFitnessFunction<TChromosome> fitnessFunction,
    Func<TChromosome, TChromosome, double, Result<TChromosome>> crossoverFunc,
    Func<TChromosome, Random, Result<TChromosome>> mutationFunc,
    int generations,
    double crossoverRate = 0.8,
    double mutationRate = 0.1,
    double elitismRate = 0.1)
```

### UnwrapOrDefault
Extracts value from Result with fallback:
```csharp
Step<TIn, TChromosome> UnwrapOrDefault<TIn, TChromosome>(
    this Step<TIn, Result<TChromosome>> step,
    TChromosome defaultValue)
```

### MatchResult
Pattern matches on Result:
```csharp
Step<TIn, TOut> MatchResult<TIn, TChromosome, TOut>(
    this Step<TIn, Result<TChromosome>> step,
    Func<TChromosome, TOut> onSuccess,
    Func<string, TOut> onFailure)
```

## Testing

```bash
cd ../Ouroboros.Tests
dotnet test --filter "FullyQualifiedName~Genetic"
```

All 49 tests pass, covering:
- Chromosome operations and immutability
- Population management
- Selection strategies
- Crossover operators
- Mutation operators
- Full GA evolution
- Pipeline integration

## Dependencies

- **Ouroboros.Core**: Core monadic abstractions and types
- **.NET 10.0**: Latest .NET runtime

## Contributing

When extending this module:

1. Maintain functional programming principles
2. Use immutable data structures
3. Follow existing naming conventions
4. Add comprehensive tests
5. Update documentation

## License

Copyright © 2025. See repository root for license information.
