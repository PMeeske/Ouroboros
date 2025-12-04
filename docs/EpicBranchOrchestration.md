# Epic Branch Orchestration System

## Overview

The Epic Branch Orchestration system provides a powerful way to manage GitHub epics with automatic agent assignment and dedicated branch creation for each sub-issue.

## Architecture

### Core Components

1. **EpicBranchOrchestrator**: Main orchestrator that manages the lifecycle of an epic and its sub-issues
2. **SubIssueAssignment**: Records mapping sub-issues to agents and their dedicated branches
3. **PipelineBranch Integration**: Each sub-issue gets its own immutable pipeline branch for work tracking
4. **DistributedOrchestrator Integration**: Leverages the existing multi-agent system for parallel execution

### Key Features

- **Automatic Agent Assignment**: Each sub-issue is automatically assigned to a dedicated agent
- **Dedicated Branches**: Each sub-issue gets its own `PipelineBranch` for isolated work tracking
- **Status Tracking**: Monitor sub-issue progress through status transitions (Pending → BranchCreated → InProgress → Completed/Failed)
- **Parallel Execution**: Execute multiple sub-issues concurrently using the distributed orchestrator
- **Functional Design**: Immutable data structures following category theory principles

## Usage

### Basic Example: Register an Epic

```csharp
using LangChainPipeline.Agent.MetaAI;

// Initialize the orchestrators
var safetyGuard = new SafetyGuard(PermissionLevel.Isolated);
var distributor = new DistributedOrchestrator(safetyGuard);
var epicOrchestrator = new EpicBranchOrchestrator(distributor);

// Register Epic #120 with its sub-issues
var epicResult = await epicOrchestrator.RegisterEpicAsync(
    120,
    "🚀 Production-ready Release v1.0",
    "This epic tracks every task required to ship the first production-ready release",
    new List<int> { 121, 122, 123, 124, 125 } // Sub-issue numbers
);
```

### Working with Sub-Issues

```csharp
// Get all assignments for an epic
var assignments = epicOrchestrator.GetSubIssueAssignments(120);

foreach (var assignment in assignments)
{
    Console.WriteLine($"Issue #{assignment.IssueNumber}:");
    Console.WriteLine($"  Agent: {assignment.AssignedAgentId}");
    Console.WriteLine($"  Branch: {assignment.BranchName}");
    Console.WriteLine($"  Status: {assignment.Status}");
}

// Get a specific sub-issue assignment
var issue121 = epicOrchestrator.GetSubIssueAssignment(120, 121);
```

### Executing Work on a Sub-Issue

```csharp
// Execute work on sub-issue #121
var result = await epicOrchestrator.ExecuteSubIssueAsync(
    120, // Epic number
    121, // Sub-issue number
    async assignment =>
    {
        // Your work logic here
        Console.WriteLine($"Working on {assignment.BranchName}...");
        
        // Update the branch with events
        if (assignment.Branch != null)
        {
            var updatedBranch = assignment.Branch
                .WithIngestEvent("data-source", new[] { "doc1", "doc2" })
                .WithReasoning(
                    new Draft("Initial analysis..."),
                    "Analyze requirements",
                    null);
            
            var updatedAssignment = assignment with { Branch = updatedBranch };
            return Result<SubIssueAssignment, string>.Success(updatedAssignment);
        }
        
        return Result<SubIssueAssignment, string>.Success(assignment);
    }
);
```

### Parallel Execution

```csharp
// Execute multiple sub-issues in parallel
var subIssues = new[] { 121, 122, 123, 124, 125 };

var tasks = subIssues.Select(async issueNumber =>
{
    return await epicOrchestrator.ExecuteSubIssueAsync(
        120,
        issueNumber,
        async assignment =>
        {
            // Work logic for each sub-issue
            await Task.Delay(1000); // Simulate work
            return Result<SubIssueAssignment, string>.Success(assignment);
        });
});

var results = await Task.WhenAll(tasks);

// Check results
var successCount = results.Count(r => r.IsSuccess);
Console.WriteLine($"Completed: {successCount}/{results.Length}");
```

### Status Management

```csharp
// Update sub-issue status manually if needed
var statusResult = epicOrchestrator.UpdateSubIssueStatus(
    120,
    121,
    SubIssueStatus.Completed,
    errorMessage: null
);

if (statusResult.IsSuccess)
{
    Console.WriteLine("Status updated successfully");
}
```

## Configuration

The `EpicBranchConfig` allows customization of the orchestrator behavior:

```csharp
var config = new EpicBranchConfig(
    BranchPrefix: "epic",                 // Prefix for branch names
    AgentPoolPrefix: "sub-issue-agent",   // Prefix for agent IDs
    AutoCreateBranches: true,             // Automatically create PipelineBranch for each sub-issue
    AutoAssignAgents: true,               // Automatically assign agents when registering epic
    MaxConcurrentSubIssues: 5             // Maximum concurrent executions
);

var orchestrator = new EpicBranchOrchestrator(distributor, config);
```

## Data Models

### SubIssueAssignment

