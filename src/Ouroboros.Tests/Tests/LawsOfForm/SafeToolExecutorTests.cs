// <copyright file="SafeToolExecutorTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.LawsOfForm;

using FluentAssertions;
using Ouroboros.Core.LawsOfForm;
using Ouroboros.Core.Monads;
using Xunit;

/// <summary>
/// Tests for SafeToolExecutor.
/// </summary>
[Trait("Category", "Unit")]
public class SafeToolExecutorTests
{
    [Fact]
    public async Task ExecuteWithAudit_AllCriteriaPass_ExecutesTool()
    {
        // Arrange
        var toolLookup = new MockToolLookup(success: true, output: "tool output");
        var executor = new SafeToolExecutor(toolLookup)
            .AddCriterion("auth", (_, _) => Form.Mark)
            .AddCriterion("rate", (_, _) => Form.Mark);

        var toolCall = new ToolCall("test_tool", "test_args");
        var context = CreateMockContext();

        // Act
        var decision = await executor.ExecuteWithAudit(toolCall, context);

        // Assert
        decision.Certainty.IsMark().Should().BeTrue();
        decision.Result.IsSuccess.Should().BeTrue();
        decision.Result.Value.Output.Should().Be("tool output");
        decision.EvidenceTrail.Should().HaveCount(2);
        decision.EvidenceTrail.All(e => e.Evaluation.IsMark()).Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteWithAudit_OneCriterionFails_RejectsExecution()
    {
        // Arrange
        var toolLookup = new MockToolLookup(success: true, output: "should not execute");
        var executor = new SafeToolExecutor(toolLookup)
            .AddCriterion("auth", (_, _) => Form.Mark)
            .AddCriterion("rate", (_, _) => Form.Void); // This fails

        var toolCall = new ToolCall("test_tool", "test_args");
        var context = CreateMockContext();

        // Act
        var decision = await executor.ExecuteWithAudit(toolCall, context);

        // Assert
        decision.Certainty.IsVoid().Should().BeTrue();
        decision.Result.IsFailure.Should().BeTrue();
        decision.Reasoning.Should().Contain("rate");
        decision.EvidenceTrail.Should().Contain(e => e.CriterionName == "rate" && e.Evaluation.IsVoid());
    }

    [Fact]
    public async Task ExecuteWithAudit_OneCriterionUncertain_WithoutHandler_ReturnsUncertain()
    {
        // Arrange
        var toolLookup = new MockToolLookup(success: true, output: "output");
        var executor = new SafeToolExecutor(toolLookup)
            .AddCriterion("auth", (_, _) => Form.Mark)
            .AddCriterion("safety", (_, _) => Form.Imaginary); // Uncertain

        var toolCall = new ToolCall("test_tool", "test_args");
        var context = CreateMockContext();

        // Act
        var decision = await executor.ExecuteWithAudit(toolCall, context);

        // Assert
        decision.Certainty.IsImaginary().Should().BeTrue();
        decision.Result.IsFailure.Should().BeTrue();
        decision.Reasoning.Should().Contain("safety");
    }

    [Fact]
    public async Task ExecuteWithAudit_UncertainWithApprovalHandler_ExecutesOnApproval()
    {
        // Arrange
        var toolLookup = new MockToolLookup(success: true, output: "approved output");
        var executor = new SafeToolExecutor(toolLookup)
            .AddCriterion("safety", (_, _) => Form.Imaginary)
            .OnUncertain(async _ => await Task.FromResult(true)); // Approve

        var toolCall = new ToolCall("test_tool", "test_args");
        var context = CreateMockContext();

        // Act
        var decision = await executor.ExecuteWithAudit(toolCall, context);

        // Assert
        decision.Certainty.IsMark().Should().BeTrue();
        decision.Result.IsSuccess.Should().BeTrue();
        decision.EvidenceTrail.Should().Contain(e => e.CriterionName == "human_approval");
    }

    [Fact]
    public async Task ExecuteWithAudit_UncertainWithRejectionHandler_Rejects()
    {
        // Arrange
        var toolLookup = new MockToolLookup(success: true, output: "should not execute");
        var executor = new SafeToolExecutor(toolLookup)
            .AddCriterion("safety", (_, _) => Form.Imaginary)
            .OnUncertain(async _ => await Task.FromResult(false)); // Reject

        var toolCall = new ToolCall("test_tool", "test_args");
        var context = CreateMockContext();

        // Act
        var decision = await executor.ExecuteWithAudit(toolCall, context);

        // Assert
        decision.Certainty.IsVoid().Should().BeTrue();
        decision.Result.IsFailure.Should().BeTrue();
        decision.Reasoning.Should().Contain("Human review declined");
    }

    [Fact]
    public async Task ExecuteWithAudit_ToolNotFound_ReturnsRejection()
    {
        // Arrange
        var toolLookup = new MockToolLookup(toolExists: false);
        var executor = new SafeToolExecutor(toolLookup)
            .AddCriterion("auth", (_, _) => Form.Mark);

        var toolCall = new ToolCall("nonexistent_tool", "args");
        var context = CreateMockContext();

        // Act
        var decision = await executor.ExecuteWithAudit(toolCall, context);

        // Assert
        decision.Certainty.IsVoid().Should().BeTrue();
        decision.Result.IsFailure.Should().BeTrue();
        decision.Result.Error.Should().Contain("not found");
    }

    [Fact]
    public async Task ExecuteWithAudit_ToolThrowsException_ReturnsRejection()
    {
        // Arrange
        var toolLookup = new MockToolLookup(throwsException: true);
        var executor = new SafeToolExecutor(toolLookup)
            .AddCriterion("auth", (_, _) => Form.Mark);

        var toolCall = new ToolCall("failing_tool", "args");
        var context = CreateMockContext();

        // Act
        var decision = await executor.ExecuteWithAudit(toolCall, context);

        // Assert
        decision.Certainty.IsVoid().Should().BeTrue();
        decision.Result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ExecuteWithAudit_CriterionThrowsException_TreatsAsUncertain()
    {
        // Arrange
        var toolLookup = new MockToolLookup(success: true, output: "output");
        var executor = new SafeToolExecutor(toolLookup)
            .AddCriterion("auth", (_, _) => Form.Mark)
            .AddCriterion("buggy", (_, _) => throw new InvalidOperationException("Criterion failed"));

        var toolCall = new ToolCall("test_tool", "args");
        var context = CreateMockContext();

        // Act
        var decision = await executor.ExecuteWithAudit(toolCall, context);

        // Assert
        decision.Certainty.IsImaginary().Should().BeTrue();
        decision.EvidenceTrail.Should().Contain(e =>
            e.CriterionName == "buggy" && e.Evaluation.IsImaginary());
    }

    private static ExecutionContext CreateMockContext()
    {
        var user = new UserInfo("test_user", new HashSet<string> { "execute_tools" });
        var rateLimiter = new MockRateLimiter();
        var contentFilter = new MockContentFilter();

        return new ExecutionContext(user, rateLimiter, contentFilter);
    }

    private sealed class MockToolLookup : IToolLookup
    {
        private readonly bool toolExists;
        private readonly bool success;
        private readonly string output;
        private readonly bool throwsException;

        public MockToolLookup(
            bool toolExists = true,
            bool success = true,
            string output = "output",
            bool throwsException = false)
        {
            this.toolExists = toolExists;
            this.success = success;
            this.output = output;
            this.throwsException = throwsException;
        }

        public Option<IToolExecutor> GetTool(string toolName)
        {
            if (!this.toolExists)
            {
                return Option<IToolExecutor>.None();
            }

            return Option<IToolExecutor>.Some(new MockTool(this.success, this.output, this.throwsException));
        }
    }

    private sealed class MockTool : IToolExecutor
    {
        private readonly bool success;
        private readonly string output;
        private readonly bool throwsException;

        public MockTool(bool success, string output, bool throwsException)
        {
            this.success = success;
            this.output = output;
            this.throwsException = throwsException;
        }

        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (this.throwsException)
            {
                throw new InvalidOperationException("Tool execution failed");
            }

            var result = this.success
                ? Result<string, string>.Success(this.output)
                : Result<string, string>.Failure("Tool failed");

            return Task.FromResult(result);
        }
    }

    private sealed class MockRateLimiter : IRateLimiter
    {
        public bool IsAllowed(ToolCall toolCall) => true;

        public void Record(ToolCall toolCall)
        {
        }
    }

    private sealed class MockContentFilter : IContentFilter
    {
        public SafetyLevel Analyze(string content) => SafetyLevel.Safe;
    }
}
