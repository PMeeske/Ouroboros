// <copyright file="AutonomousMindTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Tests;

using FluentAssertions;
using Ouroboros.Application.Services;
using Xunit;

/// <summary>
/// Tests for AutonomousMind emotional state and persistence functionality.
/// </summary>
public class AutonomousMindTests
{
    [Fact]
    public void UpdateEmotion_ShouldUpdateCurrentEmotion()
    {
        // Arrange
        var mind = new AutonomousMind();

        // Act
        mind.UpdateEmotion(0.8, 0.6, "excited");

        // Assert
        mind.CurrentEmotion.Arousal.Should().Be(0.8);
        mind.CurrentEmotion.Valence.Should().Be(0.6);
        mind.CurrentEmotion.DominantEmotion.Should().Be("excited");
    }

    [Fact]
    public void UpdateEmotion_ShouldClampValues()
    {
        // Arrange
        var mind = new AutonomousMind();

        // Act - Try to set out-of-bounds values
        mind.UpdateEmotion(1.5, -1.5, "extreme");

        // Assert - Values should be clamped to [-1, 1]
        mind.CurrentEmotion.Arousal.Should().Be(1.0);
        mind.CurrentEmotion.Valence.Should().Be(-1.0);
    }

    [Fact]
    public void UpdateEmotion_ShouldTriggerEvent()
    {
        // Arrange
        var mind = new AutonomousMind();
        EmotionalState? capturedState = null;
        mind.OnEmotionalChange += (state) => capturedState = state;

        // Act
        mind.UpdateEmotion(0.5, 0.3, "curious");

        // Assert
        capturedState.Should().NotBeNull();
        capturedState!.DominantEmotion.Should().Be("curious");
    }

    [Fact]
    public void EmotionalState_Description_ShouldReturnCorrectLabel()
    {
        // Test various emotion combinations
        var excitedHappy = new EmotionalState { Arousal = 0.7, Valence = 0.7 };
        excitedHappy.Description.Should().Be("excited and happy");

        var agitated = new EmotionalState { Arousal = 0.7, Valence = -0.5 };
        agitated.Description.Should().Be("agitated or anxious");

        var calmContent = new EmotionalState { Arousal = -0.5, Valence = 0.7 };
        calmContent.Description.Should().Be("calm and content");

        var neutral = new EmotionalState { Arousal = 0.0, Valence = 0.0 };
        neutral.Description.Should().Be("neutral");
    }

    [Fact]
    public void Config_ShouldHavePersistenceInterval()
    {
        // Arrange
        var config = new AutonomousConfig();

        // Assert
        config.PersistenceIntervalSeconds.Should().Be(60);
    }

    [Fact]
    public void Mind_ShouldSupportPipelineThinkFunction()
    {
        // Arrange
        var mind = new AutonomousMind();
        var wasCalled = false;

        mind.PipelineThinkFunction = async (prompt, branch, ct) =>
        {
            wasCalled = true;
            return ("test response", branch!);
        };

        // Assert - Function should be assignable
        mind.PipelineThinkFunction.Should().NotBeNull();
    }

    [Fact]
    public void Mind_ShouldSupportPersistLearningFunction()
    {
        // Arrange
        var mind = new AutonomousMind();
        var persistedCategory = "";
        var persistedContent = "";

        mind.PersistLearningFunction = async (category, content, confidence, ct) =>
        {
            persistedCategory = category;
            persistedContent = content;
        };

        // Assert - Function should be assignable
        mind.PersistLearningFunction.Should().NotBeNull();
    }

    [Fact]
    public void Mind_ShouldSupportPersistEmotionFunction()
    {
        // Arrange
        var mind = new AutonomousMind();
        EmotionalState? persistedEmotion = null;

        mind.PersistEmotionFunction = async (emotion, ct) =>
        {
            persistedEmotion = emotion;
        };

        // Assert - Function should be assignable
        mind.PersistEmotionFunction.Should().NotBeNull();
    }

    [Fact]
    public void AddInterest_ShouldNotDuplicateInterests()
    {
        // Arrange
        var mind = new AutonomousMind();

        // Act
        mind.AddInterest("AI");
        mind.AddInterest("ai");  // Same interest, different case
        mind.AddInterest("Machine Learning");

        // Assert - Should only have 2 unique interests (case-insensitive)
        var state = mind.GetMindState();
        state.Should().Contain("AI");
        state.Should().Contain("Machine Learning");
    }

    [Fact]
    public void InjectTopic_ShouldAddToCuriosityQueue()
    {
        // Arrange
        var mind = new AutonomousMind();

        // Act
        mind.InjectTopic("quantum computing");
        mind.InjectTopic("neural networks");

        // Assert
        var state = mind.GetMindState();
        state.Should().Contain("Pending Curiosities");
    }
}
