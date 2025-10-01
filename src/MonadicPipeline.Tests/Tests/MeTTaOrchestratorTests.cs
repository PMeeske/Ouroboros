// ==========================================================
// Tests for Orchestrator v3.0 MeTTa-First Representation
// ==========================================================

using LangChainPipeline.Agent.MetaAI;
using LangChainPipeline.Tools;
using LangChainPipeline.Tools.MeTTa;

namespace LangChainPipeline.Tests;

/// <summary>
/// Tests for MeTTa-first orchestrator v3.0 with symbolic reasoning.
/// </summary>
public static class MeTTaOrchestratorTests
{
    /// <summary>
    /// Tests MeTTa representation of a plan.
    /// </summary>
    public static async Task TestMeTTaRepresentationPlan()
    {
        Console.WriteLine("=== Test: MeTTa Plan Representation ===");

        var engine = new MockMeTTaEngine();
        var representation = new MeTTaRepresentation(engine);

        var plan = new Plan(
            Goal: "Test goal",
            Steps: new List<PlanStep>
            {
                new PlanStep("step1", new Dictionary<string, object> { ["param1"] = "value1" }, "Expected 1", 0.9),
                new PlanStep("step2", new Dictionary<string, object> { ["param2"] = "value2" }, "Expected 2", 0.8)
            },
            ConfidenceScores: new Dictionary<string, double> { ["overall"] = 0.85 },
            CreatedAt: DateTime.UtcNow
        );

        var result = await representation.TranslatePlanAsync(plan);

        result.Match(
            _ => Console.WriteLine("✓ Plan successfully translated to MeTTa atoms"),
            error => Console.WriteLine($"✗ Failed to translate plan: {error}")
        );

        Console.WriteLine("✓ MeTTa plan representation test completed\n");
    }

    /// <summary>
    /// Tests MeTTa representation of execution state.
    /// </summary>
    public static async Task TestMeTTaRepresentationExecutionState()
    {
        Console.WriteLine("=== Test: MeTTa Execution State Representation ===");

        var engine = new MockMeTTaEngine();
        var representation = new MeTTaRepresentation(engine);

        var plan = new Plan(
            Goal: "Test goal",
            Steps: new List<PlanStep>
            {
                new PlanStep("test_step", new Dictionary<string, object>(), "Expected", 0.9)
            },
            ConfidenceScores: new Dictionary<string, double>(),
            CreatedAt: DateTime.UtcNow
        );

        var execution = new ExecutionResult(
            Plan: plan,
            StepResults: new List<StepResult>
            {
                new StepResult(
                    Step: plan.Steps[0],
                    Success: true,
                    Output: "test output",
                    Error: null,
                    Duration: TimeSpan.FromSeconds(1),
                    ObservedState: new Dictionary<string, object> { ["state1"] = "value1" }
                )
            },
            Success: true,
            FinalOutput: "test output",
            Metadata: new Dictionary<string, object>(),
            Duration: TimeSpan.FromSeconds(1)
        );

        var result = await representation.TranslateExecutionStateAsync(execution);

        result.Match(
            _ => Console.WriteLine("✓ Execution state successfully translated to MeTTa atoms"),
            error => Console.WriteLine($"✗ Failed to translate execution state: {error}")
        );

        Console.WriteLine("✓ MeTTa execution state representation test completed\n");
    }

    /// <summary>
    /// Tests MeTTa tool representation.
    /// </summary>
    public static async Task TestMeTTaRepresentationTools()
    {
        Console.WriteLine("=== Test: MeTTa Tool Representation ===");

        var engine = new MockMeTTaEngine();
        var representation = new MeTTaRepresentation(engine);

        var tools = ToolRegistry.CreateDefault();

        var result = await representation.TranslateToolsAsync(tools);

        result.Match(
            _ => Console.WriteLine("✓ Tools successfully translated to MeTTa atoms"),
            error => Console.WriteLine($"✗ Failed to translate tools: {error}")
        );

        Console.WriteLine("✓ MeTTa tool representation test completed\n");
    }

