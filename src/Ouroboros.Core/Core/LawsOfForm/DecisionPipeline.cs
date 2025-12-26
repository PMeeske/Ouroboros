// <copyright file="DecisionPipeline.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.LawsOfForm;

/// <summary>
/// Provides pipeline operations for composing multiple auditable decisions.
/// Uses Laws of Form to combine criteria with proper three-valued logic.
/// </summary>
public static class DecisionPipeline
{
    /// <summary>
    /// Evaluates multiple decision criteria and combines them using AND logic.
    /// All criteria must pass (Mark) for the final decision to be approved.
    /// If any criterion is inconclusive (Imaginary), the result is inconclusive.
    /// </summary>
    /// <typeparam name="TInput">The input type for evaluation.</typeparam>
    /// <typeparam name="TOutput">The output type after all criteria pass.</typeparam>
    /// <param name="input">The input value to evaluate.</param>
    /// <param name="criteria">List of decision-making functions to apply.</param>
    /// <param name="onAllPass">Function to produce output when all criteria pass.</param>
    /// <returns>A combined auditable decision.</returns>
    /// <example>
    /// var decision = DecisionPipeline.Evaluate(
    ///     application,
    ///     new Func&lt;Application, AuditableDecision&lt;Application&gt;&gt;[]
    ///     {
    ///         CheckIdentity,
    ///         CheckCreditScore,
    ///         CheckFraudIndicators
    ///     },
    ///     app =&gt; new ApprovedAccount(app));
    /// </example>
    public static AuditableDecision<TOutput> Evaluate<TInput, TOutput>(
        TInput input,
        IEnumerable<Func<TInput, AuditableDecision<TInput>>> criteria,
        Func<TInput, TOutput> onAllPass)
    {
        if (criteria == null)
        {
            throw new ArgumentNullException(nameof(criteria));
        }

        var criteriaList = criteria as List<Func<TInput, AuditableDecision<TInput>>> ?? criteria.ToList();
        if (criteriaList.Count == 0)
        {
            throw new ArgumentException("At least one criterion must be provided", nameof(criteria));
        }

        var decisions = criteriaList.Select(criterion => criterion(input)).ToList();
        var allEvidence = decisions.SelectMany(d => d.Evidence).ToList();

        // Combine all states using AND logic
        var combinedState = decisions
            .Select(d => d.State)
            .Aggregate(Form.Mark, (acc, state) => acc.And(state));

        var reasonings = decisions.Select((d, i) => $"Criterion {i + 1}: {d.Reasoning}").ToList();
        var combinedReasoning = string.Join(" | ", reasonings);

        // If all criteria passed (Mark), apply the transformation
        if (combinedState == Form.Mark)
        {
            var output = onAllPass(input);
            return AuditableDecision<TOutput>.Approve(
                output,
                $"All {decisions.Count} criteria passed. {combinedReasoning}",
                allEvidence);
        }

        // If any criterion is inconclusive (Imaginary)
        if (combinedState == Form.Imaginary)
        {
            var maxPhase = decisions
                .Where(d => d.ConfidencePhase.HasValue)
                .Select(d => d.ConfidencePhase!.Value)
                .DefaultIfEmpty(0.5)
                .Max();

            return AuditableDecision<TOutput>.Inconclusive(
                maxPhase,
                $"Inconclusive evaluation. {combinedReasoning}",
                allEvidence);
        }

        // Otherwise, at least one criterion failed (Void)
        var failedCriteria = decisions
            .Select((d, i) => new { Index = i, Decision = d })
            .Where(x => x.Decision.State == Form.Void)
            .Select(x => $"Criterion {x.Index + 1}")
            .ToList();

        return AuditableDecision<TOutput>.Reject(
            $"Failed criteria: {string.Join(", ", failedCriteria)}. {combinedReasoning}",
            allEvidence);
    }

