# Epic #120 Integration Guide

This guide demonstrates how to use the Epic Branch Orchestration system to manage Epic #120 (Production-ready Release v1.0) with its sub-issues.

## Overview

Epic #120 has multiple sub-issues (121-150) that need to be coordinated across different work streams. The Epic Branch Orchestration system provides:

1. **Automatic Agent Assignment**: Each sub-issue gets its own dedicated agent
2. **Isolated Branches**: Each sub-issue has its own `PipelineBranch` for tracking work
3. **Status Tracking**: Monitor progress of all sub-issues
4. **Parallel Execution**: Work on multiple sub-issues simultaneously

## Quick Start

### Step 1: Initialize the System

```csharp
using LangChainPipeline.Agent.MetaAI;
using LangChainPipeline.Core.Monads;

// Create safety guard and distributed orchestrator
var safetyGuard = new SafetyGuard(PermissionLevel.Isolated);
var distributor = new DistributedOrchestrator(safetyGuard);

// Create epic orchestrator with custom configuration
var epicOrchestrator = new EpicBranchOrchestrator(
    distributor,
    new EpicBranchConfig(
        BranchPrefix: "epic-120",           // Branches will be epic-120/sub-issue-XXX
        AgentPoolPrefix: "v1.0-agent",      // Agents will be v1.0-agent-120-XXX
        AutoCreateBranches: true,           // Automatically create branches
        AutoAssignAgents: true,             // Automatically assign agents
        MaxConcurrentSubIssues: 5           // Limit concurrent executions
    ));
```

### Step 2: Register Epic #120

```csharp
// Define all sub-issues for Epic #120
var subIssues = new List<int>
{
    121, 122, 123, 124, 125, 126, 127, 128, 129, 130,
    131, 132, 133, 134, 135, 136, 137, 138, 139, 140,
    141, 142, 143, 144, 145, 146, 147, 148, 149, 150
};

// Register the epic
var epicResult = await epicOrchestrator.RegisterEpicAsync(
    120,
    "🚀 Production-ready Release v1.0",
    "This epic tracks every task required to ship the first production-ready release",
    subIssues
);

if (epicResult.IsSuccess)
{
    Console.WriteLine($"✅ Registered Epic #120 with {subIssues.Count} sub-issues");
    Console.WriteLine($"   Agents and branches automatically created for each sub-issue");
}
```

### Step 3: View All Assignments

```csharp
// Get all sub-issue assignments
var assignments = epicOrchestrator.GetSubIssueAssignments(120);

Console.WriteLine("\n📋 Sub-issue Assignments:");
foreach (var assignment in assignments.OrderBy(a => a.IssueNumber))
{
    Console.WriteLine($"  Issue #{assignment.IssueNumber}:");
    Console.WriteLine($"    Agent: {assignment.AssignedAgentId}");
    Console.WriteLine($"    Branch: {assignment.BranchName}");
    Console.WriteLine($"    Status: {assignment.Status}");
}
```

### Step 4: Work on Specific Sub-Issues

#### Example: Issue #121 - Inventory Current State

```csharp
var result = await epicOrchestrator.ExecuteSubIssueAsync(
    120,  // Epic number
    121,  // Sub-issue number
    async assignment =>
    {
        Console.WriteLine($"🔨 Working on {assignment.BranchName}...");
        
        // Perform actual work
        if (assignment.Branch != null)
        {
            // Example: Inventory existing tests
            var testFiles = Directory.GetFiles("tests", "*.cs", SearchOption.AllDirectories);
            
            // Record the inventory in the branch
            var updatedBranch = assignment.Branch.WithIngestEvent(
                "test-inventory",
                testFiles.Select(f => Path.GetFileName(f))
            );
            
            // Add reasoning about the inventory
            updatedBranch = updatedBranch.WithReasoning(
                new Draft($"Found {testFiles.Length} test files"),
                "Inventory test coverage",
                null
            );
            
            var updatedAssignment = assignment with { Branch = updatedBranch };
            return Result<SubIssueAssignment, string>.Success(updatedAssignment);
        }
        
        return Result<SubIssueAssignment, string>.Success(assignment);
    }
);

if (result.IsSuccess)
{
    Console.WriteLine("✅ Sub-issue #121 completed!");
}
```

