// <copyright file="MeTTaVerificationStep.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Pipeline.Verification;

using Ouroboros.Tools.MeTTa;

/// <summary>
/// Pipeline step that enforces symbolic guard rails using MeTTa's type system.
/// Verifies that LLM-generated plans conform to security constraints before execution.
/// </summary>
/// <remarks>
/// This step implements neuro-symbolic verification where:
/// - The LLM generates a plan (neural)
/// - MeTTa verifies the plan against symbolic rules (symbolic)
/// - Only valid plans proceed to execution
/// 
/// Guard rails are deterministic - they evaluate to True/False 100% reliably.
/// </remarks>
public sealed class MeTTaVerificationStep
{
    private readonly IMeTTaEngine _engine;
    private readonly SafeContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="MeTTaVerificationStep"/> class.
    /// </summary>
    /// <param name="engine">The MeTTa engine for symbolic verification.</param>
    /// <param name="context">The security context for verification.</param>
    public MeTTaVerificationStep(IMeTTaEngine engine, SafeContext context = SafeContext.ReadOnly)
    {
        this._engine = engine ?? throw new ArgumentNullException(nameof(engine));
        this._context = context;
    }

    /// <summary>
    /// Verifies a plan against the symbolic guard rails.
    /// </summary>
    /// <param name="plan">The plan to verify.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// A Result containing the verified plan on success,
    /// or a SecurityException error if guard rails are violated.
    /// </returns>
    public async Task<Result<Plan, SecurityException>> VerifyAsync(Plan plan, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        // Verify each action in the plan
        foreach (PlanAction action in plan.Actions)
        {
            string atom = action.ToMeTTaAtom();
            string contextAtom = this._context.ToMeTTaAtom();

            // Construct the MeTTa query to check if action is allowed
            string query = $"!(Allowed {atom} {contextAtom})";

            Result<string, string> result = await this._engine.ExecuteQueryAsync(query, ct);

            // Check the result
            bool isAllowed = result.Match(
                success => this.ParseAllowedResult(success),
                error => false); // If engine fails, treat as not allowed

            if (!isAllowed)
            {
                string message = $"Guard Rail Violation: Action {atom} is forbidden in {contextAtom} context.";
                return Result<Plan, SecurityException>.Failure(
                    new SecurityException(message, atom, this._context));
            }
        }

        return Result<Plan, SecurityException>.Success(plan);
    }

    /// <summary>
    /// Creates a Kleisli arrow for plan verification.
    /// </summary>
    /// <returns>A step that verifies plans.</returns>
    public KleisliResult<Plan, Plan, SecurityException> AsArrow()
        => plan => this.VerifyAsync(plan);

    /// <summary>
    /// Initializes the MeTTa engine with guard rail rules.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public async Task<Result<Unit, string>> InitializeGuardRailsAsync(CancellationToken ct = default)
    {
        // Define the type system for actions
        string[] rules =
        [
            "; Define the Type Hierarchy for Actions",
            "(: Action Type)",
            "(: FileSystemAction (-> String Action))",
            "(: NetworkAction (-> String Action))",
            "(: ToolAction (-> String Action))",
            "",
            "; Define Safe Contexts (The Guard Rails)",
            "(: SafeContext Type)",
            "(: ReadOnly SafeContext)",
            "(: FullAccess SafeContext)",
            "",
            "; Define the Permission Logic",
            "(: Allowed (-> Action SafeContext Bool))",
            "",
            "; Reading is allowed in ReadOnly context",
            "(= (Allowed (FileSystemAction \"read\") ReadOnly) True)",
            "; Writing is NOT allowed in ReadOnly context",
            "(= (Allowed (FileSystemAction \"write\") ReadOnly) False)",
            "; Delete is NOT allowed in ReadOnly context",
            "(= (Allowed (FileSystemAction \"delete\") ReadOnly) False)",
            "; Network GET is allowed in ReadOnly context",
            "(= (Allowed (NetworkAction \"get\") ReadOnly) True)",
            "; Network POST is NOT allowed in ReadOnly context",
            "(= (Allowed (NetworkAction \"post\") ReadOnly) False)",
            "; All actions allowed in FullAccess",
            "(= (Allowed $action FullAccess) True)",
        ];

        foreach (string rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule) || rule.TrimStart().StartsWith(';'))
            {
                continue; // Skip empty lines and comments
            }

            Result<Unit, string> result = await this._engine.AddFactAsync(rule, ct);
            if (result.IsFailure)
            {
                return result;
            }
        }

        return Result<Unit, string>.Success(Unit.Value);
    }

    /// <summary>
    /// Parses the MeTTa result to determine if an action is allowed.
    /// </summary>
    private bool ParseAllowedResult(string result)
    {
        // MeTTa returns results in various formats
        // We check for True/true or absence of False/false
        string normalized = result.Trim().ToLowerInvariant();
        return normalized.Contains("true") && !normalized.Contains("false");
    }
}

/// <summary>
/// Extension methods for integrating MeTTa verification into pipelines.
/// </summary>
public static class MeTTaVerificationExtensions
{
    /// <summary>
    /// Adds MeTTa verification to a plan generation pipeline.
    /// </summary>
    /// <param name="planStep">The step that generates plans.</param>
    /// <param name="engine">The MeTTa engine for verification.</param>
    /// <param name="context">The security context.</param>
    /// <typeparam name="TInput">The input type.</typeparam>
    /// <returns>A composed step that generates and verifies plans.</returns>
    public static Step<TInput, Result<Plan, SecurityException>> WithVerification<TInput>(
        this Step<TInput, Plan> planStep,
        IMeTTaEngine engine,
        SafeContext context = SafeContext.ReadOnly)
    {
        MeTTaVerificationStep verifier = new(engine, context);
        return async input =>
        {
            Plan plan = await planStep(input);
            return await verifier.VerifyAsync(plan);
        };
    }
}
