// <copyright file="TestExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.Benchmarks;

/// <summary>
/// Represents a test example with input, expected output, and validation function.
/// </summary>
/// <param name="Input">The input to be processed.</param>
/// <param name="ExpectedOutput">The expected output for validation.</param>
/// <param name="Validator">Function to validate the actual output against the expected output.</param>
public sealed record TestExample(
    string Input,
    string ExpectedOutput,
    Func<string, string, bool> Validator);
