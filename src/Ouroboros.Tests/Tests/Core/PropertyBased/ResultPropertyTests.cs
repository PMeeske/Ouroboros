// <copyright file="ResultPropertyTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Core.PropertyBased;

using FsCheck.Xunit;
using Ouroboros.Core.Monads;

/// <summary>
/// Property-based tests for Result monad laws using FsCheck.
/// Verifies that monad laws hold for arbitrary inputs with both Success and Failure cases.
/// </summary>
[Trait("Category", "Property")]
public class ResultPropertyTests
{
    /// <summary>
    /// Verifies the Left Identity monad law for Result: return a >>= f ≡ f a
    /// Tests with functions that always return Success.
    /// </summary>
    /// <param name="a">Arbitrary integer value.</param>
    /// <returns>True if left identity holds.</returns>
    [Property(MaxTest = 1000)]
    public bool Result_LeftIdentity_HoldsForAllInts_TotalFunction(int a)
    {
        // return a >>= f ≡ f a
        Func<int, Result<int, string>> f = x => Result<int, string>.Success(x * 2);
        var left = Result<int, string>.Success(a).Bind(f);
        var right = f(a);

        return (left.IsSuccess == right.IsSuccess &&
                (!left.IsSuccess || left.Value == right.Value));
    }

    /// <summary>
    /// Verifies the Left Identity monad law with functions that may return Failure.
    /// Tests edge cases where function conditionally fails.
    /// </summary>
    /// <param name="a">Arbitrary integer value.</param>
    /// <returns>True if left identity holds with partial functions.</returns>
    [Property(MaxTest = 1000)]
    public bool Result_LeftIdentity_HoldsForAllInts_PartialFunction(int a)
    {
        // return a >>= f ≡ f a
        Func<int, Result<int, string>> f = x => x >= 0
            ? Result<int, string>.Success(x * 2)
            : Result<int, string>.Failure("negative value");

        var left = Result<int, string>.Success(a).Bind(f);
        var right = f(a);

        return (left.IsSuccess == right.IsSuccess &&
                (left.IsFailure || left.Value == right.Value) &&
                (left.IsSuccess || left.Error == right.Error));
    }

    /// <summary>
    /// Verifies the Right Identity monad law: m >>= return ≡ m
    /// Tests with arbitrary Result values (both Success and Failure).
    /// </summary>
    /// <param name="isSuccess">Whether the result is successful.</param>
    /// <param name="value">The value if successful.</param>
    /// <param name="error">The error if failed.</param>
    /// <returns>True if right identity holds.</returns>
    [Property(MaxTest = 1000)]
    public bool Result_RightIdentity_HoldsForAllResults(bool isSuccess, int value, string error)
    {
        // m >>= return ≡ m
        var m = isSuccess
            ? Result<int, string>.Success(value)
            : Result<int, string>.Failure(error ?? "error");

        var result = m.Bind(x => Result<int, string>.Success(x));

        return (result.IsSuccess == m.IsSuccess &&
                (result.IsFailure || result.Value == m.Value) &&
                (result.IsSuccess || result.Error == m.Error));
    }

    /// <summary>
    /// Verifies the Associativity monad law: (m >>= f) >>= g ≡ m >>= (\x -> f x >>= g)
    /// Tests with total functions that always succeed.
    /// </summary>
    /// <param name="a">Arbitrary integer value.</param>
    /// <returns>True if associativity holds.</returns>
    [Property(MaxTest = 1000)]
    public bool Result_Associativity_HoldsForAllInts(int a)
    {
        // (m >>= f) >>= g ≡ m >>= (\x -> f x >>= g)
        var m = Result<int, string>.Success(a);
        Func<int, Result<int, string>> f = x => Result<int, string>.Success(x * 2);
        Func<int, Result<int, string>> g = x => Result<int, string>.Success(x + 3);

        var left = m.Bind(f).Bind(g);
        var right = m.Bind(x => f(x).Bind(g));

        return (left.IsSuccess == right.IsSuccess &&
                (!left.IsSuccess || left.Value == right.Value));
    }

    /// <summary>
    /// Verifies Associativity with Failure propagation.
    /// Tests that (m >>= f) >>= g ≡ m >>= (x => f(x) >>= g) even when f returns Failure.
    /// </summary>
    /// <param name="a">Arbitrary integer value.</param>
    /// <returns>True if associativity holds with failure propagation.</returns>
    [Property(MaxTest = 1000)]
    public bool Result_Associativity_WithFailurePropagation_HoldsForAllInts(int a)
    {
        // (m >>= f) >>= g ≡ m >>= (\x -> f x >>= g)
        var m = Result<int, string>.Success(a);
        Func<int, Result<int, string>> f = x => x % 2 == 0
            ? Result<int, string>.Success(x / 2)
            : Result<int, string>.Failure("odd number");
        Func<int, Result<int, string>> g = x => x > 0
            ? Result<int, string>.Success(x + 10)
            : Result<int, string>.Failure("non-positive");

        var left = m.Bind(f).Bind(g);
        var right = m.Bind(x => f(x).Bind(g));

        return (left.IsSuccess == right.IsSuccess &&
                (left.IsFailure || left.Value == right.Value) &&
                (left.IsSuccess || left.Error == right.Error));
    }

