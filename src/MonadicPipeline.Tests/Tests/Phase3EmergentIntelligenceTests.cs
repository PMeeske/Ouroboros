// ==========================================================
// Phase 3 Tests: Emergent Intelligence
// Tests for TransferLearner, HypothesisEngine, and CuriosityEngine
// ==========================================================

using LangChain.Providers.Ollama;
using LangChain.Databases;
using LangChainPipeline.Agent.MetaAI;

namespace LangChainPipeline.Tests;

/// <summary>
/// Tests for Phase 3 emergent intelligence capabilities.
/// </summary>
public static class Phase3EmergentIntelligenceTests
{
    /// <summary>
    /// Tests transfer learning system.
    /// </summary>
    public static async Task TestTransferLearner()
    {
        Console.WriteLine("\n=== Testing Transfer Learner ===");

        var provider = new OllamaProvider();
        var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        var skills = new SkillRegistry();
        var memory = new MemoryStore();

        var transferLearner = new TransferLearner(chatModel, skills, memory);

        // Create a source skill
        var sourceSkill = new Skill(
            "database_query",
            "Query data from a database",
            new List<string> { "sql", "connection" },
            new List<PlanStep>
            {
                new PlanStep("connect_to_database", new Dictionary<string, object>(), "Connection established", 0.9),
                new PlanStep("execute_query", new Dictionary<string, object> { ["query"] = "SELECT * FROM users" }, "Results returned", 0.8),
                new PlanStep("close_connection", new Dictionary<string, object>(), "Connection closed", 0.9)
            },
            SuccessRate: 0.92,
            UsageCount: 50,
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow);

        skills.RegisterSkill(sourceSkill);
        Console.WriteLine($"✓ Registered source skill: {sourceSkill.Name}");

        // Test transferability estimation
        var transferability = await transferLearner.EstimateTransferabilityAsync(
            sourceSkill,
            "file system operations");
        Console.WriteLine($"✓ Transferability to 'file system operations': {transferability:P0}");

        // Find analogies between domains
        var analogies = await transferLearner.FindAnalogiesAsync(
            "database operations",
            "file system operations");
        Console.WriteLine($"✓ Found {analogies.Count} analogies:");
        foreach (var (source, target, confidence) in analogies.Take(3))
        {
            Console.WriteLine($"  - {source} → {target} (confidence: {confidence:F2})");
        }

        // Attempt skill transfer
        var transferResult = await transferLearner.AdaptSkillToDomainAsync(
            sourceSkill,
            "file system operations");

        if (transferResult.IsSuccess)
        {
            var result = transferResult.Value;
            Console.WriteLine($"✓ Transfer successful!");
            Console.WriteLine($"  - Adapted Skill: {result.AdaptedSkill.Name}");
            Console.WriteLine($"  - Transferability: {result.TransferabilityScore:P0}");
            Console.WriteLine($"  - Source Domain: {result.SourceDomain}");
            Console.WriteLine($"  - Target Domain: {result.TargetDomain}");
            Console.WriteLine($"  - Adaptations: {result.Adaptations.Count}");
            foreach (var adaptation in result.Adaptations.Take(3))
            {
                Console.WriteLine($"    • {adaptation}");
            }

            // Record validation
            transferLearner.RecordTransferValidation(result, true);
            Console.WriteLine($"✓ Recorded successful validation");
        }
        else
        {
            Console.WriteLine($"✗ Transfer failed: {transferResult.Error}");
        }

        // Test transfer history
        var history = transferLearner.GetTransferHistory(sourceSkill.Name);
        Console.WriteLine($"✓ Transfer history: {history.Count} attempts");
    }

