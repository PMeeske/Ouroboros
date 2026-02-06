// <copyright file="EthicsMessageFilterTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Ouroboros.Application.Autonomous;
using Ouroboros.Core.Ethics;
using Ouroboros.Core.Monads;
using Ouroboros.Domain.Autonomous;
using Ouroboros.Tests.Mocks;
using Xunit;

namespace Ouroboros.Tests.Tests.Autonomous;

/// <summary>
/// Tests for the ethics message filter.
/// </summary>
public sealed class EthicsMessageFilterTests
{
    private readonly Mock<ILogger<EthicsMessageFilter>> _mockLogger;

    public EthicsMessageFilterTests()
    {
        _mockLogger = new Mock<ILogger<EthicsMessageFilter>>();
    }

    [Theory]
    [InlineData("reflection.request")]
    [InlineData("reflection.request.response")]
    [InlineData("health.check")]
    [InlineData("health.check.response")]
    [InlineData("notification.send")]
    public async Task SafeTopics_ShouldBypassEthicsEvaluation(string safeTopic)
    {
        // Arrange
        var mockFramework = new MockEthicsFramework();
        var filter = new EthicsMessageFilter(mockFramework, _mockLogger.Object);
        var message = CreateTestMessage(safeTopic);

        // Act
        var result = await filter.ShouldRouteAsync(message);

        // Assert
        result.Should().BeTrue();
        mockFramework.EvaluateActionCallCount.Should().Be(0, "safe topics should bypass ethics evaluation");
    }

    [Theory]
    [InlineData("some.action.response")]
    [InlineData("dangerous.action.response")]
    [InlineData("system.modify.response")]
    public async Task ResponseTopics_ShouldBypassEthicsEvaluation(string responseTopic)
    {
        // Arrange
        var mockFramework = new MockEthicsFramework();
        var filter = new EthicsMessageFilter(mockFramework, _mockLogger.Object);
        var message = CreateTestMessage(responseTopic);

        // Act
        var result = await filter.ShouldRouteAsync(message);

        // Assert
        result.Should().BeTrue();
        mockFramework.EvaluateActionCallCount.Should().Be(0, "response topics should bypass ethics evaluation");
    }

    [Fact]
    public async Task NonSafeTopic_ShouldTriggerEthicsEvaluation_AndRouteIfPermitted()
    {
        // Arrange
        var mockFramework = new MockEthicsFramework((action, context) =>
        {
            return EthicalClearance.Permitted("Action is permitted");
        });
        var filter = new EthicsMessageFilter(mockFramework, _mockLogger.Object);
        var message = CreateTestMessage("system.modify");

        // Act
        var result = await filter.ShouldRouteAsync(message);

        // Assert
        result.Should().BeTrue();
        mockFramework.EvaluateActionCallCount.Should().Be(1, "non-safe topics should trigger ethics evaluation");
    }

    [Fact]
    public async Task NonSafeTopic_ShouldTriggerEthicsEvaluation_AndBlockIfDenied()
    {
        // Arrange
        var violation = new EthicalViolation
        {
            ViolatedPrinciple = EthicalPrinciple.DoNoHarm,
            Description = "Action would cause harm",
            Severity = ViolationSeverity.High,
            Evidence = "Test evidence",
            AffectedParties = new[] { "Users" }
        };

        var mockFramework = new MockEthicsFramework((action, context) =>
        {
            return EthicalClearance.Denied("Action is denied", new[] { violation });
        });
        var filter = new EthicsMessageFilter(mockFramework, _mockLogger.Object);
        var message = CreateTestMessage("dangerous.action");

        // Act
        var result = await filter.ShouldRouteAsync(message);

        // Assert
        result.Should().BeFalse();
        mockFramework.EvaluateActionCallCount.Should().Be(1);
    }

