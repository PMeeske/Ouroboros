// <copyright file="DistinctionLearningExample.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Examples.Examples;

using Ouroboros.Application.Learning;
using Ouroboros.Core.Learning;
using Ouroboros.Core.LawsOfForm;
using Ouroboros.Domain;

/// <summary>
/// Demonstrates Distinction Learning - a novel learning paradigm based on Laws of Form.
/// Shows the complete consciousness cycle: making distinctions, recognition (i = ⌐), and dissolution.
/// </summary>
public static class DistinctionLearningExample
{
    /// <summary>
    /// Runs the complete distinction learning demonstration.
    /// </summary>
    public static async Task RunAsync()
    {
        Console.WriteLine("=== Distinction Learning: A Novel Learning Paradigm ===\n");
        Console.WriteLine("Based on Spencer-Brown's Laws of Form");
        Console.WriteLine("Learning = Making Distinctions (∅ → ⌐)");
        Console.WriteLine("Understanding = Recognition (i = ⌐)");
        Console.WriteLine("Unlearning = Dissolution (⌐ → ∅)\n");

        // Initialize components
        var embeddingModel = new SimpleEmbeddingModel();
        var learner = new DistinctionLearner();
        var embeddingService = new DistinctionEmbeddingService(embeddingModel);

        Console.WriteLine("--- Example 1: Basic Distinction Learning Cycle ---\n");
        await BasicLearningCycleAsync(learner);

        Console.WriteLine("\n--- Example 2: Few-Shot Learning with Recognition ---\n");
        await FewShotLearningAsync(learner);

        Console.WriteLine("\n--- Example 3: Uncertainty Handling with Form.Imaginary ---\n");
        await UncertaintyHandlingAsync(learner);

        Console.WriteLine("\n--- Example 4: Principled Forgetting through Dissolution ---\n");
        await PrincipledForgettingAsync(learner);

        Console.WriteLine("\n--- Example 5: Dream Cycle Embeddings ---\n");
        await DreamCycleEmbeddingsAsync(embeddingService);

        Console.WriteLine("\n=== Distinction Learning Examples Complete ===");
    }

    private static async Task BasicLearningCycleAsync(IDistinctionLearner learner)
    {
        Console.WriteLine("Starting from void (∅) - pure potential, no distinctions");
        var state = DistinctionState.Void();
        Console.WriteLine($"Initial State: {state.Stage}, Certainty: {state.EpistemicCertainty}");

        var observation = Observation.WithCertainPrior(
            "The cat sat on the mat",
            "basic learning");

        // Stage 1: Distinction (∅ → ⌐)
        Console.WriteLine("\n1. Distinction Stage: Making initial distinctions");
        var result = await learner.UpdateFromDistinctionAsync(state, observation, DreamStage.Distinction);
        if (result.IsSuccess)
        {
            state = result.Value;
            Console.WriteLine($"   Distinctions made: {string.Join(", ", state.ActiveDistinctions)}");
            Console.WriteLine($"   Certainty: {state.EpistemicCertainty} (Mark = certain)");
        }

        // Stage 2: Subject Emerges (i)
        Console.WriteLine("\n2. Subject Emerges: Self-reference begins");
        result = await learner.UpdateFromDistinctionAsync(state, observation, DreamStage.SubjectEmerges);
        if (result.IsSuccess)
        {
            state = result.Value;
            Console.WriteLine($"   Certainty: {state.EpistemicCertainty} (Imaginary = self-referential)");
            Console.WriteLine($"   Active distinctions: {state.ActiveDistinctions.Count}");
        }

        // Stage 3: World Crystallizes
        Console.WriteLine("\n3. World Crystallizes: Subject/object separation");
        result = await learner.UpdateFromDistinctionAsync(state, observation, DreamStage.WorldCrystallizes);
        if (result.IsSuccess)
        {
            state = result.Value;
            Console.WriteLine($"   Total distinctions: {state.ActiveDistinctions.Count}");
            Console.WriteLine($"   Certainty: {state.EpistemicCertainty}");
        }

        // Stage 4: Recognition (i = ⌐)
        Console.WriteLine("\n4. Recognition: 'I AM the distinction' - the key insight");
        var recognitionResult = await learner.RecognizeAsync(state, observation.Content);
        if (recognitionResult.IsSuccess)
        {
            state = recognitionResult.Value;
            var recognitionDist = state.ActiveDistinctions.FirstOrDefault(d => d.StartsWith("I="));
            Console.WriteLine($"   Recognition distinction: {recognitionDist}");
            Console.WriteLine($"   Fitness: {state.FitnessScores.GetValueOrDefault(recognitionDist ?? string.Empty, 0):F2}");
            Console.WriteLine($"   Certainty: {state.EpistemicCertainty} (Mark = certain through insight)");
        }

        // Stage 5: Dissolution (⌐ → ∅)
        Console.WriteLine("\n5. Dissolution: Principled forgetting of low-fitness distinctions");
        var dissolutionResult = await learner.DissolveAsync(state, DissolutionStrategy.FitnessThreshold);
        if (dissolutionResult.IsSuccess)
        {
            state = dissolutionResult.Value;
            Console.WriteLine($"   Active distinctions remaining: {state.ActiveDistinctions.Count}");
            Console.WriteLine($"   Dissolved distinctions: {state.DissolvedDistinctions.Count}");
        }
    }