#### Example: Issue #133 - Aggregate Existing Discussions

```csharp
var result = await epicOrchestrator.ExecuteSubIssueAsync(
    120,
    133,
    async assignment =>
    {
        // Fetch GitHub discussions (pseudo-code)
        var discussions = await FetchGitHubDiscussions();
        
        if (assignment.Branch != null)
        {
            var updatedBranch = assignment.Branch.WithIngestEvent(
                "github-discussions",
                discussions.Select(d => d.Title)
            );
            
            updatedBranch = updatedBranch.WithReasoning(
                new FinalSpec($"Aggregated {discussions.Count} discussions"),
                "Review and categorize discussions",
                null
            );
            
            var updatedAssignment = assignment with { Branch = updatedBranch };
            return Result<SubIssueAssignment, string>.Success(updatedAssignment);
        }
        
        return Result<SubIssueAssignment, string>.Success(assignment);
    }
);
```

#### Example: Issue #138 - Lock & Tag Scope

```csharp
// Requires GitHub token with repo permissions
string githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? "your-token-here";
var scopeLockTool = new GitHubScopeLockTool(githubToken, "PMeeske", "Ouroboros");

var result = await epicOrchestrator.ExecuteSubIssueAsync(
    120,
    138,
    async assignment =>
    {
        // Verify prerequisites are completed
        Console.WriteLine("Verifying prerequisites...");
        // - Issue #134: Must-Have Feature List
        // - Issue #135: Non-Functional Requirements
        // - Issue #136: KPIs & Acceptance Criteria
        // - Issue #137: Stakeholder Review Loop
        
        if (assignment.Branch != null)
        {
            // Apply scope lock to issue #2
            var lockArgs = System.Text.Json.JsonSerializer.Serialize(new
            {
                IssueNumber = 2,
                Milestone = "v1.0"
            });
            
            var lockResult = await scopeLockTool.InvokeAsync(lockArgs);
            
            if (!lockResult.IsSuccess)
            {
                return Result<SubIssueAssignment, string>.Failure(
                    $"Scope lock failed: {lockResult.Error}");
            }
            
            // Record the scope lock in the branch
            var updatedBranch = assignment.Branch.WithIngestEvent(
                "scope-lock-applied",
                new[] { "issue-2-locked", "milestone-v1.0", "label-scope-locked" }
            );
            
            updatedBranch = updatedBranch.WithReasoning(
                new FinalSpec(
                    "Scope formally locked for v1.0 release. " +
                    "Issue #2 tagged with 'scope-locked' label. " +
                    "Confirmation comment posted. " +
                    "No further scope changes allowed without explicit approval."),
                "Apply scope lock to prevent scope creep",
                null
            );
            
            var updatedAssignment = assignment with { Branch = updatedBranch };
            return Result<SubIssueAssignment, string>.Success(updatedAssignment);
        }
        
        return Result<SubIssueAssignment, string>.Success(assignment);
    }
);
```

### Step 5: Parallel Execution

Work on multiple sub-issues simultaneously:

```csharp
// Group sub-issues by category
var infrastructureTasks = new[] { 121, 122, 125 };  // Inventory, Dependencies, Dashboard
var requirementsTasks = new[] { 133, 134, 135 };    // Discussions, Features, NFRs

// Execute infrastructure tasks in parallel
var infrastructureTasks = infrastructureTasks.Select(async issueNumber =>
{
    return await epicOrchestrator.ExecuteSubIssueAsync(
        120,
        issueNumber,
        async assignment =>
        {
            // Infrastructure-specific work
            await PerformInfrastructureWork(assignment);
            return Result<SubIssueAssignment, string>.Success(assignment);
        });
});

var infrastructureResults = await Task.WhenAll(infrastructureTasks);

// Execute requirements tasks in parallel
var requirementsTasksList = requirementsTasks.Select(async issueNumber =>
{
    return await epicOrchestrator.ExecuteSubIssueAsync(
        120,
        issueNumber,
        async assignment =>
        {
            // Requirements-specific work
            await PerformRequirementsWork(assignment);
            return Result<SubIssueAssignment, string>.Success(assignment);
        });
});

var requirementsResults = await Task.WhenAll(requirementsTasksList);

// Report overall progress
Console.WriteLine($"\n📊 Progress Report:");
Console.WriteLine($"  Infrastructure: {infrastructureResults.Count(r => r.IsSuccess)}/{infrastructureResults.Length}");
Console.WriteLine($"  Requirements: {requirementsResults.Count(r => r.IsSuccess)}/{requirementsResults.Length}");
```

