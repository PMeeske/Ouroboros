#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using CommandLine;

namespace Ouroboros.Options;

[Verb("maintenance", HelpText = "Maintenance operations for DAG compaction, archiving, and anomaly detection.")]
public sealed class MaintenanceOptions
{
    [Option('c', "command", Required = true, HelpText = "Maintenance command: compact, archive, detect-anomalies, schedule, history, alerts")]
    public string Command { get; set; } = string.Empty;

    [Option("archive-age-days", Required = false, HelpText = "Archive snapshots older than specified days", Default = 30)]
    public int ArchiveAgeDays { get; set; } = 30;

    [Option("archive-path", Required = false, HelpText = "Archive destination path")]
    public string? ArchivePath { get; set; }

    [Option("task-name", Required = false, HelpText = "Maintenance task name")]
    public string? TaskName { get; set; }

    [Option("schedule-hours", Required = false, HelpText = "Schedule interval in hours", Default = 24)]
    public int ScheduleHours { get; set; } = 24;

    [Option("alert-id", Required = false, HelpText = "Anomaly alert ID (GUID)")]
    public string? AlertId { get; set; }

    [Option("resolution", Required = false, HelpText = "Alert resolution description")]
    public string? Resolution { get; set; }

    [Option("format", Required = false, HelpText = "Output format: json|table|summary", Default = "summary")]
    public string Format { get; set; } = "summary";

    [Option("limit", Required = false, HelpText = "Limit number of results", Default = 50)]
    public int Limit { get; set; } = 50;

    [Option("unresolved-only", Required = false, HelpText = "Show only unresolved alerts", Default = true)]
    public bool UnresolvedOnly { get; set; } = true;

    [Option('v', "verbose", Required = false, HelpText = "Enable verbose output", Default = false)]
    public bool Verbose { get; set; }
}
