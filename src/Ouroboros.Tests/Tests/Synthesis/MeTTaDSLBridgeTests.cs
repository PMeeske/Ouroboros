// <copyright file="MeTTaDSLBridgeTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Core.Hyperon;
using Ouroboros.Core.Synthesis;
using Xunit;

namespace Ouroboros.Tests.Synthesis;

/// <summary>
/// Tests for MeTTa DSL bridge conversion functionality.
/// </summary>
public class MeTTaDSLBridgeTests
{
    [Fact]
    public void ASTToMeTTa_WithPrimitiveNode_ShouldConvertToSymbol()
    {
        // Arrange
        var node = new ASTNode("Primitive", "add", new List<ASTNode>());

        // Act
        var result = MeTTaDSLBridge.ASTToMeTTa(node);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<Symbol>();
        ((Symbol)result.Value).Name.Should().Be("add");
    }

    [Fact]
    public void ASTToMeTTa_WithVariableNode_ShouldConvertToVariable()
    {
        // Arrange
        var node = new ASTNode("Variable", "$x", new List<ASTNode>());

        // Act
        var result = MeTTaDSLBridge.ASTToMeTTa(node);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<Variable>();
        ((Variable)result.Value).Name.Should().Be("x");
    }

    [Fact]
    public void ASTToMeTTa_WithApplicationNode_ShouldConvertToExpression()
    {
        // Arrange
        var child1 = new ASTNode("Primitive", "x", new List<ASTNode>());
        var child2 = new ASTNode("Primitive", "y", new List<ASTNode>());
        var node = new ASTNode("Apply", "add", new List<ASTNode> { child1, child2 });

        // Act
        var result = MeTTaDSLBridge.ASTToMeTTa(node);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<Expression>();
        var expr = (Expression)result.Value;
        expr.Children.Should().HaveCount(3); // operator + 2 arguments
    }

    [Fact]
    public void MeTTaToAST_WithSymbol_ShouldConvertToPrimitive()
    {
        // Arrange
        var atom = Atom.Sym("add");

        // Act
        var result = MeTTaDSLBridge.MeTTaToAST(atom);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.NodeType.Should().Be("Primitive");
        result.Value.Value.Should().Be("add");
    }

    [Fact]
    public void MeTTaToAST_WithVariable_ShouldConvertToVariable()
    {
        // Arrange
        var atom = Atom.Var("x");

        // Act
        var result = MeTTaDSLBridge.MeTTaToAST(atom);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.NodeType.Should().Be("Variable");
        result.Value.Value.Should().Be("$x");
    }

    [Fact]
    public void MeTTaToAST_WithExpression_ShouldConvertToApplication()
    {
        // Arrange
        var atom = Atom.Expr(Atom.Sym("add"), Atom.Sym("x"), Atom.Sym("y"));

        // Act
        var result = MeTTaDSLBridge.MeTTaToAST(atom);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.NodeType.Should().Be("Apply");
        result.Value.Value.Should().Be("add");
        result.Value.Children.Should().HaveCount(2);
    }

    [Fact]
    public void ProgramToMeTTa_WithValidProgram_ShouldConvert()
    {
        // Arrange
        var node = new ASTNode("Primitive", "test", new List<ASTNode>());
        var ast = new AbstractSyntaxTree(node, 1, 1);
        var dsl = new DomainSpecificLanguage("Test", new List<Primitive>(), new List<TypeRule>(), new List<RewriteRule>());
        var program = new SynthesisProgram("test", ast, dsl, 0.0);

        // Act
        var result = MeTTaDSLBridge.ProgramToMeTTa(program);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<Symbol>();
    }

    [Fact]
    public void PrimitiveToMeTTa_ShouldCreateTypeAnnotation()
    {
        // Arrange
        var primitive = new Primitive("add", "int -> int -> int", args => 0, 0.0);

        // Act
        var atom = MeTTaDSLBridge.PrimitiveToMeTTa(primitive);

        // Assert
        atom.Should().BeOfType<Expression>();
        var expr = (Expression)atom;
        expr.Children.Should().HaveCount(3); // : name type
        ((Symbol)expr.Children[0]).Name.Should().Be(":");
        ((Symbol)expr.Children[1]).Name.Should().Be("add");
        ((Symbol)expr.Children[2]).Name.Should().Be("int -> int -> int");
    }

    [Fact]
    public void TypeRuleToMeTTa_ShouldCreateArrowType()
    {
        // Arrange
        var typeRule = new TypeRule("Add", new List<string> { "int", "int" }, "int");

        // Act
        var atom = MeTTaDSLBridge.TypeRuleToMeTTa(typeRule);

        // Assert
        atom.Should().BeOfType<Expression>();
        var expr = (Expression)atom;
        expr.Children.Should().HaveCount(3); // : ruleName (-> ...)
        ((Symbol)expr.Children[0]).Name.Should().Be(":");
        ((Symbol)expr.Children[1]).Name.Should().Be("Add");
        expr.Children[2].Should().BeOfType<Expression>();
    }

    [Fact]
    public void DSLToMeTTa_WithPrimitivesAndRules_ShouldConvertAll()
    {
        // Arrange
        var primitives = new List<Primitive>
        {
            new Primitive("add", "int -> int -> int", args => 0, 0.0),
            new Primitive("mul", "int -> int -> int", args => 0, 0.0),
        };
        var typeRules = new List<TypeRule>
        {
            new TypeRule("Add", new List<string> { "int", "int" }, "int"),
        };
        var dsl = new DomainSpecificLanguage("Test", primitives, typeRules, new List<RewriteRule>());

        // Act
        var atoms = MeTTaDSLBridge.DSLToMeTTa(dsl);

        // Assert
        atoms.Should().HaveCount(3); // 2 primitives + 1 type rule
        atoms.Should().AllBeOfType<Expression>();
    }

    [Fact]
    public void DSLToMeTTa_WithEmptyDSL_ShouldReturnEmptyList()
    {
        // Arrange
        var dsl = new DomainSpecificLanguage("Empty", new List<Primitive>(), new List<TypeRule>(), new List<RewriteRule>());

        // Act
        var atoms = MeTTaDSLBridge.DSLToMeTTa(dsl);

        // Assert
        atoms.Should().BeEmpty();
    }

    [Fact]
    public void ASTToMeTTa_AndBack_ShouldPreserveStructure()
    {
        // Arrange
        var original = new ASTNode("Primitive", "test", new List<ASTNode>());

        // Act
        var mettaResult = MeTTaDSLBridge.ASTToMeTTa(original);
        var astResult = MeTTaDSLBridge.MeTTaToAST(mettaResult.Value);

        // Assert
        mettaResult.IsSuccess.Should().BeTrue();
        astResult.IsSuccess.Should().BeTrue();
        astResult.Value.Value.Should().Be(original.Value);
    }

    [Fact]
    public void ASTToMeTTa_WithNestedApplication_ShouldConvertCorrectly()
    {
        // Arrange
        var innerNode = new ASTNode("Apply", "add", new List<ASTNode>
        {
            new ASTNode("Primitive", "x", new List<ASTNode>()),
            new ASTNode("Primitive", "y", new List<ASTNode>()),
        });
        var outerNode = new ASTNode("Apply", "mul", new List<ASTNode>
        {
            innerNode,
            new ASTNode("Primitive", "z", new List<ASTNode>()),
        });

        // Act
        var result = MeTTaDSLBridge.ASTToMeTTa(outerNode);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<Expression>();
        var expr = (Expression)result.Value;
        expr.Children.Should().HaveCountGreaterThan(1);
    }
}
