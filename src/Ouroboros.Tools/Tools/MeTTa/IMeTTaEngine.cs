// <copyright file="IMeTTaEngine.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tools.MeTTa;

/// <summary>
/// Represents a MeTTa symbolic reasoning engine that can execute queries,
/// apply rules, and perform plan verification.
/// </summary>
public interface IMeTTaEngine : IDisposable
{
    /// <summary>
    /// Executes a MeTTa query and returns the result.
    /// </summary>
    /// <param name="query">The MeTTa query to execute.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the query result or an error message.</returns>
    Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default);

    /// <summary>
    /// Adds a fact to the MeTTa knowledge base.
    /// </summary>
    /// <param name="fact">The fact to add in MeTTa format.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result indicating success or failure.</returns>
    Task<Result<Unit, string>> AddFactAsync(string fact, CancellationToken ct = default);

    /// <summary>
    /// Applies a rule to the knowledge base.
    /// </summary>
    /// <param name="rule">The rule to apply in MeTTa format.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the application result or an error message.</returns>
    Task<Result<string, string>> ApplyRuleAsync(string rule, CancellationToken ct = default);

    /// <summary>
    /// Verifies a plan using symbolic reasoning.
    /// </summary>
    /// <param name="plan">The plan to verify in MeTTa format.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing verification result (true/false) or an error message.</returns>
    Task<Result<bool, string>> VerifyPlanAsync(string plan, CancellationToken ct = default);

    /// <summary>
    /// Resets the engine state, clearing all facts and rules.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result indicating success or failure.</returns>
    Task<Result<Unit, string>> ResetAsync(CancellationToken ct = default);
}

/// <summary>
/// Unit type for representing void/empty success results.
/// </summary>
public readonly struct Unit
{
    /// <summary>
    /// Gets the singleton Unit value.
    /// </summary>
    public static Unit Value => default;

    /// <summary>
    /// Returns string representation.
    /// </summary>
    /// <returns></returns>
    public override string ToString() => "()";
}
