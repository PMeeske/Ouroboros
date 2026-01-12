// <copyright file="LawsOfFormIntegrationTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.LawsOfForm;

using FluentAssertions;
using Ouroboros.Core.LawsOfForm;
using Ouroboros.Core.Monads;
using Xunit;

/// <summary>
/// Integration tests demonstrating the full LLM safety pipeline.
/// Shows how all Laws of Form components work together for safe tool execution.
/// </summary>
[Trait("Category", "Integration")]
public class LawsOfFormIntegrationTests
{
    [Fact]
    public async Task FullPipeline_AllSafetyChecksPass_ExecutesSuccessfully()
    {
        // Arrange - Setup the complete safety pipeline
        var toolLookup = new TestToolLookup();
        var approvalQueue = new ToolApprovalQueue();
        var executor = new SafeToolExecutor(toolLookup)
            .AddCriterion("authorization", (call, ctx) =>
                ctx.User.HasPermission(call.ToolName).ToForm())
            .AddCriterion("rate_limit", (call, ctx) =>
                ctx.RateLimiter.IsAllowed(call).ToForm())
            .AddCriterion("content_safety", (call, ctx) =>
                ctx.ContentFilter.Analyze(call.Arguments) switch
                {
                    SafetyLevel.Safe => Form.Mark,
                    SafetyLevel.Unsafe => Form.Void,
                    SafetyLevel.Uncertain => Form.Imaginary,
                    _ => Form.Void // Default to unsafe for unknown values
                })
            .AddCriterion("model_confidence", (call, ctx) =>
                call.Confidence.ToForm(highThreshold: 0.85, lowThreshold: 0.4));

        var toolCall = new ToolCall("safe_tool", "safe_args", confidence: 0.9);
        var context = CreateTestContext();

        // Act - Execute with full safety audit
        var decision = await executor.ExecuteWithAudit(toolCall, context);

        // Assert - All checks passed, tool executed
        decision.Certainty.IsMark().Should().BeTrue();
        decision.Result.IsSuccess.Should().BeTrue();
        decision.EvidenceTrail.Should().HaveCount(4);
        decision.EvidenceTrail.All(e => e.Evaluation.IsMark()).Should().BeTrue();

        // Verify audit trail
        var auditLog = decision.ToAuditEntry();
        auditLog.Should().Contain("authorization");
        auditLog.Should().Contain("rate_limit");
        auditLog.Should().Contain("content_safety");
        auditLog.Should().Contain("model_confidence");
    }

    [Fact]
    public async Task FullPipeline_UncertainSafety_RoutesToHumanApproval()
    {
        // Arrange - Tool with uncertain content
        var toolLookup = new TestToolLookup();
        var approvalQueue = new ToolApprovalQueue();

        var executor = new SafeToolExecutor(toolLookup)
            .AddCriterion("authorization", (call, ctx) => Form.Mark)
            .AddCriterion("content_safety", (call, ctx) => Form.Imaginary) // Uncertain
            .OnUncertain(async (call, ctx) =>
            {
                // Simulate human approval flow
                var uncertainDecision = AuditableDecision<ToolResult>.Uncertain("needs review", "uncertain safety");
                var queueId = approvalQueue.Enqueue(call, uncertainDecision);

                // Simulate immediate approval for test
                await approvalQueue.Resolve(queueId, approved: true, "Human reviewer approved");
                return true;
            });

        var toolCall = new ToolCall("uncertain_tool", "uncertain_args");
        var context = CreateTestContext();

        // Act
        var decision = await executor.ExecuteWithAudit(toolCall, context);

        // Assert - Human approved, tool executed
        decision.Certainty.IsMark().Should().BeTrue();
        decision.Result.IsSuccess.Should().BeTrue();
        decision.EvidenceTrail.Should().Contain(e => e.CriterionName == "human_approval");
    }

    [Fact]
    public async Task FullPipeline_MultipleModels_ConsensusCheck()
    {
        // Arrange - Multiple LLM responses about same topic
        var detector = new ContradictionDetector(new SimpleClaimExtractor());
        var responses = new[]
        {
            new LlmResponse("Python is a high-level programming language", confidence: 0.9),
            new LlmResponse("Python is good for data science", confidence: 0.85),
            new LlmResponse("Python has dynamic typing", confidence: 0.9)
        };

        // Act - Check for contradictions
        var consistency = detector.AnalyzeMultiple(responses);

        // Also aggregate by confidence
        var consensusResult = ConfidenceGatedPipeline.AggregateResponses(responses);

        // Assert - Consistent and high confidence
        consistency.IsMark().Should().BeTrue("responses should be consistent");
        consensusResult.IsSuccess.Should().BeTrue();
        consensusResult.Value.Confidence.Should().BeGreaterThanOrEqualTo(0.85);
    }

    [Fact]
    public async Task FullPipeline_ContradictoryResponses_DetectsIssue()
    {
        // Arrange - Contradictory claims
        var claims = new[]
        {
            new Claim("The system is secure", 0.9, "model1"),
            new Claim("The system is not secure", 0.9, "model2")
        };

        var extractor = new TestClaimExtractor(claims);
        var detector = new ContradictionDetector(extractor);

        var response = new LlmResponse("contradictory content", confidence: 0.9);

        // Act
        var consistency = detector.Analyze(response);

        // Assert - Contradiction detected (Imaginary state)
        consistency.IsImaginary().Should().BeTrue("contradictory claims should result in imaginary form");
    }

