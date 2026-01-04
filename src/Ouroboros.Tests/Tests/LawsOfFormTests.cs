// <copyright file="LawsOfFormTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests;

using FluentAssertions;
using Ouroboros.Core.LawsOfForm;
using Ouroboros.Core.Monads;
using Xunit;

/// <summary>
/// Tests for the Laws of Form implementation.
/// Validates Spencer-Brown's calculus of indications and its integration
/// with the monadic pipeline system.
/// </summary>
[Trait("Category", "Unit")]
public class LawsOfFormTests
{
    #region Basic Form Tests

    [Fact]
    public void Void_IsVoid()
    {
        // Arrange & Act
        var form = Form.Void;

        // Assert
        form.IsVoid().Should().BeTrue();
        form.IsMarked().Should().BeFalse();
    }

    [Fact]
    public void Cross_IsMarked()
    {
        // Arrange & Act
        var form = Form.Mark;

        // Assert
        form.IsMarked().Should().BeTrue();
        form.IsVoid().Should().BeFalse();
    }

    [Fact]
    public void Mark_CreatesDistinction()
    {
        // Arrange & Act
        var form = Form.CrossForm(Form.Void);

        // Assert
        form.IsMarked().Should().BeTrue();
        form.ToString().Should().Contain("⌐");
    }

    [Fact]
    public void Void_ToString_IsEmpty()
    {
        // Arrange & Act
        var form = Form.Void;

        // Assert
        form.ToString().Should().Be("∅");
    }

    #endregion

    #region Law of Crossing Tests

    [Fact]
    public void LawOfCrossing_DoubleMarkVoid_EqualsVoid()
    {
        // Law of Crossing: Mark(Mark(Void)) = Void
        // The fundamental cancellation law
        var form = Form.CrossForm(Form.CrossForm(Form.Void));

        // Act
        var result = form.Eval();

        // Assert
        result.IsVoid().Should().BeTrue("Mark(Mark(Void)) should equal Void by the Law of Crossing");
    }

    [Fact]
    public void LawOfCrossing_TripleMark_EqualsMark()
    {
        // Mark(Mark(Mark(Void))) = Mark(Void) since inner double mark cancels
        var form = Form.CrossForm(Form.CrossForm(Form.CrossForm(Form.Void)));

        // Act
        var result = form.Eval();

        // Assert
        result.IsMarked().Should().BeTrue("Triple mark should reduce to single mark");
    }

    [Fact]
    public void LawOfCrossing_QuadrupleMark_EqualsVoid()
    {
        // Mark(Mark(Mark(Mark(Void)))) = Void (two pairs cancel)
        var form = Form.CrossForm(Form.CrossForm(Form.CrossForm(Form.CrossForm(Form.Void))));

        // Act
        var result = form.Eval();

        // Assert
        result.IsVoid().Should().BeTrue("Quadruple mark should reduce to void");
    }

    [Fact]
    public void LawOfCrossing_EvenMarks_AlwaysVoid()
    {
        // Any even number of marks around void should reduce to void
        var form = Form.Void;
        for (int i = 0; i < 6; i++)
        {
            form = Form.CrossForm(form);
        }

        // Act
        var result = form.Eval();

        // Assert
        result.IsVoid().Should().BeTrue("Even number of marks should reduce to void");
    }

    [Fact]
    public void LawOfCrossing_OddMarks_AlwaysMarked()
    {
        // Any odd number of marks around void should reduce to marked
        var form = Form.Void;
        for (int i = 0; i < 5; i++)
        {
            form = Form.CrossForm(form);
        }

        // Act
        var result = form.Eval();

        // Assert
        result.IsMarked().Should().BeTrue("Odd number of marks should reduce to marked");
    }

    #endregion

    #region Law of Calling Tests

    [Fact]
    public void LawOfCalling_CallingSameForm_Condenses()
    {
        // Calling a form with itself should condense to just the form
        var mark = Form.Mark;
        var form = mark.Call(mark);

        // Act
        var result = form.Eval();

        // Assert
        result.IsMarked().Should().BeTrue("Mark called with Mark should condense to Mark");
    }

    [Fact]
    public void LawOfCalling_VoidWithVoid_IsVoid()
    {
        // Void indicated with void is still void
        var form = Form.Void.Call(Form.Void);

        // Act
        var result = form.Eval();

        // Assert
        result.IsVoid().Should().BeTrue("Void call Void should be Void");
    }