    /// <summary>
    /// Evaluates multiple decision criteria and combines them using OR logic.
    /// At least one criterion must pass (Mark) for the final decision to be approved.
    /// </summary>
    /// <typeparam name="TInput">The input type for evaluation.</typeparam>
    /// <param name="input">The input value to evaluate.</param>
    /// <param name="criteria">List of decision-making functions to apply.</param>
    /// <returns>A combined auditable decision.</returns>
    public static AuditableDecision<TInput> EvaluateAny<TInput>(
        TInput input,
        IEnumerable<Func<TInput, AuditableDecision<TInput>>> criteria)
    {
        if (criteria == null)
        {
            throw new ArgumentNullException(nameof(criteria));
        }

        var criteriaList = criteria as List<Func<TInput, AuditableDecision<TInput>>> ?? criteria.ToList();
        if (criteriaList.Count == 0)
        {
            throw new ArgumentException("At least one criterion must be provided", nameof(criteria));
        }

        var decisions = criteriaList.Select(criterion => criterion(input)).ToList();
        var allEvidence = decisions.SelectMany(d => d.Evidence).ToList();

        // Combine all states using OR logic
        var combinedState = decisions
            .Select(d => d.State)
            .Aggregate(Form.Void, (acc, state) => acc.Or(state));

        var reasonings = decisions.Select((d, i) => $"Criterion {i + 1}: {d.Reasoning}").ToList();
        var combinedReasoning = string.Join(" | ", reasonings);

        // If at least one criterion passed (Mark)
        if (combinedState == Form.Mark)
        {
            var passedCriteria = decisions
                .Select((d, i) => new { Index = i, Decision = d })
                .Where(x => x.Decision.State == Form.Mark)
                .Select(x => $"Criterion {x.Index + 1}")
                .ToList();

            return AuditableDecision<TInput>.Approve(
                input,
                $"Passed criteria: {string.Join(", ", passedCriteria)}. {combinedReasoning}",
                allEvidence);
        }

        // If any criterion is inconclusive (Imaginary) and none passed
        if (combinedState == Form.Imaginary)
        {
            var maxPhase = decisions
                .Where(d => d.ConfidencePhase.HasValue)
                .Select(d => d.ConfidencePhase!.Value)
                .DefaultIfEmpty(0.5)
                .Max();

            return AuditableDecision<TInput>.Inconclusive(
                maxPhase,
                $"No definitive pass, some inconclusive. {combinedReasoning}",
                allEvidence);
        }

        // Otherwise, all criteria failed (Void)
        return AuditableDecision<TInput>.Reject(
            $"All {decisions.Count} criteria failed. {combinedReasoning}",
            allEvidence);
    }

    /// <summary>
    /// Chains multiple decision steps in sequence, where each step depends on the previous.
    /// Stops at the first rejection or inconclusive decision.
    /// </summary>
    /// <typeparam name="T">The value type flowing through the pipeline.</typeparam>
    /// <param name="initial">The initial decision to start with.</param>
    /// <param name="steps">Subsequent decision-making steps.</param>
    /// <returns>The final decision after all steps (or the first non-Mark decision).</returns>
    public static AuditableDecision<T> Chain<T>(
        AuditableDecision<T> initial,
        params Func<T, AuditableDecision<T>>[] steps)
    {
        var current = initial;

        foreach (var step in steps)
        {
            // Stop if current decision is not approved
            if (current.State != Form.Mark || current.Value is null)
            {
                return current;
            }

            var next = step(current.Value!);
            var combinedEvidence = current.Evidence.Concat(next.Evidence).ToList();

            // Combine states
            var combinedState = current.State.And(next.State);

            if (combinedState == Form.Mark && next.Value is not null)
            {
                current = new AuditableDecision<T>(
                    Result<T, string>.Success(next.Value),
                    combinedState,
                    $"{current.Reasoning} → {next.Reasoning}",
                    combinedEvidence.ToList(),
                    DateTime.UtcNow,
                    null,
                    null);
            }
            else if (combinedState == Form.Imaginary)
            {
                return AuditableDecision<T>.Inconclusive(
                    next.ConfidencePhase ?? current.ConfidencePhase ?? 0.5,
                    $"{current.Reasoning} → {next.Reasoning}",
                    combinedEvidence);
            }
            else
            {
                return AuditableDecision<T>.Reject(
                    $"{current.Reasoning} → {next.Reasoning}",
                    combinedEvidence);
            }
        }

        return current;
    }
}
