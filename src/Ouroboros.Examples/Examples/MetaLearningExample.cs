// <copyright file="MetaLearningExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Agent.MetaLearning;
using Ouroboros.Domain;
using Ouroboros.Domain.MetaLearning;

namespace Ouroboros.Examples;

/// <summary>
/// Demonstrates meta-learning capabilities for fast task adaptation.
/// Shows MAML and Reptile algorithms with few-shot learning.
/// </summary>
public static class MetaLearningExample
{
    /// <summary>
    /// Demonstrates basic meta-learning workflow with MAML.
    /// </summary>
    public static async Task RunMAMLExampleAsync()
    {
        Console.WriteLine("=== MAML Meta-Learning Example ===\n");

        // Create mock embedding model
        var embeddingModel = new MockEmbeddingModel();

        // Create meta-learning engine
        var engine = new MetaLearningEngine(embeddingModel, seed: 42);

        // Create task families for meta-training
        var taskFamilies = CreateSampleTaskFamilies();

        Console.WriteLine($"Created {taskFamilies.Count} task families");
        Console.WriteLine($"Total tasks: {taskFamilies.Sum(f => f.TotalTasks)}");
        Console.WriteLine();

        // Configure MAML algorithm
        var config = MetaLearningConfig.DefaultMAML with
        {
            MetaIterations = 20,
            TaskBatchSize = 2,
            InnerSteps = 3,
        };

        Console.WriteLine($"Meta-training with {config.Algorithm} algorithm...");
        Console.WriteLine($"  Inner learning rate: {config.InnerLearningRate}");
        Console.WriteLine($"  Outer learning rate: {config.OuterLearningRate}");
        Console.WriteLine($"  Meta iterations: {config.MetaIterations}");
        Console.WriteLine();

        // Meta-train the model
        var metaTrainResult = await engine.MetaTrainAsync(taskFamilies, config, CancellationToken.None);

        if (metaTrainResult.IsFailure)
        {
            Console.WriteLine($"Meta-training failed: {metaTrainResult.Error}");
            return;
        }

        Console.WriteLine("Meta-training completed successfully!");
        Console.WriteLine($"Model ID: {metaTrainResult.Value.Id}");
        Console.WriteLine($"Trained at: {metaTrainResult.Value.TrainedAt}");
        Console.WriteLine();

        // Demonstrate few-shot adaptation to a new task
        Console.WriteLine("=== Few-Shot Task Adaptation ===\n");

        var fewShotExamples = new List<Example>
        {
            Example.Create("What is 2+2?", "4"),
            Example.Create("What is 5+3?", "8"),
            Example.Create("What is 10-7?", "3"),
        };

        Console.WriteLine($"Adapting to new task with {fewShotExamples.Count} examples...");

        var adaptResult = await engine.AdaptToTaskAsync(
            metaTrainResult.Value,
            fewShotExamples,
            adaptationSteps: 5,
            CancellationToken.None);

        if (adaptResult.IsSuccess)
        {
            Console.WriteLine("Adaptation successful!");
            Console.WriteLine($"  Adaptation steps: {adaptResult.Value.AdaptationSteps}");
            Console.WriteLine($"  Adaptation time: {adaptResult.Value.AdaptationTime.TotalMilliseconds:F2}ms");
            Console.WriteLine($"  Validation performance: {adaptResult.Value.ValidationPerformance:P2}");
            Console.WriteLine($"  Steps per second: {adaptResult.Value.StepsPerSecond:F2}");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates Reptile algorithm for meta-learning.
    /// </summary>
    public static async Task RunReptileExampleAsync()
    {
        Console.WriteLine("=== Reptile Meta-Learning Example ===\n");

        var embeddingModel = new MockEmbeddingModel();
        var engine = new MetaLearningEngine(embeddingModel, seed: 42);
        var taskFamilies = CreateSampleTaskFamilies();

        // Configure Reptile algorithm
        var config = MetaLearningConfig.DefaultReptile with
        {
            MetaIterations = 30,
            InnerSteps = 5,
        };

        Console.WriteLine($"Meta-training with {config.Algorithm} algorithm...");
        Console.WriteLine($"  Inner learning rate: {config.InnerLearningRate}");
        Console.WriteLine($"  Outer learning rate: {config.OuterLearningRate}");
        Console.WriteLine($"  Meta iterations: {config.MetaIterations}");
        Console.WriteLine();

        var metaTrainResult = await engine.MetaTrainAsync(taskFamilies, config, CancellationToken.None);

        if (metaTrainResult.IsSuccess)
        {
            Console.WriteLine("Meta-training completed successfully!");
            Console.WriteLine($"Model age: {metaTrainResult.Value.Age.TotalSeconds:F2}s");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Demonstrates task similarity computation.
    /// </summary>
    public static async Task RunTaskSimilarityExampleAsync()
    {
        Console.WriteLine("=== Task Similarity Example ===\n");

        var embeddingModel = new MockEmbeddingModel();
        var engine = new MetaLearningEngine(embeddingModel, seed: 42);
        var taskFamilies = CreateSampleTaskFamilies();

        // Quick meta-training
        var config = MetaLearningConfig.DefaultReptile with { MetaIterations = 10 };
        var metaTrainResult = await engine.MetaTrainAsync(taskFamilies, config, CancellationToken.None);

        if (metaTrainResult.IsFailure)
        {
            Console.WriteLine($"Meta-training failed: {metaTrainResult.Error}");
            return;
        }

        var metaModel = metaTrainResult.Value;
        var tasks = taskFamilies[0].TrainingTasks;

        Console.WriteLine("Computing task similarities...\n");

        for (var i = 0; i < Math.Min(3, tasks.Count); i++)
        {
            for (var j = i; j < Math.Min(3, tasks.Count); j++)
            {
                var similarityResult = await engine.ComputeTaskSimilarityAsync(
                    tasks[i],
                    tasks[j],
                    metaModel,
                    CancellationToken.None);

                if (similarityResult.IsSuccess)
                {
                    Console.WriteLine(
                        $"Similarity between '{tasks[i].Name}' and '{tasks[j].Name}': {similarityResult.Value:F4}");
                }
            }
        }

        Console.WriteLine();

        // Demonstrate task embedding
        Console.WriteLine("=== Task Embeddings ===\n");

        foreach (var task in tasks.Take(2))
        {
            var embeddingResult = await engine.EmbedTaskAsync(task, metaModel, CancellationToken.None);

            if (embeddingResult.IsSuccess)
            {
                var embedding = embeddingResult.Value;
                Console.WriteLine($"Task: {task.Name}");
                Console.WriteLine($"  Embedding dimension: {embedding.Dimension}");
                Console.WriteLine($"  Characteristics:");
                foreach (var (key, value) in embedding.Characteristics)
                {
                    Console.WriteLine($"    - {key}: {value}");
                }

                Console.WriteLine();
            }
        }
    }

    /// <summary>
    /// Runs all meta-learning examples.
    /// </summary>
    public static async Task RunAllExamplesAsync()
    {
        try
        {
            await RunMAMLExampleAsync();
            await RunReptileExampleAsync();
            await RunTaskSimilarityExampleAsync();

            Console.WriteLine("=== All Examples Completed ===");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    private static List<TaskFamily> CreateSampleTaskFamilies()
    {
        // Create tasks for arithmetic domain
        var arithmeticTasks = new List<SynthesisTask>
        {
            SynthesisTask.Create(
                "Addition",
                "Arithmetic",
                new List<Example>
                {
                    Example.Create("1+1", "2"),
                    Example.Create("2+3", "5"),
                    Example.Create("4+5", "9"),
                },
                new List<Example>
                {
                    Example.Create("6+2", "8"),
                }),
            SynthesisTask.Create(
                "Subtraction",
                "Arithmetic",
                new List<Example>
                {
                    Example.Create("5-2", "3"),
                    Example.Create("10-4", "6"),
                    Example.Create("8-3", "5"),
                },
                new List<Example>
                {
                    Example.Create("9-5", "4"),
                }),
            SynthesisTask.Create(
                "Multiplication",
                "Arithmetic",
                new List<Example>
                {
                    Example.Create("2*3", "6"),
                    Example.Create("4*5", "20"),
                    Example.Create("3*3", "9"),
                },
                new List<Example>
                {
                    Example.Create("5*2", "10"),
                }),
        };

        var arithmeticFamily = TaskFamily.Create("Arithmetic", arithmeticTasks, validationSplit: 0.3);

        // Create tasks for text transformation domain
        var textTasks = new List<SynthesisTask>
        {
            SynthesisTask.Create(
                "Uppercase",
                "TextTransform",
                new List<Example>
                {
                    Example.Create("hello", "HELLO"),
                    Example.Create("world", "WORLD"),
                    Example.Create("test", "TEST"),
                },
                new List<Example>
                {
                    Example.Create("example", "EXAMPLE"),
                }),
            SynthesisTask.Create(
                "Reverse",
                "TextTransform",
                new List<Example>
                {
                    Example.Create("abc", "cba"),
                    Example.Create("hello", "olleh"),
                    Example.Create("test", "tset"),
                },
                new List<Example>
                {
                    Example.Create("world", "dlrow"),
                }),
        };

        var textFamily = TaskFamily.Create("TextTransform", textTasks, validationSplit: 0.3);

        return new List<TaskFamily> { arithmeticFamily, textFamily };
    }

    /// <summary>
    /// Simple mock embedding model for examples.
    /// </summary>
    private class MockEmbeddingModel : IEmbeddingModel
    {
        public Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
        {
            // Create deterministic embedding from input hash
            var hash = input.GetHashCode();
            var random = new Random(hash);
            var embedding = new float[128];

            for (var i = 0; i < embedding.Length; i++)
            {
                embedding[i] = (float)random.NextDouble();
            }

            // Normalize
            var norm = Math.Sqrt(embedding.Sum(x => x * x));
            for (var i = 0; i < embedding.Length; i++)
            {
                embedding[i] /= (float)norm;
            }

            return Task.FromResult(embedding);
        }
    }
}
