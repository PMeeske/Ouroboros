// <copyright file="ProgramSynthesisBenchmarks.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

#pragma warning disable IDE0008 // Use explicit type
#pragma warning disable CA2007 // ConfigureAwait
#pragma warning disable SA1101 // Prefix local calls with this
#pragma warning disable SA1600 // Elements should be documented

using BenchmarkDotNet.Attributes;
using Ouroboros.Core.Monads;
using Ouroboros.Core.Synthesis;

namespace Ouroboros.Benchmarks;

/// <summary>
/// Benchmarks for program synthesis performance.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 5)]
public class ProgramSynthesisBenchmarks
{
    private ProgramSynthesisEngine smallBeamEngine = null!;
    private ProgramSynthesisEngine mediumBeamEngine = null!;
    private ProgramSynthesisEngine largeBeamEngine = null!;
    private DomainSpecificLanguage dsl = null!;
    private List<InputOutputExample> simpleExamples = null!;
    private List<InputOutputExample> complexExamples = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.smallBeamEngine = new ProgramSynthesisEngine(beamWidth: 10, maxDepth: 3);
        this.mediumBeamEngine = new ProgramSynthesisEngine(beamWidth: 50, maxDepth: 5);
        this.largeBeamEngine = new ProgramSynthesisEngine(beamWidth: 100, maxDepth: 7);

        this.dsl = CreateArithmeticDSL();

        this.simpleExamples = new List<InputOutputExample>
        {
            new InputOutputExample(1, 2),
            new InputOutputExample(2, 4),
        };

        this.complexExamples = new List<InputOutputExample>
        {
            new InputOutputExample(1, 2),
            new InputOutputExample(2, 4),
            new InputOutputExample(3, 6),
            new InputOutputExample(4, 8),
            new InputOutputExample(5, 10),
        };
    }

    [Benchmark(Baseline = true)]
    public async Task SmallBeam_SimpleExamples()
    {
        await this.smallBeamEngine.SynthesizeProgramAsync(
            this.simpleExamples,
            this.dsl,
            TimeSpan.FromSeconds(5));
    }

    [Benchmark]
    public async Task MediumBeam_SimpleExamples()
    {
        await this.mediumBeamEngine.SynthesizeProgramAsync(
            this.simpleExamples,
            this.dsl,
            TimeSpan.FromSeconds(5));
    }

    [Benchmark]
    public async Task LargeBeam_SimpleExamples()
    {
        await this.largeBeamEngine.SynthesizeProgramAsync(
            this.simpleExamples,
            this.dsl,
            TimeSpan.FromSeconds(5));
    }

    [Benchmark]
    public async Task SmallBeam_ComplexExamples()
    {
        await this.smallBeamEngine.SynthesizeProgramAsync(
            this.complexExamples,
            this.dsl,
            TimeSpan.FromSeconds(10));
    }

    [Benchmark]
    public async Task MediumBeam_ComplexExamples()
    {
        await this.mediumBeamEngine.SynthesizeProgramAsync(
            this.complexExamples,
            this.dsl,
            TimeSpan.FromSeconds(10));
    }

    [Benchmark]
    public async Task LibraryLearning_AntiUnification()
    {
        var programs = await CreateSamplePrograms();
        await this.smallBeamEngine.ExtractReusablePrimitivesAsync(
            programs,
            CompressionStrategy.AntiUnification);
    }

    [Benchmark]
    public async Task TrainRecognitionModel()
    {
        var task = new SynthesisTask(
            "Double input",
            this.simpleExamples,
            this.dsl);
        var program = CreateSampleProgram();
        var pairs = new List<(SynthesisTask Task, Ouroboros.Core.Synthesis.Program Solution)> { (task, program) };

        await this.smallBeamEngine.TrainRecognitionModelAsync(pairs);
    }

    [Benchmark]
    public async Task DSLEvolution()
    {
        var stats = new UsageStatistics(
            new Dictionary<string, int> { { "double", 10 } },
            new Dictionary<string, double> { { "double", 0.8 } },
            20);

        var newPrims = new List<Primitive>
        {
            new Primitive("triple", "int -> int", args => (int)args[0] * 3, -1.0),
        };

        await this.smallBeamEngine.EvolveDSLAsync(this.dsl, newPrims, stats);
    }

    [Benchmark]
    public void MeTTaConversion()
    {
        var program = this.CreateSampleProgram();
        MeTTaDSLBridge.ProgramToMeTTa(program);
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

    private Ouroboros.Core.Synthesis.Program CreateSampleProgram()
    {
        var node = new ASTNode("Primitive", "double", new List<ASTNode>());
        var ast = new AbstractSyntaxTree(node, 1, 1);
        return new Ouroboros.Core.Synthesis.Program("double", ast, this.dsl, -1.0, null);
    }

    private async Task<List<Ouroboros.Core.Synthesis.Program>> CreateSamplePrograms()
    {
        var programs = new List<Ouroboros.Core.Synthesis.Program>
        {
            this.CreateSampleProgram(),
            this.CreateSampleProgram(),
        };
        await Task.CompletedTask;
        return programs;
    }
}

/// <summary>
/// Benchmarks for specific synthesis algorithms.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 1, iterationCount: 3)]
public class SynthesisAlgorithmBenchmarks
{
    private ProgramSynthesisEngine engine = null!;
    private List<Ouroboros.Core.Synthesis.Program> programs = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.engine = new ProgramSynthesisEngine();
        this.programs = new List<Ouroboros.Core.Synthesis.Program>
        {
            CreateProgram("prog1"),
            CreateProgram("prog2"),
            CreateProgram("prog3"),
        };
    }

    [Benchmark]
    public async Task AntiUnification()
    {
        await this.engine.ExtractReusablePrimitivesAsync(
            this.programs,
            CompressionStrategy.AntiUnification);
    }

    [Benchmark]
    public async Task EGraph()
    {
        await this.engine.ExtractReusablePrimitivesAsync(
            this.programs,
            CompressionStrategy.EGraph);
    }

    [Benchmark]
    public async Task FragmentGrammar()
    {
        await this.engine.ExtractReusablePrimitivesAsync(
            this.programs,
            CompressionStrategy.FragmentGrammar);
    }

    private static Ouroboros.Core.Synthesis.Program CreateProgram(string name)
    {
        var node = new ASTNode("Primitive", name, new List<ASTNode>());
        var ast = new AbstractSyntaxTree(node, 1, 1);
        var dsl = new DomainSpecificLanguage("Test", new List<Primitive>(), new List<TypeRule>(), new List<RewriteRule>());
        return new Ouroboros.Core.Synthesis.Program(name, ast, dsl, -1.0, null);
    }
}
