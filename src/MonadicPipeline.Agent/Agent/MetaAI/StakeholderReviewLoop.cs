#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
// ==========================================================
// Stakeholder Review Loop - PR-based approval workflow
// ==========================================================

namespace LangChainPipeline.Agent.MetaAI;

/// <summary>
/// Represents a PR (Pull Request) for stakeholder review.
/// </summary>
public sealed record PullRequest(
    string Id,
    string Title,
    string Description,
    string DraftSpec,
    List<string> RequiredReviewers,
    DateTime CreatedAt);

/// <summary>
/// Represents a review comment on a PR.
/// </summary>
public sealed record ReviewComment(
    string CommentId,
    string ReviewerId,
    string Content,
    ReviewCommentStatus Status,
    DateTime CreatedAt,
    DateTime? ResolvedAt = null);

/// <summary>
/// Status of a review comment.
/// </summary>
public enum ReviewCommentStatus
{
    Open,
    Resolved,
    Dismissed
}

/// <summary>
/// Represents a stakeholder's review decision.
/// </summary>
public sealed record ReviewDecision(
    string ReviewerId,
    bool Approved,
    string? Feedback,
    List<ReviewComment>? Comments,
    DateTime ReviewedAt);

/// <summary>
/// Represents the state of a PR review process.
/// </summary>
public sealed record ReviewState(
    PullRequest PR,
    List<ReviewDecision> Reviews,
    List<ReviewComment> AllComments,
    ReviewStatus Status,
    DateTime LastUpdatedAt);

/// <summary>
/// Overall status of the review process.
/// </summary>
public enum ReviewStatus
{
    Draft,
    AwaitingReview,
    ChangesRequested,
    Approved,
    Merged
}

/// <summary>
/// Result of a stakeholder review loop execution.
/// </summary>
public sealed record StakeholderReviewResult(
    ReviewState FinalState,
    bool AllApproved,
    int TotalReviewers,
    int ApprovedCount,
    int CommentsResolved,
    int CommentsRemaining,
    TimeSpan Duration,
    string Summary);

/// <summary>
/// Interface for interacting with the PR/review system (GitHub, etc.).
/// </summary>
public interface IReviewSystemProvider
{
    /// <summary>
    /// Opens a new PR with the given spec.
    /// </summary>
    Task<Result<PullRequest, string>> OpenPullRequestAsync(
        string title,
        string description,
        string draftSpec,
        List<string> requiredReviewers,
        CancellationToken ct = default);

    /// <summary>
    /// Requests reviews from specified reviewers.
    /// </summary>
    Task<Result<bool, string>> RequestReviewersAsync(
        string prId,
        List<string> reviewers,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves review decisions for a PR.
    /// </summary>
    Task<Result<List<ReviewDecision>, string>> GetReviewDecisionsAsync(
        string prId,
        CancellationToken ct = default);

    /// <summary>
    /// Retrieves all comments on a PR.
    /// </summary>
    Task<Result<List<ReviewComment>, string>> GetCommentsAsync(
        string prId,
        CancellationToken ct = default);

    /// <summary>
    /// Resolves a specific comment.
    /// </summary>
    Task<Result<bool, string>> ResolveCommentAsync(
        string prId,
        string commentId,
        string resolution,
        CancellationToken ct = default);

    /// <summary>
    /// Merges the PR after all approvals are collected.
    /// </summary>
    Task<Result<bool, string>> MergePullRequestAsync(
        string prId,
        string mergeCommitMessage,
        CancellationToken ct = default);
}

/// <summary>
/// Configuration for stakeholder review loop.
/// </summary>
public sealed record StakeholderReviewConfig(
    int MinimumRequiredApprovals = 2,
    bool RequireAllReviewersApprove = true,
    bool AutoResolveNonBlockingComments = false,
    TimeSpan ReviewTimeout = default,
    TimeSpan PollingInterval = default);

/// <summary>
/// Interface for stakeholder review loop orchestration.
/// </summary>
public interface IStakeholderReviewLoop
{
    /// <summary>
    /// Executes the complete stakeholder review workflow.
    /// Opens PR, requests reviews, resolves comments, and merges when approved.
    /// </summary>
    Task<Result<StakeholderReviewResult, string>> ExecuteReviewLoopAsync(
        string title,
        string description,
        string draftSpec,
        List<string> requiredReviewers,
        StakeholderReviewConfig? config = null,
        CancellationToken ct = default);

    /// <summary>
    /// Monitors an existing PR until all approvals are collected.
    /// </summary>
    Task<Result<ReviewState, string>> MonitorReviewProgressAsync(
        string prId,
        StakeholderReviewConfig? config = null,
        CancellationToken ct = default);