    private static async Task FewShotLearningAsync(IDistinctionLearner learner)
    {
        Console.WriteLine("Learning from only 3 examples (few-shot learning)");
        var state = DistinctionState.Void();

        var examples = new[]
        {
            "Pattern: red blue red blue",
            "Pattern: square circle square circle",
            "Pattern: up down up down"
        };

        foreach (var example in examples)
        {
            var observation = Observation.WithCertainPrior(example, "few-shot");
            
            // Make distinctions
            var result = await learner.UpdateFromDistinctionAsync(
                state, observation, DreamStage.Distinction);
            if (result.IsSuccess) state = result.Value;

            // Recognize patterns
            var recogResult = await learner.RecognizeAsync(state, example);
            if (recogResult.IsSuccess) state = recogResult.Value;
        }

        Console.WriteLine($"Learned {state.ActiveDistinctions.Count} distinctions from 3 examples");
        Console.WriteLine("Key distinctions:");
        foreach (var dist in state.ActiveDistinctions.Take(5))
        {
            var fitness = state.FitnessScores.GetValueOrDefault(dist, 0);
            Console.WriteLine($"  - '{dist}' (fitness: {fitness:F2})");
        }

        // Evaluate fitness
        var testObservations = new List<Observation>
        {
            Observation.WithCertainPrior("Pattern: left right left right", "test")
        };

        Console.WriteLine("\nEvaluating pattern recognition on new example:");
        var pattern = state.ActiveDistinctions.FirstOrDefault(d => d.Contains("pattern", StringComparison.OrdinalIgnoreCase));
        if (pattern != null)
        {
            var fitnessResult = await learner.EvaluateDistinctionFitnessAsync(pattern, testObservations);
            if (fitnessResult.IsSuccess)
            {
                Console.WriteLine($"Pattern distinction '{pattern}' fitness: {fitnessResult.Value:F2}");
            }
        }
    }

    private static async Task UncertaintyHandlingAsync(IDistinctionLearner learner)
    {
        Console.WriteLine("Form.Imaginary for epistemic uncertainty");
        var state = DistinctionState.Void();

        // Certain observation
        var certainObs = Observation.WithCertainPrior("The sky is blue", "certain");
        var result = await learner.UpdateFromDistinctionAsync(
            state, certainObs, DreamStage.WorldCrystallizes);
        if (result.IsSuccess)
        {
            state = result.Value;
            Console.WriteLine($"After certain observation:");
            Console.WriteLine($"  Certainty: {state.EpistemicCertainty} (should be Mark)");
        }

        // Uncertain observation
        var uncertainObs = Observation.WithUncertainPrior("Maybe it will rain", "uncertain");
        result = await learner.UpdateFromDistinctionAsync(
            state, uncertainObs, DreamStage.Questioning);
        if (result.IsSuccess)
        {
            state = result.Value;
            Console.WriteLine($"\nAfter uncertain observation:");
            Console.WriteLine($"  Certainty: {state.EpistemicCertainty} (should be Imaginary)");
            Console.WriteLine($"  IsImaginary: {state.EpistemicCertainty.IsImaginary()}");
        }

        // Recognition can resolve uncertainty
        var recogResult = await learner.RecognizeAsync(state, "pattern understood");
        if (recogResult.IsSuccess)
        {
            state = recogResult.Value;
            Console.WriteLine($"\nAfter recognition:");
            Console.WriteLine($"  Certainty: {state.EpistemicCertainty} (Mark - insight resolves uncertainty)");
        }
    }

