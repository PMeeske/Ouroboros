// <copyright file="MetaLearningBenchmarks.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Ouroboros.Agent.MetaLearning;
using Ouroboros.Domain;
using Ouroboros.Domain.MetaLearning;

namespace Ouroboros.Benchmarks;

/// <summary>
/// Benchmarks for meta-learning performance.
/// Measures MAML, Reptile algorithms and few-shot adaptation speed.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(warmupCount: 2, iterationCount: 5)]
public class MetaLearningBenchmarks
{
    private MetaLearningEngine engine = null!;
    private List<TaskFamily> taskFamilies = null!;
    private MetaModel mamlModel = null!;
    private MetaModel reptileModel = null!;
    private List<Example> fewShotExamples = null!;

    /// <summary>
    /// Setup benchmark data and models.
    /// </summary>
    [GlobalSetup]
    public async Task Setup()
    {
        var embeddingModel = new MockEmbeddingModel();
        this.engine = new MetaLearningEngine(embeddingModel, seed: 42);
        this.taskFamilies = CreateBenchmarkTaskFamilies();
        this.fewShotExamples = CreateFewShotExamples();

        // Pre-train models for adaptation benchmarks
        var mamlConfig = MetaLearningConfig.DefaultMAML with { MetaIterations = 10 };
        var mamlResult = await this.engine.MetaTrainAsync(this.taskFamilies, mamlConfig, CancellationToken.None);
        this.mamlModel = mamlResult.Value;

        var reptileConfig = MetaLearningConfig.DefaultReptile with { MetaIterations = 10 };
        var reptileResult = await this.engine.MetaTrainAsync(this.taskFamilies, reptileConfig, CancellationToken.None);
        this.reptileModel = reptileResult.Value;
    }

    /// <summary>
    /// Benchmarks MAML meta-training performance.
    /// </summary>
    [Benchmark]
    public async Task MAMLMetaTraining()
    {
        var config = MetaLearningConfig.DefaultMAML with
        {
            MetaIterations = 10,
            TaskBatchSize = 2,
        };
        await this.engine.MetaTrainAsync(this.taskFamilies, config, CancellationToken.None);
    }

    /// <summary>
    /// Benchmarks Reptile meta-training performance.
    /// </summary>
    [Benchmark]
    public async Task ReptileMetaTraining()
    {
        var config = MetaLearningConfig.DefaultReptile with
        {
            MetaIterations = 20,
        };
        await this.engine.MetaTrainAsync(this.taskFamilies, config, CancellationToken.None);
    }

    /// <summary>
    /// Benchmarks few-shot adaptation with 3 examples.
    /// </summary>
    [Benchmark]
    public async Task FewShotAdaptation_3Examples()
    {
        var examples = this.fewShotExamples.Take(3).ToList();
        await this.engine.AdaptToTaskAsync(this.mamlModel, examples, adaptationSteps: 5, CancellationToken.None);
    }

    /// <summary>
    /// Benchmarks few-shot adaptation with 5 examples.
    /// </summary>
    [Benchmark]
    public async Task FewShotAdaptation_5Examples()
    {
        var examples = this.fewShotExamples.Take(5).ToList();
        await this.engine.AdaptToTaskAsync(this.mamlModel, examples, adaptationSteps: 5, CancellationToken.None);
    }

    /// <summary>
    /// Benchmarks few-shot adaptation with 10 examples.
    /// </summary>
    [Benchmark]
    public async Task FewShotAdaptation_10Examples()
    {
        await this.engine.AdaptToTaskAsync(
            this.mamlModel,
            this.fewShotExamples,
            adaptationSteps: 5,
            CancellationToken.None);
    }

    /// <summary>
    /// Benchmarks task embedding computation.
    /// </summary>
    [Benchmark]
    public async Task TaskEmbedding()
    {
        var task = this.taskFamilies[0].TrainingTasks[0];
        await this.engine.EmbedTaskAsync(task, this.mamlModel, CancellationToken.None);
    }

    /// <summary>
    /// Benchmarks task similarity computation.
    /// </summary>
    [Benchmark]
    public async Task TaskSimilarity()
    {
        var taskA = this.taskFamilies[0].TrainingTasks[0];
        var taskB = this.taskFamilies[0].TrainingTasks[1];
        await this.engine.ComputeTaskSimilarityAsync(taskA, taskB, this.mamlModel, CancellationToken.None);
    }

    /// <summary>
    /// Benchmarks MAML adaptation vs Reptile adaptation.
    /// </summary>
    [Benchmark]
    public async Task CompareMAMLvsReptileAdaptation()
    {
        var examples = this.fewShotExamples.Take(3).ToList();
        await this.engine.AdaptToTaskAsync(this.mamlModel, examples, adaptationSteps: 5, CancellationToken.None);
        await this.engine.AdaptToTaskAsync(this.reptileModel, examples, adaptationSteps: 5, CancellationToken.None);
    }

    private static List<TaskFamily> CreateBenchmarkTaskFamilies()
    {
        var tasks = new List<SynthesisTask>();

        // Create 10 diverse tasks for realistic benchmarking
        for (var i = 0; i < 10; i++)
        {
            var trainExamples = Enumerable.Range(0, 10)
                .Select(j => Example.Create($"input_{i}_{j}", $"output_{i}_{j}"))
                .ToList();

            var valExamples = Enumerable.Range(0, 3)
                .Select(j => Example.Create($"val_{i}_{j}", $"valOut_{i}_{j}"))
                .ToList();

            tasks.Add(SynthesisTask.Create($"Task{i}", "Benchmark", trainExamples, valExamples));
        }

        var family = TaskFamily.Create("Benchmark", tasks, validationSplit: 0.2);
        return new List<TaskFamily> { family };
    }

    private static List<Example> CreateFewShotExamples()
    {
        return Enumerable.Range(0, 10)
            .Select(i => Example.Create($"few_shot_{i}", $"output_{i}"))
            .ToList();
    }

    /// <summary>
    /// Mock embedding model for benchmarks.
    /// </summary>
    private class MockEmbeddingModel : IEmbeddingModel
    {
        public Task<float[]> CreateEmbeddingsAsync(string input, CancellationToken ct = default)
        {
            var hash = input.GetHashCode();
            var random = new Random(hash);
            var embedding = new float[128];

            for (var i = 0; i < embedding.Length; i++)
            {
                embedding[i] = (float)random.NextDouble();
            }

            var norm = Math.Sqrt(embedding.Sum(x => x * x));
            for (var i = 0; i < embedding.Length; i++)
            {
                embedding[i] /= (float)norm;
            }

            return Task.FromResult(embedding);
        }
    }
}
