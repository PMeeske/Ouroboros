// <copyright file="DagMeTTaExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Pipeline.Branches;

using System.Text;
using Ouroboros.Tools.MeTTa;

/// <summary>
/// Extension methods for encoding DAG structures as MeTTa symbolic facts.
/// Enables neuro-symbolic integration by representing pipeline branches and events
/// in a format suitable for symbolic reasoning and constraint checking.
/// </summary>
public static class DagMeTTaExtensions
{
    /// <summary>
    /// Encodes a PipelineBranch as a collection of MeTTa facts.
    /// </summary>
    /// <param name="branch">The pipeline branch to encode.</param>
    /// <returns>A list of MeTTa fact strings representing the branch structure.</returns>
    public static IReadOnlyList<string> ToMeTTaFacts(this PipelineBranch branch)
    {
        ArgumentNullException.ThrowIfNull(branch);

        List<string> facts = new();

        // Encode branch metadata
        facts.Add($"(: (Branch \"{branch.Name}\") BranchType)");
        facts.Add($"(HasEventCount (Branch \"{branch.Name}\") {branch.Events.Count})");
        facts.Add($"(HasSource (Branch \"{branch.Name}\") \"{branch.Source.ToString()}\")");

        // Encode each event
        for (int i = 0; i < branch.Events.Count; i++)
        {
            PipelineEvent evt = branch.Events[i];
            string eventFacts = EncodeEvent(branch.Name, evt, i);
            if (!string.IsNullOrEmpty(eventFacts))
            {
                facts.Add(eventFacts);
            }

            // Encode ordering constraint
            if (i > 0)
            {
                PipelineEvent prevEvt = branch.Events[i - 1];
                facts.Add($"(Before (Event \"{prevEvt.Id}\") (Event \"{evt.Id}\"))");
            }
        }

        return facts;
    }

    /// <summary>
    /// Encodes a single PipelineEvent as a MeTTa fact.
    /// </summary>
    /// <param name="branchName">The name of the branch containing this event.</param>
    /// <param name="evt">The event to encode.</param>
    /// <param name="index">The position of this event in the branch.</param>
    /// <returns>A MeTTa fact string representing the event.</returns>
    private static string EncodeEvent(string branchName, PipelineEvent evt, int index)
    {
        StringBuilder fact = new();

        fact.Append($"(BelongsToBranch (Event \"{evt.Id}\") (Branch \"{branchName}\"))");

        // Add event-specific encoding based on type
        if (evt is ReasoningStep reasoning)
        {
            fact.Append($"\n(: (Event \"{evt.Id}\") ReasoningEvent)");
            fact.Append($"\n(HasReasoningKind (Event \"{evt.Id}\") {reasoning.StepKind})");
            fact.Append($"\n(EventAtIndex (Event \"{evt.Id}\") {index})");
            
            // Encode tool usage if present
            if (reasoning.ToolCalls != null && reasoning.ToolCalls.Count > 0)
            {
                foreach (var tool in reasoning.ToolCalls)
                {
                    fact.Append($"\n(UsesTool (Event \"{evt.Id}\") \"{tool.ToolName}\")");
                }
            }
        }
        else if (evt is IngestBatch ingest)
        {
            fact.Append($"\n(: (Event \"{evt.Id}\") IngestEvent)");
            fact.Append($"\n(IngestedCount (Event \"{evt.Id}\") {ingest.Ids.Count})");
            fact.Append($"\n(EventAtIndex (Event \"{evt.Id}\") {index})");
        }

        return fact.ToString();
    }

    /// <summary>
    /// Encodes DAG constraints as MeTTa rules.
    /// These constraints ensure valid DAG operations like preventing cycles,
    /// enforcing dependencies, and validating branch operations.
    /// </summary>
    /// <returns>A list of MeTTa rule strings for DAG constraints.</returns>
    public static IReadOnlyList<string> GetDagConstraintRules()
    {
        return new[]
        {
            "; DAG Constraint Rules",
            "(: BranchType Type)",
            "(: EventType Type)",
            "(: ReasoningEvent EventType)",
            "(: IngestEvent EventType)",
            "",
            "; Temporal Ordering Constraints",
            "(: Before (-> EventType EventType Bool))",
            "(: EventAtIndex (-> EventType Number Bool))",
            "",
            "; Branch Membership",
            "(: BelongsToBranch (-> EventType BranchType Bool))",
            "(: HasEventCount (-> BranchType Number Bool))",
            "",
            "; Tool Usage Constraints",
            "(: UsesTool (-> EventType String Bool))",
            "(: HasReasoningKind (-> EventType Symbol Bool))",
            "",
            "; Acyclicity Constraint: No event can be before itself (transitively)",
            "(= (Acyclic $e1 $e2) (and (Before $e1 $e2) (not (Before $e2 $e1))))",
            "",
            "; Valid Fork: A branch fork must preserve event ordering",
            "(= (ValidFork $branch1 $branch2) ",
            "    (match &self (BelongsToBranch $evt $branch1) ",
            "        (BelongsToBranch $evt $branch2)))",
            "",
            "; Event Dependency: An event can only depend on events that occurred before it",
            "(= (ValidDependency $e1 $e2) (Before $e2 $e1))",
        };
    }

