// <copyright file="TaskDetectorTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using FluentAssertions;
using Ouroboros.Providers.Routing;
using Xunit;

namespace Ouroboros.Tests.Providers.Routing;

/// <summary>
/// Unit tests for TaskDetector class.
/// Validates task type detection from various prompt types.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TaskDetectorTests
{
    [Fact]
    public void DetectTaskType_WithSimplePrompt_ReturnsSimple()
    {
        // Arrange
        string prompt = "Hello, how are you?";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt);

        // Assert
        result.Should().Be(TaskType.Simple);
    }

    [Fact]
    public void DetectTaskType_WithReasoningPrompt_ReturnsReasoning()
    {
        // Arrange
        string prompt = "Explain why neural networks are effective for pattern recognition.";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt);

        // Assert
        result.Should().Be(TaskType.Reasoning);
    }

    [Fact]
    public void DetectTaskType_WithPlanningPrompt_ReturnsPlanning()
    {
        // Arrange
        string prompt = "Create a step-by-step plan for implementing a machine learning pipeline.";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt);

        // Assert
        result.Should().Be(TaskType.Planning);
    }

    [Fact]
    public void DetectTaskType_WithCodingPrompt_ReturnsCoding()
    {
        // Arrange
        string prompt = "Write a function in Python that implements quicksort algorithm.";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt);

        // Assert
        result.Should().Be(TaskType.Coding);
    }

    [Fact]
    public void DetectTaskType_WithCodeBlock_ReturnsCoding()
    {
        // Arrange
        string prompt = @"
Fix this code:
```python
def hello():
    print('Hello World')
```
";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt);

        // Assert
        result.Should().Be(TaskType.Coding);
    }

    [Fact]
    public void DetectTaskType_WithWhyQuestion_ReturnsReasoning()
    {
        // Arrange
        string prompt = "Why does water boil at lower temperatures at higher altitudes?";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt);

        // Assert
        result.Should().Be(TaskType.Reasoning);
    }

    [Fact]
    public void DetectTaskType_WithAnalyzeRequest_ReturnsReasoning()
    {
        // Arrange
        string prompt = "Analyze the impact of climate change on agriculture.";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt);

        // Assert
        result.Should().Be(TaskType.Reasoning);
    }

    [Fact]
    public void DetectTaskType_WithHowToQuestion_ReturnsPlanning()
    {
        // Arrange
        string prompt = "How to build a scalable microservices architecture?";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt);

        // Assert
        result.Should().Be(TaskType.Planning);
    }

    [Fact]
    public void DetectTaskType_WithListStructure_ReturnsPlanning()
    {
        // Arrange
        string prompt = @"
Create a plan with these steps:
1. Requirements gathering
2. System design
3. Implementation
4. Testing
";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt);

        // Assert
        result.Should().Be(TaskType.Planning);
    }

    [Fact]
    public void DetectTaskType_WithEmptyPrompt_ReturnsSimple()
    {
        // Arrange
        string prompt = "";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt);

        // Assert
        result.Should().Be(TaskType.Simple);
    }

    [Theory]
    [InlineData(TaskDetectionStrategy.Heuristic)]
    [InlineData(TaskDetectionStrategy.Hybrid)]
    public void DetectTaskType_WithDifferentStrategies_WorksCorrectly(TaskDetectionStrategy strategy)
    {
        // Arrange
        string prompt = "Explain the reasoning behind quantum mechanics.";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt, strategy);

        // Assert
        result.Should().Be(TaskType.Reasoning);
    }

    [Fact]
    public void DetectTaskType_WithRuleBasedStrategy_DetectsReasoningFromWhy()
    {
        // Arrange - RuleBased strategy detects reasoning from "why" questions
        string prompt = "Why does the sun rise in the east?";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt, TaskDetectionStrategy.RuleBased);

        // Assert
        result.Should().Be(TaskType.Reasoning);
    }

    [Fact]
    public void DetectTaskType_WithImplementRequest_ReturnsCoding()
    {
        // Arrange
        string prompt = "Implement a REST API endpoint for user authentication.";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt);

        // Assert
        result.Should().Be(TaskType.Coding);
    }

    [Fact]
    public void DetectTaskType_WithMultipleKeywords_SelectsMostRelevant()
    {
        // Arrange - Has both reasoning and planning keywords, but more planning context
        string prompt = "Plan the steps to analyze and decompose a complex reasoning problem.";

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt);

        // Assert
        // Should detect planning due to stronger planning keywords
        result.Should().BeOneOf(TaskType.Planning, TaskType.Reasoning);
    }

    [Fact]
    public void DetectTaskType_WithLongTextNoKeywords_ReturnsUnknown()
    {
        // Arrange - Long text without specific task keywords
        string prompt = new string('x', 600); // 600 chars of 'x'

        // Act
        TaskType result = TaskDetector.DetectTaskType(prompt);

        // Assert
        result.Should().Be(TaskType.Unknown);
    }
}
