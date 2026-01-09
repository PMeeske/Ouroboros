// <copyright file="FormMeTTaBridge.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Collections.Immutable;
using Ouroboros.Core.LawsOfForm;

namespace Ouroboros.Core.Hyperon;

/// <summary>
/// Event arguments for distinction events in the Laws of Form / MeTTa bridge.
/// </summary>
public sealed class DistinctionEventArgs : EventArgs
{
    /// <summary>
    /// Gets the type of distinction event.
    /// </summary>
    public DistinctionEventType EventType { get; init; }

    /// <summary>
    /// Gets the form state before the event.
    /// </summary>
    public Form? PreviousState { get; init; }

    /// <summary>
    /// Gets the form state after the event.
    /// </summary>
    public Form CurrentState { get; init; }

    /// <summary>
    /// Gets the atom that triggered this distinction (if any).
    /// </summary>
    public Atom? TriggerAtom { get; init; }

    /// <summary>
    /// Gets the name or identifier of the distinction context.
    /// </summary>
    public string? Context { get; init; }

    /// <summary>
    /// Gets the timestamp of the event.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Types of distinction events that can occur.
/// </summary>
public enum DistinctionEventType
{
    /// <summary>
    /// A new distinction was drawn (mark created).
    /// </summary>
    DistinctionDrawn,

    /// <summary>
    /// A distinction was crossed (negated).
    /// </summary>
    Crossed,

    /// <summary>
    /// Two distinctions were condensed (Law of Calling).
    /// </summary>
    Condensed,

    /// <summary>
    /// A double crossing was cancelled (Law of Crossing).
    /// </summary>
    Cancelled,

    /// <summary>
    /// A re-entry (self-reference) was created.
    /// </summary>
    ReEntryCreated,

    /// <summary>
    /// A distinction collapsed from imaginary to certain.
    /// </summary>
    Collapsed,

    /// <summary>
    /// A pattern was matched in the atom space.
    /// </summary>
    PatternMatched,

    /// <summary>
    /// An inference was derived through unification.
    /// </summary>
    InferenceDerived
}

/// <summary>
/// Event arguments for truth value changes in Laws of Form reasoning.
/// </summary>
public sealed class TruthValueEventArgs : EventArgs
{
    /// <summary>
    /// Gets the expression being evaluated.
    /// </summary>
    public required Atom Expression { get; init; }

    /// <summary>
    /// Gets the evaluated truth value as a Form.
    /// </summary>
    public Form TruthValue { get; init; }

    /// <summary>
    /// Gets the reasoning trace that led to this truth value.
    /// </summary>
    public ImmutableList<string> ReasoningTrace { get; init; } = ImmutableList<string>.Empty;

    /// <summary>
    /// Gets whether the truth value is certain (Mark or Void) or uncertain (Imaginary).
    /// </summary>
    public bool IsCertain => TruthValue.IsCertain();
}

/// <summary>
/// Event arguments for meta-level reasoning events.
/// </summary>
public sealed class MetaReasoningEventArgs : EventArgs
{
    /// <summary>
    /// Gets the meta-level operation being performed.
    /// </summary>
    public required string Operation { get; init; }

    /// <summary>
    /// Gets the object-level expression being reasoned about.
    /// </summary>
    public required Atom ObjectLevel { get; init; }

    /// <summary>
    /// Gets the meta-level representation.
    /// </summary>
    public required Atom MetaLevel { get; init; }

    /// <summary>
    /// Gets any bindings discovered during meta-reasoning.
    /// </summary>
    public Substitution Bindings { get; init; } = Substitution.Empty;
}

/// <summary>
/// Bridge between Laws of Form distinctions and MeTTa/Hyperon symbolic reasoning.
/// Uses events to connect the two paradigms, enabling:
/// - Distinction-gated inference
/// - Form-based pattern matching
/// - Meta-level reasoning about certainty
/// - Self-referential loops via re-entry.
/// </summary>
public sealed class FormMeTTaBridge : IDisposable
{
    private readonly IAtomSpace _space;
    private readonly Interpreter _interpreter;
    private readonly ConcurrentDictionary<string, Form> _distinctionContext;
    private readonly ConcurrentDictionary<string, FormAtom> _formCache;
    private bool _disposed;

