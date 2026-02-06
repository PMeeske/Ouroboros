// <copyright file="KleisliPropertyTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Core.PropertyBased;

using FsCheck.Xunit;
using Ouroboros.Core.Kleisli;
using Ouroboros.Core.Monads;

/// <summary>
/// Property-based tests for Kleisli arrow composition laws using FsCheck.
/// Verifies that Kleisli composition satisfies category laws for arbitrary async arrows.
/// </summary>
[Trait("Category", "Property")]
public class KleisliPropertyTests
{
    /// <summary>
    /// Verifies Associativity of KleisliResult composition: (f >=> g) >=> h ≡ f >=> (g >=> h)
    /// Tests with arrows that always succeed.
    /// </summary>
    /// <param name="input">Arbitrary integer input.</param>
    /// <returns>True if associativity holds.</returns>
    [Property(MaxTest = 1000)]
    public async Task<bool> KleisliResult_Associativity_HoldsForAllInts(int input)
    {
        // (f >=> g) >=> h ≡ f >=> (g >=> h)
        KleisliResult<int, int, string> f = x =>
            Task.FromResult(Result<int, string>.Success(x * 2));

        KleisliResult<int, int, string> g = x =>
            Task.FromResult(Result<int, string>.Success(x + 10));

        KleisliResult<int, int, string> h = x =>
            Task.FromResult(Result<int, string>.Success(x - 5));

        // Left: (f >=> g) >=> h
        var leftComposed = await ComposeKleisliResult(
            ComposeKleisliResult(f, g),
            h)(input);

        // Right: f >=> (g >=> h)
        var rightComposed = await ComposeKleisliResult(
            f,
            ComposeKleisliResult(g, h))(input);

        return (leftComposed.IsSuccess == rightComposed.IsSuccess &&
                (leftComposed.IsFailure || leftComposed.Value == rightComposed.Value));
    }

    /// <summary>
    /// Verifies Associativity with arrows that may fail based on input.
    /// Tests that associativity holds even when intermediate arrows fail.
    /// </summary>
    /// <param name="input">Arbitrary integer input.</param>
    /// <returns>True if associativity holds with failure cases.</returns>
    [Property(MaxTest = 1000)]
    public async Task<bool> KleisliResult_Associativity_WithFailures_HoldsForAllInts(int input)
    {
        // (f >=> g) >=> h ≡ f >=> (g >=> h)
        KleisliResult<int, int, string> f = x =>
            Task.FromResult(x >= 0
                ? Result<int, string>.Success(x * 2)
                : Result<int, string>.Failure("negative input"));

        KleisliResult<int, int, string> g = x =>
            Task.FromResult(x < 100
                ? Result<int, string>.Success(x + 10)
                : Result<int, string>.Failure("too large"));

        KleisliResult<int, int, string> h = x =>
            Task.FromResult(x % 2 == 0
                ? Result<int, string>.Success(x / 2)
                : Result<int, string>.Failure("odd number"));

        // Left: (f >=> g) >=> h
        var leftComposed = await ComposeKleisliResult(
            ComposeKleisliResult(f, g),
            h)(input);

        // Right: f >=> (g >=> h)
        var rightComposed = await ComposeKleisliResult(
            f,
            ComposeKleisliResult(g, h))(input);

        return (leftComposed.IsSuccess == rightComposed.IsSuccess &&
                (leftComposed.IsFailure || leftComposed.Value == rightComposed.Value) &&
                (leftComposed.IsSuccess || leftComposed.Error == rightComposed.Error));
    }

    /// <summary>
    /// Verifies Left Identity for Kleisli composition: id >=> f ≡ f
    /// The identity arrow should not affect composition.
    /// </summary>
    /// <param name="input">Arbitrary integer input.</param>
    /// <returns>True if left identity holds.</returns>
    [Property(MaxTest = 1000)]
    public async Task<bool> KleisliResult_LeftIdentity_HoldsForAllInts(int input)
    {
        // id >=> f ≡ f
        KleisliResult<int, int, string> identity = x =>
            Task.FromResult(Result<int, string>.Success(x));

        KleisliResult<int, int, string> f = x =>
            Task.FromResult(Result<int, string>.Success(x * 3 + 7));

        var composed = await ComposeKleisliResult(identity, f)(input);
        var direct = await f(input);

        return (composed.IsSuccess == direct.IsSuccess &&
                composed.Value == direct.Value);
    }

