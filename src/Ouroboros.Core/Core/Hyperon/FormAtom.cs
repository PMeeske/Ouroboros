// <copyright file="FormAtom.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using Ouroboros.Core.LawsOfForm;

namespace Ouroboros.Core.Hyperon;

/// <summary>
/// Represents a Laws of Form distinction as a MeTTa atom.
/// This creates a grounded type that embeds LoF reasoning into Hyperon.
/// </summary>
public sealed record FormAtom : Atom
{
    /// <summary>
    /// Gets the underlying Laws of Form value.
    /// </summary>
    public Form Form { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FormAtom"/> class.
    /// </summary>
    /// <param name="form">The Laws of Form value.</param>
    public FormAtom(Form form)
    {
        Form = form;
    }

    /// <summary>
    /// Creates a FormAtom from Mark (distinction).
    /// </summary>
    public static FormAtom Mark => new(Form.Mark);

    /// <summary>
    /// Creates a FormAtom from Void (no distinction).
    /// </summary>
    public static FormAtom Void => new(Form.Void);

    /// <summary>
    /// Creates a FormAtom from Imaginary (re-entrant).
    /// </summary>
    public static FormAtom Imaginary => new(Form.Imaginary);

    /// <inheritdoc/>
    public override string ToSExpr()
        => Form.Match(
            onMark: () => "⌐",
            onVoid: () => "∅",
            onImaginary: () => "ℑ");

    /// <inheritdoc/>
    public override bool ContainsVariables() => false;

    /// <summary>
    /// Applies the Law of Crossing (negation).
    /// </summary>
    /// <returns>The crossed form.</returns>
    public FormAtom Cross() => new(Form.Not());

    /// <summary>
    /// Applies the Law of Calling (idempotent operation).
    /// </summary>
    /// <param name="other">The form to call with.</param>
    /// <returns>The result of calling.</returns>
    public FormAtom Call(FormAtom other) => new(Form.Call(other.Form));

    /// <summary>
    /// Conjunction with another form.
    /// </summary>
    /// <param name="other">The other form.</param>
    /// <returns>The conjunction result.</returns>
    public FormAtom And(FormAtom other) => new(Form.And(other.Form));

    /// <summary>
    /// Disjunction with another form.
    /// </summary>
    /// <param name="other">The other form.</param>
    /// <returns>The disjunction result.</returns>
    public FormAtom Or(FormAtom other) => new(Form.Or(other.Form));

    /// <summary>
    /// Evaluates the form through Laws of Form reduction rules.
    /// </summary>
    /// <returns>The evaluated form.</returns>
    public FormAtom Eval() => new(Form.Eval());
}

/// <summary>
/// Represents a Laws of Form expression tree as a MeTTa atom.
/// This allows complex nested forms to be represented and manipulated.
/// </summary>
public sealed record FormExpression : Atom
{
    /// <summary>
    /// Gets the operator symbol for this expression.
    /// </summary>
    public string Operator { get; }

    /// <summary>
    /// Gets the operands of this expression.
    /// </summary>
    public ImmutableList<Atom> Operands { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FormExpression"/> class.
    /// </summary>
    /// <param name="op">The operator.</param>
    /// <param name="operands">The operands.</param>
    public FormExpression(string op, ImmutableList<Atom> operands)
    {
        Operator = op;
        Operands = operands;
    }

    /// <summary>
    /// Creates a Cross (mark/negation) expression.
    /// </summary>
    /// <param name="inner">The inner form.</param>
    /// <returns>A cross expression.</returns>
    public static FormExpression Cross(Atom inner)
        => new("cross", ImmutableList.Create(inner));

    /// <summary>
    /// Creates a Call (indication) expression.
    /// </summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>A call expression.</returns>
    public static FormExpression Call(Atom left, Atom right)
        => new("call", ImmutableList.Create(left, right));

    /// <summary>
    /// Creates a ReEntry (self-reference) expression.
    /// </summary>
    /// <param name="form">The form to re-enter.</param>
    /// <returns>A re-entry expression.</returns>
    public static FormExpression ReEntry(Atom form)
        => new("reentry", ImmutableList.Create(form));

    /// <inheritdoc/>
    public override string ToSExpr()
        => $"({Operator} {string.Join(" ", Operands.Select(o => o.ToSExpr()))})";

    /// <inheritdoc/>
    public override bool ContainsVariables()
        => Operands.Any(o => o.ContainsVariables());

    /// <summary>
    /// Evaluates this form expression according to Laws of Form rules.
    /// </summary>
    /// <returns>The reduced form atom.</returns>
    public FormAtom Evaluate()
    {
        return Operator switch
        {
            "cross" => EvaluateCross(),
            "call" => EvaluateCall(),
            "reentry" => FormAtom.Imaginary,
            "and" => EvaluateBinary((a, b) => a.And(b)),
            "or" => EvaluateBinary((a, b) => a.Or(b)),
            _ => FormAtom.Void
        };
    }

    private FormAtom EvaluateCross()
    {
        if (Operands.Count == 0)
        {
            return FormAtom.Mark;
        }

        var inner = ResolveToFormAtom(Operands[0]);
        return inner.Cross();
    }

    private FormAtom EvaluateCall()
    {
        if (Operands.Count < 2)
        {
            return FormAtom.Void;
        }

        var left = ResolveToFormAtom(Operands[0]);
        var right = ResolveToFormAtom(Operands[1]);
        return left.Call(right);
    }

    private FormAtom EvaluateBinary(Func<FormAtom, FormAtom, FormAtom> op)
    {
        if (Operands.Count < 2)
        {
            return FormAtom.Void;
        }

        var left = ResolveToFormAtom(Operands[0]);
        var right = ResolveToFormAtom(Operands[1]);
        return op(left, right);
    }

    private static FormAtom ResolveToFormAtom(Atom atom)
    {
        return atom switch
        {
            FormAtom fa => fa,
            FormExpression fe => fe.Evaluate(),
            Symbol s when s.Name == "Mark" || s.Name == "⌐" => FormAtom.Mark,
            Symbol s when s.Name == "Void" || s.Name == "∅" => FormAtom.Void,
            Symbol s when s.Name == "Imaginary" || s.Name == "ℑ" => FormAtom.Imaginary,
            _ => FormAtom.Void
        };
    }
}
