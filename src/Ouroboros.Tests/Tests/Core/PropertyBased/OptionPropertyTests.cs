// <copyright file="OptionPropertyTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Core.PropertyBased;

using FluentAssertions;
using FsCheck.Xunit;
using Ouroboros.Core.Monads;
using Xunit;

/// <summary>
/// Property-based tests for Option monad laws using FsCheck.
/// Verifies that monad laws hold for arbitrary inputs, not just fixed test values.
/// </summary>
[Trait("Category", "Property")]
public class OptionPropertyTests
{
    /// <summary>
    /// Verifies the Left Identity monad law: return a >>= f ≡ f a
    /// Tests with total functions that always return Some.
    /// </summary>
    /// <param name="a">Arbitrary integer value.</param>
    /// <returns>True if left identity holds.</returns>
    [Property(MaxTest = 1000)]
    public bool Option_LeftIdentity_HoldsForAllInts_TotalFunction(int a)
    {
        // return a >>= f ≡ f a
        Func<int, Option<int>> f = x => Option<int>.Some(x * 2);
        var left = Option<int>.Some(a).Bind(f);
        var right = f(a);
        return left.HasValue == right.HasValue &&
                (!left.HasValue || left.Value == right.Value);
    }

    /// <summary>
    /// Verifies the Left Identity monad law with partial functions that may return None.
    /// Tests edge case where function conditionally returns None.
    /// </summary>
    /// <param name="a">Arbitrary integer value.</param>
    /// <returns>True if left identity holds with partial functions.</returns>
    [Property(MaxTest = 1000)]
    public bool Option_LeftIdentity_HoldsForAllInts_PartialFunction(int a)
    {
        // return a >>= f ≡ f a
        Func<int, Option<int>> f = x => x != 0
            ? Option<int>.Some(x * 2)
            : Option<int>.None();
        var left = Option<int>.Some(a).Bind(f);
        var right = f(a);
        return (left.HasValue == right.HasValue &&
                (!left.HasValue || left.Value == right.Value));
    }

    /// <summary>
    /// Verifies the Right Identity monad law: m >>= return ≡ m
    /// Tests with arbitrary Option values (both Some and None).
    /// </summary>
    /// <param name="hasValue">Whether the option has a value.</param>
    /// <param name="value">The value if present.</param>
    /// <returns>True if right identity holds.</returns>
    [Property(MaxTest = 1000)]
    public bool Option_RightIdentity_HoldsForAllOptions(bool hasValue, int value)
    {
        // m >>= return ≡ m
        var m = hasValue ? Option<int>.Some(value) : Option<int>.None();
        var result = m.Bind(x => Option<int>.Some(x));

        return (result.HasValue == m.HasValue &&
                (!result.HasValue || result.Value == m.Value));
    }

    /// <summary>
    /// Verifies the Associativity monad law: (m >>= f) >>= g ≡ m >>= (\x -> f x >>= g)
    /// Tests with total functions.
    /// </summary>
    /// <param name="a">Arbitrary integer value.</param>
    /// <returns>True if associativity holds.</returns>
    [Property(MaxTest = 1000)]
    public bool Option_Associativity_HoldsForAllInts(int a)
    {
        // (m >>= f) >>= g ≡ m >>= (\x -> f x >>= g)
        var m = Option<int>.Some(a);
        Func<int, Option<int>> f = x => Option<int>.Some(x * 2);
        Func<int, Option<int>> g = x => Option<int>.Some(x + 3);

        var left = m.Bind(f).Bind(g);
        var right = m.Bind(x => f(x).Bind(g));

        return (left.HasValue == right.HasValue &&
                (!left.HasValue || left.Value == right.Value));
    }

