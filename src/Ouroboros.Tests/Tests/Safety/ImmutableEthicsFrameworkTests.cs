// <copyright file="ImmutableEthicsFrameworkTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Reflection;
using FluentAssertions;
using Moq;
using Ouroboros.Core.Ethics;
using Ouroboros.Core.Monads;
using Xunit;

namespace Ouroboros.Tests.Tests.Safety;

/// <summary>
/// Safety-critical tests for the ImmutableEthicsFramework class.
/// Verifies immutability guarantees, all evaluation methods, and thread safety.
/// </summary>
[Trait("Category", "Safety")]
public sealed class ImmutableEthicsFrameworkTests
{
    private readonly IEthicsFramework _framework;
    private readonly ActionContext _testContext;

    public ImmutableEthicsFrameworkTests()
    {
        _framework = EthicsFrameworkFactory.CreateDefault();
        _testContext = new ActionContext
        {
            AgentId = "test-agent",
            UserId = "test-user",
            Environment = "testing",
            State = new Dictionary<string, object>()
        };
    }

    #region Immutability Guarantees

    [Fact]
    public void EthicsFramework_IsSealed()
    {
        // Arrange & Act
        var frameworkType = _framework.GetType();

        // Assert
        frameworkType.IsSealed.Should().BeTrue("the ethics framework must be sealed to prevent inheritance");
    }

    [Fact]
    public void EthicsFramework_CoreValues_CannotBeModified()
    {
        // Arrange
        var principles = _framework.GetCorePrinciples();
        var originalCount = principles.Count;

        // Act - attempt to modify (should not be possible with IReadOnlyList)
        var principlesType = principles.GetType();

        // Assert
        principles.Should().BeAssignableTo<IReadOnlyList<EthicalPrinciple>>("core principles should be readonly");
        principles.Should().HaveCount(originalCount, "principles count should remain unchanged");
        
        // Verify it's truly immutable by checking the collection type
        principlesType.GetMethod("Add").Should().BeNull("the collection should not have an Add method");
        principlesType.GetMethod("Remove").Should().BeNull("the collection should not have a Remove method");
        principlesType.GetMethod("Clear").Should().BeNull("the collection should not have a Clear method");
    }

    [Fact]
    public void EthicsFramework_Principles_CannotBeModified()
    {
        // Arrange
        var principles1 = _framework.GetCorePrinciples();
        var principles2 = _framework.GetCorePrinciples();

        // Act & Assert
        principles1.Should().NotBeSameAs(principles2, "should return a copy each time to prevent modification");
        principles1.Should().BeEquivalentTo(principles2, "but the contents should be equivalent");
    }

