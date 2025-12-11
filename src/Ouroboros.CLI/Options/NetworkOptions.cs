using CommandLine;

namespace LangChainPipeline.Options;

/// <summary>
/// Options for network state commands.
/// </summary>
[Verb("network", HelpText = "Manage emergent network state with Merkle-DAG reasoning history")]
public class NetworkOptions
{
    [Option('c', "create-node", Required = false, HelpText = "Create a new node with type and JSON payload")]
    public bool CreateNode { get; set; }

    [Option('t', "type", Required = false, HelpText = "Type name for the node (e.g., Draft, Critique)")]
    public string? TypeName { get; set; }

    [Option('p', "payload", Required = false, HelpText = "JSON payload for the node")]
    public string? Payload { get; set; }

    [Option('a', "add-transition", Required = false, HelpText = "Add a transition between nodes")]
    public bool AddTransition { get; set; }

    [Option("input", Required = false, HelpText = "Input node ID for transition")]
    public string? InputId { get; set; }

    [Option("output", Required = false, HelpText = "Output node ID for transition")]
    public string? OutputId { get; set; }

    [Option("operation", Required = false, HelpText = "Operation name for transition")]
    public string? OperationName { get; set; }

    [Option('v', "view-dag", Required = false, HelpText = "View the current DAG structure")]
    public bool ViewDag { get; set; }

    [Option('s', "snapshot", Required = false, HelpText = "Create and display a global network state snapshot")]
    public bool CreateSnapshot { get; set; }

    [Option('r', "replay", Required = false, HelpText = "Replay transitions to a target node")]
    public string? ReplayToNode { get; set; }

    [Option('l', "list-nodes", Required = false, HelpText = "List all nodes, optionally filtered by type")]
    public bool ListNodes { get; set; }

    [Option('e', "list-edges", Required = false, HelpText = "List all transitions")]
    public bool ListEdges { get; set; }

    [Option('i', "interactive", Required = false, HelpText = "Run interactive demo")]
    public bool Interactive { get; set; }
}
