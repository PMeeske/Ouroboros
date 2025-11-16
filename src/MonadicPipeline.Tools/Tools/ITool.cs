// <copyright file="ITool.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Tools;

/// <summary>
/// Defines the contract for tools that can be invoked within the pipeline system.
/// </summary>
public interface ITool
{
    /// <summary>
    /// Gets the unique name of the tool.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a human-readable description of what the tool does.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the JSON Schema for the tool's input arguments.
    /// If null, the tool accepts free-form text input.
    /// </summary>
    string? JsonSchema { get; }

    /// <summary>
    /// Invokes the tool with the specified input.
    /// </summary>
    /// <param name="input">The input string or JSON for the tool.</param>
    /// <param name="ct">Cancellation token to cancel the operation.</param>
    /// <returns>A task representing the tool's result with proper error handling.</returns>
    Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default);
}
