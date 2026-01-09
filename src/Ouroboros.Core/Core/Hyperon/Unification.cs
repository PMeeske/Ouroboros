// <copyright file="Unification.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Hyperon;

/// <summary>
/// Provides unification operations for atoms.
/// Unification finds a substitution that makes two atoms identical.
/// </summary>
public static class Unifier
{
    /// <summary>
    /// Attempts to unify two atoms, finding a substitution that makes them equal.
    /// </summary>
    /// <param name="pattern">The pattern atom (typically contains variables).</param>
    /// <param name="target">The target atom to match against.</param>
    /// <param name="initial">Optional initial substitution to extend.</param>
    /// <returns>A substitution if unification succeeds, null otherwise.</returns>
    public static Substitution? Unify(Atom pattern, Atom target, Substitution? initial = null)
    {
        var subst = initial ?? Substitution.Empty;
        return UnifyInternal(pattern, target, subst);
    }

    private static Substitution? UnifyInternal(Atom pattern, Atom target, Substitution subst)
    {
        // Apply current substitution to resolve any already-bound variables
        pattern = subst.Apply(pattern);
        target = subst.Apply(target);

        // If pattern and target are now equal, we're done
        if (pattern.Equals(target))
        {
            return subst;
        }

        // Variable in pattern: bind to target
        if (pattern is Variable pVar)
        {
            return BindVariable(pVar, target, subst);
        }

        // Variable in target: bind to pattern
        if (target is Variable tVar)
        {
            return BindVariable(tVar, pattern, subst);
        }

        // Both are expressions: unify structurally
        if (pattern is Expression pExpr && target is Expression tExpr)
        {
            return UnifyExpressions(pExpr, tExpr, subst);
        }

        // Symbols must be equal (already checked above)
        // No match found
        return null;
    }

    private static Substitution? BindVariable(Variable variable, Atom value, Substitution subst)
    {
        // Occurs check: prevent infinite recursion (e.g., $x = f($x))
        if (OccursIn(variable, value))
        {
            return null;
        }

        // Check existing binding
        var existing = subst.Lookup(variable.Name);
        if (existing.HasValue)
        {
            // Variable already bound - check if it unifies with the new value
            return UnifyInternal(existing.Value!, value, subst);
        }

        // New binding
        return subst.Bind(variable.Name, value);
    }

    private static bool OccursIn(Variable variable, Atom atom)
    {
        return atom switch
        {
            Variable v => v.Name == variable.Name,
            Expression e => e.Children.Any(child => OccursIn(variable, child)),
            _ => false
        };
    }

    private static Substitution? UnifyExpressions(Expression pattern, Expression target, Substitution subst)
    {
        if (pattern.Children.Count != target.Children.Count)
        {
            return null;
        }

        var current = subst;
        for (var i = 0; i < pattern.Children.Count; i++)
        {
            var result = UnifyInternal(pattern.Children[i], target.Children[i], current);
            if (result is null)
            {
                return null;
            }

            current = result;
        }

        return current;
    }

    /// <summary>
    /// Generates all possible unifications of a pattern against a collection of atoms.
    /// </summary>
    /// <param name="pattern">The pattern to match.</param>
    /// <param name="atoms">The atoms to match against.</param>
    /// <returns>Enumerable of successful substitutions.</returns>
    public static IEnumerable<Substitution> UnifyAll(Atom pattern, IEnumerable<Atom> atoms)
    {
        foreach (var atom in atoms)
        {
            var result = Unify(pattern, atom);
            if (result is not null)
            {
                yield return result;
            }
        }
    }

    /// <summary>
    /// Checks if two atoms can be unified.
    /// </summary>
    /// <param name="a">First atom.</param>
    /// <param name="b">Second atom.</param>
    /// <returns>True if the atoms can be unified.</returns>
    public static bool CanUnify(Atom a, Atom b) => Unify(a, b) is not null;
}
