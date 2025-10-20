// ==========================================================
// Example: Issue #138 - Lock & Tag Scope
// Demonstrates using GitHubScopeLockTool to prevent scope creep
// ==========================================================

using LangChainPipeline.Agent.MetaAI;
using LangChainPipeline.Core.Monads;
using LangChainPipeline.Tools;

namespace LangChainPipeline.Examples.EpicWorkflow;

/// <summary>
/// Example demonstrating Issue #138 workflow for locking scope to prevent uncontrolled scope creep.
/// This shows how to use the GitHubScopeLockTool to add scope-locked labels and comments to GitHub issues.
/// </summary>
public static class Issue138ScopeLockExample
{
    /// <summary>
    /// Demonstrates the complete workflow for Issue #138 (Lock and Tag Scope).
    /// </summary>
    /// <param name="githubToken">GitHub personal access token with repo permissions.</param>
    /// <param name="owner">Repository owner (username or organization).</param>
    /// <param name="repo">Repository name.</param>
    public static async Task RunScopeLockWorkflowAsync(string githubToken, string owner, string repo)
    {
        Console.WriteLine("=== Issue #138: Lock & Tag Scope ===\n");
        Console.WriteLine("Goal: Prevent uncontrolled scope creep by locking issue scope\n");

        // Step 1: Initialize the Epic Branch Orchestrator
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

        // Step 2: Register Epic #120 with Issue #138
        Console.WriteLine("Registering Epic #120 with Issue #138...");
        var epicResult = await epicOrchestrator.RegisterEpicAsync(
            120,
            "🚀 Production-ready Release v1.0",
            "This epic tracks every task required to ship the first production-ready release",
            new List<int> { 138 });

        if (!epicResult.IsSuccess)
        {
            Console.WriteLine($"❌ Failed to register epic: {epicResult.Error}");
            return;
        }

        Console.WriteLine("✅ Epic registered successfully\n");

        // Step 3: Create the GitHubScopeLockTool
        Console.WriteLine("Initializing GitHubScopeLockTool...");
        var scopeLockTool = new GitHubScopeLockTool(githubToken, owner, repo);
        Console.WriteLine($"✅ Tool initialized for repository: {owner}/{repo}\n");

        // Step 4: Execute the scope lock workflow for Issue #138
        Console.WriteLine("Executing scope lock workflow for Issue #138...");
        var executionResult = await epicOrchestrator.ExecuteSubIssueAsync(
            120,
            138,
            async assignment =>
            {
                Console.WriteLine($"  🔨 Agent {assignment.AssignedAgentId} working on {assignment.BranchName}...");

                // Simulate: Verify that specs have been merged (prerequisite)
                Console.WriteLine("  📋 Verifying prerequisites:");
                Console.WriteLine("    - Must-Have Feature List (Issue #134): ✅ Completed");
                Console.WriteLine("    - Non-Functional Requirements (Issue #135): ✅ Completed");
                Console.WriteLine("    - KPIs & Acceptance Criteria (Issue #136): ✅ Completed");
                Console.WriteLine("    - Stakeholder Review Loop (Issue #137): ✅ Completed");
                Console.WriteLine("    - All specifications merged and reviewed: ✅ Ready\n");

                // Apply scope lock to issue #2 (example issue for scope locking)
                Console.WriteLine("  🔒 Applying scope lock to issue #2...");
                var lockArgs = System.Text.Json.JsonSerializer.Serialize(new
                {
                    IssueNumber = 2,
                    Milestone = "v1.0"
                });

                var lockResult = await scopeLockTool.InvokeAsync(lockArgs);

                if (lockResult.IsSuccess)
                {
                    Console.WriteLine($"  ✅ Scope lock applied successfully:");
                    Console.WriteLine($"     {lockResult.Value}");
                }
                else
                {
                    Console.WriteLine($"  ❌ Scope lock failed: {lockResult.Error}");
                    return Result<SubIssueAssignment, string>.Failure($"Scope lock failed: {lockResult.Error}");
                }

                // Update the branch with the scope lock event
                if (assignment.Branch != null)
                {
                    var updatedBranch = assignment.Branch.WithIngestEvent(
                        "scope-lock-applied",
                        new[] { $"issue-2-locked", "milestone-v1.0", "label-scope-locked-added" });

                    updatedBranch = updatedBranch.WithReasoning(
                        new Domain.States.FinalSpec(
                            "Scope formally locked for v1.0 release. " +
                            "Issue #2 tagged with 'scope-locked' label and milestone updated to v1.0. " +
                            "Confirmation comment posted. No further scope changes allowed without explicit approval."),
                        "Apply scope lock and finalize v1.0 requirements",
                        null);

                    var updatedAssignment = assignment with { Branch = updatedBranch };
                    Console.WriteLine($"  ✅ Branch updated with scope lock event\n");
                    return Result<SubIssueAssignment, string>.Success(updatedAssignment);
                }

                return Result<SubIssueAssignment, string>.Success(assignment);
            });

        if (executionResult.IsSuccess)
        {
            Console.WriteLine("✅ Issue #138 (Lock & Tag Scope) completed successfully!");
            Console.WriteLine("\n📊 Results:");
            Console.WriteLine("  - Label 'scope-locked' added to issue #2");
            Console.WriteLine("  - Confirmation comment posted");
            Console.WriteLine("  - Milestone updated to v1.0");
            Console.WriteLine("  - Epic #120 milestone row updated");
            Console.WriteLine("\n🎯 Outcome: Scope is now formally locked to prevent uncontrolled scope creep");
        }
        else
        {
            Console.WriteLine($"❌ Issue #138 failed: {executionResult.Error}");
        }
    }

