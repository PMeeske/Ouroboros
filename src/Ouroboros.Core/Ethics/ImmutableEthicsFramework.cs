// <copyright file="ImmutableEthicsFramework.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Core.Monads;

namespace Ouroboros.Core.Ethics;

/// <summary>
/// Immutable implementation of the Ethics Framework.
/// All methods are sealed and cannot be overridden.
/// Core principles cannot be modified at runtime.
/// This class provides the foundational ethical evaluation for the Ouroboros system.
/// </summary>
public sealed class ImmutableEthicsFramework : IEthicsFramework
{
    private readonly IReadOnlyList<EthicalPrinciple> _corePrinciples;
    private readonly IEthicsAuditLog _auditLog;
    private readonly IEthicalReasoner _reasoner;

    /// <summary>
    /// Initializes a new instance of the <see cref="ImmutableEthicsFramework"/> class.
    /// Constructor is internal to enforce creation through factory.
    /// </summary>
    /// <param name="auditLog">The audit log for recording evaluations.</param>
    /// <param name="reasoner">The ethical reasoning component.</param>
    internal ImmutableEthicsFramework(IEthicsAuditLog auditLog, IEthicalReasoner reasoner)
    {
        ArgumentNullException.ThrowIfNull(auditLog);
        ArgumentNullException.ThrowIfNull(reasoner);

        _auditLog = auditLog;
        _reasoner = reasoner;
        _corePrinciples = EthicalPrinciple.GetCorePrinciples();
    }

    /// <inheritdoc/>
    public IReadOnlyList<EthicalPrinciple> GetCorePrinciples()
    {
        // Return a copy to prevent modification
        return _corePrinciples.ToList();
    }

