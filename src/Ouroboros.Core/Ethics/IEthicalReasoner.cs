// <copyright file="IEthicalReasoner.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Ethics;

/// <summary>
/// Interface for ethical reasoning logic.
/// Separates reasoning algorithms from the framework interface.
/// </summary>
public interface IEthicalReasoner
{
    /// <summary>
    /// Analyzes a proposed action against ethical principles.
    /// </summary>
    /// <param name="action">The action to analyze.</param>
    /// <param name="context">The action context.</param>
    /// <param name="principles">The ethical principles to evaluate against.</param>
    /// <returns>A tuple containing violations and concerns.</returns>
    (IReadOnlyList<EthicalViolation> violations, IReadOnlyList<EthicalConcern> concerns) 
        AnalyzeAction(
            ProposedAction action,
            ActionContext context,
            IReadOnlyList<EthicalPrinciple> principles);

    /// <summary>
    /// Determines if an action description contains harmful keywords or patterns.
    /// </summary>
    /// <param name="description">The action description to check.</param>
    /// <returns>True if harmful patterns are detected.</returns>
    bool ContainsHarmfulPatterns(string description);

    /// <summary>
    /// Determines if an action requires human approval based on risk level.
    /// </summary>
    /// <param name="action">The action to evaluate.</param>
    /// <param name="context">The action context.</param>
    /// <returns>True if human approval is required.</returns>
    bool RequiresHumanApproval(ProposedAction action, ActionContext context);
}
