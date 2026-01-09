// <copyright file="HyperonPlanner.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

#pragma warning disable SA1309 // Field names should not begin with underscore
#pragma warning disable SA1101 // Prefix local calls with this
#pragma warning disable IDE0007 // Use implicit type

namespace Ouroboros.Pipeline.Planning;

using Ouroboros.Core.Hyperon;
using Ouroboros.Tools.MeTTa;

/// <summary>
/// Hyperon-native planner that uses the full AtomSpace and Interpreter
/// for advanced symbolic reasoning and planning.
/// </summary>
/// <remarks>
/// This planner extends the capabilities of MeTTaPlanner by:
/// <list type="bullet">
/// <item>Using native AtomSpace for true unification.</item>
/// <item>Supporting grounded operations for runtime introspection.</item>
/// <item>Enabling meta-level reasoning about plans.</item>
/// <item>Providing neuro-symbolic fusion points for LLM integration.</item>
/// </list>
/// </remarks>
public sealed class HyperonPlanner : IAsyncDisposable
{
    private readonly HyperonMeTTaEngine _engine;
    private readonly HyperonFlowIntegration _flow;
    private readonly Dictionary<string, (MeTTaType Input, MeTTaType Output)> _toolSignatures = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="HyperonPlanner"/> class.
    /// </summary>
    public HyperonPlanner()
    {
        _engine = new HyperonMeTTaEngine();
        _flow = new HyperonFlowIntegration(_engine);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="HyperonPlanner"/> class with an existing engine.
    /// </summary>
    /// <param name="engine">The Hyperon engine to use.</param>
    public HyperonPlanner(HyperonMeTTaEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _flow = new HyperonFlowIntegration(_engine);
    }

    /// <summary>
    /// Gets the underlying Hyperon engine.
    /// </summary>
    public HyperonMeTTaEngine Engine => _engine;

    /// <summary>
    /// Gets the flow integration for reactive patterns.
    /// </summary>
    public HyperonFlowIntegration Flow => _flow;

    /// <summary>
    /// Initializes the planner with Hyperon-native planning rules.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        // Register planning infrastructure
        var planningKb = @"
; Planning type system
(: Type Atom)
(: Tool Atom)
(: Plan Atom)

; Type definitions
(: Text Type)
(: Summary Type)
(: Code Type)
(: TestResult Type)
(: Query Type)
(: Answer Type)
(: Context Type)
(: Embedding Type)
(: SearchResult Type)

; Planning operators
(: -> (-> Type Type Type))
(: compose (-> Plan Plan Plan))
(: sequence (-> Plan Plan Plan))
(: parallel (-> Plan Plan Plan))
(: conditional (-> Atom Plan Plan Plan))

; Backward chaining for planning
(= (solve $goal $goal) (identity))
(= (solve $start $goal)
   (match &self (: $tool (-> $start $intermediate))
     (compose (step $tool) (solve $intermediate $goal))))

; Tool chain composition
(= (compose (identity) $p) $p)
(= (compose $p (identity)) $p)
(= (compose (step $t1) (step $t2)) (chain $t1 $t2))
(= (compose (chain $ts...) (step $t)) (chain $ts... $t))

; Find all paths from type A to type B
(= (all-paths $start $end)
   (match &self (: $tool (-> $start $mid))
     (if (== $mid $end)
         (step $tool)
         (compose (step $tool) (all-paths $mid $end)))))

; Cost-aware planning
(: cost (-> Tool Number))
(= (plan-cost (identity)) 0)
(= (plan-cost (step $t)) (cost $t))
(= (plan-cost (compose $p1 $p2)) (+ (plan-cost $p1) (plan-cost $p2)))

; Optimal plan selection
(= (optimal-plan $start $end)
   (min-by plan-cost (all-paths $start $end)))

; Constraint-based planning
(: requires (-> Tool Type Bool))
(: provides (-> Tool Type Bool))
(= (can-execute $tool $context)
   (all (map (requires $tool) (has-type $context))))

; Meta-level planning (planning about planning)
(: PlanningGoal Atom)
(: PlanningConstraint Atom)
(= (meta-plan $goal $constraints)
   (filter (satisfies-all $constraints) (all-paths $goal)))
";

        await _engine.LoadMeTTaSourceAsync(planningKb, ct);

        // Create planning flow
        _flow.CreateFlow("planning", "Main planning reasoning flow")
            .LoadFacts(
                "(implies (goal $type) (needs-planning $type))",
                "(implies (needs-planning $type) (find-tools $type))")
            .ApplyRule("(implies (PlanningGoal $g) (meta-plan $g))");

        // Subscribe to planning events
        _flow.SubscribePattern(
            "new-goal",
            "(PlanningGoal $goal)",
            match =>
            {
                // Auto-trigger planning when new goals are added
                var goalOption = match.Bindings.Lookup("goal");
                if (goalOption.HasValue)
                {
                    // Planning triggered for goal: goalOption.Value
                }
            });
    }

