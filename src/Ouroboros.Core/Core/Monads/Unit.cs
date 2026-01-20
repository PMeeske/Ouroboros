// <copyright file="Unit.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Monads;

/// <summary>
/// Unit type representing a void result in functional programming.
/// Used for operations that succeed but don't return a meaningful value.
/// This is the standard functional programming "unit" type.
/// </summary>
public readonly struct Unit : IEquatable<Unit>
{
    /// <summary>
    /// Gets the singleton instance of Unit.
    /// </summary>
    public static Unit Value => default;

    /// <summary>
    /// Determines equality with another Unit.
    /// </summary>
    /// <param name="other">The other unit to compare.</param>
    /// <returns>Always true since all Unit values are equal.</returns>
    public bool Equals(Unit other) => true;

    /// <summary>
    /// Determines equality with an object.
    /// </summary>
    /// <param name="obj">The object to compare.</param>
    /// <returns>True if the object is a Unit.</returns>
    public override bool Equals(object? obj) => obj is Unit;

    /// <summary>
    /// Gets the hash code.
    /// </summary>
    /// <returns>Always returns 0 since all Unit values are equal.</returns>
    public override int GetHashCode() => 0;

    /// <summary>
    /// Gets the string representation.
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
