// ==========================================================
// Stakeholder Review Loop Tests
// Tests for PR-based approval workflows
// ==========================================================

using LangChainPipeline.Agent.MetaAI;
using Xunit;

namespace LangChainPipeline.Tests.Agent;

/// <summary>
/// Tests for stakeholder review loop functionality.
/// </summary>
public class StakeholderReviewLoopTests
{
    /// <summary>
    /// Tests basic stakeholder review loop workflow.
    /// </summary>
    [Fact]
    public async Task TestBasicReviewLoop()
    {
        var mockProvider = new MockReviewSystemProvider();
        var reviewLoop = new StakeholderReviewLoop(mockProvider);

        var requiredReviewers = new List<string> { "reviewer1", "reviewer2", "reviewer3" };
        var draftSpec = "# Draft Specification\n\nThis is a test draft spec for review.";

        // Start review loop
        var reviewTask = reviewLoop.ExecuteReviewLoopAsync(
            "Test Feature Spec",
            "Testing stakeholder review workflow",
            draftSpec,
            requiredReviewers,
            new StakeholderReviewConfig(
                MinimumRequiredApprovals: 2,
                RequireAllReviewersApprove: true,
                ReviewTimeout: TimeSpan.FromSeconds(30),
                PollingInterval: TimeSpan.FromMilliseconds(500)),
            CancellationToken.None);

        // Simulate reviews coming in
        await Task.Delay(100);

        // Get the PR ID (we need to access the mock provider's internal state)
        // For testing, we'll simulate the reviews directly
        var prId = "test-pr-1";

        // Simulate all required reviewers approving
        mockProvider.SimulateReview(prId, "reviewer1", true, "Looks good!");
        mockProvider.SimulateReview(prId, "reviewer2", true, "LGTM");
        mockProvider.SimulateReview(prId, "reviewer3", true, "Approved");

        // Wait for review loop to complete
        var result = await reviewTask;

        Assert.True(result.IsSuccess, $"Review loop should succeed, but got: {result.Error}");

        var reviewResult = result.Value;

        Assert.True(reviewResult.AllApproved, "All reviewers should have approved");
        Assert.Equal(3, reviewResult.ApprovedCount);
    }

    /// <summary>
    /// Tests review loop with comments that need resolution.
    /// </summary>
    [Fact]
    public async Task TestReviewLoopWithComments()
    {
        var mockProvider = new MockReviewSystemProvider();
        var reviewLoop = new StakeholderReviewLoop(mockProvider);

        var requiredReviewers = new List<string> { "reviewer1", "reviewer2" };
        var draftSpec = "# Draft Specification\n\nThis is a test draft spec for review.";

        // Open a PR
        var prResult = await mockProvider.OpenPullRequestAsync(
            "Test Feature Spec",
            "Testing review with comments",
            draftSpec,
            requiredReviewers);

        Assert.True(prResult.IsSuccess, $"PR creation should succeed");

        var pr = prResult.Value;

        // Simulate reviews with comments
        mockProvider.SimulateReview(pr.Id, "reviewer1", false, "Needs changes");
        mockProvider.SimulateComment(pr.Id, "reviewer1", "Please update section 2");
        mockProvider.SimulateComment(pr.Id, "reviewer2", "Minor typo in introduction");

        // Get comments
        var commentsResult = await mockProvider.GetCommentsAsync(pr.Id);
        Assert.True(commentsResult.IsSuccess, "Should be able to get comments");

        var comments = commentsResult.Value;

        Assert.Equal(2, comments.Count);

        // Resolve comments
        var resolveResult = await reviewLoop.ResolveCommentsAsync(pr.Id, comments);

        Assert.True(resolveResult.IsSuccess, $"Comment resolution should succeed");

        var resolvedCount = resolveResult.Value;

        Assert.Equal(2, resolvedCount);
    }

    /// <summary>
    /// Tests monitoring review progress.
    /// </summary>
    [Fact]
    public async Task TestMonitorReviewProgress()
    {
        var mockProvider = new MockReviewSystemProvider();
        var reviewLoop = new StakeholderReviewLoop(mockProvider);

        var requiredReviewers = new List<string> { "reviewer1", "reviewer2" };
        var draftSpec = "# Draft Specification\n\nMonitoring test.";

        // Open a PR
        var prResult = await mockProvider.OpenPullRequestAsync(
            "Monitor Test",
            "Testing review monitoring",
            draftSpec,
            requiredReviewers);

        Assert.True(prResult.IsSuccess, "PR creation should succeed");

        var pr = prResult.Value;

        // Start monitoring in background
        var monitorTask = reviewLoop.MonitorReviewProgressAsync(
            pr.Id,
            new StakeholderReviewConfig(
                RequireAllReviewersApprove: true,
                ReviewTimeout: TimeSpan.FromSeconds(10),
                PollingInterval: TimeSpan.FromMilliseconds(200)));

        // Simulate reviews coming in over time
        await Task.Delay(100);
        mockProvider.SimulateReview(pr.Id, "reviewer1", true, "Approved");

        await Task.Delay(200);
        mockProvider.SimulateReview(pr.Id, "reviewer2", true, "LGTM");

        // Wait for monitoring to complete
        var result = await monitorTask;

        Assert.True(result.IsSuccess, $"Monitoring should succeed, but got: {result.Error}");

        var state = result.Value;

        Assert.Equal(2, state.Reviews.Count);
        Assert.Equal(ReviewStatus.Approved, state.Status);
    }

