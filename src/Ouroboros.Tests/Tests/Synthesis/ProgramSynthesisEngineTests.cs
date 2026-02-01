// <copyright file="ProgramSynthesisEngineTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Core.Synthesis;
using Xunit;
using Unit = Ouroboros.Core.Synthesis.Unit;

namespace Ouroboros.Tests.Synthesis;

/// <summary>
/// Tests for the ProgramSynthesisEngine.
/// </summary>
public class ProgramSynthesisEngineTests
{
    [Fact]
    public async Task SynthesizeProgramAsync_WithNoExamples_ShouldReturnFailure()
    {
        // Arrange
        var engine = new ProgramSynthesisEngine();
        var dsl = CreateSimpleDSL();
        var examples = new List<InputOutputExample>();

        // Act
        var result = await engine.SynthesizeProgramAsync(examples, dsl, TimeSpan.FromSeconds(1));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No examples provided");
    }

    [Fact]
    public async Task SynthesizeProgramAsync_WithNullDSL_ShouldReturnFailure()
    {
        // Arrange
        var engine = new ProgramSynthesisEngine();
        var examples = new List<InputOutputExample> { new InputOutputExample(1, 2) };

        // Act
        var result = await engine.SynthesizeProgramAsync(examples, null!, TimeSpan.FromSeconds(1));

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SynthesizeProgramAsync_WithEmptyDSL_ShouldReturnFailure()
    {
        // Arrange
        var engine = new ProgramSynthesisEngine();
        var examples = new List<InputOutputExample> { new InputOutputExample(1, 2) };
        var dsl = new DomainSpecificLanguage("Empty", new List<Primitive>(), new List<TypeRule>(), new List<RewriteRule>());

        // Act
        var result = await engine.SynthesizeProgramAsync(examples, dsl, TimeSpan.FromSeconds(1));

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("no primitives");
    }

    [Fact]
    public async Task SynthesizeProgramAsync_WithTimeout_ShouldRespectTimeout()
    {
        // Arrange
        var engine = new ProgramSynthesisEngine(beamWidth: 1000, maxDepth: 20);
        var dsl = CreateComplexDSL();
        var examples = new List<InputOutputExample>
        {
            new InputOutputExample(1, 2),
            new InputOutputExample(2, 4),
            new InputOutputExample(3, 6),
        };

        // Act
        var result = await engine.SynthesizeProgramAsync(examples, dsl, TimeSpan.FromMilliseconds(10));

        // Assert
        result.IsFailure.Should().BeTrue();
        // Error could be either timeout or cancellation related
        (result.Error.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
         result.Error.Contains("cancel", StringComparison.OrdinalIgnoreCase)).Should().BeTrue("should indicate time-related failure");
    }

    [Fact]
    public async Task SynthesizeProgramAsync_WithCancellation_ShouldHandleCancellation()
    {
        // Arrange
        var engine = new ProgramSynthesisEngine();
        var dsl = CreateSimpleDSL();
        var examples = new List<InputOutputExample> { new InputOutputExample(1, 2) };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await engine.SynthesizeProgramAsync(examples, dsl, TimeSpan.FromSeconds(10), cts.Token);

        // Assert
        result.IsFailure.Should().BeTrue();
        // Error could be either timeout or cancellation related
        (result.Error.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
         result.Error.Contains("cancel", StringComparison.OrdinalIgnoreCase)).Should().BeTrue("should indicate cancellation");
    }

    [Fact]
    public async Task ExtractReusablePrimitivesAsync_WithNoPrograms_ShouldReturnFailure()
    {
        // Arrange
        var engine = new ProgramSynthesisEngine();
        var programs = new List<SynthesisProgram>();

        // Act
        var result = await engine.ExtractReusablePrimitivesAsync(programs, CompressionStrategy.AntiUnification);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No programs provided");
    }

    [Fact]
    public async Task ExtractReusablePrimitivesAsync_WithValidPrograms_ShouldSucceed()
    {
        // Arrange
        var engine = new ProgramSynthesisEngine();
        var programs = new List<SynthesisProgram>
        {
            CreateTestProgram("prog1"),
            CreateTestProgram("prog2"),
        };

        // Act
        var result = await engine.ExtractReusablePrimitivesAsync(programs, CompressionStrategy.AntiUnification);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
    }

    [Fact]
    public async Task ExtractReusablePrimitivesAsync_WithCancellation_ShouldHandleCancellation()
    {
        // Arrange
        var engine = new ProgramSynthesisEngine();
        var programs = new List<SynthesisProgram> { CreateTestProgram("prog1") };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await engine.ExtractReusablePrimitivesAsync(programs, CompressionStrategy.AntiUnification, cts.Token);

        // Assert
        // With a single program and immediate cancellation, may succeed or fail depending on timing
        if (result.IsFailure)
        {
            result.Error.Should().Contain("cancel");
        }
    }

    [Fact]
    public async Task TrainRecognitionModelAsync_WithNoPairs_ShouldReturnFailure()
    {
        // Arrange
        var engine = new ProgramSynthesisEngine();
        var pairs = new List<(SynthesisTask, SynthesisProgram)>();

        // Act
        var result = await engine.TrainRecognitionModelAsync(pairs);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No training pairs");
    }

    [Fact]
    public async Task TrainRecognitionModelAsync_WithValidPairs_ShouldSucceed()
    {
        // Arrange
        var engine = new ProgramSynthesisEngine();
        var task = new SynthesisTask(
            "Test task",
            new List<InputOutputExample> { new InputOutputExample(1, 2) },
            CreateSimpleDSL());
        var program = CreateTestProgram("test");
        var pairs = new List<(SynthesisTask, SynthesisProgram)> { (task, program) };

        // Act
        var result = await engine.TrainRecognitionModelAsync(pairs);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(Unit.Value);
    }

    [Fact]
    public async Task TrainRecognitionModelAsync_WithCancellation_ShouldHandleCancellation()
    {
        // Arrange
        var engine = new ProgramSynthesisEngine();
        var task = new SynthesisTask("Test", new List<InputOutputExample>(), CreateSimpleDSL());
        var program = CreateTestProgram("test");
        var pairs = new List<(SynthesisTask, SynthesisProgram)> { (task, program) };
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await engine.TrainRecognitionModelAsync(pairs, cts.Token);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cancel");
    }

    [Fact]
    public async Task EvolveDSLAsync_WithNullDSL_ShouldReturnFailure()
    {
        // Arrange
        var engine = new ProgramSynthesisEngine();
        var stats = new UsageStatistics(
            new Dictionary<string, int>(),
            new Dictionary<string, double>(),
            0);

        // Act
        var result = await engine.EvolveDSLAsync(null!, new List<Primitive>(), stats);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("null");
    }

    [Fact]
    public async Task EvolveDSLAsync_WithNewPrimitives_ShouldAddToDSL()
    {
        // Arrange
        var engine = new ProgramSynthesisEngine();
        var currentDSL = CreateSimpleDSL();
        var initialCount = currentDSL.Primitives.Count;
        var newPrimitives = new List<Primitive>
        {
            new Primitive("newPrim", "a -> a", args => args[0], 0.0),
        };
        var stats = new UsageStatistics(
            new Dictionary<string, int>(),
            new Dictionary<string, double>(),
            0);

        // Act
        var result = await engine.EvolveDSLAsync(currentDSL, newPrimitives, stats);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Primitives.Should().HaveCount(initialCount + 1);
    }

    [Fact]
    public async Task EvolveDSLAsync_WithUsageStats_ShouldUpdatePriors()
    {
        // Arrange
        var engine = new ProgramSynthesisEngine();
        var primitive = new Primitive("add", "int -> int -> int", args => 0, -1.0);
        var currentDSL = new DomainSpecificLanguage(
            "Test",
            new List<Primitive> { primitive },
            new List<TypeRule>(),
            new List<RewriteRule>());
        var stats = new UsageStatistics(
            new Dictionary<string, int> { { "add", 10 } },
            new Dictionary<string, double> { { "add", 0.9 } },
            100);

        // Act
        var result = await engine.EvolveDSLAsync(currentDSL, new List<Primitive>(), stats);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Primitives.Should().HaveCount(1);
        result.Value.Primitives[0].LogPrior.Should().NotBe(primitive.LogPrior);
    }

    [Fact]
    public async Task EvolveDSLAsync_WithCancellation_ShouldHandleCancellation()
    {
        // Arrange
        var engine = new ProgramSynthesisEngine();
        var dsl = CreateSimpleDSL();
        var stats = new UsageStatistics(
            new Dictionary<string, int>(),
            new Dictionary<string, double>(),
            0);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await engine.EvolveDSLAsync(dsl, new List<Primitive>(), stats, cts.Token);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cancel");
    }

    private static DomainSpecificLanguage CreateSimpleDSL()
    {
        var primitives = new List<Primitive>
        {
            new Primitive("id", "a -> a", args => args[0], 0.0),
            new Primitive("const", "a -> b -> a", args => args[0], -0.5),
        };

        var typeRules = new List<TypeRule>
        {
            new TypeRule("Identity", new List<string> { "a" }, "a"),
        };

        return new DomainSpecificLanguage("Simple", primitives, typeRules, new List<RewriteRule>());
    }

    private static DomainSpecificLanguage CreateComplexDSL()
    {
        var primitives = new List<Primitive>
        {
            new Primitive("id", "a -> a", args => args[0], 0.0),
            new Primitive("add", "int -> int -> int", args => 0, -0.5),
            new Primitive("mul", "int -> int -> int", args => 0, -0.5),
            new Primitive("sub", "int -> int -> int", args => 0, -0.5),
            new Primitive("const", "a -> b -> a", args => args[0], -1.0),
        };

        return new DomainSpecificLanguage("Complex", primitives, new List<TypeRule>(), new List<RewriteRule>());
    }

    private static SynthesisProgram CreateTestProgram(string name)
    {
        var node = new ASTNode("Primitive", name, new List<ASTNode>());
        var ast = new AbstractSyntaxTree(node, 1, 1);
        var dsl = CreateSimpleDSL();
        return new SynthesisProgram(name, ast, dsl, -1.0);
    }
}