    /// <summary>
    /// Raised when a distinction is drawn, crossed, or otherwise modified.
    /// </summary>
    public event EventHandler<DistinctionEventArgs>? DistinctionChanged;

    /// <summary>
    /// Raised when a truth value is evaluated.
    /// </summary>
    public event EventHandler<TruthValueEventArgs>? TruthValueEvaluated;

    /// <summary>
    /// Raised when meta-level reasoning occurs.
    /// </summary>
    public event EventHandler<MetaReasoningEventArgs>? MetaReasoningPerformed;

    /// <summary>
    /// Initializes a new instance of the <see cref="FormMeTTaBridge"/> class.
    /// </summary>
    /// <param name="space">The atom space for MeTTa operations.</param>
    /// <param name="groundedOps">Optional custom grounded operations.</param>
    public FormMeTTaBridge(IAtomSpace space, GroundedRegistry? groundedOps = null)
    {
        _space = space ?? throw new ArgumentNullException(nameof(space));
        _distinctionContext = new ConcurrentDictionary<string, Form>();
        _formCache = new ConcurrentDictionary<string, FormAtom>();

        // Create registry with Laws of Form operations
        var registry = groundedOps ?? GroundedRegistry.CreateStandard();
        RegisterFormOperations(registry);

        _interpreter = new Interpreter(space, registry);

        // Add Laws of Form axioms to the space
        InitializeFormAxioms();
    }

    /// <summary>
    /// Draws a distinction in the given context.
    /// </summary>
    /// <param name="context">The context name.</param>
    /// <param name="reason">Optional reason for the distinction.</param>
    /// <returns>The resulting form.</returns>
    public Form DrawDistinction(string context, Atom? reason = null)
    {
        var previous = _distinctionContext.GetValueOrDefault(context, Form.Void);
        var newForm = Form.Mark;

        _distinctionContext[context] = newForm;
        _formCache[context] = new FormAtom(newForm);

        // Add to atom space
        var distinctionAtom = Atom.Expr(
            Atom.Sym("Distinction"),
            Atom.Sym(context),
            Atom.Sym("Mark"));
        _space.Add(distinctionAtom);

        OnDistinctionChanged(new DistinctionEventArgs
        {
            EventType = DistinctionEventType.DistinctionDrawn,
            PreviousState = previous,
            CurrentState = newForm,
            TriggerAtom = reason,
            Context = context
        });

        return newForm;
    }

    /// <summary>
    /// Crosses (negates) a distinction in the given context.
    /// </summary>
    /// <param name="context">The context name.</param>
    /// <returns>The crossed form.</returns>
    public Form CrossDistinction(string context)
    {
        var previous = _distinctionContext.GetValueOrDefault(context, Form.Void);
        var newForm = previous.Not();

        _distinctionContext[context] = newForm;
        _formCache[context] = new FormAtom(newForm);

        // Determine if this is a cancellation (double crossing)
        var eventType = newForm.IsVoid() && previous.IsMarked()
            ? DistinctionEventType.Cancelled
            : DistinctionEventType.Crossed;

        // Update atom space
        var distinctionAtom = Atom.Expr(
            Atom.Sym("Distinction"),
            Atom.Sym(context),
            newForm.ToMeTTa());
        _space.Add(distinctionAtom);

        OnDistinctionChanged(new DistinctionEventArgs
        {
            EventType = eventType,
            PreviousState = previous,
            CurrentState = newForm,
            Context = context
        });

        return newForm;
    }

