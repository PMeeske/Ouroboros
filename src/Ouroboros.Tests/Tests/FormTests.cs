// <copyright file="FormTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests;

using FluentAssertions;
using Ouroboros.Core.LawsOfForm;
using Xunit;

/// <summary>
/// Tests for the Form enum and FormExtensions.
/// Validates Laws of Form algebraic properties.
/// </summary>
[Trait("Category", "Unit")]
public class FormTests
{
    [Fact]
    public void Cross_FollowsLawOfCrossing_DoubleInversion()
    {
        // Law of Crossing: Cross(Cross(x)) = x
        Form.Mark.Not().Not().Should().Be(Form.Mark);
        Form.Void.Not().Not().Should().Be(Form.Void);
        Form.Imaginary.Not().Not().Should().Be(Form.Imaginary);
    }

    [Fact]
    public void Cross_InvertsMarkAndVoid()
    {
        Form.Mark.Not().Should().Be(Form.Void);
        Form.Void.Not().Should().Be(Form.Mark);
    }

    [Fact]
    public void Cross_ImaginaryIsSelfDual()
    {
        Form.Imaginary.Not().Should().Be(Form.Imaginary);
    }

    [Fact]
    public void Calling_IsIdempotent()
    {
        // Law of Calling: Calling(x) = x (idempotent)
        Form.Mark.Calling().Should().Be(Form.Mark);
        Form.Void.Calling().Should().Be(Form.Void);
        Form.Imaginary.Calling().Should().Be(Form.Imaginary);
    }

    public static IEnumerable<object[]> AndBooleanLogicData()
    {
        yield return new object[] { Form.Mark, Form.Mark, Form.Mark };
        yield return new object[] { Form.Mark, Form.Void, Form.Void };
        yield return new object[] { Form.Void, Form.Mark, Form.Void };
        yield return new object[] { Form.Void, Form.Void, Form.Void };
    }

    [Theory]
    [MemberData(nameof(AndBooleanLogicData))]
    public void And_FollowsBooleanLogicForCertainValues(Form left, Form right, Form expected)
    {
        left.And(right).Should().Be(expected);
    }

    public static IEnumerable<object[]> AndImaginaryData()
    {
        yield return new object[] { Form.Mark, Form.Imaginary };
        yield return new object[] { Form.Imaginary, Form.Mark };
        yield return new object[] { Form.Void, Form.Imaginary };
        yield return new object[] { Form.Imaginary, Form.Void };
        yield return new object[] { Form.Imaginary, Form.Imaginary };
    }

    [Theory]
    [MemberData(nameof(AndImaginaryData))]
    public void And_ImaginaryPropagates(Form left, Form right)
    {
        left.And(right).Should().Be(Form.Imaginary);
    }

    public static IEnumerable<object[]> OrBooleanLogicData()
    {
        yield return new object[] { Form.Mark, Form.Mark, Form.Mark };
        yield return new object[] { Form.Mark, Form.Void, Form.Mark };
        yield return new object[] { Form.Void, Form.Mark, Form.Mark };
        yield return new object[] { Form.Void, Form.Void, Form.Void };
    }

    [Theory]
    [MemberData(nameof(OrBooleanLogicData))]
    public void Or_FollowsBooleanLogicForCertainValues(Form left, Form right, Form expected)
    {
        left.Or(right).Should().Be(expected);
    }

    public static IEnumerable<object[]> OrImaginaryData()
    {
        // When either side is Mark, Mark wins (Mark OR anything = Mark)
        // So only test cases where Imaginary propagates are Void+Imaginary and Imaginary+Imaginary
        yield return new object[] { Form.Void, Form.Imaginary };
        yield return new object[] { Form.Imaginary, Form.Void };
        yield return new object[] { Form.Imaginary, Form.Imaginary };
    }

    [Theory]
    [MemberData(nameof(OrImaginaryData))]
    public void Or_ImaginaryPropagates(Form left, Form right)
    {
        left.Or(right).Should().Be(Form.Imaginary);
    }

