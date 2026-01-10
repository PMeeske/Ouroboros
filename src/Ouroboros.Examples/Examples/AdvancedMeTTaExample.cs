// <copyright file="AdvancedMeTTaExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Examples;

using Ouroboros.Tools.MeTTa;

/// <summary>
/// Examples demonstrating Advanced MeTTa Engine capabilities.
/// </summary>
public static class AdvancedMeTTaExample
{
    /// <summary>
    /// Demonstrates rule induction from observations.
    /// </summary>
    public static async Task DemonstrateRuleInduction()
    {
        Console.WriteLine("=== Advanced MeTTa: Rule Induction ===\n");

        // Create engine with mock base engine for demonstration
        var baseEngine = new MockMeTTaEngine();
        var engine = new AdvancedMeTTaEngine(baseEngine);

        // Provide observations about family relationships
        var observations = new List<Fact>
        {
            new Fact("parent", new List<string> { "alice", "bob" }, 1.0),
            new Fact("parent", new List<string> { "alice", "charlie" }, 1.0),
            new Fact("parent", new List<string> { "bob", "dave" }, 1.0),
            new Fact("parent", new List<string> { "charlie", "eve" }, 1.0),
            new Fact("parent", new List<string> { "dave", "frank" }, 1.0),
            new Fact("parent", new List<string> { "eve", "grace" }, 1.0),
        };

        Console.WriteLine("Observations:");
        foreach (var obs in observations)
        {
            Console.WriteLine($"  {obs.Predicate}({string.Join(", ", obs.Arguments)})");
        }

        Console.WriteLine("\nInducing rules using FOIL algorithm...");

        // Induce rules from observations
        var result = await engine.InduceRulesAsync(observations, InductionStrategy.FOIL);

        result.Match(
            rules =>
            {
                Console.WriteLine($"\n✓ Induced {rules.Count} rule(s):");
                foreach (var rule in rules)
                {
                    Console.WriteLine($"  Rule: {rule.Name}");
                    Console.WriteLine($"    Premises: {string.Join(", ", rule.Premises.Select(p => p.Template))}");
                    Console.WriteLine($"    Conclusion: {rule.Conclusion.Template}");
                    Console.WriteLine($"    Confidence: {rule.Confidence:F2}");
                }
            },
            error => Console.WriteLine($"✗ Error: {error}"));

        engine.Dispose();
        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates theorem proving using resolution.
    /// </summary>
    public static async Task DemonstrateTheoremProving()
    {
        Console.WriteLine("=== Advanced MeTTa: Theorem Proving ===\n");

        var baseEngine = new MockMeTTaEngine();
        var engine = new AdvancedMeTTaEngine(baseEngine);

        // Define theorem and axioms
        var theorem = "(mortal socrates)";
        var axioms = new List<string>
        {
            "(human socrates)",
            "(implies (human X) (mortal X))",
        };

        Console.WriteLine("Theorem to prove: " + theorem);
        Console.WriteLine("Axioms:");
        foreach (var axiom in axioms)
        {
            Console.WriteLine($"  {axiom}");
        }

        Console.WriteLine("\nProving using Resolution strategy...");

        var result = await engine.ProveTheoremAsync(theorem, axioms, ProofStrategy.Resolution);

        result.Match(
            trace =>
            {
                Console.WriteLine($"\n✓ Proof completed:");
                Console.WriteLine($"  Proved: {trace.Proved}");
                Console.WriteLine($"  Steps: {trace.Steps.Count}");
                foreach (var step in trace.Steps)
                {
                    Console.WriteLine($"    - {step.Inference}");
                }

                if (!trace.Proved && trace.CounterExample != null)
                {
                    Console.WriteLine($"  Counter-example: {trace.CounterExample}");
                }
            },
            error => Console.WriteLine($"✗ Error: {error}"));

        engine.Dispose();
        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates forward chaining inference.
    /// </summary>
    public static async Task DemonstrateForwardChaining()
    {
        Console.WriteLine("=== Advanced MeTTa: Forward Chaining ===\n");

        var baseEngine = new MockMeTTaEngine();
        var engine = new AdvancedMeTTaEngine(baseEngine);

        // Define rules
        var rules = new List<Rule>
        {
            new Rule(
                "mortality",
                new List<Pattern> { new Pattern("(human $x)", new List<string> { "$x" }) },
                new Pattern("(mortal $x)", new List<string> { "$x" }),
                1.0),
            new Rule(
                "philosopher",
                new List<Pattern> { new Pattern("(greek $x)", new List<string> { "$x" }) },
                new Pattern("(philosopher $x)", new List<string> { "$x" }),
                0.8),
        };

        // Define initial facts
        var facts = new List<Fact>
        {
            new Fact("human", new List<string> { "socrates" }, 1.0),
            new Fact("human", new List<string> { "plato" }, 1.0),
            new Fact("greek", new List<string> { "socrates" }, 1.0),
            new Fact("greek", new List<string> { "plato" }, 1.0),
        };

        Console.WriteLine("Initial facts:");
        foreach (var fact in facts)
        {
            Console.WriteLine($"  {fact.Predicate}({string.Join(", ", fact.Arguments)})");
        }

        Console.WriteLine("\nApplying forward chaining...");

        var result = await engine.ForwardChainAsync(rules, facts, maxSteps: 10);

        result.Match(
            derivedFacts =>
            {
                Console.WriteLine($"\n✓ Derived {derivedFacts.Count} total facts:");
                var newFacts = derivedFacts.Except(facts).ToList();
                Console.WriteLine($"  New facts: {newFacts.Count}");
                foreach (var fact in newFacts)
                {
                    Console.WriteLine($"    {fact.Predicate}({string.Join(", ", fact.Arguments)}) [confidence: {fact.Confidence:F2}]");
                }
            },
            error => Console.WriteLine($"✗ Error: {error}"));

        engine.Dispose();
        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates backward chaining to prove a goal.
    /// </summary>
    public static async Task DemonstrateBackwardChaining()
    {
        Console.WriteLine("=== Advanced MeTTa: Backward Chaining ===\n");

        var baseEngine = new MockMeTTaEngine();
        var engine = new AdvancedMeTTaEngine(baseEngine);

        // Define goal to prove
        var goal = new Fact("mortal", new List<string> { "socrates" }, 1.0);

        // Define rules
        var rules = new List<Rule>
        {
            new Rule(
                "mortality",
                new List<Pattern> { new Pattern("(human $x)", new List<string> { "$x" }) },
                new Pattern("(mortal $x)", new List<string> { "$x" }),
                1.0),
        };

        // Define known facts
        var knownFacts = new List<Fact>
        {
            new Fact("human", new List<string> { "socrates" }, 1.0),
        };

        Console.WriteLine($"Goal to prove: {goal.Predicate}({string.Join(", ", goal.Arguments)})");
        Console.WriteLine("\nKnown facts:");
        foreach (var fact in knownFacts)
        {
            Console.WriteLine($"  {fact.Predicate}({string.Join(", ", fact.Arguments)})");
        }

        Console.WriteLine("\nApplying backward chaining...");

        var result = await engine.BackwardChainAsync(goal, rules, knownFacts);

        result.Match(
            requiredFacts =>
            {
                Console.WriteLine($"\n✓ Goal can be proved!");
                Console.WriteLine($"  Required facts: {requiredFacts.Count}");
                foreach (var fact in requiredFacts)
                {
                    Console.WriteLine($"    {fact.Predicate}({string.Join(", ", fact.Arguments)})");
                }
            },
            error => Console.WriteLine($"✗ Error: {error}"));

        engine.Dispose();
        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates abductive hypothesis generation.
    /// </summary>
    public static async Task DemonstrateHypothesisGeneration()
    {
        Console.WriteLine("=== Advanced MeTTa: Hypothesis Generation ===\n");

        var baseEngine = new MockMeTTaEngine();
        var engine = new AdvancedMeTTaEngine(baseEngine);

        // Define observation
        var observation = "(fly eagle)";
        var backgroundKnowledge = new List<string>
        {
            "(has-wings eagle)",
            "(bird eagle)",
            "(predator eagle)",
        };

        Console.WriteLine($"Observation: {observation}");
        Console.WriteLine("\nBackground knowledge:");
        foreach (var knowledge in backgroundKnowledge)
        {
            Console.WriteLine($"  {knowledge}");
        }

        Console.WriteLine("\nGenerating hypotheses...");

        var result = await engine.GenerateHypothesesAsync(observation, backgroundKnowledge);

        result.Match(
            hypotheses =>
            {
                Console.WriteLine($"\n✓ Generated {hypotheses.Count} hypothesis/hypotheses:");
                foreach (var hypothesis in hypotheses)
                {
                    Console.WriteLine($"\n  Statement: {hypothesis.Statement}");
                    Console.WriteLine($"  Plausibility: {hypothesis.Plausibility:F2}");
                    Console.WriteLine($"  Supporting evidence: {hypothesis.SupportingEvidence.Count} fact(s)");
                }
            },
            error => Console.WriteLine($"✗ Error: {error}"));

        engine.Dispose();
        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates type inference.
    /// </summary>
    public static async Task DemonstrateTypeInference()
    {
        Console.WriteLine("=== Advanced MeTTa: Type Inference ===\n");

        var baseEngine = new MockMeTTaEngine();
        var engine = new AdvancedMeTTaEngine(baseEngine);

        var context = new TypeContext(
            new Dictionary<string, string> { { "x", "Int" } },
            new List<string> { "x : Int" });

        var testAtoms = new[] { "42", "3.14", "\"hello\"", "$x", "(+ 1 2)" };

        Console.WriteLine("Inferring types for atoms:");

        foreach (var atom in testAtoms)
        {
            var result = await engine.InferTypeAsync(atom, context);
            result.Match(
                typedAtom => Console.WriteLine($"  {atom} : {typedAtom.Type}"),
                error => Console.WriteLine($"  {atom} : Error - {error}"));
        }

        engine.Dispose();
        Console.WriteLine();
    }

    /// <summary>
    /// Runs all advanced MeTTa examples.
    /// </summary>
    public static async Task RunAllExamples()
    {
        Console.WriteLine("╔════════════════════════════════════════════════════╗");
        Console.WriteLine("║   Advanced MeTTa Engine - Example Demonstrations  ║");
        Console.WriteLine("╚════════════════════════════════════════════════════╝\n");

        await DemonstrateRuleInduction();
        await DemonstrateTheoremProving();
        await DemonstrateForwardChaining();
        await DemonstrateBackwardChaining();
        await DemonstrateHypothesisGeneration();
        await DemonstrateTypeInference();

        Console.WriteLine("╔════════════════════════════════════════════════════╗");
        Console.WriteLine("║        All examples completed successfully!        ║");
        Console.WriteLine("╚════════════════════════════════════════════════════╝");
    }
}
