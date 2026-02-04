// <copyright file="MockEthicsFramework.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Mocks;

using Ouroboros.Core.Ethics;

/// <summary>
/// Mock implementation of IEthicsFramework for testing purposes.
/// Provides configurable ethical evaluation behavior.
/// </summary>
public sealed class MockEthicsFramework : IEthicsFramework
{
    private readonly Func<ProposedAction, ActionContext, EthicalClearance>? actionEvaluator;
    private readonly List<EthicalPrinciple> principles = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MockEthicsFramework"/> class.
    /// By default, all actions are ethically approved.
    /// </summary>
    public MockEthicsFramework()
    {
        // Add default principles
        this.principles.Add(EthicalPrinciple.DoNoHarm);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MockEthicsFramework"/> class with custom action evaluator.
    /// </summary>
    /// <param name="actionEvaluator">Custom function to evaluate actions.</param>
    public MockEthicsFramework(Func<ProposedAction, ActionContext, EthicalClearance> actionEvaluator)
        : this()
    {
        this.actionEvaluator = actionEvaluator;
    }

    /// <summary>
    /// Gets the number of times EvaluateActionAsync was called.
    /// </summary>
    public int EvaluateActionCallCount { get; private set; }

    /// <summary>
    /// Gets the number of times EvaluatePlanAsync was called.
    /// </summary>
    public int EvaluatePlanCallCount { get; private set; }

    /// <summary>
    /// Evaluates a proposed action for ethical compliance.
    /// </summary>
    public Task<Result<EthicalClearance, string>> EvaluateActionAsync(
        ProposedAction action,
        ActionContext context,
        CancellationToken ct = default)
    {
        this.EvaluateActionCallCount++;

        EthicalClearance clearance;
        if (this.actionEvaluator != null)
        {
            clearance = this.actionEvaluator(action, context);
        }
        else
        {
            // Default: approve all actions
            clearance = EthicalClearance.Permitted("Default approval for testing");
        }

        return Task.FromResult(Result<EthicalClearance, string>.Success(clearance));
    }

    /// <summary>
    /// Evaluates a plan for ethical compliance.
    /// </summary>
    public Task<Result<EthicalClearance, string>> EvaluatePlanAsync(
        PlanContext planContext,
        CancellationToken ct = default)
    {
        this.EvaluatePlanCallCount++;

        // Default: approve all plans
        var clearance = EthicalClearance.Permitted("Default plan approval for testing");

        return Task.FromResult(Result<EthicalClearance, string>.Success(clearance));
    }

    /// <summary>
    /// Evaluates a goal for ethical alignment.
    /// </summary>
    public Task<Result<EthicalClearance, string>> EvaluateGoalAsync(
        Goal goal,
        ActionContext context,
        CancellationToken ct = default)
    {
        // Default: approve all goals
        var clearance = EthicalClearance.Permitted("Default goal approval for testing");

        return Task.FromResult(Result<EthicalClearance, string>.Success(clearance));
    }

    /// <summary>
    /// Evaluates skill usage for ethical compliance.
    /// </summary>
    public Task<Result<EthicalClearance, string>> EvaluateSkillAsync(
        SkillUsageContext skillContext,
        CancellationToken ct = default)
    {
        // Default: approve all skills
        var clearance = EthicalClearance.Permitted("Default skill approval for testing");

        return Task.FromResult(Result<EthicalClearance, string>.Success(clearance));
    }

    /// <summary>
    /// Evaluates research activities for ethical compliance.
    /// </summary>
    public Task<Result<EthicalClearance, string>> EvaluateResearchAsync(
        string researchDescription,
        ActionContext context,
        CancellationToken ct = default)
    {
        // Default: approve all research
        var clearance = EthicalClearance.Permitted("Default research approval for testing");

        return Task.FromResult(Result<EthicalClearance, string>.Success(clearance));
    }

    /// <summary>
    /// Evaluates self-modification requests for ethical compliance.
    /// </summary>
    public Task<Result<EthicalClearance, string>> EvaluateSelfModificationAsync(
        SelfModificationRequest request,
        CancellationToken ct = default)
    {
        // Default: require approval for modifications (safer default)
        var clearance = EthicalClearance.RequiresApproval("Self-modification requires human approval in testing");

        return Task.FromResult(Result<EthicalClearance, string>.Success(clearance));
    }

    /// <summary>
    /// Gets core ethical principles.
    /// </summary>
    public IReadOnlyList<EthicalPrinciple> GetCorePrinciples()
    {
        return this.principles;
    }

    /// <summary>
    /// Reports an ethical concern.
    /// </summary>
    public Task ReportEthicalConcernAsync(
        EthicalConcern concern,
        ActionContext context,
        CancellationToken ct = default)
    {
        // No-op for mock
        return Task.CompletedTask;
    }
}