    /// <summary>
    /// Demonstrates how to use the GitHubScopeLockTool directly without the epic orchestrator.
    /// </summary>
    /// <param name="githubToken">GitHub personal access token with repo permissions.</param>
    /// <param name="owner">Repository owner (username or organization).</param>
    /// <param name="repo">Repository name.</param>
    public static async Task RunDirectScopeLockAsync(string githubToken, string owner, string repo)
    {
        Console.WriteLine("=== Direct Scope Lock Example ===\n");

        // Create the tool
        var scopeLockTool = new GitHubScopeLockTool(githubToken, owner, repo);

        // Lock scope for issue #2
        Console.WriteLine("Locking scope for issue #2...");
        var lockArgs = System.Text.Json.JsonSerializer.Serialize(new
        {
            IssueNumber = 2,
            Milestone = "v1.0"
        });

        var result = await scopeLockTool.InvokeAsync(lockArgs);

        if (result.IsSuccess)
        {
            Console.WriteLine("✅ Success!");
            Console.WriteLine(result.Value);
        }
        else
        {
            Console.WriteLine($"❌ Failed: {result.Error}");
        }
    }

    /// <summary>
    /// Main entry point for the example.
    /// Note: Requires GITHUB_TOKEN environment variable to be set.
    /// </summary>
    public static async Task Main(string[] args)
    {
        try
        {
            // Get GitHub credentials from environment
            string? githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            string owner = args.Length > 0 ? args[0] : "PMeeske";
            string repo = args.Length > 1 ? args[1] : "MonadicPipeline";

            if (string.IsNullOrEmpty(githubToken))
            {
                Console.WriteLine("⚠️  GITHUB_TOKEN environment variable not set.");
                Console.WriteLine("   This example requires a GitHub personal access token with 'repo' permissions.");
                Console.WriteLine("\nTo run this example:");
                Console.WriteLine("  1. Create a GitHub token at: https://github.com/settings/tokens");
                Console.WriteLine("  2. Set the environment variable: export GITHUB_TOKEN=your_token_here");
                Console.WriteLine("  3. Run the example again\n");
                Console.WriteLine("Proceeding with demonstration (will fail at GitHub API calls)...\n");
                githubToken = "demo-token-not-valid";
            }

            // Run the complete workflow
            await RunScopeLockWorkflowAsync(githubToken, owner, repo);

            Console.WriteLine("\n" + new string('=', 60) + "\n");

            // Optional: Run direct scope lock example
            if (!string.IsNullOrEmpty(githubToken) && githubToken != "demo-token-not-valid")
            {
                await RunDirectScopeLockAsync(githubToken, owner, repo);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
}
