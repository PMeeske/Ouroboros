// <copyright file="CausalMeTTaIntegrationTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Reasoning;

using FluentAssertions;
using Ouroboros.Core.Reasoning;
using Xunit;

/// <summary>
/// Tests for MeTTa integration with causal reasoning.
/// Validates conversion of causal graphs to symbolic MeTTa representation.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CausalMeTTaIntegrationTests
{
    [Fact]
    public void ConvertToMeTTa_WithValidGraph_ReturnsSuccess()
    {
        // Arrange
        var graph = this.CreateSimpleCausalGraph();

        // Act
        var result = CausalMeTTaIntegration.ConvertToMeTTa(graph);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNullOrEmpty();
        result.Value.Should().Contain("causal-space");
        result.Value.Should().Contain("variable");
        result.Value.Should().Contain("causes");
    }

    [Fact]
    public void ConvertToMeTTa_WithNullGraph_ReturnsFailure()
    {
        // Act
        var result = CausalMeTTaIntegration.ConvertToMeTTa(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("null");
    }

    [Fact]
    public void ConvertToMeTTa_ContainsAllVariables()
    {
        // Arrange
        var graph = this.CreateSimpleCausalGraph();

        // Act
        var result = CausalMeTTaIntegration.ConvertToMeTTa(graph);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("X");
        result.Value.Should().Contain("Y");
        result.Value.Should().Contain("Z");
    }

    [Fact]
    public void ConvertToMeTTa_ContainsAllEdges()
    {
        // Arrange
        var graph = this.CreateSimpleCausalGraph();

        // Act
        var result = CausalMeTTaIntegration.ConvertToMeTTa(graph);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("causes X Y");
        result.Value.Should().Contain("causes Y Z");
    }

    [Fact]
    public void EdgeToMeTTaAtom_CreatesValidAtom()
    {
        // Arrange
        var edge = new CausalEdge("X", "Y", 0.8, EdgeType.Direct);

        // Act
        var atom = CausalMeTTaIntegration.EdgeToMeTTaAtom(edge);

        // Assert
        atom.Should().Contain("causes");
        atom.Should().Contain("X");
        atom.Should().Contain("Y");
        atom.Should().Contain("0.8");
        atom.Should().Contain("direct");
    }

    [Fact]
    public void VariableToMeTTaAtom_CreatesValidAtom()
    {
        // Arrange
        var variable = new Variable("X", VariableType.Continuous, new List<object> { 0.0, 1.0 });

        // Act
        var atom = CausalMeTTaIntegration.VariableToMeTTaAtom(variable);

        // Assert
        atom.Should().Contain("variable");
        atom.Should().Contain("X");
        atom.Should().Contain("continuous");
    }

    [Fact]
    public void GenerateDSeparationQuery_CreatesValidQuery()
    {
        // Arrange
        var conditioningSet = new List<string> { "Z" };

        // Act
        var query = CausalMeTTaIntegration.GenerateDSeparationQuery("X", "Y", conditioningSet);

        // Assert
        query.Should().Contain("d-separated");
        query.Should().Contain("X");
        query.Should().Contain("Y");
        query.Should().Contain("Z");
    }

    [Fact]
    public void GenerateInterventionQuery_CreatesValidQuery()
    {
        // Act
        var query = CausalMeTTaIntegration.GenerateInterventionQuery("X", "Y");

        // Assert
        query.Should().Contain("intervention-effect");
        query.Should().Contain("X");
        query.Should().Contain("Y");
    }

    [Fact]
    public void GenerateCounterfactualQuery_CreatesValidQuery()
    {
        // Arrange
        var factual = new Observation(
            new Dictionary<string, object> { { "X", 1.0 }, { "Y", 2.0 } },
            DateTime.UtcNow,
            null);

        // Act
        var query = CausalMeTTaIntegration.GenerateCounterfactualQuery("X", "Y", factual);

        // Assert
        query.Should().Contain("counterfactual");
        query.Should().Contain("X");
        query.Should().Contain("Y");
        query.Should().Contain("observed");
    }

    [Fact]
    public void GeneratePathFindingRules_CreatesValidRules()
    {
        // Arrange
        var graph = this.CreateSimpleCausalGraph();

        // Act
        var rules = CausalMeTTaIntegration.GeneratePathFindingRules(graph);

        // Assert
        rules.Should().NotBeNullOrEmpty();
        rules.Should().Contain("path");
        rules.Should().Contain("causes");
    }

    [Fact]
    public void GenerateEffectComputationRules_CreatesValidRules()
    {
        // Arrange
        var graph = this.CreateSimpleCausalGraph();

        // Act
        var rules = CausalMeTTaIntegration.GenerateEffectComputationRules(graph);

        // Assert
        rules.Should().NotBeNullOrEmpty();
        rules.Should().Contain("total-effect");
        rules.Should().Contain("direct-effect");
        rules.Should().Contain("path-effect");
    }

    [Fact]
    public void ExplanationToMeTTa_WithValidExplanation_ReturnsSuccess()
    {
        // Arrange
        var explanation = this.CreateSimpleExplanation();

        // Act
        var result = CausalMeTTaIntegration.ExplanationToMeTTa(explanation);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("explanation");
        result.Value.Should().Contain("attributions");
        result.Value.Should().Contain("causal-paths");
    }

    [Fact]
    public void ExplanationToMeTTa_WithNullExplanation_ReturnsFailure()
    {
        // Act
        var result = CausalMeTTaIntegration.ExplanationToMeTTa(null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("null");
    }

    [Fact]
    public void GenerateInterventionPlanningRules_CreatesValidRules()
    {
        // Act
        var rules = CausalMeTTaIntegration.GenerateInterventionPlanningRules();

        // Assert
        rules.Should().NotBeNullOrEmpty();
        rules.Should().Contain("best-intervention");
        rules.Should().Contain("intervention-candidate");
        rules.Should().Contain("side-effects");
    }

    private CausalGraph CreateSimpleCausalGraph()
    {
        var variables = new List<Variable>
        {
            new Variable("X", VariableType.Continuous, new List<object> { 0.0, 1.0 }),
            new Variable("Y", VariableType.Continuous, new List<object> { 0.0, 1.0 }),
            new Variable("Z", VariableType.Continuous, new List<object> { 0.0, 1.0 }),
        };

        var edges = new List<CausalEdge>
        {
            new CausalEdge("X", "Y", 0.8, EdgeType.Direct),
            new CausalEdge("Y", "Z", 0.7, EdgeType.Direct),
        };

        var equations = new Dictionary<string, StructuralEquation>
        {
            ["Y"] = new StructuralEquation(
                "Y",
                new List<string> { "X" },
                vals => Convert.ToDouble(vals["X"]) * 0.8,
                0.1),
            ["Z"] = new StructuralEquation(
                "Z",
                new List<string> { "Y" },
                vals => Convert.ToDouble(vals["Y"]) * 0.7,
                0.1),
        };

        return new CausalGraph(variables, edges, equations);
    }

    private Explanation CreateSimpleExplanation()
    {
        var paths = new List<CausalPath>
        {
            new CausalPath(
                new List<string> { "X", "Y" },
                0.8,
                true,
                new List<CausalEdge> { new CausalEdge("X", "Y", 0.8, EdgeType.Direct) }),
        };

        var attributions = new Dictionary<string, double>
        {
            { "X", 0.8 },
            { "Y", 0.2 },
        };

        return new Explanation("Z", paths, attributions, "X is the primary cause");
    }
}
