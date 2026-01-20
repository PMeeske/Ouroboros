// <copyright file="FormExtensionsTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.LawsOfForm;

using FluentAssertions;
using Ouroboros.Core.LawsOfForm;
using Ouroboros.Core.Monads;
using Xunit;

/// <summary>
/// Tests for FormExtensions utility methods.
/// </summary>
[Trait("Category", "Unit")]
public class FormExtensionsTests
{
    [Fact]
    public void ToForm_True_ReturnsMark()
    {
        // Act
        var form = true.ToForm();

        // Assert
        form.IsMark().Should().BeTrue();
    }

    [Fact]
    public void ToForm_False_ReturnsVoid()
    {
        // Act
        var form = false.ToForm();

        // Assert
        form.IsVoid().Should().BeTrue();
    }

    [Fact]
    public void ToForm_HighConfidence_ReturnsMark()
    {
        // Act
        var form = 0.9.ToForm(highThreshold: 0.8, lowThreshold: 0.3);

        // Assert
        form.IsMark().Should().BeTrue();
    }

    [Fact]
    public void ToForm_LowConfidence_ReturnsVoid()
    {
        // Act
        var form = 0.2.ToForm(highThreshold: 0.8, lowThreshold: 0.3);

        // Assert
        form.IsVoid().Should().BeTrue();
    }

    [Fact]
    public void ToForm_MediumConfidence_ReturnsImaginary()
    {
        // Act
        var form = 0.5.ToForm(highThreshold: 0.8, lowThreshold: 0.3);

        // Assert
        form.IsImaginary().Should().BeTrue();
    }

    [Fact]
    public void ToForm_NullableWithValue_ReturnsMark()
    {
        // Arrange
        int? value = 42;

        // Act
        var form = value.ToForm();

        // Assert
        form.IsMark().Should().BeTrue();
    }

    [Fact]
    public void ToForm_NullableWithoutValue_ReturnsVoid()
    {
        // Arrange
        int? value = null;

        // Act
        var form = value.ToForm();

        // Assert
        form.IsVoid().Should().BeTrue();
    }

    [Fact]
    public void ToFormRef_NonNull_ReturnsMark()
    {
        // Arrange
        string? value = "test";

        // Act
        var form = value.ToFormRef();

        // Assert
        form.IsMark().Should().BeTrue();
    }

    [Fact]
    public void ToFormRef_Null_ReturnsVoid()
    {
        // Arrange
        string? value = null;

        // Act
        var form = value.ToFormRef();

        // Assert
        form.IsVoid().Should().BeTrue();
    }

    [Fact]
    public void All_AllMarked_ReturnsMark()
    {
        // Arrange
        var forms = new[] { Form.Mark, Form.Mark, Form.Mark };

        // Act
        var result = FormExtensions.All(forms);

        // Assert
        result.IsMark().Should().BeTrue();
    }

    [Fact]
    public void All_OneVoid_ReturnsVoid()
    {
        // Arrange
        var forms = new[] { Form.Mark, Form.Void, Form.Mark };

        // Act
        var result = FormExtensions.All(forms);

        // Assert
        result.IsVoid().Should().BeTrue();
    }

    [Fact]
    public void All_OneImaginary_ReturnsImaginary()
    {
        // Arrange
        var forms = new[] { Form.Mark, Form.Imaginary, Form.Mark };

        // Act
        var result = FormExtensions.All(forms);

        // Assert
        result.IsImaginary().Should().BeTrue();
    }

    [Fact]
    public void All_Empty_ReturnsMark()
    {
        // Act
        var result = FormExtensions.All();

        // Assert
        result.IsMark().Should().BeTrue();
    }

    [Fact]
    public void Any_OneMark_ReturnsMark()
    {
        // Arrange
        var forms = new[] { Form.Void, Form.Mark, Form.Void };

        // Act
        var result = FormExtensions.Any(forms);

        // Assert
        result.IsMark().Should().BeTrue();
    }

    [Fact]
    public void Any_AllVoid_ReturnsVoid()
    {
        // Arrange
        var forms = new[] { Form.Void, Form.Void, Form.Void };

        // Act
        var result = FormExtensions.Any(forms);

        // Assert
        result.IsVoid().Should().BeTrue();
    }

    [Fact]
    public void Any_ImaginaryWithoutMark_ReturnsImaginary()
    {
        // Arrange
        var forms = new[] { Form.Void, Form.Imaginary, Form.Void };

        // Act
        var result = FormExtensions.Any(forms);

        // Assert
        result.IsImaginary().Should().BeTrue();
    }

    [Fact]
    public void Any_Empty_ReturnsVoid()
    {
        // Act
        var result = FormExtensions.Any();

        // Assert
        result.IsVoid().Should().BeTrue();
    }

    [Fact]
    public void Superposition_StrongMarkConsensus_ReturnsMark()
    {
        // Arrange
        var opinions = new[]
        {
            (Form.Mark, 3.0),
            (Form.Mark, 2.0),
            (Form.Void, 1.0)
        };

        // Act
        var result = FormExtensions.Superposition(opinions);

        // Assert
        result.IsMark().Should().BeTrue();
    }

    [Fact]
    public void Superposition_StrongVoidConsensus_ReturnsVoid()
    {
        // Arrange
        var opinions = new[]
        {
            (Form.Void, 3.0),
            (Form.Void, 2.0),
            (Form.Mark, 1.0)
        };

        // Act
        var result = FormExtensions.Superposition(opinions);

        // Assert
        result.IsVoid().Should().BeTrue();
    }

    [Fact]
    public void Superposition_MixedWithoutConsensus_ReturnsImaginary()
    {
        // Arrange
        var opinions = new[]
        {
            (Form.Mark, 1.0),
            (Form.Void, 1.0)
        };

        // Act
        var result = FormExtensions.Superposition(opinions);

        // Assert
        result.IsImaginary().Should().BeTrue();
    }

    [Fact]
    public void Superposition_WithImaginary_ReturnsImaginary()
    {
        // Arrange
        var opinions = new[]
        {
            (Form.Mark, 3.0),
            (Form.Imaginary, 1.0)
        };

        // Act
        var result = FormExtensions.Superposition(opinions);

        // Assert
        result.IsImaginary().Should().BeTrue();
    }

    [Fact]
    public void ToResult_Mark_ReturnsSuccess()
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
    public void ToResult_Void_ReturnsFailure()
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
    public void ToResult_Imaginary_ReturnsFailureWithUncertaintyMessage()
    {
        // Arrange
        var form = Form.Imaginary;

        // Act
        var result = form.ToResult(42, "error");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Uncertain");
    }

    [Fact]
    public void ToOption_Mark_ReturnsSome()
    {
        // Arrange
        var form = Form.Mark;

        // Act
        var option = form.ToOption(42);

        // Assert
        option.HasValue.Should().BeTrue();
        option.Value.Should().Be(42);
    }

    [Fact]
    public void ToOption_Void_ReturnsNone()
    {
        // Arrange
        var form = Form.Void;

        // Act
        var option = form.ToOption(42);

        // Assert
        option.HasValue.Should().BeFalse();
    }

    [Fact]
    public void ToOption_Imaginary_ReturnsNone()
    {
        // Arrange
        var form = Form.Imaginary;

        // Act
        var option = form.ToOption(42);

        // Assert
        option.HasValue.Should().BeFalse();
    }
}
