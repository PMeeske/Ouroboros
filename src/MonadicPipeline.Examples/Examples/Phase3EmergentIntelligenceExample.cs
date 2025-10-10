// ==========================================================
// Phase 3 Emergent Intelligence Example
// Demonstrates transfer learning, hypothesis testing, and curiosity
// ==========================================================

using LangChain.Providers.Ollama;
using LangChainPipeline.Agent.MetaAI;

namespace LangChainPipeline.Examples;

/// <summary>
/// Example demonstrating Phase 3 emergent intelligence capabilities.
/// Shows transfer learning, hypothesis generation/testing, and curiosity-driven exploration.
/// </summary>
public static class Phase3EmergentIntelligenceExample
{
    /// <summary>
    /// Demonstrates complete Phase 3 workflow.
    /// </summary>
    public static async Task RunCompleteWorkflow()
    {
        Console.WriteLine("=== Phase 3 Emergent Intelligence Example ===\n");
        Console.WriteLine("This example demonstrates:");
        Console.WriteLine("1. Transfer Learning - applying skills across domains");
        Console.WriteLine("2. Hypothesis Generation & Testing - scientific reasoning");
        Console.WriteLine("3. Curiosity-Driven Exploration - autonomous learning\n");

        // Setup
        var provider = new OllamaProvider();
        var llm = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        var tools = ToolRegistry.CreateDefault();
        var memory = new PersistentMemoryStore();
        var skills = new SkillRegistry();
        var safety = new SafetyGuard();
        var router = new UncertaintyRouter(null!, 0.7);

        var orchestrator = new MetaAIPlannerOrchestrator(
            llm,
            tools,
            memory,
            skills,
            router,
            safety);

        // Initialize Phase 3 components
        var transferLearner = new TransferLearner(llm, skills, memory);
        var hypothesisEngine = new HypothesisEngine(llm, orchestrator, memory);
        var curiosityEngine = new CuriosityEngine(llm, memory, skills, safety);

        Console.WriteLine("✓ Phase 3 components initialized\n");

        // === Part 1: Transfer Learning ===
        Console.WriteLine("=== Part 1: Transfer Learning ===\n");

        // Register a skill learned in one domain
        var codingSkill = new Skill(
            "debug_code",
            "Systematically debug code by identifying and fixing errors",
            new List<string> { "code_analysis", "error_detection" },
            new List<PlanStep>
            {
                new PlanStep("analyze_error_message", new Dictionary<string, object>(), "Error understood", 0.9),
                new PlanStep("locate_source", new Dictionary<string, object>(), "Source identified", 0.8),
                new PlanStep("propose_fix", new Dictionary<string, object>(), "Fix proposed", 0.7),
                new PlanStep("validate_solution", new Dictionary<string, object>(), "Solution validated", 0.85)
            },
            SuccessRate: 0.87,
            UsageCount: 42,
            DateTime.UtcNow.AddDays(-20),
            DateTime.UtcNow);

        skills.RegisterSkill(codingSkill);
        Console.WriteLine($"Registered skill: {codingSkill.Name}");
        Console.WriteLine($"Domain: Software debugging\n");

        // Estimate transferability to a different domain
        var targetDomain = "troubleshooting mechanical systems";
        var transferability = await transferLearner.EstimateTransferabilityAsync(codingSkill, targetDomain);

        Console.WriteLine($"Transferability Analysis:");
        Console.WriteLine($"  Source Domain: Software debugging");
        Console.WriteLine($"  Target Domain: {targetDomain}");
        Console.WriteLine($"  Transferability Score: {transferability:P0}\n");

        // Find analogies between domains
        var analogies = await transferLearner.FindAnalogiesAsync(
            "software debugging",
            targetDomain);

        if (analogies.Any())
        {
            Console.WriteLine("Analogical Mappings:");
            foreach (var (source, target, confidence) in analogies.Take(4))
            {
                Console.WriteLine($"  • {source} → {target} (confidence: {confidence:F2})");
            }
            Console.WriteLine();
        }

        // Perform transfer
        var transferResult = await transferLearner.AdaptSkillToDomainAsync(
            codingSkill,
            targetDomain);

        if (transferResult.IsSuccess)
        {
            var result = transferResult.Value;
            Console.WriteLine("✓ Transfer Successful!\n");
            Console.WriteLine($"Adapted Skill: {result.AdaptedSkill.Name}");
            Console.WriteLine($"Description: {result.AdaptedSkill.Description}");
            Console.WriteLine($"Transferability: {result.TransferabilityScore:P0}");
            Console.WriteLine($"Adjusted Success Rate: {result.AdaptedSkill.SuccessRate:P0}\n");

            Console.WriteLine("Adaptations Made:");
            foreach (var adaptation in result.Adaptations.Take(5))
            {
                Console.WriteLine($"  • {adaptation}");
            }
            Console.WriteLine();
        }

        // === Part 2: Hypothesis Generation & Testing ===
        Console.WriteLine("\n=== Part 2: Hypothesis Generation & Testing ===\n");

        // Observe a pattern
        var observation = "Tasks involving systematic step-by-step procedures have consistently higher success rates across all domains";

        Console.WriteLine($"Observation: {observation}\n");

        // Generate hypothesis
        var hypothesisResult = await hypothesisEngine.GenerateHypothesisAsync(observation);

        if (hypothesisResult.IsSuccess)
        {
            var hypothesis = hypothesisResult.Value;

            Console.WriteLine("Generated Hypothesis:");
            Console.WriteLine($"  Statement: {hypothesis.Statement}");
            Console.WriteLine($"  Domain: {hypothesis.Domain}");
            Console.WriteLine($"  Initial Confidence: {hypothesis.Confidence:P0}\n");

            if (hypothesis.SupportingEvidence.Any())
            {
                Console.WriteLine("Supporting Evidence:");
                foreach (var evidence in hypothesis.SupportingEvidence.Take(3))
                {
                    Console.WriteLine($"  • {evidence}");
                }
                Console.WriteLine();
            }

            // Design experiment to test the hypothesis
            var experimentResult = await hypothesisEngine.DesignExperimentAsync(hypothesis);

            if (experimentResult.IsSuccess)
            {
                var experiment = experimentResult.Value;

                Console.WriteLine("Designed Experiment:");
                Console.WriteLine($"  Description: {experiment.Description}");
                Console.WriteLine($"  Steps: {experiment.Steps.Count}\n");

                Console.WriteLine("Experimental Steps:");
                for (int i = 0; i < experiment.Steps.Count; i++)
                {
                    Console.WriteLine($"  {i + 1}. {experiment.Steps[i].Action}");
                }
                Console.WriteLine();

                if (experiment.ExpectedOutcomes.Any())
                {
                    Console.WriteLine("Expected Outcomes:");
                    foreach (var outcome in experiment.ExpectedOutcomes)
                    {
                        Console.WriteLine($"  • {outcome.Key}: {outcome.Value}");
                    }
                    Console.WriteLine();
                }

                Console.WriteLine("Note: In a real scenario, the experiment would be executed");
                Console.WriteLine("and results would validate or refute the hypothesis.\n");
            }

            // Simulate gathering additional evidence
            hypothesisEngine.UpdateHypothesis(
                hypothesis.Id,
                "Another procedural task completed with 95% accuracy",
                supports: true);

            hypothesisEngine.UpdateHypothesis(
                hypothesis.Id,
                "Ad-hoc approach worked well in creative task",
                supports: false);

            Console.WriteLine("Updated hypothesis with new evidence");

            // Check confidence trend
            var trend = hypothesisEngine.GetConfidenceTrend(hypothesis.Id);
            Console.WriteLine($"\nConfidence Trend ({trend.Count} data points):");
            foreach (var (time, conf) in trend)
            {
                Console.WriteLine($"  {time:HH:mm:ss} - {conf:P0}");
            }
            Console.WriteLine();
        }

        // Use abductive reasoning
        var observations = new List<string>
        {
            "Transfer learning works better with abstract skills",
            "High-level strategies transfer more easily than low-level tactics",
            "Domain-independent patterns show consistent results"
        };

        Console.WriteLine("Abductive Reasoning from Multiple Observations:");
        foreach (var obs in observations)
        {
            Console.WriteLine($"  • {obs}");
        }
        Console.WriteLine();

        var abductiveResult = await hypothesisEngine.AbductiveReasoningAsync(observations);

        if (abductiveResult.IsSuccess)
        {
            var bestExplanation = abductiveResult.Value;
            Console.WriteLine("Best Explanation (Abductive Reasoning):");
            Console.WriteLine($"  {bestExplanation.Statement}");
            Console.WriteLine($"  Confidence: {bestExplanation.Confidence:P0}\n");
        }

        // === Part 3: Curiosity-Driven Exploration ===
        Console.WriteLine("\n=== Part 3: Curiosity-Driven Exploration ===\n");

        // Check if agent should explore
        var shouldExplore = await curiosityEngine.ShouldExploreAsync();
        Console.WriteLine($"Exploration Decision: {(shouldExplore ? "EXPLORE" : "EXPLOIT")}");

        if (shouldExplore)
        {
            Console.WriteLine("Agent has decided to explore based on intrinsic motivation\n");

            // Identify exploration opportunities
            var opportunities = await curiosityEngine.IdentifyExplorationOpportunitiesAsync(5);

            Console.WriteLine($"Exploration Opportunities Identified: {opportunities.Count}\n");

            foreach (var opp in opportunities.Take(3))
            {
                Console.WriteLine($"Opportunity: {opp.Description}");
                Console.WriteLine($"  Novelty Score: {opp.NoveltyScore:P0}");
                Console.WriteLine($"  Information Gain: {opp.InformationGainEstimate:P0}");
                Console.WriteLine($"  Identified: {opp.IdentifiedAt:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine();
            }

            // Generate exploratory plan
            var exploratoryPlanResult = await curiosityEngine.GenerateExploratoryPlanAsync();

            if (exploratoryPlanResult.IsSuccess)
            {
                var expPlan = exploratoryPlanResult.Value;

                Console.WriteLine("Generated Exploratory Plan:");
                Console.WriteLine($"  Goal: {expPlan.Goal}");
                Console.WriteLine($"  Type: Curiosity-driven exploration");

                if (expPlan.ConfidenceScores.TryGetValue("novelty", out var noveltyScore))
                {
                    Console.WriteLine($"  Novelty: {noveltyScore:P0}");
                }

                Console.WriteLine($"\n  Steps ({expPlan.Steps.Count}):");
                for (int i = 0; i < expPlan.Steps.Count; i++)
                {
                    var step = expPlan.Steps[i];
                    Console.WriteLine($"  {i + 1}. {step.Action}");

                    if (step.Parameters.TryGetValue("expected_learning", out var learning))
                    {
                        Console.WriteLine($"     Expected Learning: {learning}");
                    }
                }
                Console.WriteLine();

                // Compute novelty of the plan
                var planNovelty = await curiosityEngine.ComputeNoveltyAsync(expPlan);
                Console.WriteLine($"Plan Novelty Score: {planNovelty:P0}");
                Console.WriteLine("(Higher novelty indicates more unique exploration)\n");
            }
        }
        else
        {
            Console.WriteLine("Agent has decided to exploit existing knowledge\n");
        }

        // Estimate information gain for specific areas
        var areas = new[] { "neural networks", "optimization algorithms", "data visualization" };

        Console.WriteLine("Information Gain Estimates:");
        foreach (var area in areas)
        {
            var infoGain = await curiosityEngine.EstimateInformationGainAsync(area);
            Console.WriteLine($"  • {area}: {infoGain:P0}");
        }
        Console.WriteLine();

        // Show exploration statistics
        var stats = curiosityEngine.GetExplorationStats();
        Console.WriteLine("Exploration Statistics:");
        foreach (var (metric, value) in stats)
        {
            Console.WriteLine($"  • {metric.Replace("_", " ")}: {value:F2}");
        }

        // === Summary ===
        Console.WriteLine("\n\n=== Phase 3 Demonstration Complete ===\n");

        Console.WriteLine("Capabilities Demonstrated:");
        Console.WriteLine("  ✓ Transfer Learning");
        Console.WriteLine("    - Cross-domain skill adaptation");
        Console.WriteLine("    - Analogical reasoning");
        Console.WriteLine("    - Transferability assessment");
        Console.WriteLine();
        Console.WriteLine("  ✓ Hypothesis Generation & Testing");
        Console.WriteLine("    - Hypothesis formation from observations");
        Console.WriteLine("    - Experiment design");
        Console.WriteLine("    - Abductive reasoning");
        Console.WriteLine("    - Evidence-based confidence adjustment");
        Console.WriteLine();
        Console.WriteLine("  ✓ Curiosity-Driven Exploration");
        Console.WriteLine("    - Novelty detection");
        Console.WriteLine("    - Exploration vs exploitation balancing");
        Console.WriteLine("    - Autonomous learning opportunities");
        Console.WriteLine("    - Information gain estimation");
        Console.WriteLine();

        Console.WriteLine("These capabilities enable:");
        Console.WriteLine("  • Applying knowledge across different domains");
        Console.WriteLine("  • Scientific reasoning about patterns and behaviors");
        Console.WriteLine("  • Autonomous exploration during idle time");
        Console.WriteLine("  • Compositional generalization");
        Console.WriteLine();

        Console.WriteLine("The agent is now capable of emergent intelligent behavior!");
    }
}