    /// <summary>
    /// Verifies Right Identity for Kleisli composition: f >=> id ≡ f
    /// The identity arrow should not affect composition.
    /// </summary>
    /// <param name="input">Arbitrary integer input.</param>
    /// <returns>True if right identity holds.</returns>
    [Property(MaxTest = 1000)]
    public async Task<bool> KleisliResult_RightIdentity_HoldsForAllInts(int input)
    {
        // f >=> id ≡ f
        KleisliResult<int, int, string> f = x =>
            Task.FromResult(Result<int, string>.Success(x * 3 + 7));

        KleisliResult<int, int, string> identity = x =>
            Task.FromResult(Result<int, string>.Success(x));

        var composed = await ComposeKleisliResult(f, identity)(input);
        var direct = await f(input);

        return (composed.IsSuccess == direct.IsSuccess &&
                composed.Value == direct.Value);
    }

    /// <summary>
    /// Verifies Left Identity with arrows that may fail.
    /// Tests that identity composition works correctly with failure cases.
    /// </summary>
    /// <param name="input">Arbitrary integer input.</param>
    /// <returns>True if left identity holds with failures.</returns>
    [Property(MaxTest = 1000)]
    public async Task<bool> KleisliResult_LeftIdentity_WithFailures_HoldsForAllInts(int input)
    {
        // id >=> f ≡ f
        KleisliResult<int, int, string> identity = x =>
            Task.FromResult(Result<int, string>.Success(x));

        KleisliResult<int, int, string> f = x =>
            Task.FromResult(x >= 0
                ? Result<int, string>.Success(x * 2)
                : Result<int, string>.Failure("negative"));

        var composed = await ComposeKleisliResult(identity, f)(input);
        var direct = await f(input);

        return (composed.IsSuccess == direct.IsSuccess &&
                (composed.IsFailure || composed.Value == direct.Value) &&
                (composed.IsSuccess || composed.Error == direct.Error));
    }

    /// <summary>
    /// Verifies Right Identity with arrows that may fail.
    /// Tests that identity composition works correctly with failure cases.
    /// </summary>
    /// <param name="input">Arbitrary integer input.</param>
    /// <returns>True if right identity holds with failures.</returns>
    [Property(MaxTest = 1000)]
    public async Task<bool> KleisliResult_RightIdentity_WithFailures_HoldsForAllInts(int input)
    {
        // f >=> id ≡ f
        KleisliResult<int, int, string> f = x =>
            Task.FromResult(x >= 0
                ? Result<int, string>.Success(x * 2)
                : Result<int, string>.Failure("negative"));

        KleisliResult<int, int, string> identity = x =>
            Task.FromResult(Result<int, string>.Success(x));

        var composed = await ComposeKleisliResult(f, identity)(input);
        var direct = await f(input);

        return (composed.IsSuccess == direct.IsSuccess &&
                (composed.IsFailure || composed.Value == direct.Value) &&
                (composed.IsSuccess || composed.Error == direct.Error));
    }

    /// <summary>
    /// Helper method to compose two KleisliResult arrows.
    /// Implements Kleisli composition: (f >=> g)(a) = f(a) >>= g
    /// </summary>
    /// <typeparam name="TIn">Input type.</typeparam>
    /// <typeparam name="TMid">Intermediate type.</typeparam>
    /// <typeparam name="TOut">Output type.</typeparam>
    /// <typeparam name="TError">Error type.</typeparam>
    /// <param name="f">First arrow.</param>
    /// <param name="g">Second arrow.</param>
    /// <returns>Composed Kleisli arrow.</returns>
    private static KleisliResult<TIn, TOut, TError> ComposeKleisliResult<TIn, TMid, TOut, TError>(
        KleisliResult<TIn, TMid, TError> f,
        KleisliResult<TMid, TOut, TError> g)
    {
        return async input =>
        {
            var firstResult = await f(input);
            if (firstResult.IsFailure)
            {
                return Result<TOut, TError>.Failure(firstResult.Error);
            }

            return await g(firstResult.Value);
        };
    }
}
