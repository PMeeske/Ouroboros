// ==========================================================
// Orchestration Experiment Tests
// Tests for A/B testing framework
// ==========================================================

using FluentAssertions;
using Ouroboros.Agent;
using Ouroboros.Agent.MetaAI;
using Ouroboros.Tools;
using Xunit;

namespace Ouroboros.Tests;

/// <summary>
/// Tests for the OrchestrationExperiment A/B testing framework.
/// </summary>
[Trait("Category", "Unit")]
public class OrchestrationExperimentTests
{
    [Fact]
    public async Task RunExperimentAsync_WithEmptyExperimentId_ShouldReturnFailure()
    {
        // Arrange
        var experiment = new OrchestrationExperiment();
        var variants = CreateMockOrchestrators(2);
        var prompts = new List<string> { "test prompt" };

        // Act
        var result = await experiment.RunExperimentAsync("", variants, prompts);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task RunExperimentAsync_WithSingleVariant_ShouldReturnFailure()
    {
        // Arrange
        var experiment = new OrchestrationExperiment();
        var variants = CreateMockOrchestrators(1);
        var prompts = new List<string> { "test prompt" };

        // Act
        var result = await experiment.RunExperimentAsync("test-exp", variants, prompts);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task RunExperimentAsync_WithNoPrompts_ShouldReturnFailure()
    {
        // Arrange
        var experiment = new OrchestrationExperiment();
        var variants = CreateMockOrchestrators(2);
        var prompts = new List<string>();

        // Act
        var result = await experiment.RunExperimentAsync("test-exp", variants, prompts);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task RunExperimentAsync_WithValidInputs_ShouldReturnSuccess()
    {
        // Arrange
        var experiment = new OrchestrationExperiment();
        var variants = CreateMockOrchestrators(2);
        var prompts = new List<string> { "test prompt 1", "test prompt 2" };

        // Act
        var result = await experiment.RunExperimentAsync("test-exp", variants, prompts);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ExperimentId.Should().Be("test-exp");
        result.Value.VariantResults.Should().HaveCount(2);
        result.Value.Status.Should().Be(ExperimentStatus.Completed);
    }

    [Fact]
    public async Task RunExperimentAsync_ShouldCalculateMetricsForEachVariant()
    {
        // Arrange
        var experiment = new OrchestrationExperiment();
        var variants = CreateMockOrchestrators(2);
        var prompts = new List<string> { "prompt 1", "prompt 2", "prompt 3" };

        // Act
        var result = await experiment.RunExperimentAsync("metrics-exp", variants, prompts);

        // Assert
        result.IsSuccess.Should().BeTrue();
        foreach (var variant in result.Value.VariantResults)
        {
            variant.Metrics.TotalPrompts.Should().Be(3);
            variant.Metrics.SuccessRate.Should().BeGreaterThanOrEqualTo(0);
            variant.Metrics.SuccessRate.Should().BeLessThanOrEqualTo(1);
            variant.Metrics.AverageLatencyMs.Should().BeGreaterThanOrEqualTo(0);
        }
    }

    [Fact]
    public async Task RunExperimentAsync_ShouldIncludeStatisticalAnalysis()
    {
        // Arrange
        var experiment = new OrchestrationExperiment();
        var variants = CreateMockOrchestrators(2);
        var prompts = new List<string> { "prompt 1", "prompt 2" };

        // Act
        var result = await experiment.RunExperimentAsync("stats-exp", variants, prompts);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Analysis.Should().NotBeNull();
        result.Value.Analysis!.Interpretation.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RunExperimentAsync_WithCancellation_ShouldReturnFailure()
    {
        // Arrange
        var experiment = new OrchestrationExperiment();
        var slowVariants = CreateSlowMockOrchestrators(2, delayMs: 1000);
        var prompts = new List<string> { "prompt" };
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(50); // Cancel quickly

        // Act
        var result = await experiment.RunExperimentAsync("cancel-exp", slowVariants, prompts, cts.Token);

        // Assert
        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task RunExperimentAsync_DuplicateExperimentId_ShouldReturnFailure()
    {
        // Arrange
        var experiment = new OrchestrationExperiment();
        var slowVariants = CreateSlowMockOrchestrators(2, delayMs: 500);
        var prompts = new List<string> { "prompt" };

        // Start first experiment (don't await)
        var task1 = experiment.RunExperimentAsync("dup-exp", slowVariants, prompts);

        // Give it time to register
        await Task.Delay(50);

        // Try to start duplicate
        var result2 = await experiment.RunExperimentAsync("dup-exp", slowVariants, prompts);

        // Assert
        result2.IsSuccess.Should().BeFalse();

        // Clean up
        await task1;
    }

    [Fact]
    public async Task GetExperimentResult_AfterCompletion_ShouldReturnResult()
    {
        // Arrange
        var experiment = new OrchestrationExperiment();
        var variants = CreateMockOrchestrators(2);
        var prompts = new List<string> { "prompt" };

        await experiment.RunExperimentAsync("get-exp", variants, prompts);

        // Act
        var result = experiment.GetExperimentResult("get-exp");

        // Assert
        result.HasValue.Should().BeTrue();
        result.Value!.ExperimentId.Should().Be("get-exp");
    }

    [Fact]
    public void GetExperimentResult_NonExistent_ShouldReturnNone()
    {
        // Arrange
        var experiment = new OrchestrationExperiment();

        // Act
        var result = experiment.GetExperimentResult("non-existent");

        // Assert
        result.HasValue.Should().BeFalse();
    }

    [Fact]
    public async Task IsExperimentRunning_WhileRunning_ShouldReturnTrue()
    {
        // Arrange
        var experiment = new OrchestrationExperiment();
        var slowVariants = CreateSlowMockOrchestrators(2, delayMs: 500);
        var prompts = new List<string> { "prompt" };

        // Start experiment without awaiting
        var task = experiment.RunExperimentAsync("running-exp", slowVariants, prompts);

        // Give it time to start
        await Task.Delay(50);

        // Act
        var isRunning = experiment.IsExperimentRunning("running-exp");

        // Assert
        isRunning.Should().BeTrue();

        // Clean up
        await task;
    }

    [Fact]
    public async Task IsExperimentRunning_AfterCompletion_ShouldReturnFalse()
    {
        // Arrange
        var experiment = new OrchestrationExperiment();
        var variants = CreateMockOrchestrators(2);
        var prompts = new List<string> { "prompt" };

        await experiment.RunExperimentAsync("done-exp", variants, prompts);

        // Act
        var isRunning = experiment.IsExperimentRunning("done-exp");

        // Assert
        isRunning.Should().BeFalse();
    }

    [Fact]
    public async Task CompletedExperiments_ShouldContainFinishedExperiments()
    {
        // Arrange
        var experiment = new OrchestrationExperiment();
        var variants = CreateMockOrchestrators(2);
        var prompts = new List<string> { "prompt" };

        await experiment.RunExperimentAsync("exp1", variants, prompts);
        await experiment.RunExperimentAsync("exp2", variants, prompts);

        // Act
        var completed = experiment.CompletedExperiments;

        // Assert
        completed.Should().ContainKey("exp1");
        completed.Should().ContainKey("exp2");
    }

    [Fact]
    public async Task ExperimentResult_Duration_ShouldBeCalculated()
    {
        // Arrange
        var experiment = new OrchestrationExperiment();
        var variants = CreateMockOrchestrators(2);
        var prompts = new List<string> { "prompt" };

        // Act
        var result = await experiment.RunExperimentAsync("duration-exp", variants, prompts);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public void VariantMetrics_ShouldHaveCorrectProperties()
    {
        // Arrange
        var metrics = new VariantMetrics(
            SuccessRate: 0.95,
            AverageLatencyMs: 100,
            P95LatencyMs: 150,
            P99LatencyMs: 200,
            AverageConfidence: 0.85,
            TotalPrompts: 100,
            SuccessfulPrompts: 95);

        // Assert
        metrics.SuccessRate.Should().Be(0.95);
        metrics.AverageLatencyMs.Should().Be(100);
        metrics.P95LatencyMs.Should().Be(150);
        metrics.P99LatencyMs.Should().Be(200);
        metrics.AverageConfidence.Should().Be(0.85);
        metrics.TotalPrompts.Should().Be(100);
        metrics.SuccessfulPrompts.Should().Be(95);
    }

    [Fact]
    public void StatisticalAnalysis_ShouldContainInterpretation()
    {
        // Arrange
        var analysis = new StatisticalAnalysis(
            EffectSize: 0.6,
            IsSignificant: true,
            Interpretation: "Medium difference between variants");

        // Assert
        analysis.EffectSize.Should().Be(0.6);
        analysis.IsSignificant.Should().BeTrue();
        analysis.Interpretation.Should().Contain("Medium");
    }

    [Fact]
    public void PromptResult_ShouldCaptureSuccessAndFailure()
    {
        // Arrange
        var success = new PromptResult(
            Prompt: "test",
            Success: true,
            LatencyMs: 50,
            ConfidenceScore: 0.9,
            SelectedModel: "gpt-4",
            Error: null);

        var failure = new PromptResult(
            Prompt: "test",
            Success: false,
            LatencyMs: 100,
            ConfidenceScore: 0,
            SelectedModel: null,
            Error: "Model unavailable");

        // Assert
        success.Success.Should().BeTrue();
        success.SelectedModel.Should().Be("gpt-4");
        failure.Success.Should().BeFalse();
        failure.Error.Should().Be("Model unavailable");
    }

    private static List<IModelOrchestrator> CreateMockOrchestrators(int count)
    {
        var orchestrators = new List<IModelOrchestrator>();

        for (int i = 0; i < count; i++)
        {
            orchestrators.Add(new MockOrchestrator(i, delayMs: 0));
        }

        return orchestrators;
    }

    private static List<IModelOrchestrator> CreateSlowMockOrchestrators(int count, int delayMs)
    {
        var orchestrators = new List<IModelOrchestrator>();

        for (int i = 0; i < count; i++)
        {
            orchestrators.Add(new MockOrchestrator(i, delayMs));
        }

        return orchestrators;
    }

    /// <summary>
    /// Simple mock orchestrator for testing.
    /// </summary>
    private sealed class MockOrchestrator : IModelOrchestrator
    {
        private readonly int _variantNum;
        private readonly int _delayMs;

        public MockOrchestrator(int variantNum, int delayMs)
        {
            _variantNum = variantNum;
            _delayMs = delayMs;
        }

        public Task<Result<OrchestratorDecision, string>> SelectModelAsync(
            string prompt,
            Dictionary<string, object>? context = null,
            CancellationToken ct = default)
        {
            return SelectModelInternalAsync(prompt, ct);
        }

        private async Task<Result<OrchestratorDecision, string>> SelectModelInternalAsync(
            string prompt,
            CancellationToken ct)
        {
            if (_delayMs > 0)
            {
                await Task.Delay(_delayMs, ct);
            }

            var decision = new OrchestratorDecision(
                SelectedModel: null!,
                ModelName: $"model_{_variantNum}",
                Reason: "Test selection",
                RecommendedTools: new ToolRegistry(),
                ConfidenceScore: 0.8 + (_variantNum * 0.05));

            return Result<OrchestratorDecision, string>.Success(decision);
        }

        public UseCase ClassifyUseCase(string prompt)
        {
            return new UseCase(UseCaseType.Reasoning, 5, Array.Empty<string>(), 0.5, 0.5);
        }

        public void RegisterModel(ModelCapability capability) { }

        public void RecordMetric(string resourceName, double latencyMs, bool success) { }

        public IReadOnlyDictionary<string, PerformanceMetrics> GetMetrics()
        {
            return new Dictionary<string, PerformanceMetrics>();
        }
    }
}