```csharp
public sealed record SubIssueAssignment(
    int IssueNumber,
    string Title,
    string Description,
    string AssignedAgentId,
    string BranchName,
    PipelineBranch? Branch,
    SubIssueStatus Status,
    DateTime CreatedAt,
    DateTime? CompletedAt = null,
    string? ErrorMessage = null);
```

### SubIssueStatus

```csharp
public enum SubIssueStatus
{
    Pending,        // Initial state
    BranchCreated,  // Branch created, waiting for work
    InProgress,     // Currently being worked on
    Completed,      // Successfully completed
    Failed          // Failed with error
}
```

## Integration with Existing Systems

The Epic Branch Orchestration system integrates seamlessly with:

1. **DistributedOrchestrator**: Uses the agent registry and heartbeat system
2. **PipelineBranch**: Each sub-issue gets an immutable pipeline branch for event tracking
3. **Result Monads**: All operations return `Result<T, E>` for robust error handling
4. **Safety Guards**: Leverages the safety system for sandboxed execution

## Example: Epic #120 Workflow

See `Epic120Example.cs` for a complete example demonstrating:
- Registering Epic #120 with 30+ sub-issues
- Automatic agent and branch assignment
- Executing work on specific sub-issues
- Parallel execution of multiple sub-issues
- Status tracking and reporting

```bash
# Run the example
dotnet run --project src/Ouroboros.Examples -- epic120
```

## Branch Naming Convention

Branches are automatically named using the pattern:
```
{BranchPrefix}-{EpicNumber}/sub-issue-{SubIssueNumber}
```

For example:
- `epic-120/sub-issue-121`
- `epic-120/sub-issue-122`
- `epic-120/sub-issue-123`

## Agent Naming Convention

Agents are automatically named using the pattern:
```
{AgentPoolPrefix}-{EpicNumber}-{SubIssueNumber}
```

For example:
- `sub-issue-agent-120-121`
- `sub-issue-agent-120-122`
- `sub-issue-agent-120-123`

## Best Practices

1. **Use Functional Updates**: Always use `with` expressions to update assignments
2. **Handle Failures**: Check `Result.IsSuccess` before accessing values
3. **Leverage Parallelism**: Use `Task.WhenAll` for concurrent sub-issue execution
4. **Track Progress**: Monitor status transitions and update heartbeats
5. **Immutable Branches**: Use `WithReasoning()` and `WithIngestEvent()` to update branches functionally

## Extending the System

To add custom logic:

```csharp
public class CustomEpicOrchestrator
{
    private readonly IEpicBranchOrchestrator _orchestrator;
    
    public async Task<Result<string, string>> ProcessEpicAsync(int epicNumber)
    {
        var assignments = _orchestrator.GetSubIssueAssignments(epicNumber);
        
        foreach (var assignment in assignments)
        {
            // Custom processing logic
            await _orchestrator.ExecuteSubIssueAsync(
                epicNumber,
                assignment.IssueNumber,
                async a => await CustomWorkAsync(a)
            );
        }
        
        return Result<string, string>.Success("Processing complete");
    }
    
    private async Task<Result<SubIssueAssignment, string>> CustomWorkAsync(
        SubIssueAssignment assignment)
    {
        // Your custom logic here
        return Result<SubIssueAssignment, string>.Success(assignment);
    }
}
```

## API Reference

### IEpicBranchOrchestrator Interface

```csharp
public interface IEpicBranchOrchestrator
{
    Task<Result<Epic, string>> RegisterEpicAsync(
        int epicNumber,
        string epicTitle,
        string epicDescription,
        List<int> subIssueNumbers,
        CancellationToken ct = default);

    Task<Result<SubIssueAssignment, string>> AssignSubIssueAsync(
        int epicNumber,
        int subIssueNumber,
        string? preferredAgentId = null,
        CancellationToken ct = default);

    IReadOnlyList<SubIssueAssignment> GetSubIssueAssignments(int epicNumber);

    SubIssueAssignment? GetSubIssueAssignment(int epicNumber, int subIssueNumber);

    Result<SubIssueAssignment, string> UpdateSubIssueStatus(
        int epicNumber,
        int subIssueNumber,
        SubIssueStatus status,
        string? errorMessage = null);

    Task<Result<SubIssueAssignment, string>> ExecuteSubIssueAsync(
        int epicNumber,
        int subIssueNumber,
        Func<SubIssueAssignment, Task<Result<SubIssueAssignment, string>>> workFunc,
        CancellationToken ct = default);
}
```

## Testing

Example test structure:

```csharp
[Fact]
public async Task RegisterEpic_WithValidData_ReturnsSuccess()
{
    // Arrange
    var orchestrator = CreateTestOrchestrator();
    
    // Act
    var result = await orchestrator.RegisterEpicAsync(
        1, "Test Epic", "Description", new List<int> { 1, 2, 3 });
    
    // Assert
    Assert.True(result.IsSuccess);
    Assert.Equal(3, orchestrator.GetSubIssueAssignments(1).Count);
}
```

## See Also

- `DistributedOrchestrator.cs` - Multi-agent orchestration
- `PipelineBranch.cs` - Immutable pipeline branches
- `Result.cs` - Monadic error handling
- `Epic120Example.cs` - Complete usage example
