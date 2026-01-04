// <copyright file="InputValidatorTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests;

using FluentAssertions;
using Ouroboros.Core.Security;
using Xunit;

/// <summary>
/// Tests for input validation and sanitization functionality.
/// </summary>
[Trait("Category", "Unit")]
public class InputValidatorTests
{
    [Fact]
    public void ValidateAndSanitize_ValidInput_ReturnsSuccess()
    {
        // Arrange
        var validator = new InputValidator();
        var input = "This is a valid input";
        var context = ValidationContext.Default;

        // Act
        var result = validator.ValidateAndSanitize(input, context);

        // Assert
        result.IsValid.Should().BeTrue();
        result.SanitizedValue.Should().Be(input);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ValidateAndSanitize_EmptyInput_WhenNotAllowed_ReturnsFailure()
    {
        // Arrange
        var validator = new InputValidator();
        var input = string.Empty;
        var context = new ValidationContext { AllowEmpty = false };

        // Act
        var result = validator.ValidateAndSanitize(input, context);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain("Input cannot be empty");
    }

    [Fact]
    public void ValidateAndSanitize_EmptyInput_WhenAllowed_ReturnsSuccess()
    {
        // Arrange
        var validator = new InputValidator();
        var input = string.Empty;
        var context = new ValidationContext { AllowEmpty = true };

        // Act
        var result = validator.ValidateAndSanitize(input, context);

        // Assert
        result.IsValid.Should().BeTrue();
        result.SanitizedValue.Should().Be(string.Empty);
    }

    [Fact]
    public void ValidateAndSanitize_InputTooLong_ReturnsFailure()
    {
        // Arrange
        var validator = new InputValidator();
        var input = new string('a', 101);
        var context = new ValidationContext { MaxLength = 100 };

        // Act
        var result = validator.ValidateAndSanitize(input, context);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("exceeds maximum length"));
    }

    [Fact]
    public void ValidateAndSanitize_InputTooShort_ReturnsFailure()
    {
        // Arrange
        var validator = new InputValidator();
        var input = "ab";
        var context = new ValidationContext { MinLength = 5 };

        // Act
        var result = validator.ValidateAndSanitize(input, context);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("must be at least"));
    }

    [Theory]
    [InlineData("'; DROP TABLE users--")]
    [InlineData("1' OR '1'='1")]
    [InlineData("UNION SELECT * FROM passwords")]
    public void ValidateAndSanitize_SqlInjectionPatterns_ReturnsFailure(string maliciousInput)
    {
        // Arrange
        var validator = new InputValidator();
        var context = ValidationContext.Default;

        // Act
        var result = validator.ValidateAndSanitize(maliciousInput, context);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("SQL injection"));
    }

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("javascript:alert('xss')")]
    [InlineData("<iframe src='evil.com'></iframe>")]
    public void ValidateAndSanitize_ScriptInjectionPatterns_ReturnsFailure(string maliciousInput)
    {
        // Arrange
        var validator = new InputValidator();
        var context = ValidationContext.Default;

        // Act
        var result = validator.ValidateAndSanitize(maliciousInput, context);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("script injection"));
    }

    [Theory]
    [InlineData("cat /etc/passwd")]
    [InlineData("cmd.exe && dir")]
    [InlineData("$(whoami)")]
    public void ValidateAndSanitize_CommandInjectionPatterns_ReturnsFailure(string maliciousInput)
    {
        // Arrange
        var validator = new InputValidator();
        var context = ValidationContext.Default;

        // Act
        var result = validator.ValidateAndSanitize(maliciousInput, context);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("command injection"));
    }

    [Fact]
    public void ValidateAndSanitize_WithControlCharacters_ReturnsFailure()
    {
        // Arrange
        var validator = new InputValidator();
        var input = "Hello\0World";
        var context = ValidationContext.Default;

        // Act
        var result = validator.ValidateAndSanitize(input, context);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("null bytes"));
    }

    [Fact]
    public void ValidateAndSanitize_WithBlockedCharacters_ReturnsFailure()
    {
        // Arrange
        var validator = new InputValidator();
        var input = "Hello<World>";
        var context = new ValidationContext
        {
            BlockedCharacters = new HashSet<char> { '<', '>' },
        };

        // Act
        var result = validator.ValidateAndSanitize(input, context);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.Contains("blocked character"));
    }

    [Fact]
    public void ValidateAndSanitize_TrimsWhitespace_WhenEnabled()
    {
        // Arrange
        var validator = new InputValidator();
        var input = "  Hello World  ";
        var context = new ValidationContext { TrimWhitespace = true };

        // Act
        var result = validator.ValidateAndSanitize(input, context);

        // Assert
        result.IsValid.Should().BeTrue();
        result.SanitizedValue.Should().Be("Hello World");
    }

    [Fact]
    public void ValidateAndSanitize_NormalizesLineEndings_WhenEnabled()
    {
        // Arrange
        var validator = new InputValidator();
        var input = "Line1\r\nLine2\rLine3";
        var context = new ValidationContext { NormalizeLineEndings = true };

        // Act
        var result = validator.ValidateAndSanitize(input, context);

        // Assert
        result.IsValid.Should().BeTrue();
        result.SanitizedValue.Should().Be("Line1\nLine2\nLine3");
    }

    [Fact]
    public void ValidateAndSanitize_EscapesHtml_WhenEnabled()
    {
        // Arrange
        var validator = new InputValidator(ValidationOptions.Lenient);
        var input = "<div>Hello & goodbye</div>";
        var context = new ValidationContext { EscapeHtml = true };

        // Act
        var result = validator.ValidateAndSanitize(input, context);

        // Assert
        result.IsValid.Should().BeTrue();
        result.SanitizedValue.Should().Contain("&lt;");
        result.SanitizedValue.Should().Contain("&gt;");
        result.SanitizedValue.Should().Contain("&amp;");
    }

    [Fact]
    public void ValidationContext_Strict_HasStrictSettings()
    {
        // Arrange & Act
        var context = ValidationContext.Strict;

        // Assert
        context.MaxLength.Should().Be(1000);
        context.EscapeHtml.Should().BeTrue();
        context.BlockedCharacters.Should().NotBeNull();
        context.BlockedCharacters!.Should().Contain(new[] { '<', '>', '&', '"', '\'' });
    }

    [Fact]
    public void ValidationContext_ToolParameter_HasReasonableDefaults()
    {
        // Arrange & Act
        var context = ValidationContext.ToolParameter;

        // Assert
        context.MaxLength.Should().Be(5000);
        context.TrimWhitespace.Should().BeTrue();
        context.NormalizeLineEndings.Should().BeTrue();
    }
}
