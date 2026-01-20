// <copyright file="QueryPlanTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Pipeline.GraphRAG.Models;

namespace Ouroboros.Tests.GraphRAG;

/// <summary>
/// Tests for QueryPlan and related types.
/// </summary>
[Trait("Category", "Unit")]
public class QueryPlanTests
{
    [Fact]
    public void QueryPlan_SingleHop_ShouldCreateSimplePlan()
    {
        // Act
        var plan = QueryPlan.SingleHop("Find all engineers");

        // Assert
        plan.OriginalQuery.Should().Be("Find all engineers");
        plan.QueryType.Should().Be(QueryType.SingleHop);
        plan.Steps.Should().HaveCount(1);
        plan.Steps[0].StepType.Should().Be(QueryStepType.VectorSearch);
    }

    [Fact]
    public void QueryPlan_EstimatedComplexity_ShouldReflectStepsAndType()
    {
        // Arrange
        var singleHop = QueryPlan.SingleHop("Simple query");
        var multiHop = new QueryPlan(
            "Complex query",
            QueryType.MultiHop,
            [
                new QueryStep(1, QueryStepType.VectorSearch, "step1", []),
                new QueryStep(2, QueryStepType.GraphTraversal, "step2", [1]),
                new QueryStep(3, QueryStepType.Inference, "step3", [2])
            ]);

        // Assert
        singleHop.EstimatedComplexity.Should().Be(1); // 1 step, no multi-hop penalty
        multiHop.EstimatedComplexity.Should().Be(5); // 3 steps + 2 for multi-hop
    }

    [Fact]
    public void QueryStep_ShouldStoreDependencies()
    {
        // Act
        var step = new QueryStep(
            2,
            QueryStepType.GraphTraversal,
            "Traverse from entity",
            [1])
        {
            EntityTypeFilter = ["Person", "Organization"],
            MaxHops = 3
        };

        // Assert
        step.Order.Should().Be(2);
        step.StepType.Should().Be(QueryStepType.GraphTraversal);
        step.Dependencies.Should().Contain(1);
        step.EntityTypeFilter.Should().HaveCount(2);
        step.MaxHops.Should().Be(3);
    }

    [Fact]
    public void QueryType_ShouldHaveExpectedValues()
    {
        // Assert
        Enum.GetValues<QueryType>().Should().HaveCount(4);
        QueryType.SingleHop.Should().BeDefined();
        QueryType.MultiHop.Should().BeDefined();
        QueryType.Aggregation.Should().BeDefined();
        QueryType.Comparison.Should().BeDefined();
    }

    [Fact]
    public void QueryStepType_ShouldHaveExpectedValues()
    {
        // Assert
        Enum.GetValues<QueryStepType>().Should().HaveCount(8);
        QueryStepType.VectorSearch.Should().BeDefined();
        QueryStepType.GraphTraversal.Should().BeDefined();
        QueryStepType.SymbolicMatch.Should().BeDefined();
        QueryStepType.TypeFilter.Should().BeDefined();
        QueryStepType.PropertyFilter.Should().BeDefined();
        QueryStepType.Aggregate.Should().BeDefined();
        QueryStepType.Rank.Should().BeDefined();
        QueryStepType.Inference.Should().BeDefined();
    }
}
