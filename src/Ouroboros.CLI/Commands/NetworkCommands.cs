using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using LangChainPipeline.Domain.States;
using LangChainPipeline.Network;
using LangChainPipeline.Options;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// CLI commands for interacting with the emergent network state.
/// </summary>
public static class NetworkCommands
{
    private static readonly MerkleDag Dag = new();
    private static readonly NetworkStateProjector Projector = new(Dag);
    private static readonly TransitionReplayEngine ReplayEngine = new(Dag);

    /// <summary>
    /// Executes network state commands based on the provided options.
    /// </summary>
    public static async Task RunAsync(NetworkOptions options)
    {
        Console.WriteLine("=== Emergent Network State Manager ===\n");

        try
        {
            if (options.Interactive)
            {
                await RunInteractiveDemoAsync();
                return;
            }

            if (options.CreateNode)
            {
                CreateNode(options);
            }
            else if (options.AddTransition)
            {
                AddTransition(options);
            }
            else if (options.ViewDag)
            {
                ViewDag(options);
            }
            else if (options.CreateSnapshot)
            {
                CreateAndDisplaySnapshot();
            }
            else if (!string.IsNullOrEmpty(options.ReplayToNode))
            {
                ReplayTransitions(options.ReplayToNode);
            }
            else if (options.ListNodes)
            {
                ListNodes(options);
            }
            else if (options.ListEdges)
            {
                ListEdges();
            }
            else
            {
                Console.WriteLine("Please specify a command. Use --help for options.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    private static void CreateNode(NetworkOptions options)
    {
        if (string.IsNullOrEmpty(options.TypeName) || string.IsNullOrEmpty(options.Payload))
        {
            Console.WriteLine("Error: --type and --payload are required for creating a node.");
            return;
        }

        var node = MonadNode.FromPayload(options.TypeName, options.Payload);
        var result = Dag.AddNode(node);

        if (result.IsSuccess)
        {
            Console.WriteLine($"✓ Created node: {node.Id}");
            Console.WriteLine($"  Type: {node.TypeName}");
            Console.WriteLine($"  Hash: {node.Hash}");
            Console.WriteLine($"  Created: {node.CreatedAt}");
        }
        else
        {
            Console.WriteLine($"✗ Failed to create node: {result.Error}");
        }
    }

    private static void AddTransition(NetworkOptions options)
    {
        if (string.IsNullOrEmpty(options.InputId) || 
            string.IsNullOrEmpty(options.OutputId) || 
            string.IsNullOrEmpty(options.OperationName))
        {
            Console.WriteLine("Error: --input, --output, and --operation are required for adding a transition.");
            return;
        }

        if (!Guid.TryParse(options.InputId, out var inputGuid) || 
            !Guid.TryParse(options.OutputId, out var outputGuid))
        {
            Console.WriteLine("Error: Invalid node ID format.");
            return;
        }

        var edge = TransitionEdge.CreateSimple(
            inputGuid,
            outputGuid,
            options.OperationName,
            new { Timestamp = DateTimeOffset.UtcNow });

        var result = Dag.AddEdge(edge);

        if (result.IsSuccess)
        {
            Console.WriteLine($"✓ Created transition: {edge.Id}");
            Console.WriteLine($"  Operation: {edge.OperationName}");
            Console.WriteLine($"  Input: {edge.InputIds[0]}");
            Console.WriteLine($"  Output: {edge.OutputId}");
            Console.WriteLine($"  Hash: {edge.Hash}");
        }
        else
        {
            Console.WriteLine($"✗ Failed to create transition: {result.Error}");
        }
    }

    private static void ViewDag(NetworkOptions options)
    {
        Console.WriteLine($"DAG Statistics:");
        Console.WriteLine($"  Total Nodes: {Dag.NodeCount}");
        Console.WriteLine($"  Total Transitions: {Dag.EdgeCount}");
        Console.WriteLine();

        var rootNodes = Dag.GetRootNodes().ToList();
        var leafNodes = Dag.GetLeafNodes().ToList();

        Console.WriteLine($"Root Nodes ({rootNodes.Count}):");
        foreach (var node in rootNodes)
        {
            Console.WriteLine($"  - {node.Id} ({node.TypeName})");
        }

        Console.WriteLine();
        Console.WriteLine($"Leaf Nodes ({leafNodes.Count}):");
        foreach (var node in leafNodes)
        {
            Console.WriteLine($"  - {node.Id} ({node.TypeName})");
        }

        // Verify integrity
        var integrityResult = Dag.VerifyIntegrity();
        Console.WriteLine();
        if (integrityResult.IsSuccess)
        {
            Console.WriteLine("✓ DAG integrity verified");
        }
        else
        {
            Console.WriteLine($"✗ DAG integrity check failed: {integrityResult.Error}");
        }
    }

    private static void CreateAndDisplaySnapshot()
    {
        var snapshot = Projector.CreateSnapshot();

        Console.WriteLine($"Global Network State Snapshot:");
        Console.WriteLine($"  Epoch: {snapshot.Epoch}");
        Console.WriteLine($"  Timestamp: {snapshot.Timestamp}");
        Console.WriteLine($"  Total Nodes: {snapshot.TotalNodes}");
        Console.WriteLine($"  Total Transitions: {snapshot.TotalTransitions}");
        Console.WriteLine();

        Console.WriteLine("Nodes by Type:");
        foreach (var kvp in snapshot.NodeCountByType)
        {
            Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
        }

        Console.WriteLine();
        Console.WriteLine("Transitions by Operation:");
        foreach (var kvp in snapshot.TransitionCountByOperation)
        {
            Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
        }

        if (snapshot.AverageConfidence.HasValue)
        {
            Console.WriteLine();
            Console.WriteLine($"Average Confidence: {snapshot.AverageConfidence.Value:F2}");
        }

        if (snapshot.TotalProcessingTimeMs.HasValue)
        {
            Console.WriteLine($"Total Processing Time: {snapshot.TotalProcessingTimeMs.Value}ms");
        }
    }

    private static void ReplayTransitions(string nodeIdStr)
    {
        if (!Guid.TryParse(nodeIdStr, out var nodeId))
        {
            Console.WriteLine("Error: Invalid node ID format.");
            return;
        }

        var result = ReplayEngine.ReplayPathToNode(nodeId);

        if (result.IsSuccess)
        {
            Console.WriteLine($"Transition Path to Node {nodeId}:");
            Console.WriteLine();

            for (var i = 0; i < result.Value.Length; i++)
            {
                var edge = result.Value[i];
                Console.WriteLine($"Step {i + 1}: {edge.OperationName}");
                Console.WriteLine($"  ID: {edge.Id}");
                Console.WriteLine($"  Input: {edge.InputIds[0]}");
                Console.WriteLine($"  Output: {edge.OutputId}");
                Console.WriteLine($"  Created: {edge.CreatedAt}");
                Console.WriteLine();
            }
        }
        else
        {
            Console.WriteLine($"✗ Failed to replay: {result.Error}");
        }
    }

    private static void ListNodes(NetworkOptions options)
    {
        IEnumerable<MonadNode> nodes = Dag.Nodes.Values;

        if (!string.IsNullOrEmpty(options.TypeName))
        {
            nodes = Dag.GetNodesByType(options.TypeName);
            Console.WriteLine($"Nodes of type '{options.TypeName}':");
        }
        else
        {
            Console.WriteLine("All Nodes:");
        }

        Console.WriteLine();

        foreach (var node in nodes)
        {
            Console.WriteLine($"ID: {node.Id}");
            Console.WriteLine($"  Type: {node.TypeName}");
            Console.WriteLine($"  Created: {node.CreatedAt}");
            Console.WriteLine($"  Parents: {node.ParentIds.Length}");
            Console.WriteLine($"  Hash: {node.Hash[..16]}...");
            Console.WriteLine();
        }

        Console.WriteLine($"Total: {nodes.Count()} nodes");
    }

    private static void ListEdges()
    {
        Console.WriteLine("All Transitions:");
        Console.WriteLine();

        foreach (var edge in Dag.Edges.Values)
        {
            Console.WriteLine($"ID: {edge.Id}");
            Console.WriteLine($"  Operation: {edge.OperationName}");
            Console.WriteLine($"  Input(s): {string.Join(", ", edge.InputIds)}");
            Console.WriteLine($"  Output: {edge.OutputId}");
            Console.WriteLine($"  Created: {edge.CreatedAt}");
            if (edge.Confidence.HasValue)
            {
                Console.WriteLine($"  Confidence: {edge.Confidence.Value:F2}");
            }
            Console.WriteLine();
        }

        Console.WriteLine($"Total: {Dag.EdgeCount} transitions");
    }

    private static async Task RunInteractiveDemoAsync()
    {
        Console.WriteLine("=== Interactive Network State Demo ===");
        Console.WriteLine("Creating a reasoning chain: Draft → Critique → Improve → Final\n");

        // Create Draft node
        var draft = new Draft("Initial implementation of the feature");
        var draftNode = MonadNode.FromReasoningState(draft);
        Dag.AddNode(draftNode);
        Console.WriteLine($"1. Created Draft node: {draftNode.Id}");
        await Task.Delay(500);

        // Create Critique node
        var critique = new Critique("The implementation lacks error handling and edge case validation");
        var critiqueNode = MonadNode.FromReasoningState(critique, ImmutableArray.Create(draftNode.Id));
        Dag.AddNode(critiqueNode);
        Console.WriteLine($"2. Created Critique node: {critiqueNode.Id}");
        await Task.Delay(500);

        // Add transition: Draft → Critique
        var edge1 = TransitionEdge.CreateSimple(
            draftNode.Id,
            critiqueNode.Id,
            "UseCritique",
            new { Prompt = "Analyze and critique the draft" },
            confidence: 0.85,
            durationMs: 1200);
        Dag.AddEdge(edge1);
        Console.WriteLine($"3. Added transition: Draft → Critique");
        await Task.Delay(500);

        // Create Improved node
        var improved = new Draft("Enhanced implementation with comprehensive error handling and validation");
        var improvedNode = MonadNode.FromReasoningState(improved, ImmutableArray.Create(critiqueNode.Id));
        Dag.AddNode(improvedNode);
        Console.WriteLine($"4. Created Improved Draft node: {improvedNode.Id}");
        await Task.Delay(500);

        // Add transition: Critique → Improved
        var edge2 = TransitionEdge.CreateSimple(
            critiqueNode.Id,
            improvedNode.Id,
            "UseImprove",
            new { Prompt = "Improve based on critique" },
            confidence: 0.92,
            durationMs: 1500);
        Dag.AddEdge(edge2);
        Console.WriteLine($"5. Added transition: Critique → Improved");
        await Task.Delay(500);

        // Create Final node
        var final = new FinalSpec("Production-ready implementation with full error handling, validation, and documentation");
        var finalNode = MonadNode.FromReasoningState(final, ImmutableArray.Create(improvedNode.Id));
        Dag.AddNode(finalNode);
        Console.WriteLine($"6. Created Final node: {finalNode.Id}");
        await Task.Delay(500);

        // Add transition: Improved → Final
        var edge3 = TransitionEdge.CreateSimple(
            improvedNode.Id,
            finalNode.Id,
            "Finalize",
            new { Prompt = "Finalize the specification" },
            confidence: 0.95,
            durationMs: 800);
        Dag.AddEdge(edge3);
        Console.WriteLine($"7. Added transition: Improved → Final");
        Console.WriteLine();

        // Display DAG summary
        Console.WriteLine("=== DAG Summary ===");
        Console.WriteLine($"Total Nodes: {Dag.NodeCount}");
        Console.WriteLine($"Total Transitions: {Dag.EdgeCount}");
        Console.WriteLine();

        // Create snapshot
        Console.WriteLine("=== Global Network State Snapshot ===");
        var snapshot = Projector.CreateSnapshot(
            ImmutableDictionary<string, string>.Empty.Add("demo", "reasoning-chain"));
        
        Console.WriteLine($"Epoch: {snapshot.Epoch}");
        Console.WriteLine($"Total Nodes: {snapshot.TotalNodes}");
        Console.WriteLine($"Total Transitions: {snapshot.TotalTransitions}");
        Console.WriteLine($"Average Confidence: {snapshot.AverageConfidence:F2}");
        Console.WriteLine($"Total Processing Time: {snapshot.TotalProcessingTimeMs}ms");
        Console.WriteLine();

        Console.WriteLine("Nodes by Type:");
        foreach (var kvp in snapshot.NodeCountByType)
        {
            Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
        }
        Console.WriteLine();

        // Replay path
        Console.WriteLine("=== Replaying Path to Final Node ===");
        var replayResult = ReplayEngine.ReplayPathToNode(finalNode.Id);
        if (replayResult.IsSuccess)
        {
            for (var i = 0; i < replayResult.Value.Length; i++)
            {
                var edge = replayResult.Value[i];
                Console.WriteLine($"Step {i + 1}: {edge.OperationName} (Confidence: {edge.Confidence:F2})");
            }
        }

        Console.WriteLine();
        Console.WriteLine("✓ Demo completed successfully!");
    }
}
