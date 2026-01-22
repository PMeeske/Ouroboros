// <copyright file="DreamOptions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using CommandLine;

namespace Ouroboros.CLI.Options;

/// <summary>
/// Command-line options for consciousness dream exploration.
/// </summary>
[Verb("dream", HelpText = "Explore the consciousness dream cycle for any circumstance")]
public sealed class DreamOptions
{
    /// <summary>
    /// Gets or sets the circumstance to dream about.
    /// </summary>
    [Value(0, Required = true, MetaName = "circumstance", HelpText = "The circumstance to walk through the dream cycle")]
    public required string Circumstance { get; set; }

    /// <summary>
    /// Gets or sets whether to show full details for each stage.
    /// </summary>
    [Option('d', "detailed", Required = false, HelpText = "Show detailed information for each stage")]
    public bool Detailed { get; set; }

    /// <summary>
    /// Gets or sets the delay in milliseconds between stages.
    /// </summary>
    [Option("delay", Required = false, Default = 1500, HelpText = "Delay in milliseconds between stages (for contemplation)")]
    public int DelayMs { get; set; }

    /// <summary>
    /// Gets or sets whether to show MeTTa symbolic cores.
    /// </summary>
    [Option("show-metta", Required = false, HelpText = "Display MeTTa symbolic representations")]
    public bool ShowMeTTa { get; set; }

    /// <summary>
    /// Gets or sets whether to use compact output.
    /// </summary>
    [Option('c', "compact", Required = false, HelpText = "Use compact output format")]
    public bool Compact { get; set; }
}
