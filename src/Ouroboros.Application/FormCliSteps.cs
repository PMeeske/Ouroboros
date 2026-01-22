// <copyright file="FormCliSteps.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using Ouroboros.Core.SpencerBrown;

namespace Ouroboros.Application;

/// <summary>
/// CLI pipeline steps exposing Spencer-Brown's Laws of Form operations.
/// These tokens make formal distinction calculus available in the DSL.
/// </summary>
/// <remarks>
/// <para>Laws of Form provides a foundational algebra for:</para>
/// <list type="bullet">
///   <item><description>Distinction and boundary operations</description></item>
///   <item><description>Self-referential structures</description></item>
///   <item><description>Logical and computational duality</description></item>
/// </list>
/// </remarks>
public static class FormCliSteps
{
    /// <summary>
    /// Mark (⊢) - Creates a distinction, entering the marked state.
    /// The fundamental operation of indication.
    /// </summary>
    /// <remarks>
    /// Usage: Mark "value" | ...
    /// This wraps the pipeline output in a Form, making it a marked value.
    /// </remarks>
    [PipelineToken("Mark", "Indicate", "Distinguish", "⊢")]
    public static Step<CliPipelineState, CliPipelineState> MarkToken(string? args) =>
        async state =>
        {
            // If we have explicit args, mark them; otherwise mark the current output
            string valueToMark = string.IsNullOrWhiteSpace(args) ? state.Output : args.Trim();
            var form = Form<string>.Mark(valueToMark);

            state.Output = form.ToString();
            state.Branch = state.Branch.WithIngestEvent("form:mark", new[] { valueToMark });

            if (state.Trace)
            {
                Console.WriteLine($"[form] Mark: {valueToMark} → {form}");
            }

            return state;
        };

    /// <summary>
    /// Cross (⊢→) - Crosses the boundary of the current form.
    /// Entering a marked space or exiting to unmarked.
    /// </summary>
    /// <remarks>
    /// Usage: ... | Cross | ...
    /// Increases the distinction depth of the current form.
    /// </remarks>
    [PipelineToken("Cross", "Boundary", "Enter", "⊢→")]
    public static Step<CliPipelineState, CliPipelineState> CrossToken(string? args) =>
        async state =>
        {
            // Parse current state as a form and cross it
            var form = ParseFormFromOutput(state.Output);
            var crossed = form.Cross();

            state.Output = crossed.ToString();
            state.Branch = state.Branch.WithIngestEvent("form:cross", new[] { form.ToString(), crossed.ToString() });

            if (state.Trace)
            {
                Console.WriteLine($"[form] Cross: {form} → {crossed}");
            }

            return state;
        };

    /// <summary>
    /// Call - Law of Calling (⊢⊢ = ⊢): Condenses nested marks.
    /// Idempotence: marking what is already marked changes nothing.
    /// </summary>
    /// <remarks>
    /// Usage: ... | Call | ...
    /// Reduces any marked form to depth 1.
    /// </remarks>
    [PipelineToken("Call", "Condense", "Calling", "⊢⊢=⊢")]
    public static Step<CliPipelineState, CliPipelineState> CallToken(string? args) =>
        async state =>
        {
            var form = ParseFormFromOutput(state.Output);
            var condensed = form.Call();

            state.Output = condensed.ToString();
            state.Branch = state.Branch.WithIngestEvent("form:call", new[] { form.ToString(), condensed.ToString() });

            if (state.Trace)
            {
                Console.WriteLine($"[form] Law of Calling: {form} → {condensed}");
            }

            return state;
        };

    /// <summary>
    /// Recross - Law of Crossing (⊢⊢ = ∅): Double crossing returns to void.
    /// Cancellation: distinction of distinction yields the unmarked state.
    /// </summary>
    /// <remarks>
    /// Usage: ... | Recross | ...
    /// Reduces depth by 2, potentially returning to void.
    /// </remarks>
    [PipelineToken("Recross", "Cancel", "Crossing", "⊢⊢=∅")]
    public static Step<CliPipelineState, CliPipelineState> RecrossToken(string? args) =>
        async state =>
        {
            var form = ParseFormFromOutput(state.Output);
            var cancelled = form.Recross();

            state.Output = cancelled.ToString();
            state.Branch = state.Branch.WithIngestEvent("form:recross", new[] { form.ToString(), cancelled.ToString() });

            if (state.Trace)
            {
                Console.WriteLine($"[form] Law of Crossing: {form} → {cancelled}");
            }

            return state;
        };

    /// <summary>
    /// Void (∅) - Creates the unmarked/void state.
    /// The undifferentiated potential before distinction.
    /// </summary>
    /// <remarks>
    /// Usage: Void | ...
    /// Resets the pipeline to the void state.
    /// </remarks>
    [PipelineToken("Void", "Unmarked", "Empty", "∅")]
    public static Step<CliPipelineState, CliPipelineState> VoidToken(string? args) =>
        async state =>
        {
            var form = Form<string>.Void();

            state.Output = form.ToString();
            state.Branch = state.Branch.WithIngestEvent("form:void", Array.Empty<string>());

            if (state.Trace)
            {
                Console.WriteLine($"[form] Void: → {form}");
            }

            return state;
        };

