// <copyright file="EmergentNetworkStateExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using Ouroboros.Domain.States;
using Ouroboros.Network;

namespace Ouroboros.Examples;

/// <summary>
/// Example demonstrating the Emergent Network State system.
/// Shows how to create nodes, record transitions, and project global state.
/// </summary>
public static class EmergentNetworkStateExample
{
    /// <summary>
    /// Demonstrates a complete reasoning chain from Draft to Final.
    /// </summary>
    public static void RunExample()
    {
        Console.WriteLine("=== Emergent Network State Example ===\n");

        // Initialize the DAG and supporting services
        var dag = new MerkleDag();
        var projector = new NetworkStateProjector(dag);
        var replayEngine = new TransitionReplayEngine(dag);

        // Step 1: Create initial draft
        Console.WriteLine("Step 1: Creating initial draft...");
        var draft = new Draft("Implement user authentication with basic password validation");
        var draftNode = MonadNode.FromReasoningState(draft);
        
        var addDraftResult = dag.AddNode(draftNode);
        if (addDraftResult.IsSuccess)
        {
            Console.WriteLine($"✓ Created draft node: {draftNode.Id}");
            Console.WriteLine($"  Hash: {draftNode.Hash[..16]}...\n");
        }

        // Step 2: Create critique
        Console.WriteLine("Step 2: Generating critique...");
        var critique = new Critique(
            "The draft lacks consideration for: " +
            "1. Multi-factor authentication " +
            "2. Password complexity requirements " +
            "3. Rate limiting against brute force " +
            "4. Secure password storage (hashing)");
        var critiqueNode = MonadNode.FromReasoningState(
            critique,
            ImmutableArray.Create(draftNode.Id));
        
        dag.AddNode(critiqueNode);
        Console.WriteLine($"✓ Created critique node: {critiqueNode.Id}\n");

        // Record the transition
        var critiqueTransition = TransitionEdge.CreateSimple(
            draftNode.Id,
            critiqueNode.Id,
            "UseCritique",
            new { 
                Prompt = "Analyze security implications",
                Model = "reasoning-agent-v1"
            },
            confidence: 0.87,
            durationMs: 1234);
        
        dag.AddEdge(critiqueTransition);
        Console.WriteLine("✓ Recorded Draft → Critique transition\n");

        // Step 3: Create improved draft
        Console.WriteLine("Step 3: Improving based on critique...");
        var improved = new Draft(
            "Implement user authentication with: " +
            "- bcrypt password hashing " +
            "- Configurable complexity requirements " +
            "- Rate limiting (max 5 attempts/minute) " +
            "- Optional MFA via TOTP");
        var improvedNode = MonadNode.FromReasoningState(
            improved,
            ImmutableArray.Create(critiqueNode.Id));
        
        dag.AddNode(improvedNode);
        Console.WriteLine($"✓ Created improved draft: {improvedNode.Id}\n");

        var improveTransition = TransitionEdge.CreateSimple(
            critiqueNode.Id,
            improvedNode.Id,
            "UseImprove",
            new { 
                Prompt = "Address critique points",
                Model = "reasoning-agent-v1"
            },
            confidence: 0.94,
            durationMs: 2156);
        
        dag.AddEdge(improveTransition);
        Console.WriteLine("✓ Recorded Critique → Improved transition\n");

        // Step 4: Create final specification
        Console.WriteLine("Step 4: Finalizing specification...");
        var final = new FinalSpec(
            "Production authentication system with bcrypt hashing (cost 12), " +
            "NIST-compliant password policy, Redis-backed rate limiting, " +
            "and TOTP MFA support with recovery codes");
        var finalNode = MonadNode.FromReasoningState(
            final,
            ImmutableArray.Create(improvedNode.Id));
        
        dag.AddNode(finalNode);
        Console.WriteLine($"✓ Created final spec: {finalNode.Id}\n");

        var finalizeTransition = TransitionEdge.CreateSimple(
            improvedNode.Id,
            finalNode.Id,
            "Finalize",
            new { 
                Prompt = "Create production specification",
                Model = "reasoning-agent-v1"
            },
            confidence: 0.96,
            durationMs: 1876);
        
        dag.AddEdge(finalizeTransition);
        Console.WriteLine("✓ Recorded Improved → Final transition\n");

        // Step 5: Create global state snapshot
        Console.WriteLine("=== Global Network State ===");
        var snapshot = projector.CreateSnapshot(
            ImmutableDictionary<string, string>.Empty
                .Add("project", "authentication-system")
                .Add("version", "1.0"));

        Console.WriteLine($"Epoch: {snapshot.Epoch}");
        Console.WriteLine($"Total Nodes: {snapshot.TotalNodes}");
        Console.WriteLine($"Total Transitions: {snapshot.TotalTransitions}");
        Console.WriteLine($"Average Confidence: {snapshot.AverageConfidence:F3}");
        Console.WriteLine($"Total Processing Time: {snapshot.TotalProcessingTimeMs}ms\n");

        Console.WriteLine("Nodes by Type:");
        foreach (var kvp in snapshot.NodeCountByType)
        {
            Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
        }

        Console.WriteLine();

        // Step 6: Replay the reasoning path
        Console.WriteLine("=== Reasoning Path Replay ===");
        var replayResult = replayEngine.ReplayPathToNode(finalNode.Id);
        
        if (replayResult.IsSuccess)
        {
            Console.WriteLine($"Successfully replayed path with {replayResult.Value.Length} transitions:\n");
            
            for (var i = 0; i < replayResult.Value.Length; i++)
            {
                var edge = replayResult.Value[i];
                Console.WriteLine($"{i + 1}. {edge.OperationName}");
                Console.WriteLine($"   Confidence: {edge.Confidence:F2}");
                Console.WriteLine($"   Duration: {edge.DurationMs}ms");
                Console.WriteLine($"   Hash: {edge.Hash[..16]}...");
                Console.WriteLine();
            }
        }

        // Step 7: Verify DAG integrity
        Console.WriteLine("=== Integrity Verification ===");
        var integrityResult = dag.VerifyIntegrity();
        
        if (integrityResult.IsSuccess)
        {
            Console.WriteLine("✓ DAG integrity verified successfully");
            Console.WriteLine("  - All node hashes are valid");
            Console.WriteLine("  - All edge hashes are valid");
            Console.WriteLine("  - No cycles detected");
        }
        else
        {
            Console.WriteLine($"✗ Integrity check failed: {integrityResult.Error}");
        }

        Console.WriteLine("\n=== Example Complete ===");
    }

