// <copyright file="Phase2SelfModelExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using LangChainPipeline.Agent.MetaAI;
using LangChainPipeline.Agent.MetaAI.SelfModel;

namespace Ouroboros.Examples.Examples;

/// <summary>
/// Demonstrates Phase 2: Integrated Self-Model functionality.
/// Shows identity graph, global workspace, and predictive monitoring in action.
/// </summary>
public static class Phase2SelfModelExample
{
    /// <summary>
    /// Runs the Phase 2 self-model demonstration.
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine("=== Phase 2: Integrated Self-Model Example ===\n");

        // 1. Initialize self-model components
        Console.WriteLine("1. Initializing self-model components...\n");
        
        var capabilityRegistry = CreateCapabilityRegistry();
        var identityGraph = new IdentityGraph(
            Guid.NewGuid(),
            "DemoAgent",
            capabilityRegistry,
            Path.Combine(Path.GetTempPath(), "demo_agent_identity.json"));

        var globalWorkspace = new GlobalWorkspace();
        var predictiveMonitor = new PredictiveMonitor();

        // 2. Demonstrate Identity Graph
        Console.WriteLine("2. Identity Graph - Managing agent state...\n");
        
        // Register custom resources
        identityGraph.RegisterResource(new AgentResource(
            "API_Tokens",
            "Quota",
            1000.0,
            1000.0,
            "tokens/hour",
            DateTime.UtcNow,
            new Dictionary<string, object>()));

        // Create commitments
        AgentCommitment commitment1 = identityGraph.CreateCommitment(
            "Process user requests",
            DateTime.UtcNow.AddHours(8),
            0.8);

        AgentCommitment commitment2 = identityGraph.CreateCommitment(
            "Generate weekly report",
            DateTime.UtcNow.AddDays(1),
            0.6);

        Console.WriteLine($"Created {identityGraph.GetActiveCommitments().Count} active commitments");
        Console.WriteLine($"Resources tracked: {(await identityGraph.GetStateAsync()).Resources.Count}\n");

        // 3. Demonstrate Global Workspace
        Console.WriteLine("3. Global Workspace - Attention management...\n");

        // Add items with different priorities
        globalWorkspace.AddItem(
            "User query received: 'Explain machine learning'",
            WorkspacePriority.High,
            "UserInterface",
            new List<string> { "query", "ml" });

        globalWorkspace.AddItem(
            "Background task: Update knowledge base",
            WorkspacePriority.Low,
            "BackgroundService",
            new List<string> { "maintenance" });

        globalWorkspace.AddItem(
            "CRITICAL: API rate limit approaching",
            WorkspacePriority.Critical,
            "RateLimiter",
            new List<string> { "alert", "quota" });

        WorkspaceStatistics stats = globalWorkspace.GetStatistics();
        Console.WriteLine($"Workspace items: {stats.TotalItems}");
        Console.WriteLine($"High-priority items: {stats.HighPriorityItems}");
        Console.WriteLine($"Average attention weight: {stats.AverageAttentionWeight:F2}\n");

        // Show top attention items
        Console.WriteLine("Top attention items:");
        foreach (WorkspaceItem item in globalWorkspace.GetItems().Take(3))
        {
            Console.WriteLine($"  [{item.Priority}] {item.Content} (weight: {item.GetAttentionWeight():F2})");
        }
        Console.WriteLine();

        // 4. Demonstrate Predictive Monitoring
        Console.WriteLine("4. Predictive Monitoring - Forecasts and anomalies...\n");

        // Create forecasts
        Forecast responseForecast = predictiveMonitor.CreateForecast(
            "Response time forecast for next hour",
            "response_time_ms",
            250.0,
            0.85,
            DateTime.UtcNow.AddHours(1));

        Forecast successForecast = predictiveMonitor.CreateForecast(
            "Success rate forecast",
            "success_rate",
            0.95,
            0.9,
            DateTime.UtcNow.AddHours(1));

        Console.WriteLine($"Created {predictiveMonitor.GetPendingForecasts().Count} forecasts");

        // Simulate some history for anomaly detection
        for (int i = 0; i < 20; i++)
        {
            Forecast f = predictiveMonitor.CreateForecast(
                $"Historical metric {i}",
                "cpu_usage",
                50.0 + (i * 0.5),
                0.8,
                DateTime.UtcNow);
            predictiveMonitor.UpdateForecastOutcome(f.Id, 50.0 + (i * 0.5) + (Random.Shared.NextDouble() * 2));
        }