    /// <summary>
    /// Encodes a plan constraint that can be verified using MeTTa.
    /// </summary>
    /// <param name="constraint">The constraint description.</param>
    /// <param name="branchName">The branch this constraint applies to.</param>
    /// <returns>A MeTTa query string for checking the constraint.</returns>
    public static string EncodeConstraintQuery(string constraint, string branchName)
    {
        ArgumentException.ThrowIfNullOrEmpty(constraint);
        ArgumentException.ThrowIfNullOrEmpty(branchName);

        // Map common constraint types to MeTTa queries
        return constraint.ToLowerInvariant() switch
        {
            "acyclic" => $"!(and (BelongsToBranch $e1 (Branch \"{branchName}\")) (Acyclic $e1 $e1))",
            "valid-ordering" => $"!(and (Before $e1 $e2) (EventAtIndex $e1 $i1) (EventAtIndex $e2 $i2) (< $i1 $i2))",
            "no-tool-conflicts" => $"!(and (UsesTool $e1 $tool) (UsesTool $e2 $tool) (not (= $e1 $e2)))",
            _ => $"!(CheckConstraint \"{constraint}\" (Branch \"{branchName}\"))"
        };
    }

    /// <summary>
    /// Adds DAG facts to a MeTTa engine from a pipeline branch.
    /// </summary>
    /// <param name="engine">The MeTTa engine to add facts to.</param>
    /// <param name="branch">The branch to encode.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result indicating success or failure.</returns>
    public static async Task<Result<Unit, string>> AddBranchFactsAsync(
        this IMeTTaEngine engine,
        PipelineBranch branch,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentNullException.ThrowIfNull(branch);

        // First, add DAG constraint rules
        var rules = GetDagConstraintRules();
        foreach (string rule in rules)
        {
            if (string.IsNullOrWhiteSpace(rule) || rule.TrimStart().StartsWith(';'))
            {
                continue;
            }

            Result<Unit, string> result = await engine.AddFactAsync(rule, ct);
            if (result.IsFailure)
            {
                return Result<Unit, string>.Failure($"Failed to add DAG rule: {result.Error}");
            }
        }

        // Then add branch-specific facts
        var facts = branch.ToMeTTaFacts();
        foreach (string fact in facts)
        {
            if (string.IsNullOrWhiteSpace(fact))
            {
                continue;
            }

            Result<Unit, string> result = await engine.AddFactAsync(fact, ct);
            if (result.IsFailure)
            {
                return Result<Unit, string>.Failure($"Failed to add branch fact: {result.Error}");
            }
        }

        return Result<Unit, string>.Success(Unit.Value);
    }

    /// <summary>
    /// Verifies that a branch satisfies DAG constraints.
    /// </summary>
    /// <param name="engine">The MeTTa engine configured with DAG rules.</param>
    /// <param name="branchName">The branch to verify.</param>
    /// <param name="constraint">The constraint to check.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing true if valid, false if invalid, or an error.</returns>
    public static async Task<Result<bool, string>> VerifyDagConstraintAsync(
        this IMeTTaEngine engine,
        string branchName,
        string constraint,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(engine);
        ArgumentException.ThrowIfNullOrEmpty(branchName);
        ArgumentException.ThrowIfNullOrEmpty(constraint);

        string query = EncodeConstraintQuery(constraint, branchName);
        Result<string, string> result = await engine.ExecuteQueryAsync(query, ct);

        return result.Match(
            success =>
            {
                // Parse the result - empty or "[]" means constraint is satisfied
                bool isValid = string.IsNullOrWhiteSpace(success) ||
                              success.Trim() == "[]" ||
                              success.Trim() == "()" ||
                              success.Contains("True", StringComparison.OrdinalIgnoreCase);
                return Result<bool, string>.Success(isValid);
            },
            error => Result<bool, string>.Failure(error));
    }
}
