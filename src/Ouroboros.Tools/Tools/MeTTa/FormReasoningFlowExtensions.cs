// <copyright file="FormReasoningFlowExtensions.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Tools.MeTTa;

using Ouroboros.Core.Hyperon;
using Ouroboros.Core.LawsOfForm;
using Ouroboros.Core.Monads;

/// <summary>
/// Event arguments for distinction-based reasoning events.
/// </summary>
public sealed class FormReasoningEventArgs : EventArgs
{
    /// <summary>
    /// Gets the reasoning operation type.
    /// </summary>
    public required string Operation { get; init; }

    /// <summary>
    /// Gets the form state involved.
    /// </summary>
    public Form FormState { get; init; }

    /// <summary>
    /// Gets the context identifier.
    /// </summary>
    public string? Context { get; init; }

    /// <summary>
    /// Gets any associated atoms.
    /// </summary>
    public IReadOnlyList<Atom> RelatedAtoms { get; init; } = Array.Empty<Atom>();

    /// <summary>
    /// Gets the reasoning trace.
    /// </summary>
    public IReadOnlyList<string> Trace { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Gets the timestamp.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Extension methods for integrating Laws of Form reasoning into Hyperon flows.
/// </summary>
public static class FormReasoningFlowExtensions
{
    /// <summary>
    /// Draws a distinction and adds it to the flow.
    /// </summary>
    /// <param name="flow">The flow to extend.</param>
    /// <param name="context">The distinction context.</param>
    /// <param name="onDistinction">Optional handler when distinction is drawn.</param>
    /// <returns>The flow for chaining.</returns>
    public static HyperonFlow DrawDistinction(
        this HyperonFlow flow,
        string context,
        Action<Form>? onDistinction = null)
    {
        return flow.Transform(async (engine, ct) =>
        {
            // Add distinction fact
            await engine.AddFactAsync($"(Distinction {context} Mark)", ct);
            await engine.AddFactAsync($"(DistinctionDrawn {context} {DateTime.UtcNow.Ticks})", ct);

            onDistinction?.Invoke(Form.Mark);
        });
    }

    /// <summary>
    /// Crosses (negates) a distinction in the flow.
    /// </summary>
    /// <param name="flow">The flow to extend.</param>
    /// <param name="context">The distinction context.</param>
    /// <param name="onCrossed">Optional handler after crossing.</param>
    /// <returns>The flow for chaining.</returns>
    public static HyperonFlow CrossDistinction(
        this HyperonFlow flow,
        string context,
        Action<Form>? onCrossed = null)
    {
        return flow.Transform(async (engine, ct) =>
        {
            // Query current state
            Result<string, string> currentResult = await engine.ExecuteQueryAsync(
                $"(match &self (Distinction {context} $state) $state)", ct);

            Form newState = Form.Void;

            if (currentResult.IsSuccess)
            {
                Option<Form> currentForm = ParseFormState(currentResult.Value);
                if (currentForm.HasValue)
                {
                    newState = currentForm.Value.Not();
                }
            }

            // Update distinction
            await engine.AddFactAsync($"(Distinction {context} {FormToSymbol(newState)})", ct);
            await engine.AddFactAsync($"(DistinctionCrossed {context} {DateTime.UtcNow.Ticks})", ct);

            onCrossed?.Invoke(newState);
        });
    }

    /// <summary>
    /// Creates a re-entry (self-referential) form in the flow.
    /// </summary>
    /// <param name="flow">The flow to extend.</param>
    /// <param name="context">The context for re-entry.</param>
    /// <param name="onReEntry">Optional handler for re-entry creation.</param>
    /// <returns>The flow for chaining.</returns>
    public static HyperonFlow CreateReEntry(
        this HyperonFlow flow,
        string context,
        Action<Form>? onReEntry = null)
    {
        return flow.Transform(async (engine, ct) =>
        {
            // Add re-entry fact
            await engine.AddFactAsync($"(ReEntry {context} Imaginary)", ct);
            await engine.AddFactAsync($"(ReEntryCreated {context} {DateTime.UtcNow.Ticks})", ct);

            // Add self-reference rule
            await engine.ApplyRuleAsync(
                $"(implies (ReEntry {context} $x) (SelfReference {context}))", ct);

            onReEntry?.Invoke(Form.Imaginary);
        });
    }

    /// <summary>
    /// Performs distinction-gated inference - only executes if the guard is marked.
    /// </summary>
    /// <param name="flow">The flow to extend.</param>
    /// <param name="guard">The guard distinction context.</param>
    /// <param name="query">The query to execute if guard is marked.</param>
    /// <param name="onResult">Handler for query results.</param>
    /// <returns>The flow for chaining.</returns>
    public static HyperonFlow DistinctionGated(
        this HyperonFlow flow,
        string guard,
        string query,
        Action<string>? onResult = null)
    {
        return flow.Transform(async (engine, ct) =>
        {
            // Check guard state
            Result<string, string> guardResult = await engine.ExecuteQueryAsync(
                $"(match &self (Distinction {guard} Mark) True)", ct);

            if (!guardResult.IsSuccess || !guardResult.Value.Contains("True"))
            {
                // Guard not marked, skip
                return;
            }

            // Execute gated query
            Result<string, string> result = await engine.ExecuteQueryAsync(query, ct);

            if (result.IsSuccess)
            {
                onResult?.Invoke(result.Value);

                // Log gated inference
                await engine.AddFactAsync(
                    $"(GatedInference {guard} executed {DateTime.UtcNow.Ticks})", ct);
            }
        });
    }

    /// <summary>
    /// Applies the Law of Crossing (double negation elimination).
    /// </summary>
    /// <param name="flow">The flow to extend.</param>
    /// <param name="context">The context to simplify.</param>
    /// <returns>The flow for chaining.</returns>
    public static HyperonFlow ApplyLawOfCrossing(
        this HyperonFlow flow,
        string context)
    {
        return flow.Transform(async (engine, ct) =>
        {
            // Query for double-crossed forms
            Result<string, string> result = await engine.ExecuteQueryAsync(
                $"(match &self (Distinction {context} (cross (cross $x))) $x)", ct);

            if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Value))
            {
                // Apply simplification
                await engine.AddFactAsync(
                    $"(Distinction {context} {result.Value})", ct);
                await engine.AddFactAsync(
                    $"(LawOfCrossingApplied {context} {DateTime.UtcNow.Ticks})", ct);
            }
        });
    }

