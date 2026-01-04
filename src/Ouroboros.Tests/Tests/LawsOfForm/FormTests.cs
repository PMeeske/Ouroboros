// <copyright file="FormTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.LawsOfForm;

using FluentAssertions;
using Ouroboros.Core.LawsOfForm;
using Xunit;

/// <summary>
/// Tests for the Form type from Laws of Form.
/// Validates three-valued logic and Laws of Form axioms.
/// </summary>
[Trait("Category", "Unit")]
public class FormTests
{
    [Fact]
    public void Cross_CreatesMarkedForm()
    {
        // Act
        var form = Form.Mark;

        // Assert
        form.IsMark().Should().BeTrue();
        form.IsVoid().Should().BeFalse();
        form.IsImaginary().Should().BeFalse();
    }

    [Fact]
    public void Void_CreatesVoidForm()
    {
        // Act
        var form = Form.Void;

        // Assert
        form.IsVoid().Should().BeTrue();
        form.IsMark().Should().BeFalse();
        form.IsImaginary().Should().BeFalse();
    }

    [Fact]
    public void Imaginary_CreatesImaginaryForm()
    {
        // Act
        var form = Form.Imaginary;

        // Assert
        form.IsImaginary().Should().BeTrue();
        form.IsMark().Should().BeFalse();
        form.IsVoid().Should().BeFalse();
    }

    [Fact]
    public void Not_DoubleNegation_ReturnsOriginal()
    {
        // Law of Form: ⌐⌐M = M (double negation returns to original)
        // Mark.Not() = Void, Void.Not() = Mark
        // Arrange
        var form = Form.Mark;

        // Act
        var result = form.Not().Not();

        // Assert
        result.IsMark().Should().BeTrue();
    }

    [Fact]
    public void Not_NegatingVoid_ReturnsMark()
    {
        // Arrange
        var form = Form.Void;

        // Act
        var result = form.Not();

        // Assert
        result.IsMark().Should().BeTrue();
    }

    [Fact]
    public void Not_NegatingImaginary_ReturnsImaginary()
    {
        // Self-negating property of imaginary forms
        // Arrange
        var form = Form.Imaginary;

        // Act
        var result = form.Not();

        // Assert
        result.IsImaginary().Should().BeTrue();
    }

    [Fact]
    public void And_MarkAndMark_ReturnsMark()
    {
        // Arrange
        var left = Form.Mark;
        var right = Form.Mark;

        // Act
        var result = left.And(right);

        // Assert
        result.IsMark().Should().BeTrue();
    }

    [Fact]
    public void And_MarkAndVoid_ReturnsVoid()
    {
        // Arrange
        var left = Form.Mark;
        var right = Form.Void;

        // Act
        var result = left.And(right);

        // Assert
        result.IsVoid().Should().BeTrue();
    }

    [Fact]
    public void And_WithImaginary_ReturnsImaginary()
    {
        // Arrange
        var left = Form.Mark;
        var right = Form.Imaginary;

        // Act
        var result = left.And(right);

        // Assert
        result.IsImaginary().Should().BeTrue();
    }

    [Fact]
    public void Or_MarkOrAnything_ReturnsMark()
    {
        // Arrange
        var left = Form.Mark;
        var right = Form.Void;

        // Act
        var result = left.Or(right);

        // Assert
        result.IsMark().Should().BeTrue();
    }

    [Fact]
    public void Or_VoidOrVoid_ReturnsVoid()
    {
        // Arrange
        var left = Form.Void;
        var right = Form.Void;

        // Act
        var result = left.Or(right);

        // Assert
        result.IsVoid().Should().BeTrue();
    }

    [Fact]
    public void Or_VoidOrImaginary_ReturnsImaginary()
    {
        // Arrange
        var left = Form.Void;
        var right = Form.Imaginary;

        // Act
        var result = left.Or(right);

        // Assert
        result.IsImaginary().Should().BeTrue();
    }

    [Fact]
    public void NotOperator_NegatesForm()
    {
        // Arrange
        var form = Form.Mark;

        // Act
        var result = !form;

        // Assert
        result.IsVoid().Should().BeTrue();
    }

    [Fact]
    public void AndOperator_PerformsConjunction()
    {
        // Arrange
        var left = Form.Mark;
        var right = Form.Mark;

        // Act
        var result = left & right;

        // Assert
        result.IsMark().Should().BeTrue();
    }

    [Fact]
    public void OrOperator_PerformsDisjunction()
    {
        // Arrange
        var left = Form.Void;
        var right = Form.Mark;

        // Act
        var result = left | right;

        // Assert
        result.IsMark().Should().BeTrue();
    }

    [Fact]
    public void Match_OnMark_ExecutesMarkFunction()
    {
        // Arrange
        var form = Form.Mark;
        var executed = string.Empty;

        // Act
        form.Match(
            onMark: () => { executed = "mark"; },
            onVoid: () => { executed = "void"; },
            onImaginary: () => { executed = "imaginary"; });

        // Assert
        executed.Should().Be("mark");
    }

    [Fact]
    public void Match_OnVoid_ExecutesVoidFunction()
    {
        // Arrange
        var form = Form.Void;
        var executed = string.Empty;

        // Act
        form.Match(
            onMark: () => { executed = "mark"; },
            onVoid: () => { executed = "void"; },
            onImaginary: () => { executed = "imaginary"; });

        // Assert
        executed.Should().Be("void");
    }

    [Fact]
    public void Match_OnImaginary_ExecutesImaginaryFunction()
    {
        // Arrange
        var form = Form.Imaginary;
        var executed = string.Empty;

        // Act
        form.Match(
            onMark: () => { executed = "mark"; },
            onVoid: () => { executed = "void"; },
            onImaginary: () => { executed = "imaginary"; });

        // Assert
        executed.Should().Be("imaginary");
    }

    [Fact]
    public void MatchWithReturn_OnMark_ReturnsMarkResult()
    {
        // Arrange
        var form = Form.Mark;

        // Act
        var result = form.Match(
            onMark: () => "mark",
            onVoid: () => "void",
            onImaginary: () => "imaginary");

        // Assert
        result.Should().Be("mark");
    }

    [Fact]
    public void Equals_SameForms_ReturnsTrue()
    {
        // Arrange
        var form1 = Form.Mark;
        var form2 = Form.Mark;

        // Act & Assert
        form1.Equals(form2).Should().BeTrue();
        (form1 == form2).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentForms_ReturnsFalse()
    {
        // Arrange
        var form1 = Form.Mark;
        var form2 = Form.Void;

        // Act & Assert
        form1.Equals(form2).Should().BeFalse();
        (form1 != form2).Should().BeTrue();
    }

    [Fact]
    public void ToString_Mark_ReturnsSymbol()
    {
        // Arrange
        var form = Form.Mark;

        // Act
        var result = form.ToString();

        // Assert
        result.Should().Be("⌐");
    }

    [Fact]
    public void ToString_Void_ReturnsSymbol()
    {
        // Arrange
        var form = Form.Void;

        // Act
        var result = form.ToString();

        // Assert
        result.Should().Be("∅");
    }

    [Fact]
    public void ToString_Imaginary_ReturnsSymbol()
    {
        // Arrange
        var form = Form.Imaginary;

        // Act
        var result = form.ToString();

        // Assert
        result.Should().Be("i");
    }
}
