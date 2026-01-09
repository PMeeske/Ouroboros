// <copyright file="FormReasoningTools.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Text.Json;
using Ouroboros.Core.Hyperon;
using Ouroboros.Core.LawsOfForm;

namespace Ouroboros.Tools.MeTTa;

/// <summary>
/// Tools that expose Laws of Form reasoning to the agent system.
/// These tools enable distinction-gated inference and form-based logic.
/// </summary>
public static class FormReasoningTools
{
    /// <summary>
    /// Extends a ToolRegistry with Laws of Form reasoning tools.
    /// </summary>
    /// <param name="registry">The registry to extend.</param>
    /// <param name="bridge">The FormMeTTaBridge instance to use.</param>
    /// <returns>A new registry with LoF tools added.</returns>
    public static ToolRegistry WithFormReasoningTools(this ToolRegistry registry, FormMeTTaBridge bridge)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(bridge);

        return registry
            .WithTool(new DrawDistinctionTool(bridge))
            .WithTool(new CrossDistinctionTool(bridge))
            .WithTool(new EvaluateCertaintyTool(bridge))
            .WithTool(new DistinctionGatedInferenceTool(bridge))
            .WithTool(new FormPatternMatchTool(bridge))
            .WithTool(new CreateReEntryTool(bridge));
    }

    /// <summary>
    /// Creates a ToolRegistry with Laws of Form tools pre-registered.
    /// </summary>
    /// <param name="bridge">The FormMeTTaBridge instance to use.</param>
    /// <returns>A new ToolRegistry with LoF tools.</returns>
    public static ToolRegistry CreateWithFormReasoning(FormMeTTaBridge bridge)
    {
        return ToolRegistry.CreateDefault().WithFormReasoningTools(bridge);
    }
}

/// <summary>
/// Tool for drawing a distinction (creating a mark) in a context.
/// </summary>
public sealed class DrawDistinctionTool : ITool
{
    private readonly FormMeTTaBridge _bridge;

    /// <summary>
    /// Initializes a new instance of the <see cref="DrawDistinctionTool"/> class.
    /// </summary>
    /// <param name="bridge">The FormMeTTaBridge to use.</param>
    public DrawDistinctionTool(FormMeTTaBridge bridge)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
    }

    /// <inheritdoc/>
    public string Name => "lof_draw_distinction";

    /// <inheritdoc/>
    public string Description =>
        "Draws a distinction (creates a mark) in the Laws of Form calculus. " +
        "A mark indicates certain affirmation. Use to establish a definite boundary or assertion.";

    /// <inheritdoc/>
    public string JsonSchema => """
        {
            "type": "object",
            "properties": {
                "context": {
                    "type": "string",
                    "description": "The context name for this distinction"
                },
                "reason": {
                    "type": "string",
                    "description": "Optional reason for drawing this distinction"
                }
            },
            "required": ["context"]
        }
        """;

    /// <inheritdoc/>
    public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(input) ?? new();
            string context = args.TryGetValue("context", out var c) ? c.GetString() ?? "default" : "default";

            Form result = _bridge.DrawDistinction(context);
            string formState = result.IsMarked() ? "Mark (certain)" : result.IsVoid() ? "Void (negated)" : "Imaginary (uncertain)";

            return Task.FromResult(Result<string, string>.Success($"Distinction drawn in '{context}': {formState}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<string, string>.Failure($"Error: {ex.Message}"));
        }
    }
}

/// <summary>
/// Tool for crossing (negating) a distinction.
/// </summary>
public sealed class CrossDistinctionTool : ITool
{
    private readonly FormMeTTaBridge _bridge;