    /// <summary>
    /// Demonstrates custom node types and parallel reasoning paths.
    /// </summary>
    public static void RunAdvancedExample()
    {
        Console.WriteLine("=== Advanced: Parallel Reasoning Paths ===\n");

        var dag = new MerkleDag();
        var projector = new NetworkStateProjector(dag);

        // Create a root problem statement
        var problem = MonadNode.FromPayload(
            "Problem",
            new { Description = "Design a distributed caching system" });
        dag.AddNode(problem);

        // Create two parallel exploration paths
        var approach1 = MonadNode.FromPayload(
            "Approach",
            new { Name = "Redis-based", Pros = new[] { "Simple", "Fast" } },
            ImmutableArray.Create(problem.Id));
        
        var approach2 = MonadNode.FromPayload(
            "Approach",
            new { Name = "Hazelcast-based", Pros = new[] { "Distributed", "Resilient" } },
            ImmutableArray.Create(problem.Id));

        dag.AddNode(approach1);
        dag.AddNode(approach2);

        // Add transitions
        dag.AddEdge(TransitionEdge.CreateSimple(
            problem.Id, approach1.Id, "Explore", new { Path = 1 }));
        dag.AddEdge(TransitionEdge.CreateSimple(
            problem.Id, approach2.Id, "Explore", new { Path = 2 }));

        // Create synthesis node
        var synthesis = MonadNode.FromPayload(
            "Synthesis",
            new { 
                Recommendation = "Use Redis for hot cache, Hazelcast for distributed state",
                InputApproaches = 2
            },
            ImmutableArray.Create(approach1.Id, approach2.Id));
        
        dag.AddNode(synthesis);

        // Multi-input transition
        dag.AddEdge(TransitionEdge.Create(
            ImmutableArray.Create(approach1.Id, approach2.Id),
            synthesis.Id,
            "Synthesize",
            new { Strategy = "Best-of-both" },
            confidence: 0.89));

        // Project state
        var state = projector.ProjectCurrentState();
        Console.WriteLine($"Parallel paths: {state.RootNodeIds.Length} roots → {state.LeafNodeIds.Length} leaves");
        Console.WriteLine($"Multi-input transitions: {dag.Edges.Values.Count(e => e.InputIds.Length > 1)}");
        
        Console.WriteLine("\n✓ Advanced example complete");
    }
}
