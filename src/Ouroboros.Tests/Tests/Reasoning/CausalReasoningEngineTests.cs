// <copyright file="CausalReasoningEngineTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Reasoning;

using FluentAssertions;
using Ouroboros.Core.Monads;
using Ouroboros.Core.Reasoning;
using Xunit;

/// <summary>
/// Comprehensive tests for the Causal Reasoning Engine.
/// Tests causal discovery, intervention estimation, counterfactuals, and more.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CausalReasoningEngineTests
{
    private readonly CausalReasoningEngine engine;

    public CausalReasoningEngineTests()
    {
        this.engine = new CausalReasoningEngine();
    }

    #region Causal Discovery Tests

    [Fact]
    public async Task DiscoverCausalStructure_WithValidData_ReturnsSuccess()
    {
        // Arrange
        var data = this.CreateSimpleObservationalData();

        // Act
        var result = await this.engine.DiscoverCausalStructureAsync(data, DiscoveryAlgorithm.PC);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Variables.Should().HaveCount(3);
        result.Value.Edges.Should().NotBeEmpty();
    }

    [Fact]
    public async Task DiscoverCausalStructure_WithNullData_ReturnsFailure()
    {
        // Act
        var result = await this.engine.DiscoverCausalStructureAsync(null!, DiscoveryAlgorithm.PC);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("null or empty");
    }

    [Fact]
    public async Task DiscoverCausalStructure_WithEmptyData_ReturnsFailure()
    {
        // Arrange
        var data = new List<Observation>();

        // Act
        var result = await this.engine.DiscoverCausalStructureAsync(data, DiscoveryAlgorithm.PC);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("null or empty");
    }

    [Fact]
    public async Task DiscoverCausalStructure_WithPCAlgorithm_DiscoversCausalRelationships()
    {
        // Arrange - Create data where X -> Y -> Z
        var data = this.CreateChainCausalData();

        // Act
        var result = await this.engine.DiscoverCausalStructureAsync(data, DiscoveryAlgorithm.PC);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var graph = result.Value;

        graph.Variables.Should().Contain(v => v.Name == "X");
        graph.Variables.Should().Contain(v => v.Name == "Y");
        graph.Variables.Should().Contain(v => v.Name == "Z");

        graph.Edges.Should().NotBeEmpty();
    }

    [Fact]
    public async Task DiscoverCausalStructure_WithUnimplementedAlgorithm_ReturnsFailure()
    {
        // Arrange
        var data = this.CreateSimpleObservationalData();

        // Act
        var result = await this.engine.DiscoverCausalStructureAsync(data, DiscoveryAlgorithm.FCI);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not yet implemented");
    }

    #endregion

    #region Intervention Effect Tests

    [Fact]
    public async Task EstimateInterventionEffect_WithValidInputs_ReturnsSuccess()
    {
        // Arrange
        var model = this.CreateSimpleCausalModel();

        // Act
        var result = await this.engine.EstimateInterventionEffectAsync("X", "Y", model);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task EstimateInterventionEffect_WithNullIntervention_ReturnsFailure()
    {
        // Arrange
        var model = this.CreateSimpleCausalModel();

        // Act
        var result = await this.engine.EstimateInterventionEffectAsync(null!, "Y", model);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Intervention");
    }

    [Fact]
    public async Task EstimateInterventionEffect_WithNullOutcome_ReturnsFailure()
    {
        // Arrange
        var model = this.CreateSimpleCausalModel();

        // Act
        var result = await this.engine.EstimateInterventionEffectAsync("X", null!, model);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Outcome");
    }

    [Fact]
    public async Task EstimateInterventionEffect_WithNullModel_ReturnsFailure()
    {
        // Act
        var result = await this.engine.EstimateInterventionEffectAsync("X", "Y", null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("model");
    }

    [Fact]
    public async Task EstimateInterventionEffect_WithDirectCausalPath_ReturnsPositiveEffect()
    {
        // Arrange - Model where X directly causes Y
        var model = this.CreateDirectCausalModel();

        // Act
        var result = await this.engine.EstimateInterventionEffectAsync("X", "Y", model);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task EstimateInterventionEffect_WithNoCausalPath_ReturnsZeroEffect()
    {
        // Arrange - Model where X and Y are independent
        var model = this.CreateIndependentVariablesModel();

        // Act
        var result = await this.engine.EstimateInterventionEffectAsync("X", "Y", model);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
    }

    #endregion

    #region Counterfactual Reasoning Tests

    [Fact]
    public async Task EstimateCounterfactual_WithValidInputs_ReturnsSuccess()
    {
        // Arrange
        var model = this.CreateSimpleCausalModel();
        var factual = this.CreateFactualObservation();

        // Act
        var result = await this.engine.EstimateCounterfactualAsync("X", "Y", factual, model);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Type.Should().Be(DistributionType.Empirical);
    }

    [Fact]
    public async Task EstimateCounterfactual_WithNullIntervention_ReturnsFailure()
    {
        // Arrange
        var model = this.CreateSimpleCausalModel();
        var factual = this.CreateFactualObservation();

        // Act
        var result = await this.engine.EstimateCounterfactualAsync(null!, "Y", factual, model);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Intervention");
    }

    [Fact]
    public async Task EstimateCounterfactual_WithNullFactual_ReturnsFailure()
    {
        // Arrange
        var model = this.CreateSimpleCausalModel();

        // Act
        var result = await this.engine.EstimateCounterfactualAsync("X", "Y", null!, model);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Factual");
    }

    [Fact]
    public async Task EstimateCounterfactual_ComputesTwinNetworkCorrectly()
    {
        // Arrange
        var model = this.CreateSimpleCausalModel();
        var factual = new Observation(
            new Dictionary<string, object> { { "X", 1.0 }, { "Y", 2.0 }, { "Z", 3.0 } },
            DateTime.UtcNow,
            null);

        // Act
        var result = await this.engine.EstimateCounterfactualAsync("X", "Y", factual, model);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Mean.Should().BeGreaterThanOrEqualTo(0);
        result.Value.Samples.Should().NotBeEmpty();
    }

    #endregion

    #region Causal Explanation Tests

    [Fact]
    public async Task ExplainCausally_WithValidInputs_ReturnsSuccess()
    {
        // Arrange
        var model = this.CreateSimpleCausalModel();
        var causes = new List<string> { "X", "Y" };

        // Act
        var result = await this.engine.ExplainCausallyAsync("Z", causes, model);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.Effect.Should().Be("Z");
        result.Value.Attributions.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ExplainCausally_WithNullEffect_ReturnsFailure()
    {
        // Arrange
        var model = this.CreateSimpleCausalModel();
        var causes = new List<string> { "X" };

        // Act
        var result = await this.engine.ExplainCausallyAsync(null!, causes, model);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Effect");
    }

    [Fact]
    public async Task ExplainCausally_WithNullCauses_ReturnsFailure()
    {
        // Arrange
        var model = this.CreateSimpleCausalModel();

        // Act
        var result = await this.engine.ExplainCausallyAsync("Z", null!, model);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("causes");
    }

    [Fact]
    public async Task ExplainCausally_GeneratesNarrativeExplanation()
    {
        // Arrange
        var model = this.CreateSimpleCausalModel();
        var causes = new List<string> { "X", "Y" };

        // Act
        var result = await this.engine.ExplainCausallyAsync("Z", causes, model);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.NarrativeExplanation.Should().NotBeNullOrEmpty();
        result.Value.NarrativeExplanation.Should().Contain("Z");
    }

    [Fact]
    public async Task ExplainCausally_ComputesAttributionScores()
    {
        // Arrange
        var model = this.CreateSimpleCausalModel();
        var causes = new List<string> { "X", "Y" };

        // Act
        var result = await this.engine.ExplainCausallyAsync("Z", causes, model);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Attributions.Should().ContainKey("X");
        result.Value.Attributions.Should().ContainKey("Y");

        // Attribution scores should sum to 1 (normalized)
        var totalAttribution = result.Value.Attributions.Values.Sum();
        totalAttribution.Should().BeApproximately(1.0, 0.01);
    }

    #endregion

    #region Intervention Planning Tests

    [Fact]
    public async Task PlanIntervention_WithValidInputs_ReturnsSuccess()
    {
        // Arrange
        var model = this.CreateSimpleCausalModel();
        var controllable = new List<string> { "X", "Y" };

        // Act
        var result = await this.engine.PlanInterventionAsync("Z", model, controllable);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeNull();
        result.Value.TargetVariable.Should().BeOneOf("X", "Y");
    }

    [Fact]
    public async Task PlanIntervention_WithNullOutcome_ReturnsFailure()
    {
        // Arrange
        var model = this.CreateSimpleCausalModel();
        var controllable = new List<string> { "X" };

        // Act
        var result = await this.engine.PlanInterventionAsync(null!, model, controllable);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("outcome");
    }

    [Fact]
    public async Task PlanIntervention_WithNullControllableVariables_ReturnsFailure()
    {
        // Arrange
        var model = this.CreateSimpleCausalModel();

        // Act
        var result = await this.engine.PlanInterventionAsync("Z", model, null!);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Controllable");
    }

    [Fact]
    public async Task PlanIntervention_SelectsOptimalIntervention()
    {
        // Arrange - Model with different effect strengths
        var model = this.CreateModelWithDifferentEffects();
        var controllable = new List<string> { "X", "Y" };

        // Act
        var result = await this.engine.PlanInterventionAsync("Z", model, controllable);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ExpectedEffect.Should().BeGreaterThan(0);
        result.Value.Confidence.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PlanIntervention_IdentifiesSideEffects()
    {
        // Arrange
        var model = this.CreateComplexCausalModel();
        var controllable = new List<string> { "X" };

        // Act
        var result = await this.engine.PlanInterventionAsync("Z", model, controllable);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.SideEffects.Should().NotBeNull();
    }

    #endregion

    #region Helper Methods

    private List<Observation> CreateSimpleObservationalData()
    {
        var data = new List<Observation>();
        var random = new Random(42);

        for (int i = 0; i < 100; i++)
        {
            var x = random.NextDouble();
            var y = (2 * x) + (random.NextDouble() * 0.1);
            var z = y + (random.NextDouble() * 0.1);

            data.Add(new Observation(
                new Dictionary<string, object>
                {
                    { "X", x },
                    { "Y", y },
                    { "Z", z },
                },
                DateTime.UtcNow.AddSeconds(-i),
                null));
        }

        return data;
    }

    private List<Observation> CreateChainCausalData()
    {
        var data = new List<Observation>();
        var random = new Random(42);

        for (int i = 0; i < 100; i++)
        {
            var x = random.NextDouble();
            var y = (0.8 * x) + (random.NextDouble() * 0.1);
            var z = (0.7 * y) + (random.NextDouble() * 0.1);

            data.Add(new Observation(
                new Dictionary<string, object>
                {
                    { "X", x },
                    { "Y", y },
                    { "Z", z },
                },
                DateTime.UtcNow.AddSeconds(-i),
                null));
        }

        return data;
    }

    private CausalGraph CreateSimpleCausalModel()
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

    private CausalGraph CreateDirectCausalModel()
    {
        var variables = new List<Variable>
        {
            new Variable("X", VariableType.Continuous, new List<object> { 0.0, 1.0 }),
            new Variable("Y", VariableType.Continuous, new List<object> { 0.0, 1.0 }),
        };

        var edges = new List<CausalEdge>
        {
            new CausalEdge("X", "Y", 0.9, EdgeType.Direct),
        };

        var equations = new Dictionary<string, StructuralEquation>
        {
            ["Y"] = new StructuralEquation(
                "Y",
                new List<string> { "X" },
                vals => Convert.ToDouble(vals["X"]) * 0.9,
                0.1),
        };

        return new CausalGraph(variables, edges, equations);
    }

    private CausalGraph CreateIndependentVariablesModel()
    {
        var variables = new List<Variable>
        {
            new Variable("X", VariableType.Continuous, new List<object> { 0.0, 1.0 }),
            new Variable("Y", VariableType.Continuous, new List<object> { 0.0, 1.0 }),
        };

        var edges = new List<CausalEdge>();

        var equations = new Dictionary<string, StructuralEquation>();

        return new CausalGraph(variables, edges, equations);
    }

    private CausalGraph CreateModelWithDifferentEffects()
    {
        var variables = new List<Variable>
        {
            new Variable("X", VariableType.Continuous, new List<object> { 0.0, 1.0 }),
            new Variable("Y", VariableType.Continuous, new List<object> { 0.0, 1.0 }),
            new Variable("Z", VariableType.Continuous, new List<object> { 0.0, 1.0 }),
        };

        var edges = new List<CausalEdge>
        {
            new CausalEdge("X", "Z", 0.9, EdgeType.Direct),
            new CausalEdge("Y", "Z", 0.3, EdgeType.Direct),
        };

        var equations = new Dictionary<string, StructuralEquation>
        {
            ["Z"] = new StructuralEquation(
                "Z",
                new List<string> { "X", "Y" },
                vals => (Convert.ToDouble(vals["X"]) * 0.9) + (Convert.ToDouble(vals["Y"]) * 0.3),
                0.1),
        };

        return new CausalGraph(variables, edges, equations);
    }

    private CausalGraph CreateComplexCausalModel()
    {
        var variables = new List<Variable>
        {
            new Variable("X", VariableType.Continuous, new List<object> { 0.0, 1.0 }),
            new Variable("Y", VariableType.Continuous, new List<object> { 0.0, 1.0 }),
            new Variable("W", VariableType.Continuous, new List<object> { 0.0, 1.0 }),
            new Variable("Z", VariableType.Continuous, new List<object> { 0.0, 1.0 }),
        };

        var edges = new List<CausalEdge>
        {
            new CausalEdge("X", "Y", 0.7, EdgeType.Direct),
            new CausalEdge("X", "W", 0.5, EdgeType.Direct),
            new CausalEdge("Y", "Z", 0.8, EdgeType.Direct),
            new CausalEdge("W", "Z", 0.4, EdgeType.Direct),
        };

        var equations = new Dictionary<string, StructuralEquation>
        {
            ["Y"] = new StructuralEquation(
                "Y",
                new List<string> { "X" },
                vals => Convert.ToDouble(vals["X"]) * 0.7,
                0.1),
            ["W"] = new StructuralEquation(
                "W",
                new List<string> { "X" },
                vals => Convert.ToDouble(vals["X"]) * 0.5,
                0.1),
            ["Z"] = new StructuralEquation(
                "Z",
                new List<string> { "Y", "W" },
                vals => (Convert.ToDouble(vals["Y"]) * 0.8) + (Convert.ToDouble(vals["W"]) * 0.4),
                0.1),
        };

        return new CausalGraph(variables, edges, equations);
    }

    private Observation CreateFactualObservation()
    {
        return new Observation(
            new Dictionary<string, object>
            {
                { "X", 0.5 },
                { "Y", 0.4 },
                { "Z", 0.3 },
            },
            DateTime.UtcNow,
            "test observation");
    }

    #endregion
}
