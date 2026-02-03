// <copyright file="DistinctionLearningBenchmark.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Ouroboros.Application.Learning;
using Ouroboros.Core.DistinctionLearning;
using Ouroboros.Core.Learning;
using Ouroboros.Core.LawsOfForm;
using Ouroboros.Core.Monads;
using Ouroboros.Domain;
using Ouroboros.Domain.DistinctionLearning;

namespace Ouroboros.Benchmarks.Benchmarks;

/// <summary>
/// Benchmarks for Distinction Learning framework.
/// Tests the novel learning paradigm based on Laws of Form:
/// - Learning = Making distinctions (∅ → ⌐)
/// - Understanding = Recognition (i = ⌐)
/// - Unlearning = Dissolution
/// - Uncertainty = Imaginary state
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 5)]
public class DistinctionLearningBenchmark
{
    private IDistinctionLearner learner = null!;
    private DistinctionEmbeddingService embeddingService = null!;
    private List<Observation> trainingObservations = null!;
    private List<Observation> testObservations = null!;
    private DistinctionState initialState = null!;

    /// <summary>
    /// Setup benchmark data and components.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var embeddingModel = new MockEmbeddingModel();
        var mockStorage = new MockDistinctionWeightStorage();
        this.learner = new DistinctionLearner(mockStorage);
        this.embeddingService = new DistinctionEmbeddingService(embeddingModel);
        