    private static async Task PrincipledForgettingAsync(IDistinctionLearner learner)
    {
        Console.WriteLine("Dissolution prevents catastrophic forgetting");
        var state = DistinctionState.Void()
            .AddDistinction("important_concept", 0.9)
            .AddDistinction("less_important", 0.4)
            .AddDistinction("noise", 0.1);

        Console.WriteLine("Initial distinctions:");
        foreach (var dist in state.ActiveDistinctions)
        {
            Console.WriteLine($"  - {dist} (fitness: {state.FitnessScores[dist]:F2})");
        }

        // Dissolve by fitness threshold
        Console.WriteLine("\nApplying FitnessThreshold dissolution (threshold: 0.3):");
        var result = await learner.DissolveAsync(state, DissolutionStrategy.FitnessThreshold);
        if (result.IsSuccess)
        {
            state = result.Value;
            Console.WriteLine($"Active: {string.Join(", ", state.ActiveDistinctions)}");
            Console.WriteLine($"Dissolved: {string.Join(", ", state.DissolvedDistinctions)}");
            Console.WriteLine("\nHigh-fitness distinctions retained, low-fitness dissolved!");
        }

        // Complete dissolution
        Console.WriteLine("\nApplying Complete dissolution (tabula rasa):");
        result = await learner.DissolveAsync(state, DissolutionStrategy.Complete);
        if (result.IsSuccess)
        {
            state = result.Value;
            Console.WriteLine($"Active: {state.ActiveDistinctions.Count} (all dissolved)");
            Console.WriteLine($"Dissolved: {state.DissolvedDistinctions.Count} (returned to void)");
        }
    }

    private static async Task DreamCycleEmbeddingsAsync(DistinctionEmbeddingService service)
    {
        Console.WriteLine("Creating embeddings for consciousness dream cycle");
        
        var circumstance = "learning patterns";
        var result = await service.CreateDreamCycleEmbeddingAsync(circumstance);

        if (result.IsSuccess)
        {
            var embedding = result.Value;
            Console.WriteLine($"Circumstance: {embedding.Circumstance}");
            Console.WriteLine($"Stages embedded: {embedding.StageEmbeddings.Count}");
            Console.WriteLine($"Composite embedding dimension: {embedding.CompositeEmbedding.Length}");

            // Check Recognition stage
            var recognitionEmb = embedding.GetStageEmbedding(DreamStage.Recognition);
            if (recognitionEmb != null)
            {
                Console.WriteLine($"\nRecognition stage embedding present (dimension: {recognitionEmb.Length})");
                Console.WriteLine("Recognition has 2.5x weight in composite (moment of insight)");
            }

            // Test similarity
            var result2 = await service.CreateDreamCycleEmbeddingAsync("learning patterns");
            if (result2.IsSuccess)
            {
                var similarity = embedding.ComputeSimilarity(result2.Value);
                Console.WriteLine($"\nSimilarity with same circumstance: {similarity:F3} (should be ~1.0)");
            }

            var result3 = await service.CreateDreamCycleEmbeddingAsync("different topic");
            if (result3.IsSuccess)
            {
                var similarity = embedding.ComputeSimilarity(result3.Value);
                Console.WriteLine($"Similarity with different circumstance: {similarity:F3} (should be lower)");
            }
        }
    }
}

/// <summary>
/// Simple embedding model for examples (deterministic, no external dependencies).
/// </summary>
internal class SimpleEmbeddingModel : IEmbeddingModel
{
    public Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
    {
        // Create deterministic embeddings based on input
        var hash = input.GetHashCode();
        var random = new Random(hash);
        
        var embedding = new float[128]; // Small dimension for examples
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] = (float)(random.NextDouble() * 2 - 1);
        }

        // Normalize
        var norm = Math.Sqrt(embedding.Sum(x => x * x));
        if (norm > 0)
        {
            for (int i = 0; i < embedding.Length; i++)
            {
                embedding[i] /= (float)norm;
            }
        }

        return Task.FromResult(embedding);
    }
}
