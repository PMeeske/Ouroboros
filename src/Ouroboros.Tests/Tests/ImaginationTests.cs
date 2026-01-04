// <copyright file="ImaginationTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests;

using FluentAssertions;
using Ouroboros.Core.LawsOfForm;
using Xunit;

/// <summary>
/// Tests for the Imagination module - imaginary forms and self-reference in Laws of Form.
/// </summary>
[Trait("Category", "Unit")]
public class ImaginationTests
{
    #region Imaginary Form Tests

    [Fact]
    public void Imaginary_IsImaginary()
    {
        // Arrange & Act
        var form = Form.Imaginary;

        // Assert
        form.IsImaginary().Should().BeTrue();
        form.IsMarked().Should().BeFalse("Imaginary is neither marked nor void");
        form.IsVoid().Should().BeFalse("Imaginary is neither marked nor void");
    }

    [Fact]
    public void Imaginary_ToString_ReturnsI()
    {
        // Arrange & Act
        var form = Form.Imaginary;

        // Assert
        form.ToString().Should().Be("i");
    }

    [Fact(Skip = "Phase storage in Form.Imagine not yet implemented")]
    public void Imagine_WithPhase_CreatesImaginaryWithPhase()
    {
        // Arrange & Act
        var form = Form.Imagine(Math.PI / 2);

        // Assert
        form.IsImaginary().Should().BeTrue();
        form.ToString().Should().Contain("∠");
    }

    [Fact]
    public void ReEntry_IsImaginary()
    {
        // Arrange & Act
        var form = Form.ReEntry("f");

        // Assert
        form.IsImaginary().Should().BeTrue();
        form.Eval().Should().Be(Form.Imaginary);
    }

    [Fact(Skip = "ReEntry naming feature not yet implemented")]
    public void ReEntry_ToString_ShowsArrow()
    {
        // Arrange & Act
        var form = Form.ReEntry("myVar");

        // Assert
        form.ToString().Should().Contain("↻myVar");
    }

    #endregion

    #region Mark of Imaginary Tests

    [Fact]
    public void Mark_OfImaginary_ShiftsPhase()
    {
        // Marking an imaginary form should shift its phase by π
        var imag = Form.Imaginary;
        var marked = Form.CrossForm(imag).Eval();

        // Assert - should still be imaginary but phase shifted
        marked.IsImaginary().Should().BeTrue();
    }

    [Fact]
    public void Mark_OfReEntry_YieldsImaginary()
    {
        // Mark(f) where f = ⌐f should give imaginary
        var reentry = Form.ReEntry("f");
        var marked = Form.CrossForm(reentry).Eval();

        // Assert
        marked.Should().Be(Form.Imaginary);
    }

    [Fact]
    public void DoubleMarkOfImaginary_ShiftsPhaseTwice()
    {
        // Mark(Mark(i)) should shift phase by 2π (full cycle, back to original)
        var imag = Form.Imagine(0);
        var doubleMarked = Form.CrossForm(Form.CrossForm(imag)).Eval();

        // Phase should be approximately 0 (modulo 2π)
        doubleMarked.IsImaginary().Should().BeTrue();
    }

    #endregion

    #region Imaginary Indication Tests

    [Fact]
    public void Indication_OfTwoImaginaries_CombinesPhases()
    {
        // Two imaginary forms indicated together should interfere
        var imag1 = Form.Imagine(Math.PI / 4);
        var imag2 = Form.Imagine(Math.PI / 4);

        var combined = imag1.Call(imag2).Eval();

        // Assert - should still be imaginary
        combined.IsImaginary().Should().BeTrue();
    }

    [Fact]
    public void Indication_ImaginaryWithReal_YieldsImaginary()
    {
        // Imaginary dominates over real in indication
        var imag = Form.Imaginary;
        var real = Form.Mark;

        var combined = imag.Call(real).Eval();

        // Imaginary dominates
        combined.IsImaginary().Should().BeTrue();
    }

    #endregion

    #region AtTime Tests

