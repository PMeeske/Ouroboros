// <copyright file="UnifiedOrchestrationIntegrationTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Tests.UnitTests;

using Ouroboros.Agent;
using Xunit;
using FluentAssertions;

/// <summary>
/// Integration tests for unified orchestration infrastructure.
/// Tests end-to-end scenarios with composed orchestrators.
/// </summary>
[Trait("Category", "Integration")]
public sealed class UnifiedOrchestrationIntegrationTests
{
    private sealed class StepOrchestrator : OrchestratorBase<string, string>
    {
        private readonly string _step;

        public StepOrchestrator(string step, OrchestratorConfig? config = null)
            : base($"step_{step}", config ?? OrchestratorConfig.Default())
        {
            _step = step;
        }

        protected override Task<string> ExecuteCoreAsync(string input, OrchestratorContext context)
        {
            return Task.FromResult($"{input}|{_step}");
        }
    }

    private sealed class AnalysisOrchestrator : OrchestratorBase<string, Dictionary<string, object>>
    {
        public AnalysisOrchestrator(OrchestratorConfig? config = null)
            : base("analyzer", config ?? OrchestratorConfig.Default())
        {
        }

        protected override Task<Dictionary<string, object>> ExecuteCoreAsync(
            string input,
            OrchestratorContext context)
        {
            return Task.FromResult(new Dictionary<string, object>
            {
                ["length"] = input.Length,
                ["word_count"] = input.Split(' ').Length,
                ["original"] = input
            });
        }
    }

