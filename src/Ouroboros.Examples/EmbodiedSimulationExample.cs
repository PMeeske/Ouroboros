// <copyright file="EmbodiedSimulationExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Application.Embodied;
using Ouroboros.Core.Monads;
using Ouroboros.Core.Ethics;
using Ouroboros.Domain.Embodied;
using Ouroboros.Domain.Reinforcement;

namespace Ouroboros.Examples;

/// <summary>
/// Example demonstrating embodied simulation with Unity ML-Agents integration.
/// Shows how to create environments, initialize agents, execute actions, and learn from experience.
/// </summary>
public static class EmbodiedSimulationExample
{
    /// <summary>
    /// Runs a complete embodied simulation example.
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    public static async Task RunAsync()
    {
        Console.WriteLine("=== Embodied Simulation Example ===\n");

        // Create environment manager
        var environmentManager = new EnvironmentManager(NullLogger<EnvironmentManager>.Instance);

        // List available environments
        Console.WriteLine("1. Listing available environments...");
        var listResult = await environmentManager.ListAvailableEnvironmentsAsync();
        if (listResult.IsSuccess)
        {
            foreach (var env in listResult.Value)
            {
                Console.WriteLine($"   - {env.Name}: {env.Description}");
                Console.WriteLine($"     Type: {env.Type}, Actions: {string.Join(", ", env.AvailableActions)}");
            }
        }

        Console.WriteLine("\n2. Creating embodied agent...");
        var ethics = EthicsFrameworkFactory.CreateDefault();
        var agent = new EmbodiedAgent(environmentManager, ethics, NullLogger<EmbodiedAgent>.Instance);

        // Define environment configuration
        var config = new EnvironmentConfig(
            SceneName: "BasicNavigation",
            Parameters: new Dictionary<string, object>
            {
                ["difficulty"] = 1,
                ["maxSteps"] = 100,
            },
            AvailableActions: new List<string> { "MoveForward", "MoveBackward", "TurnLeft", "TurnRight" },
            Type: EnvironmentType.Unity);

        // Initialize agent in environment
        Console.WriteLine("\n3. Initializing agent in environment...");
        var initResult = await agent.InitializeInEnvironmentAsync(config);
        if (initResult.IsSuccess)
        {
            Console.WriteLine("   Agent initialized successfully!");
        }
        else
        {
            Console.WriteLine($"   Initialization failed: {initResult.Error}");
            return;
        }

        // Perceive initial state
        Console.WriteLine("\n4. Perceiving initial state...");
        var perceiveResult = await agent.PerceiveAsync();
        if (perceiveResult.IsSuccess)
        {
            var state = perceiveResult.Value;
            Console.WriteLine($"   Position: ({state.Position.X:F2}, {state.Position.Y:F2}, {state.Position.Z:F2})");
            Console.WriteLine($"   Rotation: ({state.Rotation.X:F2}, {state.Rotation.Y:F2}, {state.Rotation.Z:F2}, {state.Rotation.W:F2})");
            Console.WriteLine($"   Velocity: ({state.Velocity.X:F2}, {state.Velocity.Y:F2}, {state.Velocity.Z:F2})");
        }

        // Execute a sequence of actions
        Console.WriteLine("\n5. Executing actions...");
        var transitions = new List<EmbodiedTransition>();

        for (int i = 0; i < 5; i++)
        {
            var stateBefore = perceiveResult.Value;

            // Create a movement action
            var action = EmbodiedAction.Move(
                new Vector3(0.5f, 0f, 0f),
                $"MoveForward_{i}");

            Console.WriteLine($"   Executing: {action.ActionName}");

            // Execute action
            var actionResult = await agent.ActAsync(action);
            if (actionResult.IsSuccess)
            {
                var result = actionResult.Value;
                Console.WriteLine($"     Success: {result.Success}, Reward: {result.Reward:F2}, Terminal: {result.EpisodeTerminated}");

                // Store transition for learning
                transitions.Add(new EmbodiedTransition(
                    StateBefore: stateBefore,
                    Action: action,
                    StateAfter: result.ResultingState,
                    Reward: result.Reward,
                    Terminal: result.EpisodeTerminated));

                perceiveResult = Result<SensorState, string>.Success(result.ResultingState);
            }
            else
            {
                Console.WriteLine($"     Failed: {actionResult.Error}");
            }

            await Task.Delay(100); // Simulate time between actions
        }

        // Learn from experience
        Console.WriteLine("\n6. Learning from experience...");
        Console.WriteLine($"   Processing {transitions.Count} transitions");
        var learnResult = await agent.LearnFromExperienceAsync(transitions);
        if (learnResult.IsSuccess)
        {
            Console.WriteLine("   Learning completed successfully!");
        }
        else
        {
            Console.WriteLine($"   Learning failed: {learnResult.Error}");
        }

        // Plan to achieve a goal
        Console.WriteLine("\n7. Planning embodied actions...");
        var currentState = perceiveResult.Value;
        var planResult = await agent.PlanEmbodiedAsync("Navigate to target", currentState);
        if (planResult.IsSuccess)
        {
            var plan = planResult.Value;
            Console.WriteLine($"   Generated plan for: {plan.Goal}");
            Console.WriteLine($"   Actions: {plan.Actions.Count}");
            Console.WriteLine($"   Confidence: {plan.Confidence:P0}");
            Console.WriteLine($"   Estimated Reward: {plan.EstimatedReward:F2}");

            foreach (var (action, index) in plan.Actions.Select((a, i) => (a, i)))
            {
                Console.WriteLine($"     Step {index + 1}: {action.ActionName ?? "Unnamed"}");
            }
        }

        Console.WriteLine("\n=== Example Complete ===");
    }

    /// <summary>
    /// Demonstrates Unity ML-Agents client usage.
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    public static async Task DemonstrateUnityClientAsync()
    {
        Console.WriteLine("\n=== Unity ML-Agents Client Example ===\n");

        await using var client = new UnityMLAgentsClient(
            "localhost",
            5005,
            NullLogger<UnityMLAgentsClient>.Instance);

        Console.WriteLine("1. Connecting to Unity ML-Agents...");
        var connectResult = await client.ConnectAsync();
        if (connectResult.IsSuccess)
        {
            Console.WriteLine("   Connected successfully!");

            // Get initial sensor state
            Console.WriteLine("\n2. Getting sensor state...");
            var stateResult = await client.GetSensorStateAsync();
            if (stateResult.IsSuccess)
            {
                Console.WriteLine("   Sensor state retrieved");
            }

            // Send an action
            Console.WriteLine("\n3. Sending action...");
            var action = EmbodiedAction.Move(Vector3.UnitX, "MoveRight");
            var actionResult = await client.SendActionAsync(action);
            if (actionResult.IsSuccess)
            {
                Console.WriteLine("   Action executed successfully");
            }

            // Reset environment
            Console.WriteLine("\n4. Resetting environment...");
            var resetResult = await client.ResetEnvironmentAsync();
            if (resetResult.IsSuccess)
            {
                Console.WriteLine("   Environment reset successfully");
            }

            Console.WriteLine("\n5. Disconnecting...");
            await client.DisconnectAsync();
            Console.WriteLine("   Disconnected");
        }
        else
        {
            Console.WriteLine($"   Connection failed: {connectResult.Error}");
            Console.WriteLine("   (This is expected if Unity ML-Agents server is not running)");
        }

        Console.WriteLine("\n=== Client Example Complete ===");
    }
}
