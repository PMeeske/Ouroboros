// <copyright file="ProgramSynthesisExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Core.Synthesis;

namespace Ouroboros.Examples;

/// <summary>
/// Demonstrates program synthesis capabilities with the ProgramSynthesisEngine.
/// Shows how to define DSLs, provide examples, and synthesize programs.
/// </summary>
public static class ProgramSynthesisExample
{
    /// <summary>
    /// Demonstrates basic program synthesis from input-output examples.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunBasicSynthesisExample()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║   Program Synthesis - Basic Example                 ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝\n");

        // Create a simple arithmetic DSL
        var dsl = CreateArithmeticDSL();

        Console.WriteLine($"DSL: {dsl.Name}");
        Console.WriteLine($"Primitives: {dsl.Primitives.Count}");
        foreach (var prim in dsl.Primitives)
        {
            Console.WriteLine($"  • {prim.Name}: {prim.Type}");
        }

        Console.WriteLine("\nInput-Output Examples:");
        var examples = new List<InputOutputExample>
        {
            new InputOutputExample(2, 4),
            new InputOutputExample(3, 6),
            new InputOutputExample(5, 10),
        };

        foreach (var example in examples)
        {
            Console.WriteLine($"  {example.Input} → {example.ExpectedOutput}");
        }

        // Create synthesis engine
        var engine = new ProgramSynthesisEngine(beamWidth: 50, maxDepth: 5);

        Console.WriteLine("\nSynthesizing program...");
        var result = await engine.SynthesizeProgramAsync(examples, dsl, TimeSpan.FromSeconds(30));

        result.Match(
            program =>
            {
                Console.WriteLine($"✓ Synthesis succeeded!");
                Console.WriteLine($"  Source: {program.SourceCode}");
                Console.WriteLine($"  Log Probability: {program.LogProbability:F3}");
                Console.WriteLine($"  AST Depth: {program.AST.Depth}");
                Console.WriteLine($"  Node Count: {program.AST.NodeCount}");
            },
            error =>
            {
                Console.WriteLine($"✗ Synthesis failed: {error}");
            });