    /// <summary>
    /// Verifies Associativity with partial functions that may return None.
    /// Tests that associativity holds even when intermediate functions return None.
    /// </summary>
    /// <param name="a">Arbitrary integer value.</param>
    /// <returns>True if associativity holds with partial functions.</returns>
    [Property(MaxTest = 1000)]
    public bool Option_Associativity_WithPartialFunctions_HoldsForAllInts(int a)
    {
        // (m >>= f) >>= g ≡ m >>= (\x -> f x >>= g)
        var m = Option<int>.Some(a);
        Func<int, Option<int>> f = x => x > 0
            ? Option<int>.Some(x * 2)
            : Option<int>.None();
        Func<int, Option<int>> g = x => x < 1000
            ? Option<int>.Some(x + 3)
            : Option<int>.None();

        var left = m.Bind(f).Bind(g);
        var right = m.Bind(x => f(x).Bind(g));

        return (left.HasValue == right.HasValue &&
                (!left.HasValue || left.Value == right.Value));
    }

    /// <summary>
    /// Verifies Associativity when starting with None.
    /// Tests that None short-circuits correctly in both associativity forms.
    /// </summary>
    [Fact]
    public void Option_Associativity_WithNone_Holds()
    {
        // (m >>= f) >>= g ≡ m >>= (\x -> f x >>= g)
        var m = Option<int>.None();
        Func<int, Option<int>> f = x => Option<int>.Some(x * 2);
        Func<int, Option<int>> g = x => Option<int>.Some(x + 3);

        var left = m.Bind(f).Bind(g);
        var right = m.Bind(x => f(x).Bind(g));

        left.HasValue.Should().Be(right.HasValue);
    }

    /// <summary>
    /// Verifies Functor Law 1 (Identity): option.Map(id) ≡ option
    /// The identity function should not change the option.
    /// </summary>
    /// <param name="hasValue">Whether the option has a value.</param>
    /// <param name="value">The value if present.</param>
    /// <returns>True if functor identity law holds.</returns>
    [Property(MaxTest = 1000)]
    public bool Option_FunctorLaw_Identity_HoldsForAllOptions(bool hasValue, int value)
    {
        // fmap id ≡ id
        var option = hasValue ? Option<int>.Some(value) : Option<int>.None();
        Func<int, int> identity = x => x;
        var mapped = option.Map(identity);

        return (mapped.HasValue == option.HasValue &&
                (!mapped.HasValue || mapped.Value == option.Value));
    }

    /// <summary>
    /// Verifies Functor Law 2 (Composition): option.Map(f).Map(g) ≡ option.Map(x => g(f(x)))
    /// Mapping composed functions should equal composing the maps.
    /// </summary>
    /// <param name="hasValue">Whether the option has a value.</param>
    /// <param name="value">The value if present.</param>
    /// <returns>True if functor composition law holds.</returns>
    [Property(MaxTest = 1000)]
    public bool Option_FunctorLaw_Composition_HoldsForAllOptions(bool hasValue, int value)
    {
        // fmap (g . f) ≡ fmap g . fmap f
        var option = hasValue ? Option<int>.Some(value) : Option<int>.None();
        Func<int, int> f = x => x * 2;
        Func<int, int> g = x => x + 10;

        var left = option.Map(f).Map(g);
        var right = option.Map(x => g(f(x)));

        return (left.HasValue == right.HasValue &&
                (!left.HasValue || left.Value == right.Value));
    }

    /// <summary>
    /// Verifies that Map and Bind are related by the functor-monad relationship.
    /// option.Map(f) ≡ option.Bind(x => Some(f(x)))
    /// </summary>
    /// <param name="hasValue">Whether the option has a value.</param>
    /// <param name="value">The value if present.</param>
    /// <returns>True if map-bind relationship holds.</returns>
    [Property(MaxTest = 1000)]
    public bool Option_MapBindRelationship_HoldsForAllOptions(bool hasValue, int value)
    {
        // fmap f ≡ (>>= return . f)
        var option = hasValue ? Option<int>.Some(value) : Option<int>.None();
        Func<int, int> f = x => x * 3 + 7;

        var mappedResult = option.Map(f);
        var bindResult = option.Bind(x => Option<int>.Some(f(x)));

        return (mappedResult.HasValue == bindResult.HasValue &&
                (!mappedResult.HasValue || mappedResult.Value == bindResult.Value));
    }
}
