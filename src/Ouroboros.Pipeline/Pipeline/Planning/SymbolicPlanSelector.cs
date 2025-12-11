// <copyright file="SymbolicPlanSelector.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Pipeline.Planning;

using Ouroboros.Tools.MeTTa;
using LangChainPipeline.Pipeline.Verification;

/// <summary>
/// Represents a plan candidate with associated symbolic properties.
/// </summary>
/// <param name="Plan">The plan being considered.</param>
/// <param name="Score">Symbolic score or ranking (higher is better).</param>
/// <param name="Explanation">Symbolic explanation for why this plan was selected.</param>
public sealed record PlanCandidate(Plan Plan, double Score, string Explanation);

/// <summary>
/// Uses symbolic reasoning to select and rank plans based on constraints and preferences.
/// Implements explainable plan selection using MeTTa backward chaining.
/// </summary>
public sealed class SymbolicPlanSelector
{
    private readonly IMeTTaEngine _engine;

    /// <summary>
    /// Initializes a new instance of the <see cref="SymbolicPlanSelector"/> class.
    /// </summary>
    /// <param name="engine">The MeTTa engine for symbolic reasoning.</param>
    public SymbolicPlanSelector(IMeTTaEngine engine)
    {
        this._engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    /// <summary>
    /// Selects the best plan from candidates using symbolic constraints.
    /// </summary>
    /// <param name="candidates">The candidate plans to evaluate.</param>
    /// <param name="context">The context for plan evaluation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The selected plan with explanation, or an error.</returns>
    public async Task<Result<PlanCandidate, string>> SelectBestPlanAsync(
        IEnumerable<Plan> candidates,
        SafeContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        List<Plan> planList = candidates.ToList();
        if (planList.Count == 0)
        {
            return Result<PlanCandidate, string>.Failure("No candidate plans provided");
        }

        List<PlanCandidate> scoredCandidates = new();

        foreach (Plan plan in planList)
        {
            // Score each plan based on symbolic constraints
            Result<PlanCandidate, string> scoreResult = await ScorePlanAsync(plan, context, ct);
            
            if (scoreResult.IsSuccess)
            {
                scoredCandidates.Add(scoreResult.Value);
            }
        }

        if (scoredCandidates.Count == 0)
        {
            return Result<PlanCandidate, string>.Failure("No valid plans found after constraint checking");
        }

        // Select the highest-scoring plan
        PlanCandidate best = scoredCandidates.OrderByDescending(c => c.Score).First();
        return Result<PlanCandidate, string>.Success(best);
    }

    /// <summary>
    /// Scores a plan using symbolic reasoning about its properties.
    /// </summary>
    /// <param name="plan">The plan to score.</param>
    /// <param name="context">The security context.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A scored plan candidate with explanation.</returns>
    public async Task<Result<PlanCandidate, string>> ScorePlanAsync(
        Plan plan,
        SafeContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        double score = 0.0;
        List<string> explanations = new();

        // Check if plan is valid in the given context
        foreach (PlanAction action in plan.Actions)
        {
            string atom = action.ToMeTTaAtom();
            string contextAtom = context.ToMeTTaAtom();
            string query = $"!(Allowed {atom} {contextAtom})";

            Result<string, string> result = await this._engine.ExecuteQueryAsync(query, ct);
            
            bool isAllowed = result.Match(
                success => ParseBooleanResult(success),
                error => false);

            if (!isAllowed)
            {
                // Plan contains forbidden actions - score very low
                score -= 1000.0;
                explanations.Add($"Action {atom} is not allowed in {contextAtom}");
            }
            else
            {
                score += 10.0;
                explanations.Add($"Action {atom} is permitted");
            }
        }

        // Query symbolic properties for additional scoring
        string complexityQuery = $"!(PlanComplexity {plan.Actions.Count})";
        Result<string, string> complexityResult = await this._engine.ExecuteQueryAsync(complexityQuery, ct);
        
        complexityResult.Match(
            success =>
            {
                // Prefer simpler plans (fewer actions)
                score -= plan.Actions.Count * 0.5;
                explanations.Add($"Plan has {plan.Actions.Count} actions (simpler is better)");
                return Unit.Value;
            },
            error => Unit.Value);

        string explanation = string.Join("; ", explanations);
        return Result<PlanCandidate, string>.Success(new PlanCandidate(plan, score, explanation));
    }

    /// <summary>
    /// Checks if a plan satisfies a specific constraint.
    /// </summary>
    /// <param name="plan">The plan to check.</param>
    /// <param name="constraint">The constraint description in natural language.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the constraint is satisfied, false otherwise.</returns>
    public async Task<Result<bool, string>> CheckConstraintAsync(
        Plan plan,
        string constraint,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrEmpty(constraint);

        // Encode the constraint as a MeTTa query
        string encodedConstraint = EncodeConstraintForPlan(plan, constraint);
        string query = $"!(CheckConstraint {encodedConstraint})";

        Result<string, string> result = await this._engine.ExecuteQueryAsync(query, ct);

        return result.Match(
            success => Result<bool, string>.Success(ParseBooleanResult(success)),
            error => Result<bool, string>.Failure(error));
    }

    /// <summary>
    /// Explains why a plan was selected or rejected.
    /// </summary>
    /// <param name="plan">The plan to explain.</param>
    /// <param name="context">The context for explanation.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A human-readable explanation of the plan's properties.</returns>
    public async Task<Result<string, string>> ExplainPlanAsync(
        Plan plan,
        SafeContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        Result<PlanCandidate, string> scoreResult = await ScorePlanAsync(plan, context, ct);

        return scoreResult.Match(
            candidate => Result<string, string>.Success(
                $"Plan '{plan.Description}' scored {candidate.Score:F2}. {candidate.Explanation}"),
            error => Result<string, string>.Failure(error));
    }

    /// <summary>
    /// Initializes the plan selector with scoring rules.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public async Task<Result<Unit, string>> InitializeAsync(CancellationToken ct = default)
    {
        string[] rules =
        [
            "; Plan Selection Rules",
            "(: PlanComplexity (-> Number Score))",
            "(: CheckConstraint (-> String Bool))",
            "",
            "; Scoring preferences",
            "(= (PreferSimple $actionCount) (< $actionCount 5))",
            "(= (PreferSafe $context) (= $context ReadOnly))",
            "",
            "; Constraint definitions",
            "(= (CheckConstraint \"no-writes\") (not (HasAction FileSystemAction \"write\")))",
            "(= (CheckConstraint \"no-network\") (not (HasAction NetworkAction $op)))",
        ];

        foreach (string rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule) || rule.TrimStart().StartsWith(';'))
            {
                continue;
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
    /// Parses a MeTTa boolean result.
    /// </summary>
    private static bool ParseBooleanResult(string result)
    {
        if (string.IsNullOrWhiteSpace(result))
        {
            return false;
        }

        string normalized = result.Trim().ToLowerInvariant();
        return normalized == "true" ||
               normalized == "[true]" ||
               normalized == "(true)" ||
               normalized == "[[true]]" ||
               normalized.Contains("true");
    }

    /// <summary>
    /// Encodes a natural language constraint as a MeTTa expression.
    /// </summary>
    private static string EncodeConstraintForPlan(Plan plan, string constraint)
    {
        // Simple encoding - in practice, this could use NLP or predefined mappings
        return constraint.ToLowerInvariant() switch
        {
            "no writes" or "read-only" => "\"no-writes\"",
            "no network" or "offline" => "\"no-network\"",
            _ => $"\"{constraint}\""
        };
    }
}