        Console.WriteLine("\n✓ Basic synthesis example completed!\n");
    }

    /// <summary>
    /// Demonstrates library learning through primitive extraction.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunLibraryLearningExample()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║   Library Learning Example                          ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝\n");

        var dsl = CreateArithmeticDSL();
        var engine = new ProgramSynthesisEngine(beamWidth: 30, maxDepth: 4);

        // Synthesize multiple programs
        Console.WriteLine("Synthesizing multiple programs...");
        var tasks = new List<List<InputOutputExample>>
        {
            new List<InputOutputExample> { new(1, 2), new(2, 4), new(3, 6) },
            new List<InputOutputExample> { new(1, 3), new(2, 6), new(3, 9) },
            new List<InputOutputExample> { new(1, 1), new(2, 4), new(3, 9) },
        };

        var successfulPrograms = new List<Program>();
        foreach (var examples in tasks)
        {
            var result = await engine.SynthesizeProgramAsync(examples, dsl, TimeSpan.FromSeconds(10));
            if (result.IsSuccess)
            {
                successfulPrograms.Add(result.Value);
                Console.WriteLine($"  ✓ Synthesized: {result.Value.SourceCode}");
            }
        }

        if (successfulPrograms.Count > 0)
        {
            Console.WriteLine($"\nExtracting reusable primitives from {successfulPrograms.Count} programs...");
            var extractionResult = await engine.ExtractReusablePrimitivesAsync(
                successfulPrograms,
                CompressionStrategy.AntiUnification);

            extractionResult.Match(
                primitives =>
                {
                    Console.WriteLine($"✓ Extracted {primitives.Count} new primitives:");
                    foreach (var prim in primitives)
                    {
                        Console.WriteLine($"  • {prim.Name}: {prim.Type} (prior: {prim.LogPrior:F3})");
                    }
                },
                error =>
                {
                    Console.WriteLine($"✗ Extraction failed: {error}");
                });
        }

        Console.WriteLine("\n✓ Library learning example completed!\n");
    }

    /// <summary>
    /// Demonstrates DSL evolution with usage statistics.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunDSLEvolutionExample()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║   DSL Evolution Example                             ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝\n");

        var engine = new ProgramSynthesisEngine();
        var dsl = CreateArithmeticDSL();

        Console.WriteLine($"Initial DSL: {dsl.Primitives.Count} primitives");

        // Simulate usage statistics
        var stats = new UsageStatistics(
            new Dictionary<string, int>
            {
                { "double", 50 },
                { "add", 30 },
                { "identity", 5 },
            },
            new Dictionary<string, double>
            {
                { "double", 0.9 },
                { "add", 0.7 },
                { "identity", 0.3 },
            },
            100);

        Console.WriteLine("\nUsage Statistics:");
        foreach (var (name, count) in stats.PrimitiveUseCounts)
        {
            var successRate = stats.PrimitiveSuccessRates[name];
            Console.WriteLine($"  • {name}: used {count} times, {successRate:P0} success rate");
        }

        // Define new learned primitives
        var newPrimitives = new List<Primitive>
        {
            new Primitive("triple", "int -> int", args => (int)args[0] * 3, -1.0),
            new Primitive("quadruple", "int -> int", args => (int)args[0] * 4, -1.0),
        };

        Console.WriteLine($"\nAdding {newPrimitives.Count} new primitives...");

        // Evolve DSL
        var result = await engine.EvolveDSLAsync(dsl, newPrimitives, stats);

        result.Match(
            evolvedDSL =>
            {
                Console.WriteLine($"✓ DSL evolved successfully!");
                Console.WriteLine($"  New primitive count: {evolvedDSL.Primitives.Count}");
                Console.WriteLine($"  Primitives with adjusted priors:");
                foreach (var prim in evolvedDSL.Primitives.OrderByDescending(p => p.LogPrior).Take(5))
                {
                    Console.WriteLine($"    • {prim.Name}: {prim.LogPrior:F3}");
                }
            },
            error =>
            {
                Console.WriteLine($"✗ Evolution failed: {error}");
            });

        Console.WriteLine("\n✓ DSL evolution example completed!\n");
    }

    /// <summary>
    /// Demonstrates training the recognition model (wake-sleep algorithm).
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunRecognitionTrainingExample()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║   Recognition Model Training Example                ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝\n");

        var engine = new ProgramSynthesisEngine();
        var dsl = CreateArithmeticDSL();

        // Create task-solution pairs for training
        var trainingPairs = new List<(SynthesisTask, Program)>();

        var task1 = new SynthesisTask(
            "Double the input",
            new List<InputOutputExample> { new(1, 2), new(2, 4) },
            dsl);
        var program1 = CreateSampleProgram("double", dsl);
        trainingPairs.Add((task1, program1));

        var task2 = new SynthesisTask(
            "Add one",
            new List<InputOutputExample> { new(1, 2), new(3, 4) },
            dsl);
        var program2 = CreateSampleProgram("(add 1)", dsl);
        trainingPairs.Add((task2, program2));

        Console.WriteLine($"Training pairs: {trainingPairs.Count}");
        foreach (var (task, program) in trainingPairs)
        {
            Console.WriteLine($"  • {task.Description} → {program.SourceCode}");
        }

        Console.WriteLine("\nTraining recognition model...");
        var result = await engine.TrainRecognitionModelAsync(trainingPairs);

        result.Match(
            _ =>
            {
                Console.WriteLine("✓ Training completed successfully!");
                Console.WriteLine("  Model can now guide synthesis with learned patterns");
            },
            error =>
            {
                Console.WriteLine($"✗ Training failed: {error}");
            });

        Console.WriteLine("\n✓ Recognition training example completed!\n");
    }

    private static DomainSpecificLanguage CreateArithmeticDSL()
    {
        var primitives = new List<Primitive>
        {
            new Primitive(
                "identity",
                "int -> int",
                args => args[0],
                -0.5),
            new Primitive(
                "double",
                "int -> int",
                args => (int)args[0] * 2,
                -1.0),
            new Primitive(
                "add",
                "int -> int -> int",
                args => (int)args[0] + (int)args[1],
                -1.5),
            new Primitive(
                "const",
                "int -> int -> int",
                args => args[0],
                -2.0),
        };

        var typeRules = new List<TypeRule>
        {
            new TypeRule("Identity", new List<string> { "int" }, "int"),
            new TypeRule("Double", new List<string> { "int" }, "int"),
            new TypeRule("Add", new List<string> { "int", "int" }, "int"),
        };

        return new DomainSpecificLanguage("Arithmetic", primitives, typeRules, new List<RewriteRule>());
    }

    private static Program CreateSampleProgram(string sourceCode, DomainSpecificLanguage dsl)
    {
        var node = new ASTNode("Primitive", sourceCode, new List<ASTNode>());
        var ast = new AbstractSyntaxTree(node, 1, 1);
        return new Program(sourceCode, ast, dsl, -1.0);
    }
}