    [Fact]
    public async Task FullPipeline_ConfidenceGating_FiltersLowConfidence()
    {
        // Arrange - Mixed confidence responses
        var responses = new[]
        {
            new LlmResponse("High confidence answer", confidence: 0.95),
            new LlmResponse("Low confidence guess", confidence: 0.4),
            new LlmResponse("Medium confidence", confidence: 0.75),
            new LlmResponse("Another high confidence", confidence: 0.9)
        };

        var gate = ConfidenceGatedPipeline.FilterByConfidence(threshold: 0.8);

        // Act
        var filtered = await gate(responses);

        // Assert - Only high confidence responses remain
        filtered.Should().HaveCount(2);
        filtered.All(r => r.Confidence >= 0.8).Should().BeTrue();
    }

    [Fact]
    public async Task FullPipeline_CombinedSafety_MultiCriteriaDecision()
    {
        // Arrange - Simulate real-world multi-criteria decision
        var toolLookup = new TestToolLookup();
        var executor = new SafeToolExecutor(toolLookup)
            .AddCriterion("auth", (_, ctx) => ctx.User.HasPermission("admin").ToForm())
            .AddCriterion("rate", (_, ctx) => ctx.RateLimiter.IsAllowed(null!).ToForm())
            .AddCriterion("content", (call, ctx) =>
            {
                var safety = ctx.ContentFilter.Analyze(call.Arguments);
                return safety switch
                {
                    SafetyLevel.Safe => Form.Mark,
                    SafetyLevel.Unsafe => Form.Void,
                    _ => Form.Imaginary
                };
            })
            .AddCriterion("confidence", (call, _) => call.Confidence.ToForm());

        var context = CreateTestContext(hasAdminPermission: true);
        var toolCall = new ToolCall("admin_tool", "safe_content", confidence: 0.95);

        // Act
        var decision = await executor.ExecuteWithAudit(toolCall, context);

        // Assert - All criteria evaluated and combined using AND logic
        decision.Certainty.IsMark().Should().BeTrue();
        decision.EvidenceTrail.Should().HaveCount(4);

        // Verify Laws of Form logic: All Mark → Mark (using FormExtensions.All)
        var allForms = decision.EvidenceTrail.Select(e => e.Evaluation).ToArray();
        var combined = FormExtensions.All(allForms);
        combined.IsMark().Should().BeTrue();
    }

    [Fact]
    public void LawsOfForm_Properties_DoubleNegation()
    {
        // Demonstrate fundamental Laws of Form property
        // In classical logic and Laws of Form: Not(Not(x)) = x (involution)
        var form = Form.Mark;
        var doubleNegated = form.Not().Not();

        // Double negation returns to the original state (involution property)
        doubleNegated.IsMark().Should().BeTrue();
    }

    [Fact]
    public void LawsOfForm_Properties_ImaginaryReEntry()
    {
        // Demonstrate re-entry property: ⌐imaginary = imaginary
        var imaginary = Form.Imaginary;
        var negated = imaginary.Not();

        negated.IsImaginary().Should().BeTrue("imaginary is self-negating");
    }

    [Fact]
    public void LawsOfForm_Properties_Conjunction()
    {
        // Demonstrate conjunction (AND) properties
        (Form.Mark & Form.Mark).IsMark().Should().BeTrue("mark AND mark = mark");
        (Form.Mark & Form.Void).IsVoid().Should().BeTrue("mark AND void = void");
        (Form.Mark & Form.Imaginary).IsImaginary().Should().BeTrue("anything AND imaginary = imaginary");
    }

    [Fact]
    public void LawsOfForm_Properties_Disjunction()
    {
        // Demonstrate disjunction (OR) properties
        (Form.Mark | Form.Void).IsMark().Should().BeTrue("mark OR void = mark");
        (Form.Void | Form.Void).IsVoid().Should().BeTrue("void OR void = void");
        (Form.Void | Form.Imaginary).IsImaginary().Should().BeTrue("void OR imaginary = imaginary");
    }

    private static ExecutionContext CreateTestContext(bool hasAdminPermission = false)
    {
        var permissions = new HashSet<string> { "execute_tools", "safe_tool" };
        if (hasAdminPermission)
        {
            permissions.Add("admin");
        }

        var user = new UserInfo("test_user", permissions);
        var rateLimiter = new TestRateLimiter();
        var contentFilter = new TestContentFilter();

        return new ExecutionContext(user, rateLimiter, contentFilter);
    }

    private sealed class TestToolLookup : IToolLookup
    {
        public Option<IToolExecutor> GetTool(string toolName)
        {
            return Option<IToolExecutor>.Some(new TestTool());
        }
    }

    private sealed class TestTool : IToolExecutor
    {
        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            return Task.FromResult(Result<string, string>.Success($"Executed with input: {input}"));
        }
    }

    private sealed class TestRateLimiter : IRateLimiter
    {
        public bool IsAllowed(ToolCall toolCall) => true;

        public void Record(ToolCall toolCall)
        {
        }
    }

    private sealed class TestContentFilter : IContentFilter
    {
        public SafetyLevel Analyze(string content)
        {
            if (content.Contains("unsafe"))
            {
                return SafetyLevel.Unsafe;
            }

            if (content.Contains("uncertain"))
            {
                return SafetyLevel.Uncertain;
            }

            return SafetyLevel.Safe;
        }
    }

    private sealed class TestClaimExtractor : IClaimExtractor
    {
        private readonly IReadOnlyList<Claim> claims;

        public TestClaimExtractor(IReadOnlyList<Claim> claims)
        {
            this.claims = claims;
        }

        public IReadOnlyList<Claim> ExtractClaims(string text, string source)
        {
            return this.claims;
        }
    }
}
