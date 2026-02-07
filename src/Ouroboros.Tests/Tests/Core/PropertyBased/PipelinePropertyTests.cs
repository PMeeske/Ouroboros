// <copyright file="PipelinePropertyTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Core.PropertyBased;

using FsCheck.Xunit;
using Ouroboros.Core.Steps;

/// <summary>
/// Property-based tests for Pipeline monad laws using FsCheck.
/// Verifies that Pipeline operations satisfy functor and monad laws for arbitrary inputs.
/// </summary>
[Trait("Category", "Property")]
public class PipelinePropertyTests
{
    /// <summary>
    /// Verifies that Map preserves identity: pipeline.Map(x => x) produces same output as pipeline.
    /// Tests the functor identity law for pipelines.
    /// </summary>
    /// <param name="input">Arbitrary integer input.</param>
    /// <returns>True if map identity holds.</returns>
    [Property(MaxTest = 1000)]
    public async Task<bool> Pipeline_Map_PreservesIdentity_ForAllInts(int input)
    {
        // pipeline.Map(id) ≡ pipeline
        var pipeline = Pipeline.Lift<int, int>(x => x * 2 + 5);
        Func<int, int> identity = x => x;

        var originalResult = await pipeline.RunAsync(input);
        var mappedResult = await pipeline.Map(identity).RunAsync(input);

        return (originalResult == mappedResult);
    }

    /// <summary>
    /// Verifies Map composition law: pipeline.Map(f).Map(g) ≡ pipeline.Map(x => g(f(x)))
    /// Tests that mapping composed functions equals composing the maps.
    /// </summary>
    /// <param name="input">Arbitrary integer input.</param>
    /// <returns>True if map composition holds.</returns>
    [Property(MaxTest = 1000)]
    public async Task<bool> Pipeline_Map_CompositionLaw_HoldsForAllInts(int input)
    {
        // pipeline.Map(f).Map(g) ≡ pipeline.Map(g ∘ f)
        var pipeline = Pipeline.Lift<int, int>(x => x * 2);
        Func<int, int> f = x => x + 10;
        Func<int, int> g = x => x * 3;

        var composedMaps = await pipeline.Map(f).Map(g).RunAsync(input);
        var singleMap = await pipeline.Map(x => g(f(x))).RunAsync(input);

        return (composedMaps == singleMap);
    }

    /// <summary>
    /// Verifies Bind associativity: (pipeline.Bind(f)).Bind(g) ≡ pipeline.Bind(x => Bind f and g)
    /// Tests the monad associativity law for pipelines.
    /// Note: Pipeline.Bind has a specific signature where the Step input must match TOut.
    /// </summary>
    /// <param name="input">Arbitrary integer input.</param>
    /// <returns>True if bind associativity holds.</returns>
    [Property(MaxTest = 1000)]
    public async Task<bool> Pipeline_Bind_AssociativityLaw_HoldsForAllInts(int input)
    {
        // Test associativity through Then composition which is the Kleisli composition
        var pipeline = Pipeline.Lift<int, int>(x => x * 2);

        Step<int, int> f = x => Task.FromResult(x + 10);
        Step<int, int> g = x => Task.FromResult(x * 3);

        // Left associative: (pipeline.Then(f)).Then(g)
        var leftAssoc = await pipeline.Then(f).Then(g).RunAsync(input);
        
        // Right associative: pipeline.Then(f composed with g)
        Step<int, int> fThenG = async x => await g(await f(x));
        var rightAssoc = await pipeline.Then(fThenG).RunAsync(input);

        return (leftAssoc == rightAssoc);
    }

    /// <summary>
    /// Verifies Bind left identity: Pipeline.Pure().Bind(f) ≡ f(input)
    /// Tests that pure (return) followed by bind is equivalent to just applying the function.
    /// </summary>
    /// <param name="input">Arbitrary integer input.</param>
    /// <returns>True if bind left identity holds.</returns>
    [Property(MaxTest = 1000)]
    public async Task<bool> Pipeline_Bind_LeftIdentity_HoldsForAllInts(int input)
    {
        // return a >>= f ≡ f a
        Func<int, Step<int, int>> f = x => async _ => x * 3 + 7;

        var pipeline = Pipeline.Pure<int>();
        var boundResult = await pipeline.Bind(f).RunAsync(input);

        // f(input)(input) - call the kleisli arrow with input
        var fStep = f(input);
        var directResult = await fStep(input);

        return (boundResult == directResult);
    }