    [Fact]
    public void Or_MarkWithImaginary_ReturnsMark()
    {
        // Mark OR anything = Mark (Mark takes precedence)
        Form.Mark.Or(Form.Imaginary).Should().Be(Form.Mark);
        Form.Imaginary.Or(Form.Mark).Should().Be(Form.Mark);
    }

    [Fact]
    public void IsCertain_ReturnsTrueForMarkAndVoid()
    {
        Form.Mark.IsCertain().Should().BeTrue();
        Form.Void.IsCertain().Should().BeTrue();
    }

    [Fact]
    public void IsCertain_ReturnsFalseForImaginary()
    {
        Form.Imaginary.IsCertain().Should().BeFalse();
    }

    [Fact]
    public void ToForm_ConvertsBoolCorrectly()
    {
        true.ToForm().Should().Be(Form.Mark);
        false.ToForm().Should().Be(Form.Void);
    }

    [Fact]
    public void ToForm_ConvertsNullableBoolCorrectly()
    {
        ((bool?)true).ToForm().Should().Be(Form.Mark);
        ((bool?)false).ToForm().Should().Be(Form.Void);
        ((bool?)null).ToForm().Should().Be(Form.Imaginary);
    }

    [Fact]
    public void ToBool_ConvertsCertainFormsCorrectly()
    {
        Form.Mark.ToBool().HasValue.Should().BeTrue();
        Form.Mark.ToBool().Value.Should().BeTrue();

        Form.Void.ToBool().HasValue.Should().BeTrue();
        Form.Void.ToBool().Value.Should().BeFalse();
    }

    [Fact]
    public void ToBool_ReturnsNoneForImaginary()
    {
        Form.Imaginary.ToBool().HasValue.Should().BeFalse();
    }

    [Fact]
    public void And_IsCommutative()
    {
        // Property: x AND y = y AND x
        Form.Mark.And(Form.Void).Should().Be(Form.Void.And(Form.Mark));
        Form.Mark.And(Form.Imaginary).Should().Be(Form.Imaginary.And(Form.Mark));
        Form.Void.And(Form.Imaginary).Should().Be(Form.Imaginary.And(Form.Void));
    }

    [Fact]
    public void Or_IsCommutative()
    {
        // Property: x OR y = y OR x
        Form.Mark.Or(Form.Void).Should().Be(Form.Void.Or(Form.Mark));
        Form.Mark.Or(Form.Imaginary).Should().Be(Form.Imaginary.Or(Form.Mark));
        Form.Void.Or(Form.Imaginary).Should().Be(Form.Imaginary.Or(Form.Void));
    }

    [Fact]
    public void And_IsAssociative()
    {
        // Property: (x AND y) AND z = x AND (y AND z)
        var x = Form.Mark;
        var y = Form.Void;
        var z = Form.Imaginary;

        x.And(y).And(z).Should().Be(x.And(y.And(z)));
    }

    [Fact]
    public void Or_IsAssociative()
    {
        // Property: (x OR y) OR z = x OR (y OR z)
        var x = Form.Mark;
        var y = Form.Void;
        var z = Form.Imaginary;

        x.Or(y).Or(z).Should().Be(x.Or(y.Or(z)));
    }

    [Fact]
    public void DeMorgansLaw_HoldsForCertainValues()
    {
        // NOT(x AND y) = (NOT x) OR (NOT y)
        var x = Form.Mark;
        var y = Form.Void;

        x.And(y).Not().Should().Be(x.Not().Or(y.Not()));
    }

    [Fact]
    public void Mark_IsIdentityForAnd()
    {
        // x AND Mark = x
        Form.Mark.And(Form.Mark).Should().Be(Form.Mark);
        Form.Void.And(Form.Mark).Should().Be(Form.Void);
    }

    [Fact]
    public void Void_IsIdentityForOr()
    {
        // x OR Void = x
        Form.Mark.Or(Form.Void).Should().Be(Form.Mark);
        Form.Void.Or(Form.Void).Should().Be(Form.Void);
    }
}
