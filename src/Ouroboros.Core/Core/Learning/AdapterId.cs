// <copyright file="AdapterId.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Core.Learning;

/// <summary>
/// Unique identifier for a LoRA/PEFT adapter.
/// </summary>
/// <param name="Value">The unique identifier value.</param>
public sealed record AdapterId(Guid Value)
{
    /// <summary>
    /// Creates a new adapter ID with a random GUID.
    /// </summary>
    /// <returns>A new adapter ID.</returns>
    public static AdapterId NewId() => new(Guid.NewGuid());

    /// <summary>
    /// Creates an adapter ID from a string representation.
    /// </summary>
    /// <param name="value">The string representation of the GUID.</param>
    /// <returns>An adapter ID if parsing succeeds, otherwise None.</returns>
    public static Monads.Option<AdapterId> FromString(string value)
    {
        if (Guid.TryParse(value, out var guid))
        {
            return Monads.Option<AdapterId>.Some(new AdapterId(guid));
        }

        return Monads.Option<AdapterId>.None();
    }

    /// <summary>
    /// Returns the string representation of the adapter ID.
    /// </summary>
    /// <returns>The GUID as a string.</returns>
    public override string ToString() => this.Value.ToString();
}
