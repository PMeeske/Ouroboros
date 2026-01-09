// <copyright file="Substitution.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Collections.Immutable;

namespace Ouroboros.Core.Hyperon;

/// <summary>
/// Represents a binding of variables to atoms (substitution).
/// Used to track variable assignments during unification.
/// Immutable and composable.
/// </summary>
public sealed record Substitution
{
    /// <summary>
    /// Gets the empty substitution with no bindings.
    /// </summary>
    public static Substitution Empty { get; } = new(ImmutableDictionary<string, Atom>.Empty);

    /// <summary>
    /// Gets the variable bindings (variable name to atom mapping).
    /// </summary>
    public ImmutableDictionary<string, Atom> Bindings { get; }

    private Substitution(ImmutableDictionary<string, Atom> bindings)
    {
        Bindings = bindings;
    }

    /// <summary>
    /// Creates a new substitution with a single binding.
    /// </summary>
    /// <param name="varName">The variable name (without '$').</param>
    /// <param name="value">The atom to bind.</param>
    /// <returns>A new substitution with the binding.</returns>
    public static Substitution Of(string varName, Atom value) =>
        Empty.Bind(varName, value);

    /// <summary>
    /// Tries to look up the binding for a variable.
    /// </summary>
    /// <param name="varName">The variable name (without '$').</param>
    /// <returns>The bound atom wrapped in Option, or None if not bound.</returns>
    public Option<Atom> Lookup(string varName) =>
        Bindings.TryGetValue(varName, out var value)
            ? Option<Atom>.Some(value)
            : Option<Atom>.None();

    /// <summary>
    /// Creates a new substitution with an additional binding.
    /// If the variable is already bound, returns the same substitution if the value matches,
    /// otherwise returns a failed substitution (represented as null in composition).
    /// </summary>
    /// <param name="varName">The variable name (without '$').</param>
    /// <param name="value">The atom to bind.</param>
    /// <returns>A new substitution with the binding added.</returns>
    public Substitution Bind(string varName, Atom value) =>
        new(Bindings.SetItem(varName, value));

    /// <summary>
    /// Composes this substitution with another, returning a substitution
    /// containing all bindings from both.
    /// </summary>
    /// <param name="other">The other substitution.</param>
    /// <returns>The composed substitution, or null if compositions conflict.</returns>
    public Substitution? Compose(Substitution other)
    {
        var result = Bindings;
        foreach (var (varName, value) in other.Bindings)
        {
            if (result.TryGetValue(varName, out var existing))
            {
                if (!existing.Equals(value))
                {
                    return null; // Conflict
                }
            }
            else
            {
                result = result.Add(varName, value);
            }
        }

        return new Substitution(result);
    }

    /// <summary>
    /// Applies this substitution to an atom, replacing all bound variables with their values.
    /// </summary>
    /// <param name="atom">The atom to apply the substitution to.</param>
    /// <returns>The atom with variables replaced by their bound values.</returns>
    public Atom Apply(Atom atom)
    {
        return atom switch
        {
            Variable v => Lookup(v.Name).HasValue ? Lookup(v.Name).Value! : atom,
            Expression e => Atom.Expr(e.Children.Select(Apply)),
            _ => atom
        };
    }

    /// <summary>
    /// Checks if this substitution is empty (no bindings).
    /// </summary>
    public bool IsEmpty => Bindings.IsEmpty;

    /// <summary>
    /// Gets the count of bindings in this substitution.
    /// </summary>
    public int Count => Bindings.Count;

    /// <inheritdoc/>
    public override string ToString()
    {
        if (IsEmpty)
        {
            return "{}";
        }

        var bindings = Bindings.Select(kv => $"${kv.Key} -> {kv.Value.ToSExpr()}");
        return $"{{{string.Join(", ", bindings)}}}";
    }
}