        // Detect an anomaly
        AnomalyDetection anomaly = await predictiveMonitor.DetectAnomalyAsync("cpu_usage", 95.0);
        Console.WriteLine($"Anomaly detected: {anomaly.IsAnomaly}");
        if (anomaly.IsAnomaly)
        {
            Console.WriteLine($"  Metric: {anomaly.MetricName}");
            Console.WriteLine($"  Observed: {anomaly.ObservedValue:F2}");
            Console.WriteLine($"  Expected: {anomaly.ExpectedValue:F2}");
            Console.WriteLine($"  Severity: {anomaly.Severity}");
        }
        Console.WriteLine();

        // Get calibration metrics
        ForecastCalibration calibration = predictiveMonitor.GetCalibration(TimeSpan.FromDays(1));
        Console.WriteLine("Forecast Calibration:");
        Console.WriteLine($"  Total forecasts: {calibration.TotalForecasts}");
        Console.WriteLine($"  Average accuracy: {calibration.AverageAccuracy:P1}");
        Console.WriteLine($"  Brier score: {calibration.BrierScore:F3}");
        Console.WriteLine();

        // 5. Update commitment progress
        Console.WriteLine("5. Updating commitments and recording performance...\n");
        
        identityGraph.UpdateCommitment(commitment1.Id, CommitmentStatus.InProgress, 40.0);
        
        // Record some task results
        var taskResult = new ExecutionResult(
            new Plan("Process query", new List<PlanStep>(), new Dictionary<string, double>(), DateTime.UtcNow),
            new List<StepResult>(),
            true,
            "Query processed successfully",
            new Dictionary<string, object>(),
            TimeSpan.FromMilliseconds(250));
        
        identityGraph.RecordTaskResult(taskResult);

        // 6. Get complete agent state
        Console.WriteLine("6. Agent Identity State Summary:\n");
        
        AgentIdentityState state = await identityGraph.GetStateAsync();
        Console.WriteLine($"Agent: {state.Name} ({state.AgentId})");
        Console.WriteLine($"Capabilities: {state.Capabilities.Count}");
        Console.WriteLine($"Active Commitments: {state.Commitments.Count(c => c.Status == CommitmentStatus.InProgress)}");
        Console.WriteLine($"Performance:");
        Console.WriteLine($"  Success Rate: {state.Performance.OverallSuccessRate:P0}");
        Console.WriteLine($"  Total Tasks: {state.Performance.TotalTasks}");
        Console.WriteLine($"  Avg Response Time: {state.Performance.AverageResponseTime:F0}ms");
        Console.WriteLine();

        // 7. Demonstrate workspace broadcasts for high-priority items
        Console.WriteLine("7. Recent workspace broadcasts:\n");
        
        List<WorkspaceBroadcast> broadcasts = globalWorkspace.GetRecentBroadcasts(5);
        foreach (WorkspaceBroadcast broadcast in broadcasts)
        {
            Console.WriteLine($"  [{broadcast.BroadcastTime:HH:mm:ss}] {broadcast.BroadcastReason}");
            Console.WriteLine($"    Content: {broadcast.Item.Content}");
        }
        Console.WriteLine();

        // 8. Save agent state
        Console.WriteLine("8. Persisting agent state...\n");
        await identityGraph.SaveStateAsync();
        Console.WriteLine($"State saved to: {Path.Combine(Path.GetTempPath(), "demo_agent_identity.json")}");

        Console.WriteLine("\n=== Phase 2 Example Complete ===");
        Console.WriteLine("\nKey Takeaways:");
        Console.WriteLine("✓ Identity Graph tracks agent capabilities, resources, and commitments");
        Console.WriteLine("✓ Global Workspace manages attention with priority-based policies");
        Console.WriteLine("✓ Predictive Monitor forecasts metrics and detects anomalies");
        Console.WriteLine("✓ All components work together for comprehensive self-modeling");
    }

    private static ICapabilityRegistry CreateCapabilityRegistry()
    {
        var mockLlm = new MockChatModel();
        var toolRegistry = new ToolRegistry();
        
        var registry = new CapabilityRegistry(mockLlm, toolRegistry, new CapabilityRegistryConfig());
        
        // Register some example capabilities
        registry.RegisterCapability(new AgentCapability(
            "QueryProcessing",
            "Process and respond to user queries",
            new List<string> { "LLM", "VectorStore" },
            0.92,
            250.0,
            new List<string> { "Limited to English language" },
            150,
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow,
            new Dictionary<string, object>()));

        registry.RegisterCapability(new AgentCapability(
            "DataRetrieval",
            "Retrieve relevant information from knowledge base",
            new List<string> { "VectorStore" },
            0.88,
            150.0,
            new List<string> { "Requires indexed data" },
            200,
            DateTime.UtcNow.AddDays(-60),
            DateTime.UtcNow,
            new Dictionary<string, object>()));

        return registry;
    }

    private sealed class MockChatModel : IChatCompletionModel
    {
        public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
        {
            return Task.FromResult("Mock LLM response for demonstration purposes");
        }
    }
}
