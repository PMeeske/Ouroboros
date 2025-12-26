#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using CommandLine;

namespace Ouroboros.Options;

[Verb("dag", HelpText = "DAG (Directed Acyclic Graph) operations for pipeline branch management.")]
public sealed class DagOptions
{
    [Option('c', "command", Required = true, HelpText = "DAG command: snapshot, show, replay, validate, retention")]
    public string Command { get; set; } = string.Empty;

    [Option('b', "branch", Required = false, HelpText = "Branch name for snapshot/show operations")]
    public string? BranchName { get; set; }

    [Option('e', "epoch", Required = false, HelpText = "Epoch number for show/replay operations")]
    public long? EpochNumber { get; set; }

    [Option('o', "output", Required = false, HelpText = "Output file path for snapshot export")]
    public string? OutputPath { get; set; }

    [Option('i', "input", Required = false, HelpText = "Input file path for snapshot import/replay")]
    public string? InputPath { get; set; }

    [Option("dry-run", Required = false, HelpText = "Perform dry run (no actual changes)", Default = false)]
    public bool DryRun { get; set; }

    [Option("max-age-days", Required = false, HelpText = "Maximum age of snapshots in days for retention policy")]
    public int? MaxAgeDays { get; set; }

    [Option("max-count", Required = false, HelpText = "Maximum number of snapshots to retain per branch")]
    public int? MaxCount { get; set; }

    [Option("format", Required = false, HelpText = "Output format: json|summary", Default = "summary")]
    public string Format { get; set; } = "summary";

    [Option('v', "verbose", Required = false, HelpText = "Enable verbose output", Default = false)]
    public bool Verbose { get; set; }
}
