// <copyright file="WorldModelExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Examples;

using Ouroboros.Agent.MetaAI.WorldModel;

/// <summary>
/// Example demonstrating world model learning and imagination-based planning.
/// Shows how to use the WorldModelEngine for model-based reinforcement learning.
/// </summary>
public static class WorldModelExample
{
    /// <summary>
    /// Demonstrates the complete workflow of world model learning.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task RunAsync()
    {
        Console.WriteLine("=== World Model Learning Example ===\n");

        // Create the world model engine
        var engine = new WorldModelEngine(seed: 42);

        // Step 1: Generate synthetic training data (in practice, comes from environment)
        Console.WriteLine("1. Generating training data...");
        var transitions = GenerateSyntheticData(numTransitions: 100);
        Console.WriteLine($"   Generated {transitions.Count} transitions\n");

        // Step 2: Learn a world model from experience
        Console.WriteLine("2. Learning world model from experience...");
        var learnResult = await engine.LearnModelAsync(transitions, ModelArchitecture.MLP);

        if (learnResult.IsFailure)
        {
            Console.WriteLine($"   Failed to learn model: {learnResult.Error}");
            return;
        }

        var model = learnResult.Value;
        Console.WriteLine($"   Model ID: {model.Id}");
        Console.WriteLine($"   Domain: {model.Domain}");
        Console.WriteLine($"   Training samples: {model.Hyperparameters["training_samples"]}\n");

        // Step 3: Evaluate model quality on test set
        Console.WriteLine("3. Evaluating model quality...");
        var testData = GenerateSyntheticData(numTransitions: 20, seed: 999);
        var evalResult = await engine.EvaluateModelAsync(model, testData);

        if (evalResult.IsSuccess)
        {
            var quality = evalResult.Value;
            Console.WriteLine($"   Prediction Accuracy: {quality.PredictionAccuracy:F3}");
            Console.WriteLine($"   Reward Correlation: {quality.RewardCorrelation:F3}");
            Console.WriteLine($"   Terminal Accuracy: {quality.TerminalAccuracy:F3}");
            Console.WriteLine($"   Calibration Error: {quality.CalibrationError:F3}");
            Console.WriteLine($"   Test Samples: {quality.TestSamples}\n");
        }

        // Step 4: Plan in imagination using the learned model
        Console.WriteLine("4. Planning in imagination...");
        var initialState = transitions[0].PreviousState;
        var planResult = await engine.PlanInImaginationAsync(
            initialState,
            goal: "Maximize cumulative reward",
            model,
            lookaheadDepth: 5);

        if (planResult.IsSuccess)
        {
            var plan = planResult.Value;
            Console.WriteLine($"   Plan Description: {plan.Description}");
            Console.WriteLine($"   Expected Reward: {plan.ExpectedReward:F2}");
            Console.WriteLine($"   Confidence: {plan.Confidence:F2}");
            Console.WriteLine($"   Actions ({plan.Actions.Count}):");
            for (int i = 0; i < plan.Actions.Count; i++)
            {
                Console.WriteLine($"     {i + 1}. {plan.Actions[i].Name}");
            }

            Console.WriteLine();
        }

        // Step 5: Generate synthetic experience for data augmentation
        Console.WriteLine("5. Generating synthetic experience...");
        var syntheticResult = await engine.GenerateSyntheticExperienceAsync(
            model,
            initialState,
            trajectoryLength: 10);

        if (syntheticResult.IsSuccess)
        {
            var syntheticExperience = syntheticResult.Value;
            Console.WriteLine($"   Generated {syntheticExperience.Count} synthetic transitions");
            Console.WriteLine($"   Trajectory terminated: {syntheticExperience.Any() && syntheticExperience.Last().Terminal}");

            // Show first few transitions
            Console.WriteLine("   First 3 transitions:");
            foreach (var transition in syntheticExperience.Take(3))
            {
                Console.WriteLine($"     Action: {transition.ActionTaken.Name}, Reward: {transition.Reward:F2}, Terminal: {transition.Terminal}");
            }
        }

        Console.WriteLine("\n=== Example Complete ===");
    }

    private static List<Transition> GenerateSyntheticData(int numTransitions, int seed = 42)
    {
        var random = new Random(seed);
        var transitions = new List<Transition>();
        int embeddingSize = 16;

        for (int i = 0; i < numTransitions; i++)
        {
            var prevState = CreateRandomState(random, embeddingSize);
            var action = CreateRandomAction(random);
            var nextState = CreateRandomState(random, embeddingSize);
            var reward = random.NextDouble() * 10 - 5; // -5 to 5
            var terminal = random.NextDouble() < 0.1; // 10% terminal

            transitions.Add(new Transition(prevState, action, nextState, reward, terminal));
        }

        return transitions;
    }

    private static State CreateRandomState(Random random, int embeddingSize)
    {
        var features = new Dictionary<string, object>
        {
            ["position_x"] = random.NextDouble() * 100,
            ["position_y"] = random.NextDouble() * 100,
            ["velocity"] = random.NextDouble() * 10,
            ["health"] = random.Next(0, 100),
        };

        var embedding = new float[embeddingSize];
        for (int i = 0; i < embeddingSize; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1);
        }

        return new State(features, embedding);
    }

    private static Action CreateRandomAction(Random random)
    {
        var actionNames = new[] { "move_forward", "move_backward", "turn_left", "turn_right", "jump", "attack" };
        var name = actionNames[random.Next(actionNames.Length)];
        var parameters = new Dictionary<string, object>
        {
            ["speed"] = random.NextDouble(),
            ["duration"] = random.NextDouble() * 2,
        };

        return new Action(name, parameters);
    }
}