    /// <summary>
    /// Creates a re-entry (self-referential form) in the given context.
    /// </summary>
    /// <param name="context">The context name.</param>
    /// <returns>The imaginary (re-entrant) form.</returns>
    public Form CreateReEntry(string context)
    {
        var previous = _distinctionContext.GetValueOrDefault(context, Form.Void);
        var newForm = Form.Imaginary;

        _distinctionContext[context] = newForm;
        _formCache[context] = new FormAtom(newForm);

        // Add re-entry to atom space
        var reentryAtom = Atom.Expr(
            Atom.Sym("ReEntry"),
            Atom.Sym(context),
            Atom.Sym("Imaginary"));
        _space.Add(reentryAtom);

        OnDistinctionChanged(new DistinctionEventArgs
        {
            EventType = DistinctionEventType.ReEntryCreated,
            PreviousState = previous,
            CurrentState = newForm,
            Context = context
        });

        return newForm;
    }

    /// <summary>
    /// Evaluates an atom's truth value using Laws of Form semantics.
    /// </summary>
    /// <param name="expression">The expression to evaluate.</param>
    /// <returns>The truth value as a Form.</returns>
    public Form EvaluateTruthValue(Atom expression)
    {
        var trace = ImmutableList.CreateBuilder<string>();
        var result = EvaluateTruthValueInternal(expression, trace);

        OnTruthValueEvaluated(new TruthValueEventArgs
        {
            Expression = expression,
            TruthValue = result,
            ReasoningTrace = trace.ToImmutable()
        });

        return result;
    }

    /// <summary>
    /// Performs distinction-gated inference.
    /// Only performs inference if the guard distinction is marked.
    /// </summary>
    /// <param name="guard">The guard context name.</param>
    /// <param name="query">The query to execute if guard is marked.</param>
    /// <returns>Results if guard is marked, empty otherwise.</returns>
    public IEnumerable<Atom> DistinctionGatedInference(string guard, Atom query)
    {
        var guardForm = _distinctionContext.GetValueOrDefault(guard, Form.Void);

        if (!guardForm.IsMarked())
        {
            yield break;
        }

        foreach (var result in _interpreter.Evaluate(query))
        {
            OnDistinctionChanged(new DistinctionEventArgs
            {
                EventType = DistinctionEventType.InferenceDerived,
                CurrentState = guardForm,
                TriggerAtom = query,
                Context = guard
            });

            yield return result;
        }
    }

    /// <summary>
    /// Matches a pattern and returns results with their form states.
    /// </summary>
    /// <param name="pattern">The pattern to match.</param>
    /// <returns>Matching results with associated form states.</returns>
    public IEnumerable<(Atom Result, Form State, Substitution Bindings)> FormGatedMatch(Atom pattern)
    {
        foreach (var (result, bindings) in _interpreter.EvaluateWithBindings(pattern))
        {
            // Compute the form state based on certainty of bindings
            var form = ComputeBindingCertainty(bindings);

            OnDistinctionChanged(new DistinctionEventArgs
            {
                EventType = DistinctionEventType.PatternMatched,
                CurrentState = form,
                TriggerAtom = pattern
            });

            yield return (result, form, bindings);
        }
    }

    /// <summary>
    /// Performs meta-reasoning about an expression.
    /// Creates a quoted representation and reasons about its structure.
    /// </summary>
    /// <param name="expression">The expression to reason about.</param>
    /// <returns>Meta-level insights as atoms.</returns>
    public IEnumerable<Atom> MetaReason(Atom expression)
    {
        // Create meta-level representation
        var metaAtom = MeTTaSpec.Quote(expression);

        // Add meta-level facts about the expression
        var metaFacts = new List<Atom>
        {
            Atom.Expr(Atom.Sym("is-expression"), metaAtom),
            Atom.Expr(Atom.Sym("has-variables"), Atom.Sym(expression.ContainsVariables() ? "True" : "False"))
        };

        if (expression is Expression expr && expr.Children.Count > 0)
        {
            metaFacts.Add(Atom.Expr(Atom.Sym("head"), metaAtom, expr.Children[0]));
            metaFacts.Add(Atom.Expr(Atom.Sym("arity"), metaAtom, Atom.Sym(expr.Children.Count.ToString())));
        }

        foreach (var fact in metaFacts)
        {
            _space.Add(fact);

            OnMetaReasoningPerformed(new MetaReasoningEventArgs
            {
                Operation = "analyze",
                ObjectLevel = expression,
                MetaLevel = fact
            });

            yield return fact;
        }
    }