    [Fact]
    public void LawOfCalling_MarkWithVoid_IsMark()
    {
        // Mark indicated with void is mark (void is identity)
        var form = Form.Mark.Call(Form.Void);

        // Act
        var result = form.Eval();

        // Assert
        result.IsMarked().Should().BeTrue("Mark call Void should be Mark");
    }

    [Fact]
    public void LawOfCalling_VoidWithMark_IsMark()
    {
        // Void indicated with mark is mark
        var form = Form.Void.Call(Form.Mark);

        // Act
        var result = form.Eval();

        // Assert
        result.IsMarked().Should().BeTrue("Void call Mark should be Mark");
    }

    #endregion

    #region Boolean Algebra Equivalence Tests

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void Form_ToBoolean_Roundtrips(bool input, bool _ /* unused */)
    {
        // Arrange
        var form = input.ToForm();

        // Act
        var result = form.ToBoolean();

        // Assert
        result.Should().Be(input);
    }

    [Fact]
    public void Not_InvertsMarkedToVoid()
    {
        // Not(Mark) = Void via double crossing
        var form = Form.Mark.Not();

        // Act
        var result = form.Eval();

        // Assert
        result.IsVoid().Should().BeTrue("Not(Mark) should be Void");
    }

    [Fact]
    public void Not_InvertsVoidToMarked()
    {
        // Not(Void) = Mark
        var form = Form.Void.Not();

        // Act
        var result = form.Eval();

        // Assert
        result.IsMarked().Should().BeTrue("Not(Void) should be Mark");
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(false, false, false)]
    public void Or_MatchesBooleanOr(bool a, bool b, bool expected)
    {
        // Arrange
        var formA = a.ToForm();
        var formB = b.ToForm();

        // Act
        var result = formA.Or(formB).Eval().ToBoolean();

        // Assert
        result.Should().Be(expected, $"Or({a}, {b}) should equal {expected}");
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, false)]
    public void And_MatchesBooleanAnd(bool a, bool b, bool expected)
    {
        // Arrange
        var formA = a.ToForm();
        var formB = b.ToForm();

        // Act
        var result = formA.And(formB).Eval().ToBoolean();

        // Assert
        result.Should().Be(expected, $"And({a}, {b}) should equal {expected}");
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, true)]
    [InlineData(false, false, true)]
    public void Implies_MatchesBooleanImplication(bool a, bool b, bool expected)
    {
        // Arrange
        var formA = a.ToForm();
        var formB = b.ToForm();

        // Act
        var result = formA.Implies(formB).Eval().ToBoolean();

        // Assert
        result.Should().Be(expected, $"Implies({a}, {b}) should equal {expected}");
    }

    #endregion

    #region Monad Integration Tests

    [Fact]
    public void ToResult_MarkedForm_ReturnsSuccess()
    {
        // Arrange
        var form = Form.Mark;

        // Act
        var result = form.ToResult(42, "error");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void ToResult_VoidForm_ReturnsFailure()
    {
        // Arrange
        var form = Form.Void;

        // Act
        var result = form.ToResult(42, "error");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("error");
    }

    [Fact]
    public void ToOption_MarkedForm_ReturnsSome()
    {
        // Arrange
        var form = Form.Mark;

        // Act
        var result = form.ToOption("value");

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value.Should().Be("value");
    }

    [Fact]
    public void ToOption_VoidForm_ReturnsNone()
    {
        // Arrange
        var form = Form.Void;

        // Act
        var result = form.ToOption("value");

        // Assert
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public void FromOption_Some_ReturnsMarked()
    {
        // Arrange
        var option = Option<int>.Some(42);

        // Act
        var form = FormExtensions.FromOption(option);

        // Assert
        form.IsMarked().Should().BeTrue();
    }

    [Fact]
    public void FromOption_None_ReturnsVoid()
    {
        // Arrange
        var option = Option<int>.None();

        // Act
        var form = FormExtensions.FromOption(option);

        // Assert
        form.IsVoid().Should().BeTrue();
    }

    [Fact]
    public void FromResult_Success_ReturnsMarked()
    {
        // Arrange
        var result = Result<int, string>.Success(42);

        // Act
        var form = FormExtensions.FromResult(result);

        // Assert
        form.IsMarked().Should().BeTrue();
    }

    [Fact]
    public void FromResult_Failure_ReturnsVoid()
    {
        // Arrange
        var result = Result<int, string>.Failure("error");

        // Act
        var form = FormExtensions.FromResult(result);

        // Assert
        form.IsVoid().Should().BeTrue();
    }

    #endregion

    #region Form Match Tests

    [Fact]
    public void Match_OnMarked_ExecutesMarkedFunction()
    {
        // Arrange
        var form = Form.Mark;

        // Act
        var result = form.Match(
            onMark: () => "marked",
            onVoid: () => "void",
            onImaginary: () => "imaginary");

        // Assert
        result.Should().Be("marked");
    }

    [Fact]
    public void Match_OnVoid_ExecutesVoidFunction()
    {
        // Arrange
        var form = Form.Void;

        // Act
        var result = form.Match(
            onMark: () => "marked",
            onVoid: () => "void",
            onImaginary: () => "imaginary");

        // Assert
        result.Should().Be("void");
    }

    [Fact]
    public void Map_OnMarkedForm_TransformsInner()
    {
        // Arrange
        var form = Form.Mark;

        // Act
        var result = form.Map(f => Form.CrossForm(f));

        // Assert - mapping adds another mark
        result.Eval().IsVoid().Should().BeTrue("Mark(Mark(x)) should be void");
    }

    #endregion

    #region Algebraic Laws Tests

    [Fact]
    public void Form_SatisfiesDoubleNegationElimination()
    {
        // ¬¬A = A
        var a = Form.Mark;
        var doubleNegation = a.Not().Not();

        // Act
        var result = doubleNegation.Eval();

        // Assert
        result.IsMarked().Should().Be(a.IsMarked(), "Double negation should be eliminated");
    }

    [Fact]
    public void Form_SatisfiesDeMorgansLaw_OrCase()
    {
        // ¬(A ∨ B) = ¬A ∧ ¬B
        var a = Form.Mark;
        var b = Form.Void;

        var leftSide = a.Or(b).Not().Eval();
        var rightSide = a.Not().And(b.Not()).Eval();

        // Assert
        leftSide.IsMarked().Should().Be(rightSide.IsMarked(), "De Morgan's law should hold");
    }

    [Fact]
    public void Form_SatisfiesDeMorgansLaw_AndCase()
    {
        // ¬(A ∧ B) = ¬A ∨ ¬B
        var a = Form.Mark;
        var b = Form.Void;

        var leftSide = a.And(b).Not().Eval();
        var rightSide = a.Not().Or(b.Not()).Eval();

        // Assert
        leftSide.IsMarked().Should().Be(rightSide.IsMarked(), "De Morgan's law should hold");
    }

    [Fact]
    public void Form_OrIsCommutative()
    {
        // A ∨ B = B ∨ A
        var a = Form.Mark;
        var b = Form.Void;

        var ab = a.Or(b).Eval();
        var ba = b.Or(a).Eval();

        // Assert
        ab.IsMarked().Should().Be(ba.IsMarked(), "Or should be commutative");
    }

    [Fact]
    public void Form_AndIsCommutative()
    {
        // A ∧ B = B ∧ A
        var a = Form.Mark;
        var b = Form.Void;

        var ab = a.And(b).Eval();
        var ba = b.And(a).Eval();

        // Assert
        ab.IsMarked().Should().Be(ba.IsMarked(), "And should be commutative");
    }

    [Fact]
    public void Form_SatisfiesExcludedMiddle()
    {
        // A ∨ ¬A = True (Mark)
        var a = Form.Void;
        var excludedMiddle = a.Or(a.Not());

        // Assert
        excludedMiddle.IsMarked().Should().BeTrue("Law of excluded middle should hold");
    }

    [Fact]
    public void Form_SatisfiesNonContradiction()
    {
        // A ∧ ¬A = False (Void)
        var a = Form.Mark;
        var contradiction = a.And(a.Not());

        // Assert
        contradiction.IsVoid().Should().BeTrue("Non-contradiction should hold");
    }

    #endregion

    #region Record Equality Tests

    [Fact]
    public void Void_EqualsVoid()
    {
        // Arrange
        var void1 = Form.Void;
        var void2 = Form.Void;

        // Assert
        void1.Should().Be(void2);
    }

    [Fact]
    public void Cross_EqualsCross()
    {
        // Arrange
        var cross1 = Form.Mark;
        var cross2 = Form.Mark;

        // These are structurally equal Mark(Void) forms
        cross1.IsMarked().Should().Be(cross2.IsMarked());
    }

    [Fact]
    public void DifferentForms_AreNotEqual()
    {
        // Arrange
        var voidForm = Form.Void;
        var markForm = Form.Mark;

        // Assert
        voidForm.Should().NotBe(markForm);
    }

    #endregion
}