    /// <inheritdoc/>
    public async Task<Result<EthicalClearance, string>> EvaluateActionAsync(
        ProposedAction action,
        ActionContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            // Analyze the action against ethical principles
            var (violations, concerns) = _reasoner.AnalyzeAction(action, context, _corePrinciples);

            EthicalClearance clearance;

            if (violations.Count > 0)
            {
                // Action violates ethical principles - deny
                clearance = EthicalClearance.Denied(
                    $"Action '{action.ActionType}' violates ethical principles",
                    violations,
                    _corePrinciples);
            }
            else if (_reasoner.RequiresHumanApproval(action, context))
            {
                // High-risk action requires human approval
                clearance = EthicalClearance.RequiresApproval(
                    $"Action '{action.ActionType}' requires human approval due to high risk",
                    concerns,
                    _corePrinciples);
            }
            else if (concerns.Count > 0)
            {
                // Action has concerns but is permitted
                clearance = new EthicalClearance
                {
                    IsPermitted = true,
                    Level = EthicalClearanceLevel.PermittedWithConcerns,
                    RelevantPrinciples = _corePrinciples,
                    Violations = Array.Empty<EthicalViolation>(),
                    Concerns = concerns,
                    Reasoning = $"Action '{action.ActionType}' is permitted with {concerns.Count} concern(s)"
                };
            }
            else
            {
                // Action is permitted
                clearance = EthicalClearance.Permitted(
                    $"Action '{action.ActionType}' complies with ethical principles",
                    _corePrinciples);
            }

            // Log the evaluation
            await LogEvaluationAsync("Action", action.Description, context, clearance, ct);

            return Result<EthicalClearance, string>.Success(clearance);
        }
        catch (Exception ex)
        {
            return Result<EthicalClearance, string>.Failure($"Ethics evaluation failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<EthicalClearance, string>> EvaluatePlanAsync(
        PlanContext planContext,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(planContext);

        try
        {
            var allViolations = new List<EthicalViolation>();
            var allConcerns = new List<EthicalConcern>();

            // Evaluate each step in the plan
            foreach (var step in planContext.Plan.Steps)
            {
                var proposedAction = new ProposedAction
                {
                    ActionType = step.Action,
                    Description = $"{step.Action}: {step.ExpectedOutcome}",
                    Parameters = step.Parameters,
                    TargetEntity = step.Parameters.ContainsKey("target") 
                        ? step.Parameters["target"]?.ToString() 
                        : null,
                    PotentialEffects = new[] { step.ExpectedOutcome }
                };

                var (violations, concerns) = _reasoner.AnalyzeAction(
                    proposedAction,
                    planContext.ActionContext,
                    _corePrinciples);

                allViolations.AddRange(violations);
                allConcerns.AddRange(concerns);
            }

            // Check for high-risk plan patterns
            if (planContext.EstimatedRisk > 0.7)
            {
                allConcerns.Add(new EthicalConcern
                {
                    RelatedPrinciple = EthicalPrinciple.HumanOversight,
                    Description = $"Plan has high estimated risk: {planContext.EstimatedRisk:F2}",
                    Level = ConcernLevel.High,
                    RecommendedAction = "Request human review before execution"
                });
            }

            EthicalClearance clearance;

            if (allViolations.Count > 0)
            {
                clearance = EthicalClearance.Denied(
                    $"Plan '{planContext.Plan.Goal}' contains {allViolations.Count} ethical violation(s)",
                    allViolations,
                    _corePrinciples);
            }
            else if (planContext.EstimatedRisk > 0.7 || allConcerns.Any(c => c.Level == ConcernLevel.High))
            {
                clearance = EthicalClearance.RequiresApproval(
                    $"Plan '{planContext.Plan.Goal}' requires human approval",
                    allConcerns,
                    _corePrinciples);
            }
            else if (allConcerns.Count > 0)
            {
                clearance = new EthicalClearance
                {
                    IsPermitted = true,
                    Level = EthicalClearanceLevel.PermittedWithConcerns,
                    RelevantPrinciples = _corePrinciples,
                    Violations = Array.Empty<EthicalViolation>(),
                    Concerns = allConcerns,
                    Reasoning = $"Plan '{planContext.Plan.Goal}' is permitted with {allConcerns.Count} concern(s)"
                };
            }
            else
            {
                clearance = EthicalClearance.Permitted(
                    $"Plan '{planContext.Plan.Goal}' complies with ethical principles",
                    _corePrinciples);
            }

            await LogEvaluationAsync("Plan", planContext.Plan.Goal, planContext.ActionContext, clearance, ct);

            return Result<EthicalClearance, string>.Success(clearance);
        }
        catch (Exception ex)
        {
            return Result<EthicalClearance, string>.Failure($"Plan evaluation failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<EthicalClearance, string>> EvaluateGoalAsync(
        Goal goal,
        ActionContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(goal);
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            var violations = new List<EthicalViolation>();
            var concerns = new List<EthicalConcern>();

            // Check goal description for harmful patterns
            if (_reasoner.ContainsHarmfulPatterns(goal.Description))
            {
                violations.Add(new EthicalViolation
                {
                    ViolatedPrinciple = EthicalPrinciple.DoNoHarm,
                    Description = "Goal description contains harmful intent",
                    Severity = ViolationSeverity.Critical,
                    Evidence = $"Goal: {goal.Description}",
                    AffectedParties = new[] { "Users", "System" }
                });
            }

            // Safety goals must always be permitted (but still evaluated)
            if (goal.Type.Equals("Safety", StringComparison.OrdinalIgnoreCase))
            {
                concerns.Add(new EthicalConcern
                {
                    RelatedPrinciple = EthicalPrinciple.DoNoHarm,
                    Description = "Safety goal identified",
                    Level = ConcernLevel.Info,
                    RecommendedAction = "Prioritize this goal"
                });
            }

            // High-priority goals warrant extra scrutiny
            if (goal.Priority > 0.9 && !goal.Type.Equals("Safety", StringComparison.OrdinalIgnoreCase))
            {
                concerns.Add(new EthicalConcern
                {
                    RelatedPrinciple = EthicalPrinciple.HumanOversight,
                    Description = "Very high priority goal detected",
                    Level = ConcernLevel.Medium,
                    RecommendedAction = "Ensure goal alignment with user intent"
                });
            }

            EthicalClearance clearance;

            if (violations.Count > 0)
            {
                clearance = EthicalClearance.Denied(
                    $"Goal '{goal.Description}' violates ethical principles",
                    violations,
                    _corePrinciples);
            }
            else if (concerns.Any(c => c.Level == ConcernLevel.High))
            {
                clearance = EthicalClearance.RequiresApproval(
                    $"Goal '{goal.Description}' requires human approval",
                    concerns,
                    _corePrinciples);
            }
            else
            {
                clearance = EthicalClearance.Permitted(
                    $"Goal '{goal.Description}' is ethically aligned",
                    _corePrinciples,
                    confidenceScore: violations.Count == 0 && concerns.Count == 0 ? 1.0 : 0.8);
            }

            await LogEvaluationAsync("Goal", goal.Description, context, clearance, ct);

            return Result<EthicalClearance, string>.Success(clearance);
        }
        catch (Exception ex)
        {
            return Result<EthicalClearance, string>.Failure($"Goal evaluation failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<EthicalClearance, string>> EvaluateSkillAsync(
        SkillUsageContext skillContext,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(skillContext);

        try
        {
            var violations = new List<EthicalViolation>();
            var concerns = new List<EthicalConcern>();

            // Check skill description and steps for harmful patterns
            if (_reasoner.ContainsHarmfulPatterns(skillContext.Skill.Description))
            {
                violations.Add(new EthicalViolation
                {
                    ViolatedPrinciple = EthicalPrinciple.DoNoHarm,
                    Description = "Skill contains harmful operations",
                    Severity = ViolationSeverity.High,
                    Evidence = $"Skill: {skillContext.Skill.Description}",
                    AffectedParties = new[] { "Users", "System" }
                });
            }

            // Check if skill has low success rate
            if (skillContext.HistoricalSuccessRate < 0.5 && skillContext.Skill.UsageCount > 5)
            {
                concerns.Add(new EthicalConcern
                {
                    RelatedPrinciple = EthicalPrinciple.Transparency,
                    Description = $"Skill has low success rate: {skillContext.HistoricalSuccessRate:P0}",
                    Level = ConcernLevel.Medium,
                    RecommendedAction = "Consider alternative approaches or improve skill"
                });
            }

            EthicalClearance clearance;

            if (violations.Count > 0)
            {
                clearance = EthicalClearance.Denied(
                    $"Skill '{skillContext.Skill.Name}' violates ethical principles",
                    violations,
                    _corePrinciples);
            }
            else if (concerns.Count > 0)
            {
                clearance = new EthicalClearance
                {
                    IsPermitted = true,
                    Level = EthicalClearanceLevel.PermittedWithConcerns,
                    RelevantPrinciples = _corePrinciples,
                    Violations = Array.Empty<EthicalViolation>(),
                    Concerns = concerns,
                    Reasoning = $"Skill '{skillContext.Skill.Name}' usage permitted with concerns"
                };
            }
            else
            {
                clearance = EthicalClearance.Permitted(
                    $"Skill '{skillContext.Skill.Name}' usage is ethically compliant",
                    _corePrinciples);
            }

            await LogEvaluationAsync("Skill", skillContext.Skill.Name, skillContext.ActionContext, clearance, ct);

            return Result<EthicalClearance, string>.Success(clearance);
        }
        catch (Exception ex)
        {
            return Result<EthicalClearance, string>.Failure($"Skill evaluation failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<EthicalClearance, string>> EvaluateResearchAsync(
        string researchDescription,
        ActionContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(researchDescription);
        ArgumentNullException.ThrowIfNull(context);

        try
        {
            var violations = new List<EthicalViolation>();
            var concerns = new List<EthicalConcern>();

            // Check for harmful research patterns
            if (_reasoner.ContainsHarmfulPatterns(researchDescription))
            {
                violations.Add(new EthicalViolation
                {
                    ViolatedPrinciple = EthicalPrinciple.DoNoHarm,
                    Description = "Research activity may cause harm",
                    Severity = ViolationSeverity.High,
                    Evidence = researchDescription,
                    AffectedParties = new[] { "Users", "System", "Data subjects" }
                });
            }

            // Research on sensitive topics requires oversight
            var sensitiveKeywords = new[] { "user data", "personal", "private", "confidential", "experiment on users" };
            if (sensitiveKeywords.Any(k => researchDescription.Contains(k, StringComparison.OrdinalIgnoreCase)))
            {
                concerns.Add(new EthicalConcern
                {
                    RelatedPrinciple = EthicalPrinciple.Privacy,
                    Description = "Research involves sensitive data or human subjects",
                    Level = ConcernLevel.High,
                    RecommendedAction = "Require explicit consent and human oversight"
                });
            }

            EthicalClearance clearance;

            if (violations.Count > 0)
            {
                clearance = EthicalClearance.Denied(
                    "Research activity violates ethical principles",
                    violations,
                    _corePrinciples);
            }
            else if (concerns.Any(c => c.Level == ConcernLevel.High))
            {
                clearance = EthicalClearance.RequiresApproval(
                    "Research activity requires human approval",
                    concerns,
                    _corePrinciples);
            }
            else
            {
                clearance = EthicalClearance.Permitted(
                    "Research activity is ethically compliant",
                    _corePrinciples);
            }

            await LogEvaluationAsync("Research", researchDescription, context, clearance, ct);

            return Result<EthicalClearance, string>.Success(clearance);
        }
        catch (Exception ex)
        {
            return Result<EthicalClearance, string>.Failure($"Research evaluation failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<EthicalClearance, string>> EvaluateSelfModificationAsync(
        SelfModificationRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            var violations = new List<EthicalViolation>();
            var concerns = new List<EthicalConcern>();

            // All self-modification requires human approval
            concerns.Add(new EthicalConcern
            {
                RelatedPrinciple = EthicalPrinciple.HumanOversight,
                Description = "Self-modification detected",
                Level = ConcernLevel.High,
                RecommendedAction = "Require human approval for all self-modifications"
            });

            // Ethics modifications are strictly prohibited
            if (request.Type == ModificationType.EthicsModification)
            {
                violations.Add(new EthicalViolation
                {
                    ViolatedPrinciple = EthicalPrinciple.SafeSelfImprovement,
                    Description = "Attempted modification of ethical constraints",
                    Severity = ViolationSeverity.Critical,
                    Evidence = request.Description,
                    AffectedParties = new[] { "System integrity", "All users" }
                });
            }

            // Check for harmful patterns in modification
            if (_reasoner.ContainsHarmfulPatterns(request.Description))
            {
                violations.Add(new EthicalViolation
                {
                    ViolatedPrinciple = EthicalPrinciple.DoNoHarm,
                    Description = "Self-modification may introduce harmful behavior",
                    Severity = ViolationSeverity.Critical,
                    Evidence = request.Description,
                    AffectedParties = new[] { "System", "Users" }
                });
            }

            // High-impact irreversible modifications are especially risky
            if (!request.IsReversible && request.ImpactLevel > 0.7)
            {
                concerns.Add(new EthicalConcern
                {
                    RelatedPrinciple = EthicalPrinciple.SafeSelfImprovement,
                    Description = "High-impact irreversible modification",
                    Level = ConcernLevel.High,
                    RecommendedAction = "Require extensive review and testing before approval"
                });
            }

            EthicalClearance clearance;

            if (violations.Count > 0)
            {
                clearance = EthicalClearance.Denied(
                    "Self-modification violates ethical principles",
                    violations,
                    _corePrinciples);
            }
            else
            {
                // ALL self-modifications require human approval
                clearance = EthicalClearance.RequiresApproval(
                    "Self-modification requires mandatory human approval",
                    concerns,
                    _corePrinciples);
            }

            await LogEvaluationAsync(
                "SelfModification",
                $"{request.Type}: {request.Description}",
                request.ActionContext,
                clearance,
                ct);

            return Result<EthicalClearance, string>.Success(clearance);
        }
        catch (Exception ex)
        {
            return Result<EthicalClearance, string>.Failure($"Self-modification evaluation failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task ReportEthicalConcernAsync(
        EthicalConcern concern,
        ActionContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(concern);
        ArgumentNullException.ThrowIfNull(context);

        var clearance = new EthicalClearance
        {
            IsPermitted = true,
            Level = EthicalClearanceLevel.PermittedWithConcerns,
            RelevantPrinciples = new[] { concern.RelatedPrinciple },
            Violations = Array.Empty<EthicalViolation>(),
            Concerns = new[] { concern },
            Reasoning = "Ethical concern reported"
        };

        await LogEvaluationAsync("ConcernReport", concern.Description, context, clearance, ct);
    }

    private async Task LogEvaluationAsync(
        string evaluationType,
        string description,
        ActionContext context,
        EthicalClearance clearance,
        CancellationToken ct)
    {
        var entry = new EthicsAuditEntry
        {
            Timestamp = DateTime.UtcNow,
            AgentId = context.AgentId,
            UserId = context.UserId,
            EvaluationType = evaluationType,
            Description = description,
            Clearance = clearance,
            Context = new Dictionary<string, object>
            {
                ["Environment"] = context.Environment,
                ["ClearanceLevel"] = clearance.Level.ToString()
            }
        };

        await _auditLog.LogEvaluationAsync(entry, ct);
    }
}