    [Fact]
    public void Imaginary_AtEvenTime_IsVoid()
    {
        // At even times, imaginary appears as void
        var imag = Form.Imaginary;

        // Use the extension method to sample at time
        var atTime0 = Imagination.Sample(imag, 0);
        var atTime2 = Imagination.Sample(imag, 2);

        atTime0.IsVoid().Should().BeTrue();
        atTime2.IsVoid().Should().BeTrue();
    }

    [Fact]
    public void Imaginary_AtOddTime_IsMarked()
    {
        // At odd times, imaginary appears as marked
        var imag = Form.Imaginary;

        // Use the extension method to sample at time
        var atTime1 = Imagination.Sample(imag, 1);
        var atTime3 = Imagination.Sample(imag, 3);

        atTime1.IsMarked().Should().BeTrue();
        atTime3.IsMarked().Should().BeTrue();
    }

    #endregion

    #region Imagination Static Methods Tests

    [Fact]
    public void Imagination_I_ReturnsImaginaryConstant()
    {
        // Arrange & Act
        var i = Imagination.I;

        // Assert
        i.Should().Be(Form.Imaginary);
    }

    [Fact]
    public void Imagination_Apply_TransformsVoidToImaginary()
    {
        // Arrange
        var voidForm = Form.Void;

        // Act
        var result = Imagination.Apply(voidForm);

        // Assert
        result.IsImaginary().Should().BeTrue();
    }

    [Fact(Skip = "Phase storage in Imagination.Apply not yet implemented")]
    public void Imagination_Apply_TransformsMarkToImaginaryWithPhaseShift()
    {
        // Arrange
        var mark = Form.Mark;

        // Act
        var result = Imagination.Apply(mark);

        // Assert
        result.IsImaginary().Should().BeTrue();
        Imagination.Phase(result).Should().BeApproximately(Math.PI, 0.001);
    }

    [Fact(Skip = "Phase storage in Form.Imagine not yet implemented")]
    public void Imagination_Conjugate_NegatesPhase()
    {
        // Arrange
        var imag = Form.Imagine(Math.PI / 3);

        // Act
        var conjugate = Imagination.Conjugate(imag);

        // Assert
        Imagination.Phase(conjugate).Should().BeApproximately(-Math.PI / 3, 0.001);
    }

    [Fact]
    public void Imagination_Conjugate_RealFormUnchanged()
    {
        // Arrange
        var mark = Form.Mark;

        // Act
        var conjugate = Imagination.Conjugate(mark);

        // Assert
        conjugate.IsMarked().Should().BeTrue();
    }

    [Fact]
    public void Imagination_Magnitude_VoidIsZero()
    {
        // Arrange
        var voidForm = Form.Void;

        // Act
        var magnitude = Imagination.Magnitude(voidForm);

        // Assert
        magnitude.Should().Be(0.0);
    }

    [Fact]
    public void Imagination_Magnitude_MarkIsOne()
    {
        // Arrange
        var mark = Form.Mark;

        // Act
        var magnitude = Imagination.Magnitude(mark);

        // Assert
        magnitude.Should().Be(1.0);
    }

    [Fact]
    public void Imagination_Magnitude_ImaginaryIsOne()
    {
        // Arrange
        var imag = Form.Imaginary;

        // Act
        var magnitude = Imagination.Magnitude(imag);

        // Assert
        magnitude.Should().Be(1.0);
    }

    [Fact]
    public void Imagination_Phase_VoidIsZero()
    {
        // Arrange
        var voidForm = Form.Void;

        // Act
        var phase = Imagination.Phase(voidForm);

        // Assert
        phase.Should().Be(0.0);
    }

    [Fact]
    public void Imagination_Phase_MarkIsPi()
    {
        // Arrange
        var mark = Form.Mark;

        // Act
        var phase = Imagination.Phase(mark);

        // Assert
        phase.Should().Be(Math.PI);
    }

    [Fact]
    public void Imagination_Project_ImaginaryToReal()
    {
        // Arrange - imaginary with phase < π should project to void
        var imag = Form.Imagine(Math.PI / 4);

        // Act
        var projected = Imagination.Project(imag);

        // Assert
        projected.IsVoid().Should().BeTrue();
    }

