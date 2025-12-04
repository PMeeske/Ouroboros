# Stakeholder Review Loop

**Issue #137 - Part of Epic #120**

## Overview

The Stakeholder Review Loop provides an automated workflow for collecting approvals from stakeholders through a PR-based review process. This feature implements the final validation step before locking scope for production releases.

## Purpose

- **Goal**: Collect approvals from all required stakeholders
- **Inputs**: Draft specifications or feature documents
- **Actions**: Open PR, request reviewers, track feedback, resolve comments
- **Output**: Merged PR with all approvals
- **Done Criteria**: All required reviewers approve the changes

## Architecture

### Core Components

#### `IStakeholderReviewLoop`
Main interface for orchestrating the review workflow.

```csharp
public interface IStakeholderReviewLoop
{
    Task<Result<StakeholderReviewResult, string>> ExecuteReviewLoopAsync(...);
    Task<Result<ReviewState, string>> MonitorReviewProgressAsync(...);
    Task<Result<int, string>> ResolveCommentsAsync(...);
}
```

#### `IReviewSystemProvider`
Abstraction for interacting with PR/review systems (GitHub, GitLab, etc.).

```csharp
public interface IReviewSystemProvider
{
    Task<Result<PullRequest, string>> OpenPullRequestAsync(...);
    Task<Result<bool, string>> RequestReviewersAsync(...);
    Task<Result<List<ReviewDecision>, string>> GetReviewDecisionsAsync(...);
    Task<Result<List<ReviewComment>, string>> GetCommentsAsync(...);
    Task<Result<bool, string>> ResolveCommentAsync(...);
    Task<Result<bool, string>> MergePullRequestAsync(...);
}
```

### Key Types

#### `PullRequest`
Represents a PR for stakeholder review.
```csharp
public sealed record PullRequest(
    string Id,
    string Title,
    string Description,
    string DraftSpec,
    List<string> RequiredReviewers,
    DateTime CreatedAt);
```

#### `ReviewDecision`
Represents a stakeholder's review decision.
```csharp
public sealed record ReviewDecision(
    string ReviewerId,
    bool Approved,
    string? Feedback,
    List<ReviewComment>? Comments,
    DateTime ReviewedAt);
```

#### `ReviewState`
Tracks the overall state of the review process.
```csharp
public sealed record ReviewState(
    PullRequest PR,
    List<ReviewDecision> Reviews,
    List<ReviewComment> AllComments,
    ReviewStatus Status,
    DateTime LastUpdatedAt);
```

### Review Status States

- **Draft**: PR is being prepared
- **AwaitingReview**: Waiting for reviewers to respond
- **ChangesRequested**: Reviewers have requested changes or left unresolved comments
- **Approved**: All required approvals collected
- **Merged**: PR has been successfully merged

## Configuration

```csharp
public sealed record StakeholderReviewConfig(
    int MinimumRequiredApprovals = 2,
    bool RequireAllReviewersApprove = true,
    bool AutoResolveNonBlockingComments = false,
    TimeSpan ReviewTimeout = default,
    TimeSpan PollingInterval = default);
```

**Parameters:**
- `MinimumRequiredApprovals`: Minimum number of approvals needed (when not requiring all)
- `RequireAllReviewersApprove`: Whether all reviewers must approve (vs. minimum threshold)
- `AutoResolveNonBlockingComments`: Automatically resolve non-blocking comments
- `ReviewTimeout`: Maximum time to wait for reviews (default: 24 hours)
- `PollingInterval`: How often to check for new reviews (default: 5 minutes)

## Usage Examples

### Basic Review Workflow

```csharp
var reviewProvider = new MockReviewSystemProvider(); // or GitHubReviewProvider
var reviewLoop = new StakeholderReviewLoop(reviewProvider);

var draftSpec = @"
# Feature Specification: Advanced Search

## Overview
Implement advanced search with filters and facets...
";

var requiredReviewers = new List<string>
{
    "tech-lead@company.com",
    "product-manager@company.com",
    "architect@company.com"
};

var config = new StakeholderReviewConfig(
    RequireAllReviewersApprove: true,
    ReviewTimeout: TimeSpan.FromHours(48));

var result = await reviewLoop.ExecuteReviewLoopAsync(
    "Advanced Search Feature Specification",
    "Spec for v2.0 advanced search",
    draftSpec,
    requiredReviewers,
    config);

result.Match(
    reviewResult =>
    {
        Console.WriteLine($"✅ All approved! Merged in {reviewResult.Duration}");
        Console.WriteLine($"   Approvals: {reviewResult.ApprovedCount}/{reviewResult.TotalReviewers}");
    },
    error =>
    {
        Console.WriteLine($"❌ Review failed: {error}");
    });
```

### Monitoring Review Progress

```csharp
// Start monitoring an existing PR
var monitorResult = await reviewLoop.MonitorReviewProgressAsync(
    prId,
    new StakeholderReviewConfig(
        RequireAllReviewersApprove: true,
        ReviewTimeout: TimeSpan.FromDays(7),
        PollingInterval: TimeSpan.FromHours(2)));

monitorResult.Match(
    state =>
    {
        Console.WriteLine($"Status: {state.Status}");
        Console.WriteLine($"Approved: {state.Reviews.Count(r => r.Approved)}");
        Console.WriteLine($"Comments: {state.AllComments.Count}");
    },
    error => Console.WriteLine($"Monitoring failed: {error}"));
```