    /// <summary>
    /// Attempts to automatically resolve comments based on feedback.
    /// </summary>
    Task<Result<int, string>> ResolveCommentsAsync(
        string prId,
        List<ReviewComment> comments,
        CancellationToken ct = default);
}

/// <summary>
/// Mock implementation of review system provider for testing.
/// </summary>
public sealed class MockReviewSystemProvider : IReviewSystemProvider
{
    private readonly Dictionary<string, PullRequest> _prs = new();
    private readonly Dictionary<string, List<ReviewDecision>> _reviews = new();
    private readonly Dictionary<string, List<ReviewComment>> _comments = new();
    public string? LastCreatedPrId { get; private set; }

    public Task<Result<PullRequest, string>> OpenPullRequestAsync(
        string title,
        string description,
        string draftSpec,
        List<string> requiredReviewers,
        CancellationToken ct = default)
    {
        PullRequest pr = new PullRequest(
            Guid.NewGuid().ToString(),
            title,
            description,
            draftSpec,
            requiredReviewers,
            DateTime.UtcNow);

        _prs[pr.Id] = pr;
        _reviews[pr.Id] = new List<ReviewDecision>();
        _comments[pr.Id] = new List<ReviewComment>();
        LastCreatedPrId = pr.Id;

        return Task.FromResult(Result<PullRequest, string>.Success(pr));
    }

    public Task<Result<bool, string>> RequestReviewersAsync(
        string prId,
        List<string> reviewers,
        CancellationToken ct = default)
    {
        if (!_prs.ContainsKey(prId))
            return Task.FromResult(Result<bool, string>.Failure("PR not found"));

        // Simulate reviewer requests
        return Task.FromResult(Result<bool, string>.Success(true));
    }

    public Task<Result<List<ReviewDecision>, string>> GetReviewDecisionsAsync(
        string prId,
        CancellationToken ct = default)
    {
        if (!_reviews.ContainsKey(prId))
            return Task.FromResult(Result<List<ReviewDecision>, string>.Failure("PR not found"));

        return Task.FromResult(Result<List<ReviewDecision>, string>.Success(_reviews[prId]));
    }

    public Task<Result<List<ReviewComment>, string>> GetCommentsAsync(
        string prId,
        CancellationToken ct = default)
    {
        if (!_comments.ContainsKey(prId))
            return Task.FromResult(Result<List<ReviewComment>, string>.Failure("PR not found"));

        return Task.FromResult(Result<List<ReviewComment>, string>.Success(_comments[prId]));
    }

    public Task<Result<bool, string>> ResolveCommentAsync(
        string prId,
        string commentId,
        string resolution,
        CancellationToken ct = default)
    {
        if (!_comments.ContainsKey(prId))
            return Task.FromResult(Result<bool, string>.Failure("PR not found"));

        ReviewComment? comment = _comments[prId].FirstOrDefault(c => c.CommentId == commentId);
        if (comment == null)
            return Task.FromResult(Result<bool, string>.Failure("Comment not found"));

        // Remove old comment and add resolved version
        ReviewComment updatedComment = comment with
        {
            Status = ReviewCommentStatus.Resolved,
            ResolvedAt = DateTime.UtcNow
        };

        List<ReviewComment> comments = _comments[prId];
        int index = comments.IndexOf(comment);
        if (index >= 0)
        {
            comments[index] = updatedComment;
        }

        return Task.FromResult(Result<bool, string>.Success(true));
    }

    public Task<Result<bool, string>> MergePullRequestAsync(
        string prId,
        string mergeCommitMessage,
        CancellationToken ct = default)
    {
        if (!_prs.ContainsKey(prId))
            return Task.FromResult(Result<bool, string>.Failure("PR not found"));

        // Simulate merge
        return Task.FromResult(Result<bool, string>.Success(true));
    }

    // Test helper methods
    public void SimulateReview(string prId, string reviewerId, bool approved, string? feedback = null)
    {
        if (!_reviews.ContainsKey(prId)) return;

        // Remove any existing review from this reviewer (they can update their review)
        ReviewDecision? existingReview = _reviews[prId].FirstOrDefault(r => r.ReviewerId == reviewerId);
        if (existingReview != null)
        {
            _reviews[prId].Remove(existingReview);
        }

        ReviewDecision review = new ReviewDecision(
            reviewerId,
            approved,
            feedback,
            null,
            DateTime.UtcNow);

        _reviews[prId].Add(review);
    }