        this.initialState = DistinctionState.Void();
        this.trainingObservations = this.CreateTrainingObservations();
        this.testObservations = this.CreateTestObservations();
    }

    /// <summary>
    /// ARC_DistinctionLearning: Tests if Recognition stage helps solve novel ARC-style pattern tasks.
    /// Evaluates pattern recognition and generalization through the dream cycle.
    /// </summary>
    [Benchmark]
    public async Task<double> ARC_DistinctionLearning()
    {
        var state = this.initialState;
        var correctPredictions = 0;
        var totalTests = 0;

        // Training phase: walk through dream cycle with pattern observations
        var patternObservations = this.CreateARCPatternObservations();
        
        foreach (var observation in patternObservations)
        {
            // Progress through dream stages - use string stage names
            var stages = new[] { "Distinction", "Recognition", "WorldCrystallizes", "Dissolution" };
            
            foreach (var stage in stages)
            {
                var result = await this.learner.UpdateFromDistinctionAsync(state, observation, stage);
                if (result.IsSuccess)
                {
                    state = result.Value;
                }

                // Apply recognition at Recognition stage
                if (stage == "Recognition")
                {
                    var recognitionResult = await this.learner.RecognizeAsync(state, observation.Content);
                    if (recognitionResult.IsSuccess)
                    {
                        state = recognitionResult.Value;
                    }
                }

                // Apply dissolution at Dissolution stage
                if (stage == "Dissolution")
                {
                    var dissolutionResult = await this.learner.DissolveAsync(state, DissolutionStrategy.FitnessThreshold);
                    // DissolveAsync returns Result<Unit, string>, not Result<DistinctionState, string>
                    // So we don't update state from dissolution result
                }
            }
        }

        // Test phase: evaluate on novel patterns
        foreach (var testObs in this.CreateARCTestObservations())
        {
            // Check if learned distinctions help identify the pattern
            var hasRelevantDistinctions = state.ActiveDistinctionNames.Any(d => 
                testObs.Content.Contains(d, StringComparison.OrdinalIgnoreCase));
            
            if (hasRelevantDistinctions)
            {
                correctPredictions++;
            }
            totalTests++;
        }

        return totalTests > 0 ? (double)correctPredictions / totalTests : 0.0;
    }

    /// <summary>
    /// FewShot_DistinctionLearning: Tests learning with only 3-5 examples.
    /// Evaluates how quickly distinctions can be formed from minimal data.
    /// </summary>
    [Benchmark]
    public async Task<double> FewShot_DistinctionLearning()
    {
        var state = this.initialState;
        var fewShotExamples = this.trainingObservations.Take(5).ToList();

        // Learn from only 5 examples through one dream cycle
        foreach (var observation in fewShotExamples)
        {
            var result = await this.learner.UpdateFromDistinctionAsync(
                state, 
                observation, 
                "Distinction");
            
            if (result.IsSuccess)
            {
                state = result.Value;
            }
        }

        // Evaluate fitness of learned distinctions
        var totalFitness = 0.0;
        var distinctionCount = 0;

        foreach (var distinction in state.ActiveDistinctionNames)
        {
            var fitnessResult = await this.learner.EvaluateDistinctionFitnessAsync(
                distinction, 
                fewShotExamples);
            
            if (fitnessResult.IsSuccess)
            {
                totalFitness += fitnessResult.Value;
                distinctionCount++;
            }
        }

        return distinctionCount > 0 ? totalFitness / distinctionCount : 0.0;
    }

    /// <summary>
    /// Uncertainty_Calibration: Tests if Form.Imaginary correctly identifies uncertain cases.
    /// Evaluates epistemic uncertainty tracking through the dream cycle.
    /// </summary>
    [Benchmark]
    public async Task<double> Uncertainty_Calibration()
    {
        var state = this.initialState;
        var uncertainObservations = this.CreateUncertainObservations();
        var certainObservations = this.CreateCertainObservations();
        
        var correctUncertainty = 0;
        var totalAssessments = 0;

        // Process uncertain observations - should result in Imaginary form
        foreach (var observation in uncertainObservations)
        {
            var result = await this.learner.UpdateFromDistinctionAsync(
                state, 
                observation, 
                "Questioning"); // Questioning creates uncertainty
            
            if (result.IsSuccess)
            {
                state = result.Value;
                if (state.EpistemicCertainty.IsImaginary())
                {
                    correctUncertainty++;
                }
                totalAssessments++;
            }
        }

        // Process certain observations - should result in Mark or Void
        foreach (var observation in certainObservations)
        {
            var result = await this.learner.UpdateFromDistinctionAsync(
                state, 
                observation, 
                "WorldCrystallizes"); // Crystallizing creates certainty
            
            if (result.IsSuccess)
            {
                state = result.Value;
                if (state.EpistemicCertainty.IsCertain())
                {
                    correctUncertainty++;
                }
                totalAssessments++;
            }
        }

        return totalAssessments > 0 ? (double)correctUncertainty / totalAssessments : 0.0;
    }

    /// <summary>
    /// CatastrophicForgetting_Prevention: Tests if Dissolution enables graceful forgetting across sequential tasks.
    /// Evaluates selective retention vs catastrophic forgetting.
    /// </summary>
    [Benchmark]
    public async Task<double> CatastrophicForgetting_Prevention()
    {
        var state = this.initialState;
        
        // Learn Task A
        var taskAObservations = this.CreateTaskAObservations();
        foreach (var obs in taskAObservations)
        {
            var result = await this.learner.UpdateFromDistinctionAsync(
                state, obs, "WorldCrystallizes");
            if (result.IsSuccess)
            {
                state = result.Value;
            }
        }

        var taskADistinctions = new HashSet<string>(state.ActiveDistinctionNames);

        // Apply selective dissolution before Task B
        var dissolutionResult = await this.learner.DissolveAsync(
            state, 
            DissolutionStrategy.FitnessThreshold);
        
        // DissolveAsync doesn't return updated state, just success/failure

        // Learn Task B
        var taskBObservations = this.CreateTaskBObservations();
        foreach (var obs in taskBObservations)
        {
            var result = await this.learner.UpdateFromDistinctionAsync(
                state, obs, "WorldCrystallizes");
            if (result.IsSuccess)
            {
                state = result.Value;
            }
        }

        // Check retention of important Task A distinctions
        var retainedTaskA = state.ActiveDistinctionNames
            .Count(d => taskADistinctions.Contains(d));
        
        var retentionRate = taskADistinctions.Count > 0 
            ? (double)retainedTaskA / taskADistinctions.Count 
            : 0.0;

        // Good performance = some retention (not zero) but not all (allowing learning)
        // Optimal range: 0.3 to 0.7 (selective retention)
        return Math.Abs(retentionRate - 0.5); // Distance from ideal 50% retention
    }

    /// <summary>
    /// SelfCorrection_Rate: Tests if Recognition enables correcting incorrect assumptions.
    /// Evaluates the insight moment (i = ⌐) for self-correction.
    /// </summary>
    [Benchmark]
    public async Task<double> SelfCorrection_Rate()
    {
        var state = this.initialState;
        
        // Introduce incorrect distinction
        state = state.AddDistinction("incorrect_assumption", 0.8);

        // Provide contradictory observations
        var correctingObservations = this.CreateCorrectingObservations();
        
        var correctionsMade = 0;
        var totalAttempts = 0;

        foreach (var observation in correctingObservations)
        {
            // First questioning (doubt)
            var questionResult = await this.learner.UpdateFromDistinctionAsync(
                state, observation, "Questioning");
            
            if (questionResult.IsSuccess)
            {
                state = questionResult.Value;
            }

            // Then recognition (insight)
            var recognitionResult = await this.learner.RecognizeAsync(
                state, observation.Content);
            
            if (recognitionResult.IsSuccess)
            {
                state = recognitionResult.Value;
                
                // Check if incorrect assumption was dissolved or fitness reduced
                var incorrectFitness = state.FitnessScores.GetValueOrDefault("incorrect_assumption", 0.0);
                if (incorrectFitness < 0.5 || !state.ActiveDistinctionNames.Contains("incorrect_assumption"))
                {
                    correctionsMade++;
                }
            }
            
            totalAttempts++;
        }

        return totalAttempts > 0 ? (double)correctionsMade / totalAttempts : 0.0;
    }

    private List<Observation> CreateTrainingObservations()
    {
        return new List<Observation>
        {
            Observation.WithCertainPrior("The cat sat on the mat", "training"),
            Observation.WithCertainPrior("The dog ran in the park", "training"),
            Observation.WithCertainPrior("The bird flew over the tree", "training"),
            Observation.WithCertainPrior("The fish swam in the pond", "training"),
            Observation.WithCertainPrior("The horse galloped across the field", "training"),
        };
    }

    private List<Observation> CreateTestObservations()
    {
        return new List<Observation>
        {
            Observation.WithCertainPrior("The mouse ran under the chair", "test"),
            Observation.WithCertainPrior("The rabbit jumped over the fence", "test"),
        };
    }

    private List<Observation> CreateARCPatternObservations()
    {
        return new List<Observation>
        {
            Observation.WithCertainPrior("pattern: red blue red blue", "arc_pattern"),
            Observation.WithCertainPrior("pattern: square circle square circle", "arc_pattern"),
            Observation.WithCertainPrior("pattern: up down up down", "arc_pattern"),
        };
    }

    private List<Observation> CreateARCTestObservations()
    {
        return new List<Observation>
        {
            Observation.WithCertainPrior("pattern: left right left right", "arc_test"),
            Observation.WithCertainPrior("pattern: big small big small", "arc_test"),
        };
    }

    private List<Observation> CreateUncertainObservations()
    {
        return new List<Observation>
        {
            Observation.WithUncertainPrior("Maybe it will rain tomorrow", "uncertain"),
            Observation.WithUncertainPrior("I think this might be correct", "uncertain"),
        };
    }

    private List<Observation> CreateCertainObservations()
    {
        return new List<Observation>
        {
            Observation.WithCertainPrior("The sky is blue", "certain"),
            Observation.WithCertainPrior("Water freezes at zero degrees", "certain"),
        };
    }

    private List<Observation> CreateTaskAObservations()
    {
        return new List<Observation>
        {
            Observation.WithCertainPrior("TaskA: apple banana cherry", "taskA"),
            Observation.WithCertainPrior("TaskA: fruits are sweet", "taskA"),
        };
    }

    private List<Observation> CreateTaskBObservations()
    {
        return new List<Observation>
        {
            Observation.WithCertainPrior("TaskB: carrot broccoli spinach", "taskB"),
            Observation.WithCertainPrior("TaskB: vegetables are nutritious", "taskB"),
        };
    }

    private List<Observation> CreateCorrectingObservations()
    {
        return new List<Observation>
        {
            Observation.WithCertainPrior("This contradicts the incorrect_assumption", "correction"),
            Observation.WithCertainPrior("Actually the correct understanding is different", "correction"),
        };
    }
}