    /// <summary>
    /// Plans a tool chain using Hyperon's native unification.
    /// </summary>
    /// <param name="startType">The input type.</param>
    /// <param name="endType">The desired output type.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the tool chain or an error.</returns>
    public async Task<Result<ToolChain, string>> PlanAsync(
        MeTTaType startType,
        MeTTaType endType,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(startType);
        ArgumentNullException.ThrowIfNull(endType);

        try
        {
            // Use native solve
            Result<string, string> result = await _engine.ExecuteQueryAsync(
                $"!(solve {startType.Name} {endType.Name})",
                ct);

            if (!result.IsSuccess)
                return Result<ToolChain, string>.Failure(result.Error);

            return ParseToolChain(result.Value);
        }
        catch (Exception ex)
        {
            return Result<ToolChain, string>.Failure($"Planning exception: {ex.Message}");
        }
    }

    /// <summary>
    /// Plans with constraints using meta-level reasoning.
    /// </summary>
    /// <param name="startType">The input type.</param>
    /// <param name="endType">The desired output type.</param>
    /// <param name="constraints">Planning constraints.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the optimal tool chain.</returns>
    public async Task<Result<ToolChain, string>> PlanWithConstraintsAsync(
        MeTTaType startType,
        MeTTaType endType,
        IEnumerable<string> constraints,
        CancellationToken ct = default)
    {
        // Add constraints to space
        foreach (var constraint in constraints)
        {
            _engine.AddAtom(Atom.Expr(
                Atom.Sym("PlanningConstraint"),
                Atom.Sym(constraint)));
        }

        // Use meta-planning
        Result<string, string> result = await _engine.ExecuteQueryAsync(
            $"!(meta-plan (solve {startType.Name} {endType.Name}) (PlanningConstraint $c))",
            ct);

        if (!result.IsSuccess)
            return Result<ToolChain, string>.Failure(result.Error);

        return ParseToolChain(result.Value);
    }

    /// <summary>
    /// Finds the optimal plan considering tool costs.
    /// </summary>
    /// <param name="startType">The input type.</param>
    /// <param name="endType">The desired output type.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the optimal tool chain.</returns>
    public async Task<Result<ToolChain, string>> FindOptimalPlanAsync(
        MeTTaType startType,
        MeTTaType endType,
        CancellationToken ct = default)
    {
        Result<string, string> result = await _engine.ExecuteQueryAsync(
            $"!(optimal-plan {startType.Name} {endType.Name})",
            ct);

        if (!result.IsSuccess)
            return Result<ToolChain, string>.Failure(result.Error);

        return ParseToolChain(result.Value);
    }

