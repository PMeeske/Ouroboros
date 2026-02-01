// <copyright file="CliResult.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.CLI.Fixtures;

/// <summary>
/// Represents the result of a CLI command execution.
/// </summary>
public class CliResult
{
    /// <summary>
    /// Gets or sets the standard output captured from the command.
    /// </summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the standard error captured from the command.
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the exit code returned by the command.
    /// </summary>
    public int ExitCode { get; set; }

    /// <summary>
    /// Gets or sets the execution time in milliseconds.
    /// </summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// Gets a value indicating whether the command was successful (exit code 0).
    /// </summary>
    public bool IsSuccess => ExitCode == 0;

    /// <summary>
    /// Gets a value indicating whether the command failed (exit code non-zero).
    /// </summary>
    public bool IsFailure => ExitCode != 0;

    /// <summary>
    /// Gets a value indicating whether the command produced any output.
    /// </summary>
    public bool HasOutput => !string.IsNullOrEmpty(Output);

    /// <summary>
    /// Gets a value indicating whether the command produced any errors.
    /// </summary>
    public bool HasError => !string.IsNullOrEmpty(Error);
}
