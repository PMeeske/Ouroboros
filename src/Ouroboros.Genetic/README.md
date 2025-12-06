# Ouroboros.Genetic

Evolutionary optimization module for Ouroboros monadic pipelines using genetic algorithms.

## Overview

This module enables evolutionary optimization of pipeline parameters, configurations, and prompt variations through genetic algorithms. It treats pipeline optimization as a natural selection problem where the best solutions "survive" and "reproduce" across generations.

## Key Features

- **Type-Safe**: Generic implementations work with any gene type
- **Monadic**: Integrates seamlessly with Ouroboros' monadic pipeline architecture  
- **Functional**: Immutable data structures and pure operations
- **Extensible**: Easy to customize selection, crossover, and mutation strategies
- **Well-Tested**: 49 comprehensive tests with 100% pass rate

## Quick Start

```csharp
using LangChainPipeline.Genetic.Core;
using LangChainPipeline.Genetic.Extensions;

// 1. Define a fitness function
var fitnessFunction = new MyFitnessFunction();

// 2. Create a step factory from genes
Func<MyGene, Step<Input, Output>> stepFactory = gene =>
    input => Task.FromResult(ProcessWithGene(input, gene));

// 3. Define mutation
Func<MyGene, MyGene> mutateGene = gene => MutateGene(gene);

// 4. Create initial population
var initialPopulation = new List<IChromosome<MyGene>>
{
    new Chromosome<MyGene>(new List<MyGene> { gene1 }),
    new Chromosome<MyGene>(new List<MyGene> { gene2 }),
    // ... more chromosomes
};

// 5. Evolve!
var evolvedStep = GeneticPipelineExtensions.Identity<Input>()
    .Evolve(
        stepFactory,
        fitnessFunction,
        mutateGene,
        initialPopulation,
        generations: 50);

var result = await evolvedStep(myInput);
```

## Project Structure

```
Ouroboros.Genetic/
├── Abstractions/           # Core interfaces
│   ├── IChromosome.cs
│   ├── IFitnessFunction.cs
│   └── IGeneticAlgorithm.cs
├── Core/                   # Genetic algorithm components
│   ├── Chromosome.cs
│   ├── Population.cs
│   ├── GeneticAlgorithm.cs
│   ├── RouletteWheelSelection.cs
│   ├── UniformCrossover.cs
│   └── Mutation.cs
├── Steps/                  # Pipeline step wrappers
│   └── GeneticEvolutionStep.cs
└── Extensions/             # Fluent API
    └── GeneticPipelineExtensions.cs
```

## Core Concepts

### Chromosome
Represents a potential solution with genes and fitness score:
```csharp
var chromosome = new Chromosome<int>(new List<int> { 1, 2, 3 }, fitness: 42.0);
```

### Population
Manages a collection of chromosomes:
```csharp
var population = new Population<int>(chromosomes);
var best = population.BestChromosome;
var avg = population.AverageFitness;
```

### Genetic Algorithm
Orchestrates evolution through selection, crossover, mutation, and elitism:
```csharp
var ga = new GeneticAlgorithm<T>(
    fitnessFunction,
    mutateGene,
    mutationRate: 0.01,
    crossoverRate: 0.8,
    elitismRate: 0.1,
    seed: 42);

var result = await ga.EvolveAsync(initialPopulation, generations: 100);
```

### Pipeline Integration
Fluent API for evolving pipeline steps:
```csharp
// Basic evolution
var step = Identity<T>().Evolve(...);

// Evolution with metadata
var step = Identity<T>().EvolveWithMetadata(...);
```

## Configuration

Key parameters for tuning evolution:

- **`mutationRate`** (0.01): Probability of gene mutation
- **`crossoverRate`** (0.8): Probability of crossover
- **`elitismRate`** (0.1): Proportion of elite preservation
- **`generations`**: Number of evolution cycles
- **`seed`**: Random seed for reproducibility

## Examples

See `/docs/GENETIC_ALGORITHM_MODULE.md` for comprehensive documentation and examples:

- Optimizing numerical parameters
- Evolving LLM configurations  
- Multi-parameter optimization
- Custom fitness functions

Run the examples:
```bash
cd ../Ouroboros.Examples
dotnet run
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
