// <copyright file="PipelineResult.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Pipeline.Middleware;

/// <summary>
/// Result of pipeline execution.
/// </summary>
public sealed record PipelineResult(
    bool Success,
    string? Output,
    Exception? Error = null)
{
    /// <summary>
    /// Creates a successful pipeline result.
    /// </summary>
    public static PipelineResult Successful(string output) => new(
        Success: true,
        Output: output,
        Error: null);

    /// <summary>
    /// Creates a failed pipeline result.
    /// </summary>
    public static PipelineResult Failed(Exception error) => new(
        Success: false,
        Output: null,
        Error: error);
}
