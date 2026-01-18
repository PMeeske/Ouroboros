// <copyright file="OperatingCostAuditArrowsTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests;

using LangChain.DocumentLoaders;
using LangChain.Providers;

/// <summary>
/// Tests for the OperatingCostAuditArrows reasoning steps.
/// </summary>
[Trait("Category", "Unit")]
public class OperatingCostAuditArrowsTests
{
    private readonly ToolRegistry _tools = ToolRegistry.CreateDefault();
    private readonly TrackedVectorStore _store = new();

    [Fact]
    public void ExtractCategoriesArrow_ReturnsStepFunction()
    {
        // Arrange
        var mockLlm = CreateMockLlm("Extracted categories: heating, water, garbage");

        // Act
        var arrow = OperatingCostAuditArrows.ExtractCategoriesArrow(
            mockLlm,
            _tools,
            "Sample operating cost statement");

        // Assert
        arrow.Should().NotBeNull();
    }

    [Fact]
    public async Task ExtractCategoriesArrow_AddsReasoningToBranch()
    {
        // Arrange
        var mockLlm = CreateMockLlm("Extracted categories:\n- Heating: 500 EUR\n- Water: 200 EUR");
        var branch = new PipelineBranch("test", _store, DataSource.FromPath("."));

        var arrow = OperatingCostAuditArrows.ExtractCategoriesArrow(
            mockLlm,
            _tools,
            "Operating cost statement content");

        // Act
        var result = await arrow(branch);

        // Assert
        result.Events.Should().ContainSingle(e => e is ReasoningStep);
        var reasoningStep = result.Events.OfType<ReasoningStep>().First();
        reasoningStep.State.Should().BeOfType<Draft>();
        reasoningStep.State.Text.Should().Contain("Extracted categories");
    }

    [Fact]
    public void AnalyzeMainStatementArrow_ReturnsStepFunction()
    {
        // Arrange
        var mockLlm = CreateMockLlm("Analysis complete");

        // Act
        var arrow = OperatingCostAuditArrows.AnalyzeMainStatementArrow(
            mockLlm,
            _tools,
            "Sample statement");

        // Assert
        arrow.Should().NotBeNull();
    }

    [Fact]
    public async Task AnalyzeMainStatementArrow_AddsAnalysisToBranch()
    {
        // Arrange
        var analysisResult = @"
Analysis of operating cost statement:
- Total costs: OK - visible on main statement
- Reference metric: UNCLEAR - not labeled as living area or MEA
- Balance: OK - clearly shown";

        var mockLlm = CreateMockLlm(analysisResult);
        var branch = new PipelineBranch("test", _store, DataSource.FromPath("."));

        var arrow = OperatingCostAuditArrows.AnalyzeMainStatementArrow(
            mockLlm,
            _tools,
            "Operating cost statement content");

        // Act
        var result = await arrow(branch);

        // Assert
        result.Events.Should().ContainSingle(e => e is ReasoningStep);
        var reasoningStep = result.Events.OfType<ReasoningStep>().First();
        reasoningStep.State.Text.Should().Contain("Total costs: OK");
        reasoningStep.State.Text.Should().Contain("Reference metric: UNCLEAR");
    }

