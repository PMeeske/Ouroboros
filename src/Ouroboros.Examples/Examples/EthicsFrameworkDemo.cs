// <copyright file="EthicsFrameworkDemo.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Core.Ethics;

namespace Ouroboros.Examples;

/// <summary>
/// Demonstrates the Ethics Framework in action.
/// </summary>
public static class EthicsFrameworkDemo
{
    /// <summary>
    /// Runs a demonstration of the ethics framework.
    /// </summary>
    public static async Task RunDemoAsync()
    {
        Console.WriteLine("=== Ouroboros Ethics Framework Demo ===\n");

        // Create the ethics framework
        var framework = EthicsFrameworkFactory.CreateDefault();

        // Show core principles
        Console.WriteLine("Core Ethical Principles:");
        foreach (var principle in framework.GetCorePrinciples())
        {
            Console.WriteLine($"  - {principle.Name} (Priority: {principle.Priority}, Mandatory: {principle.IsMandatory})");
        }
        Console.WriteLine();

        // Create a test context
        var context = new ActionContext
        {
            AgentId = "demo-agent",
            UserId = "demo-user",
            Environment = "demo",
            State = new Dictionary<string, object>()
        };

        // Test 1: Safe action
        Console.WriteLine("Test 1: Evaluating safe action...");
        var safeAction = new ProposedAction
        {
            ActionType = "read_file",
            Description = "Read configuration file",
            Parameters = new Dictionary<string, object> { ["path"] = "/config/app.json" },
            PotentialEffects = new[] { "Load settings" }
        };

        var result1 = await framework.EvaluateActionAsync(safeAction, context);
        Console.WriteLine($"  Result: {result1.Value.Level}");
        Console.WriteLine($"  Permitted: {result1.Value.IsPermitted}");
        Console.WriteLine($"  Reasoning: {result1.Value.Reasoning}\n");

        // Test 2: Harmful action
        Console.WriteLine("Test 2: Evaluating harmful action...");
        var harmfulAction = new ProposedAction
        {
            ActionType = "attack_system",
            Description = "Attempt to harm the system",
            Parameters = new Dictionary<string, object>(),
            PotentialEffects = new[] { "System damage" }
        };

        var result2 = await framework.EvaluateActionAsync(harmfulAction, context);
        Console.WriteLine($"  Result: {result2.Value.Level}");
        Console.WriteLine($"  Permitted: {result2.Value.IsPermitted}");
        Console.WriteLine($"  Violations: {result2.Value.Violations.Count}");
        if (result2.Value.Violations.Count > 0)
        {
            Console.WriteLine($"  First Violation: {result2.Value.Violations[0].Description}");
        }
        Console.WriteLine();

        // Test 3: Self-modification
        Console.WriteLine("Test 3: Evaluating self-modification request...");
        var selfModRequest = new SelfModificationRequest
        {
            Type = ModificationType.CapabilityAddition,
            Description = "Add new learning capability",
            Justification = "Improve performance",
            ActionContext = context,
            ExpectedImprovements = new[] { "Better learning" },
            PotentialRisks = new[] { "Unknown behavior" },
            IsReversible = true,
            ImpactLevel = 0.6
        };

        var result3 = await framework.EvaluateSelfModificationAsync(selfModRequest);
        Console.WriteLine($"  Result: {result3.Value.Level}");
        Console.WriteLine($"  Permitted: {result3.Value.IsPermitted}");
        Console.WriteLine($"  Reasoning: {result3.Value.Reasoning}\n");

        Console.WriteLine("=== Demo Complete ===");
    }
}
