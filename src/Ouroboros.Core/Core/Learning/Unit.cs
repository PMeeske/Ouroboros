// <copyright file="Unit.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Learning;

/// <summary>
/// Represents a unit type (void equivalent in functional programming).
/// Used to indicate successful completion without a meaningful return value.
/// </summary>
public readonly struct Unit : IEquatable<Unit>
{
    /// <summary>
    /// The singleton instance of Unit.
    /// </summary>
    public static readonly Unit Value = default;

    /// <summary>
    /// Determines equality with another Unit instance.
    /// All Unit instances are equal.
    /// </summary>
    /// <param name="other">The other Unit instance.</param>
    /// <returns>Always true.</returns>
    public bool Equals(Unit other) => true;

    /// <summary>
    /// Determines equality with an object.
    /// </summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>True if obj is a Unit instance.</returns>
    public override bool Equals(object? obj) => obj is Unit;

    /// <summary>
    /// Gets the hash code for Unit.
    /// </summary>
    /// <returns>Always returns 0.</returns>
    public override int GetHashCode() => 0;

    /// <summary>
    /// Returns a string representation of Unit.
    /// </summary>
    /// <returns>The string "()".</returns>
    public override string ToString() => "()";

    /// <summary>
    /// Equality operator.
    /// </summary>
    public static bool operator ==(Unit left, Unit right) => true;

    /// <summary>
    /// Inequality operator.
    /// </summary>
    public static bool operator !=(Unit left, Unit right) => false;
}
