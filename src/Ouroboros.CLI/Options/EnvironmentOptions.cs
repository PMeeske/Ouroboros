// <copyright file="EnvironmentOptions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using CommandLine;

namespace Ouroboros.Options;

/// <summary>
/// CLI options for environment commands.
/// </summary>
[Verb("env", HelpText = "Run environment interaction commands for RL training")]
public class EnvironmentOptions
{
    /// <summary>
    /// Gets or sets the subcommand to execute.
    /// </summary>
    [Value(0, Required = true, HelpText = "Subcommand: step, run, replay")]
    public string? Command { get; set; }

    /// <summary>
    /// Gets or sets the environment name.
    /// </summary>
    [Option('e', "environment", Default = "gridworld", HelpText = "Environment name (gridworld)")]
    public string Environment { get; set; } = "gridworld";

    /// <summary>
    /// Gets or sets the policy type.
    /// </summary>
    [Option('p', "policy", Default = "epsilon-greedy", HelpText = "Policy type (epsilon-greedy, bandit, random)")]
    public string Policy { get; set; } = "epsilon-greedy";

    /// <summary>
    /// Gets or sets the exploration parameter (epsilon for epsilon-greedy).
    /// </summary>
    [Option("epsilon", Default = 0.1, HelpText = "Exploration rate for epsilon-greedy policy")]
    public double Epsilon { get; set; } = 0.1;

    /// <summary>
    /// Gets or sets the number of episodes to run.
    /// </summary>
    [Option('n', "episodes", Default = 10, HelpText = "Number of episodes to run")]
    public int Episodes { get; set; } = 10;

    /// <summary>
    /// Gets or sets the maximum steps per episode.
    /// </summary>
    [Option("max-steps", Default = 100, HelpText = "Maximum steps per episode")]
    public int MaxSteps { get; set; } = 100;

    /// <summary>
    /// Gets or sets the grid width (for gridworld).
    /// </summary>
    [Option("width", Default = 5, HelpText = "Grid width for gridworld")]
    public int Width { get; set; } = 5;

    /// <summary>
    /// Gets or sets the grid height (for gridworld).
    /// </summary>
    [Option("height", Default = 5, HelpText = "Grid height for gridworld")]
    public int Height { get; set; } = 5;

    /// <summary>
    /// Gets or sets whether to show verbose output.
    /// </summary>
    [Option('v', "verbose", Default = false, HelpText = "Show verbose output")]
    public bool Verbose { get; set; }

    /// <summary>
    /// Gets or sets the output file for episode data.
    /// </summary>
    [Option('o', "output", HelpText = "Output file for episode data (JSON)")]
    public string? OutputFile { get; set; }

    /// <summary>
    /// Gets or sets the input file for replay.
    /// </summary>
    [Option('i', "input", HelpText = "Input file for episode replay (JSON)")]
    public string? InputFile { get; set; }

    /// <summary>
    /// Gets or sets the random seed.
    /// </summary>
    [Option('s', "seed", HelpText = "Random seed for reproducibility")]
    public int? Seed { get; set; }
}
