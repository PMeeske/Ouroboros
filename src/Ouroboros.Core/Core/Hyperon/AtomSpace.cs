// <copyright file="AtomSpace.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using System.Collections.Immutable;

namespace Ouroboros.Core.Hyperon;

/// <summary>
/// Interface for an atom space - a storage for atoms supporting queries with unification.
/// </summary>
public interface IAtomSpace
{
    /// <summary>
    /// Adds an atom to the space.
    /// </summary>
    /// <param name="atom">The atom to add.</param>
    /// <returns>True if the atom was newly added, false if it already existed.</returns>
    bool Add(Atom atom);

    /// <summary>
    /// Adds multiple atoms to the space.
    /// </summary>
    /// <param name="atoms">The atoms to add.</param>
    /// <returns>The number of newly added atoms.</returns>
    int AddRange(IEnumerable<Atom> atoms);

    /// <summary>
    /// Removes an atom from the space.
    /// </summary>
    /// <param name="atom">The atom to remove.</param>
    /// <returns>True if the atom was removed, false if it didn't exist.</returns>
    bool Remove(Atom atom);

    /// <summary>
    /// Checks if the space contains the given atom (exact match).
    /// </summary>
    /// <param name="atom">The atom to check.</param>
    /// <returns>True if the atom exists in the space.</returns>
    bool Contains(Atom atom);

    /// <summary>
    /// Gets all atoms in the space.
    /// </summary>
    /// <returns>Enumerable of all atoms.</returns>
    IEnumerable<Atom> All();

    /// <summary>
    /// Queries the space for atoms matching the given pattern.
    /// Returns pairs of (matching atom, substitution that made the match).
    /// </summary>
    /// <param name="pattern">The pattern atom (may contain variables).</param>
    /// <returns>Enumerable of (matched atom, substitution) pairs.</returns>
    IEnumerable<(Atom Atom, Substitution Bindings)> Query(Atom pattern);

    /// <summary>
    /// Gets the count of atoms in the space.
    /// </summary>
    int Count { get; }
}

/// <summary>
/// Thread-safe in-memory atom space implementation with indexing for efficient queries.
/// Supports pattern matching with unification.
/// </summary>
public sealed class AtomSpace : IAtomSpace
{
    private readonly ConcurrentDictionary<Atom, byte> atoms = new();

    // Index for expressions by their head symbol for faster lookups
    private readonly ConcurrentDictionary<string, ConcurrentBag<Expression>> symbolIndex = new();

    /// <inheritdoc/>
    public int Count => atoms.Count;

    /// <inheritdoc/>
    public bool Add(Atom atom)
    {
        if (!atoms.TryAdd(atom, 0))
        {
            return false;
        }

        // Index expressions by their head symbol
        if (atom is Expression expr && expr.Children.Count > 0 && expr.Children[0] is Symbol headSym)
        {
            var bag = symbolIndex.GetOrAdd(headSym.Name, _ => new ConcurrentBag<Expression>());
            bag.Add(expr);
        }

        return true;
    }

    /// <inheritdoc/>
    public int AddRange(IEnumerable<Atom> atomsToAdd)
    {
        var count = 0;
        foreach (var atom in atomsToAdd)
        {
            if (Add(atom))
            {
                count++;
            }
        }

        return count;
    }

    /// <inheritdoc/>
    public bool Remove(Atom atom)
    {
        // Note: We don't remove from the index for simplicity in concurrent scenarios
        // A more sophisticated implementation could use reference counting
        return atoms.TryRemove(atom, out _);
    }

    /// <inheritdoc/>
    public bool Contains(Atom atom) => atoms.ContainsKey(atom);

    /// <inheritdoc/>
    public IEnumerable<Atom> All() => atoms.Keys;

    /// <inheritdoc/>
    public IEnumerable<(Atom Atom, Substitution Bindings)> Query(Atom pattern)
    {
        // If pattern has no variables, just check for existence
        if (!pattern.ContainsVariables())
        {
            if (Contains(pattern))
            {
                yield return (pattern, Substitution.Empty);
            }

            yield break;
        }

        // Use index if pattern is an expression with a symbol head
        IEnumerable<Atom> candidates;
        if (pattern is Expression patternExpr && patternExpr.Children.Count > 0 && patternExpr.Children[0] is Symbol headSym)
        {
            // Look up indexed expressions with the same head
            candidates = symbolIndex.TryGetValue(headSym.Name, out var indexed)
                ? indexed.Cast<Atom>()
                : All();
        }
        else
        {
            candidates = All();
        }

        // Try to unify pattern with each candidate
        foreach (var candidate in candidates)
        {
            var unifyResult = Unifier.Unify(pattern, candidate);
            if (unifyResult is not null)
            {
                yield return (candidate, unifyResult);
            }
        }
    }

    /// <summary>
    /// Clears all atoms from the space.
    /// </summary>
    public void Clear()
    {
        atoms.Clear();
        symbolIndex.Clear();
    }

    /// <summary>
    /// Creates a snapshot of the current atom space as an immutable list.
    /// </summary>
    /// <returns>An immutable list of all atoms.</returns>
    public ImmutableList<Atom> Snapshot() => All().ToImmutableList();
}