    /// <summary>
    /// Tests review loop with minimum required approvals (not all reviewers).
    /// </summary>
    [Fact]
    public async Task TestMinimumRequiredApprovals()
    {
        var mockProvider = new MockReviewSystemProvider();
        var reviewLoop = new StakeholderReviewLoop(mockProvider);

        var requiredReviewers = new List<string> { "reviewer1", "reviewer2", "reviewer3" };
        var draftSpec = "# Draft Specification\n\nMinimum approvals test.";

        // Open a PR
        var prResult = await mockProvider.OpenPullRequestAsync(
            "Minimum Approvals Test",
            "Testing minimum required approvals",
            draftSpec,
            requiredReviewers);

        Assert.True(prResult.IsSuccess, "PR creation should succeed");

        var pr = prResult.Value;

        // Simulate only 2 out of 3 reviewers approving
        mockProvider.SimulateReview(pr.Id, "reviewer1", true, "Approved");
        mockProvider.SimulateReview(pr.Id, "reviewer2", true, "LGTM");

        // Monitor with minimum required approvals = 2
        var monitorResult = await reviewLoop.MonitorReviewProgressAsync(
            pr.Id,
            new StakeholderReviewConfig(
                MinimumRequiredApprovals: 2,
                RequireAllReviewersApprove: false,
                ReviewTimeout: TimeSpan.FromSeconds(5),
                PollingInterval: TimeSpan.FromMilliseconds(100)));

        Assert.True(monitorResult.IsSuccess, $"Monitoring should succeed");

        var state = monitorResult.Value;

        Assert.Equal(ReviewStatus.Approved, state.Status);
    }

    /// <summary>
    /// Tests review state transitions.
    /// </summary>
    [Fact]
    public async Task TestReviewStateTransitions()
    {
        Console.WriteLine("=== Test: Review State Transitions ===");

        var mockProvider = new MockReviewSystemProvider();
        var reviewLoop = new StakeholderReviewLoop(mockProvider);

        var requiredReviewers = new List<string> { "reviewer1" };
        var draftSpec = "# Draft Specification\n\nState transitions test.";

        // Open a PR
        var prResult = await mockProvider.OpenPullRequestAsync(
            "State Transitions Test",
            "Testing review state transitions",
            draftSpec,
            requiredReviewers);

        if (!prResult.IsSuccess)
            throw new Exception("PR creation should succeed");

        var pr = prResult.Value;

        // State 1: AwaitingReview (no reviews yet)
        var reviewsResult1 = await mockProvider.GetReviewDecisionsAsync(pr.Id);
        if (!reviewsResult1.IsSuccess || reviewsResult1.Value.Any())
            throw new Exception("Should start with no reviews");

        Console.WriteLine("✓ State 1: AwaitingReview (no reviews)");

        // State 2: ChangesRequested (negative review)
        mockProvider.SimulateReview(pr.Id, "reviewer1", false, "Needs changes");
        mockProvider.SimulateComment(pr.Id, "reviewer1", "Please fix this");

        var reviewsResult2 = await mockProvider.GetReviewDecisionsAsync(pr.Id);
        Assert.True(reviewsResult2.IsSuccess);

        // Resolve comment
        var commentsResult = await mockProvider.GetCommentsAsync(pr.Id);
        if (commentsResult.IsSuccess && commentsResult.Value.Any())
        {
            await mockProvider.ResolveCommentAsync(
                pr.Id,
                commentsResult.Value[0].CommentId,
                "Fixed");
        }

        // State 4: Approved (positive review)
        mockProvider.SimulateReview(pr.Id, "reviewer1", true, "Approved");

        var monitorResult = await reviewLoop.MonitorReviewProgressAsync(
            pr.Id,
            new StakeholderReviewConfig(
                RequireAllReviewersApprove: true,
                ReviewTimeout: TimeSpan.FromSeconds(5),
                PollingInterval: TimeSpan.FromMilliseconds(100)));

        Assert.True(monitorResult.IsSuccess, "Should successfully monitor");

        var finalState = monitorResult.Value;

        Assert.Equal(ReviewStatus.Approved, finalState.Status);
    }
}
