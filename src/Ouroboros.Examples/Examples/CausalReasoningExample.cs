// <copyright file="CausalReasoningExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Examples;

using Ouroboros.Core.Reasoning;

/// <summary>
/// Demonstrates the Causal Reasoning Engine with Pearl's causal inference framework.
/// Shows causal discovery, do-calculus, counterfactuals, explanations, and intervention planning.
/// </summary>
public static class CausalReasoningExample
{
    /// <summary>
    /// Runs the complete causal reasoning example demonstrating all features.
    /// </summary>
    /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
    public static async Task RunCompleteExample()
    {
        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║   Causal Reasoning Engine - Complete Example        ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝\n");

        await RunCausalDiscoveryExample();
        Console.WriteLine();

        await RunInterventionEffectExample();
        Console.WriteLine();

        await RunCounterfactualExample();
        Console.WriteLine();

        await RunCausalExplanationExample();
        Console.WriteLine();

        await RunInterventionPlanningExample();
        Console.WriteLine();

        Console.WriteLine("✓ Causal reasoning examples completed!\n");
    }

    /// <summary>
    /// Demonstrates causal structure discovery from observational data.
    /// </summary>
    public static async Task RunCausalDiscoveryExample()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("Example 1: Causal Structure Discovery");
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("Scenario: Learning causal relationships from health data");
        Console.WriteLine();

        var engine = new CausalReasoningEngine();

        // Generate synthetic health data: Exercise -> Weight -> Health
        var data = GenerateHealthData();

        Console.WriteLine($"Generated {data.Count} observations of Exercise, Weight, and Health");
        Console.WriteLine("\nDiscovering causal structure using PC algorithm...");

        var result = await engine.DiscoverCausalStructureAsync(data, DiscoveryAlgorithm.PC);

