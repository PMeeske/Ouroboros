// <copyright file="Unit.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Synthesis;

/// <summary>
/// Represents a unit type (void equivalent) for functional programming.
/// Used when an operation completes successfully but has no meaningful return value.
/// </summary>
public readonly struct Unit : IEquatable<Unit>
{
    /// <summary>
    /// Gets the singleton instance of Unit.
    /// </summary>
    public static Unit Value { get; } = default;

    /// <summary>
    /// Determines whether two Unit instances are equal (always true).
    /// </summary>
    /// <param name="other">The other Unit instance.</param>
    /// <returns>Always returns true.</returns>
    public bool Equals(Unit other) => true;

    /// <summary>
    /// Determines whether this instance equals another object.
    /// </summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>True if obj is a Unit instance.</returns>
    public override bool Equals(object? obj) => obj is Unit;

    /// <summary>
    /// Returns a hash code for this instance.
    /// </summary>
    /// <returns>Always returns 0.</returns>
    public override int GetHashCode() => 0;

    /// <summary>
    /// Returns a string representation of Unit.
    /// </summary>
    /// <returns>The string "()".</returns>
    public override string ToString() => "()";

    /// <summary>
    /// Equality operator for Unit.
    /// </summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>Always returns true.</returns>
    public static bool operator ==(Unit left, Unit right) => true;

    /// <summary>
    /// Inequality operator for Unit.
    /// </summary>
    /// <param name="left">Left operand.</param>
    /// <param name="right">Right operand.</param>
    /// <returns>Always returns false.</returns>
    public static bool operator !=(Unit left, Unit right) => false;
}