    /// <summary>
    /// CrossProduct (×) - Combines two values into a product form.
    /// The categorical product in the category of forms.
    /// </summary>
    /// <remarks>
    /// Usage: CrossProduct "value1,value2" | ...
    /// Creates a marked product if both inputs are marked.
    /// </remarks>
    [PipelineToken("CrossProduct", "Product", "×", "Pair")]
    public static Step<CliPipelineState, CliPipelineState> CrossProductToken(string? args) =>
        async state =>
        {
            // Parse args as "value1,value2" or use output
            string[] parts;
            if (!string.IsNullOrWhiteSpace(args) && args.Contains(','))
            {
                parts = args.Split(',', 2, StringSplitOptions.TrimEntries);
            }
            else
            {
                parts = new[] { state.Output, args ?? string.Empty };
            }

            var form1 = Form<string>.Mark(parts[0]);
            var form2 = parts.Length > 1 ? Form<string>.Mark(parts[1]) : Form<string>.Void();

            var product = LawsOfForm.Product(form1, form2);
            state.Output = product.Match(
                pair => $"({pair.Item1}, {pair.Item2})",
                () => "∅");

            state.Branch = state.Branch.WithIngestEvent("form:crossproduct", parts);

            if (state.Trace)
            {
                Console.WriteLine($"[form] CrossProduct: {form1} × {form2} → {state.Output}");
            }

            return state;
        };

    /// <summary>
    /// ReEntry (⟲) - Creates a self-referential oscillating form.
    /// Models the imaginary value from self-distinction.
    /// </summary>
    /// <remarks>
    /// Usage: ReEntry [iterations] | ...
    /// Applies re-entry iteration to create temporal oscillation.
    /// </remarks>
    [PipelineToken("ReEntry", "Oscillate", "SelfRef", "⟲")]
    public static Step<CliPipelineState, CliPipelineState> ReEntryToken(string? args) =>
        async state =>
        {
            int iterations = 2; // Default: one complete oscillation
            if (int.TryParse(args?.Trim(), out int parsed))
            {
                iterations = Math.Max(1, Math.Min(parsed, 100));
            }

            var form = ParseFormFromOutput(state.Output);

            // Apply oscillating crosses
            var result = form;
            var history = new List<string> { result.ToString() };

            for (int i = 0; i < iterations; i++)
            {
                result = result.Cross();
                history.Add(result.ToString());
            }

            state.Output = result.ToString();
            state.Branch = state.Branch.WithIngestEvent("form:reentry", history.ToArray());

            if (state.Trace)
            {
                Console.WriteLine($"[form] ReEntry ({iterations}): {string.Join(" → ", history)}");
            }

            return state;
        };

    /// <summary>
    /// FormMatch - Pattern match on form state (marked/void).
    /// </summary>
    /// <remarks>
    /// Usage: FormMatch "markedResult,voidResult" | ...
    /// Returns appropriate result based on form state.
    /// </remarks>
    [PipelineToken("FormMatch", "MatchForm", "Cata")]
    public static Step<CliPipelineState, CliPipelineState> FormMatchToken(string? args) =>
        async state =>
        {
            var form = ParseFormFromOutput(state.Output);

            string[] cases = (args ?? ",").Split(',', 2, StringSplitOptions.TrimEntries);
            string whenMarked = cases.Length > 0 ? cases[0] : "marked";
            string whenVoid = cases.Length > 1 ? cases[1] : "void";

            string result = form.Match(
                _ => whenMarked,
                () => whenVoid);

            state.Output = result;
            state.Branch = state.Branch.WithIngestEvent("form:match", new[] { form.ToString(), result });

            if (state.Trace)
            {
                Console.WriteLine($"[form] Match: {form} → {result}");
            }

            return state;
        };

    /// <summary>
    /// Parses a form from the output string representation.
    /// </summary>
    private static Form<string> ParseFormFromOutput(string output)
    {
        if (string.IsNullOrWhiteSpace(output) || output == "∅")
        {
            return Form<string>.Void();
        }

        // Count leading marks (⊢)
        int depth = 0;
        int i = 0;
        while (i < output.Length && output[i] == '⊢')
        {
            depth++;
            i++;
        }

        // Extract value from [value] if present
        string value = output;
        if (output.Contains('[') && output.Contains(']'))
        {
            int start = output.IndexOf('[') + 1;
            int end = output.LastIndexOf(']');
            if (start < end)
            {
                value = output[start..end];
            }
        }
        else if (depth > 0)
        {
            value = output[depth..];
        }

        if (depth == 0 && !output.StartsWith("⊢"))
        {
            // Unmarked value - mark it
            return Form<string>.Mark(value);
        }

        // Reconstruct with proper depth
        var form = Form<string>.Mark(value);
        for (int d = 1; d < depth; d++)
        {
            form = form.Cross();
        }

        return form;
    }
}
