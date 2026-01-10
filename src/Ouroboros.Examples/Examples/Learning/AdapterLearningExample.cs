// <copyright file="AdapterLearningExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Examples.Learning;

using Microsoft.Extensions.Logging.Abstractions;
using Ouroboros.Core.Learning;
using Ouroboros.Domain.Learning;

/// <summary>
/// Example demonstrating the usage of the LoRA/PEFT Adapter Learning Engine.
/// This example shows how to create, train, and use adapters for continual learning.
/// </summary>
public static class AdapterLearningExample
{
    /// <summary>
    /// Runs the complete adapter learning example.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    public static async Task RunExampleAsync()
    {
        Console.WriteLine("=== LoRA/PEFT Adapter Learning Example ===\n");

        // 1. Setup the learning engine with mock components
        var peft = new MockPeftIntegration(NullLogger<MockPeftIntegration>.Instance);
        var storage = new InMemoryAdapterStorage(NullLogger<InMemoryAdapterStorage>.Instance);
        var blobStorage = new FileSystemBlobStorage(
            Path.Combine(Path.GetTempPath(), "adapter_example"),
            NullLogger<FileSystemBlobStorage>.Instance);

        var engine = new AdapterLearningEngine(
            peft,
            storage,
            blobStorage,
            "llama-2-7b",
            NullLogger<AdapterLearningEngine>.Instance);

        // 2. Create an adapter for a specific task
        Console.WriteLine("Creating adapter for 'sentiment-analysis' task...");
        var createResult = await engine.CreateAdapterAsync(
            "sentiment-analysis",
            AdapterConfig.Default());

        if (createResult.IsFailure)
        {
            Console.WriteLine($"Failed to create adapter: {createResult.Error}");
            return;
        }

        var adapterId = createResult.Value;
        Console.WriteLine($"✓ Created adapter: {adapterId}\n");

        // 3. Train the adapter with examples
        Console.WriteLine("Training adapter with sentiment examples...");
        var trainingExamples = new List<TrainingExample>
        {
            new("I love this product!", "positive", 1.0),
            new("This is terrible.", "negative", 1.0),
            new("It's okay, nothing special.", "neutral", 0.8),
            new("Best purchase ever!", "positive", 1.0),
            new("Waste of money.", "negative", 1.0),
        };

        var trainResult = await engine.TrainAdapterAsync(
            adapterId,
            trainingExamples,
            TrainingConfig.Fast());

        if (trainResult.IsFailure)
        {
            Console.WriteLine($"Training failed: {trainResult.Error}");
            return;
        }

        Console.WriteLine("✓ Training completed successfully\n");

        // 4. Generate with the adapted model
        Console.WriteLine("Generating sentiment analysis...");
        var generateResult = await engine.GenerateWithAdapterAsync(
            "Analyze: This restaurant has amazing food!",
            adapterId);

        if (generateResult.IsFailure)
        {
            Console.WriteLine($"Generation failed: {generateResult.Error}");
            return;
        }

        Console.WriteLine($"Response: {generateResult.Value}\n");

        // 5. Learn from user feedback
        Console.WriteLine("Applying user feedback...");
        var feedback = FeedbackSignal.UserCorrection("highly positive");
        var feedbackResult = await engine.LearnFromFeedbackAsync(
            "Analyze: This is the best thing ever!",
            "positive",
            feedback,
            adapterId);

        if (feedbackResult.IsFailure)
        {
            Console.WriteLine($"Feedback learning failed: {feedbackResult.Error}");
            return;
        }

        Console.WriteLine("✓ Learned from feedback\n");

        // 6. Create another adapter for a different task
        Console.WriteLine("Creating second adapter for 'translation' task...");
        var adapter2Result = await engine.CreateAdapterAsync(
            "translation",
            AdapterConfig.Default());

        if (adapter2Result.IsSuccess)
        {
            Console.WriteLine($"✓ Created adapter: {adapter2Result.Value}\n");

            // 7. Merge adapters
            Console.WriteLine("Merging adapters...");
            var mergeResult = await engine.MergeAdaptersAsync(
                new List<AdapterId> { adapterId, adapter2Result.Value },
                MergeStrategy.Average);

            if (mergeResult.IsSuccess)
            {
                Console.WriteLine("✓ Adapters merged successfully\n");
            }
            else
            {
                Console.WriteLine($"Merge failed: {mergeResult.Error}\n");
            }
        }

        // 8. Demonstrate continual learning without catastrophic forgetting
        Console.WriteLine("Demonstrating continual learning...");
        Console.WriteLine("Training on new examples while preserving old knowledge...");

        var additionalExamples = new List<TrainingExample>
        {
            new("The service was exceptional!", "positive", 1.0),
            new("Disappointing experience.", "negative", 1.0),
        };

        var continualResult = await engine.TrainAdapterAsync(
            adapterId,
            additionalExamples,
            TrainingConfig.Default() with { IncrementalUpdate = true });

        if (continualResult.IsSuccess)
        {
            Console.WriteLine("✓ Continual learning completed without forgetting\n");
        }

        Console.WriteLine("=== Example completed successfully ===");
    }
}