    public void SimulateComment(string prId, string reviewerId, string content)
    {
        if (!_comments.ContainsKey(prId)) return;

        ReviewComment comment = new ReviewComment(
            Guid.NewGuid().ToString(),
            reviewerId,
            content,
            ReviewCommentStatus.Open,
            DateTime.UtcNow);

        _comments[prId].Add(comment);
    }
}

/// <summary>
/// Implementation of stakeholder review loop orchestration.
/// </summary>
public sealed class StakeholderReviewLoop : IStakeholderReviewLoop
{
    private readonly IReviewSystemProvider _reviewSystem;

    public StakeholderReviewLoop(IReviewSystemProvider reviewSystem)
    {
        _reviewSystem = reviewSystem ?? throw new ArgumentNullException(nameof(reviewSystem));
    }

    /// <summary>
    /// Executes the complete stakeholder review workflow.
    /// Opens PR, requests reviews, resolves comments, and merges when approved.
    /// </summary>
    public async Task<Result<StakeholderReviewResult, string>> ExecuteReviewLoopAsync(
        string title,
        string description,
        string draftSpec,
        List<string> requiredReviewers,
        StakeholderReviewConfig? config = null,
        CancellationToken ct = default)
    {
        config ??= new StakeholderReviewConfig(
            ReviewTimeout: TimeSpan.FromHours(24),
            PollingInterval: TimeSpan.FromMinutes(5));

        System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Step 1: Open PR
            Result<PullRequest, string> prResult = await _reviewSystem.OpenPullRequestAsync(
                title, description, draftSpec, requiredReviewers, ct);

            if (!prResult.IsSuccess)
                return Result<StakeholderReviewResult, string>.Failure(
                    $"Failed to open PR: {prResult.Error}");

            PullRequest pr = prResult.Value;

            // Step 2: Request reviewers
            Result<bool, string> requestResult = await _reviewSystem.RequestReviewersAsync(
                pr.Id, requiredReviewers, ct);

            if (!requestResult.IsSuccess)
                return Result<StakeholderReviewResult, string>.Failure(
                    $"Failed to request reviewers: {requestResult.Error}");

            // Step 3: Monitor review progress
            Result<ReviewState, string> monitorResult = await MonitorReviewProgressAsync(pr.Id, config, ct);

            if (!monitorResult.IsSuccess)
                return Result<StakeholderReviewResult, string>.Failure(
                    $"Review monitoring failed: {monitorResult.Error}");

            ReviewState finalState = monitorResult.Value;

            // Step 4: Check if all required approvals are obtained
            bool allApproved = CheckAllApproved(finalState, config);

            if (!allApproved)
                return Result<StakeholderReviewResult, string>.Failure(
                    "Not all required approvals obtained");

            // Step 5: Resolve remaining comments
            List<ReviewComment> openComments = finalState.AllComments
                .Where(c => c.Status == ReviewCommentStatus.Open)
                .ToList();

            if (openComments.Any())
            {
                Result<int, string> resolveResult = await ResolveCommentsAsync(pr.Id, openComments, ct);
                if (!resolveResult.IsSuccess && !config.AutoResolveNonBlockingComments)
                    return Result<StakeholderReviewResult, string>.Failure(
                        $"Comment resolution failed: {resolveResult.Error}");
            }

            // Step 6: Merge PR
            Result<bool, string> mergeResult = await _reviewSystem.MergePullRequestAsync(
                pr.Id,
                $"Merge approved by all reviewers: {string.Join(", ", requiredReviewers)}",
                ct);

            if (!mergeResult.IsSuccess)
                return Result<StakeholderReviewResult, string>.Failure(
                    $"Failed to merge PR: {mergeResult.Error}");

            sw.Stop();

            StakeholderReviewResult result = new StakeholderReviewResult(
                finalState with { Status = ReviewStatus.Merged },
                true,
                requiredReviewers.Count,
                finalState.Reviews.Count(r => r.Approved),
                finalState.AllComments.Count(c => c.Status == ReviewCommentStatus.Resolved),
                finalState.AllComments.Count(c => c.Status == ReviewCommentStatus.Open),
                sw.Elapsed,
                $"PR '{title}' successfully merged after {finalState.Reviews.Count} approvals");

            return Result<StakeholderReviewResult, string>.Success(result);
        }
        catch (Exception ex)
        {
            return Result<StakeholderReviewResult, string>.Failure(
                $"Review loop execution failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Monitors an existing PR until all approvals are collected.
    /// </summary>
    public async Task<Result<ReviewState, string>> MonitorReviewProgressAsync(
        string prId,
        StakeholderReviewConfig? config = null,
        CancellationToken ct = default)
    {
        config ??= new StakeholderReviewConfig();

        DateTime startTime = DateTime.UtcNow;
        TimeSpan timeout = config.ReviewTimeout == default
            ? TimeSpan.FromHours(24)
            : config.ReviewTimeout;

        TimeSpan pollingInterval = config.PollingInterval == default
            ? TimeSpan.FromMinutes(5)
            : config.PollingInterval;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Check timeout
                if (DateTime.UtcNow - startTime > timeout)
                    return Result<ReviewState, string>.Failure("Review timeout exceeded");

                // Get current review decisions
                Result<List<ReviewDecision>, string> reviewsResult = await _reviewSystem.GetReviewDecisionsAsync(prId, ct);
                if (!reviewsResult.IsSuccess)
                    return Result<ReviewState, string>.Failure(
                        $"Failed to get reviews: {reviewsResult.Error}");

                List<ReviewDecision> reviews = reviewsResult.Value;

                // Get all comments
                Result<List<ReviewComment>, string> commentsResult = await _reviewSystem.GetCommentsAsync(prId, ct);
                if (!commentsResult.IsSuccess)
                    return Result<ReviewState, string>.Failure(
                        $"Failed to get comments: {commentsResult.Error}");

                List<ReviewComment> comments = commentsResult.Value;

                // Build current state
                ReviewStatus status = DetermineReviewStatus(reviews, comments, config);
                ReviewState reviewState = new ReviewState(
                    new PullRequest(prId, "", "", "", new List<string>(), DateTime.UtcNow),
                    reviews,
                    comments,
                    status,
                    DateTime.UtcNow);

                // Check if review is complete
                if (status == ReviewStatus.Approved)
                    return Result<ReviewState, string>.Success(reviewState);

                // Wait before next poll
                await Task.Delay(pollingInterval, ct);
            }

            return Result<ReviewState, string>.Failure("Monitoring cancelled");
        }
        catch (Exception ex)
        {
            return Result<ReviewState, string>.Failure(
                $"Review monitoring failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Attempts to automatically resolve comments based on feedback.
    /// </summary>
    public async Task<Result<int, string>> ResolveCommentsAsync(
        string prId,
        List<ReviewComment> comments,
        CancellationToken ct = default)
    {
        try
        {
            int resolvedCount = 0;

            // Create a copy to avoid modification during enumeration
            List<ReviewComment> openComments = comments.Where(c => c.Status == ReviewCommentStatus.Open).ToList();

            foreach (ReviewComment? comment in openComments)
            {
                // Generate resolution based on comment content
                string resolution = GenerateResolution(comment.Content);

                Result<bool, string> resolveResult = await _reviewSystem.ResolveCommentAsync(
                    prId, comment.CommentId, resolution, ct);

                if (resolveResult.IsSuccess)
                    resolvedCount++;
            }

            return Result<int, string>.Success(resolvedCount);
        }
        catch (Exception ex)
        {
            return Result<int, string>.Failure(
                $"Comment resolution failed: {ex.Message}");
        }
    }

    private bool CheckAllApproved(ReviewState state, StakeholderReviewConfig config)
    {
        int approvedCount = state.Reviews.Count(r => r.Approved);

        if (config.RequireAllReviewersApprove)
        {
            // All reviewers must approve
            return state.Reviews.Count > 0 && state.Reviews.All(r => r.Approved);
        }
        else
        {
            // Minimum number of approvals
            return approvedCount >= config.MinimumRequiredApprovals;
        }
    }

    private ReviewStatus DetermineReviewStatus(
        List<ReviewDecision> reviews,
        List<ReviewComment> comments,
        StakeholderReviewConfig config)
    {
        if (!reviews.Any())
            return ReviewStatus.AwaitingReview;

        bool hasOpenComments = comments.Any(c => c.Status == ReviewCommentStatus.Open);
        bool allApproved = reviews.All(r => r.Approved);
        bool hasRejections = reviews.Any(r => !r.Approved);

        if (hasRejections || hasOpenComments)
            return ReviewStatus.ChangesRequested;

        if (config.RequireAllReviewersApprove)
        {
            if (allApproved && reviews.Count > 0)
                return ReviewStatus.Approved;
        }
        else
        {
            if (reviews.Count(r => r.Approved) >= config.MinimumRequiredApprovals)
                return ReviewStatus.Approved;
        }

        return ReviewStatus.AwaitingReview;
    }

    private string GenerateResolution(string commentContent)
    {
        // Simple resolution generation
        // In production, this could use an LLM to generate proper responses
        return $"Addressed: {commentContent}";
    }
}