### Step 6: Monitor Progress

```csharp
// Get current status
var allAssignments = epicOrchestrator.GetSubIssueAssignments(120);

var stats = new
{
    Total = allAssignments.Count,
    Completed = allAssignments.Count(a => a.Status == SubIssueStatus.Completed),
    InProgress = allAssignments.Count(a => a.Status == SubIssueStatus.InProgress),
    Failed = allAssignments.Count(a => a.Status == SubIssueStatus.Failed),
    Pending = allAssignments.Count(a => 
        a.Status == SubIssueStatus.Pending || 
        a.Status == SubIssueStatus.BranchCreated)
};

Console.WriteLine("\n📈 Epic #120 Status:");
Console.WriteLine($"  Total Sub-issues: {stats.Total}");
Console.WriteLine($"  ✅ Completed: {stats.Completed} ({stats.Completed * 100 / stats.Total}%)");
Console.WriteLine($"  🔄 In Progress: {stats.InProgress}");
Console.WriteLine($"  ❌ Failed: {stats.Failed}");
Console.WriteLine($"  ⏳ Pending: {stats.Pending}");

// Show failed issues
var failedIssues = allAssignments.Where(a => a.Status == SubIssueStatus.Failed);
if (failedIssues.Any())
{
    Console.WriteLine("\n❌ Failed Issues:");
    foreach (var issue in failedIssues)
    {
        Console.WriteLine($"  Issue #{issue.IssueNumber}: {issue.ErrorMessage}");
    }
}
```

## Sub-Issue Workflow Patterns

### Pattern 1: Data Collection and Analysis

```csharp
async Task<Result<SubIssueAssignment, string>> DataCollectionWorkflow(SubIssueAssignment assignment)
{
    if (assignment.Branch == null)
        return Result<SubIssueAssignment, string>.Failure("No branch available");
    
    // Step 1: Collect data
    var data = await CollectData();
    var branch = assignment.Branch.WithIngestEvent(
        "data-collection",
        data.Select(d => d.Id)
    );
    
    // Step 2: Analyze data
    var analysis = AnalyzeData(data);
    branch = branch.WithReasoning(
        new Draft(analysis),
        "Data analysis",
        null
    );
    
    // Step 3: Generate recommendations
    var recommendations = GenerateRecommendations(analysis);
    branch = branch.WithReasoning(
        new FinalSpec(recommendations),
        "Generate recommendations",
        null
    );
    
    return Result<SubIssueAssignment, string>.Success(
        assignment with { Branch = branch }
    );
}
```

### Pattern 2: Document Review and Synthesis

```csharp
async Task<Result<SubIssueAssignment, string>> DocumentReviewWorkflow(SubIssueAssignment assignment)
{
    if (assignment.Branch == null)
        return Result<SubIssueAssignment, string>.Failure("No branch available");
    
    // Step 1: Load documents
    var documents = await LoadDocuments();
    var branch = assignment.Branch.WithIngestEvent(
        "document-review",
        documents.Select(d => d.Title)
    );
    
    // Step 2: Extract key points
    var keyPoints = ExtractKeyPoints(documents);
    branch = branch.WithReasoning(
        new Draft(string.Join("\n", keyPoints)),
        "Extract key points",
        null
    );
    
    // Step 3: Synthesize findings
    var synthesis = SynthesizeFindings(keyPoints);
    branch = branch.WithReasoning(
        new FinalSpec(synthesis),
        "Synthesize findings",
        null
    );
    
    return Result<SubIssueAssignment, string>.Success(
        assignment with { Branch = branch }
    );
}
```