    [Fact]
    public async Task EndToEnd_MultiStepPipeline_ShouldProcessSequentially()
    {
        // Arrange
        var step1 = new StepOrchestrator("tokenize");
        var step2 = new StepOrchestrator("normalize");
        var step3 = new StepOrchestrator("encode");

        var pipeline = step1.AsComposable()
            .Then(step2.AsComposable())
            .Then(step3.AsComposable());

        // Act
        var result = await pipeline.ExecuteAsync("input");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("input|tokenize|normalize|encode");
        result.ExecutionTime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task EndToEnd_TransformationChain_ShouldApplyAllTransformations()
    {
        // Arrange
        var step = new StepOrchestrator("process");
        
        var chain = step.AsComposable()
            .Map(s => s.ToUpperInvariant())
            .Map(s => s.Replace("|", " -> "))
            .Map(s => $"[{s}]");

        // Act
        var result = await chain.ExecuteAsync("data");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("[DATA -> PROCESS]");
    }

    [Fact]
    public async Task EndToEnd_ParallelProcessing_ShouldExecuteConcurrently()
    {
        // Arrange
        var step1 = new StepOrchestrator("analysis");
        var step2 = new StepOrchestrator("validation");
        var step3 = new StepOrchestrator("enrichment");

        var parallel = OrchestratorComposer.Parallel(step1, step2, step3);

        // Act
        var result = await parallel.ExecuteAsync("parallel_input");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().HaveCount(3);
        result.Output.Should().Contain("parallel_input|analysis");
        result.Output.Should().Contain("parallel_input|validation");
        result.Output.Should().Contain("parallel_input|enrichment");
    }

    [Fact]
    public async Task EndToEnd_FallbackRecovery_ShouldRecoverFromPrimaryFailure()
    {
        // Arrange
        var failingConfig = new OrchestratorConfig
        {
            EnableMetrics = true,
            EnableTracing = true
        };

        var primary = CompositeOrchestrator<string, string>.FromFunc(
            "failing_primary",
            (input, _) => throw new InvalidOperationException("Primary failed"));

        var fallback = new StepOrchestrator("fallback_recovery");

        var withFallback = OrchestratorComposer.WithFallback(primary, fallback);

        // Act
        var result = await withFallback.ExecuteAsync("test_fallback");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("test_fallback|fallback_recovery");
    }

    [Fact]
    public async Task EndToEnd_ConditionalRouting_ShouldRouteBasedOnCondition()
    {
        // Arrange
        var shortPath = new StepOrchestrator("short_process");
        var longPath = new StepOrchestrator("long_process");

        var router = OrchestratorComposer.Conditional(
            input => input.Length < 10,
            shortPath,
            longPath);

        // Act
        var shortResult = await router.ExecuteAsync("short");
        var longResult = await router.ExecuteAsync("this is a much longer input");

        // Assert
        shortResult.Success.Should().BeTrue();
        shortResult.Output.Should().Contain("short_process");

        longResult.Success.Should().BeTrue();
        longResult.Output.Should().Contain("long_process");
    }

    [Fact]
    public async Task EndToEnd_RetryMechanism_ShouldRetryUntilSuccess()
    {
        // Arrange
        int attemptCount = 0;
        var unreliable = CompositeOrchestrator<string, string>.FromFunc(
            "unreliable",
            (input, _) =>
            {
                attemptCount++;
                if (attemptCount < 3)
                {
                    throw new InvalidOperationException($"Attempt {attemptCount} failed");
                }
                return Task.FromResult($"Success on attempt {attemptCount}");
            });

        var withRetry = OrchestratorComposer.WithRetry(
            unreliable,
            maxRetries: 5,
            delay: TimeSpan.FromMilliseconds(10));

        // Act
        var result = await withRetry.ExecuteAsync("test_retry");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("Success on attempt 3");
        attemptCount.Should().Be(3);
    }

    [Fact]
    public async Task EndToEnd_ComplexComposition_ShouldHandleAllPatterns()
    {
        // Arrange - Build a complex pipeline with multiple patterns
        var config = OrchestratorConfig.Default();
        
        // Step 1: Initial processing
        var initialProcessor = new StepOrchestrator("init", config);
        
        // Step 2: Parallel validation and enrichment
        var validator = new StepOrchestrator("validate", config);
        var enricher = new StepOrchestrator("enrich", config);
        
        // Step 3: Final transformation
        var finalTransform = new StepOrchestrator("finalize", config);

        // Build complex pipeline
        var pipeline = initialProcessor.AsComposable()
            .Then(CompositeOrchestrator<string, string[]>.FromFunc(
                "parallel_phase",
                async (input, ctx) =>
                {
                    var tasks = new[]
                    {
                        validator.ExecuteAsync(input, ctx),
                        enricher.ExecuteAsync(input, ctx)
                    };
                    var results = await Task.WhenAll(tasks);
                    return results.Select(r => r.Output!).ToArray();
                }))
            .Map(results => string.Join(" & ", results))
            .Then(finalTransform.AsComposable());

        // Act
        var result = await pipeline.ExecuteAsync("complex");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("init");
        result.Output.Should().Contain("validate");
        result.Output.Should().Contain("enrich");
        result.Output.Should().Contain("finalize");
    }

    [Fact]
    public async Task EndToEnd_MetricsAggregation_ShouldTrackAcrossOrchestrators()
    {
        // Arrange
        var step1 = new StepOrchestrator("step1");
        var step2 = new StepOrchestrator("step2");
        var step3 = new StepOrchestrator("step3");

        // Execute multiple times
        for (int i = 0; i < 5; i++)
        {
            await step1.ExecuteAsync($"test{i}");
            await step2.ExecuteAsync($"test{i}");
            await step3.ExecuteAsync($"test{i}");
        }

        // Act
        var metrics1 = step1.Metrics;
        var metrics2 = step2.Metrics;
        var metrics3 = step3.Metrics;

        // Assert
        metrics1.TotalExecutions.Should().Be(5);
        metrics1.SuccessRate.Should().Be(1.0);
        
        metrics2.TotalExecutions.Should().Be(5);
        metrics2.SuccessRate.Should().Be(1.0);
        
        metrics3.TotalExecutions.Should().Be(5);
        metrics3.SuccessRate.Should().Be(1.0);
    }

    [Fact]
    public async Task EndToEnd_ErrorPropagation_ShouldPropagateErrorsThroughChain()
    {
        // Arrange
        var step1 = new StepOrchestrator("step1");
        var failingStep = CompositeOrchestrator<string, string>.FromFunc(
            "failing",
            (input, _) => throw new InvalidOperationException("Step failed"));
        var step3 = new StepOrchestrator("step3");

        var pipeline = step1.AsComposable()
            .Then(failingStep)
            .Then(step3.AsComposable());

        // Act
        var result = await pipeline.ExecuteAsync("test_error");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Step failed");
    }

    [Fact]
    public async Task EndToEnd_ContextPropagation_ShouldMaintainContextThroughChain()
    {
        // Arrange
        var capturedContexts = new List<OrchestratorContext>();
        
        var step1 = CompositeOrchestrator<string, string>.FromFunc(
            "step1",
            (input, ctx) =>
            {
                capturedContexts.Add(ctx);
                return Task.FromResult($"{input}|1");
            });
        
        var step2 = CompositeOrchestrator<string, string>.FromFunc(
            "step2",
            (input, ctx) =>
            {
                capturedContexts.Add(ctx);
                return Task.FromResult($"{input}|2");
            });

        var pipeline = step1.Then(step2);

        var context = OrchestratorContext.Create()
            .WithMetadata("request_id", "req123")
            .WithMetadata("user_id", "user456");

        // Act
        await pipeline.ExecuteAsync("test", context);

        // Assert
        capturedContexts.Should().HaveCount(2);
        capturedContexts[0].GetMetadata<string>("request_id").Should().Be("req123");
        capturedContexts[1].GetMetadata<string>("request_id").Should().Be("req123");
    }

    [Fact]
    public async Task EndToEnd_TypeTransformation_ShouldHandleDifferentTypes()
    {
        // Arrange
        var stringStep = new StepOrchestrator("process");
        var analyzer = new AnalysisOrchestrator();

        var pipeline = stringStep.AsComposable()
            .Then(analyzer.AsComposable())
            .Map(analysis => $"Length: {analysis["length"]}, Words: {analysis["word_count"]}");

        // Act
        var result = await pipeline.ExecuteAsync("Hello world test");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Contain("Length:");
        result.Output.Should().Contain("Words: 3");
    }

    [Fact]
    public async Task EndToEnd_HealthChecks_ShouldProvideHealthInformation()
    {
        // Arrange
        var orchestrator = new StepOrchestrator("health_test");
        
        // Execute a few times
        await orchestrator.ExecuteAsync("test1");
        await orchestrator.ExecuteAsync("test2");

        // Act
        var health = await orchestrator.GetHealthAsync();

        // Assert
        health.Should().ContainKey("orchestrator_name");
        health.Should().ContainKey("status");
        health.Should().ContainKey("total_executions");
        health.Should().ContainKey("success_rate");
        health["orchestrator_name"].Should().Be("step_health_test");
        health["total_executions"].Should().Be(2);
    }
}