    /// <summary>
    /// Verifies Bind right identity: pipeline.Bind(x => Pure step) ≡ pipeline
    /// Tests that binding with pure (return) doesn't change the result.
    /// </summary>
    /// <param name="input">Arbitrary integer input.</param>
    /// <returns>True if bind right identity holds.</returns>
    [Property(MaxTest = 1000)]
    public async Task<bool> Pipeline_Bind_RightIdentity_HoldsForAllInts(int input)
    {
        // m >>= return ≡ m
        var pipeline = Pipeline.Lift<int, int>(x => x * 2 + 5);

        var originalResult = await pipeline.RunAsync(input);
        var boundResult = await pipeline.Bind(x => (Step<int, int>)(async _ => x)).RunAsync(input);

        return (originalResult == boundResult);
    }

    /// <summary>
    /// Verifies Then composition associativity: (p.Then(f)).Then(g) ≡ p.Then(f.Then(g))
    /// Tests that Kleisli composition is associative.
    /// </summary>
    /// <param name="input">Arbitrary integer input.</param>
    /// <returns>True if Then associativity holds.</returns>
    [Property(MaxTest = 1000)]
    public async Task<bool> Pipeline_Then_AssociativityLaw_HoldsForAllInts(int input)
    {
        // (f >=> g) >=> h ≡ f >=> (g >=> h)
        var pipeline = Pipeline.Lift<int, int>(x => x + 1);
        Step<int, int> f = x => Task.FromResult(x * 2);
        Step<int, int> g = x => Task.FromResult(x + 10);
        Step<int, int> h = x => Task.FromResult(x - 5);

        var leftAssoc = await pipeline.Then(f).Then(g).Then(h).RunAsync(input);

        // For right associativity, we need to compose g and h first
        Step<int, int> gThenH = async x => await h(await g(x));
        var rightAssoc = await pipeline.Then(f).Then(gThenH).RunAsync(input);

        return (leftAssoc == rightAssoc);
    }

    /// <summary>
    /// Verifies MapAsync preserves identity.
    /// Tests that async identity mapping doesn't change the output.
    /// </summary>
    /// <param name="input">Arbitrary integer input.</param>
    /// <returns>True if MapAsync identity holds.</returns>
    [Property(MaxTest = 1000)]
    public async Task<bool> Pipeline_MapAsync_PreservesIdentity_ForAllInts(int input)
    {
        // pipeline.MapAsync(async x => x) ≡ pipeline
        var pipeline = Pipeline.Lift<int, int>(x => x * 2 + 5);
        Func<int, Task<int>> asyncIdentity = x => Task.FromResult(x);

        var originalResult = await pipeline.RunAsync(input);
        var mappedResult = await pipeline.MapAsync(asyncIdentity).RunAsync(input);

        return (originalResult == mappedResult);
    }

    /// <summary>
    /// Verifies that Pure followed by Map equals direct function application.
    /// Tests the relationship between Pure and Map.
    /// </summary>
    /// <param name="input">Arbitrary integer input.</param>
    /// <returns>True if Pure-Map relationship holds.</returns>
    [Property(MaxTest = 1000)]
    public async Task<bool> Pipeline_Pure_MapRelationship_HoldsForAllInts(int input)
    {
        // Pure<T>().Map(f) should be equivalent to Lift(f)
        Func<int, int> f = x => x * 3 + 7;

        var pureThenMap = await Pipeline.Pure<int>().Map(f).RunAsync(input);
        var liftedFunction = await Pipeline.Lift(f).RunAsync(input);

        return (pureThenMap == liftedFunction);
    }

    /// <summary>
    /// Verifies that Map and Then are related correctly for pipelines.
    /// pipeline.Map(f) should behave like pipeline.Then(async x => f(x))
    /// </summary>
    /// <param name="input">Arbitrary integer input.</param>
    /// <returns>True if Map-Then relationship holds.</returns>
    [Property(MaxTest = 1000)]
    public async Task<bool> Pipeline_Map_ThenRelationship_HoldsForAllInts(int input)
    {
        // pipeline.Map(f) ≡ pipeline.Then(Step that wraps f)
        var pipeline = Pipeline.Lift<int, int>(x => x * 2);
        Func<int, int> f = x => x + 10;

        var mappedResult = await pipeline.Map(f).RunAsync(input);
        Step<int, int> stepF = x => Task.FromResult(f(x));
        var thenResult = await pipeline.Then(stepF).RunAsync(input);

        return (mappedResult == thenResult);
    }
}