### Pattern 3: Automated Task Execution

```csharp
async Task<Result<SubIssueAssignment, string>> AutomatedTaskWorkflow(SubIssueAssignment assignment)
{
    if (assignment.Branch == null)
        return Result<SubIssueAssignment, string>.Failure("No branch available");
    
    var branch = assignment.Branch;
    
    try
    {
        // Step 1: Validate prerequisites
        var validation = ValidatePrerequisites();
        branch = branch.WithReasoning(
            new Draft($"Prerequisites: {validation}"),
            "Validate prerequisites",
            null
        );
        
        // Step 2: Execute automated tasks
        var results = await ExecuteAutomatedTasks();
        branch = branch.WithIngestEvent(
            "task-execution",
            results.Select(r => r.TaskId)
        );
        
        // Step 3: Verify results
        var verification = VerifyResults(results);
        branch = branch.WithReasoning(
            new FinalSpec(verification),
            "Verify results",
            null
        );
        
        return Result<SubIssueAssignment, string>.Success(
            assignment with { Branch = branch }
        );
    }
    catch (Exception ex)
    {
        return Result<SubIssueAssignment, string>.Failure(
            $"Task execution failed: {ex.Message}"
        );
    }
}
```

### Pattern 4: Scope Locking Workflow (Issue #138)

The scope locking pattern is used to formally lock the scope of a release to prevent uncontrolled scope creep. This pattern uses the `GitHubScopeLockTool` to:

1. Add a "scope-locked" label to the issue
2. Post a confirmation comment explaining the scope lock
3. Update the milestone to track the locked scope
4. Record the scope lock event in the pipeline branch

```csharp
async Task<Result<SubIssueAssignment, string>> ScopeLockingWorkflow(
    SubIssueAssignment assignment,
    GitHubScopeLockTool scopeLockTool,
    int issueNumber,
    string milestone)
{
    if (assignment.Branch == null)
        return Result<SubIssueAssignment, string>.Failure("No branch available");
    
    var branch = assignment.Branch;
    
    // Step 1: Validate prerequisites (all specs merged and reviewed)
    var validationResult = ValidatePrerequisites();
    if (!validationResult.IsSuccess)
    {
        return Result<SubIssueAssignment, string>.Failure(
            $"Prerequisites not met: {validationResult.Error}");
    }
    
    // Step 2: Apply scope lock to the GitHub issue
    var lockArgs = System.Text.Json.JsonSerializer.Serialize(new
    {
        IssueNumber = issueNumber,
        Milestone = milestone
    });
    
    var lockResult = await scopeLockTool.InvokeAsync(lockArgs);
    if (!lockResult.IsSuccess)
    {
        return Result<SubIssueAssignment, string>.Failure(
            $"Scope lock failed: {lockResult.Error}");
    }
    
    // Step 3: Record the scope lock in the branch
    branch = branch.WithIngestEvent(
        "scope-lock-applied",
        new[] { $"issue-{issueNumber}-locked", $"milestone-{milestone}", "label-scope-locked" }
    );
    
    // Step 4: Generate final specification documenting the lock
    var scopeSpec = $"Scope formally locked for {milestone} release. " +
                   $"Issue #{issueNumber} tagged with 'scope-locked' label. " +
                   $"No further scope changes allowed without explicit approval. " +
                   $"Change control process must be followed for any modifications.";
    
    branch = branch.WithReasoning(
        new FinalSpec(scopeSpec),
        "Apply scope lock to prevent scope creep",
        null
    );
    
    return Result<SubIssueAssignment, string>.Success(
        assignment with { Branch = branch }
    );
}

// Usage example for Issue #138
var githubToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
var scopeLockTool = new GitHubScopeLockTool(githubToken, "PMeeske", "Ouroboros");

var result = await epicOrchestrator.ExecuteSubIssueAsync(
    120,
    138,
    async assignment => await ScopeLockingWorkflow(
        assignment,
        scopeLockTool,
        issueNumber: 2,
        milestone: "v1.0"
    )
);
```