    /// <summary>
    /// Gets the current form state for a context.
    /// </summary>
    /// <param name="context">The context name.</param>
    /// <returns>The current form, or Void if not set.</returns>
    public Form GetFormState(string context)
        => _distinctionContext.GetValueOrDefault(context, Form.Void);

    /// <summary>
    /// Gets all active distinctions.
    /// </summary>
    /// <returns>Dictionary of context names to forms.</returns>
    public IReadOnlyDictionary<string, Form> GetAllDistinctions()
        => _distinctionContext.ToImmutableDictionary();

    /// <summary>
    /// Clears a distinction context.
    /// </summary>
    /// <param name="context">The context to clear.</param>
    public void ClearDistinction(string context)
    {
        _distinctionContext.TryRemove(context, out _);
        _formCache.TryRemove(context, out _);
    }

    /// <summary>
    /// Gets the underlying interpreter.
    /// </summary>
    public Interpreter Interpreter => _interpreter;

    private void InitializeFormAxioms()
    {
        // Law of Crossing: (implies (cross (cross X)) X)
        _space.Add(MeTTaSpec.Implies(
            Atom.Expr(Atom.Sym("cross"), Atom.Expr(Atom.Sym("cross"), Atom.Var("X"))),
            Atom.Var("X")));

        // Law of Calling: (implies (call X X) X)
        _space.Add(MeTTaSpec.Implies(
            Atom.Expr(Atom.Sym("call"), Atom.Var("X"), Atom.Var("X")),
            Atom.Var("X")));

        // Mark crossed with Void equals Mark
        _space.Add(MeTTaSpec.Implies(
            Atom.Expr(Atom.Sym("cross"), Atom.Sym("Void")),
            Atom.Sym("Mark")));

        // Void is the identity for Call
        _space.Add(MeTTaSpec.Implies(
            Atom.Expr(Atom.Sym("call"), Atom.Var("X"), Atom.Sym("Void")),
            Atom.Var("X")));

        // Re-entry produces Imaginary
        _space.Add(MeTTaSpec.Implies(
            Atom.Expr(Atom.Sym("reentry"), Atom.Var("X")),
            Atom.Sym("Imaginary")));

        // Type declarations for Form values
        _space.Add(MeTTaSpec.TypeOf(Atom.Sym("Mark"), Atom.Sym("Form")));
        _space.Add(MeTTaSpec.TypeOf(Atom.Sym("Void"), Atom.Sym("Form")));
        _space.Add(MeTTaSpec.TypeOf(Atom.Sym("Imaginary"), Atom.Sym("Form")));
    }

