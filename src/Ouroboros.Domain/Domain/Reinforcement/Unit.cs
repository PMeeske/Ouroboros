// <copyright file="Unit.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Reinforcement;

/// <summary>
/// Unit type representing a void result in functional programming.
/// Used for operations that succeed but don't return a meaningful value.
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
    public bool Equals(Unit other) => true;

    /// <summary>
    /// Determines equality with an object.
    /// </summary>
    public override bool Equals(object? obj) => obj is Unit;

    /// <summary>
    /// Gets the hash code.
    /// </summary>
    public override int GetHashCode() => 0;

    /// <summary>
    /// String representation.
    /// </summary>
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