    /// <summary>
    /// Initializes a new instance of the <see cref="CrossDistinctionTool"/> class.
    /// </summary>
    /// <param name="bridge">The FormMeTTaBridge to use.</param>
    public CrossDistinctionTool(FormMeTTaBridge bridge)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
    }

    /// <inheritdoc/>
    public string Name => "lof_cross_distinction";

    /// <inheritdoc/>
    public string Description =>
        "Crosses (negates) a distinction in the Laws of Form calculus. " +
        "Crossing transforms: Mark->Void, Void->Mark, Imaginary->Imaginary. " +
        "Double crossing cancels (Law of Crossing).";

    /// <inheritdoc/>
    public string JsonSchema => """
        {
            "type": "object",
            "properties": {
                "context": {
                    "type": "string",
                    "description": "The context name of the distinction to cross"
                }
            },
            "required": ["context"]
        }
        """;

    /// <inheritdoc/>
    public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(input) ?? new();
            string context = args.TryGetValue("context", out var c) ? c.GetString() ?? "default" : "default";

            Form result = _bridge.CrossDistinction(context);
            string formState = result.IsMarked() ? "Mark (certain)" : result.IsVoid() ? "Void (negated)" : "Imaginary (uncertain)";

            return Task.FromResult(Result<string, string>.Success($"Distinction crossed in '{context}': {formState}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<string, string>.Failure($"Error: {ex.Message}"));
        }
    }
}

/// <summary>
/// Tool for evaluating the certainty of a form.
/// </summary>
public sealed class EvaluateCertaintyTool : ITool
{
    private readonly FormMeTTaBridge _bridge;

    /// <summary>
    /// Initializes a new instance of the <see cref="EvaluateCertaintyTool"/> class.
    /// </summary>
    /// <param name="bridge">The FormMeTTaBridge to use.</param>
    public EvaluateCertaintyTool(FormMeTTaBridge bridge)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
    }

    /// <inheritdoc/>
    public string Name => "lof_evaluate_certainty";

    /// <inheritdoc/>
    public string Description =>
        "Evaluates the certainty of an expression in the Laws of Form calculus. " +
        "Returns: Mark (certain affirmation), Void (certain negation), or Imaginary (uncertain/paradoxical).";

    /// <inheritdoc/>
    public string JsonSchema => """
        {
            "type": "object",
            "properties": {
                "expression": {
                    "type": "string",
                    "description": "The MeTTa expression to evaluate for certainty"
                }
            },
            "required": ["expression"]
        }
        """;

    /// <inheritdoc/>
    public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(input) ?? new();
            string expression = args.TryGetValue("expression", out var e) ? e.GetString() ?? "" : "";

            Form result = _bridge.EvaluateTruthValue(Atom.Sym(expression));
            string certainty = result.IsMarked() ? "Certain (affirmed)"
                : result.IsVoid() ? "Certain (negated)"
                : "Uncertain (imaginary/paradoxical)";

            return Task.FromResult(Result<string, string>.Success($"Certainty of '{expression}': {certainty}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<string, string>.Failure($"Error: {ex.Message}"));
        }
    }
}

/// <summary>
/// Tool for distinction-gated inference.
/// </summary>
public sealed class DistinctionGatedInferenceTool : ITool
{
    private readonly FormMeTTaBridge _bridge;

    /// <summary>
    /// Initializes a new instance of the <see cref="DistinctionGatedInferenceTool"/> class.
    /// </summary>
    /// <param name="bridge">The FormMeTTaBridge to use.</param>
    public DistinctionGatedInferenceTool(FormMeTTaBridge bridge)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
    }

    /// <inheritdoc/>
    public string Name => "lof_gated_inference";

    /// <inheritdoc/>
    public string Description =>
        "Performs distinction-gated inference: only derives conclusions if the distinction context is marked (certain). " +
        "Pattern is matched in MeTTa knowledge base, but results are only returned if the gate is certain.";

    /// <inheritdoc/>
    public string JsonSchema => """
        {
            "type": "object",
            "properties": {
                "context": {
                    "type": "string",
                    "description": "The distinction context that gates the inference"
                },
                "pattern": {
                    "type": "string",
                    "description": "The MeTTa pattern to match"
                }
            },
            "required": ["context", "pattern"]
        }
        """;

    /// <inheritdoc/>
    public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(input) ?? new();
            string context = args.TryGetValue("context", out var c) ? c.GetString() ?? "default" : "default";
            string pattern = args.TryGetValue("pattern", out var p) ? p.GetString() ?? "" : "";

            var results = _bridge.DistinctionGatedInference(context, Atom.Sym(pattern)).ToList();
            string output = results.Count > 0
                ? $"Gated inference succeeded: {string.Join(", ", results)}"
                : "Gated inference blocked: distinction not marked (uncertain/negated)";

            return Task.FromResult(Result<string, string>.Success(output));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<string, string>.Failure($"Error: {ex.Message}"));
        }
    }
}

