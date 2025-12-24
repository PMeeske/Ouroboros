// <copyright file="AuditableDecision.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Core.LawsOfForm;

/// <summary>
/// Represents a decision with full audit trail and evidence.
/// Combines the decision result with certainty information and reasoning.
/// </summary>
/// <typeparam name="T">The type of the decision result.</typeparam>
public sealed record AuditableDecision<T>
{
    /// <summary>
    /// Gets the result of the decision.
    /// </summary>
    public Result<T, string> Result { get; init; }

    /// <summary>
    /// Gets the certainty level of the decision as a Form.
    /// </summary>
    public Form Certainty { get; init; }

    /// <summary>
    /// Gets the reasoning behind the decision.
    /// </summary>
    public string Reasoning { get; init; }

    /// <summary>
    /// Gets the evidence that led to this decision.
    /// Each piece of evidence includes a criterion name and its evaluation.
    /// </summary>
    public IReadOnlyList<Evidence> EvidenceTrail { get; init; }

    /// <summary>
    /// Gets the timestamp when the decision was made.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Gets optional metadata about the decision context.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AuditableDecision{T}"/> class.
    /// </summary>
    /// <param name="result">The decision result.</param>
    /// <param name="certainty">The certainty level.</param>
    /// <param name="reasoning">The reasoning.</param>
    /// <param name="evidenceTrail">The evidence trail.</param>
    /// <param name="timestamp">The timestamp.</param>
    /// <param name="metadata">Optional metadata.</param>
    public AuditableDecision(
        Result<T, string> result,
        Form certainty,
        string reasoning,
        IReadOnlyList<Evidence> evidenceTrail,
        DateTime? timestamp = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        this.Result = result;
        this.Certainty = certainty;
        this.Reasoning = reasoning;
        this.EvidenceTrail = evidenceTrail;
        this.Timestamp = timestamp ?? DateTime.UtcNow;
        this.Metadata = metadata ?? new Dictionary<string, string>();
    }

    /// <summary>
    /// Creates a decision with Mark certainty (certain affirmative).
    /// </summary>
    /// <param name="value">The success value.</param>
    /// <param name="reasoning">The reasoning.</param>
    /// <param name="evidence">The evidence trail.</param>
    /// <returns>An auditable decision with Mark certainty.</returns>
    public static AuditableDecision<T> Approve(
        T value,
        string reasoning,
        params Evidence[] evidence)
    {
        return new AuditableDecision<T>(
            Result<T, string>.Success(value),
            Form.Cross(),
            reasoning,
            evidence);
    }

    /// <summary>
    /// Creates a decision with Void certainty (certain negative).
    /// </summary>
    /// <param name="error">The error message.</param>
    /// <param name="reasoning">The reasoning.</param>
    /// <param name="evidence">The evidence trail.</param>
    /// <returns>An auditable decision with Void certainty.</returns>
    public static AuditableDecision<T> Reject(
        string error,
        string reasoning,
        params Evidence[] evidence)
    {
        return new AuditableDecision<T>(
            Result<T, string>.Failure(error),
            Form.Void,
            reasoning,
            evidence);
    }

    /// <summary>
    /// Creates a decision with Imaginary certainty (uncertain/requires escalation).
    /// </summary>
    /// <param name="error">The error message describing the uncertainty.</param>
    /// <param name="reasoning">The reasoning.</param>
    /// <param name="evidence">The evidence trail.</param>
    /// <returns>An auditable decision with Imaginary certainty.</returns>
    public static AuditableDecision<T> Uncertain(
        string error,
        string reasoning,
        params Evidence[] evidence)
    {
        return new AuditableDecision<T>(
            Result<T, string>.Failure(error),
            Form.Imaginary,
            reasoning,
            evidence);
    }

    /// <summary>
    /// Adds additional evidence to the decision.
    /// </summary>
    /// <param name="evidence">The evidence to add.</param>
    /// <returns>A new decision with the added evidence.</returns>
    public AuditableDecision<T> WithEvidence(Evidence evidence)
    {
        var newEvidence = new List<Evidence>(this.EvidenceTrail) { evidence };
        return this with { EvidenceTrail = newEvidence };
    }

    /// <summary>
    /// Adds metadata to the decision.
    /// </summary>
    /// <param name="key">The metadata key.</param>
    /// <param name="value">The metadata value.</param>
    /// <returns>A new decision with the added metadata.</returns>
    public AuditableDecision<T> WithMetadata(string key, string value)
    {
        var newMetadata = new Dictionary<string, string>(this.Metadata)
        {
            [key] = value
        };
        return this with { Metadata = newMetadata };
    }

    /// <summary>
    /// Converts the decision to an audit log entry format.
    /// </summary>
    /// <returns>A formatted audit log entry.</returns>
    public string ToAuditEntry()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[{this.Timestamp:O}] Decision: {this.Certainty}");
        sb.AppendLine($"Result: {(this.Result.IsSuccess ? "Success" : "Failure")}");
        sb.AppendLine($"Reasoning: {this.Reasoning}");
        sb.AppendLine("Evidence Trail:");

        foreach (var evidence in this.EvidenceTrail)
        {
            sb.AppendLine($"  - {evidence.CriterionName}: {evidence.Evaluation} ({evidence.Description})");
        }

        if (this.Metadata.Count > 0)
        {
            sb.AppendLine("Metadata:");
            foreach (var (key, value) in this.Metadata)
            {
                sb.AppendLine($"  - {key}: {value}");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Pattern matching on the certainty state.
    /// </summary>
    /// <typeparam name="TResult">The result type.</typeparam>
    /// <param name="onCertain">Function to execute if certain (Mark).</param>
    /// <param name="onRejected">Function to execute if rejected (Void).</param>
    /// <param name="onUncertain">Function to execute if uncertain (Imaginary).</param>
    /// <returns>The result of the matched function.</returns>
    public TResult Match<TResult>(
        Func<T, TResult> onCertain,
        Func<string, TResult> onRejected,
        Func<string, TResult> onUncertain)
    {
        return this.Certainty.Match(
            onMark: () => this.Result.IsSuccess ? onCertain(this.Result.Value) : onRejected(this.Result.Error),
            onVoid: () => onRejected(this.Result.Error),
            onImaginary: () => onUncertain(this.Result.Error));
    }
}

/// <summary>
/// Represents a piece of evidence in a decision trail.
/// </summary>
public sealed record Evidence
{
    /// <summary>
    /// Gets the name of the criterion being evaluated.
    /// </summary>
    public string CriterionName { get; init; }

    /// <summary>
    /// Gets the evaluation result as a Form.
    /// </summary>
    public Form Evaluation { get; init; }

    /// <summary>
    /// Gets a human-readable description of the evidence.
    /// </summary>
    public string Description { get; init; }

    /// <summary>
    /// Gets the timestamp when the evidence was collected.
    /// </summary>
    public DateTime Timestamp { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Evidence"/> class.
    /// </summary>
    /// <param name="criterionName">The criterion name.</param>
    /// <param name="evaluation">The evaluation result.</param>
    /// <param name="description">The description.</param>
    /// <param name="timestamp">Optional timestamp.</param>
    public Evidence(
        string criterionName,
        Form evaluation,
        string description,
        DateTime? timestamp = null)
    {
        this.CriterionName = criterionName;
        this.Evaluation = evaluation;
        this.Description = description;
        this.Timestamp = timestamp ?? DateTime.UtcNow;
    }
}
