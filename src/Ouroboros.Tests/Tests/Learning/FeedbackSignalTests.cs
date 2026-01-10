// <copyright file="FeedbackSignalTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Learning;

using FluentAssertions;
using Ouroboros.Core.Learning;
using Xunit;

/// <summary>
/// Unit tests for FeedbackSignal type.
/// </summary>
[Trait("Category", "Unit")]
public class FeedbackSignalTests
{
    [Fact]
    public void UserCorrection_CreatesFeedbackWithCorrection()
    {
        // Act
        var feedback = FeedbackSignal.UserCorrection("corrected text");

        // Assert
        feedback.Type.Should().Be(FeedbackType.UserCorrection);
        feedback.Score.Should().Be(1.0);
        feedback.Correction.Should().Be("corrected text");
    }

    [Fact]
    public void Success_CreatesSuccessFeedback()
    {
        // Act
        var feedback = FeedbackSignal.Success(0.8);

        // Assert
        feedback.Type.Should().Be(FeedbackType.SuccessSignal);
        feedback.Score.Should().Be(0.8);
        feedback.Correction.Should().BeNull();
    }

    [Fact]
    public void Success_ClampsScoreToRange()
    {
        // Act
        var feedback1 = FeedbackSignal.Success(1.5);
        var feedback2 = FeedbackSignal.Success(-0.5);

        // Assert
        feedback1.Score.Should().Be(1.0);
        feedback2.Score.Should().Be(0.0);
    }

    [Fact]
    public void Failure_CreatesFailureFeedback()
    {
        // Act
        var feedback = FeedbackSignal.Failure(-0.5);

        // Assert
        feedback.Type.Should().Be(FeedbackType.FailureSignal);
        feedback.Score.Should().Be(-0.5);
    }

    [Fact]
    public void Failure_ClampsScoreToRange()
    {
        // Act
        var feedback1 = FeedbackSignal.Failure(0.5);
        var feedback2 = FeedbackSignal.Failure(-1.5);

        // Assert
        feedback1.Score.Should().Be(0.0);
        feedback2.Score.Should().Be(-1.0);
    }

    [Fact]
    public void Preference_CreatesPreferenceFeedback()
    {
        // Act
        var feedback = FeedbackSignal.Preference(0.7);

        // Assert
        feedback.Type.Should().Be(FeedbackType.PreferenceRanking);
        feedback.Score.Should().Be(0.7);
    }

    [Fact]
    public void Validate_WithValidFeedback_ReturnsSuccess()
    {
        // Arrange
        var feedback = FeedbackSignal.Success(0.8);

        // Act
        var result = feedback.Validate();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithScoreTooHigh_ReturnsFailure()
    {
        // Arrange
        var feedback = new FeedbackSignal(FeedbackType.SuccessSignal, 1.5);

        // Act
        var result = feedback.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Score must be between -1.0 and 1.0");
    }

    [Fact]
    public void Validate_WithScoreTooLow_ReturnsFailure()
    {
        // Arrange
        var feedback = new FeedbackSignal(FeedbackType.FailureSignal, -1.5);

        // Act
        var result = feedback.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Score must be between -1.0 and 1.0");
    }

    [Fact]
    public void Validate_UserCorrectionWithoutCorrectionText_ReturnsFailure()
    {
        // Arrange
        var feedback = new FeedbackSignal(FeedbackType.UserCorrection, 1.0, null);

        // Act
        var result = feedback.Validate();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("User correction requires correction text");
    }
}