    /// <summary>
    /// Verifies Associativity when starting with Failure.
    /// Tests that Failure short-circuits correctly in both associativity forms.
    /// </summary>
    /// <param name="errorMsg">The error message.</param>
    /// <returns>True if associativity holds for Failure.</returns>
    [Property(MaxTest = 1000)]
    public bool Result_Associativity_WithFailure_HoldsForAllErrors(string errorMsg)
    {
        // (m >>= f) >>= g ≡ m >>= (\x -> f x >>= g)
        var m = Result<int, string>.Failure(errorMsg ?? "error");
        Func<int, Result<int, string>> f = x => Result<int, string>.Success(x * 2);
        Func<int, Result<int, string>> g = x => Result<int, string>.Success(x + 3);

        var left = m.Bind(f).Bind(g);
        var right = m.Bind(x => f(x).Bind(g));

        return (left.IsFailure && right.IsFailure &&
                left.Error == right.Error);
    }

    /// <summary>
    /// Verifies that Map preserves Success/Failure status.
    /// Success(a).Map(f) should always be Success(f(a)).
    /// </summary>
    /// <param name="a">Arbitrary integer value.</param>
    /// <returns>True if Map preserves Success.</returns>
    [Property(MaxTest = 1000)]
    public bool Result_Map_PreservesSuccess_ForAllInts(int a)
    {
        // Success(a).Map(f) ≡ Success(f(a))
        var result = Result<int, string>.Success(a);
        Func<int, int> f = x => x * 3 + 7;

        var mapped = result.Map(f);
        var expected = Result<int, string>.Success(f(a));

        return (mapped.IsSuccess == expected.IsSuccess &&
                mapped.Value == expected.Value);
    }

    /// <summary>
    /// Verifies that Map preserves Failure and doesn't execute the function.
    /// Failure(e).Map(f) should always be Failure(e).
    /// </summary>
    /// <param name="errorMsg">The error message.</param>
    /// <returns>True if Map preserves Failure.</returns>
    [Property(MaxTest = 1000)]
    public bool Result_Map_PreservesFailure_ForAllErrors(string errorMsg)
    {
        // Failure(e).Map(f) ≡ Failure(e)
        var result = Result<int, string>.Failure(errorMsg ?? "error");
        Func<int, int> f = x => x * 3 + 7;

        var mapped = result.Map(f);

        return (mapped.IsFailure && mapped.Error == (errorMsg ?? "error"));
    }

    /// <summary>
    /// Verifies Functor Law 1 (Identity): result.Map(id) ≡ result
    /// The identity function should not change the result.
    /// </summary>
    /// <param name="isSuccess">Whether the result is successful.</param>
    /// <param name="value">The value if successful.</param>
    /// <param name="error">The error if failed.</param>
    /// <returns>True if functor identity law holds.</returns>
    [Property(MaxTest = 1000)]
    public bool Result_FunctorLaw_Identity_HoldsForAllResults(bool isSuccess, int value, string error)
    {
        // fmap id ≡ id
        var result = isSuccess
            ? Result<int, string>.Success(value)
            : Result<int, string>.Failure(error ?? "error");
        Func<int, int> identity = x => x;
        var mapped = result.Map(identity);

        return (mapped.IsSuccess == result.IsSuccess &&
                (mapped.IsFailure || mapped.Value == result.Value) &&
                (mapped.IsSuccess || mapped.Error == result.Error));
    }

    /// <summary>
    /// Verifies Functor Law 2 (Composition): result.Map(f).Map(g) ≡ result.Map(x => g(f(x)))
    /// Mapping composed functions should equal composing the maps.
    /// </summary>
    /// <param name="isSuccess">Whether the result is successful.</param>
    /// <param name="value">The value if successful.</param>
    /// <param name="error">The error if failed.</param>
    /// <returns>True if functor composition law holds.</returns>
    [Property(MaxTest = 1000)]
    public bool Result_FunctorLaw_Composition_HoldsForAllResults(bool isSuccess, int value, string error)
    {
        // fmap (g . f) ≡ fmap g . fmap f
        var result = isSuccess
            ? Result<int, string>.Success(value)
            : Result<int, string>.Failure(error ?? "error");
        Func<int, int> f = x => x * 2;
        Func<int, int> g = x => x + 10;

        var left = result.Map(f).Map(g);
        var right = result.Map(x => g(f(x)));

        return (left.IsSuccess == right.IsSuccess &&
                (left.IsFailure || left.Value == right.Value) &&
                (left.IsSuccess || left.Error == right.Error));
    }

    /// <summary>
    /// Verifies that Map and Bind are related by the functor-monad relationship.
    /// result.Map(f) ≡ result.Bind(x => Success(f(x)))
    /// </summary>
    /// <param name="isSuccess">Whether the result is successful.</param>
    /// <param name="value">The value if successful.</param>
    /// <param name="error">The error if failed.</param>
    /// <returns>True if map-bind relationship holds.</returns>
    [Property(MaxTest = 1000)]
    public bool Result_MapBindRelationship_HoldsForAllResults(bool isSuccess, int value, string error)
    {
        // fmap f ≡ (>>= return . f)
        var result = isSuccess
            ? Result<int, string>.Success(value)
            : Result<int, string>.Failure(error ?? "error");
        Func<int, int> f = x => x * 3 + 7;

        var mappedResult = result.Map(f);
        var bindResult = result.Bind(x => Result<int, string>.Success(f(x)));

        return (mappedResult.IsSuccess == bindResult.IsSuccess &&
                (mappedResult.IsFailure || mappedResult.Value == bindResult.Value) &&
                (mappedResult.IsSuccess || mappedResult.Error == bindResult.Error));
    }
}
