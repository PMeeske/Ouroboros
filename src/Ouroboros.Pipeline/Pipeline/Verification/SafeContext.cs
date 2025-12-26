// <copyright file="SafeContext.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Verification;

/// <summary>
/// Represents the security context for pipeline execution.
/// Used by MeTTa symbolic guard rails to enforce access control.
/// </summary>
public enum SafeContext
{
    /// <summary>
    /// Read-only context that blocks all write operations.
    /// </summary>
    ReadOnly,

    /// <summary>
    /// Full access context that allows all operations.
    /// </summary>
    FullAccess,
}

/// <summary>
/// Extension methods for <see cref="SafeContext"/>.
/// </summary>
public static class SafeContextExtensions
{
    /// <summary>
    /// Converts the context to a MeTTa atom representation.
    /// </summary>
    /// <param name="context">The context to convert.</param>
    /// <returns>The MeTTa atom string.</returns>
    public static string ToMeTTaAtom(this SafeContext context) => context switch
    {
        SafeContext.ReadOnly => "ReadOnly",
        SafeContext.FullAccess => "FullAccess",
        _ => throw new ArgumentOutOfRangeException(nameof(context), context, "Unknown context type"),
    };
}