    private void RegisterFormOperations(GroundedRegistry registry)
    {
        // cross: Apply Law of Crossing (negation)
        registry.Register("cross", (space, args) =>
        {
            if (args.Children.Count < 2)
            {
                return Enumerable.Empty<Atom>();
            }

            var inner = args.Children[1];
            var formOpt = inner.ToForm();

            if (formOpt.HasValue)
            {
                return new[] { formOpt.Value.Not().ToMeTTa() };
            }

            // Handle nested cross expressions
            if (inner is Expression expr &&
                expr.Children.Count > 0 &&
                expr.Children[0] is Symbol s &&
                s.Name == "cross")
            {
                // Double crossing cancels: (cross (cross X)) = X
                if (expr.Children.Count > 1)
                {
                    return new[] { expr.Children[1] };
                }
            }

            return new[] { FormExpression.Cross(inner) };
        });

        // call: Apply Law of Calling (indication)
        registry.Register("call", (space, args) =>
        {
            if (args.Children.Count < 3)
            {
                return Enumerable.Empty<Atom>();
            }

            var left = args.Children[1];
            var right = args.Children[2];

            var leftForm = left.ToForm();
            var rightForm = right.ToForm();

            if (leftForm.HasValue && rightForm.HasValue)
            {
                return new[] { leftForm.Value.Call(rightForm.Value).ToMeTTa() };
            }

            // Identity law: call X Void = X
            if (right is Symbol sym && sym.Name == "Void")
            {
                return new[] { left };
            }

            return new[] { FormExpression.Call(left, right) };
        });

        // reentry: Create self-referential form
        registry.Register("reentry", (_, args) =>
        {
            return new[] { Atom.Sym("Imaginary") };
        });

        // eval-form: Evaluate a form expression
        registry.Register("eval-form", (_, args) =>
        {
            if (args.Children.Count < 2)
            {
                return new[] { Atom.Sym("Void") };
            }

            var expr = args.Children[1];

            if (expr is FormExpression fe)
            {
                return new[] { fe.Evaluate() };
            }

            var formOpt = expr.ToForm();
            return formOpt.HasValue
                ? new[] { formOpt.Value.Eval().ToMeTTa() }
                : new[] { Atom.Sym("Void") };
        });

        // is-marked: Check if a form is marked
        registry.Register("is-marked", (_, args) =>
        {
            if (args.Children.Count < 2)
            {
                return new[] { MeTTaSpec.False };
            }

            var formOpt = args.Children[1].ToForm();
            return formOpt.HasValue && formOpt.Value.IsMarked()
                ? new[] { MeTTaSpec.True }
                : new[] { MeTTaSpec.False };
        });

        // is-certain: Check if a form is certain (not imaginary)
        registry.Register("is-certain", (_, args) =>
        {
            if (args.Children.Count < 2)
            {
                return new[] { MeTTaSpec.False };
            }

            var formOpt = args.Children[1].ToForm();
            return formOpt.HasValue && formOpt.Value.IsCertain()
                ? new[] { MeTTaSpec.True }
                : new[] { MeTTaSpec.False };
        });
    }

    private Form EvaluateTruthValueInternal(Atom expression, ImmutableList<string>.Builder trace)
    {
        trace.Add($"Evaluating: {expression.ToSExpr()}");

        // Direct form atoms
        if (expression is FormAtom fa)
        {
            trace.Add($"Direct FormAtom: {fa.Form}");
            return fa.Form;
        }

        // Symbol to form conversion
        var directForm = expression.ToForm();
        if (directForm.HasValue)
        {
            trace.Add($"Symbol to Form: {directForm.Value}");
            return directForm.Value;
        }

        // Expression evaluation
        if (expression is Expression expr && expr.Children.Count > 0)
        {
            if (expr.Children[0] is Symbol head)
            {
                return head.Name switch
                {
                    "cross" => EvaluateCross(expr, trace),
                    "call" => EvaluateCall(expr, trace),
                    "and" => EvaluateAnd(expr, trace),
                    "or" => EvaluateOr(expr, trace),
                    "not" => EvaluateNot(expr, trace),
                    "reentry" => EvaluateReentry(trace),
                    "implies" => EvaluateImplies(expr, trace),
                    _ => EvaluateGeneric(expr, trace)
                };
            }
        }

        // Query the interpreter
        var results = _interpreter.Evaluate(expression).ToList();
        trace.Add($"Interpreter results: {results.Count}");

        if (results.Count == 0)
        {
            return Form.Void;
        }

        // If any result is marked, return Mark
        foreach (var result in results)
        {
            var resultForm = result.ToForm();
            if (resultForm.HasValue && resultForm.Value.IsMarked())
            {
                return Form.Mark;
            }
        }

        return Form.Void;
    }

    private Form EvaluateCross(Expression expr, ImmutableList<string>.Builder trace)
    {
        if (expr.Children.Count < 2)
        {
            trace.Add("Cross with no argument = Mark");
            return Form.Mark;
        }

        var innerForm = EvaluateTruthValueInternal(expr.Children[1], trace);
        var result = innerForm.Not();
        trace.Add($"Cross({innerForm}) = {result}");
        return result;
    }