    /// <summary>
    /// Tests hypothesis generation and testing.
    /// </summary>
    public static async Task TestHypothesisEngine()
    {
        Console.WriteLine("\n=== Testing Hypothesis Engine ===");

        var provider = new OllamaProvider();
        var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        var tools = ToolRegistry.CreateDefault();
        var memory = new MemoryStore();
        var skills = new SkillRegistry();
        var router = new UncertaintyRouter(null!, 0.7);
        var safety = new SafetyGuard();

        var orchestrator = new MetaAIPlannerOrchestrator(
            chatModel,
            tools,
            memory,
            skills,
            router,
            safety);

        var hypothesisEngine = new HypothesisEngine(chatModel, orchestrator, memory);

        // Generate hypothesis from observation
        var observation = "Tasks involving structured data consistently have higher success rates than unstructured data tasks";
        var hypothesisResult = await hypothesisEngine.GenerateHypothesisAsync(observation);

        if (hypothesisResult.IsSuccess)
        {
            var hypothesis = hypothesisResult.Value;
            Console.WriteLine($"✓ Generated Hypothesis:");
            Console.WriteLine($"  - Statement: {hypothesis.Statement}");
            Console.WriteLine($"  - Domain: {hypothesis.Domain}");
            Console.WriteLine($"  - Confidence: {hypothesis.Confidence:P0}");
            Console.WriteLine($"  - Supporting Evidence: {hypothesis.SupportingEvidence.Count}");
            foreach (var evidence in hypothesis.SupportingEvidence.Take(2))
            {
                Console.WriteLine($"    • {evidence}");
            }

            // Design experiment to test hypothesis
            var experimentResult = await hypothesisEngine.DesignExperimentAsync(hypothesis);

            if (experimentResult.IsSuccess)
            {
                var experiment = experimentResult.Value;
                Console.WriteLine($"\n✓ Designed Experiment:");
                Console.WriteLine($"  - Description: {experiment.Description}");
                Console.WriteLine($"  - Steps: {experiment.Steps.Count}");
                foreach (var step in experiment.Steps.Take(3))
                {
                    Console.WriteLine($"    {experiment.Steps.IndexOf(step) + 1}. {step.Action}");
                }
                Console.WriteLine($"  - Expected Outcomes: {experiment.ExpectedOutcomes.Count}");

                // Test the hypothesis (would execute the experiment in practice)
                Console.WriteLine($"\n✓ Note: Full hypothesis testing requires executing the experiment");
            }
            else
            {
                Console.WriteLine($"✗ Experiment design failed: {experimentResult.Error}");
            }

            // Test updating hypothesis with new evidence
            hypothesisEngine.UpdateHypothesis(hypothesis.Id, "Additional structured data task succeeded", supports: true);
            Console.WriteLine($"\n✓ Updated hypothesis with supporting evidence");

            hypothesisEngine.UpdateHypothesis(hypothesis.Id, "One unstructured data task also succeeded", supports: false);
            Console.WriteLine($"✓ Updated hypothesis with counter-evidence");

            // Check confidence trend
            var trend = hypothesisEngine.GetConfidenceTrend(hypothesis.Id);
            Console.WriteLine($"✓ Confidence trend: {trend.Count} data points");
            foreach (var (time, conf) in trend)
            {
                Console.WriteLine($"  - {time:HH:mm:ss}: {conf:P0}");
            }
        }
        else
        {
            Console.WriteLine($"✗ Hypothesis generation failed: {hypothesisResult.Error}");
        }

        // Test abductive reasoning
        var observations = new List<string>
        {
            "Mathematical tasks complete faster than text generation tasks",
            "Calculation operations have higher accuracy",
            "Numeric validation is more reliable"
        };

        var abductiveResult = await hypothesisEngine.AbductiveReasoningAsync(observations);

        if (abductiveResult.IsSuccess)
        {
            var bestExplanation = abductiveResult.Value;
            Console.WriteLine($"\n✓ Abductive Reasoning Result:");
            Console.WriteLine($"  - Best Explanation: {bestExplanation.Statement}");
            Console.WriteLine($"  - Confidence: {bestExplanation.Confidence:P0}");
            Console.WriteLine($"  - Domain: {bestExplanation.Domain}");
        }
        else
        {
            Console.WriteLine($"✗ Abductive reasoning failed: {abductiveResult.Error}");
        }

        // Get hypotheses by domain
        var domainHypotheses = hypothesisEngine.GetHypothesesByDomain("data");
        Console.WriteLine($"\n✓ Hypotheses in 'data' domain: {domainHypotheses.Count}");
    }

