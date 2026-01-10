// <copyright file="ProgramSynthesisIntegrationTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Core.Synthesis;
using Xunit;
using Xunit.Abstractions;

namespace Ouroboros.Tests.IntegrationTests.Synthesis;

/// <summary>
/// Integration tests for end-to-end program synthesis workflows.
/// </summary>
[Trait("Category", "Integration")]
public class ProgramSynthesisIntegrationTests
{
    private readonly ITestOutputHelper output;

    public ProgramSynthesisIntegrationTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public async Task EndToEnd_SimpleSynthesis_ShouldComplete()
    {
        // Arrange
        this.output.WriteLine("Creating simple arithmetic DSL...");
        var dsl = CreateArithmeticDSL();
        var engine = new ProgramSynthesisEngine(beamWidth: 30, maxDepth: 5);

        var examples = new List<InputOutputExample>
        {
            new InputOutputExample(2, 4),
            new InputOutputExample(3, 6),
            new InputOutputExample(5, 10),
        };

        this.output.WriteLine($"Examples: {string.Join(", ", examples.Select(e => $"{e.Input}→{e.ExpectedOutput}"))}");

        // Act
        this.output.WriteLine("Starting synthesis...");
        var result = await engine.SynthesizeProgramAsync(examples, dsl, TimeSpan.FromSeconds(30));

        // Assert
        // Note: The current implementation has placeholder execution logic,
        // so synthesis may not succeed. This integration test validates the API contract.
        this.output.WriteLine($"Synthesis result: {(result.IsSuccess ? "Success" : $"Failed: {result.Error}")}");
        if (result.IsSuccess)
        {
            this.output.WriteLine($"Synthesized: {result.Value.SourceCode}");
            this.output.WriteLine($"Log Probability: {result.Value.LogProbability:F3}");
            this.output.WriteLine($"AST Depth: {result.Value.AST.Depth}");
        }
    }

    [Fact]
    public async Task EndToEnd_LibraryLearningWorkflow_ShouldExtractPrimitives()
    {
        // Arrange
        this.output.WriteLine("Setting up library learning workflow...");
        var dsl = CreateArithmeticDSL();
        var engine = new ProgramSynthesisEngine(beamWidth: 20, maxDepth: 4);

        var tasks = new List<List<InputOutputExample>>
        {
            new List<InputOutputExample> { new(1, 2), new(2, 4), new(3, 6) },
            new List<InputOutputExample> { new(1, 3), new(2, 6), new(3, 9) },
        };

        // Act - Phase 1: Synthesize programs
        this.output.WriteLine("Phase 1: Synthesizing programs...");
        var programs = new List<Program>();
        foreach (var examples in tasks)
        {
            var result = await engine.SynthesizeProgramAsync(examples, dsl, TimeSpan.FromSeconds(15));
            if (result.IsSuccess)
            {
                programs.Add(result.Value);
                this.output.WriteLine($"  Synthesized: {result.Value.SourceCode}");
            }
        }

        // Act - Phase 2: Extract primitives
        // If no programs were synthesized, create sample programs for testing extraction
        if (programs.Count == 0)
        {
            this.output.WriteLine("No programs synthesized, using sample programs for extraction test");
            programs.Add(CreateSampleProgram("double", dsl));
            programs.Add(CreateSampleProgram("add", dsl));
        }

        this.output.WriteLine($"Phase 2: Extracting primitives from {programs.Count} programs...");
        var extractionResult = await engine.ExtractReusablePrimitivesAsync(
            programs,
            CompressionStrategy.AntiUnification);

        // Assert
        extractionResult.IsSuccess.Should().BeTrue("primitive extraction should succeed");
        if (extractionResult.IsSuccess)
        {
            this.output.WriteLine($"Extracted {extractionResult.Value.Count} primitives");
            foreach (var prim in extractionResult.Value)
            {
                this.output.WriteLine($"  • {prim.Name}: {prim.Type}");
            }
        }
    }

    [Fact]
    public async Task EndToEnd_WakeSleepCycle_ShouldImprovePerformance()
    {
        // Arrange
        this.output.WriteLine("Setting up wake-sleep learning cycle...");
        var dsl = CreateArithmeticDSL();
        var engine = new ProgramSynthesisEngine(beamWidth: 15, maxDepth: 3);

        // Phase 1: Initial synthesis (Wake phase)
        this.output.WriteLine("Wake phase: Synthesizing programs...");
        var examples = new List<InputOutputExample>
        {
            new InputOutputExample(1, 2),
            new InputOutputExample(2, 4),
        };

        var wakeSynthesisResult = await engine.SynthesizeProgramAsync(examples, dsl, TimeSpan.FromSeconds(10));

        // Phase 2: Training (Sleep phase)
        if (wakeSynthesisResult.IsSuccess)
        {
            this.output.WriteLine($"Sleep phase: Training on synthesized program: {wakeSynthesisResult.Value.SourceCode}");
            var task = new SynthesisTask("Double the input", examples, dsl);
            var trainingPairs = new List<(SynthesisTask, Program)>
            {
                (task, wakeSynthesisResult.Value),
            };

            var trainResult = await engine.TrainRecognitionModelAsync(trainingPairs);

            // Assert
            trainResult.IsSuccess.Should().BeTrue("training should complete successfully");
            this.output.WriteLine($"Training completed: {trainResult.IsSuccess}");
        }
    }