    private Form EvaluateCall(Expression expr, ImmutableList<string>.Builder trace)
    {
        if (expr.Children.Count < 3)
        {
            trace.Add("Call with insufficient arguments = Void");
            return Form.Void;
        }

        var left = EvaluateTruthValueInternal(expr.Children[1], trace);
        var right = EvaluateTruthValueInternal(expr.Children[2], trace);
        var result = left.Call(right);
        trace.Add($"Call({left}, {right}) = {result}");
        return result;
    }

    private Form EvaluateAnd(Expression expr, ImmutableList<string>.Builder trace)
    {
        if (expr.Children.Count < 3)
        {
            return Form.Void;
        }

        var left = EvaluateTruthValueInternal(expr.Children[1], trace);
        var right = EvaluateTruthValueInternal(expr.Children[2], trace);
        var result = left.And(right);
        trace.Add($"And({left}, {right}) = {result}");
        return result;
    }

    private Form EvaluateOr(Expression expr, ImmutableList<string>.Builder trace)
    {
        if (expr.Children.Count < 3)
        {
            return Form.Void;
        }

        var left = EvaluateTruthValueInternal(expr.Children[1], trace);
        var right = EvaluateTruthValueInternal(expr.Children[2], trace);
        var result = left.Or(right);
        trace.Add($"Or({left}, {right}) = {result}");
        return result;
    }

    private Form EvaluateNot(Expression expr, ImmutableList<string>.Builder trace)
    {
        if (expr.Children.Count < 2)
        {
            return Form.Mark;
        }

        var inner = EvaluateTruthValueInternal(expr.Children[1], trace);
        var result = inner.Not();
        trace.Add($"Not({inner}) = {result}");
        return result;
    }

    private Form EvaluateReentry(ImmutableList<string>.Builder trace)
    {
        trace.Add("ReEntry = Imaginary");
        return Form.Imaginary;
    }

    private Form EvaluateImplies(Expression expr, ImmutableList<string>.Builder trace)
    {
        if (expr.Children.Count < 3)
        {
            return Form.Void;
        }

        var condition = EvaluateTruthValueInternal(expr.Children[1], trace);

        // Short-circuit: false antecedent makes implication vacuously true
        if (condition.IsVoid())
        {
            trace.Add("Implies: condition is Void, vacuously true");
            return Form.Mark;
        }

        // Imaginary condition propagates uncertainty
        if (condition.IsImaginary())
        {
            trace.Add("Implies: condition is Imaginary, result uncertain");
            return Form.Imaginary;
        }

        var conclusion = EvaluateTruthValueInternal(expr.Children[2], trace);
        trace.Add($"Implies({condition}, {conclusion}) = {conclusion}");
        return conclusion;
    }

    private Form EvaluateGeneric(Expression expr, ImmutableList<string>.Builder trace)
    {
        // Generic evaluation: check if expression matches anything
        var matches = _space.Query(expr).ToList();
        trace.Add($"Generic query found {matches.Count} matches");
        return matches.Count > 0 ? Form.Mark : Form.Void;
    }

    private Form ComputeBindingCertainty(Substitution bindings)
    {
        if (bindings.IsEmpty)
        {
            return Form.Mark; // Ground match is certain
        }

        // Check if any binding contains variables (uncertain)
        foreach (var kvp in bindings.Bindings)
        {
            if (kvp.Value.ContainsVariables())
            {
                return Form.Imaginary; // Still has unknowns
            }
        }

        return Form.Mark; // All bindings are ground
    }

    private void OnDistinctionChanged(DistinctionEventArgs e)
        => DistinctionChanged?.Invoke(this, e);

    private void OnTruthValueEvaluated(TruthValueEventArgs e)
        => TruthValueEvaluated?.Invoke(this, e);

    private void OnMetaReasoningPerformed(MetaReasoningEventArgs e)
        => MetaReasoningPerformed?.Invoke(this, e);

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _distinctionContext.Clear();
        _formCache.Clear();
        _disposed = true;
    }
}
