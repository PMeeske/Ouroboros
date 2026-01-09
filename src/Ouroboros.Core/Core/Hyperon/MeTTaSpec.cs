// <copyright file="MeTTaSpec.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using Ouroboros.Core.LawsOfForm;

namespace Ouroboros.Core.Hyperon;

/// <summary>
/// MeTTa (Meta Type Talk) specification types for Hyperon.
/// MeTTa is a symbolic AI language that combines:
/// - Pattern matching and unification
/// - Functional programming with type inference
/// - Meta-level reasoning and self-modification
/// - Grounded operations connecting to external systems.
/// </summary>
public static class MeTTaSpec
{
    #region Core MeTTa Types

    /// <summary>
    /// Represents a MeTTa type (metatype) for the type system.
    /// </summary>
    public static Symbol Type => Atom.Sym("Type");

    /// <summary>
    /// Represents the function type constructor.
    /// (-> InputType OutputType) represents a function type.
    /// </summary>
    public static Symbol Arrow => Atom.Sym("->");

    /// <summary>
    /// Represents the Atom metatype - all atoms inherit from Atom.
    /// </summary>
    public static Symbol AtomType => Atom.Sym("Atom");

    /// <summary>
    /// Represents the Symbol metatype.
    /// </summary>
    public static Symbol SymbolType => Atom.Sym("Symbol");

    /// <summary>
    /// Represents the Variable metatype.
    /// </summary>
    public static Symbol VariableType => Atom.Sym("Variable");

    /// <summary>
    /// Represents the Expression metatype.
    /// </summary>
    public static Symbol ExpressionType => Atom.Sym("Expression");

    /// <summary>
    /// Represents grounded types that connect to external systems.
    /// </summary>
    public static Symbol GroundedType => Atom.Sym("Grounded");

    /// <summary>
    /// Represents the Unit type (empty/void type).
    /// </summary>
    public static Symbol Unit => Atom.Sym("Unit");

    /// <summary>
    /// Represents the Boolean type.
    /// </summary>
    public static Symbol Bool => Atom.Sym("Bool");

    /// <summary>
    /// Represents the Number type.
    /// </summary>
    public static Symbol Number => Atom.Sym("Number");

    /// <summary>
    /// Represents the String type.
    /// </summary>
    public static Symbol String => Atom.Sym("String");

    #endregion

    #region Standard Operations

    /// <summary>
    /// Creates a type annotation expression: (: term type).
    /// </summary>
    /// <param name="term">The term to annotate.</param>
    /// <param name="type">The type annotation.</param>
    /// <returns>Type annotation expression.</returns>
    public static Expression TypeOf(Atom term, Atom type)
        => Atom.Expr(Atom.Sym(":"), term, type);

    /// <summary>
    /// Creates a function type expression: (-> input output).
    /// </summary>
    /// <param name="input">The input type.</param>
    /// <param name="output">The output type.</param>
    /// <returns>Function type expression.</returns>
    public static Expression FunctionType(Atom input, Atom output)
        => Atom.Expr(Arrow, input, output);

    /// <summary>
    /// Creates a match expression: (match space pattern template).
    /// </summary>
    /// <param name="space">The space to match in.</param>
    /// <param name="pattern">The pattern to match.</param>
    /// <param name="template">The result template.</param>
    /// <returns>Match expression.</returns>
    public static Expression Match(Atom space, Atom pattern, Atom template)
        => Atom.Expr(Atom.Sym("match"), space, pattern, template);

    /// <summary>
    /// Creates an import expression: (import! module).
    /// </summary>
    /// <param name="module">The module to import.</param>
    /// <returns>Import expression.</returns>
    public static Expression Import(Atom module)
        => Atom.Expr(Atom.Sym("import!"), module);

    /// <summary>
    /// Creates an add-atom expression: (add-atom space atom).
    /// </summary>
    /// <param name="space">The space to add to.</param>
    /// <param name="atom">The atom to add.</param>
    /// <returns>Add-atom expression.</returns>
    public static Expression AddAtom(Atom space, Atom atom)
        => Atom.Expr(Atom.Sym("add-atom"), space, atom);

    /// <summary>
    /// Creates a remove-atom expression: (remove-atom space atom).
    /// </summary>
    /// <param name="space">The space to remove from.</param>
    /// <param name="atom">The atom to remove.</param>
    /// <returns>Remove-atom expression.</returns>
    public static Expression RemoveAtom(Atom space, Atom atom)
        => Atom.Expr(Atom.Sym("remove-atom"), space, atom);

    /// <summary>
    /// Creates a collapse expression for executing non-determinism.
    /// </summary>
    /// <param name="expr">The expression to collapse.</param>
    /// <returns>Collapse expression.</returns>
    public static Expression Collapse(Atom expr)
        => Atom.Expr(Atom.Sym("collapse"), expr);

    /// <summary>
    /// Creates a superpose expression for non-deterministic choice.
    /// </summary>
    /// <param name="alternatives">The alternative atoms.</param>
    /// <returns>Superpose expression.</returns>
    public static Expression Superpose(params Atom[] alternatives)
        => Atom.Expr(new[] { Atom.Sym("superpose") }.Concat(alternatives).ToArray());

    #endregion

    #region Meta-Level Operations

    /// <summary>
    /// Creates an eval expression for meta-evaluation.
    /// </summary>
    /// <param name="expr">The expression to evaluate.</param>
    /// <returns>Eval expression.</returns>
    public static Expression Eval(Atom expr)
        => Atom.Expr(Atom.Sym("eval"), expr);

