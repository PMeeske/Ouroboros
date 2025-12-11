// <copyright file="SelfOptions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using CommandLine;

namespace Ouroboros.CLI.Options;

/// <summary>
/// Command-line options for self-model operations.
/// </summary>
[Verb("self", HelpText = "Agent self-model operations (state, forecast, commitments, explain, query, plan)")]
public sealed class SelfOptions
{
    /// <summary>
    /// Gets or sets the self-model command to execute.
    /// </summary>
    [Value(0, Required = true, MetaName = "command", HelpText = "Command: state, forecast, commitments, explain, query, plan")]
    public required string Command { get; set; }

    /// <summary>
    /// Gets or sets optional event ID for explain command, or MeTTa query for query command.
    /// </summary>
    [Option('e', "event", Required = false, HelpText = "Event ID to explain (for explain command) or MeTTa query string (for query command)")]
    public string? EventId { get; set; }

    /// <summary>
    /// Gets or sets whether to include full context.
    /// </summary>
    [Option('c', "context", Required = false, Default = true, HelpText = "Include full context in explanation")]
    public bool IncludeContext { get; set; }

    /// <summary>
    /// Gets or sets the output format.
    /// </summary>
    [Option('f', "format", Required = false, Default = "table", HelpText = "Output format: table, json, summary")]
    public string OutputFormat { get; set; } = "table";

    /// <summary>
    /// Gets or sets verbose output flag.
    /// </summary>
    [Option('v', "verbose", Required = false, HelpText = "Enable verbose output")]
    public bool Verbose { get; set; }

    /// <summary>
    /// Gets or sets the interactive mode flag.
    /// </summary>
    [Option('i', "interactive", Required = false, HelpText = "Start interactive MeTTa REPL mode (for query and plan commands)")]
    public bool Interactive { get; set; }

    /// <summary>
    /// Gets or sets the output path for saving results.
    /// </summary>
    [Option('o', "output", Required = false, HelpText = "Output file path (JSON)")]
    public string? OutputPath { get; set; }
}
