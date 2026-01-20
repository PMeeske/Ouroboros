// <copyright file="ParsingTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Hyperon;

using System.Collections.Immutable;
using FluentAssertions;
using Ouroboros.Core.Hyperon;
using Ouroboros.Core.Hyperon.Parsing;
using Xunit;

/// <summary>
/// Tests for the SExpressionParser class.
/// </summary>
[Trait("Category", "Unit")]
public class ParsingTests
{
    private readonly SExpressionParser parser = new();

    #region Symbol Parsing Tests

    [Fact]
    public void Parse_SimpleSymbol_Succeeds()
    {
        // Act
        var result = parser.Parse("Human");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<Symbol>();
        ((Symbol)result.Value).Name.Should().Be("Human");
    }

    [Fact]
    public void Parse_SymbolWithNumbers_Succeeds()
    {
        // Act
        var result = parser.Parse("test123");

        // Assert
        result.IsSuccess.Should().BeTrue();
        ((Symbol)result.Value).Name.Should().Be("test123");
    }

    [Fact]
    public void Parse_SymbolWithUnderscores_Succeeds()
    {
        // Act
        var result = parser.Parse("test_name");

        // Assert
        result.IsSuccess.Should().BeTrue();
        ((Symbol)result.Value).Name.Should().Be("test_name");
    }

    #endregion

    #region Variable Parsing Tests

    [Fact]
    public void Parse_Variable_Succeeds()
    {
        // Act
        var result = parser.Parse("$x");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<Variable>();
        ((Variable)result.Value).Name.Should().Be("x");
    }

    [Fact]
    public void Parse_VariableWithLongName_Succeeds()
    {
        // Act
        var result = parser.Parse("$person");

        // Assert
        result.IsSuccess.Should().BeTrue();
        ((Variable)result.Value).Name.Should().Be("person");
    }

