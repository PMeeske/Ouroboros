// <copyright file="SafeToolExecutor.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.LawsOfForm;

using Ouroboros.Core.Monads;

/// <summary>
/// Interface for tool lookup operations.
/// Abstracts tool registry to avoid circular dependencies.
/// </summary>
public interface IToolLookup
{
    /// <summary>
    /// Gets a tool by name.
    /// </summary>
    /// <param name="toolName">The name of the tool.</param>
    /// <returns>An Option containing the tool if found.</returns>
    Option<IToolExecutor> GetTool(string toolName);
}

/// <summary>
/// Interface for tool execution.
/// Abstracts the tool interface to avoid circular dependencies.
/// </summary>
public interface IToolExecutor
{
    /// <summary>
    /// Executes the tool with the given input.
    /// </summary>
    /// <param name="input">The input for the tool.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the tool output or error.</returns>
    Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default);
}

/// <summary>
/// Executes tools with safety gates based on multiple criteria.
/// Uses AuditableDecision for full audit trail and three-valued certainty logic.
/// </summary>
public sealed class SafeToolExecutor
{
    private readonly IToolLookup toolLookup;
    private readonly List<SafetyCriterion> criteria;
    private Func<ToolCall, ExecutionContext, Task<bool>>? uncertaintyHandler;

    /// <summary>
    /// Initializes a new instance of the <see cref="SafeToolExecutor"/> class.
    /// </summary>
    /// <param name="toolLookup">The tool lookup service for resolving tools.</param>
    public SafeToolExecutor(IToolLookup toolLookup)
    {
        this.toolLookup = toolLookup ?? throw new ArgumentNullException(nameof(toolLookup));
        this.criteria = new List<SafetyCriterion>();
    }

    /// <summary>
    /// Adds a safety criterion that must be satisfied for tool execution.
    /// </summary>
    /// <param name="name">The name of the criterion.</param>
    /// <param name="criterion">Function that evaluates the criterion returning a Form.</param>
    /// <returns>This executor for fluent chaining.</returns>
    public SafeToolExecutor AddCriterion(
        string name,
        Func<ToolCall, ExecutionContext, Form> criterion)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(criterion);

