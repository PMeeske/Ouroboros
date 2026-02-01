// <copyright file="SynthesisTypesTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Core.Synthesis;
using Xunit;

namespace Ouroboros.Tests.Synthesis;

/// <summary>
/// Tests for synthesis type definitions.
/// </summary>
public class SynthesisTypesTests
{
    [Fact]
    public void ASTNode_Constructor_ShouldCreateNode()
    {
        // Arrange & Act
        var node = new ASTNode("Primitive", "add", new List<ASTNode>());

        // Assert
        node.NodeType.Should().Be("Primitive");
        node.Value.Should().Be("add");
        node.Children.Should().BeEmpty();
    }

    [Fact]
    public void ASTNode_WithChildren_ShouldMaintainChildren()
    {
        // Arrange
        var child1 = new ASTNode("Primitive", "x", new List<ASTNode>());
        var child2 = new ASTNode("Primitive", "y", new List<ASTNode>());

        // Act
        var parent = new ASTNode("Apply", "add", new List<ASTNode> { child1, child2 });

        // Assert
        parent.Children.Should().HaveCount(2);
        parent.Children[0].Should().Be(child1);
        parent.Children[1].Should().Be(child2);
    }

    [Fact]
    public void AbstractSyntaxTree_Constructor_ShouldStoreProperties()
    {
        // Arrange
        var root = new ASTNode("Primitive", "test", new List<ASTNode>());

        // Act
        var ast = new AbstractSyntaxTree(root, 5, 10);

        // Assert
        ast.Root.Should().Be(root);
        ast.Depth.Should().Be(5);
        ast.NodeCount.Should().Be(10);
    }

    [Fact]
    public void InputOutputExample_Constructor_ShouldStoreValues()
    {
        // Arrange & Act
        var example = new InputOutputExample(5, 10, 1.0);

        // Assert
        example.Input.Should().Be(5);
        example.ExpectedOutput.Should().Be(10);
        example.TimeoutSeconds.Should().Be(1.0);
    }

    [Fact]
    public void Primitive_Constructor_ShouldStoreAllProperties()
    {
        // Arrange
        Func<object[], object> impl = args => args[0];

        // Act
        var primitive = new Primitive("add", "int -> int -> int", impl, -1.5);

        // Assert
        primitive.Name.Should().Be("add");
        primitive.Type.Should().Be("int -> int -> int");
        primitive.Implementation.Should().BeSameAs(impl);
        primitive.LogPrior.Should().Be(-1.5);
    }

    [Fact]
    public void DomainSpecificLanguage_Constructor_ShouldStoreComponents()
    {
        // Arrange
        var primitives = new List<Primitive>
        {
            new Primitive("add", "int -> int -> int", args => 0, 0.0),
        };
        var typeRules = new List<TypeRule>
        {
            new TypeRule("Add", new List<string> { "int", "int" }, "int"),
        };
        var optimizations = new List<RewriteRule>();

        // Act
        var dsl = new DomainSpecificLanguage("TestDSL", primitives, typeRules, optimizations);

        // Assert
        dsl.Name.Should().Be("TestDSL");
        dsl.Primitives.Should().HaveCount(1);
        dsl.TypeRules.Should().HaveCount(1);
        dsl.Optimizations.Should().BeEmpty();
    }

    [Fact]
    public void Program_WithTrace_ShouldStoreTrace()
    {
        // Arrange
        var root = new ASTNode("Primitive", "test", new List<ASTNode>());
        var ast = new AbstractSyntaxTree(root, 1, 1);
        var dsl = new DomainSpecificLanguage("Test", new List<Primitive>(), new List<TypeRule>(), new List<RewriteRule>());
        var trace = new ExecutionTrace(new List<ExecutionStep>(), new object(), TimeSpan.FromSeconds(1));

        // Act
        var program = new SynthesisProgram("test", ast, dsl, -2.0, trace);

        // Assert
        program.SourceCode.Should().Be("test");
        program.AST.Should().Be(ast);
        program.Language.Should().Be(dsl);
        program.LogProbability.Should().Be(-2.0);
        program.Trace.Should().Be(trace);
    }

    [Fact]
    public void SynthesisTask_Constructor_ShouldStoreAllFields()
    {
        // Arrange
        var examples = new List<InputOutputExample>
        {
            new InputOutputExample(1, 2),
        };
        var dsl = new DomainSpecificLanguage("Test", new List<Primitive>(), new List<TypeRule>(), new List<RewriteRule>());

        // Act
        var task = new SynthesisTask("Add one", examples, dsl);

        // Assert
        task.Description.Should().Be("Add one");
        task.Examples.Should().HaveCount(1);
        task.DSL.Should().Be(dsl);
    }

    [Fact]
    public void UsageStatistics_Constructor_ShouldInitializeCollections()
    {
        // Arrange
        var useCounts = new Dictionary<string, int> { { "add", 5 } };
        var successRates = new Dictionary<string, double> { { "add", 0.8 } };

        // Act
        var stats = new UsageStatistics(useCounts, successRates, 10);

        // Assert
        stats.PrimitiveUseCounts.Should().ContainKey("add");
        stats.PrimitiveSuccessRates.Should().ContainKey("add");
        stats.TotalProgramsSynthesized.Should().Be(10);
    }

    [Fact]
    public void ExecutionTrace_Constructor_ShouldStoreSteps()
    {
        // Arrange
        var steps = new List<ExecutionStep>
        {
            new ExecutionStep("add", new List<object> { 1, 2 }, 3),
        };

        // Act
        var trace = new ExecutionTrace(steps, 3, TimeSpan.FromSeconds(0.5));

        // Assert
        trace.Steps.Should().HaveCount(1);
        trace.FinalResult.Should().Be(3);
        trace.Duration.Should().Be(TimeSpan.FromSeconds(0.5));
    }

    [Fact]
    public void CompressionStrategy_ShouldHaveAllExpectedValues()
    {
        // Act & Assert
        Enum.GetValues<CompressionStrategy>().Should().Contain(new[]
        {
            CompressionStrategy.AntiUnification,
            CompressionStrategy.EGraph,
            CompressionStrategy.FragmentGrammar,
        });
    }
}