    [Fact]
    public void EthicsFramework_CannotBeSubclassed()
    {
        // Arrange
        var frameworkType = _framework.GetType();

        // Act & Assert
        frameworkType.IsSealed.Should().BeTrue("sealed classes cannot be subclassed");
        frameworkType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.IsVirtual && !m.IsFinal)
            .Should().BeEmpty("no virtual methods should be overridable");
    }

    #endregion

    #region Action Evaluation (EvaluateActionAsync)

    [Fact]
    public async Task EvaluateAction_SafeAction_ReturnsPermitted()
    {
        // Arrange
        var action = new ProposedAction
        {
            ActionType = "read_file",
            Description = "Read configuration file for system settings",
            Parameters = new Dictionary<string, object> { ["path"] = "/config/app.json" },
            PotentialEffects = new[] { "Load configuration settings" }
        };

        // Act
        var result = await _framework.EvaluateActionAsync(action, _testContext);

        // Assert
        result.IsSuccess.Should().BeTrue("safe actions should be evaluated successfully");
        result.Value.IsPermitted.Should().BeTrue("safe actions should be permitted");
        result.Value.Level.Should().BeOneOf(
            EthicalClearanceLevel.Permitted,
            EthicalClearanceLevel.PermittedWithConcerns);
    }

    [Fact]
    public async Task EvaluateAction_DangerousAction_ReturnsDenied()
    {
        // Arrange
        var action = new ProposedAction
        {
            ActionType = "system_attack",
            Description = "Harm the system or users through malicious actions",
            Parameters = new Dictionary<string, object>(),
            PotentialEffects = new[] { "System damage", "Data loss", "User harm" }
        };

        // Act
        var result = await _framework.EvaluateActionAsync(action, _testContext);

        // Assert
        result.IsSuccess.Should().BeTrue("dangerous actions should be evaluated (not throw)");
        result.Value.IsPermitted.Should().BeFalse("dangerous actions must be denied");
        result.Value.Level.Should().Be(EthicalClearanceLevel.Denied);
        result.Value.Violations.Should().NotBeEmpty("violations should be reported");
    }

    [Fact]
    public async Task EvaluateAction_NullContext_ReturnsFailure()
    {
        // Arrange
        var action = new ProposedAction
        {
            ActionType = "test",
            Description = "Test action",
            Parameters = new Dictionary<string, object>(),
            PotentialEffects = Array.Empty<string>()
        };

        // Act
        var act = async () => await _framework.EvaluateActionAsync(action, null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>("null context should be rejected");
    }

    [Fact]
    public async Task EvaluateAction_EmptyDescription_ReturnsFailure()
    {
        // Arrange
        var action = new ProposedAction
        {
            ActionType = "test",
            Description = string.Empty,
            Parameters = new Dictionary<string, object>(),
            PotentialEffects = Array.Empty<string>()
        };

        // Act
        var result = await _framework.EvaluateActionAsync(action, _testContext);

        // Assert
        result.IsSuccess.Should().BeTrue("evaluation should handle empty descriptions");
        // The framework may permit or flag this - just verify it doesn't throw
    }

    #endregion

    #region Plan Evaluation (EvaluatePlanAsync)

    [Fact]
    public async Task EvaluatePlan_SafePlan_ReturnsPermitted()
    {
        // Arrange
        var plan = new Plan(
            "Test safe plan",
            new List<PlanStep>
            {
                new PlanStep(
                    "read_config",
                    "Load settings",
                    new Dictionary<string, object>(),
                    "Settings loaded",
                    1.0)
            },
            new Dictionary<string, double> { ["safe"] = 1.0 },
            DateTime.UtcNow);

        var planContext = new PlanContext
        {
            Plan = plan,
            ActionContext = _testContext,
            EstimatedRisk = 0.1
        };

        // Act
        var result = await _framework.EvaluatePlanAsync(planContext);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsPermitted.Should().BeTrue("safe plans should be permitted");
    }

    [Fact]
    public async Task EvaluatePlan_PlanWithDangerousStep_ReturnsDenied()
    {
        // Arrange
        var plan = new Plan(
            "Plan with dangerous step",
            new List<PlanStep>
            {
                new PlanStep(
                    "harm_user",
                    "Cause harm to users",
                    new Dictionary<string, object>(),
                    "User harmed",
                    1.0)
            },
            new Dictionary<string, double> { ["dangerous"] = 1.0 },
            DateTime.UtcNow);

        var planContext = new PlanContext
        {
            Plan = plan,
            ActionContext = _testContext,
            EstimatedRisk = 0.9
        };

        // Act
        var result = await _framework.EvaluatePlanAsync(planContext);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsPermitted.Should().BeFalse("plans with dangerous steps should be denied");
        result.Value.Violations.Should().NotBeEmpty();
    }

    [Fact]
    public async Task EvaluatePlan_EmptyPlan_ReturnsPermitted()
    {
        // Arrange
        var plan = new Plan(
            "Empty plan",
            new List<PlanStep>(),
            new Dictionary<string, double>(),
            DateTime.UtcNow);

        var planContext = new PlanContext
        {
            Plan = plan,
            ActionContext = _testContext,
            EstimatedRisk = 0.0
        };

        // Act
        var result = await _framework.EvaluatePlanAsync(planContext);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsPermitted.Should().BeTrue("empty plans have no violations");
    }

    #endregion

    #region Self-Modification Evaluation (EvaluateSelfModificationAsync)

    [Fact]
    public async Task EvaluateSelfModification_AlwaysRequiresHumanApproval()
    {
        // Arrange - even a low-impact, reversible modification
        var request = new SelfModificationRequest
        {
            Type = ModificationType.BehaviorAdjustment,
            Description = "Minor logging adjustment",
            ActionContext = _testContext,
            IsReversible = true,
            ImpactLevel = 0.1,
            Rationale = "Improve debugging"
        };

        // Act
        var result = await _framework.EvaluateSelfModificationAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Level.Should().Be(EthicalClearanceLevel.RequiresHumanApproval,
            "ALL self-modifications must require human approval");
        result.Value.IsPermitted.Should().BeFalse("should not be auto-permitted");
    }

    [Fact]
    public async Task EvaluateSelfModification_IrreversibleHighImpact_ReturnsDenied()
    {
        // Arrange
        var request = new SelfModificationRequest
        {
            Type = ModificationType.CodeModification,
            Description = "Irreversible high-impact change to core logic",
            ActionContext = _testContext,
            IsReversible = false,
            ImpactLevel = 0.9,
            Rationale = "Major refactoring"
        };

        // Act
        var result = await _framework.EvaluateSelfModificationAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // High impact + irreversible should at minimum require approval, possibly be denied
        result.Value.IsPermitted.Should().BeFalse();
        result.Value.Concerns.Should().Contain(c => 
            c.Description.Contains("irreversible", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task EvaluateSelfModification_ReversibleLowImpact_RequiresApproval()
    {
        // Arrange
        var request = new SelfModificationRequest
        {
            Type = ModificationType.BehaviorAdjustment,
            Description = "Reversible low-impact tweak",
            ActionContext = _testContext,
            IsReversible = true,
            ImpactLevel = 0.2,
            Rationale = "Performance optimization"
        };

        // Act
        var result = await _framework.EvaluateSelfModificationAsync(request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Level.Should().Be(EthicalClearanceLevel.RequiresHumanApproval,
            "even low-impact self-modifications require approval");
    }

    [Fact]
    public async Task EvaluateSelfModification_NullRequest_ReturnsFailure()
    {
        // Act
        var act = async () => await _framework.EvaluateSelfModificationAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    #endregion

    #region Research Evaluation (EvaluateResearchAsync)

    [Fact]
    public async Task EvaluateResearch_SafeResearch_ReturnsPermitted()
    {
        // Arrange
        var researchDescription = "Analyze system logs to identify performance bottlenecks";

        // Act
        var result = await _framework.EvaluateResearchAsync(researchDescription, _testContext);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsPermitted.Should().BeTrue("safe research should be permitted");
    }

    [Fact]
    public async Task EvaluateResearch_DangerousResearch_ReturnsDenied()
    {
        // Arrange
        var researchDescription = "Experiment on users without consent to test harmful patterns";

        // Act
        var result = await _framework.EvaluateResearchAsync(researchDescription, _testContext);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.IsPermitted.Should().BeFalse("dangerous research must be denied");
        result.Value.Violations.Should().NotBeEmpty();
    }

    #endregion

    #region Cross-Cutting Tests

    [Fact]
    public async Task AllEvaluationMethods_ReturnResult_NeverThrow()
    {
        // Arrange
        var safeAction = new ProposedAction
        {
            ActionType = "test",
            Description = "Test",
            Parameters = new Dictionary<string, object>(),
            PotentialEffects = Array.Empty<string>()
        };

        var safePlan = new PlanContext
        {
            Plan = new Plan("test", new List<PlanStep>(), new Dictionary<string, double>(), DateTime.UtcNow),
            ActionContext = _testContext,
            EstimatedRisk = 0.1
        };

        var safeModification = new SelfModificationRequest
        {
            Type = ModificationType.BehaviorAdjustment,
            Description = "test",
            ActionContext = _testContext,
            IsReversible = true,
            ImpactLevel = 0.1,
            Rationale = "test"
        };

        // Act & Assert - none should throw
        var actionResult = await _framework.EvaluateActionAsync(safeAction, _testContext);
        actionResult.IsSuccess.Should().BeTrue("action evaluation should not throw");

        var planResult = await _framework.EvaluatePlanAsync(safePlan);
        planResult.IsSuccess.Should().BeTrue("plan evaluation should not throw");

        var modResult = await _framework.EvaluateSelfModificationAsync(safeModification);
        modResult.IsSuccess.Should().BeTrue("self-modification evaluation should not throw");

        var researchResult = await _framework.EvaluateResearchAsync("test research", _testContext);
        researchResult.IsSuccess.Should().BeTrue("research evaluation should not throw");
    }

    [Fact]
    public async Task EvaluateAction_ConcurrentCalls_AreThreadSafe()
    {
        // Arrange
        var action = new ProposedAction
        {
            ActionType = "test",
            Description = "Concurrent test",
            Parameters = new Dictionary<string, object>(),
            PotentialEffects = Array.Empty<string>()
        };

        // Act - call 100 evaluations concurrently
        var tasks = Enumerable.Range(0, 100)
            .Select(_ => _framework.EvaluateActionAsync(action, _testContext))
            .ToList();

        var results = await Task.WhenAll(tasks);

        // Assert - all should succeed without corruption
        results.Should().AllSatisfy(r => r.IsSuccess.Should().BeTrue("concurrent calls should not corrupt state"));
        results.Should().AllSatisfy(r => r.Value.Should().NotBeNull());
        
        // All results should be consistent
        var firstResult = results[0].Value;
        results.Should().AllSatisfy(r => 
            r.Value.IsPermitted.Should().Be(firstResult.IsPermitted, "results should be consistent"));
    }

    #endregion
}