    [Fact]
    public async Task SafeAnalyzeMainStatementArrow_ReturnsSuccess_WithValidInput()
    {
        // Arrange
        var mockLlm = CreateMockLlm("Analysis complete");
        var branch = new PipelineBranch("test", _store, DataSource.FromPath("."));

        var arrow = OperatingCostAuditArrows.SafeAnalyzeMainStatementArrow(
            mockLlm,
            _tools,
            "Valid statement content");

        // Act
        var result = await arrow(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.GetValueOrDefault(branch).Events.Should().ContainSingle(e => e is ReasoningStep);
    }

    [Fact]
    public async Task SafeAnalyzeMainStatementArrow_ReturnsFailure_WithEmptyInput()
    {
        // Arrange
        var mockLlm = CreateMockLlm("Analysis");
        var branch = new PipelineBranch("test", _store, DataSource.FromPath("."));

        var arrow = OperatingCostAuditArrows.SafeAnalyzeMainStatementArrow(
            mockLlm,
            _tools,
            string.Empty);

        // Act
        var result = await arrow(branch);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Match(
            _ => string.Empty,
            error => error).Should().Contain("Main statement cannot be empty");
    }

    [Fact]
    public async Task SafeAnalyzeMainStatementArrow_ReturnsFailure_WithWhitespaceInput()
    {
        // Arrange
        var mockLlm = CreateMockLlm("Analysis");
        var branch = new PipelineBranch("test", _store, DataSource.FromPath("."));

        var arrow = OperatingCostAuditArrows.SafeAnalyzeMainStatementArrow(
            mockLlm,
            _tools,
            "   ");

        // Act
        var result = await arrow(branch);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public void CompareWithHoaArrow_ReturnsStepFunction()
    {
        // Arrange
        var mockLlm = CreateMockLlm("Comparison complete");

        // Act
        var arrow = OperatingCostAuditArrows.CompareWithHoaArrow(
            mockLlm,
            _tools,
            "Main statement",
            "HOA statement");

        // Assert
        arrow.Should().NotBeNull();
    }

    [Fact]
    public async Task CompareWithHoaArrow_AddsCritiqueToBranch()
    {
        // Arrange
        var comparisonResult = @"
Comparison with HOA statement:
- Total heating costs match: 500 EUR in both documents
- Discrepancy in water costs: Main shows 200 EUR, HOA shows 180 EUR";

        var mockLlm = CreateMockLlm(comparisonResult);
        var branch = new PipelineBranch("test", _store, DataSource.FromPath("."));

        var arrow = OperatingCostAuditArrows.CompareWithHoaArrow(
            mockLlm,
            _tools,
            "Main statement",
            "HOA statement");

        // Act
        var result = await arrow(branch);

        // Assert
        result.Events.Should().ContainSingle(e => e is ReasoningStep);
        var reasoningStep = result.Events.OfType<ReasoningStep>().First();
        reasoningStep.State.Should().BeOfType<Critique>();
        reasoningStep.State.Text.Should().Contain("Discrepancy");
    }

    [Fact]
    public void CheckAllocationRulesArrow_ReturnsStepFunction()
    {
        // Arrange
        var mockLlm = CreateMockLlm("Allocation check complete");

        // Act
        var arrow = OperatingCostAuditArrows.CheckAllocationRulesArrow(
            mockLlm,
            _tools,
            "Main statement",
            "Rental agreement rules");

        // Assert
        arrow.Should().NotBeNull();
    }

    [Fact]
    public async Task CheckAllocationRulesArrow_AddsCritiqueToBranch()
    {
        // Arrange
        var checkResult = @"
Allocation rule comparison:
- Heating: Contractual = living area, Applied = living area - MATCH
- Water: Contractual = per person, Applied = living area - MISMATCH";

        var mockLlm = CreateMockLlm(checkResult);
        var branch = new PipelineBranch("test", _store, DataSource.FromPath("."));

        var arrow = OperatingCostAuditArrows.CheckAllocationRulesArrow(
            mockLlm,
            _tools,
            "Main statement",
            "§7 allocation: heating by living area, water per person");

        // Act
        var result = await arrow(branch);

        // Assert
        result.Events.Should().ContainSingle(e => e is ReasoningStep);
        var reasoningStep = result.Events.OfType<ReasoningStep>().First();
        reasoningStep.State.Should().BeOfType<Critique>();
        reasoningStep.State.Text.Should().Contain("MISMATCH");
    }

    [Fact]
    public void GenerateAuditReportArrow_ReturnsStepFunction()
    {
        // Arrange
        var mockLlm = CreateMockLlm("Report generated");

        // Act
        var arrow = OperatingCostAuditArrows.GenerateAuditReportArrow(mockLlm, _tools);

        // Assert
        arrow.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateAuditReportArrow_AddsFinalSpecToBranch()
    {
        // Arrange
        var reportJson = @"{
  ""documents_analyzed"": true,
  ""overall_formal_status"": ""incomplete"",
  ""categories"": [{""category"": ""heating"", ""total_costs"": ""OK""}],
  ""critical_gaps"": [""Reference metric not labeled""],
  ""summary_short"": ""Statement incomplete"",
  ""note"": ""This output does not contain legal evaluation.""
}";

        var mockLlm = CreateMockLlm(reportJson);
        var branch = new PipelineBranch("test", _store, DataSource.FromPath("."));

        var arrow = OperatingCostAuditArrows.GenerateAuditReportArrow(mockLlm, _tools);

        // Act
        var result = await arrow(branch);

        // Assert
        result.Events.Should().ContainSingle(e => e is ReasoningStep);
        var reasoningStep = result.Events.OfType<ReasoningStep>().First();
        reasoningStep.State.Should().BeOfType<FinalSpec>();
        reasoningStep.State.Text.Should().Contain("documents_analyzed");
    }

    [Fact]
    public async Task SafeGenerateAuditReportArrow_ReturnsSuccess_WhenAnalysisExists()
    {
        // Arrange
        var mockLlm = CreateMockLlm(@"{""documents_analyzed"": true}");
        var branch = new PipelineBranch("test", _store, DataSource.FromPath("."));

        // Add initial analysis to branch
        branch = branch.WithReasoning(new Draft("Initial analysis"), "prompt", null);

        var arrow = OperatingCostAuditArrows.SafeGenerateAuditReportArrow(mockLlm, _tools);

        // Act
        var result = await arrow(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SafeGenerateAuditReportArrow_ReturnsFailure_WhenNoAnalysisExists()
    {
        // Arrange
        var mockLlm = CreateMockLlm("Report");
        var branch = new PipelineBranch("test", _store, DataSource.FromPath("."));

        var arrow = OperatingCostAuditArrows.SafeGenerateAuditReportArrow(mockLlm, _tools);

        // Act
        var result = await arrow(branch);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Match(
            _ => string.Empty,
            error => error).Should().Contain("No analysis results available");
    }

    [Fact]
    public void SafeBasicAuditPipeline_ReturnsKleisliArrow()
    {
        // Arrange
        var mockLlm = CreateMockLlm("Pipeline result");

        // Act
        var pipeline = OperatingCostAuditArrows.SafeBasicAuditPipeline(
            mockLlm,
            _tools,
            "Statement content");

        // Assert
        pipeline.Should().NotBeNull();
    }

    [Fact]
    public async Task SafeBasicAuditPipeline_ExecutesTwoSteps()
    {
        // Arrange
        int callCount = 0;
        var mockLlm = CreateCallCountingMockLlm(() =>
        {
            callCount++;
            return callCount == 1
                ? "Analysis: Heating costs OK, Water UNCLEAR"
                : @"{""documents_analyzed"": true, ""summary_short"": ""Complete""}";
        });

        var branch = new PipelineBranch("test", _store, DataSource.FromPath("."));

        var pipeline = OperatingCostAuditArrows.SafeBasicAuditPipeline(
            mockLlm,
            _tools,
            "Statement content");

        // Act
        var result = await pipeline(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        callCount.Should().Be(2); // Analysis + Report generation
    }

    [Fact]
    public void FullAuditPipeline_ReturnsStepFunction()
    {
        // Arrange
        var mockLlm = CreateMockLlm("Pipeline result");

        // Act
        var pipeline = OperatingCostAuditArrows.FullAuditPipeline(
            mockLlm,
            _tools,
            "Main statement",
            "HOA statement",
            "Rental agreement rules");

        // Assert
        pipeline.Should().NotBeNull();
    }

    [Fact]
    public async Task FullAuditPipeline_ExecutesAllSteps_WhenAllInputsProvided()
    {
        // Arrange
        int callCount = 0;
        var mockLlm = CreateCallCountingMockLlm(() =>
        {
            callCount++;
            return $"Step {callCount} result";
        });

        var branch = new PipelineBranch("test", _store, DataSource.FromPath("."));

        var pipeline = OperatingCostAuditArrows.FullAuditPipeline(
            mockLlm,
            _tools,
            "Main statement",
            "HOA statement",
            "Rental agreement rules");

        // Act
        await pipeline(branch);

        // Assert
        callCount.Should().Be(4); // Analysis + HOA Compare + Allocation Check + Report
    }

    [Fact]
    public async Task FullAuditPipeline_SkipsHoaComparison_WhenHoaStatementNull()
    {
        // Arrange
        int callCount = 0;
        var mockLlm = CreateCallCountingMockLlm(() =>
        {
            callCount++;
            return $"Step {callCount} result";
        });

        var branch = new PipelineBranch("test", _store, DataSource.FromPath("."));

        var pipeline = OperatingCostAuditArrows.FullAuditPipeline(
            mockLlm,
            _tools,
            "Main statement",
            hoaStatement: null,
            "Rental agreement rules");

        // Act
        await pipeline(branch);

        // Assert
        callCount.Should().Be(3); // Analysis + Allocation Check + Report (no HOA)
    }

    [Fact]
    public async Task FullAuditPipeline_SkipsAllocationCheck_WhenRentalRulesNull()
    {
        // Arrange
        int callCount = 0;
        var mockLlm = CreateCallCountingMockLlm(() =>
        {
            callCount++;
            return $"Step {callCount} result";
        });

        var branch = new PipelineBranch("test", _store, DataSource.FromPath("."));

        var pipeline = OperatingCostAuditArrows.FullAuditPipeline(
            mockLlm,
            _tools,
            "Main statement",
            "HOA statement",
            rentalAgreementRules: null);

        // Act
        await pipeline(branch);

        // Assert
        callCount.Should().Be(3); // Analysis + HOA Compare + Report (no allocation check)
    }

    [Fact]
    public async Task FullAuditPipeline_ExecutesMinimalSteps_WhenNoOptionalInputs()
    {
        // Arrange
        int callCount = 0;
        var mockLlm = CreateCallCountingMockLlm(() =>
        {
            callCount++;
            return $"Step {callCount} result";
        });

        var branch = new PipelineBranch("test", _store, DataSource.FromPath("."));

        var pipeline = OperatingCostAuditArrows.FullAuditPipeline(
            mockLlm,
            _tools,
            "Main statement");

        // Act
        await pipeline(branch);

        // Assert
        callCount.Should().Be(2); // Analysis + Report only
    }

    /// <summary>
    /// Mock implementation of IChatCompletionModel for testing.
    /// </summary>
    private sealed class MockChatCompletionModel : IChatCompletionModel
    {
        private readonly string _response;

        public MockChatCompletionModel(string response)
        {
            _response = response;
        }

        public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
            => Task.FromResult(_response);
    }

    /// <summary>
    /// Mock implementation that counts calls and returns dynamic responses.
    /// </summary>
    private sealed class CallCountingMockChatModel : IChatCompletionModel
    {
        private readonly Func<string> _responseFactory;

        public CallCountingMockChatModel(Func<string> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
            => Task.FromResult(_responseFactory());
    }

    private static ToolAwareChatModel CreateMockLlm(string response)
        => new ToolAwareChatModel(new MockChatCompletionModel(response), ToolRegistry.CreateDefault());

    private static ToolAwareChatModel CreateCallCountingMockLlm(Func<string> responseFactory)
        => new ToolAwareChatModel(new CallCountingMockChatModel(responseFactory), ToolRegistry.CreateDefault());
}