        result.Match(
            graph =>
            {
                Console.WriteLine("✓ Causal graph discovered successfully!");
                Console.WriteLine($"  Variables: {graph.Variables.Count}");
                Console.WriteLine($"  Edges: {graph.Edges.Count}");
                Console.WriteLine("\nCausal relationships found:");
                foreach (var edge in graph.Edges)
                {
                    Console.WriteLine($"  {edge.Cause} → {edge.Effect} (strength: {edge.Strength:F2})");
                }
            },
            error => Console.WriteLine($"✗ Discovery failed: {error}"));
    }

    /// <summary>
    /// Demonstrates intervention effect estimation using do-calculus.
    /// </summary>
    public static async Task RunInterventionEffectExample()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("Example 2: Intervention Effect Estimation");
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("Scenario: Estimating effect of exercise intervention on health");
        Console.WriteLine();

        var engine = new CausalReasoningEngine();
        var model = CreateHealthCausalModel();

        Console.WriteLine("Estimating P(Health | do(Exercise = high))...");

        var result = await engine.EstimateInterventionEffectAsync("Exercise", "Health", model);

        result.Match(
            effect =>
            {
                Console.WriteLine("✓ Intervention effect estimated successfully!");
                Console.WriteLine($"  Expected causal effect: {effect:F3}");
                Console.WriteLine($"  Interpretation: Increasing exercise by 1 unit");
                Console.WriteLine($"  improves health by {effect:F3} units");
            },
            error => Console.WriteLine($"✗ Estimation failed: {error}"));
    }

    /// <summary>
    /// Demonstrates counterfactual reasoning.
    /// </summary>
    public static async Task RunCounterfactualExample()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("Example 3: Counterfactual Reasoning");
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("Scenario: 'What if' analysis - What if patient exercised more?");
        Console.WriteLine();

        var engine = new CausalReasoningEngine();
        var model = CreateHealthCausalModel();

        // Factual observation: low exercise, moderate health
        var factual = new Observation(
            new Dictionary<string, object>
            {
                { "Exercise", 0.3 },
                { "Weight", 0.7 },
                { "Health", 0.5 },
            },
            DateTime.UtcNow,
            "Patient observation");

        Console.WriteLine("Factual observation:");
        Console.WriteLine($"  Exercise: {factual.Values["Exercise"]}");
        Console.WriteLine($"  Weight: {factual.Values["Weight"]}");
        Console.WriteLine($"  Health: {factual.Values["Health"]}");
        Console.WriteLine("\nCounterfactual: What if Exercise was 0.8?");

        var result = await engine.EstimateCounterfactualAsync("Exercise", "Health", factual, model);

        result.Match(
            distribution =>
            {
                Console.WriteLine("✓ Counterfactual estimated successfully!");
                Console.WriteLine($"  Predicted counterfactual health: {distribution.Mean:F3}");
                Console.WriteLine($"  Change from factual: {distribution.Mean - 0.5:+F3}");
                Console.WriteLine($"  Distribution type: {distribution.Type}");
            },
            error => Console.WriteLine($"✗ Estimation failed: {error}"));
    }

    /// <summary>
    /// Demonstrates causal explanation generation.
    /// </summary>
    public static async Task RunCausalExplanationExample()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("Example 4: Causal Explanation");
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("Scenario: Explaining what causes poor health");
        Console.WriteLine();

        var engine = new CausalReasoningEngine();
        var model = CreateHealthCausalModel();

        var possibleCauses = new List<string> { "Exercise", "Weight" };

        Console.WriteLine($"Analyzing causes of Health from: {string.Join(", ", possibleCauses)}");

        var result = await engine.ExplainCausallyAsync("Health", possibleCauses, model);

        result.Match(
            explanation =>
            {
                Console.WriteLine("✓ Causal explanation generated successfully!");
                Console.WriteLine("\nCausal attribution:");
                foreach (var attribution in explanation.Attributions.OrderByDescending(a => a.Value))
                {
                    Console.WriteLine($"  {attribution.Key}: {attribution.Value:P1}");
                }

                Console.WriteLine($"\nCausal paths found: {explanation.CausalPaths.Count}");
                foreach (var path in explanation.CausalPaths)
                {
                    var pathStr = string.Join(" → ", path.Variables);
                    Console.WriteLine($"  {pathStr} (effect: {path.TotalEffect:F3}, direct: {path.IsDirect})");
                }

                Console.WriteLine("\nNarrative explanation:");
                Console.WriteLine($"  {explanation.NarrativeExplanation}");
            },
            error => Console.WriteLine($"✗ Explanation failed: {error}"));
    }

    /// <summary>
    /// Demonstrates intervention planning.
    /// </summary>
    public static async Task RunInterventionPlanningExample()
    {
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("Example 5: Intervention Planning");
        Console.WriteLine("═══════════════════════════════════════════════════════");
        Console.WriteLine("Scenario: Planning best intervention to improve health");
        Console.WriteLine();

        var engine = new CausalReasoningEngine();
        var model = CreateHealthCausalModel();

        var controllableVariables = new List<string> { "Exercise", "Weight" };

        Console.WriteLine($"Finding optimal intervention from: {string.Join(", ", controllableVariables)}");
        Console.WriteLine("Target: Improve Health");

        var result = await engine.PlanInterventionAsync("Health", model, controllableVariables);

        result.Match(
            intervention =>
            {
                Console.WriteLine("✓ Optimal intervention found!");
                Console.WriteLine($"  Target variable: {intervention.TargetVariable}");
                Console.WriteLine($"  Recommended value: {intervention.NewValue}");
                Console.WriteLine($"  Expected effect: {intervention.ExpectedEffect:F3}");
                Console.WriteLine($"  Confidence: {intervention.Confidence:P0}");

                if (intervention.SideEffects.Any())
                {
                    Console.WriteLine($"  Potential side effects on: {string.Join(", ", intervention.SideEffects)}");
                }
                else
                {
                    Console.WriteLine("  No significant side effects detected");
                }
            },
            error => Console.WriteLine($"✗ Planning failed: {error}"));
    }

    private static List<Observation> GenerateHealthData()
    {
        var data = new List<Observation>();
        var random = new Random(42);

        for (int i = 0; i < 200; i++)
        {
            // Causal model: Exercise -> Weight -> Health
            var exercise = random.NextDouble();
            var weight = 1.0 - (0.7 * exercise) + (random.NextDouble() * 0.2); // Higher exercise -> lower weight
            var health = (0.5 * exercise) + (0.4 * (1.0 - weight)) + (random.NextDouble() * 0.1); // Better health from exercise and lower weight

            data.Add(new Observation(
                new Dictionary<string, object>
                {
                    { "Exercise", exercise },
                    { "Weight", weight },
                    { "Health", health },
                },
                DateTime.UtcNow.AddDays(-i),
                $"observation_{i}"));
        }

        return data;
    }

    private static CausalGraph CreateHealthCausalModel()
    {
        var variables = new List<Variable>
        {
            new Variable("Exercise", VariableType.Continuous, new List<object> { 0.0, 0.5, 1.0 }),
            new Variable("Weight", VariableType.Continuous, new List<object> { 0.0, 0.5, 1.0 }),
            new Variable("Health", VariableType.Continuous, new List<object> { 0.0, 0.5, 1.0 }),
        };

        var edges = new List<CausalEdge>
        {
            new CausalEdge("Exercise", "Weight", 0.7, EdgeType.Direct),
            new CausalEdge("Exercise", "Health", 0.5, EdgeType.Direct),
            new CausalEdge("Weight", "Health", 0.4, EdgeType.Direct),
        };

        var equations = new Dictionary<string, StructuralEquation>
        {
            ["Weight"] = new StructuralEquation(
                "Weight",
                new List<string> { "Exercise" },
                vals => 1.0 - (0.7 * Convert.ToDouble(vals["Exercise"])),
                0.1),
            ["Health"] = new StructuralEquation(
                "Health",
                new List<string> { "Exercise", "Weight" },
                vals => (0.5 * Convert.ToDouble(vals["Exercise"])) +
                        (0.4 * (1.0 - Convert.ToDouble(vals["Weight"]))),
                0.1),
        };

        return new CausalGraph(variables, edges, equations);
    }
}
