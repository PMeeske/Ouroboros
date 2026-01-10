// <copyright file="Example.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain.MetaLearning;

/// <summary>
/// Represents a single input-output example for few-shot learning.
/// Used for adapting models to new tasks with minimal data.
/// </summary>
/// <param name="Input">The input prompt or query.</param>
/// <param name="Output">The expected output or response.</param>
/// <param name="Metadata">Optional metadata for the example (e.g., difficulty, domain).</param>
public sealed record Example(
    string Input,
    string Output,
    Dictionary<string, object>? Metadata = null)
{
    /// <summary>
    /// Creates an example with no metadata.
    /// </summary>
    /// <param name="input">The input text.</param>
    /// <param name="output">The expected output text.</param>
    /// <returns>A new Example instance.</returns>
    public static Example Create(string input, string output) =>
        new(input, output, null);

    /// <summary>
    /// Adds metadata to an existing example.
    /// </summary>
    /// <param name="key">Metadata key.</param>
    /// <param name="value">Metadata value.</param>
    /// <returns>A new Example with the added metadata.</returns>
    public Example WithMetadata(string key, object value)
    {
        var newMetadata = Metadata is null
            ? new Dictionary<string, object> { [key] = value }
            : new Dictionary<string, object>(Metadata) { [key] = value };
        return this with { Metadata = newMetadata };
    }
}