    /// <summary>
    /// Applies the Law of Calling (condensation).
    /// </summary>
    /// <param name="flow">The flow to extend.</param>
    /// <param name="context">The context to condense.</param>
    /// <returns>The flow for chaining.</returns>
    public static HyperonFlow ApplyLawOfCalling(
        this HyperonFlow flow,
        string context)
    {
        return flow.Transform(async (engine, ct) =>
        {
            // Query for repeated forms
            Result<string, string> result = await engine.ExecuteQueryAsync(
                $"(match &self (Distinction {context} (call $x $x)) $x)", ct);

            if (result.IsSuccess && !string.IsNullOrWhiteSpace(result.Value))
            {
                // Apply condensation
                await engine.AddFactAsync(
                    $"(Distinction {context} {result.Value})", ct);
                await engine.AddFactAsync(
                    $"(LawOfCallingApplied {context} {DateTime.UtcNow.Ticks})", ct);
            }
        });
    }

    /// <summary>
    /// Adds a certainty check step - continues only if form is certain.
    /// </summary>
    /// <param name="flow">The flow to extend.</param>
    /// <param name="context">The context to check.</param>
    /// <param name="onCertain">Handler invoked if certain.</param>
    /// <param name="onUncertain">Handler invoked if uncertain (imaginary).</param>
    /// <returns>The flow for chaining.</returns>
    public static HyperonFlow CheckCertainty(
        this HyperonFlow flow,
        string context,
        Action<Form>? onCertain = null,
        Action? onUncertain = null)
    {
        return flow.Transform(async (engine, ct) =>
        {
            Result<string, string> result = await engine.ExecuteQueryAsync(
                $"(match &self (Distinction {context} $state) $state)", ct);

            if (!result.IsSuccess)
            {
                onUncertain?.Invoke();
                return;
            }

            Option<Form> form = ParseFormState(result.Value);

            if (!form.HasValue || form.Value.IsImaginary())
            {
                onUncertain?.Invoke();
                await engine.AddFactAsync(
                    $"(UncertaintyDetected {context} {DateTime.UtcNow.Ticks})", ct);
            }
            else
            {
                onCertain?.Invoke(form.Value);
            }
        });
    }

    /// <summary>
    /// Creates a form-based conditional branch in the flow.
    /// </summary>
    /// <param name="flow">The flow to extend.</param>
    /// <param name="context">The distinction context to check.</param>
    /// <param name="onMarked">Flow continuation if marked.</param>
    /// <param name="onVoid">Flow continuation if void.</param>
    /// <param name="onImaginary">Flow continuation if imaginary.</param>
    /// <returns>The flow for chaining.</returns>
    public static HyperonFlow FormBranch(
        this HyperonFlow flow,
        string context,
        Func<HyperonFlow, HyperonFlow>? onMarked = null,
        Func<HyperonFlow, HyperonFlow>? onVoid = null,
        Func<HyperonFlow, HyperonFlow>? onImaginary = null)
    {
        return flow.Transform(async (engine, ct) =>
        {
            Result<string, string> result = await engine.ExecuteQueryAsync(
                $"(match &self (Distinction {context} $state) $state)", ct);

            if (!result.IsSuccess)
            {
                // Default to void branch
                onVoid?.Invoke(flow);
                return;
            }

            Option<Form> form = ParseFormState(result.Value);

            if (!form.HasValue)
            {
                onVoid?.Invoke(flow);
                return;
            }

            form.Value.Match(
                onMark: () => onMarked?.Invoke(flow),
                onVoid: () => onVoid?.Invoke(flow),
                onImaginary: () => onImaginary?.Invoke(flow));
        });
    }