/// <summary>
/// Mock embedding model for benchmarking (avoids external dependencies).
/// </summary>
internal class MockEmbeddingModel : IEmbeddingModel
{
    private const int StandardEmbeddingDimension = 384;
    private readonly Random random = new(42);

    public Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
    {
        // Create deterministic but unique embeddings based on input hash
        var hash = input.GetHashCode();
        var localRandom = new Random(hash);
        
        var embedding = new float[StandardEmbeddingDimension];
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] = (float)(localRandom.NextDouble() * 2 - 1); // [-1, 1]
        }

        // Normalize
        var norm = Math.Sqrt(embedding.Sum(x => x * x));
        for (int i = 0; i < embedding.Length; i++)
        {
            embedding[i] /= (float)norm;
        }

        return Task.FromResult(embedding);
    }
}

/// <summary>
/// Mock distinction weight storage for benchmarking (avoids I/O).
/// </summary>
internal class MockDistinctionWeightStorage : IDistinctionWeightStorage
{
    private readonly Dictionary<string, byte[]> storage = new();
    private readonly Dictionary<string, DistinctionWeightMetadata> metadata = new();

    public Task<Result<string, string>> StoreWeightsAsync(
        string id,
        byte[] weights,
        DistinctionWeightMetadata metadata,
        CancellationToken ct = default)
    {
        storage[id] = weights;
        this.metadata[id] = metadata;
        return Task.FromResult(Result<string, string>.Success($"mock://{id}"));
    }

    public Task<Result<byte[], string>> LoadWeightsAsync(
        string id,
        CancellationToken ct = default)
    {
        if (storage.TryGetValue(id, out var weights))
        {
            return Task.FromResult(Result<byte[], string>.Success(weights));
        }

        return Task.FromResult(Result<byte[], string>.Failure($"Weight {id} not found"));
    }

    public Task<Result<List<DistinctionWeightMetadata>, string>> ListWeightsAsync(
        CancellationToken ct = default)
    {
        return Task.FromResult(Result<List<DistinctionWeightMetadata>, string>.Success(
            metadata.Values.ToList()));
    }

    public Task<Result<Unit, string>> DissolveWeightsAsync(
        string path,
        CancellationToken ct = default)
    {
        // Mark as dissolved (no-op for mock)
        return Task.FromResult(Result<Unit, string>.Success(Unit.Value));
    }

    public Task<Result<long, string>> GetTotalStorageSizeAsync(
        CancellationToken ct = default)
    {
        var totalSize = storage.Values.Sum(w => w.Length);
        return Task.FromResult(Result<long, string>.Success((long)totalSize));
    }
}
