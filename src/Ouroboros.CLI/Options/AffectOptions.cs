// <copyright file="AffectOptions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using CommandLine;

namespace Ouroboros.CLI.Options;

/// <summary>
/// Command-line options for affective dynamics operations.
/// </summary>
[Verb("affect", HelpText = "Affective dynamics operations (show, policy, tune, signal)")]
public sealed class AffectOptions
{
    /// <summary>
    /// Gets or sets the affect command to execute.
    /// </summary>
    [Value(0, Required = true, MetaName = "command", HelpText = "Command: show, policy, tune, signal, reset")]
    public required string Command { get; set; }

    /// <summary>
    /// Gets or sets the signal type for signal command.
    /// </summary>
    [Option('t', "type", Required = false, HelpText = "Signal type: stress, confidence, curiosity, valence, arousal")]
    public string? SignalType { get; set; }

    /// <summary>
    /// Gets or sets the signal value for signal command.
    /// </summary>
    [Option('s', "signal", Required = false, HelpText = "Signal value (-1.0 to 1.0)")]
    public double? SignalValue { get; set; }

    /// <summary>
    /// Gets or sets the policy rule to modify.
    /// </summary>
    [Option('r', "rule", Required = false, HelpText = "Policy rule name to modify")]
    public string? RuleName { get; set; }

    /// <summary>
    /// Gets or sets the lower bound for tuning.
    /// </summary>
    [Option('l', "lower", Required = false, HelpText = "Lower bound for policy rule")]
    public double? LowerBound { get; set; }

    /// <summary>
    /// Gets or sets the upper bound for tuning.
    /// </summary>
    [Option('u', "upper", Required = false, HelpText = "Upper bound for policy rule")]
    public double? UpperBound { get; set; }

    /// <summary>
    /// Gets or sets the target value for tuning.
    /// </summary>
    [Option("target", Required = false, HelpText = "Target value for policy rule")]
    public double? TargetValue { get; set; }

    /// <summary>
    /// Gets or sets whether to run stress detection with FFT analysis.
    /// </summary>
    [Option("detect-stress", Required = false, HelpText = "Run stress detection with FFT analysis")]
    public bool DetectStress { get; set; }

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
    /// Gets or sets the output path for saving results.
    /// </summary>
    [Option('o', "output", Required = false, HelpText = "Output file path (JSON)")]
    public string? OutputPath { get; set; }
}