    /// <summary>
    /// Adds meta-reasoning about form states.
    /// </summary>
    /// <param name="flow">The flow to extend.</param>
    /// <param name="onMeta">Handler for meta-level insights.</param>
    /// <returns>The flow for chaining.</returns>
    public static HyperonFlow MetaReasonAboutForms(
        this HyperonFlow flow,
        Action<string>? onMeta = null)
    {
        return flow.Transform(async (engine, ct) =>
        {
            // Query all distinctions
            Result<string, string> distinctions = await engine.ExecuteQueryAsync(
                "(match &self (Distinction $ctx $state) (: $ctx $state))", ct);

            if (!distinctions.IsSuccess || string.IsNullOrWhiteSpace(distinctions.Value))
            {
                return;
            }

            // Count marked vs void vs imaginary
            var marked = 0;
            var voidCount = 0;
            var imaginary = 0;

            foreach (string part in distinctions.Value.Split(new[] { '\n', ' ', '(', ')' },
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (part == "Mark")
                {
                    marked++;
                }
                else if (part == "Void")
                {
                    voidCount++;
                }
                else if (part == "Imaginary")
                {
                    imaginary++;
                }
            }

            // Add meta-level facts
            await engine.AddFactAsync(
                $"(FormStatistics marked {marked} void {voidCount} imaginary {imaginary})", ct);

            if (imaginary > 0)
            {
                await engine.AddFactAsync(
                    $"(SystemHasUncertainty count {imaginary})", ct);
            }

            double certaintyRatio = (marked + voidCount) / (double)(marked + voidCount + imaginary);
            await engine.AddFactAsync(
                $"(CertaintyRatio {certaintyRatio:F2})", ct);

            onMeta?.Invoke($"Forms: {marked} marked, {voidCount} void, {imaginary} imaginary. Certainty: {certaintyRatio:P0}");
        });
    }

    /// <summary>
    /// Creates a consciousness loop with form-based self-reflection.
    /// </summary>
    /// <param name="integration">The flow integration.</param>
    /// <param name="loopId">Unique loop identifier.</param>
    /// <param name="onReflection">Handler for reflection events.</param>
    /// <param name="interval">Reflection interval.</param>
    /// <returns>CancellationTokenSource to stop the loop.</returns>
    public static CancellationTokenSource CreateFormAwareConsciousnessLoop(
        this HyperonFlowIntegration integration,
        string loopId,
        Action<FormReasoningEventArgs>? onReflection = null,
        TimeSpan? interval = null)
    {
        CancellationTokenSource cts = integration.CreateConsciousnessLoop(loopId, 2, interval);

        // Subscribe to distinction patterns
        integration.SubscribePattern(
            $"{loopId}_form_changes",
            "(Distinction $ctx $state)",
            match =>
            {
                Option<Atom> ctxOpt = match.Bindings.Lookup("ctx");
                Option<Atom> stateOpt = match.Bindings.Lookup("state");

                if (ctxOpt.HasValue && stateOpt.HasValue)
                {
                    Option<Form> form = ParseFormStateFromAtom(stateOpt.Value!);

                    onReflection?.Invoke(new FormReasoningEventArgs
                    {
                        Operation = "distinction_observed",
                        FormState = form.HasValue ? form.Value : Form.Void,
                        Context = ctxOpt.Value!.ToSExpr(),
                        RelatedAtoms = match.MatchedAtoms
                    });
                }
            });

        // Subscribe to re-entry patterns
        integration.SubscribePattern(
            $"{loopId}_reentry_changes",
            "(ReEntry $ctx $state)",
            match =>
            {
                onReflection?.Invoke(new FormReasoningEventArgs
                {
                    Operation = "reentry_observed",
                    FormState = Form.Imaginary,
                    RelatedAtoms = match.MatchedAtoms
                });
            });

        return cts;
    }

    private static Option<Form> ParseFormState(string value)
    {
        string trimmed = value.Trim();

        if (trimmed.Contains("Mark") || trimmed == "⌐")
        {
            return Option<Form>.Some(Form.Mark);
        }

        if (trimmed.Contains("Void") || trimmed == "∅")
        {
            return Option<Form>.Some(Form.Void);
        }

        if (trimmed.Contains("Imaginary") || trimmed == "ℑ" || trimmed == "i")
        {
            return Option<Form>.Some(Form.Imaginary);
        }

        return Option<Form>.None();
    }

    private static Option<Form> ParseFormStateFromAtom(Atom atom)
    {
        if (atom is Symbol sym)
        {
            return sym.Name switch
            {
                "Mark" or "⌐" => Option<Form>.Some(Form.Mark),
                "Void" or "∅" => Option<Form>.Some(Form.Void),
                "Imaginary" or "ℑ" or "i" => Option<Form>.Some(Form.Imaginary),
                _ => Option<Form>.None()
            };
        }

        return Option<Form>.None();
    }

    private static string FormToSymbol(Form form)
    {
        return form.Match(
            onMark: () => "Mark",
            onVoid: () => "Void",
            onImaginary: () => "Imaginary");
    }
}