    /// <summary>
    /// Registers a tool with type signature and optional cost.
    /// </summary>
    /// <param name="toolName">The tool name.</param>
    /// <param name="inputType">The input type.</param>
    /// <param name="outputType">The output type.</param>
    /// <param name="cost">Optional execution cost.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<Result<Unit, string>> RegisterToolAsync(
        string toolName,
        MeTTaType inputType,
        MeTTaType outputType,
        double cost = 1.0,
        CancellationToken ct = default)
    {
        // Add type signature
        await _engine.AddFactAsync($"(: {toolName} (-> {inputType.Name} {outputType.Name}))", ct);

        // Add cost
        _engine.AddAtom(Atom.Expr(
            Atom.Sym("cost"),
            Atom.Sym(toolName),
            Atom.Sym(cost.ToString("F2"))));

        _toolSignatures[toolName] = (inputType, outputType);

        return Result<Unit, string>.Success(Unit.Value);
    }

    /// <summary>
    /// Registers a tool with requirements and provisions.
    /// </summary>
    /// <param name="toolName">The tool name.</param>
    /// <param name="inputType">The input type.</param>
    /// <param name="outputType">The output type.</param>
    /// <param name="requires">Types required in context.</param>
    /// <param name="provides">Types provided to context.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task RegisterToolWithContextAsync(
        string toolName,
        MeTTaType inputType,
        MeTTaType outputType,
        IEnumerable<MeTTaType> requires,
        IEnumerable<MeTTaType> provides,
        CancellationToken ct = default)
    {
        // Basic registration
        await RegisterToolAsync(toolName, inputType, outputType, ct: ct);

        // Add requirements
        foreach (var req in requires)
        {
            _engine.AddAtom(Atom.Expr(
                Atom.Sym("requires"),
                Atom.Sym(toolName),
                Atom.Sym(req.Name)));
        }

        // Add provisions
        foreach (var prov in provides)
        {
            _engine.AddAtom(Atom.Expr(
                Atom.Sym("provides"),
                Atom.Sym(toolName),
                Atom.Sym(prov.Name)));
        }
    }

    /// <summary>
    /// Creates a planning flow for dynamic tool composition.
    /// </summary>
    /// <param name="name">Flow name.</param>
    /// <param name="description">Flow description.</param>
    /// <returns>A chainable HyperonFlow.</returns>
    public HyperonFlow CreatePlanningFlow(string name, string description)
    {
        return _flow.CreateFlow(name, description);
    }

    /// <summary>
    /// Executes a plan and returns intermediate results.
    /// </summary>
    /// <param name="plan">The plan to execute.</param>
    /// <param name="input">The input value.</param>
    /// <param name="executeStep">Function to execute each step.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Execution trace with results.</returns>
    public async IAsyncEnumerable<PlanExecutionStep> ExecutePlanAsync<TInput, TOutput>(
        ToolChain plan,
        TInput input,
        Func<string, object, CancellationToken, Task<object>> executeStep,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        object current = input!;

        foreach (var tool in plan.Tools)
        {
            var stepResult = new PlanExecutionStep
            {
                ToolName = tool,
                Input = current,
                StartTime = DateTime.UtcNow
            };

            try
            {
                current = await executeStep(tool, current, ct);
                stepResult.Output = current;
                stepResult.Success = true;
            }
            catch (Exception ex)
            {
                stepResult.Error = ex.Message;
                stepResult.Success = false;
            }

            stepResult.EndTime = DateTime.UtcNow;

            // Record in AtomSpace
            _engine.AddAtom(Atom.Expr(
                Atom.Sym("ExecutedStep"),
                Atom.Sym(tool),
                Atom.Sym(stepResult.Success.ToString()),
                Atom.Sym(stepResult.Duration.TotalMilliseconds.ToString("F0"))));

            yield return stepResult;

            if (!stepResult.Success) yield break;
        }
    }

    /// <summary>
    /// Verifies a plan is valid before execution.
    /// </summary>
    /// <param name="plan">The plan to verify.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Verification result.</returns>
    public async Task<Result<bool, string>> VerifyPlanAsync(
        ToolChain plan,
        CancellationToken ct = default)
    {
        if (plan.IsEmpty)
            return Result<bool, string>.Success(true);

        // Build verification query
        var tools = string.Join(" ", plan.Tools.Select(t => $"(step {t})"));
        var query = $"!(verify-chain {tools})";

        Result<string, string> result = await _engine.ExecuteQueryAsync(query, ct);

        if (!result.IsSuccess)
            return Result<bool, string>.Failure($"Plan verification failed: {result.Error}");

        if (result.Value.Contains("valid") || result.Value.Contains("True"))
            return Result<bool, string>.Success(true);

        return Result<bool, string>.Failure($"Plan verification failed: {result.Value}");
    }

    /// <summary>
    /// Gets all registered tools and their signatures.
    /// </summary>
    /// <returns>Dictionary of tool names to signatures.</returns>
    public IReadOnlyDictionary<string, (MeTTaType Input, MeTTaType Output)> GetRegisteredTools()
        => _toolSignatures;

    /// <summary>
    /// Exports the planning knowledge base.
    /// </summary>
    /// <returns>MeTTa source representation.</returns>
    public string ExportPlanningKnowledge()
        => _engine.ExportToMeTTa();

    private static Result<ToolChain, string> ParseToolChain(string result)
    {
        if (string.IsNullOrWhiteSpace(result) ||
            result.Contains("Empty") ||
            result.Contains("[]") ||
            result.Contains("identity"))
        {
            return Result<ToolChain, string>.Success(ToolChain.Empty);
        }

        // Parse chain/step expressions
        var tools = new List<string>();
        var matches = System.Text.RegularExpressions.Regex.Matches(
            result, @"\(step\s+(\w+)\)|\(chain\s+([\w\s]+)\)");

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            if (match.Groups[1].Success)
            {
                tools.Add(match.Groups[1].Value);
            }
            else if (match.Groups[2].Success)
            {
                tools.AddRange(match.Groups[2].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
            }
        }

        // Fallback: try to find tool names directly
        if (tools.Count == 0)
        {
            var toolMatches = System.Text.RegularExpressions.Regex.Matches(result, @"\b(\w+Tool)\b");
            foreach (System.Text.RegularExpressions.Match tm in toolMatches)
            {
                tools.Add(tm.Groups[1].Value);
            }
        }

        return Result<ToolChain, string>.Success(new ToolChain(tools));
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        await _flow.DisposeAsync();
    }
}

/// <summary>
/// Represents a step in plan execution.
/// </summary>
public class PlanExecutionStep
{
    /// <summary>
    /// Gets or sets the tool name.
    /// </summary>
    public required string ToolName { get; set; }

    /// <summary>
    /// Gets or sets the input to the step.
    /// </summary>
    public object? Input { get; set; }

    /// <summary>
    /// Gets or sets the output from the step.
    /// </summary>
    public object? Output { get; set; }

    /// <summary>
    /// Gets or sets whether the step succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the error message if failed.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Gets or sets the start time.
    /// </summary>
    public DateTime StartTime { get; set; }

    /// <summary>
    /// Gets or sets the end time.
    /// </summary>
    public DateTime EndTime { get; set; }

    /// <summary>
    /// Gets the duration.
    /// </summary>
    public TimeSpan Duration => EndTime - StartTime;
}
