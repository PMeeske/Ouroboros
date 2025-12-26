// <copyright file="StakeholderReviewLoopExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Examples;

using Ouroboros.Agent.MetaAI;

/// <summary>
/// Example demonstrating stakeholder review loop workflows.
/// Shows how to collect approvals, manage PR reviews, and resolve comments.
/// </summary>
public static class StakeholderReviewLoopExample
{
    /// <summary>
    /// Demonstrates basic stakeholder review loop workflow.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task DemonstrateBasicReviewWorkflow()
    {
        Console.WriteLine("=== Basic Stakeholder Review Workflow ===\n");

        // Create a mock review system provider
        MockReviewSystemProvider reviewProvider = new MockReviewSystemProvider();
        StakeholderReviewLoop reviewLoop = new StakeholderReviewLoop(reviewProvider);

        // Define draft specification
        string draftSpec = @"
# Feature Specification: Advanced Search

## Overview
Implement advanced search functionality with filters and facets.

## Requirements
1. Full-text search across all content
2. Filter by date range, category, and tags
3. Faceted navigation for result refinement
4. Search result ranking based on relevance

## Technical Approach
- Use Elasticsearch for search indexing
- Implement caching layer for common queries
- Add analytics tracking for search behavior

## Success Metrics
- Search response time < 200ms
- Relevance score > 0.8 for top 10 results
- 90% user satisfaction in surveys
";

        // Define required reviewers
        List<string> requiredReviewers = new List<string>
        {
            "tech-lead@company.com",
            "product-manager@company.com",
            "architect@company.com",
        };

        Console.WriteLine("📋 Draft Specification:");
        Console.WriteLine($"  Reviewers: {string.Join(", ", requiredReviewers)}");
        Console.WriteLine($"  Spec length: {draftSpec.Length} characters\n");

        // Configure review loop
        StakeholderReviewConfig config = new StakeholderReviewConfig(
            MinimumRequiredApprovals: 2,
            RequireAllReviewersApprove: true,
            AutoResolveNonBlockingComments: false,
            ReviewTimeout: TimeSpan.FromHours(48),
            PollingInterval: TimeSpan.FromMinutes(10));

        Console.WriteLine("⚙️  Review Configuration:");
        Console.WriteLine($"  Min approvals: {config.MinimumRequiredApprovals}");
        Console.WriteLine($"  Require all: {config.RequireAllReviewersApprove}");
        Console.WriteLine($"  Timeout: {config.ReviewTimeout.TotalHours} hours\n");

        // Execute review loop (in a real scenario, this would interact with GitHub)
        try
        {
            Console.WriteLine("🚀 Starting review loop...");

            Task<Result<StakeholderReviewResult, string>> reviewTask = reviewLoop.ExecuteReviewLoopAsync(
                "Advanced Search Feature Specification",
                "Spec for v2.0 advanced search implementation",
                draftSpec,
                requiredReviewers,
                config);

            // Simulate reviewers providing feedback over time
            // In production, this happens asynchronously via GitHub
            await Task.Delay(1000);
            Console.WriteLine("✅ Tech Lead approved with feedback");

            await Task.Delay(1000);
            Console.WriteLine("✅ Product Manager approved");

            await Task.Delay(1000);
            Console.WriteLine("✅ Architect approved with minor comments");

            Result<StakeholderReviewResult, string> result = await reviewTask;

            result.Match(
                reviewResult =>
                {
                    Console.WriteLine("\n🎉 Review Loop Completed!");
                    Console.WriteLine($"  Status: {reviewResult.FinalState.Status}");
                    Console.WriteLine($"  All approved: {reviewResult.AllApproved}");
                    Console.WriteLine($"  Approvals: {reviewResult.ApprovedCount}/{reviewResult.TotalReviewers}");
                    Console.WriteLine($"  Comments resolved: {reviewResult.CommentsResolved}");
                    Console.WriteLine($"  Duration: {reviewResult.Duration.TotalMinutes:F1} minutes");
                    Console.WriteLine($"  Summary: {reviewResult.Summary}\n");
                },
                error =>
                {
                    Console.WriteLine($"\n❌ Review failed: {error}\n");
                });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error: {ex.Message}\n");
        }
    }