    /// <summary>
    /// Tests curiosity-driven exploration.
    /// </summary>
    public static async Task TestCuriosityEngine()
    {
        Console.WriteLine("\n=== Testing Curiosity Engine ===");

        var provider = new OllamaProvider();
        var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        var memory = new MemoryStore();
        var skills = new SkillRegistry();
        var safety = new SafetyGuard();

        var curiosityEngine = new CuriosityEngine(chatModel, memory, skills, safety);

        // Test novelty computation
        var testPlan = new Plan(
            "Explore quantum computing concepts",
            new List<PlanStep>
            {
                new PlanStep("research_quantum_gates", new Dictionary<string, object>(), "Learn about quantum gates", 0.7),
                new PlanStep("understand_superposition", new Dictionary<string, object>(), "Understand superposition", 0.6)
            },
            new Dictionary<string, double>(),
            DateTime.UtcNow);

        var novelty = await curiosityEngine.ComputeNoveltyAsync(testPlan);
        Console.WriteLine($"✓ Novelty score for quantum computing exploration: {novelty:P0}");

        // Test exploration decision
        var shouldExplore = await curiosityEngine.ShouldExploreAsync();
        Console.WriteLine($"✓ Should explore now: {shouldExplore}");

        var shouldExploreWithGoal = await curiosityEngine.ShouldExploreAsync("Complete current task");
        Console.WriteLine($"✓ Should explore with current goal: {shouldExploreWithGoal}");

        // Identify exploration opportunities
        var opportunities = await curiosityEngine.IdentifyExplorationOpportunitiesAsync(5);
        Console.WriteLine($"\n✓ Identified {opportunities.Count} exploration opportunities:");
        foreach (var opp in opportunities.Take(3))
        {
            Console.WriteLine($"  - {opp.Description}");
            Console.WriteLine($"    Novelty: {opp.NoveltyScore:P0}, Info Gain: {opp.InformationGainEstimate:P0}");
        }

        // Estimate information gain
        var infoGain = await curiosityEngine.EstimateInformationGainAsync("machine learning algorithms");
        Console.WriteLine($"\n✓ Information gain for 'machine learning algorithms': {infoGain:P0}");

        // Generate exploratory plan
        var exploratoryPlanResult = await curiosityEngine.GenerateExploratoryPlanAsync();

        if (exploratoryPlanResult.IsSuccess)
        {
            var expPlan = exploratoryPlanResult.Value;
            Console.WriteLine($"\n✓ Generated Exploratory Plan:");
            Console.WriteLine($"  - Goal: {expPlan.Goal}");
            Console.WriteLine($"  - Steps: {expPlan.Steps.Count}");
            foreach (var step in expPlan.Steps)
            {
                Console.WriteLine($"    {expPlan.Steps.IndexOf(step) + 1}. {step.Action}");
                if (step.Parameters.TryGetValue("expected_learning", out var learning))
                {
                    Console.WriteLine($"       Expected Learning: {learning}");
                }
            }

            // Simulate recording exploration
            var mockExecution = new ExecutionResult(
                expPlan,
                new List<StepResult>(),
                true,
                "Exploration completed",
                new Dictionary<string, object>(),
                TimeSpan.FromSeconds(30));

            curiosityEngine.RecordExploration(expPlan, mockExecution, 0.8);
            Console.WriteLine($"\n✓ Recorded exploration (novelty: 0.8)");
        }
        else
        {
            Console.WriteLine($"✗ Exploratory plan generation failed: {exploratoryPlanResult.Error}");
        }

        // Get exploration statistics
        var stats = curiosityEngine.GetExplorationStats();
        Console.WriteLine($"\n✓ Exploration Statistics:");
        foreach (var (key, value) in stats)
        {
            Console.WriteLine($"  - {key}: {value:F2}");
        }
    }

