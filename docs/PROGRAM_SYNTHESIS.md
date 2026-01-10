# Program Synthesis Engine (F2.1)

## Overview

The Program Synthesis Engine is a neural-guided program synthesis system with library learning capabilities, implementing a DreamCoder-style wake-sleep algorithm. It synthesizes programs from input-output examples using enumerative search guided by a neural recognition model.

## Features

- **Enumerative Search**: Beam search algorithm with configurable width and depth
- **Neural Guidance**: Recognition model training for guiding synthesis
- **Library Learning**: Anti-unification based extraction of reusable primitives
- **DSL Evolution**: Usage statistics-based primitive priority adjustment
- **MeTTa Integration**: Bidirectional conversion between AST and MeTTa symbolic representation
- **Functional Programming**: Full Result<T,E> monad integration, immutable data structures
- **Type Safety**: Leverages C# 14.0 type system with nullable reference types

## Location

- **Core Implementation**: `src/Ouroboros.Core/Synthesis/`
- **Tests**: `src/Ouroboros.Tests/Tests/Synthesis/`
- **Integration Tests**: `src/Ouroboros.Tests/IntegrationTests/Synthesis/`
- **Examples**: `src/Ouroboros.Examples/Examples/ProgramSynthesisExample.cs`
- **Benchmarks**: `src/Ouroboros.Benchmarks/ProgramSynthesisBenchmarks.cs`

## Core Types

### IProgramSynthesisEngine

Main interface for the synthesis engine with four primary operations:

1. **SynthesizeProgramAsync**: Synthesizes programs from input-output examples
2. **ExtractReusablePrimitivesAsync**: Extracts common patterns from successful programs
3. **TrainRecognitionModelAsync**: Trains the neural model (wake-sleep algorithm)
4. **EvolveDSLAsync**: Evolves DSL based on usage statistics

### Domain-Specific Language (DSL)

A DSL consists of:
- **Primitives**: Basic operations with implementations and type signatures
- **Type Rules**: Constraints governing type composition
- **Rewrite Rules**: AST optimization patterns

### Program

Represents a synthesized program with:
- Source code representation
- Abstract syntax tree (AST)
- Log probability under the learned model
- Optional execution trace

## Usage Example

```csharp
using Ouroboros.Core.Synthesis;

// Create a simple arithmetic DSL
var primitives = new List<Primitive>
{
    new Primitive("double", "int -> int", args => (int)args[0] * 2, -1.0),
    new Primitive("add", "int -> int -> int", args => (int)args[0] + (int)args[1], -1.5),
};

var dsl = new DomainSpecificLanguage("Arithmetic", primitives, 
    new List<TypeRule>(), new List<RewriteRule>());

// Define input-output examples
var examples = new List<InputOutputExample>
{
    new InputOutputExample(2, 4),
    new InputOutputExample(3, 6),
    new InputOutputExample(5, 10),
};

// Create synthesis engine
var engine = new ProgramSynthesisEngine(beamWidth: 50, maxDepth: 5);

// Synthesize program
var result = await engine.SynthesizeProgramAsync(examples, dsl, TimeSpan.FromSeconds(30));

result.Match(
    program => Console.WriteLine($"Synthesized: {program.SourceCode}"),
    error => Console.WriteLine($"Failed: {error}"));
```

## Library Learning

Extract reusable primitives from successful programs:

```csharp
var successfulPrograms = new List<Program> { /* ... */ };

var extractionResult = await engine.ExtractReusablePrimitivesAsync(
    successfulPrograms,
    CompressionStrategy.AntiUnification);

extractionResult.Match(
    primitives => Console.WriteLine($"Extracted {primitives.Count} new primitives"),
    error => Console.WriteLine($"Extraction failed: {error}"));
```

## DSL Evolution

Evolve the DSL based on usage statistics:

```csharp
var stats = new UsageStatistics(
    new Dictionary<string, int> { { "double", 50 } },
    new Dictionary<string, double> { { "double", 0.9 } },
    100);

var newPrimitives = new List<Primitive>
{
    new Primitive("triple", "int -> int", args => (int)args[0] * 3, -1.0),
};

var evolvedDSL = await engine.EvolveDSLAsync(currentDSL, newPrimitives, stats);
```

## MeTTa Integration

Convert programs to MeTTa symbolic representation:

```csharp
var mettaResult = MeTTaDSLBridge.ProgramToMeTTa(program);
var dslAtoms = MeTTaDSLBridge.DSLToMeTTa(dsl);

// Convert back from MeTTa
var astResult = MeTTaDSLBridge.MeTTaToAST(mettaAtom);
```

## Compression Strategies

Three compression strategies are supported:

1. **AntiUnification**: Finds most specific generalization of program pairs
2. **EGraph**: E-graph based compression (placeholder)
3. **FragmentGrammar**: Grammar-based fragment extraction (placeholder)

## Testing

### Unit Tests (44 tests)
- Unit type tests
- Synthesis type tests
- Program synthesis engine tests
- MeTTa bridge tests

### Integration Tests (6 tests)
- End-to-end synthesis
- Library learning workflow
- Wake-sleep learning cycle
- DSL evolution
- MeTTa integration
- Performance testing

Run tests:
```bash
dotnet test --filter "FullyQualifiedName~Synthesis"
```

## Performance

The engine supports configurable parameters for performance tuning:

- **beamWidth**: Number of programs to maintain at each depth (default: 100)
- **maxDepth**: Maximum AST depth to explore (default: 10)
- **temperature**: Temperature for probabilistic sampling (default: 1.0)

## Architecture

The implementation follows functional programming principles:

1. **Result Monad**: All fallible operations return `Result<T, E>`
2. **Immutable Types**: All data structures are immutable records
3. **Pure Functions**: Operations avoid side effects where possible
4. **Monadic Composition**: Operations compose using bind/map operations

## Future Enhancements

- Complete E-graph based compression
- Fragment grammar extraction
- Deep learning-based neural guidance
- Type inference improvements
- Program simplification/optimization
- Multi-objective optimization

## API Documentation

Full XML documentation is provided for all public APIs. Use IntelliSense in Visual Studio or generate documentation with DocFX.

## Examples

See `ProgramSynthesisExample.cs` for four demonstration scenarios:
1. Basic synthesis from examples
2. Library learning workflow
3. DSL evolution with statistics
4. Recognition model training

Run examples:
```csharp
await ProgramSynthesisExample.RunBasicSynthesisExample();
await ProgramSynthesisExample.RunLibraryLearningExample();
await ProgramSynthesisExample.RunDSLEvolutionExample();
await ProgramSynthesisExample.RunRecognitionTrainingExample();
```

## Contributing

When extending the synthesis engine:

1. Follow existing functional programming patterns
2. Use Result<T,E> for error handling
3. Keep types immutable (records/readonly structs)
4. Add comprehensive unit and integration tests
5. Document all public APIs with XML comments
6. Update this README with new features

## License

See LICENSE file in the repository root.
