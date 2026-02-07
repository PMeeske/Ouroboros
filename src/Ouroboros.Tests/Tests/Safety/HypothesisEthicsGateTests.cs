// <copyright file="HypothesisEthicsGateTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using FluentAssertions;
using Moq;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Core.Ethics;
using Ouroboros.Core.Monads;
using Xunit;

// Use type aliases to disambiguate between different namespaces
using AgentHypothesis = Ouroboros.Agent.MetaAI.Hypothesis;
using AgentPlanStep = Ouroboros.Agent.MetaAI.PlanStep;
using AgentPlan = Ouroboros.Agent.MetaAI.Plan;
using AgentExperiment = Ouroboros.Agent.MetaAI.Experiment;
using AgentExecutionResult = Ouroboros.Agent.MetaAI.ExecutionResult;
using AgentHypothesisEngine = Ouroboros.Agent.MetaAI.HypothesisEngine;
using AgentStepResult = Ouroboros.Agent.MetaAI.StepResult;

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
        var plan = new AgentPlan(
            "Test experiment",
            new List<AgentPlanStep>(),
            new Dictionary<string, double>(),
            DateTime.UtcNow);
        
        var executionResult = new AgentExecutionResult(
            plan,
            new List<AgentStepResult>(),
            true,
            "Execution completed",
            new Dictionary<string, object>(),
            TimeSpan.FromMilliseconds(100));
        
        mockOrchestrator.Setup(m => m.ExecuteAsync(It.IsAny<AgentPlan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<AgentExecutionResult, string>.Success(executionResult));
        
        var engine = new AgentHypothesisEngine(
            mockLlm.Object,
            mockOrchestrator.Object,
            mockMemory.Object,
            mockEthics.Object);
        
        var hypothesis = new AgentHypothesis(
            Guid.NewGuid(),
            "Test hypothesis",
            "Testing",
            0.7,
            new List<string>(),
            new List<string>(),
            DateTime.UtcNow,
            false,
            null);
        
        var experiment = new AgentExperiment(
            Guid.NewGuid(),
            hypothesis,
            "Test experiment",
            new List<AgentPlanStep> { new AgentPlanStep("test", new Dictionary<string, object>(), "Expected", 1.0) },
            new Dictionary<string, object>(),
            DateTime.UtcNow);

        // Act
        var result = await engine.TestHypothesisAsync(hypothesis, experiment);

        // Assert
        result.IsSuccess.Should().BeTrue("permitted research should execute");
        mockEthics.Verify(m => m.EvaluateResearchAsync(It.IsAny<string>(), It.IsAny<ActionContext>(), It.IsAny<CancellationToken>()), 
            Times.Once, 
            "ethics evaluation must be called");
        mockOrchestrator.Verify(m => m.ExecuteAsync(It.IsAny<AgentPlan>(), It.IsAny<CancellationToken>()), 
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
                new List<EthicalViolation>
                {
                    new EthicalViolation
                    {
                        ViolatedPrinciple = EthicalPrinciple.DoNoHarm,
                        Description = "Harmful research",
                        Severity = ViolationSeverity.Critical,
                        Evidence = "Test evidence",
                        AffectedParties = new List<string> { "Users" }
                    }
                },
                new List<EthicalPrinciple>())));
        
        var engine = new AgentHypothesisEngine(
            mockLlm.Object,
            mockOrchestrator.Object,
            mockMemory.Object,
            mockEthics.Object);
        
        var hypothesis = new AgentHypothesis(
            Guid.NewGuid(),
            "Dangerous hypothesis",
            "Testing",
            0.7,
            new List<string>(),
            new List<string>(),
            DateTime.UtcNow,
            false,
            null);
        
        var experiment = new AgentExperiment(
            Guid.NewGuid(),
            hypothesis,
            "Dangerous experiment",
            new List<AgentPlanStep> { new AgentPlanStep("harm", new Dictionary<string, object>(), "Expected", 1.0) },
            new Dictionary<string, object>(),
            DateTime.UtcNow);

        // Act
        var result = await engine.TestHypothesisAsync(hypothesis, experiment);

        // Assert
        result.IsSuccess.Should().BeFalse("denied research should not execute");
        result.Error.Should().ContainEquivalentOf("rejected");
        mockEthics.Verify(m => m.EvaluateResearchAsync(It.IsAny<string>(), It.IsAny<ActionContext>(), It.IsAny<CancellationToken>()), 
            Times.Once);
        mockOrchestrator.Verify(m => m.ExecuteAsync(It.IsAny<AgentPlan>(), It.IsAny<CancellationToken>()), 
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
        
        var engine = new AgentHypothesisEngine(
            mockLlm.Object,
            mockOrchestrator.Object,
            mockMemory.Object,
            mockEthics.Object);
        
        var hypothesis = new AgentHypothesis(
            Guid.NewGuid(),
            "High-risk hypothesis",
            "Testing",
            0.7,
            new List<string>(),
            new List<string>(),
            DateTime.UtcNow,
            false,
            null);
        
        var experiment = new AgentExperiment(
            Guid.NewGuid(),
            hypothesis,
            "High-risk experiment",
            new List<AgentPlanStep>(),
            new Dictionary<string, object>(),
            DateTime.UtcNow);

        // Act
        var result = await engine.TestHypothesisAsync(hypothesis, experiment);

        // Assert
        result.IsSuccess.Should().BeFalse("research requiring approval should not auto-execute");
        result.Error.Should().ContainEquivalentOf("approval");
        mockOrchestrator.Verify(m => m.ExecuteAsync(It.IsAny<AgentPlan>(), It.IsAny<CancellationToken>()), 
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
        
        var engine = new AgentHypothesisEngine(
            mockLlm.Object,
            mockOrchestrator.Object,
            mockMemory.Object,
            mockEthics.Object);
        
        var hypothesis = new AgentHypothesis(
            Guid.NewGuid(),
            "Test hypothesis",
            "Testing",
            0.7,
            new List<string>(),
            new List<string>(),
            DateTime.UtcNow,
            false,
            null);
        
        var experiment = new AgentExperiment(
            Guid.NewGuid(),
            hypothesis,
            "Test experiment",
            new List<AgentPlanStep>(),
            new Dictionary<string, object>(),
            DateTime.UtcNow);

        // Act
        var result = await engine.TestHypothesisAsync(hypothesis, experiment);

        // Assert
        result.IsSuccess.Should().BeFalse("ethics failure should prevent execution");
        result.Error.Should().ContainEquivalentOf("rejected");
        mockOrchestrator.Verify(m => m.ExecuteAsync(It.IsAny<AgentPlan>(), It.IsAny<CancellationToken>()), 
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
        
        var engine = new AgentHypothesisEngine(
            mockLlm.Object,
            mockOrchestrator.Object,
            mockMemory.Object,
            mockEthics.Object);
        
        var dummyHypothesis = new AgentHypothesis(
            Guid.NewGuid(),
            "test",
            "test",
            0.5,
            new List<string>(),
            new List<string>(),
            DateTime.UtcNow,
            false,
            null);
        
        var experiment = new AgentExperiment(
            Guid.NewGuid(),
            dummyHypothesis,
            "Test experiment",
            new List<AgentPlanStep>(),
            new Dictionary<string, object>(),
            DateTime.UtcNow);

        // Act
        var result = await engine.TestHypothesisAsync(null!, experiment);

        // Assert
        result.IsSuccess.Should().BeFalse("null hypothesis should be rejected");
        result.Error.Should().ContainEquivalentOf("null");
    }

    [Fact]
    public async Task TestHypothesis_NullExperiment_ReturnsFailure()
    {
        // Arrange
        var mockLlm = new Mock<IChatCompletionModel>();
        var mockOrchestrator = new Mock<IMetaAIPlannerOrchestrator>();
        var mockMemory = new Mock<IMemoryStore>();
        var mockEthics = new Mock<IEthicsFramework>();
        
        var engine = new AgentHypothesisEngine(
            mockLlm.Object,
            mockOrchestrator.Object,
            mockMemory.Object,
            mockEthics.Object);
        
        var hypothesis = new AgentHypothesis(
            Guid.NewGuid(),
            "Test hypothesis",
            "Testing",
            0.7,
            new List<string>(),
            new List<string>(),
            DateTime.UtcNow,
            false,
            null);

        // Act
        var result = await engine.TestHypothesisAsync(hypothesis, null!);

        // Assert
        result.IsSuccess.Should().BeFalse("null experiment should be rejected");
        result.Error.Should().ContainEquivalentOf("null");
    }

    #endregion
}
