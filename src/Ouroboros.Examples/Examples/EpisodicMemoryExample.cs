// <copyright file="EpisodicMemoryExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Examples;

using System.Collections.Immutable;
using Ouroboros.Domain;
using Ouroboros.Domain.Vectors;
using Ouroboros.Pipeline.Branches;
using Ouroboros.Pipeline.Memory;
using LangChain.DocumentLoaders;

/// <summary>
/// Demonstrates the episodic memory system for experience-based learning.
/// Shows how to store, retrieve, and learn from past pipeline executions.
/// </summary>
public static class EpisodicMemoryExample
{
    /// <summary>
    /// Simple in-memory embedding model for demonstration purposes.
    /// In production, use a real embedding model like Ollama or OpenAI.
    /// </summary>
    private class SimpleEmbeddingModel : IEmbeddingModel
    {
        public Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
        {
            // Create a simple hash-based embedding
            var hash = input.GetHashCode();
            var embedding = new float[768];
            var random = new Random(hash);
            for (int i = 0; i < embedding.Length; i++)
            {
                embedding[i] = (float)random.NextDouble();
            }

            return Task.FromResult(embedding);
        }
    }

    /// <summary>
    /// Demonstrates basic episodic memory usage: storing and retrieving episodes.
    /// </summary>
    public static async Task DemonstrateBasicUsage()
    {
        Console.WriteLine("=== Episodic Memory System Example ===\n");
        Console.WriteLine("This example demonstrates storing and retrieving episodes");
        Console.WriteLine("with semantic search capabilities.\n");

        // Setup
        Console.WriteLine("1. SETUP: Initializing episodic memory system...\n");

        var embeddingModel = new SimpleEmbeddingModel();
        var memory = new EpisodicMemoryEngine(
            "http://localhost:6333",
            embeddingModel,
            "demo_episodes");

        Console.WriteLine("✓ Memory system initialized (requires Qdrant running on localhost:6333)\n");

        // Store some example episodes
        Console.WriteLine("2. STORING EPISODES: Adding past experiences...\n");

        await StoreSuccessfulEpisode(memory, "Implement authentication", 0.95);
        await StoreSuccessfulEpisode(memory, "Add user registration", 0.85);
        await StoreFailedEpisode(memory, "Deploy to production", 0.3);

        Console.WriteLine("✓ Stored 3 episodes\n");

        // Retrieve similar episodes
        Console.WriteLine("3. RETRIEVAL: Finding similar past experiences...\n");

        var query = "How to implement login functionality";
        Console.WriteLine($"Query: \"{query}\"\n");

        var result = await memory.RetrieveSimilarEpisodesAsync(query, topK: 5, minSimilarity: 0.5);

        if (result.IsSuccess)
        {
            Console.WriteLine($"Found {result.Value.Count} similar episodes:");
            foreach (var episode in result.Value)
            {
                Console.WriteLine($"  • Goal: {episode.Goal}");
                Console.WriteLine($"    Success Score: {episode.SuccessScore:F2}");
                Console.WriteLine($"    Lessons: {episode.LessonsLearned.Count} insights");
                Console.WriteLine();
            }
        }
        else
        {
            Console.WriteLine($"❌ Retrieval failed: {result.Error}\n");
        }

        // Memory consolidation
        Console.WriteLine("4. CONSOLIDATION: Pruning low-value memories...\n");

        var consolidationResult = await memory.ConsolidateMemoriesAsync(
            TimeSpan.FromHours(1),
            ConsolidationStrategy.Prune);

        if (consolidationResult.IsSuccess)
        {
            Console.WriteLine("✓ Successfully consolidated memories\n");
        }
        else
        {
            Console.WriteLine($"⚠ Consolidation had issues: {consolidationResult.Error}\n");
        }

        Console.WriteLine("Example complete!");
    }

    /// <summary>
    /// Demonstrates experience-based planning with episodic memory.
    /// </summary>
    public static async Task DemonstratePlanning()
    {
        Console.WriteLine("\n=== Experience-Based Planning Example ===\n");

        var embeddingModel = new SimpleEmbeddingModel();
        var memory = new EpisodicMemoryEngine(
            "http://localhost:6333",
            embeddingModel,
            "demo_episodes");

        // Retrieve relevant episodes
        var goal = "Deploy microservices architecture";
        Console.WriteLine($"Goal: {goal}\n");

        var episodesResult = await memory.RetrieveSimilarEpisodesAsync(goal, topK: 3);

        if (episodesResult.IsSuccess)
        {
            Console.WriteLine($"Retrieved {episodesResult.Value.Count} relevant episodes\n");

            // Generate experience-informed plan
            var planResult = await memory.PlanWithExperienceAsync(goal, episodesResult.Value);

            if (planResult.IsSuccess)
            {
                Console.WriteLine("Generated Plan:");
                Console.WriteLine(planResult.Value.Description);
                Console.WriteLine($"\nActions: {planResult.Value.Actions.Count}");
                foreach (var action in planResult.Value.Actions)
                {
                    Console.WriteLine($"  • {action.ToMeTTaAtom()}");
                }
            }
            else
            {
                Console.WriteLine($"❌ Planning failed: {planResult.Error}");
            }
        }

        Console.WriteLine("\nPlanning example complete!");
    }