### Resolving Comments

```csharp
// Get all comments
var commentsResult = await reviewProvider.GetCommentsAsync(prId);

if (commentsResult.IsSuccess)
{
    var comments = commentsResult.Value;
    
    // Resolve comments automatically
    var resolveResult = await reviewLoop.ResolveCommentsAsync(prId, comments);
    
    resolveResult.Match(
        resolved => Console.WriteLine($"Resolved {resolved} comments"),
        error => Console.WriteLine($"Failed: {error}"));
}
```

## Epic #120 Integration

The Stakeholder Review Loop is part of Epic #120's Requirements & Scope workflow:

```
Issue #133 ✅ → Issue #134 → Issue #135 ✅ → Issue #136 → Issue #137 (This Feature) → Issue #138
Aggregate     Define        NFRs          KPIs          Review Loop         Lock & Tag
Discussions   Features      Defined       Criteria      (Collect Approvals) Scope
```

### v1.0 Release Process

1. **Aggregate Requirements** (#133) - Collect all feature discussions
2. **Define Must-Have Features** (#134) - Prioritize v1.0 scope
3. **Document NFRs** (#135) - Define non-functional requirements
4. **Set KPIs** (#136) - Define success metrics
5. **Stakeholder Review** (#137 - This Feature) - Get approvals
6. **Lock Scope** (#138) - Finalize and tag v1.0 scope

### Example: v1.0 Specification Review

```csharp
var v1Spec = LoadV1Specification(); // From Issue #134, #135, #136

var stakeholders = new List<string>
{
    "technical-lead@Ouroboros.com",
    "product-owner@Ouroboros.com",
    "security-lead@Ouroboros.com",
    "qa-lead@Ouroboros.com"
};

var config = new StakeholderReviewConfig(
    MinimumRequiredApprovals: 3,
    RequireAllReviewersApprove: true,
    ReviewTimeout: TimeSpan.FromDays(7),
    PollingInterval: TimeSpan.FromHours(2));

var result = await reviewLoop.ExecuteReviewLoopAsync(
    "Ouroboros v1.0 - Production Release Specification",
    "Final specification for v1.0 release approval",
    v1Spec,
    stakeholders,
    config);

// On success: proceed to Issue #138 (Lock & Tag Scope)
```

## Implementation Providers

### MockReviewSystemProvider
For testing and development:
```csharp
var mockProvider = new MockReviewSystemProvider();
mockProvider.SimulateReview(prId, "reviewer@example.com", approved: true);
mockProvider.SimulateComment(prId, "reviewer@example.com", "Looks good!");
```

### GitHubReviewProvider (Future)
For production use with GitHub:
```csharp
var githubProvider = new GitHubReviewProvider(
    githubToken,
    "PMeeske",
    "Ouroboros");
    
var reviewLoop = new StakeholderReviewLoop(githubProvider);
```

## Testing

The implementation includes comprehensive xUnit tests:

```csharp
[Fact]
public async Task TestBasicReviewLoop() { }

[Fact]
public async Task TestReviewLoopWithComments() { }

[Fact]
public async Task TestMonitorReviewProgress() { }

[Fact]
public async Task TestMinimumRequiredApprovals() { }

[Fact]
public async Task TestReviewStateTransitions() { }
```

Run tests:
```bash
dotnet test --filter "FullyQualifiedName~StakeholderReviewLoopTests"
```

## Benefits

1. **Automated Approval Collection**: No manual tracking of stakeholder responses
2. **Comment Management**: Automatic tracking and resolution of review comments
3. **Flexible Approval Rules**: Configure minimum approvals or require all reviewers
4. **Timeout Handling**: Automatic timeout for reviews that take too long
5. **State Tracking**: Full visibility into review progress
6. **Extensible**: Easy to implement providers for different review systems

## Dependencies

- **Depends on**: Issue #136 (KPIs & Acceptance Criteria)
- **Required for**: Issue #138 (Lock & Tag Scope)
- **Part of**: Epic #120 (Production-ready Release v1.0)

## Monadic Patterns

The implementation follows Ouroboros's functional programming principles:

```csharp
// Result monad for error handling
Task<Result<StakeholderReviewResult, string>> ExecuteReviewLoopAsync(...);

// Pattern matching on results
result.Match(
    onSuccess: reviewResult => { /* handle success */ },
    onFailure: error => { /* handle error */ });

// Immutable data structures
public sealed record ReviewState(...)
public sealed record ReviewDecision(...)
```

## Future Enhancements

1. **GitHub Integration**: Native GitHub PR and review API integration
2. **Slack Notifications**: Alert reviewers via Slack when reviews are needed
3. **Email Reminders**: Automatic reminders for pending reviews
4. **Review Analytics**: Track review response times and bottlenecks
5. **Approval Hierarchies**: Support for multi-level approval workflows
6. **Conditional Approvals**: Rules-based approval requirements

## See Also

- [HumanInTheLoopOrchestrator.cs](../src/Ouroboros.Agent/Agent/MetaAI/HumanInTheLoopOrchestrator.cs) - Related approval patterns
- [Epic120Integration.md](Epic120Integration.md) - Full Epic #120 workflow
- [StakeholderReviewLoopExample.cs](../src/Ouroboros.Examples/Examples/StakeholderReviewLoopExample.cs) - Complete usage examples
