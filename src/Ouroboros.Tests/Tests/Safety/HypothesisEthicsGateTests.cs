// <copyright file="HypothesisEthicsGateTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Moq;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Core.Ethics;
using Ouroboros.Core.Monads;
using Xunit;

namespace Ouroboros.Tests.Tests.Safety;

/// <summary>
/// Safety-critical tests for the ethics gate in HypothesisEngine.TestHypothesisAsync.
/// Verifies that dangerous experiments are blocked by ethics evaluation.
/// </summary>
[Trait("Category", "Safety")]
public sealed class HypothesisEthicsGateTests
{
    #region Ethics Integration Tests

    [Fact]
    public async Task TestHypothesis_EthicsPermits_ExperimentExecutes()
    {
        // Arrange
        var mockLlm = new Mock<IChatCompletionModel>();
        var mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();
        var mockMemory = new Mock<IMemoryStore>();
        var mockEthics = new Mock<IEthicsFramework>();
        
        mockLlm.Setup(m => m.GenerateTextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("Test hypothesis");
        
        mockMemory.Setup(m => m.RetrieveRelevantExperiencesAsync(It.IsAny<MemoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Experience>());
        
        // Ethics permits the research
        mockEthics.Setup(m => m.EvaluateResearchAsync(It.IsAny<string>(), It.IsAny<ActionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<EthicalClearance, string>.Success(EthicalClearance.Permitted(
                "Research permitted",
                new List<EthicalPrinciple>())));
        
        // Orchestrator executes successfully
        mockOrchestrator.Setup(m => m.ExecuteAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<ExecutionResult, string>.Success(new ExecutionResult(
                true,
                "Execution completed",
                new Dictionary<string, object>(),
                DateTime.UtcNow,
                100)));
        
        var engine = new HypothesisEngine(
            mockLlm.Object,
            mockOrchestrator.Object,
            mockMemory.Object,
            mockEthics.Object);
        
        var hypothesis = new Hypothesis(
            Guid.NewGuid(),
            "Test hypothesis",
            "Testing",
            0.7,
            DateTime.UtcNow,
            "Test reason");
        
        var experiment = new Experiment(
            Guid.NewGuid(),
            hypothesis,
            "Test experiment",
            new List<PlanStep> { new PlanStep("test", "Test step", new Dictionary<string, object>(), "Expected", 1.0) },
            new Dictionary<string, object>(),
            DateTime.UtcNow);

        // Act
        var result = await engine.TestHypothesisAsync(hypothesis, experiment);

        // Assert
        result.IsSuccess.Should().BeTrue("permitted research should execute");
        mockEthics.Verify(m => m.EvaluateResearchAsync(It.IsAny<string>(), It.IsAny<ActionContext>(), It.IsAny<CancellationToken>()), 
            Times.Once, 
            "ethics evaluation must be called");
        mockOrchestrator.Verify(m => m.ExecuteAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()), 
            Times.Once, 
            "experiment should execute when ethics permits");
    }

    [Fact]
    public async Task TestHypothesis_EthicsDenies_ExperimentDoesNotExecute()
    {
        // Arrange
        var mockLlm = new Mock<IChatCompletionModel>();
        var mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();
        var mockMemory = new Mock<IMemoryStore>();
        var mockEthics = new Mock<IEthicsFramework>();
        
        mockMemory.Setup(m => m.RetrieveRelevantExperiencesAsync(It.IsAny<MemoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Experience>());
        
        // Ethics denies the research
        mockEthics.Setup(m => m.EvaluateResearchAsync(It.IsAny<string>(), It.IsAny<ActionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<EthicalClearance, string>.Success(EthicalClearance.Denied(
                "Research violates ethics",
                new List<EthicalViolation> { new EthicalViolation { Description = "Harmful research" } },
                new List<EthicalPrinciple>())));
        
        var engine = new HypothesisEngine(
            mockLlm.Object,
            mockOrchestrator.Object,
            mockMemory.Object,
            mockEthics.Object);
        
        var hypothesis = new Hypothesis(
            Guid.NewGuid(),
            "Dangerous hypothesis",
            "Testing",
            0.7,
            DateTime.UtcNow,
            "Test reason");
        
        var experiment = new Experiment(
            Guid.NewGuid(),
            hypothesis,
            "Dangerous experiment",
            new List<PlanStep> { new PlanStep("harm", "Harmful step", new Dictionary<string, object>(), "Expected", 1.0) },
            new Dictionary<string, object>(),
            DateTime.UtcNow);

        // Act
        var result = await engine.TestHypothesisAsync(hypothesis, experiment);

        // Assert
        result.IsSuccess.Should().BeFalse("denied research should not execute");
        result.Error.Should().Contain("rejected", StringComparison.OrdinalIgnoreCase);
        mockEthics.Verify(m => m.EvaluateResearchAsync(It.IsAny<string>(), It.IsAny<ActionContext>(), It.IsAny<CancellationToken>()), 
            Times.Once);
        mockOrchestrator.Verify(m => m.ExecuteAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()), 
            Times.Never, 
            "experiment should NOT execute when ethics denies");
    }

    [Fact]
    public async Task TestHypothesis_EthicsRequiresHumanApproval_ReturnsFailure()
    {
        // Arrange
        var mockLlm = new Mock<IChatCompletionModel>();
        var mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();
        var mockMemory = new Mock<IMemoryStore>();
        var mockEthics = new Mock<IEthicsFramework>();
        
        mockMemory.Setup(m => m.RetrieveRelevantExperiencesAsync(It.IsAny<MemoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Experience>());
        
        // Ethics requires human approval
        mockEthics.Setup(m => m.EvaluateResearchAsync(It.IsAny<string>(), It.IsAny<ActionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<EthicalClearance, string>.Success(EthicalClearance.RequiresApproval(
                "Research requires approval",
                new List<EthicalConcern>(),
                new List<EthicalPrinciple>())));
        
        var engine = new HypothesisEngine(
            mockLlm.Object,
            mockOrchestrator.Object,
            mockMemory.Object,
            mockEthics.Object);
        
        var hypothesis = new Hypothesis(
            Guid.NewGuid(),
            "High-risk hypothesis",
            "Testing",
            0.7,
            DateTime.UtcNow,
            "Test reason");
        
        var experiment = new Experiment(
            Guid.NewGuid(),
            hypothesis,
            "High-risk experiment",
            new List<PlanStep>(),
            new Dictionary<string, object>(),
            DateTime.UtcNow);

        // Act
        var result = await engine.TestHypothesisAsync(hypothesis, experiment);

        // Assert
        result.IsSuccess.Should().BeFalse("research requiring approval should not auto-execute");
        result.Error.Should().Contain("approval", StringComparison.OrdinalIgnoreCase);
        mockOrchestrator.Verify(m => m.ExecuteAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()), 
            Times.Never, 
            "experiment should NOT execute without human approval");
    }

    [Fact]
    public async Task TestHypothesis_EthicsThrows_ReturnsFailure()
    {
        // Arrange
        var mockLlm = new Mock<IChatCompletionModel>();
        var mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();
        var mockMemory = new Mock<IMemoryStore>();
        var mockEthics = new Mock<IEthicsFramework>();
        
        mockMemory.Setup(m => m.RetrieveRelevantExperiencesAsync(It.IsAny<MemoryQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Experience>());
        
        // Ethics throws an exception
        mockEthics.Setup(m => m.EvaluateResearchAsync(It.IsAny<string>(), It.IsAny<ActionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<EthicalClearance, string>.Failure("Ethics evaluation failed due to internal error"));
        
        var engine = new HypothesisEngine(
            mockLlm.Object,
            mockOrchestrator.Object,
            mockMemory.Object,
            mockEthics.Object);
        
        var hypothesis = new Hypothesis(
            Guid.NewGuid(),
            "Test hypothesis",
            "Testing",
            0.7,
            DateTime.UtcNow,
            "Test reason");
        
        var experiment = new Experiment(
            Guid.NewGuid(),
            hypothesis,
            "Test experiment",
            new List<PlanStep>(),
            new Dictionary<string, object>(),
            DateTime.UtcNow);

        // Act
        var result = await engine.TestHypothesisAsync(hypothesis, experiment);

        // Assert
        result.IsSuccess.Should().BeFalse("ethics failure should prevent execution");
        result.Error.Should().Contain("rejected", StringComparison.OrdinalIgnoreCase);
        mockOrchestrator.Verify(m => m.ExecuteAsync(It.IsAny<Plan>(), It.IsAny<CancellationToken>()), 
            Times.Never, 
            "experiment should NOT execute when ethics evaluation fails");
    }

    [Fact]
    public async Task TestHypothesis_NullHypothesis_ReturnsFailure()
    {
        // Arrange
        var mockLlm = new Mock<IChatCompletionModel>();
        var mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();
        var mockMemory = new Mock<IMemoryStore>();
        var mockEthics = new Mock<IEthicsFramework>();
        
        var engine = new HypothesisEngine(
            mockLlm.Object,
            mockOrchestrator.Object,
            mockMemory.Object,
            mockEthics.Object);
        
        var experiment = new Experiment(
            Guid.NewGuid(),
            new Hypothesis(Guid.NewGuid(), "test", "test", 0.5, DateTime.UtcNow, "test"),
            "Test experiment",
            new List<PlanStep>(),
            new Dictionary<string, object>(),
            DateTime.UtcNow);

        // Act
        var result = await engine.TestHypothesisAsync(null!, experiment);

        // Assert
        result.IsSuccess.Should().BeFalse("null hypothesis should be rejected");
        result.Error.Should().Contain("null", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TestHypothesis_NullExperiment_ReturnsFailure()
    {
        // Arrange
        var mockLlm = new Mock<IChatCompletionModel>();
        var mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();
        var mockMemory = new Mock<IMemoryStore>();
        var mockEthics = new Mock<IEthicsFramework>();
        
        var engine = new HypothesisEngine(
            mockLlm.Object,
            mockOrchestrator.Object,
            mockMemory.Object,
            mockEthics.Object);
        
        var hypothesis = new Hypothesis(
            Guid.NewGuid(),
            "Test hypothesis",
            "Testing",
            0.7,
            DateTime.UtcNow,
            "Test reason");

        // Act
        var result = await engine.TestHypothesisAsync(hypothesis, null!);

        // Assert
        result.IsSuccess.Should().BeFalse("null experiment should be rejected");
        result.Error.Should().Contain("null", StringComparison.OrdinalIgnoreCase);
    }

    #endregion
}
