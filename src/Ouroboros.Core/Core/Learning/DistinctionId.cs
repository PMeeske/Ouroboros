// <copyright file="DistinctionId.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Learning;

/// <summary>
/// Unique identifier for a distinction in the Distinction Learning framework.
/// </summary>
/// <param name="Value">The unique identifier value.</param>
public sealed record DistinctionId(Guid Value)
{
    /// <summary>
    /// Creates a new distinction ID with a random GUID.
    /// </summary>
    /// <returns>A new distinction ID.</returns>
    public static DistinctionId NewId() => new(Guid.NewGuid());

    /// <summary>
    /// Creates a distinction ID from a string representation.
    /// </summary>
    /// <param name="value">The string representation of the GUID.</param>
    /// <returns>A distinction ID if parsing succeeds, otherwise None.</returns>
    public static Monads.Option<DistinctionId> FromString(string value)
    {
        if (Guid.TryParse(value, out var guid))
        {
            return Monads.Option<DistinctionId>.Some(new DistinctionId(guid));
        }

        return Monads.Option<DistinctionId>.None();
    }

    /// <summary>
    /// Returns the string representation of the distinction ID.
    /// </summary>
    /// <returns>The GUID as a string.</returns>
    public override string ToString() => this.Value.ToString();
}
