// <copyright file="DistinctionOptions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

using CommandLine;

namespace Ouroboros.CLI.Options;

/// <summary>
/// Command-line options for distinction learning management.
/// </summary>
[Verb("distinction", HelpText = "Manage distinction learning from consciousness dream cycles")]
public sealed class DistinctionOptions
{
    /// <summary>
    /// Gets or sets the subcommand to execute.
    /// </summary>
    [Value(0, Required = true, MetaName = "command", HelpText = "Command: status, list, dissolve, learn, export, clear")]
    public required string Command { get; set; }
}

/// <summary>
/// Options for showing distinction learning status.
/// </summary>
[Verb("distinction-status", HelpText = "Show distinction learning status and statistics")]
public sealed class DistinctionStatusOptions
{
    /// <summary>
    /// Gets or sets whether to show verbose details.
    /// </summary>
    [Option('v', "verbose", Required = false, Default = false, HelpText = "Show verbose details including storage paths")]
    public bool Verbose { get; set; }
}

/// <summary>
/// Options for listing distinctions.
/// </summary>
[Verb("distinction-list", HelpText = "List all learned distinctions with optional filtering")]
public sealed class DistinctionListOptions
{
    /// <summary>
    /// Gets or sets the stage filter.
    /// </summary>
    [Option('s', "stage", Required = false, HelpText = "Filter by learning stage (e.g., Recognition, Transition)")]
    public string? Stage { get; set; }

    /// <summary>
    /// Gets or sets the minimum fitness threshold.
    /// </summary>
    [Option('f', "min-fitness", Required = false, HelpText = "Minimum fitness threshold (0.0 - 1.0)")]
    public double? MinFitness { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of results.
    /// </summary>
    [Option('l', "limit", Required = false, Default = 50, HelpText = "Maximum number of distinctions to display")]
    public int Limit { get; set; }

    /// <summary>
    /// Gets or sets whether to show dissolved distinctions.
    /// </summary>
    [Option("show-dissolved", Required = false, Default = false, HelpText = "Include dissolved distinctions in results")]
    public bool ShowDissolved { get; set; }
}

/// <summary>
/// Options for dissolving low-fitness distinctions.
/// </summary>
[Verb("distinction-dissolve", HelpText = "Dissolve low-fitness distinctions to free storage")]
public sealed class DistinctionDissolveOptions
{
    /// <summary>
    /// Gets or sets the fitness threshold.
    /// </summary>
    [Option('t', "threshold", Required = false, Default = 0.3, HelpText = "Fitness threshold below which to dissolve (0.0 - 1.0)")]
    public double Threshold { get; set; }

    /// <summary>
    /// Gets or sets whether this is a dry run.
    /// </summary>
    [Option('d', "dry-run", Required = false, Default = false, HelpText = "Preview what would be dissolved without actually doing it")]
    public bool DryRun { get; set; }

    /// <summary>
    /// Gets or sets the dissolution strategy.
    /// </summary>
    [Option("strategy", Required = false, Default = "fitness", HelpText = "Dissolution strategy: fitness, oldest, lru")]
    public string Strategy { get; set; } = "fitness";
}

/// <summary>
/// Options for manual distinction learning.
/// </summary>
[Verb("distinction-learn", HelpText = "Manually trigger distinction learning on provided text")]
public sealed class DistinctionLearnOptions
{
    /// <summary>
    /// Gets or sets the text to learn from.
    /// </summary>
    [Value(0, Required = true, MetaName = "text", HelpText = "Text content to learn distinctions from")]
    public required string Text { get; set; }

    /// <summary>
    /// Gets or sets whether to show learning stages.
    /// </summary>
    [Option("show-stages", Required = false, Default = false, HelpText = "Display the learning process through consciousness stages")]
    public bool ShowStages { get; set; }

    /// <summary>
    /// Gets or sets the learning stage.
    /// </summary>
    [Option('s', "stage", Required = false, Default = "Recognition", HelpText = "Consciousness stage for learning (Recognition, Transition, etc.)")]
    public string Stage { get; set; } = "Recognition";
}

/// <summary>
/// Options for exporting distinctions.
/// </summary>
[Verb("distinction-export", HelpText = "Export distinctions to JSON format")]
public sealed class DistinctionExportOptions
{
    /// <summary>
    /// Gets or sets the output file path.
    /// </summary>
    [Option('o', "output", Required = false, HelpText = "Output file path (default: distinctions-export.json)")]
    public string? Output { get; set; }

    /// <summary>
    /// Gets or sets whether to include dissolved distinctions.
    /// </summary>
    [Option("include-dissolved", Required = false, Default = false, HelpText = "Include dissolved distinctions in export")]
    public bool IncludeDissolved { get; set; }
}

/// <summary>
/// Options for clearing all distinctions.
/// </summary>
[Verb("distinction-clear", HelpText = "Clear all distinctions (WARNING: destructive operation)")]
public sealed class DistinctionClearOptions
{
    /// <summary>
    /// Gets or sets whether confirmation is provided.
    /// </summary>
    [Option('y', "confirm", Required = false, Default = false, HelpText = "Skip confirmation prompt")]
    public bool Confirm { get; set; }
}