        this.criteria.Add(new SafetyCriterion(name, criterion));
        return this;
    }

    /// <summary>
    /// Configures the handler for uncertain decisions (Imaginary state).
    /// </summary>
    /// <param name="handler">Async function that returns true to approve, false to reject.</param>
    /// <returns>This executor for fluent chaining.</returns>
    public SafeToolExecutor OnUncertain(Func<ToolCall, Task<bool>> handler)
    {
        this.uncertaintyHandler = (call, _) => handler(call);
        return this;
    }

    /// <summary>
    /// Configures the handler for uncertain decisions with context.
    /// </summary>
    /// <param name="handler">Async function that returns true to approve, false to reject.</param>
    /// <returns>This executor for fluent chaining.</returns>
    public SafeToolExecutor OnUncertain(Func<ToolCall, ExecutionContext, Task<bool>> handler)
    {
        this.uncertaintyHandler = handler;
        return this;
    }

    /// <summary>
    /// Executes a tool call with full safety auditing.
    /// Returns an AuditableDecision with complete evidence trail.
    /// </summary>
    /// <param name="toolCall">The tool call to execute.</param>
    /// <param name="context">The execution context.</param>
    /// <returns>An auditable decision containing the tool result or rejection/uncertainty.</returns>
    public async Task<AuditableDecision<ToolResult>> ExecuteWithAudit(
        ToolCall toolCall,
        ExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(toolCall);
        ArgumentNullException.ThrowIfNull(context);

        // Evaluate all criteria
        var evidence = new List<Evidence>();
        var criteriaForms = new List<Form>();

        foreach (var criterion in this.criteria)
        {
            try
            {
                var form = criterion.Evaluate(toolCall, context);
                criteriaForms.Add(form);

                var description = form.Match(
                    onMark: () => $"{criterion.Name} passed",
                    onVoid: () => $"{criterion.Name} failed",
                    onImaginary: () => $"{criterion.Name} uncertain");

                evidence.Add(new Evidence(criterion.Name, form, description));
            }
            catch (Exception ex)
            {
                // Treat exceptions as uncertain
                criteriaForms.Add(Form.Imaginary);
                evidence.Add(new Evidence(
                    criterion.Name,
                    Form.Imaginary,
                    $"{criterion.Name} threw exception: {ex.Message}"));
            }
        }

        // Combine all criteria using AND logic (all must pass)
        var overallCertainty = FormExtensions.All(criteriaForms.ToArray());

        // Handle each certainty state
        return await overallCertainty.Match(
            onMark: async () => await this.ExecuteTool(toolCall, context, evidence),
            onVoid: () => Task.FromResult(this.RejectExecution(toolCall, evidence)),
            onImaginary: async () => await this.HandleUncertain(toolCall, context, evidence));
    }

    private async Task<AuditableDecision<ToolResult>> ExecuteTool(
        ToolCall toolCall,
        ExecutionContext context,
        List<Evidence> evidence)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // Look up the tool
            var toolOption = this.toolLookup.GetTool(toolCall.ToolName);

            if (!toolOption.HasValue)
            {
                return AuditableDecision<ToolResult>.Reject(
                    $"Tool '{toolCall.ToolName}' not found",
                    "Tool lookup failed",
                    evidence.ToArray());
            }

            // Execute the tool
            var tool = toolOption.Value!;
            var result = await tool.InvokeAsync(toolCall.Arguments);

            var duration = DateTime.UtcNow - startTime;

            // Convert Result to ToolResult
            var toolResult = result.Match(
                onSuccess: output => ToolResult.Success(output, toolCall, duration),
                onFailure: error => ToolResult.Failure(error, toolCall, duration));

            // Record execution for rate limiting
            context.RateLimiter.Record(toolCall);

            return AuditableDecision<ToolResult>.Approve(
                toolResult,
                "All safety criteria passed, tool executed successfully",
                evidence.ToArray());
        }
        catch (Exception ex)
        {
            var duration = DateTime.UtcNow - startTime;
            var toolResult = ToolResult.Failure(ex.Message, toolCall, duration);

            return AuditableDecision<ToolResult>.Reject(
                $"Tool execution failed: {ex.Message}",
                "Exception during tool execution",
                evidence.ToArray());
        }
    }

    private AuditableDecision<ToolResult> RejectExecution(
        ToolCall toolCall,
        List<Evidence> evidence)
    {
        var failedCriteria = evidence
            .Where(e => e.Evaluation.IsVoid())
            .Select(e => e.CriterionName)
            .ToList();

        var reasoning = $"Safety criteria failed: {string.Join(", ", failedCriteria)}";

        return AuditableDecision<ToolResult>.Reject(
            "Tool execution blocked by safety criteria",
            reasoning,
            evidence.ToArray());
    }

    private async Task<AuditableDecision<ToolResult>> HandleUncertain(
        ToolCall toolCall,
        ExecutionContext context,
        List<Evidence> evidence)
    {
        var uncertainCriteria = evidence
            .Where(e => e.Evaluation.IsImaginary())
            .Select(e => e.CriterionName)
            .ToList();

        var reasoning = $"Uncertain criteria: {string.Join(", ", uncertainCriteria)}";

        if (this.uncertaintyHandler == null)
        {
            // No handler configured, escalate as uncertain
            return AuditableDecision<ToolResult>.Uncertain(
                "Tool execution requires human approval (no handler configured)",
                reasoning,
                evidence.ToArray());
        }

        try
        {
            // Invoke the uncertainty handler
            var approved = await this.uncertaintyHandler(toolCall, context);

            if (approved)
            {
                // Add approval evidence
                evidence.Add(new Evidence(
                    "human_approval",
                    Form.Mark,
                    "Human reviewer approved execution"));

                // Execute the tool
                return await this.ExecuteTool(toolCall, context, evidence);
            }
            else
            {
                // Add rejection evidence
                evidence.Add(new Evidence(
                    "human_approval",
                    Form.Void,
                    "Human reviewer rejected execution"));

                return AuditableDecision<ToolResult>.Reject(
                    "Tool execution rejected by human reviewer",
                    reasoning + " - Human review declined",
                    evidence.ToArray());
            }
        }
        catch (Exception ex)
        {
            return AuditableDecision<ToolResult>.Uncertain(
                $"Uncertainty handler failed: {ex.Message}",
                reasoning,
                evidence.ToArray());
        }
    }

    /// <summary>
    /// Internal record for storing safety criteria.
    /// </summary>
    private sealed record SafetyCriterion
    {
        public string Name { get; init; }
        public Func<ToolCall, ExecutionContext, Form> Evaluator { get; init; }

        public SafetyCriterion(string name, Func<ToolCall, ExecutionContext, Form> evaluator)
        {
            this.Name = name;
            this.Evaluator = evaluator;
        }

        public Form Evaluate(ToolCall toolCall, ExecutionContext context)
        {
            return this.Evaluator(toolCall, context);
        }
    }
}