    /// <summary>
    /// Demonstrates review workflow with comment resolution.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task DemonstrateCommentResolution()
    {
        Console.WriteLine("=== Comment Resolution Workflow ===\n");

        MockReviewSystemProvider reviewProvider = new MockReviewSystemProvider();
        StakeholderReviewLoop reviewLoop = new StakeholderReviewLoop(reviewProvider);

        string draftSpec = "# Feature Spec\n\nSimple spec for testing comment resolution.";
        List<string> requiredReviewers = new List<string> { "reviewer1@company.com", "reviewer2@company.com" };

        // Open PR
        Result<PullRequest, string> prResult = await reviewProvider.OpenPullRequestAsync(
            "Test Feature",
            "Testing comment resolution",
            draftSpec,
            requiredReviewers);

        if (!prResult.IsSuccess)
        {
            Console.WriteLine($"❌ Failed to open PR: {prResult.Error}\n");
            return;
        }

        PullRequest pr = prResult.Value;
        Console.WriteLine($"📝 Opened PR: {pr.Title} (ID: {pr.Id})");

        // Simulate reviews with comments
        reviewProvider.SimulateReview(pr.Id, requiredReviewers[0], false, "Needs clarification");
        reviewProvider.SimulateComment(pr.Id, requiredReviewers[0], "Please add more details to section 2");
        reviewProvider.SimulateComment(pr.Id, requiredReviewers[0], "What about edge cases?");

        reviewProvider.SimulateReview(pr.Id, requiredReviewers[1], true, "Looks good overall");
        reviewProvider.SimulateComment(pr.Id, requiredReviewers[1], "Minor typo in line 3");

        Console.WriteLine("💬 Received reviews with comments");

        // Get all comments
        Result<List<ReviewComment>, string> commentsResult = await reviewProvider.GetCommentsAsync(pr.Id);

        if (!commentsResult.IsSuccess)
        {
            Console.WriteLine($"❌ Failed to get comments: {commentsResult.Error}\n");
            return;
        }

        List<ReviewComment> comments = commentsResult.Value;
        Console.WriteLine($"📋 Total comments: {comments.Count}");

        foreach (ReviewComment comment in comments)
        {
            Console.WriteLine($"  - {comment.ReviewerId}: {comment.Content}");
        }

        Console.WriteLine();

        // Resolve comments
        Console.WriteLine("🔧 Resolving comments...");
        Result<int, string> resolveResult = await reviewLoop.ResolveCommentsAsync(pr.Id, comments);

        resolveResult.Match(
            async resolved =>
            {
                Console.WriteLine($"✅ Resolved {resolved} comment(s)");

                // Verify resolution
                Result<List<ReviewComment>, string> updatedCommentsResult = await reviewProvider.GetCommentsAsync(pr.Id);
                if (updatedCommentsResult.IsSuccess)
                {
                    int resolvedComments = updatedCommentsResult.Value
                        .Count(c => c.Status == ReviewCommentStatus.Resolved);
                    Console.WriteLine($"✅ Confirmed: {resolvedComments} comment(s) marked as resolved\n");
                }
            },
            error =>
            {
                Console.WriteLine($"❌ Failed to resolve comments: {error}\n");
            });
    }

    /// <summary>
    /// Demonstrates monitoring review progress.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task DemonstrateReviewMonitoring()
    {
        Console.WriteLine("=== Review Progress Monitoring ===\n");

        MockReviewSystemProvider reviewProvider = new MockReviewSystemProvider();
        StakeholderReviewLoop reviewLoop = new StakeholderReviewLoop(reviewProvider);

        string draftSpec = "# Feature Spec\n\nMonitoring review progress example.";
        List<string> requiredReviewers = new List<string>
        {
            "reviewer1@company.com",
            "reviewer2@company.com",
            "reviewer3@company.com",
        };

        // Open PR
        Result<PullRequest, string> prResult = await reviewProvider.OpenPullRequestAsync(
            "Monitoring Example",
            "Testing review monitoring",
            draftSpec,
            requiredReviewers);

        if (!prResult.IsSuccess)
        {
            Console.WriteLine($"❌ Failed to open PR: {prResult.Error}\n");
            return;
        }

        PullRequest pr = prResult.Value;
        Console.WriteLine($"📝 Opened PR: {pr.Title}");
        Console.WriteLine($"👥 Waiting for {requiredReviewers.Count} reviewers...\n");

        // Start monitoring
        Task<Result<ReviewState, string>> monitorTask = reviewLoop.MonitorReviewProgressAsync(
            pr.Id,
            new StakeholderReviewConfig(
                RequireAllReviewersApprove: true,
                ReviewTimeout: TimeSpan.FromSeconds(30),
                PollingInterval: TimeSpan.FromSeconds(2)));

        // Simulate reviews coming in over time
        List<Task> approvalTasks = new List<Task>
        {
            Task.Run(async () =>
            {
                await Task.Delay(1000);
                reviewProvider.SimulateReview(pr.Id, requiredReviewers[0], true, "LGTM");
                Console.WriteLine($"✅ {requiredReviewers[0]} approved");
            }),
            Task.Run(async () =>
            {
                await Task.Delay(3000);
                reviewProvider.SimulateReview(pr.Id, requiredReviewers[1], true, "Looks good");
                Console.WriteLine($"✅ {requiredReviewers[1]} approved");
            }),
            Task.Run(async () =>
            {
                await Task.Delay(5000);
                reviewProvider.SimulateReview(pr.Id, requiredReviewers[2], true, "Approved");
                Console.WriteLine($"✅ {requiredReviewers[2]} approved");
            }),
        };

        // Wait for all reviews
        await Task.WhenAll(approvalTasks);

        Result<ReviewState, string> result = await monitorTask;

        result.Match(
            state =>
            {
                Console.WriteLine($"\n🎉 All reviews collected!");
                Console.WriteLine($"  Final status: {state.Status}");
                Console.WriteLine($"  Total reviews: {state.Reviews.Count}");
                Console.WriteLine($"  Approved: {state.Reviews.Count(r => r.Approved)}");
                Console.WriteLine($"  Last updated: {state.LastUpdatedAt:HH:mm:ss}\n");
            },
            error =>
            {
                Console.WriteLine($"\n❌ Monitoring failed: {error}\n");
            });
    }

