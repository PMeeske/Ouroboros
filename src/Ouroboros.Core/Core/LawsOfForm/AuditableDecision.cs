// <copyright file="AuditableDecision.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Core.LawsOfForm;

using LangChainPipeline.Core.Monads;

/// <summary>
/// Represents a decision that can be audited for compliance purposes.
/// Uses Laws of Form three-valued logic to explicitly track certainty.
/// Suitable for regulated domains where uncertain decisions must be escalated.
/// </summary>
/// <typeparam name="T">The type of the approved value.</typeparam>
public sealed class AuditableDecision<T>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuditableDecision{T}"/> class.
    /// </summary>
    /// <param name="state">The form state (Mark = approved, Void = rejected, Imaginary = inconclusive).</param>
    /// <param name="value">The approved value (only present when state is Mark).</param>
    /// <param name="reasoning">The explanation for the decision.</param>
    /// <param name="evidence">List of evidence items supporting the decision.</param>
    /// <param name="confidencePhase">For Imaginary states, represents the oscillation phase (0.0 to 1.0).</param>
    /// <param name="timestamp">When the decision was made.</param>
    internal AuditableDecision(
        Form state,
        Option<T> value,
        string reasoning,
        IReadOnlyList<string> evidence,
        double? confidencePhase,
        DateTimeOffset timestamp)
    {
        this.State = state;
        this.Value = value;
        this.Reasoning = reasoning;
        this.Evidence = evidence;
        this.ConfidencePhase = confidencePhase;
        this.Timestamp = timestamp;
    }

    /// <summary>
    /// Gets the form state of the decision.
    /// </summary>
    public Form State { get; }

    /// <summary>
    /// Gets the approved value (only present when State is Mark).
    /// </summary>
    public Option<T> Value { get; }

    /// <summary>
    /// Gets the reasoning behind the decision.
    /// </summary>
    public string Reasoning { get; }

    /// <summary>
    /// Gets the evidence chain supporting the decision.
    /// </summary>
    public IReadOnlyList<string> Evidence { get; }

    /// <summary>
    /// Gets the confidence phase for Imaginary (inconclusive) decisions.
    /// Value between 0.0 and 1.0 representing the oscillation phase.
    /// </summary>
    public double? ConfidencePhase { get; }

    /// <summary>
    /// Gets the timestamp when the decision was made.
    /// </summary>
    public DateTimeOffset Timestamp { get; }

    /// <summary>
    /// Gets a value indicating whether this decision requires human review.
    /// True for Imaginary (inconclusive) states.
    /// </summary>
    public bool RequiresHumanReview => this.State == Form.Imaginary;

    /// <summary>
    /// Gets a human-readable compliance status string.
    /// </summary>
    public string ComplianceStatus => this.State switch
    {
        Form.Mark => "APPROVED",
        Form.Void => "REJECTED",
        Form.Imaginary => $"INCONCLUSIVE (confidence phase: {this.ConfidencePhase:F2})",
        _ => "UNKNOWN",
    };

    /// <summary>
    /// Creates an approved decision (Mark state).
    /// </summary>
    /// <param name="value">The approved value.</param>
    /// <param name="reasoning">The reasoning for approval.</param>
    /// <param name="evidence">Evidence supporting the approval.</param>
    /// <returns>An approved auditable decision.</returns>
    /// <example>
    /// var decision = AuditableDecision&lt;AccountApplication&gt;.Approve(
    ///     application,
    ///     "All KYC checks passed",
    ///     new[] { "ID verified", "Address confirmed", "Credit check passed" });
    /// </example>
    public static AuditableDecision<T> Approve(T value, string reasoning, IEnumerable<string>? evidence = null)
    {
        return new AuditableDecision<T>(
            Form.Mark,
            Option<T>.Some(value),
            reasoning,
            evidence?.ToList() ?? new List<string>(),
            null,
            DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Creates a rejected decision (Void state).
    /// </summary>
    /// <param name="reasoning">The reasoning for rejection.</param>
    /// <param name="evidence">Evidence supporting the rejection.</param>
    /// <returns>A rejected auditable decision.</returns>
    /// <example>
    /// var decision = AuditableDecision&lt;AccountApplication&gt;.Reject(
    ///     "Failed identity verification",
    ///     new[] { "Document mismatch", "Suspicious activity detected" });
    /// </example>
    public static AuditableDecision<T> Reject(string reasoning, IEnumerable<string>? evidence = null)
    {
        return new AuditableDecision<T>(
            Form.Void,
            Option<T>.None(),
            reasoning,
            evidence?.ToList() ?? new List<string>(),
            null,
            DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Creates an inconclusive decision (Imaginary state) that requires human review.
    /// </summary>
    /// <param name="confidencePhase">A value between 0.0 and 1.0 representing confidence oscillation.</param>
    /// <param name="reasoning">The reasoning for inconclusiveness.</param>
    /// <param name="evidence">Evidence collected so far.</param>
    /// <returns>An inconclusive auditable decision requiring human review.</returns>
    /// <example>
    /// var decision = AuditableDecision&lt;AccountApplication&gt;.Inconclusive(
    ///     0.65,
    ///     "Conflicting signals detected - manual review required",
    ///     new[] { "Credit score borderline", "Recent address change", "Limited credit history" });
    /// </example>
    public static AuditableDecision<T> Inconclusive(
        double confidencePhase,
        string reasoning,
        IEnumerable<string>? evidence = null)
    {
        if (confidencePhase is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(confidencePhase), "Confidence phase must be between 0.0 and 1.0");
        }

        return new AuditableDecision<T>(
            Form.Imaginary,
            Option<T>.None(),
            reasoning,
            evidence?.ToList() ?? new List<string>(),
            confidencePhase,
            DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Converts the decision to an audit log entry.
    /// </summary>
    /// <returns>A formatted audit entry string.</returns>
    public string ToAuditEntry()
    {
        var evidenceStr = this.Evidence.Count > 0
            ? string.Join("; ", this.Evidence)
            : "No evidence recorded";

        return $"[{this.Timestamp:yyyy-MM-dd HH:mm:ss}] {this.ComplianceStatus}\n" +
               $"Reasoning: {this.Reasoning}\n" +
               $"Evidence: {evidenceStr}";
    }

    /// <summary>
    /// Maps the approved value to a new type while preserving decision metadata.
    /// Only applicable for Mark (approved) states.
    /// </summary>
    /// <typeparam name="TResult">The new value type.</typeparam>
    /// <param name="mapper">Function to transform the approved value.</param>
    /// <returns>A new decision with the transformed value.</returns>
    public AuditableDecision<TResult> Map<TResult>(Func<T, TResult> mapper)
    {
        var newValue = this.Value.Map(mapper);
        return new AuditableDecision<TResult>(
            this.State,
            newValue,
            this.Reasoning,
            this.Evidence,
            this.ConfidencePhase,
            this.Timestamp);
    }

    /// <summary>
    /// Combines this decision with another using AND logic.
    /// Both must be approved for the result to be approved.
    /// </summary>
    /// <param name="other">The other decision to combine with.</param>
    /// <returns>A combined decision.</returns>
    public AuditableDecision<(T, T)> And(AuditableDecision<T> other)
    {
        var combinedState = this.State.And(other.State);
        var combinedEvidence = this.Evidence.Concat(other.Evidence).ToList();
        var reasoning = $"Combined: ({this.Reasoning}) AND ({other.Reasoning})";

        if (combinedState == Form.Mark && this.Value.HasValue && other.Value.HasValue)
        {
            return new AuditableDecision<(T, T)>(
                combinedState,
                Option<(T, T)>.Some((this.Value.Value!, other.Value.Value!)),
                reasoning,
                combinedEvidence,
                null,
                DateTimeOffset.UtcNow);
        }

        return new AuditableDecision<(T, T)>(
            combinedState,
            Option<(T, T)>.None(),
            reasoning,
            combinedEvidence,
            combinedState == Form.Imaginary ? Math.Max(this.ConfidencePhase ?? 0, other.ConfidencePhase ?? 0) : null,
            DateTimeOffset.UtcNow);
    }
}
