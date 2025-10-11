// ==========================================================
// Example: Epic #120 - Production-ready Release v1.0
// Demonstrates using EpicBranchOrchestrator to manage sub-issues
// ==========================================================

using LangChainPipeline.Agent.MetaAI;
using LangChainPipeline.Core.Monads;

namespace LangChainPipeline.Examples.EpicWorkflow;

/// <summary>
/// Example demonstrating epic workflow with automatic agent assignment and branch creation
/// for each sub-issue in Epic #120.
/// </summary>
public static class Epic120Example
{
    /// <summary>
    /// Demonstrates the complete workflow for Epic #120 with all its sub-issues.
    /// </summary>
    public static async Task RunEpic120WorkflowAsync()
    {
        Console.WriteLine("=== Epic #120: Production-ready Release v1.0 ===\n");

        // Initialize distributed orchestrator and epic branch orchestrator
        var safetyGuard = new SafetyGuard(PermissionLevel.Isolated);
        var distributor = new DistributedOrchestrator(safetyGuard);
        var epicOrchestrator = new EpicBranchOrchestrator(
            distributor,
            new EpicBranchConfig(
                BranchPrefix: "epic-120",
                AgentPoolPrefix: "v1.0-agent",
                AutoCreateBranches: true,
                AutoAssignAgents: true,
                MaxConcurrentSubIssues: 5));

        // Epic #120 sub-issues (based on the actual GitHub issues)
        var subIssueNumbers = new List<int>
        {
            121, // Inventory Current State
            122, // Build Dependency Graph
            123, // Milestone Date Proposal
            124, // Risk Register Creation
            125, // Tracking Dashboard Setup
            126, // Weekly Status Automation
            127, // Inventory Current State (duplicate)
            128, // Build Dependency Graph (duplicate)
            129, // Milestone Date Proposal (duplicate)
            130, // Risk Register Creation (duplicate)
            131, // Tracking Dashboard Setup (duplicate)
            132, // Weekly Status Automation (duplicate)
            133, // Aggregate Existing Discussions
            134, // Define Must-Have Feature List
            135, // Non-Functional Requirements (NFRs)
            136, // KPIs & Acceptance Criteria
            137, // Stakeholder Review Loop
            138, // Lock & Tag Scope
            139, // Inventory Current State (duplicate)
            140, // Build Dependency Graph (duplicate)
            141, // Milestone Date Proposal (duplicate)
            142, // Risk Register Creation (duplicate)
            143, // Tracking Dashboard Setup (duplicate)
            144, // Weekly Status Automation (duplicate)
            145, // Aggregate Existing Discussions (duplicate)
            146, // Define Must-Have Feature List (duplicate)
            147, // Non-Functional Requirements (NFRs) (duplicate)
            148, // KPIs & Acceptance Criteria (duplicate)
            149, // Stakeholder Review Loop (duplicate)
            150  // Lock & Tag Scope (duplicate)
        };

        // Register the epic
        Console.WriteLine("Registering Epic #120...");
        var epicResult = await epicOrchestrator.RegisterEpicAsync(
            120,
            "🚀 Production-ready Release v1.0",
            "This epic tracks every task required to ship the first production-ready release of MonadicPipeline.",
            subIssueNumbers);

        if (!epicResult.IsSuccess)
        {
            Console.WriteLine($"❌ Failed to register epic: {epicResult.Error}");
            return;
        }

        Console.WriteLine($"✅ Epic registered successfully with {subIssueNumbers.Count} sub-issues\n");

        // Display all sub-issue assignments
        Console.WriteLine("Sub-issue Assignments:");
        Console.WriteLine("======================");
        var assignments = epicOrchestrator.GetSubIssueAssignments(120);
        foreach (var assignment in assignments.OrderBy(a => a.IssueNumber))
        {
            Console.WriteLine($"  Issue #{assignment.IssueNumber}:");
            Console.WriteLine($"    Agent: {assignment.AssignedAgentId}");
            Console.WriteLine($"    Branch: {assignment.BranchName}");
            Console.WriteLine($"    Status: {assignment.Status}");
            Console.WriteLine();
        }

        // Demonstrate executing work on a specific sub-issue
        Console.WriteLine("\nExecuting work on Sub-issue #121 (Inventory Current State)...");
        var executionResult = await epicOrchestrator.ExecuteSubIssueAsync(
            120,
            121,
            async assignment =>
            {
                Console.WriteLine($"  🔨 Agent {assignment.AssignedAgentId} working on branch {assignment.BranchName}...");

                // Simulate work
                await Task.Delay(1000);

                // Update the branch with reasoning
                if (assignment.Branch != null)
                {
                    var updatedBranch = assignment.Branch.WithIngestEvent(
                        "baseline-inventory",
                        new[] { "doc1", "doc2", "doc3" });

                    var updatedAssignment = assignment with { Branch = updatedBranch };
                    Console.WriteLine($"  ✅ Work completed! Branch now has {updatedBranch.Events.Count} events.");
                    return Result<SubIssueAssignment, string>.Success(updatedAssignment);
                }

                return Result<SubIssueAssignment, string>.Success(assignment);
            });

        if (executionResult.IsSuccess)
        {
            Console.WriteLine($"✅ Sub-issue #121 completed successfully!");
        }
        else
        {
            Console.WriteLine($"❌ Sub-issue #121 failed: {executionResult.Error}");
        }

        // Display final status
        Console.WriteLine("\n=== Final Epic Status ===");
        var finalAssignments = epicOrchestrator.GetSubIssueAssignments(120);
        var completedCount = finalAssignments.Count(a => a.Status == SubIssueStatus.Completed);
        var inProgressCount = finalAssignments.Count(a => a.Status == SubIssueStatus.InProgress);
        var failedCount = finalAssignments.Count(a => a.Status == SubIssueStatus.Failed);
        var pendingCount = finalAssignments.Count(a => a.Status == SubIssueStatus.Pending || a.Status == SubIssueStatus.BranchCreated);

        Console.WriteLine($"Total Sub-issues: {finalAssignments.Count}");
        Console.WriteLine($"  ✅ Completed: {completedCount}");
        Console.WriteLine($"  🔄 In Progress: {inProgressCount}");
        Console.WriteLine($"  ❌ Failed: {failedCount}");
        Console.WriteLine($"  ⏳ Pending: {pendingCount}");

        // Display agent information
        Console.WriteLine("\n=== Registered Agents ===");
        var agents = distributor.GetAgentStatus();
        foreach (var agent in agents.Take(5)) // Show first 5 agents
        {
            Console.WriteLine($"  {agent.Name} (ID: {agent.AgentId})");
            Console.WriteLine($"    Status: {agent.Status}");
            Console.WriteLine($"    Capabilities: {string.Join(", ", agent.Capabilities)}");
            Console.WriteLine($"    Last Heartbeat: {agent.LastHeartbeat:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine();
        }

        if (agents.Count > 5)
        {
            Console.WriteLine($"  ... and {agents.Count - 5} more agents");
        }
    }

    /// <summary>
    /// Demonstrates working with a subset of sub-issues in parallel.
    /// </summary>
    public static async Task RunParallelSubIssuesAsync()
    {
        Console.WriteLine("=== Parallel Sub-issue Execution ===\n");

        var safetyGuard = new SafetyGuard(PermissionLevel.Isolated);
        var distributor = new DistributedOrchestrator(safetyGuard);
        var epicOrchestrator = new EpicBranchOrchestrator(distributor);

        // Register epic with a subset of issues
        var subIssues = new List<int> { 121, 122, 123, 124, 125 };
        await epicOrchestrator.RegisterEpicAsync(
            120,
            "Production-ready Release v1.0",
            "Epic #120",
            subIssues);

        Console.WriteLine("Executing 5 sub-issues in parallel...\n");

        // Execute multiple sub-issues concurrently
        var tasks = subIssues.Select(async issueNumber =>
        {
            return await epicOrchestrator.ExecuteSubIssueAsync(
                120,
                issueNumber,
                async assignment =>
                {
                    Console.WriteLine($"  Starting work on issue #{issueNumber} in agent {assignment.AssignedAgentId}");
                    await Task.Delay(Random.Shared.Next(500, 2000)); // Simulate varying work time
                    Console.WriteLine($"  Completed issue #{issueNumber}");
                    return Result<SubIssueAssignment, string>.Success(assignment);
                });
        });

        var results = await Task.WhenAll(tasks);

        Console.WriteLine("\n=== Results ===");
        var successCount = results.Count(r => r.IsSuccess);
        Console.WriteLine($"Successful: {successCount}/{results.Length}");
        Console.WriteLine($"Failed: {results.Length - successCount}/{results.Length}");
    }

    /// <summary>
    /// Main entry point for the example.
    /// </summary>
    public static async Task Main(string[] args)
    {
        try
        {
            await RunEpic120WorkflowAsync();

            Console.WriteLine("\n" + new string('=', 60) + "\n");

            await RunParallelSubIssuesAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}
