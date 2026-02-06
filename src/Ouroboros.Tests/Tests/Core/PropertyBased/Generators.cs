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
    // Custom generators are currently omitted because the tests work fine with FsCheck's
    // built-in generators for primitive types (int, bool, string, etc.).
    //
    // If you want to add custom generators in C#, you can use C#-friendly APIs such as:
    //   Gen.Frequency(
    //       Tuple.Create(7, someGen),
    //       Tuple.Create(3, otherGen));
    // or the WeightAndValue type:
    //   Gen.Frequency(
    //       WeightAndValue.Create(7, someGen),
    //       WeightAndValue.Create(3, otherGen));
}