/// <summary>
/// Tool for form-based pattern matching.
/// </summary>
public sealed class FormPatternMatchTool : ITool
{
    private readonly FormMeTTaBridge _bridge;

    /// <summary>
    /// Initializes a new instance of the <see cref="FormPatternMatchTool"/> class.
    /// </summary>
    /// <param name="bridge">The FormMeTTaBridge to use.</param>
    public FormPatternMatchTool(FormMeTTaBridge bridge)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
    }

    /// <inheritdoc/>
    public string Name => "lof_pattern_match";

    /// <inheritdoc/>
    public string Description =>
        "Performs Laws of Form-gated pattern matching. Only returns matches where the form " +
        "evaluates to Mark (certain). Uncertain matches are filtered out.";

    /// <inheritdoc/>
    public string JsonSchema => """
        {
            "type": "object",
            "properties": {
                "pattern": {
                    "type": "string",
                    "description": "The MeTTa pattern to match"
                },
                "template": {
                    "type": "string",
                    "description": "The template to apply to matches"
                }
            },
            "required": ["pattern", "template"]
        }
        """;

    /// <inheritdoc/>
    public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(input) ?? new();
            string pattern = args.TryGetValue("pattern", out var p) ? p.GetString() ?? "" : "";

            // FormGatedMatch returns tuples of (Result, State, Bindings) - filter to certain (marked) results
            var results = _bridge.FormGatedMatch(Atom.Sym(pattern))
                .Where(r => r.State.IsMarked())
                .Select(r => r.Result.ToString())
                .ToList();

            string output = results.Count > 0
                ? $"Form-gated matches: {string.Join(", ", results)}"
                : "No certain matches found";

            return Task.FromResult(Result<string, string>.Success(output));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<string, string>.Failure($"Error: {ex.Message}"));
        }
    }
}

/// <summary>
/// Tool for creating re-entry (self-referential) forms.
/// </summary>
public sealed class CreateReEntryTool : ITool
{
    private readonly FormMeTTaBridge _bridge;

    /// <summary>
    /// Initializes a new instance of the <see cref="CreateReEntryTool"/> class.
    /// </summary>
    /// <param name="bridge">The FormMeTTaBridge to use.</param>
    public CreateReEntryTool(FormMeTTaBridge bridge)
    {
        _bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
    }

    /// <inheritdoc/>
    public string Name => "lof_create_reentry";

    /// <inheritdoc/>
    public string Description =>
        "Creates a re-entry (self-referential) form in Laws of Form. " +
        "Re-entry produces the Imaginary state - representing oscillation, paradox, or self-reference. " +
        "Use for modeling recursive or self-modifying concepts.";

    /// <inheritdoc/>
    public string JsonSchema => """
        {
            "type": "object",
            "properties": {
                "context": {
                    "type": "string",
                    "description": "The context name for the re-entry"
                },
                "depth": {
                    "type": "integer",
                    "description": "The depth of self-reference (default: 1)"
                }
            },
            "required": ["context"]
        }
        """;

    /// <inheritdoc/>
    public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            var args = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(input) ?? new();
            string context = args.TryGetValue("context", out var c) ? c.GetString() ?? "default" : "default";

            Form result = _bridge.CreateReEntry(context);
            string formState = result.IsImaginary() ? "Imaginary (self-referential)"
                : result.IsMarked() ? "Mark (collapsed to certain)"
                : "Void (collapsed to negated)";

            return Task.FromResult(Result<string, string>.Success($"Re-entry created in '{context}': {formState}"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(Result<string, string>.Failure($"Error: {ex.Message}"));
        }
    }
}
