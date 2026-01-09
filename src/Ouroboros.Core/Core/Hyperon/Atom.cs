// <copyright file="Atom.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text;

namespace Ouroboros.Core.Hyperon;

/// <summary>
/// Abstract base record for all atom types in the MeTTa-style knowledge representation.
/// Atoms are the fundamental building blocks: symbols, variables, and expressions (S-expressions).
/// Immutable by design, with value-based equality inherited from record semantics.
/// </summary>
public abstract record Atom
{
    /// <summary>
    /// Returns the S-expression string representation of this atom.
    /// </summary>
    /// <returns>S-expression formatted string.</returns>
    public abstract string ToSExpr();

    /// <inheritdoc/>
    public sealed override string ToString() => ToSExpr();

    /// <summary>
    /// Creates a Symbol atom with the given name.
    /// </summary>
    /// <param name="name">The symbol name.</param>
    /// <returns>A new Symbol atom.</returns>
    public static Symbol Sym(string name) => new(name);

    /// <summary>
    /// Creates a Variable atom with the given name (without the '$' prefix).
    /// </summary>
    /// <param name="name">The variable name (without '$' prefix).</param>
    /// <returns>A new Variable atom.</returns>
    public static Variable Var(string name) => new(name);

    /// <summary>
    /// Creates an Expression atom from the given children.
    /// </summary>
    /// <param name="children">The child atoms.</param>
    /// <returns>A new Expression atom.</returns>
    public static Expression Expr(params Atom[] children) => new(children.ToImmutableList());

    /// <summary>
    /// Creates an Expression atom from the given children.
    /// </summary>
    /// <param name="children">The child atoms.</param>
    /// <returns>A new Expression atom.</returns>
    public static Expression Expr(ImmutableList<Atom> children) => new(children);

    /// <summary>
    /// Creates an Expression atom from an enumerable of children.
    /// </summary>
    /// <param name="children">The child atoms.</param>
    /// <returns>A new Expression atom.</returns>
    public static Expression Expr(IEnumerable<Atom> children) => new(children.ToImmutableList());

    /// <summary>
    /// Determines if this atom contains any variables.
    /// </summary>
    /// <returns>True if the atom or any nested child contains variables.</returns>
    public virtual bool ContainsVariables() => false;
}

/// <summary>
/// Represents a symbol (named constant) in the atom space.
/// Symbols are ground terms with no unbound variables.
/// Examples: Human, Mortal, Socrates, implies.
/// </summary>
/// <param name="Name">The symbol's name.</param>
public sealed record Symbol(string Name) : Atom
{
    /// <inheritdoc/>
    public override string ToSExpr() => Name;

    /// <inheritdoc/>
    public override bool ContainsVariables() => false;
}

/// <summary>
/// Represents a variable (placeholder for pattern matching) in the atom space.
/// Variables are prefixed with '$' in MeTTa syntax.
/// Examples: $x, $y, $person.
/// </summary>
/// <param name="Name">The variable's name (without '$' prefix).</param>
public sealed record Variable(string Name) : Atom
{
    /// <inheritdoc/>
    public override string ToSExpr() => $"${Name}";

    /// <inheritdoc/>
    public override bool ContainsVariables() => true;
}

/// <summary>
/// Represents an S-expression (list of atoms) in the atom space.
/// Expressions can contain nested expressions, symbols, and variables.
/// Examples: (Human Socrates), (implies (Human $x) (Mortal $x)).
/// </summary>
/// <param name="Children">The ordered list of child atoms.</param>
public sealed record Expression(ImmutableList<Atom> Children) : Atom
{
    /// <inheritdoc/>
    public override string ToSExpr()
    {
        if (Children.Count == 0)
        {
            return "()";
        }

        var sb = new StringBuilder();
        sb.Append('(');
        for (var i = 0; i < Children.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(' ');
            }

            sb.Append(Children[i].ToSExpr());
        }

        sb.Append(')');
        return sb.ToString();
    }

    /// <inheritdoc/>
    public override bool ContainsVariables() => Children.Any(c => c.ContainsVariables());

    /// <summary>
    /// Gets the head (first element) of the expression, if any.
    /// </summary>
    /// <returns>The head atom, or None if the expression is empty.</returns>
    public Option<Atom> Head() => Children.Count > 0
        ? Option<Atom>.Some(Children[0])
        : Option<Atom>.None();

    /// <summary>
    /// Gets the tail (all elements except the first) of the expression.
    /// </summary>
    /// <returns>An immutable list of the tail atoms.</returns>
    public ImmutableList<Atom> Tail() => Children.Count > 0
        ? Children.RemoveAt(0)
        : ImmutableList<Atom>.Empty;

    /// <summary>
    /// Determines equality for expressions based on structural equality of children.
    /// Required for proper value-based equality with ImmutableList.
    /// </summary>
    /// <param name="other">The other expression to compare.</param>
    /// <returns>True if the expressions are structurally equal.</returns>
    public bool Equals(Expression? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (Children.Count != other.Children.Count)
        {
            return false;
        }

        for (var i = 0; i < Children.Count; i++)
        {
            if (!Children[i].Equals(other.Children[i]))
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var child in Children)
        {
            hash.Add(child);
        }

        return hash.ToHashCode();
    }
}