    /// <summary>
    /// Demonstrates integrating episodic memory with pipeline steps.
    /// </summary>
    public static async Task DemonstrateIntegration()
    {
        Console.WriteLine("\n=== Pipeline Integration Example ===\n");

        var embeddingModel = new SimpleEmbeddingModel();
        var memory = new EpisodicMemoryEngine(
            "http://localhost:6333",
            embeddingModel,
            "demo_episodes");

        // Create a simple pipeline step
        Step<PipelineBranch, PipelineBranch> simpleStep = async branch =>
        {
            Console.WriteLine($"  Processing branch: {branch.Name}");
            return branch;
        };

        // Wrap it with episodic memory
        var enhancedStep = simpleStep.WithEpisodicMemory(
            memory,
            EpisodicMemoryExtensions.ExtractGoalFromBranch,
            topK: 3);

        // Execute the step
        var store = new TrackedVectorStore();
        var dataSource = DataSource.FromPath(Environment.CurrentDirectory);
        var branch = new PipelineBranch("demo", store, dataSource);

        Console.WriteLine("Executing pipeline with episodic memory...\n");
        var result = await enhancedStep(branch);

        Console.WriteLine("\n✓ Step executed with automatic memory storage");
        Console.WriteLine("Integration example complete!");
    }

    private static async Task StoreSuccessfulEpisode(
        IEpisodicMemoryEngine memory,
        string goal,
        double successScore)
    {
        var store = new TrackedVectorStore();
        var dataSource = DataSource.FromPath(Environment.CurrentDirectory);
        var branch = new PipelineBranch("demo", store, dataSource);

        var context = ExecutionContext.WithGoal(goal);
        var outcome = Outcome.Successful("Completed successfully", TimeSpan.FromMinutes(5));
        var metadata = ImmutableDictionary<string, object>.Empty
            .Add("example", true)
            .Add("success_score", successScore);

        var result = await memory.StoreEpisodeAsync(branch, context, outcome, metadata);

        if (result.IsSuccess)
        {
            Console.WriteLine($"  ✓ Stored: {goal} (score: {successScore:F2})");
        }
        else
        {
            Console.WriteLine($"  ❌ Failed to store: {result.Error}");
        }
    }

    private static async Task StoreFailedEpisode(
        IEpisodicMemoryEngine memory,
        string goal,
        double successScore)
    {
        var store = new TrackedVectorStore();
        var dataSource = DataSource.FromPath(Environment.CurrentDirectory);
        var branch = new PipelineBranch("demo", store, dataSource);

        var context = ExecutionContext.WithGoal(goal);
        var errors = ImmutableList<string>.Empty
            .Add("Configuration error")
            .Add("Missing dependencies");
        var outcome = Outcome.Failed("Deployment failed", TimeSpan.FromMinutes(10), errors);
        var metadata = ImmutableDictionary<string, object>.Empty
            .Add("example", true)
            .Add("success_score", successScore);

        var result = await memory.StoreEpisodeAsync(branch, context, outcome, metadata);

        if (result.IsSuccess)
        {
            Console.WriteLine($"  ✓ Stored: {goal} (score: {successScore:F2}, with errors)");
        }
        else
        {
            Console.WriteLine($"  ❌ Failed to store: {result.Error}");
        }
    }

    /// <summary>
    /// Main entry point to run all examples.
    /// </summary>
    public static async Task RunAllExamples()
    {
        try
        {
            await DemonstrateBasicUsage();
            Console.WriteLine("\n" + new string('=', 60) + "\n");
            await DemonstratePlanning();
            Console.WriteLine("\n" + new string('=', 60) + "\n");
            await DemonstrateIntegration();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ Error running examples: {ex.Message}");
            Console.WriteLine("\nNote: These examples require Qdrant to be running on localhost:6333");
            Console.WriteLine("Start Qdrant with: docker run -p 6333:6333 qdrant/qdrant");
        }
    }
}
