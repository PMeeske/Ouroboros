// <copyright file="ToolExecution.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Domain;

/// <summary>
/// Represents the execution result of a tool invocation in the pipeline.
/// Immutable record that captures tool name, arguments, output, and timestamp.
/// </summary>
/// <param name="ToolName">The name of the tool that was executed</param>
/// <param name="Arguments">The arguments passed to the tool</param>
/// <param name="Output">The output/result produced by the tool</param>
/// <param name="Timestamp">When the tool was executed</param>
public sealed record ToolExecution(
    string ToolName,
    string Arguments,
    string Output,
    DateTime Timestamp);