    [Fact]
    public void Parse_EmptyVariable_Fails()
    {
        // Act
        var result = parser.Parse("$");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    #endregion

    #region Expression Parsing Tests

    [Fact]
    public void Parse_EmptyExpression_Succeeds()
    {
        // Act
        var result = parser.Parse("()");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<Expression>();
        ((Expression)result.Value).Children.Should().BeEmpty();
    }

    [Fact]
    public void Parse_SingleElementExpression_Succeeds()
    {
        // Act
        var result = parser.Parse("(Human)");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var expr = (Expression)result.Value;
        expr.Children.Should().HaveCount(1);
        expr.Children[0].Should().Be(Atom.Sym("Human"));
    }

    [Fact]
    public void Parse_TwoElementExpression_Succeeds()
    {
        // Act
        var result = parser.Parse("(Human Socrates)");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var expr = (Expression)result.Value;
        expr.Children.Should().HaveCount(2);
        expr.Children[0].Should().Be(Atom.Sym("Human"));
        expr.Children[1].Should().Be(Atom.Sym("Socrates"));
    }

    [Fact]
    public void Parse_ExpressionWithVariable_Succeeds()
    {
        // Act
        var result = parser.Parse("(Human $x)");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var expr = (Expression)result.Value;
        expr.Children[1].Should().Be(Atom.Var("x"));
    }

    [Fact]
    public void Parse_NestedExpression_Succeeds()
    {
        // Act
        var result = parser.Parse("(implies (Human $x) (Mortal $x))");

        // Assert
        result.IsSuccess.Should().BeTrue();
        var expr = (Expression)result.Value;
        expr.Children.Should().HaveCount(3);
        expr.Children[0].Should().Be(Atom.Sym("implies"));
        expr.Children[1].Should().BeOfType<Expression>();
        expr.Children[2].Should().BeOfType<Expression>();
    }

    [Fact]
    public void Parse_DeeplyNestedExpression_Succeeds()
    {
        // Act
        var result = parser.Parse("(a (b (c (d))))");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ToSExpr().Should().Be("(a (b (c (d))))");
    }

    #endregion

    #region Roundtrip Tests

    [Theory]
    [InlineData("Human")]
    [InlineData("$x")]
    [InlineData("()")]
    [InlineData("(Human)")]
    [InlineData("(Human Socrates)")]
    [InlineData("(Human $x)")]
    [InlineData("(implies (Human $x) (Mortal $x))")]
    [InlineData("(a (b c) (d (e f)))")]
    public void Parse_Roundtrip_PreservesStructure(string input)
    {
        // Act
        var parseResult = parser.Parse(input);

        // Assert
        parseResult.IsSuccess.Should().BeTrue();
        parseResult.Value.ToSExpr().Should().Be(input);
    }

    #endregion

    #region Whitespace Handling Tests

    [Fact]
    public void Parse_ExtraWhitespace_Succeeds()
    {
        // Act
        var result = parser.Parse("(  Human   Socrates  )");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ToSExpr().Should().Be("(Human Socrates)");
    }

    [Fact]
    public void Parse_LeadingWhitespace_Succeeds()
    {
        // Act
        var result = parser.Parse("   (Human Socrates)");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ToSExpr().Should().Be("(Human Socrates)");
    }

    [Fact]
    public void Parse_TrailingWhitespace_Succeeds()
    {
        // Act
        var result = parser.Parse("(Human Socrates)   ");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ToSExpr().Should().Be("(Human Socrates)");
    }

    [Fact]
    public void Parse_Newlines_Succeeds()
    {
        // Act
        var result = parser.Parse("(Human\n  Socrates)");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ToSExpr().Should().Be("(Human Socrates)");
    }

    [Fact]
    public void Parse_Tabs_Succeeds()
    {
        // Act
        var result = parser.Parse("(Human\tSocrates)");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ToSExpr().Should().Be("(Human Socrates)");
    }

    #endregion

    #region Comment Handling Tests

    [Fact]
    public void Parse_LineComment_Ignored()
    {
        // Act
        var result = parser.Parse("; This is a comment\n(Human Socrates)");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ToSExpr().Should().Be("(Human Socrates)");
    }

    [Fact]
    public void Parse_InlineComment_Ignored()
    {
        // Act
        var result = parser.Parse("(Human ; comment\n Socrates)");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ToSExpr().Should().Be("(Human Socrates)");
    }

    #endregion

    #region Error Cases Tests

    [Fact]
    public void Parse_Empty_Fails()
    {
        // Act
        var result = parser.Parse("");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("empty");
    }

    [Fact]
    public void Parse_Whitespace_Fails()
    {
        // Act
        var result = parser.Parse("   ");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Parse_UnmatchedOpenParen_Fails()
    {
        // Act
        var result = parser.Parse("(Human Socrates");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(")");
    }

    [Fact]
    public void Parse_UnmatchedCloseParen_Fails()
    {
        // Act
        var result = parser.Parse("Human Socrates)");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public void Parse_ExtraCloseParen_Fails()
    {
        // Act
        var result = parser.Parse("(Human Socrates))");

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region ParseMultiple Tests

    [Fact]
    public void ParseMultiple_SingleExpression_Succeeds()
    {
        // Act
        var result = parser.ParseMultiple("(Human Socrates)");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
    }

    [Fact]
    public void ParseMultiple_MultipleExpressions_Succeeds()
    {
        // Act
        var result = parser.ParseMultiple("(Human Socrates) (Human Plato) (Animal Dog)");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
        result.Value[0].ToSExpr().Should().Be("(Human Socrates)");
        result.Value[1].ToSExpr().Should().Be("(Human Plato)");
        result.Value[2].ToSExpr().Should().Be("(Animal Dog)");
    }

    [Fact]
    public void ParseMultiple_WithNewlines_Succeeds()
    {
        // Act
        var result = parser.ParseMultiple("(Human Socrates)\n(Human Plato)\n(Animal Dog)");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(3);
    }

    #endregion

    #region TryParse Tests

    [Fact]
    public void TryParse_ValidInput_ReturnsTrue()
    {
        // Act
        var success = parser.TryParse("(Human Socrates)", out var atom);

        // Assert
        success.Should().BeTrue();
        atom.Should().NotBeNull();
        atom!.ToSExpr().Should().Be("(Human Socrates)");
    }

    [Fact]
    public void TryParse_InvalidInput_ReturnsFalse()
    {
        // Act
        var success = parser.TryParse("(Human Socrates", out var atom);

        // Assert
        success.Should().BeFalse();
        atom.Should().BeNull();
    }

    #endregion

    #region Special Characters Tests

    [Fact]
    public void Parse_SymbolWithHyphen_Succeeds()
    {
        // Act
        var result = parser.Parse("is-a");

        // Assert
        result.IsSuccess.Should().BeTrue();
        ((Symbol)result.Value).Name.Should().Be("is-a");
    }

    [Fact]
    public void Parse_QuotedString_TreatedAsSymbol()
    {
        // Act
        var result = parser.Parse("\"hello world\"");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeOfType<Symbol>();
        ((Symbol)result.Value).Name.Should().Be("hello world");
    }

    #endregion

    #region Complex Expression Tests

    [Fact]
    public void Parse_ComplexRule_Succeeds()
    {
        // Arrange
        var input = "(implies (and (Human $x) (Philosopher $x)) (Wise $x))";

        // Act
        var result = parser.Parse(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var expr = (Expression)result.Value;
        expr.Children.Should().HaveCount(3);
        expr.Children[0].Should().Be(Atom.Sym("implies"));
    }

    [Fact]
    public void Parse_LambdaStyle_Succeeds()
    {
        // Arrange
        var input = "(lambda ($x $y) (+ $x $y))";

        // Act
        var result = parser.Parse(input);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ToSExpr().Should().Be("(lambda ($x $y) (+ $x $y))");
    }

    #endregion
}