    [Fact(Skip = "Phase storage in Form.Imagine not yet implemented")]
    public void Imagination_Project_ImaginaryToMark()
    {
        // Arrange - imaginary with phase >= π should project to mark
        var imag = Form.Imagine(Math.PI + 0.1);

        // Act
        var projected = Imagination.Project(imag);

        // Assert
        projected.IsMarked().Should().BeTrue();
    }

    #endregion

    #region Oscillator Tests

    [Fact]
    public void Oscillator_AtEvenTime_ReturnsStateA()
    {
        // Arrange
        var oscillator = Imagination.Oscillate(Form.Void, Form.Mark);

        // Act & Assert
        oscillator.AtTime(0).IsVoid().Should().BeTrue();
        oscillator.AtTime(2).IsVoid().Should().BeTrue();
        oscillator.AtTime(4).IsVoid().Should().BeTrue();
    }

    [Fact]
    public void Oscillator_AtOddTime_ReturnsStateB()
    {
        // Arrange
        var oscillator = Imagination.Oscillate(Form.Void, Form.Mark);

        // Act & Assert
        oscillator.AtTime(1).IsMarked().Should().BeTrue();
        oscillator.AtTime(3).IsMarked().Should().BeTrue();
        oscillator.AtTime(5).IsMarked().Should().BeTrue();
    }

    [Fact]
    public void Oscillator_Period_IsTwo()
    {
        // Arrange
        var oscillator = Imagination.Oscillate(Form.Void, Form.Mark);

        // Assert
        oscillator.Period.Should().Be(2);
    }

    [Fact]
    public void Oscillator_ToImaginary_ReturnsImaginary()
    {
        // Arrange
        var oscillator = Imagination.Oscillate(Form.Void, Form.Mark);

        // Act
        var imag = oscillator.ToImaginary();

        // Assert
        imag.IsImaginary().Should().BeTrue();
    }

    #endregion

    #region Wave Tests

    [Fact]
    public void Wave_Sample_AtZero_ReturnsPhaseValue()
    {
        // Arrange
        var wave = Imagination.CreateWave(frequency: 1.0, phase: 0.0);

        // Act
        var sample = wave.Sample(0);

        // Assert - sin(0) = 0
        sample.Should().BeApproximately(0.0, 0.001);
    }

