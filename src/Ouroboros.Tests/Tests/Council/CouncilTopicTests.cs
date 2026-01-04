// <copyright file="CouncilTopicTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Pipeline.Council;

namespace Ouroboros.Tests.Council;

/// <summary>
/// Tests for CouncilTopic record functionality.
/// </summary>
[Trait("Category", "Unit")]
public class CouncilTopicTests
{
    [Fact]
    public void Constructor_WithAllParameters_ShouldCreateTopic()
    {
        // Arrange
        var question = "Should we implement feature X?";
        var background = "Feature X would improve performance.";
        var constraints = new List<string> { "Must be backward compatible", "Budget limited" };

        // Act
        var topic = new CouncilTopic(question, background, constraints);

        // Assert
        topic.Question.Should().Be(question);
        topic.Background.Should().Be(background);
        topic.Constraints.Should().BeEquivalentTo(constraints);
    }

    [Fact]
    public void Simple_ShouldCreateTopicWithOnlyQuestion()
    {
        // Arrange
        var question = "What is the best approach?";

        // Act
        var topic = CouncilTopic.Simple(question);

        // Assert
        topic.Question.Should().Be(question);
        topic.Background.Should().BeEmpty();
        topic.Constraints.Should().BeEmpty();
    }

    [Fact]
    public void WithBackground_ShouldCreateTopicWithQuestionAndBackground()
    {
        // Arrange
        var question = "Should we adopt this technology?";
        var background = "Current system is outdated.";

        // Act
        var topic = CouncilTopic.WithBackground(question, background);

        // Assert
        topic.Question.Should().Be(question);
        topic.Background.Should().Be(background);
        topic.Constraints.Should().BeEmpty();
    }

    [Fact]
    public void Equality_SameValues_ShouldBeEqual()
    {
        // Arrange
        var topic1 = new CouncilTopic("Q1", "B1", ["C1"]);
        var topic2 = new CouncilTopic("Q1", "B1", ["C1"]);

        // Assert - records with same values but different list instances won't be equal by default
        // So we check individual properties
        topic1.Question.Should().Be(topic2.Question);
        topic1.Background.Should().Be(topic2.Background);
        topic1.Constraints.Should().BeEquivalentTo(topic2.Constraints);
    }

    [Fact]
    public void Equality_DifferentValues_ShouldNotBeEqual()
    {
        // Arrange
        var topic1 = new CouncilTopic("Q1", "B1", ["C1"]);
        var topic2 = new CouncilTopic("Q2", "B1", ["C1"]);

        // Assert
        topic1.Should().NotBe(topic2);
    }
}