**Key Benefits:**
- **Prevents Scope Creep**: Formal mechanism to lock requirements
- **Transparency**: GitHub label and comment visible to all stakeholders
- **Traceability**: Scope lock event recorded in pipeline branch
- **Change Control**: Establishes process for handling scope change requests

## Epic #120 Sub-Issue Categories

### Project Management (121-126, 139-144)
- **#121, #127, #139**: Inventory Current State
- **#122, #128, #140**: Build Dependency Graph
- **#123, #129, #141**: Milestone Date Proposal
- **#124, #130, #142**: Risk Register Creation
- **#125, #131, #143**: Tracking Dashboard Setup
- **#126, #132, #144**: Weekly Status Automation

### Requirements & Scope (133-138, 145-150)
- **#133, #145**: Aggregate Existing Discussions
- **#134, #146**: Define Must-Have Feature List
- **#135, #147**: Non-Functional Requirements (NFRs) → [v1.0-nfr.md](specs/v1.0-nfr.md)
- **#136, #148**: KPIs & Acceptance Criteria
- **#137, #149**: Stakeholder Review Loop → [STAKEHOLDER_REVIEW_LOOP.md](STAKEHOLDER_REVIEW_LOOP.md) ✅
- **#138, #150**: Lock & Tag Scope

## Best Practices

1. **Group Related Sub-Issues**: Execute related sub-issues together for efficiency
2. **Use Functional Updates**: Always use `with` expressions to update assignments
3. **Track Progress in Branches**: Use `WithReasoning()` and `WithIngestEvent()` to record work
4. **Handle Failures Gracefully**: Always check `Result.IsSuccess` and handle errors
5. **Monitor Agent Heartbeats**: The system automatically tracks agent health
6. **Respect Concurrency Limits**: Configure `MaxConcurrentSubIssues` appropriately

## Troubleshooting

### Issue: Agent Assignment Failed

```csharp
var result = await epicOrchestrator.AssignSubIssueAsync(120, 121);
if (!result.IsSuccess)
{
    Console.WriteLine($"Assignment failed: {result.Error}");
    // Retry with manual agent ID
    result = await epicOrchestrator.AssignSubIssueAsync(120, 121, "custom-agent-id");
}
```

### Issue: Execution Timed Out

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
var result = await epicOrchestrator.ExecuteSubIssueAsync(
    120, 
    121,
    workFunc,
    cts.Token
);
```

### Issue: Branch Not Available

```csharp
var assignment = epicOrchestrator.GetSubIssueAssignment(120, 121);
if (assignment?.Branch == null)
{
    // Manually create branch if needed
    var store = new TrackedVectorStore();
    var source = DataSource.FromPath(Environment.CurrentDirectory);
    var branch = new PipelineBranch($"epic-120/sub-issue-121", store, source);
    
    assignment = assignment with { Branch = branch };
}
```

## Running the Example

To run the complete Epic #120 example:

```csharp
using LangChainPipeline.Examples.EpicWorkflow;

// Run the complete workflow
await Epic120Example.RunEpic120WorkflowAsync();

// Or run parallel execution example
await Epic120Example.RunParallelSubIssuesAsync();
```

## Next Steps

1. **Integrate with GitHub API**: Connect to actual GitHub issues for real-time tracking
2. **Add Persistence**: Store epic and assignment state in a database
3. **Implement Webhooks**: React to GitHub issue updates automatically
4. **Add Reporting**: Generate progress reports and dashboards
5. **Extend Work Functions**: Implement domain-specific work logic for each sub-issue category

## See Also

- [EpicBranchOrchestration.md](EpicBranchOrchestration.md) - API reference and detailed documentation
- [DistributedOrchestrator.cs](../src/Ouroboros.Agent/Agent/MetaAI/DistributedOrchestrator.cs) - Multi-agent coordination
- [PipelineBranch.cs](../src/Ouroboros.Pipeline/Pipeline/Branches/PipelineBranch.cs) - Immutable branch implementation
- [Epic120Example.cs](../src/Ouroboros.Examples/Examples/Epic120Example.cs) - Complete working example
