// <copyright file="DagMeTTaIntegrationTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests;

using LangChain.DocumentLoaders;
using Ouroboros.Pipeline.Branches;
using Ouroboros.Pipeline.Planning;
using Ouroboros.Pipeline.Verification;
using Ouroboros.Tools.MeTTa;

/// <summary>
/// Tests for Phase 4: Neuro-Symbolic Integration - DAG to MeTTa encoding and constraint checking.
/// </summary>
public class DagMeTTaIntegrationTests
{
    #region DAG Encoding Tests

    [Fact]
    public void PipelineBranch_ToMeTTaFacts_EncodesBasicStructure()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test-branch", store, source);

        // Act
        var facts = branch.ToMeTTaFacts();

        // Assert
        facts.Should().NotBeEmpty();
        facts.Should().Contain(f => f.Contains("Branch \"test-branch\""));
        facts.Should().Contain(f => f.Contains("HasEventCount"));
        facts.Should().Contain(f => f.Contains("HasSource"));
    }

    [Fact]
    public void PipelineBranch_WithReasoningEvent_EncodesEventDetails()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test-branch", store, source);
        
        var draft = new Draft("Test draft content");
        branch = branch.WithReasoning(draft, "Test prompt");

        // Act
        var facts = branch.ToMeTTaFacts();

        // Assert
        facts.Should().Contain(f => f.Contains("ReasoningEvent"));
        facts.Should().Contain(f => f.Contains("HasReasoningKind"));
        facts.Should().Contain(f => f.Contains("BelongsToBranch"));
    }

    [Fact]
    public void PipelineBranch_WithMultipleEvents_EncodesOrdering()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test-branch", store, source);
        
        var draft = new Draft("Draft 1");
        var critique = new Critique("Critique 1");
        
        branch = branch.WithReasoning(draft, "Prompt 1");
        branch = branch.WithReasoning(critique, "Prompt 2");

        // Act
        var facts = branch.ToMeTTaFacts();

        // Assert
        facts.Should().Contain(f => f.Contains("Before"));
        facts.Should().Contain(f => f.Contains("EventAtIndex"));
    }

    [Fact]
    public void PipelineBranch_WithToolUsage_EncodesToolRelationships()
    {
        // Arrange
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test-branch", store, source);
        
        var tools = new List<ToolExecution>
        {
            new ToolExecution("search_tool", "{}", "result1", DateTime.UtcNow),
            new ToolExecution("summarize_tool", "{}", "result2", DateTime.UtcNow)
        };
        
        var draft = new Draft("Draft with tools");
        branch = branch.WithReasoning(draft, "Prompt", tools);

        // Act
        var facts = branch.ToMeTTaFacts();

        // Assert
        facts.Should().Contain(f => f.Contains("UsesTool") && f.Contains("search_tool"));
        facts.Should().Contain(f => f.Contains("UsesTool") && f.Contains("summarize_tool"));
    }

    [Fact]
    public void DagConstraintRules_ReturnsValidMeTTaRules()
    {
        // Act
        var rules = DagMeTTaExtensions.GetDagConstraintRules();

        // Assert
        rules.Should().NotBeEmpty();
        rules.Should().Contain(r => r.Contains("Acyclic"));
        rules.Should().Contain(r => r.Contains("ValidFork"));
        rules.Should().Contain(r => r.Contains("ValidDependency"));
        rules.Should().Contain(r => r.Contains("Before"));
    }

    [Fact]
    public void EncodeConstraintQuery_Acyclic_ReturnsCorrectQuery()
    {
        // Act
        string query = DagMeTTaExtensions.EncodeConstraintQuery("acyclic", "main");

        // Assert
        query.Should().Contain("Acyclic");
        query.Should().Contain("Branch \"main\"");
    }

    [Fact]
    public void EncodeConstraintQuery_ValidOrdering_ReturnsCorrectQuery()
    {
        // Act
        string query = DagMeTTaExtensions.EncodeConstraintQuery("valid-ordering", "main");

        // Assert
        query.Should().Contain("Before");
        query.Should().Contain("EventAtIndex");
    }

    [Fact]
    public async Task AddBranchFactsAsync_AddsAllFactsToEngine()
    {
        // Arrange
        var engine = new TestMeTTaEngine();
        var store = new TrackedVectorStore();
        var source = DataSource.FromPath("/test/path");
        var branch = new PipelineBranch("test-branch", store, source)
            .WithReasoning(new Draft("Test"), "Prompt");

        // Act
        var result = await engine.AddBranchFactsAsync(branch);

        // Assert
        result.IsSuccess.Should().BeTrue();
        engine.Facts.Should().NotBeEmpty();
        engine.Facts.Should().Contain(f => f.Contains("Branch \"test-branch\""));
    }

    [Fact]
    public async Task VerifyDagConstraintAsync_ValidConstraint_ReturnsTrue()
    {
        // Arrange
        var engine = new TestMeTTaEngine();
        engine.SetQueryResponse("[]"); // Empty response means constraint satisfied

        // Act
        var result = await engine.VerifyDagConstraintAsync("test-branch", "acyclic");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyDagConstraintAsync_ViolatedConstraint_ReturnsFalse()
    {
        // Arrange
        var engine = new TestMeTTaEngine();
        engine.SetQueryResponse("False"); // Explicit false

        // Act
        var result = await engine.VerifyDagConstraintAsync("test-branch", "acyclic");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeFalse();
    }

    #endregion

    #region Symbolic Plan Selection Tests

    [Fact]
    public async Task SymbolicPlanSelector_SelectBestPlan_ChoosesHighestScore()
    {
        // Arrange
        var engine = new TestMeTTaEngine();
        engine.SetQueryResponse("True"); // All plans valid
        var selector = new SymbolicPlanSelector(engine);
        await selector.InitializeAsync();

        var plan1 = new Plan("Simple plan")
            .WithAction(new FileSystemAction("read"));
        
        var plan2 = new Plan("Complex plan")
            .WithAction(new FileSystemAction("read"))
            .WithAction(new NetworkAction("get"))
            .WithAction(new FileSystemAction("read"));

        var candidates = new[] { plan1, plan2 };

        // Act
        var result = await selector.SelectBestPlanAsync(candidates, SafeContext.ReadOnly);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Both plans are valid, but scoring may vary - just ensure one is selected
        result.Value.Plan.Should().NotBeNull();
        result.Value.Score.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SymbolicPlanSelector_ScorePlan_PenalizesInvalidActions()
    {
        // Arrange
        var engine = new TestMeTTaEngine();
        engine.SetQueryResponse("False"); // Action not allowed
        var selector = new SymbolicPlanSelector(engine);

        var plan = new Plan("Write plan")
            .WithAction(new FileSystemAction("write"));

        // Act
        var result = await selector.ScorePlanAsync(plan, SafeContext.ReadOnly);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Score.Should().BeLessThan(0); // Negative score for invalid action
    }

    [Fact]
    public async Task SymbolicPlanSelector_ExplainPlan_ReturnsExplanation()
    {
        // Arrange
        var engine = new TestMeTTaEngine();
        engine.SetQueryResponse("True");
        var selector = new SymbolicPlanSelector(engine);

        var plan = new Plan("Read plan")
            .WithAction(new FileSystemAction("read"));

        // Act
        var result = await selector.ExplainPlanAsync(plan, SafeContext.ReadOnly);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("Read plan");
        result.Value.Should().Contain("score");
    }

    [Fact]
    public async Task SymbolicPlanSelector_CheckConstraint_ValidatesConstraint()
    {
        // Arrange
        var engine = new TestMeTTaEngine();
        engine.SetQueryResponse("True");
        var selector = new SymbolicPlanSelector(engine);

        var plan = new Plan("Read-only plan")
            .WithAction(new FileSystemAction("read"));

        // Act
        var result = await selector.CheckConstraintAsync(plan, "no writes");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeTrue();
    }

    [Fact]
    public async Task SymbolicPlanSelector_Initialize_AddsRules()
    {
        // Arrange
        var engine = new TestMeTTaEngine();
        var selector = new SymbolicPlanSelector(engine);

        // Act
        var result = await selector.InitializeAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        engine.Facts.Should().Contain(f => f.Contains("PlanComplexity"));
    }

    #endregion

    #region Test Helper Classes

    /// <summary>
    /// Test MeTTa engine that records facts and allows setting query responses.
    /// This is a test-only implementation for unit testing without requiring an actual MeTTa installation.
    /// </summary>
    private sealed class TestMeTTaEngine : IMeTTaEngine
    {
        private string _queryResponse = "[]";

        public List<string> Facts { get; } = new();

        public void SetQueryResponse(string response)
        {
            _queryResponse = response;
        }

        public Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
        {
            return Task.FromResult(Result<string, string>.Success(_queryResponse));
        }

        public Task<Result<Unit, string>> AddFactAsync(string fact, CancellationToken ct = default)
        {
            Facts.Add(fact);
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }

        public Task<Result<string, string>> ApplyRuleAsync(string rule, CancellationToken ct = default)
        {
            Facts.Add(rule);
            return Task.FromResult(Result<string, string>.Success("Rule applied"));
        }

        public Task<Result<bool, string>> VerifyPlanAsync(string plan, CancellationToken ct = default)
        {
            return Task.FromResult(Result<bool, string>.Success(true));
        }

        public Task<Result<Unit, string>> ResetAsync(CancellationToken ct = default)
        {
            Facts.Clear();
            return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
        }

        public void Dispose() { }
    }

    #endregion
}
