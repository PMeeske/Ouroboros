// <copyright file="PipelineContext.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Middleware;

/// <summary>
/// Context for pipeline execution.
/// </summary>
public sealed record PipelineContext(
    string Input,
    Dictionary<string, object> Metadata)
{
    /// <summary>
    /// Creates a simple pipeline context with just input.
    /// </summary>
    public static PipelineContext FromInput(string input) => new(
        Input: input,
        Metadata: new Dictionary<string, object>());
}
