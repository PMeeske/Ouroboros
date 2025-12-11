#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using CommandLine;

namespace LangChainPipeline.Options;

[Verb("policy", HelpText = "Policy management operations for governance and safety.")]
public sealed class PolicyOptions
{
    [Option('c', "command", Required = true, HelpText = "Policy command: list, create, simulate, enforce, audit, approve")]
    public string Command { get; set; } = string.Empty;

    [Option('n', "name", Required = false, HelpText = "Policy name")]
    public string? Name { get; set; }

    [Option('d', "description", Required = false, HelpText = "Policy description")]
    public string? Description { get; set; }

    [Option('p', "policy-id", Required = false, HelpText = "Policy identifier (GUID)")]
    public string? PolicyId { get; set; }

    [Option('f', "file", Required = false, HelpText = "Policy definition file path (JSON)")]
    public string? FilePath { get; set; }

    [Option('o', "output", Required = false, HelpText = "Output file path for audit export")]
    public string? OutputPath { get; set; }

    [Option("format", Required = false, HelpText = "Output format: json|table|summary", Default = "summary")]
    public string Format { get; set; } = "summary";

    [Option("limit", Required = false, HelpText = "Limit number of results", Default = 50)]
    public int Limit { get; set; } = 50;

    [Option("since", Required = false, HelpText = "Filter audit trail since date (ISO 8601)")]
    public string? Since { get; set; }

    [Option("approval-id", Required = false, HelpText = "Approval request ID (GUID)")]
    public string? ApprovalId { get; set; }

    [Option("decision", Required = false, HelpText = "Approval decision: approve|reject|request-info")]
    public string? Decision { get; set; }

    [Option("approver", Required = false, HelpText = "Approver identifier")]
    public string? ApproverId { get; set; }

    [Option("comments", Required = false, HelpText = "Approval comments")]
    public string? Comments { get; set; }

    [Option('v', "verbose", Required = false, HelpText = "Enable verbose output", Default = false)]
    public bool Verbose { get; set; }
}
