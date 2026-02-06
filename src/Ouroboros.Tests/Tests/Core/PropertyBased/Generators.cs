// <copyright file="Generators.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Core.PropertyBased;

using FsCheck;
using Ouroboros.Core.Monads;

/// <summary>
/// Custom FsCheck generators for Ouroboros types.
/// Provides Arbitrary instances for Option and Result monads with reasonable distributions.
/// Note: These custom generators are optional - FsCheck can work with built-in generators for primitive types.
/// </summary>
public static class Generators
{
    // Custom generators are commented out for now as they require F# tuple syntax
    // The tests work fine with FsCheck's built-in generators for int, bool, string
    //
    // To implement custom generators in the future, use F# tuples or the WeightAndValue type:
    // Example: Gen.Frequency(WeightAndValue.Create(7, someGen), WeightAndValue.Create(3, otherGen))
}