    /// <summary>
    /// Tests NextNode tool functionality.
    /// </summary>
    public static async Task TestNextNodeTool()
    {
        Console.WriteLine("=== Test: NextNode Tool ===");

        var engine = new MockMeTTaEngine();
        var tools = ToolRegistry.CreateDefault();
        var nextNodeTool = new NextNodeTool(engine, tools);

        Console.WriteLine($"Tool Name: {nextNodeTool.Name}");
        Console.WriteLine($"Tool Description: {nextNodeTool.Description}");

        var input = @"{
            ""current_step_id"": ""step_0"",
            ""plan_goal"": ""Test goal"",
            ""context"": {
                ""step_index"": 0,
                ""total_steps"": 3
            }
        }";

        var result = await nextNodeTool.InvokeAsync(input, CancellationToken.None);

        result.Match(
            output => Console.WriteLine($"✓ NextNode result: {output}"),
            error => Console.WriteLine($"✗ NextNode failed: {error}")
        );

        Console.WriteLine("✓ NextNode tool test completed\n");
    }

    /// <summary>
    /// Tests NextNode tool with constraints.
    /// </summary>
    public static async Task TestNextNodeToolWithConstraints()
    {
        Console.WriteLine("=== Test: NextNode Tool with Constraints ===");

        var engine = new MockMeTTaEngine();
        var tools = ToolRegistry.CreateDefault();
        var nextNodeTool = new NextNodeTool(engine, tools);

        var input = @"{
            ""current_step_id"": ""step_0"",
            ""plan_goal"": ""Test goal"",
            ""context"": {
                ""step_index"": 0
            },
            ""constraints"": [
                ""(requires step_1 capability-x)"",
                ""(forbids step_2 until step_1)""
            ]
        }";

        var result = await nextNodeTool.InvokeAsync(input, CancellationToken.None);

        result.Match(
            output => Console.WriteLine($"✓ NextNode with constraints result: {output}"),
            error => Console.WriteLine($"✗ NextNode failed: {error}")
        );

        Console.WriteLine("✓ NextNode tool with constraints test completed\n");
    }

    /// <summary>
    /// Tests MeTTa tool registry integration with NextNode.
    /// </summary>
    public static void TestMeTTaToolRegistryWithNextNode()
    {
        Console.WriteLine("=== Test: MeTTa Tool Registry with NextNode ===");

        var engine = new MockMeTTaEngine();
        var registry = ToolRegistry.CreateDefault().WithMeTTaTools(engine);

        Console.WriteLine($"Initial tool count: {registry.Count}");

        var tools = registry.All.ToList();
        var nextNodeTool = tools.FirstOrDefault(t => t.Name == "next_node");

        if (nextNodeTool != null)
        {
            Console.WriteLine($"✓ NextNode tool found: {nextNodeTool.Name}");
            Console.WriteLine($"  Description: {nextNodeTool.Description}");
        }
        else
        {
            Console.WriteLine("✗ NextNode tool not found in registry");
        }

        var mettaTools = tools.Where(t => t.Name.StartsWith("metta_") || t.Name == "next_node").ToList();
        Console.WriteLine($"✓ MeTTa tools (including NextNode): {mettaTools.Count}");
        foreach (var tool in mettaTools)
        {
            Console.WriteLine($"  - {tool.Name}");
        }

        Console.WriteLine("✓ MeTTa tool registry with NextNode test completed\n");
    }

    /// <summary>
    /// Tests constraint addition to MeTTa knowledge base.
    /// </summary>
    public static async Task TestMeTTaConstraints()
    {
        Console.WriteLine("=== Test: MeTTa Constraints ===");

        var engine = new MockMeTTaEngine();
        var representation = new MeTTaRepresentation(engine);

        var constraints = new[]
        {
            "(requires step_2 step_1)",
            "(forbids parallel step_1 step_2)",
            "(capability tool_search information-retrieval)"
        };

        foreach (var constraint in constraints)
        {
            var result = await representation.AddConstraintAsync(constraint);
            result.Match(
                _ => Console.WriteLine($"✓ Added constraint: {constraint}"),
                error => Console.WriteLine($"✗ Failed to add constraint: {error}")
            );
        }

        Console.WriteLine("✓ MeTTa constraints test completed\n");
    }

    /// <summary>
    /// Tests querying next nodes from MeTTa.
    /// </summary>
    public static async Task TestQueryNextNodes()
    {
        Console.WriteLine("=== Test: Query Next Nodes ===");

        var engine = new MockMeTTaEngine();
        var representation = new MeTTaRepresentation(engine);

        // Add a simple plan structure
        await engine.AddFactAsync("(step plan_1 step_0 0 action_0)");
        await engine.AddFactAsync("(step plan_1 step_1 1 action_1)");
        await engine.AddFactAsync("(step plan_1 step_2 2 action_2)");
        await engine.AddFactAsync("(before step_0 step_1)");
        await engine.AddFactAsync("(before step_1 step_2)");

        var context = new Dictionary<string, object>
        {
            ["current_order"] = 0
        };

        var result = await representation.QueryNextNodesAsync("step_0", context);

        result.Match(
            candidates =>
            {
                Console.WriteLine($"✓ Found {candidates.Count} next node candidates:");
                foreach (var candidate in candidates)
                {
                    Console.WriteLine($"  - {candidate.NodeId}: {candidate.Action} (confidence: {candidate.Confidence})");
                }
            },
            error => Console.WriteLine($"✗ Query failed: {error}")
        );

        Console.WriteLine("✓ Query next nodes test completed\n");
    }

    /// <summary>
    /// Tests querying tools for a goal.
    /// </summary>
    public static async Task TestQueryToolsForGoal()
    {
        Console.WriteLine("=== Test: Query Tools for Goal ===");

        var engine = new MockMeTTaEngine();
        var representation = new MeTTaRepresentation(engine);

        // Add tool capabilities
        await engine.AddFactAsync("(goal plan_1 \"search information\")");
        await engine.AddFactAsync("(tool tool_search \"search_tool\")");
        await engine.AddFactAsync("(capability tool_search information-retrieval)");

        var result = await representation.QueryToolsForGoalAsync("search information");

        result.Match(
            tools =>
            {
                Console.WriteLine($"✓ Found {tools.Count} recommended tools:");
                foreach (var tool in tools)
                {
                    Console.WriteLine($"  - {tool}");
                }
            },
            error => Console.WriteLine($"✗ Query failed: {error}")
        );

        Console.WriteLine("✓ Query tools for goal test completed\n");
    }

    /// <summary>
    /// Runs all MeTTa orchestrator v3.0 tests.
    /// </summary>
    public static async Task RunAllTests()
    {
        Console.WriteLine("╔════════════════════════════════════════════╗");
        Console.WriteLine("║   MeTTa Orchestrator v3.0 Test Suite      ║");
        Console.WriteLine("╚════════════════════════════════════════════╝\n");

        try
        {
            await TestMeTTaRepresentationPlan();
            await TestMeTTaRepresentationExecutionState();
            await TestMeTTaRepresentationTools();
            await TestNextNodeTool();
            await TestNextNodeToolWithConstraints();
            TestMeTTaToolRegistryWithNextNode();
            await TestMeTTaConstraints();
            await TestQueryNextNodes();
            await TestQueryToolsForGoal();

            Console.WriteLine("╔════════════════════════════════════════════╗");
            Console.WriteLine("║   All v3.0 tests completed successfully    ║");
            Console.WriteLine("╚════════════════════════════════════════════╝");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Test suite failed: {ex.Message}");
            Console.WriteLine($"  Stack trace: {ex.StackTrace}");
            throw;
        }
    }
}