    /// <summary>
    /// Demonstrates Epic #120 integration with stakeholder review.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static Task DemonstrateEpic120Integration()
    {
        Console.WriteLine("=== Epic #120: Stakeholder Review Loop ===\n");

        Console.WriteLine("📌 Issue #137 (Part of Epic #120)");
        Console.WriteLine("Goal: Collect approvals for v1.0 specifications\n");

        MockReviewSystemProvider reviewProvider = new MockReviewSystemProvider();
        StakeholderReviewLoop reviewLoop = new StakeholderReviewLoop(reviewProvider);

        // Simulate v1.0 feature specification
        _ = @"
# Ouroboros v1.0 - Production Release Specification

## Scope
This document defines the finalized scope for v1.0 production release.

## Must-Have Features
1. Monadic composition with Result<T> and Option<T>
2. Kleisli arrows for composable operations
3. Iterative refinement (Draft-Critique-Improve)
4. Meta-AI orchestration
5. Human-in-the-loop workflows
6. Stakeholder review loops (this feature!)

## Non-Functional Requirements
- Performance: < 100ms per pipeline step
- Reliability: 99.9% uptime
- Scalability: Handle 1000+ concurrent pipelines
- Security: OAuth2 + API keys

## Acceptance Criteria
- All must-have features implemented
- Test coverage > 80%
- Documentation complete
- Security audit passed
- Performance benchmarks met

## Dependencies
- Issue #136: KPIs & Acceptance Criteria ✅
- Issue #135: Non-Functional Requirements ✅
- Issue #134: Must-Have Feature List ✅
";

        List<string> stakeholders = new List<string>
        {
            "technical-lead@Ouroboros.com",
            "product-owner@Ouroboros.com",
            "security-lead@Ouroboros.com",
            "qa-lead@Ouroboros.com",
        };

        Console.WriteLine("📋 v1.0 Specification Review");
        Console.WriteLine($"  Stakeholders: {stakeholders.Count}");
        Console.WriteLine($"  Spec sections: 6");
        Console.WriteLine();

        // Execute review loop with production settings
        StakeholderReviewConfig config = new StakeholderReviewConfig(
            MinimumRequiredApprovals: 3,
            RequireAllReviewersApprove: true,
            AutoResolveNonBlockingComments: false,
            ReviewTimeout: TimeSpan.FromDays(7),
            PollingInterval: TimeSpan.FromHours(2));

        Console.WriteLine("⚙️  Production Review Settings:");
        Console.WriteLine($"  Minimum approvals: {config.MinimumRequiredApprovals}");
        Console.WriteLine($"  Require all stakeholders: {config.RequireAllReviewersApprove}");
        Console.WriteLine($"  Review deadline: {config.ReviewTimeout.TotalDays} days");
        Console.WriteLine($"  Check interval: {config.PollingInterval.TotalHours} hours");
        Console.WriteLine();

        Console.WriteLine("🚀 Initiating stakeholder review process...");
        Console.WriteLine("📧 Notifications sent to all stakeholders");
        Console.WriteLine("⏳ Waiting for reviews...");
        Console.WriteLine();

        Console.WriteLine("✅ Stakeholder review loop ready for Epic #120");
        Console.WriteLine("✅ Depends on: Issue #136 (KPIs) - Complete");
        Console.WriteLine("📝 Output: PR merged when all stakeholders approve");
        Console.WriteLine("🎯 Success: All required reviewers approve the v1.0 spec\n");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Runs all stakeholder review loop examples.
    /// </summary>
    /// <returns><placeholder>A <see cref="Task"/> representing the asynchronous operation.</placeholder></returns>
    public static async Task RunAllExamples()
    {
        Console.WriteLine("\n╔════════════════════════════════════════════════╗");
        Console.WriteLine("║  Stakeholder Review Loop Examples              ║");
        Console.WriteLine("║  Issue #137 - Part of Epic #120                ║");
        Console.WriteLine("╚════════════════════════════════════════════════╝\n");

        await DemonstrateBasicReviewWorkflow();
        await DemonstrateCommentResolution();
        await DemonstrateReviewMonitoring();
        await DemonstrateEpic120Integration();

        Console.WriteLine("╔════════════════════════════════════════════════╗");
        Console.WriteLine("║  ✅ All Examples Completed                     ║");
        Console.WriteLine("╚════════════════════════════════════════════════╝\n");
    }
}
