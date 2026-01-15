// <copyright file="CausalReasoningBenchmarks.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Benchmarks;

using BenchmarkDotNet.Attributes;
using Ouroboros.Core.Reasoning;

/// <summary>
/// Benchmarks for causal reasoning engine performance and accuracy.
/// Evaluates causal discovery, intervention estimation, and counterfactual reasoning.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, iterationCount: 10)]
public class CausalReasoningBenchmarks
{
    private CausalReasoningEngine? engine;
    private List<Observation>? simpleData;
    private List<Observation>? mediumData;
    private List<Observation>? largeData;
    private CausalGraph? simpleModel;
    private CausalGraph? complexModel;
    private Observation? factualObservation;

    /// <summary>
    /// Setup benchmark data and models.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        this.engine = new CausalReasoningEngine();

        // Generate synthetic datasets of different sizes
        this.simpleData = this.GenerateCausalData(100, 3);  // 100 observations, 3 variables
        this.mediumData = this.GenerateCausalData(500, 5);  // 500 observations, 5 variables
        this.largeData = this.GenerateCausalData(1000, 8);  // 1000 observations, 8 variables

        // Create causal models
        this.simpleModel = this.CreateSimpleCausalModel();
        this.complexModel = this.CreateComplexCausalModel();

        // Create factual observation for counterfactual tests
        this.factualObservation = new Observation(
            new Dictionary<string, object>
            {
                { "X", 0.5 },
                { "Y", 0.4 },
                { "Z", 0.3 },
            },
            DateTime.UtcNow,
            "benchmark");
    }

    /// <summary>
    /// Benchmark: Causal discovery with PC algorithm on small dataset (100 obs, 3 vars).
    /// Target: &lt;50ms for 70%+ accuracy.
    /// </summary>
    [Benchmark(Description = "Causal Discovery - Small Dataset")]
    public async Task<CausalGraph> CausalDiscoverySmall()
    {
        var result = await this.engine!.DiscoverCausalStructureAsync(
            this.simpleData!,
            DiscoveryAlgorithm.PC);

        return result.Value;
    }

    /// <summary>
    /// Benchmark: Causal discovery on medium dataset (500 obs, 5 vars).
    /// Target: &lt;200ms.
    /// </summary>
    [Benchmark(Description = "Causal Discovery - Medium Dataset")]
    public async Task<CausalGraph> CausalDiscoveryMedium()
    {
        var result = await this.engine!.DiscoverCausalStructureAsync(
            this.mediumData!,
            DiscoveryAlgorithm.PC);

        return result.Value;
    }

    /// <summary>
    /// Benchmark: Causal discovery on large dataset (1000 obs, 8 vars).
    /// Target: &lt;1000ms.
    /// </summary>
    [Benchmark(Description = "Causal Discovery - Large Dataset")]
    public async Task<CausalGraph> CausalDiscoveryLarge()
    {
        var result = await this.engine!.DiscoverCausalStructureAsync(
            this.largeData!,
            DiscoveryAlgorithm.PC);

        return result.Value;
    }

    /// <summary>
    /// Benchmark: Intervention effect estimation on simple model.
    /// Target: &lt;10ms.
    /// </summary>
    [Benchmark(Description = "Intervention Effect - Simple Model")]
    public async Task<double> InterventionEffectSimple()
    {
        var result = await this.engine!.EstimateInterventionEffectAsync(
            "X",
            "Y",
            this.simpleModel!);

        return result.Value;
    }

    /// <summary>
    /// Benchmark: Intervention effect estimation on complex model.
    /// Target: &lt;50ms.
    /// </summary>
    [Benchmark(Description = "Intervention Effect - Complex Model")]
    public async Task<double> InterventionEffectComplex()
    {
        var result = await this.engine!.EstimateInterventionEffectAsync(
            "X",
            "Z",
            this.complexModel!);

        return result.Value;
    }

    /// <summary>
    /// Benchmark: Counterfactual reasoning.
    /// Target: &lt;20ms.
    /// </summary>
    [Benchmark(Description = "Counterfactual Reasoning")]
    public async Task<Distribution> CounterfactualEstimation()
    {
        var result = await this.engine!.EstimateCounterfactualAsync(
            "X",
            "Y",
            this.factualObservation!,
            this.simpleModel!);

        return result.Value;
    }

    /// <summary>
    /// Benchmark: Causal explanation generation.
    /// Target: &lt;30ms.
    /// </summary>
    [Benchmark(Description = "Causal Explanation")]
    public async Task<Explanation> CausalExplanation()
    {
        var result = await this.engine!.ExplainCausallyAsync(
            "Z",
            new List<string> { "X", "Y" },
            this.simpleModel!);

        return result.Value;
    }

    /// <summary>
    /// Benchmark: Intervention planning.
    /// Target: &lt;50ms.
    /// </summary>
    [Benchmark(Description = "Intervention Planning")]
    public async Task<Intervention> InterventionPlanning()
    {
        var result = await this.engine!.PlanInterventionAsync(
            "Z",
            this.complexModel!,
            new List<string> { "X", "Y" });

        return result.Value;
    }

    /// <summary>
    /// Benchmark: Full causal reasoning pipeline (discovery + intervention + explanation).
    /// Target: &lt;100ms for small dataset.
    /// </summary>
    [Benchmark(Description = "Full Pipeline")]
    public async Task<string> FullCausalPipeline()
    {
        // Discovery
        var discoveryResult = await this.engine!.DiscoverCausalStructureAsync(
            this.simpleData!,
            DiscoveryAlgorithm.PC);

        var graph = discoveryResult.Value;

        // Intervention
        var interventionResult = await this.engine!.EstimateInterventionEffectAsync(
            graph.Variables[0].Name,
            graph.Variables[1].Name,
            graph);

        // Explanation
        var explanationResult = await this.engine!.ExplainCausallyAsync(
            graph.Variables[2].Name,
            new List<string> { graph.Variables[0].Name, graph.Variables[1].Name },
            graph);

        return $"Effect: {interventionResult.Value}, Attribution: {explanationResult.Value.Attributions.Count}";
    }

    private List<Observation> GenerateCausalData(int count, int numVariables)
    {
        var data = new List<Observation>();
        var random = new Random(42);
        var varNames = Enumerable.Range(0, numVariables).Select(i => $"V{i}").ToList();

        for (int i = 0; i < count; i++)
        {
            var values = new Dictionary<string, object>();

            // Generate causal chain: V0 -> V1 -> V2 -> ... -> Vn
            double prevValue = random.NextDouble();
            values[varNames[0]] = prevValue;

            for (int v = 1; v < numVariables; v++)
            {
                var noise = random.NextDouble() * 0.1;
                var value = (0.7 * prevValue) + noise;
                values[varNames[v]] = value;
                prevValue = value;
            }

            data.Add(new Observation(values, DateTime.UtcNow.AddSeconds(-i), null));
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

    private CausalGraph CreateComplexCausalModel()
    {
        var variables = new List<Variable>
        {
            new Variable("X", VariableType.Continuous, new List<object> { 0.0, 1.0 }),
            new Variable("Y", VariableType.Continuous, new List<object> { 0.0, 1.0 }),
            new Variable("W", VariableType.Continuous, new List<object> { 0.0, 1.0 }),
            new Variable("Z", VariableType.Continuous, new List<object> { 0.0, 1.0 }),
            new Variable("V", VariableType.Continuous, new List<object> { 0.0, 1.0 }),
        };

        var edges = new List<CausalEdge>
        {
            new CausalEdge("X", "Y", 0.7, EdgeType.Direct),
            new CausalEdge("X", "W", 0.5, EdgeType.Direct),
            new CausalEdge("Y", "Z", 0.8, EdgeType.Direct),
            new CausalEdge("W", "Z", 0.4, EdgeType.Direct),
            new CausalEdge("Z", "V", 0.6, EdgeType.Direct),
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
            ["V"] = new StructuralEquation(
                "V",
                new List<string> { "Z" },
                vals => Convert.ToDouble(vals["Z"]) * 0.6,
                0.1),
        };

        return new CausalGraph(variables, edges, equations);
    }
}