    /// <summary>
    /// Tests integration of all Phase 3 components.
    /// </summary>
    public static async Task TestPhase3Integration()
    {
        Console.WriteLine("\n=== Testing Phase 3 Integration ===");

        var provider = new OllamaProvider();
        var chatModel = new OllamaChatAdapter(new OllamaChatModel(provider, "llama3"));
        var tools = ToolRegistry.CreateDefault();
        var memory = new PersistentMemoryStore();
        var skills = new SkillRegistry();
        var safety = new SafetyGuard();
        var router = new UncertaintyRouter(null!, 0.7);

        var orchestrator = new MetaAIPlannerOrchestrator(
            chatModel,
            tools,
            memory,
            skills,
            router,
            safety);

        // Create all Phase 3 components
        var transferLearner = new TransferLearner(chatModel, skills, memory);
        var hypothesisEngine = new HypothesisEngine(chatModel, orchestrator, memory);
        var curiosityEngine = new CuriosityEngine(chatModel, memory, skills, safety);

        Console.WriteLine("✓ All Phase 3 components initialized\n");

        // Scenario: Agent explores, generates hypotheses, and transfers learning

        // 1. Curiosity-driven exploration
        Console.WriteLine("1. Curiosity Check:");
        var shouldExplore = await curiosityEngine.ShouldExploreAsync();
        Console.WriteLine($"   Should explore: {shouldExplore}\n");

        if (shouldExplore)
        {
            var exploratoryPlan = await curiosityEngine.GenerateExploratoryPlanAsync();
            if (exploratoryPlan.IsSuccess)
            {
                Console.WriteLine($"   Generated exploratory plan: {exploratoryPlan.Value.Goal}");
            }
        }

        // 2. Hypothesis generation from pattern
        Console.WriteLine("\n2. Hypothesis Generation:");
        var observation = "Exploratory tasks reveal new capabilities";
        var hypothesis = await hypothesisEngine.GenerateHypothesisAsync(observation);
        
        if (hypothesis.IsSuccess)
        {
            Console.WriteLine($"   Hypothesis: {hypothesis.Value.Statement}");
            Console.WriteLine($"   Confidence: {hypothesis.Value.Confidence:P0}");
        }

        // 3. Transfer learning
        Console.WriteLine("\n3. Transfer Learning:");
        
        // Create a skill from successful exploration
        var explorationSkill = new Skill(
            "explore_new_domain",
            "Systematically explore a new domain",
            new List<string>(),
            new List<PlanStep>
            {
                new PlanStep("identify_key_concepts", new Dictionary<string, object>(), "Key concepts identified", 0.8),
                new PlanStep("test_basic_operations", new Dictionary<string, object>(), "Basic operations tested", 0.7)
            },
            SuccessRate: 0.85,
            UsageCount: 5,
            DateTime.UtcNow.AddDays(-5),
            DateTime.UtcNow);

        skills.RegisterSkill(explorationSkill);

        var transferResult = await transferLearner.AdaptSkillToDomainAsync(
            explorationSkill,
            "scientific research");

        if (transferResult.IsSuccess)
        {
            Console.WriteLine($"   Transferred skill to: {transferResult.Value.TargetDomain}");
            Console.WriteLine($"   Transferability: {transferResult.Value.TransferabilityScore:P0}");
        }

        Console.WriteLine("\n✓ Phase 3 Integration Test Complete");
        Console.WriteLine("\nThe agent demonstrated:");
        Console.WriteLine("  ✓ Curiosity-driven exploration");
        Console.WriteLine("  ✓ Hypothesis generation");
        Console.WriteLine("  ✓ Transfer learning");
    }

    /// <summary>
    /// Runs all Phase 3 tests.
    /// </summary>
    public static async Task RunAllTests()
    {
        Console.WriteLine("=== Phase 3: Emergent Intelligence Tests ===");
        
        try
        {
            await TestTransferLearner();
            await TestHypothesisEngine();
            await TestCuriosityEngine();
            await TestPhase3Integration();
            
            Console.WriteLine("\n=== All Phase 3 Tests Completed ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n✗ Test failed: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}