    /// <summary>
    /// Creates a quote expression to prevent evaluation.
    /// </summary>
    /// <param name="expr">The expression to quote.</param>
    /// <returns>Quote expression.</returns>
    public static Expression Quote(Atom expr)
        => Atom.Expr(Atom.Sym("quote"), expr);

    /// <summary>
    /// Creates an unquote expression for selective evaluation.
    /// </summary>
    /// <param name="expr">The expression to unquote.</param>
    /// <returns>Unquote expression.</returns>
    public static Expression Unquote(Atom expr)
        => Atom.Expr(Atom.Sym("unquote"), expr);

    /// <summary>
    /// Creates a get-type expression.
    /// </summary>
    /// <param name="atom">The atom to get the type of.</param>
    /// <returns>Get-type expression.</returns>
    public static Expression GetType(Atom atom)
        => Atom.Expr(Atom.Sym("get-type"), atom);

    #endregion

    #region Logic Operations

    /// <summary>
    /// Creates an implies expression: (implies condition conclusion).
    /// </summary>
    /// <param name="condition">The antecedent.</param>
    /// <param name="conclusion">The consequent.</param>
    /// <returns>Implication expression.</returns>
    public static Expression Implies(Atom condition, Atom conclusion)
        => Atom.Expr(Atom.Sym("implies"), condition, conclusion);

    /// <summary>
    /// Creates an and expression.
    /// </summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>And expression.</returns>
    public static Expression And(Atom left, Atom right)
        => Atom.Expr(Atom.Sym("and"), left, right);

    /// <summary>
    /// Creates an or expression.
    /// </summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>Or expression.</returns>
    public static Expression Or(Atom left, Atom right)
        => Atom.Expr(Atom.Sym("or"), left, right);

    /// <summary>
    /// Creates a not expression.
    /// </summary>
    /// <param name="operand">The operand to negate.</param>
    /// <returns>Not expression.</returns>
    public static Expression Not(Atom operand)
        => Atom.Expr(Atom.Sym("not"), operand);

    /// <summary>
    /// Creates a let expression for local binding.
    /// </summary>
    /// <param name="variable">The variable to bind.</param>
    /// <param name="value">The value to bind.</param>
    /// <param name="body">The body where binding is in scope.</param>
    /// <returns>Let expression.</returns>
    public static Expression Let(Variable variable, Atom value, Atom body)
        => Atom.Expr(Atom.Sym("let"), variable, value, body);

    #endregion

    #region Self-Reference Symbols

    /// <summary>
    /// Reference to the current atom space (and-self).
    /// </summary>
    public static Symbol Self => Atom.Sym("&self");

    /// <summary>
    /// Reference to the global knowledge base (and-kb).
    /// </summary>
    public static Symbol KnowledgeBase => Atom.Sym("&kb");

    /// <summary>
    /// Empty result indicator.
    /// </summary>
    public static Symbol Empty => Atom.Sym("Empty");

    /// <summary>
    /// True value.
    /// </summary>
    public static Symbol True => Atom.Sym("True");

    /// <summary>
    /// False value.
    /// </summary>
    public static Symbol False => Atom.Sym("False");

    #endregion
}

/// <summary>
/// Extension methods for creating MeTTa-compliant atoms.
/// </summary>
public static class MeTTaAtomExtensions
{
    /// <summary>
    /// Annotates an atom with a type.
    /// </summary>
    /// <param name="atom">The atom to annotate.</param>
    /// <param name="type">The type.</param>
    /// <returns>Type annotation expression.</returns>
    public static Expression WithType(this Atom atom, Atom type)
        => MeTTaSpec.TypeOf(atom, type);

    /// <summary>
    /// Creates a function type from this atom as input.
    /// </summary>
    /// <param name="inputType">The input type.</param>
    /// <param name="outputType">The output type.</param>
    /// <returns>Function type expression.</returns>
    public static Expression To(this Atom inputType, Atom outputType)
        => MeTTaSpec.FunctionType(inputType, outputType);

    /// <summary>
    /// Quotes an atom to prevent evaluation.
    /// </summary>
    /// <param name="atom">The atom to quote.</param>
    /// <returns>Quoted atom.</returns>
    public static Expression Quoted(this Atom atom)
        => MeTTaSpec.Quote(atom);

    /// <summary>
    /// Creates an implication from this atom as the condition.
    /// </summary>
    /// <param name="condition">The condition.</param>
    /// <param name="conclusion">The conclusion.</param>
    /// <returns>Implication expression.</returns>
    public static Expression ImpliesThat(this Atom condition, Atom conclusion)
        => MeTTaSpec.Implies(condition, conclusion);

    /// <summary>
    /// Converts a Form to a MeTTa atom representing its state.
    /// </summary>
    /// <param name="form">The form to convert.</param>
    /// <returns>MeTTa atom representing the form.</returns>
    public static Atom ToMeTTa(this Form form)
        => form.Match(
            onMark: () => Atom.Sym("Mark"),
            onVoid: () => Atom.Sym("Void"),
            onImaginary: () => Atom.Sym("Imaginary"));

    /// <summary>
    /// Converts a MeTTa atom to a Form.
    /// </summary>
    /// <param name="atom">The atom to convert.</param>
    /// <returns>Option containing the form if conversion is valid.</returns>
    public static Option<Form> ToForm(this Atom atom)
    {
        if (atom is Symbol sym)
        {
            return sym.Name switch
            {
                "Mark" or "True" or "⌐" => Option<Form>.Some(Form.Mark),
                "Void" or "False" or "∅" => Option<Form>.Some(Form.Void),
                "Imaginary" or "i" => Option<Form>.Some(Form.Imaginary),
                _ => Option<Form>.None()
            };
        }

        return Option<Form>.None();
    }
}