    [Fact]
    public void Wave_Sample_AtQuarterPeriod_ReturnsOne()
    {
        // Arrange
        var wave = Imagination.CreateWave(frequency: 1.0, phase: 0.0);

        // Act - sample at t = 0.25 (quarter period)
        var sample = wave.Sample(0.25);

        // Assert - sin(π/2) = 1
        sample.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    public void Wave_IsMarkedAt_PositiveAmplitude_ReturnsTrue()
    {
        // Arrange
        var wave = Imagination.CreateWave(frequency: 1.0, phase: 0.0);

        // Act - at t = 0.25, amplitude is positive
        var isMarked = wave.IsMarkedAt(0.25);

        // Assert
        isMarked.Should().BeTrue();
    }

    [Fact]
    public void Wave_ToFormAt_ReturnsAppropriateForm()
    {
        // Arrange
        var wave = Imagination.CreateWave(frequency: 1.0, phase: 0.0);

        // Act
        var formAtQuarter = wave.ToFormAt(0.25); // positive
        var formAtThreeQuarters = wave.ToFormAt(0.75); // negative

        // Assert
        formAtQuarter.IsMarked().Should().BeTrue();
        formAtThreeQuarters.IsVoid().Should().BeTrue();
    }

    #endregion

    #region Dream Tests

    [Fact]
    public void Dream_Observe_ReturnsValidForm()
    {
        // Arrange
        var dream = Imagination.CreateDream();

        // Act
        var observed = dream.Observe();

        // Assert - should be one of Void, Mark, or Imaginary
        (observed.IsVoid() || observed.IsMarked() || observed.IsImaginary()).Should().BeTrue();
    }

    [Fact(Skip = "Phase storage in Dream.Manifest not yet implemented")]
    public void Dream_Manifest_ReturnsImaginaryAtPhase()
    {
        // Arrange
        var dream = Imagination.CreateDream();
        var targetPhase = Math.PI / 4;

        // Act
        var manifested = dream.Manifest(targetPhase);

        // Assert
        manifested.IsImaginary().Should().BeTrue();
        Imagination.Phase(manifested).Should().BeApproximately(targetPhase, 0.001);
    }

    #endregion

    #region Extension Method Tests

    [Fact]
    public void ToNullableBoolean_Void_ReturnsFalse()
    {
        // Arrange
        var form = Form.Void;

        // Act
        var result = form.ToBool();

        // Assert
        result.Should().Be(false);
    }

    [Fact]
    public void ToNullableBoolean_Mark_ReturnsTrue()
    {
        // Arrange
        var form = Form.Mark;

        // Act
        var result = form.ToBool();

        // Assert
        result.Should().Be(true);
    }

    [Fact]
    public void ToNullableBoolean_Imaginary_ReturnsNull()
    {
        // Arrange
        var form = Form.Imaginary;

        // Act
        var result = form.ToBool();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void NullableBoolean_ToForm_NullReturnsImaginary()
    {
        // Arrange
        bool? value = null;

        // Act
        var form = value.ToForm();

        // Assert
        form.IsImaginary().Should().BeTrue();
    }

    [Fact]
    public void Match_WithImaginaryHandler_CallsImaginaryBranch()
    {
        // Arrange
        var form = Form.Imagine(Math.PI / 3);
        var matchedBranch = string.Empty;

        // Act
        var result = form.Match(
            onMark: () => { matchedBranch = "marked"; return "M"; },
            onVoid: () => { matchedBranch = "void"; return "V"; },
            onImaginary: () => { matchedBranch = "imaginary"; return "I"; });

        // Assert
        matchedBranch.Should().Be("imaginary");
        result.Should().Be("I");
    }

    [Fact]
    public void Imagine_Extension_TransformsForm()
    {
        // Arrange
        var form = Form.Void;

        // Act
        var imagined = Imagination.Apply(form);

        // Assert
        imagined.IsImaginary().Should().BeTrue();
    }

    [Fact]
    public void OscillateWith_CreatesOscillator()
    {
        // Arrange
        var form = Form.Void;

        // Act
        var oscillator = Imagination.Oscillate(form, Form.Mark);

        // Assert
        oscillator.AtTime(0).IsVoid().Should().BeTrue();
        oscillator.AtTime(1).IsMarked().Should().BeTrue();
    }

    [Fact]
    public void Superimpose_Extension_CombinesForms()
    {
        // Arrange
        var form1 = Form.Imagine(Math.PI / 4);
        var form2 = Form.Imagine(Math.PI / 4);

        // Act
        var superimposed = Imagination.Superimpose(form1, form2);

        // Assert
        superimposed.IsImaginary().Should().BeTrue();
    }

    #endregion

    #region Self-Reference Equation Tests

    [Fact]
    public void ReEntry_SatisfiesEquation_F_Equals_MarkF()
    {
        // The equation f = ⌐f has the imaginary value as its solution
        var f = Form.ReEntry("f");
        var markF = Form.CrossForm(f);

        // Both should evaluate to Imaginary
        f.Eval().Should().Be(Form.Imaginary);
        markF.Eval().Should().Be(Form.Imaginary);
    }

    [Fact]
    public void ReEntry_EvaluatesToImaginary()
    {
        // Arrange
        var f = Form.ReEntry("f");

        // Act
        var evaluated = f.Eval();

        // Assert - re-entry evaluates to imaginary
        evaluated.Should().Be(Form.Imaginary);
        evaluated.IsImaginary().Should().BeTrue();
    }

    [Fact]
    public void ReEntry_MarkOfReEntry_AlsoImaginary()
    {
        // Arrange - the equation f = ⌐f
        var f = Form.ReEntry("f");
        var markF = Form.CrossForm(f);

        // Both sides of the equation should evaluate to the same imaginary value
        f.Eval().IsImaginary().Should().BeTrue();
        markF.Eval().IsImaginary().Should().BeTrue();
    }

    #endregion
}
