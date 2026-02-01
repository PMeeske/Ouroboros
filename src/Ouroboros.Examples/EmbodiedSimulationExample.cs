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

    /// <summary>
    /// Demonstrates complete Unity ML-Agents system with all components.
    /// </summary>
    /// <returns>A task representing the asynchronous operation</returns>
    public static async Task DemonstrateCompleteSystemAsync()
    {
        Console.WriteLine("\n=== Complete Unity ML-Agents System Example ===\n");

        // Setup components
        var environmentManager = new EnvironmentManager(NullLogger<EnvironmentManager>.Instance);
        var ethics = EthicsFrameworkFactory.CreateDefault();

        // Create Unity ML-Agents client (optional - can be null for mock mode)
        await using var unityClient = new UnityMLAgentsClient(
            "localhost",
            5005,
            NullLogger<UnityMLAgentsClient>.Instance);

        // Create visual processor for image observations
        var visualProcessor = new VisualProcessor(
            inputWidth: 84,
            inputHeight: 84,
            featureDimension: 64,
            NullLogger<VisualProcessor>.Instance);

        // Create RL agent for policy learning
        var rlAgent = new RLAgent(
            epsilon: 0.1,
            learningRate: 0.01,
            discountFactor: 0.99,
            maxBufferSize: 10000,
            NullLogger<RLAgent>.Instance);

        // Create reward shaper for experience enhancement
        var rewardShaper = new RewardShaper(
            distanceRewardWeight: 1.0,
            velocityPenaltyWeight: 0.01,
            curiosityBonusWeight: 0.1,
            maxNoveltyBufferSize: 1000,
            NullLogger<RewardShaper>.Instance);

        // Create embodied agent with all components
        var agent = new EmbodiedAgent(
            environmentManager,
            ethics,
            NullLogger<EmbodiedAgent>.Instance,
            maxBufferSize: 10000,
            unityClient: unityClient,
            visualProcessor: visualProcessor,
            rlAgent: rlAgent,
            rewardShaper: rewardShaper);

        Console.WriteLine("1. Created agent with integrated components:");
        Console.WriteLine("   - Unity ML-Agents Client");
        Console.WriteLine("   - Visual Processor (84x84 -> 64D features)");
        Console.WriteLine($"   - RL Agent (ε={rlAgent.Epsilon}, α={rlAgent.LearningRate}, γ={rlAgent.DiscountFactor})");
        Console.WriteLine("   - Reward Shaper (distance + velocity + curiosity)");

        // Define environment configuration
        var config = new EnvironmentConfig(
            SceneName: "AdvancedNavigation",
            Parameters: new Dictionary<string, object>
            {
                ["difficulty"] = 2,
                ["maxSteps"] = 200,
                ["enableVisualObs"] = true
            },
            AvailableActions: new List<string> { "MoveForward", "MoveBackward", "TurnLeft", "TurnRight", "Jump" },
            Type: EnvironmentType.Unity);

        // Initialize agent
        Console.WriteLine("\n2. Initializing agent in Unity environment...");
        var initResult = await agent.InitializeInEnvironmentAsync(config);
        if (initResult.IsSuccess)
        {
            Console.WriteLine("   Agent initialized successfully!");
        }
        else
        {
            Console.WriteLine($"   Initialization failed: {initResult.Error}");
            Console.WriteLine("   (This is expected if Unity ML-Agents server is not running)");
            return;
        }

        // Run episode with RL agent
        Console.WriteLine("\n3. Running episode with RL-based action selection...");
        var episodeTransitions = new List<EmbodiedTransition>();

        for (int step = 0; step < 10; step++)
        {
            // Perceive current state
            var perceiveResult = await agent.PerceiveAsync();
            if (perceiveResult.IsFailure)
            {
                Console.WriteLine($"   Perception failed: {perceiveResult.Error}");
                break;
            }

            var currentState = perceiveResult.Value;

            // Select action using RL agent
            var availableActions = new[]
            {
                EmbodiedAction.Move(new Vector3(1f, 0f, 0f), "MoveForward"),
                EmbodiedAction.Move(new Vector3(-1f, 0f, 0f), "MoveBackward"),
                EmbodiedAction.Rotate(new Vector3(0f, 0.1f, 0f), "TurnLeft"),
                EmbodiedAction.Rotate(new Vector3(0f, -0.1f, 0f), "TurnRight")
            };

            var actionResult = await rlAgent.SelectActionAsync(currentState, availableActions);
            if (actionResult.IsFailure)
            {
                Console.WriteLine($"   Action selection failed: {actionResult.Error}");
                break;
            }

            var selectedAction = actionResult.Value;

            // Execute action (with reward shaping applied internally)
            var actResult = await agent.ActAsync(selectedAction);
            if (actResult.IsFailure)
            {
                Console.WriteLine($"   Action execution failed: {actResult.Error}");
                break;
            }

            var result = actResult.Value;
            Console.WriteLine($"   Step {step + 1}: {selectedAction.ActionName} -> Reward: {result.Reward:F4}");

            // Store transition for learning
            var transition = new EmbodiedTransition(
                StateBefore: currentState,
                Action: selectedAction,
                StateAfter: result.ResultingState,
                Reward: result.Reward,
                Terminal: result.EpisodeTerminated);

            episodeTransitions.Add(transition);
            rlAgent.StoreTransition(transition);

            if (result.EpisodeTerminated)
            {
                Console.WriteLine("   Episode terminated");
                break;
            }

            await Task.Delay(50); // Simulate time between steps
        }

        // Learn from experience
        Console.WriteLine("\n4. Learning from episode experience...");
        var learnResult = await agent.LearnFromExperienceAsync(episodeTransitions);
        if (learnResult.IsSuccess)
        {
            Console.WriteLine($"   Learning completed! Experience buffer: {rlAgent.ExperienceBufferSize} transitions");
        }
        else
        {
            Console.WriteLine($"   Learning failed: {learnResult.Error}");
        }

        // Demonstrate visual processing (if visual observations available)
        Console.WriteLine("\n5. Demonstrating visual processing...");
        var mockPixels = new byte[84 * 84 * 3]; // Mock RGB image
        Random.Shared.NextBytes(mockPixels);

        var featureResult = await visualProcessor.ExtractFeaturesAsync(mockPixels);
        if (featureResult.IsSuccess)
        {
            var features = featureResult.Value;
            Console.WriteLine($"   Extracted {features.Length} features from 84x84 RGB image");
            Console.WriteLine($"   Feature stats: min={features.Min():F4}, max={features.Max():F4}, mean={features.Average():F4}");
        }

        // Demonstrate Gym environment adapter
        Console.WriteLine("\n6. Demonstrating Gym environment adapter...");
        await using var gymAdapter = new GymEnvironmentAdapter(
            "CartPole-v1",
            NullLogger<GymEnvironmentAdapter>.Instance);

        var gymConnectResult = await gymAdapter.ConnectAsync();
        if (gymConnectResult.IsSuccess)
        {
            Console.WriteLine("   Connected to Gym environment (mock)");

            var resetResult = await gymAdapter.ResetAsync();
            if (resetResult.IsSuccess)
            {
                Console.WriteLine("   Environment reset successfully");
            }

            var gymAction = EmbodiedAction.Move(Vector3.UnitX, "CartPoleAction");
            var stepResult = await gymAdapter.StepAsync(gymAction);
            if (stepResult.IsSuccess)
            {
                Console.WriteLine($"   Step executed: reward={stepResult.Value.Reward:F4}");
            }
        }

        Console.WriteLine("\n=== Complete System Example Complete ===");
    }
}