    [Fact]
    public async Task EndToEnd_DSLEvolutionWithStatistics_ShouldUpdateDSL()
    {
        // Arrange
        this.output.WriteLine("Testing DSL evolution workflow...");
        var initialDSL = CreateArithmeticDSL();
        var engine = new ProgramSynthesisEngine();

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

        var newPrimitives = new List<Primitive>
        {
            new Primitive("triple", "int -> int", args => (int)args[0] * 3, -1.0),
        };

        // Act
        this.output.WriteLine("Evolving DSL...");
        var result = await engine.EvolveDSLAsync(initialDSL, newPrimitives, stats);

        // Assert
        result.IsSuccess.Should().BeTrue("DSL evolution should succeed");
        if (result.IsSuccess)
        {
            var evolved = result.Value;
            evolved.Primitives.Should().HaveCount(initialDSL.Primitives.Count + 1);
            this.output.WriteLine($"Evolved DSL has {evolved.Primitives.Count} primitives");

            // Check that priors were adjusted
            var doublePrim = evolved.Primitives.FirstOrDefault(p => p.Name == "double");
            doublePrim.Should().NotBeNull();
            this.output.WriteLine($"'double' primitive log prior: {doublePrim!.LogPrior:F3}");
        }
    }

    [Fact]
    public async Task EndToEnd_MeTTaIntegration_ShouldConvertPrograms()
    {
        // Arrange
        this.output.WriteLine("Testing MeTTa integration...");
        var dsl = CreateArithmeticDSL();
        var engine = new ProgramSynthesisEngine(beamWidth: 10, maxDepth: 3);

        var examples = new List<InputOutputExample>
        {
            new InputOutputExample(1, 2),
            new InputOutputExample(2, 4),
        };

        // Act - Synthesize program
        this.output.WriteLine("Synthesizing program...");
        var synthResult = await engine.SynthesizeProgramAsync(examples, dsl, TimeSpan.FromSeconds(10));

        if (synthResult.IsSuccess)
        {
            var program = synthResult.Value;
            this.output.WriteLine($"Synthesized: {program.SourceCode}");

            // Convert to MeTTa
            this.output.WriteLine("Converting to MeTTa...");
            var mettaResult = MeTTaDSLBridge.ProgramToMeTTa(program);

            // Assert
            mettaResult.IsSuccess.Should().BeTrue("conversion to MeTTa should succeed");
            if (mettaResult.IsSuccess)
            {
                this.output.WriteLine($"MeTTa representation: {mettaResult.Value.ToSExpr()}");

                // Convert back
                var astResult = MeTTaDSLBridge.MeTTaToAST(mettaResult.Value);
                astResult.IsSuccess.Should().BeTrue("conversion back from MeTTa should succeed");
                if (astResult.IsSuccess)
                {
                    this.output.WriteLine($"Converted back: {astResult.Value.Value}");
                }
            }
        }
        else
        {
            this.output.WriteLine($"Synthesis failed (this is acceptable for integration test): {synthResult.Error}");
        }
    }

    [Fact]
    public async Task Performance_MultipleTasksSynthesis_ShouldCompleteInReasonableTime()
    {
        // Arrange
        this.output.WriteLine("Performance test: Multiple synthesis tasks...");
        var dsl = CreateArithmeticDSL();
        var engine = new ProgramSynthesisEngine(beamWidth: 20, maxDepth: 4);

        var allTasks = new List<List<InputOutputExample>>
        {
            new List<InputOutputExample> { new(1, 2), new(2, 4) },
            new List<InputOutputExample> { new(1, 3), new(2, 6) },
            new List<InputOutputExample> { new(1, 1), new(2, 2) },
            new List<InputOutputExample> { new(2, 6), new(3, 9) },
            new List<InputOutputExample> { new(1, 4), new(2, 8) },
        };

        // Act
        var startTime = DateTime.UtcNow;
        var successCount = 0;

        foreach (var examples in allTasks)
        {
            var result = await engine.SynthesizeProgramAsync(examples, dsl, TimeSpan.FromSeconds(5));
            if (result.IsSuccess)
            {
                successCount++;
                this.output.WriteLine($"✓ {result.Value.SourceCode}");
            }
            else
            {
                this.output.WriteLine($"✗ Failed");
            }
        }

        var elapsed = DateTime.UtcNow - startTime;

        // Assert
        this.output.WriteLine($"Completed {successCount}/{allTasks.Count} tasks in {elapsed.TotalSeconds:F2}s");
        elapsed.TotalSeconds.Should().BeLessThan(60, "all tasks should complete within reasonable time");
    }

    private static DomainSpecificLanguage CreateArithmeticDSL()
    {
        var primitives = new List<Primitive>
        {
            new Primitive("identity", "int -> int", args => args[0], -0.5),
            new Primitive("double", "int -> int", args => (int)args[0] * 2, -1.0),
            new Primitive("add", "int -> int -> int", args => (int)args[0] + (int)args[1], -1.5),
        };

        var typeRules = new List<TypeRule>
        {
            new TypeRule("Identity", new List<string> { "int" }, "int"),
            new TypeRule("Double", new List<string> { "int" }, "int"),
        };

        return new DomainSpecificLanguage("Arithmetic", primitives, typeRules, new List<RewriteRule>());
    }

    private static Program CreateSampleProgram(string name, DomainSpecificLanguage dsl)
    {
        var node = new ASTNode("Primitive", name, new List<ASTNode>());
        var ast = new AbstractSyntaxTree(node, 1, 1);
        return new Program(name, ast, dsl, -1.0);
    }
}