    [Fact]
    public async Task NonSafeTopic_ShouldTriggerEthicsEvaluation_AndBlockIfRequiresApproval()
    {
        // Arrange
        var concern = new EthicalConcern
        {
            RelatedPrinciple = EthicalPrinciple.HumanOversight,
            Description = "High-risk action",
            Level = ConcernLevel.High,
            RecommendedAction = "Require human approval"
        };

        var mockFramework = new MockEthicsFramework((action, context) =>
        {
            return EthicalClearance.RequiresApproval("Action requires approval", new[] { concern });
        });
        var filter = new EthicsMessageFilter(mockFramework, _mockLogger.Object);
        var message = CreateTestMessage("high.risk.action");

        // Act
        var result = await filter.ShouldRouteAsync(message);

        // Assert
        result.Should().BeFalse();
        mockFramework.EvaluateActionCallCount.Should().Be(1);
    }

    [Fact]
    public async Task NonSafeTopic_WithConcerns_ShouldRouteIfPermittedWithConcerns()
    {
        // Arrange
        var concern = new EthicalConcern
        {
            RelatedPrinciple = EthicalPrinciple.Transparency,
            Description = "Minor concern",
            Level = ConcernLevel.Low,
            RecommendedAction = "Monitor action"
        };

        var mockFramework = new MockEthicsFramework((action, context) =>
        {
            return new EthicalClearance
            {
                IsPermitted = true,
                Level = EthicalClearanceLevel.PermittedWithConcerns,
                RelevantPrinciples = new[] { EthicalPrinciple.Transparency },
                Violations = Array.Empty<EthicalViolation>(),
                Concerns = new[] { concern },
                Reasoning = "Permitted with concerns"
            };
        });
        var filter = new EthicsMessageFilter(mockFramework, _mockLogger.Object);
        var message = CreateTestMessage("monitored.action");

        // Act
        var result = await filter.ShouldRouteAsync(message);

        // Assert
        result.Should().BeTrue();
        mockFramework.EvaluateActionCallCount.Should().Be(1);
    }

    [Fact]
    public async Task EthicsFrameworkError_ShouldBlockMessage()
    {
        // Arrange
        var mockFramework = new Mock<IEthicsFramework>();
        mockFramework
            .Setup(f => f.EvaluateActionAsync(It.IsAny<ProposedAction>(), It.IsAny<ActionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<EthicalClearance, string>.Failure("Ethics framework error"));

        var filter = new EthicsMessageFilter(mockFramework.Object, _mockLogger.Object);
        var message = CreateTestMessage("some.action");

        // Act
        var result = await filter.ShouldRouteAsync(message);

        // Assert
        result.Should().BeFalse("messages that fail ethics evaluation should be blocked");
    }

    [Fact]
    public async Task EthicsFrameworkThrows_ShouldBlockMessage()
    {
        // Arrange
        var mockFramework = new Mock<IEthicsFramework>();
        mockFramework
            .Setup(f => f.EvaluateActionAsync(It.IsAny<ProposedAction>(), It.IsAny<ActionContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Test exception"));

        var filter = new EthicsMessageFilter(mockFramework.Object, _mockLogger.Object);
        var message = CreateTestMessage("some.action");

        // Act
        var result = await filter.ShouldRouteAsync(message);

        // Assert
        result.Should().BeFalse("messages that throw during evaluation should be blocked for safety");
    }

    [Fact]
    public void Constructor_WithNullFramework_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new EthicsMessageFilter(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ShouldThrow()
    {
        // Arrange
        var mockFramework = new MockEthicsFramework();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new EthicsMessageFilter(mockFramework, null!));
    }

    [Fact]
    public async Task ShouldRouteAsync_WithNullMessage_ShouldThrow()
    {
        // Arrange
        var mockFramework = new MockEthicsFramework();
        var filter = new EthicsMessageFilter(mockFramework, _mockLogger.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await filter.ShouldRouteAsync(null!));
    }

    private static NeuronMessage CreateTestMessage(string topic, string? targetNeuron = null)
    {
        return new NeuronMessage
        {
            Id = Guid.NewGuid(),
            SourceNeuron = "test-neuron",
            TargetNeuron = targetNeuron,
            Topic = topic,
            Payload = new { test = "data" },
            Priority = IntentionPriority.Normal,
            CreatedAt = DateTime.UtcNow,
        };
    }
}
