// <copyright file="IEthicsFramework.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Core.Monads;

namespace Ouroboros.Core.Ethics;

/// <summary>
/// Core interface for the Ethics Framework.
/// Provides ethical evaluation capabilities for all agent actions.
/// This framework CANNOT be disabled or bypassed - it is a foundational safety mechanism.
/// </summary>
public interface IEthicsFramework
{
    /// <summary>
    /// Evaluates a proposed action for ethical compliance.
    /// </summary>
    /// <param name="action">The proposed action to evaluate.</param>
    /// <param name="context">The context in which the action would be performed.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An ethical clearance decision.</returns>
    Task<Result<EthicalClearance, string>> EvaluateActionAsync(
        ProposedAction action,
        ActionContext context,
        CancellationToken ct = default);

    /// <summary>
    /// Evaluates a plan (sequence of actions) for ethical compliance.
    /// </summary>
    /// <param name="planContext">The plan context containing the plan and evaluation context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An ethical clearance decision for the entire plan.</returns>
    Task<Result<EthicalClearance, string>> EvaluatePlanAsync(
        PlanContext planContext,
        CancellationToken ct = default);

    /// <summary>
    /// Evaluates a goal for ethical alignment and value compatibility.
    /// </summary>
    /// <param name="goal">The goal to evaluate.</param>
    /// <param name="context">The context in which the goal would be pursued.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An ethical clearance decision for the goal.</returns>
    Task<Result<EthicalClearance, string>> EvaluateGoalAsync(
        Goal goal,
        ActionContext context,
        CancellationToken ct = default);

    /// <summary>
    /// Evaluates the use of a learned skill for ethical compliance.
    /// </summary>
    /// <param name="skillContext">The skill usage context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An ethical clearance decision for skill usage.</returns>
    Task<Result<EthicalClearance, string>> EvaluateSkillAsync(
        SkillUsageContext skillContext,
        CancellationToken ct = default);

    /// <summary>
    /// Evaluates research activities (hypothesis testing, experiments) for ethical compliance.
    /// </summary>
    /// <param name="researchDescription">Description of the research activity.</param>
    /// <param name="context">The context in which research would be conducted.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An ethical clearance decision for the research activity.</returns>
    Task<Result<EthicalClearance, string>> EvaluateResearchAsync(
        string researchDescription,
        ActionContext context,
        CancellationToken ct = default);

    /// <summary>
    /// Evaluates a self-modification request for ethical compliance and safety.
    /// Self-modification is a high-risk operation requiring careful evaluation.
    /// </summary>
    /// <param name="request">The self-modification request to evaluate.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>An ethical clearance decision for the modification.</returns>
    Task<Result<EthicalClearance, string>> EvaluateSelfModificationAsync(
        SelfModificationRequest request,
        CancellationToken ct = default);

    /// <summary>
    /// Gets the core ethical principles used by this framework.
    /// These principles are immutable and cannot be modified at runtime.
    /// </summary>
    /// <returns>A read-only collection of core ethical principles.</returns>
    IReadOnlyList<EthicalPrinciple> GetCorePrinciples();

    /// <summary>
    /// Reports an ethical concern discovered during operation.
    /// This allows the system to flag issues that don't rise to violations but warrant attention.
    /// </summary>
    /// <param name="concern">The ethical concern to report.</param>
    /// <param name="context">The context in which the concern was identified.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ReportEthicalConcernAsync(
        EthicalConcern concern,
        ActionContext context,
        CancellationToken ct = default);
}
