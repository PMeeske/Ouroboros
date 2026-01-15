// <copyright file="OrchestratorCompositionTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace Ouroboros.Tests.UnitTests;

using Ouroboros.Agent;
using Xunit;
using FluentAssertions;

/// <summary>
/// Comprehensive unit tests for orchestrator composition helpers.
/// Tests chaining, mapping, parallel execution, fallback, and conditional routing.
/// </summary>
[Trait("Category", "Unit")]
public sealed class OrchestratorCompositionTests
{
    private sealed class SimpleOrchestrator : OrchestratorBase<string, string>
    {
        private readonly string _suffix;

        public SimpleOrchestrator(string suffix, OrchestratorConfig? config = null)
            : base($"simple_{suffix}", config ?? OrchestratorConfig.Default())
        {
            _suffix = suffix;
        }

        protected override Task<string> ExecuteCoreAsync(string input, OrchestratorContext context)
        {
            return Task.FromResult($"{input}_{_suffix}");
        }
    }

    private sealed class TransformOrchestrator : OrchestratorBase<string, int>
    {
        public TransformOrchestrator(OrchestratorConfig? config = null)
            : base("transform", config ?? OrchestratorConfig.Default())
        {
        }

        protected override Task<int> ExecuteCoreAsync(string input, OrchestratorContext context)
        {
            return Task.FromResult(input.Length);
        }
    }

    private sealed class FailingOrchestrator : OrchestratorBase<string, string>
    {
        public FailingOrchestrator()
            : base("failing", OrchestratorConfig.Default())
        {
        }

        protected override Task<string> ExecuteCoreAsync(string input, OrchestratorContext context)
        {
            throw new InvalidOperationException("Orchestrator configured to fail");
        }
    }

    [Fact]
    public async Task CompositeOrchestrator_From_ShouldWrapOrchestrator()
    {
        // Arrange
        var orchestrator = new SimpleOrchestrator("test");
        var composite = CompositeOrchestrator<string, string>.From(orchestrator);

        // Act
        var result = await composite.ExecuteAsync("input");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("input_test");
    }

    [Fact]
    public async Task CompositeOrchestrator_Then_ShouldChainOrchestrators()
    {
        // Arrange
        var first = new SimpleOrchestrator("first").AsComposable();
        var second = new SimpleOrchestrator("second").AsComposable();

        // Act
        var composed = first.Then(second);
        var result = await composed.ExecuteAsync("start");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("start_first_second");
    }

    [Fact]
    public async Task CompositeOrchestrator_Map_ShouldTransformOutput()
    {
        // Arrange
        var orchestrator = new SimpleOrchestrator("test").AsComposable();

        // Act
        var mapped = orchestrator.Map(s => s.Length);
        var result = await mapped.ExecuteAsync("hello");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be(10); // "hello_test".Length
    }

    [Fact]
    public async Task CompositeOrchestrator_Tap_ShouldExecuteSideEffect()
    {
        // Arrange
        var orchestrator = new SimpleOrchestrator("test").AsComposable();
        string? captured = null;

        // Act
        var tapped = orchestrator.Tap(output => captured = output);
        var result = await tapped.ExecuteAsync("input");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("input_test");
        captured.Should().Be("input_test");
    }

    [Fact]
    public async Task OrchestratorComposer_Start_ShouldCreateComposable()
    {
        // Arrange
        var orchestrator = new SimpleOrchestrator("test");

        // Act
        var composable = OrchestratorComposer.Start(orchestrator);
        var result = await composable.ExecuteAsync("input");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("input_test");
    }

    [Fact]
    public async Task OrchestratorComposer_StartWith_ShouldCreateFromFunction()
    {
        // Arrange & Act
        var composable = OrchestratorComposer.StartWith<string, string>(
            "test_func",
            input => Task.FromResult($"processed_{input}"));
        var result = await composable.ExecuteAsync("data");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("processed_data");
    }

    [Fact]
    public async Task OrchestratorComposer_Parallel_ShouldExecuteConcurrently()
    {
        // Arrange
        var orch1 = new SimpleOrchestrator("a");
        var orch2 = new SimpleOrchestrator("b");
        var orch3 = new SimpleOrchestrator("c");

        // Act
        var parallel = OrchestratorComposer.Parallel(orch1, orch2, orch3);
        var result = await parallel.ExecuteAsync("input");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().HaveCount(3);
        result.Output.Should().Contain("input_a");
        result.Output.Should().Contain("input_b");
        result.Output.Should().Contain("input_c");
    }

    [Fact]
    public async Task OrchestratorComposer_Parallel_WithFailure_ShouldReturnFailure()
    {
        // Arrange
        var orch1 = new SimpleOrchestrator("a");
        var orch2 = new FailingOrchestrator();

        // Act
        var parallel = OrchestratorComposer.Parallel(orch1, orch2);
        var result = await parallel.ExecuteAsync("input");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Parallel orchestration had failures");
    }

    [Fact]
    public async Task OrchestratorComposer_WithFallback_ShouldUsePrimaryWhenSuccessful()
    {
        // Arrange
        var primary = new SimpleOrchestrator("primary");
        var fallback = new SimpleOrchestrator("fallback");

        // Act
        var composed = OrchestratorComposer.WithFallback(primary, fallback);
        var result = await composed.ExecuteAsync("input");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("input_primary");
    }

    [Fact]
    public async Task OrchestratorComposer_WithFallback_ShouldUseFallbackWhenPrimaryFails()
    {
        // Arrange
        var primary = new FailingOrchestrator();
        var fallback = new SimpleOrchestrator("fallback");

        // Act
        var composed = OrchestratorComposer.WithFallback(primary, fallback);
        var result = await composed.ExecuteAsync("input");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("input_fallback");
    }

    [Fact]
    public async Task OrchestratorComposer_WithFallback_BothFail_ShouldReturnFailure()
    {
        // Arrange
        var primary = new FailingOrchestrator();
        var fallback = new FailingOrchestrator();

        // Act
        var composed = OrchestratorComposer.WithFallback(primary, fallback);
        var result = await composed.ExecuteAsync("input");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Both primary and fallback failed");
    }

    [Fact]
    public async Task OrchestratorComposer_Conditional_ShouldRouteBasedOnPredicate()
    {
        // Arrange
        var whenTrue = new SimpleOrchestrator("true_branch");
        var whenFalse = new SimpleOrchestrator("false_branch");

        // Act - condition is true
        var conditionalTrue = OrchestratorComposer.Conditional(
            input => input.Length > 5,
            whenTrue,
            whenFalse);
        var resultTrue = await conditionalTrue.ExecuteAsync("long_input");

        // Act - condition is false
        var conditionalFalse = OrchestratorComposer.Conditional(
            input => input.Length > 5,
            whenTrue,
            whenFalse);
        var resultFalse = await conditionalFalse.ExecuteAsync("short");

        // Assert
        resultTrue.Success.Should().BeTrue();
        resultTrue.Output.Should().Be("long_input_true_branch");
        
        resultFalse.Success.Should().BeTrue();
        resultFalse.Output.Should().Be("short_false_branch");
    }

    [Fact]
    public async Task OrchestratorComposer_WithRetry_ShouldRetryOnFailure()
    {
        // Arrange
        int attemptCount = 0;
        var orchestrator = CompositeOrchestrator<string, string>.FromFunc(
            "retry_test",
            (input, _) =>
            {
                attemptCount++;
                if (attemptCount < 3)
                {
                    throw new InvalidOperationException("Not yet");
                }
                return Task.FromResult($"success_{input}");
            });

        // Act
        var withRetry = OrchestratorComposer.WithRetry(orchestrator, maxRetries: 3, delay: TimeSpan.FromMilliseconds(10));
        var result = await withRetry.ExecuteAsync("test");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("success_test");
        attemptCount.Should().Be(3);
    }

    [Fact]
    public async Task OrchestratorComposer_WithRetry_ExceedingMaxRetries_ShouldFail()
    {
        // Arrange
        var orchestrator = new FailingOrchestrator();

        // Act
        var withRetry = OrchestratorComposer.WithRetry(orchestrator, maxRetries: 2, delay: TimeSpan.FromMilliseconds(10));
        var result = await withRetry.ExecuteAsync("test");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("failed after 2 attempts");
    }

    [Fact]
    public async Task OrchestratorExtensions_Bind_ShouldChainWithTransformation()
    {
        // Arrange
        var orchestrator = new SimpleOrchestrator("test").AsComposable();

        // Act
        var bound = orchestrator.Bind(async output => output.Length);
        var result = await bound.ExecuteAsync("hello");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be(10); // "hello_test".Length
    }

    [Fact]
    public async Task OrchestratorExtensions_Where_ShouldFilterOutput()
    {
        // Arrange
        var orchestrator = new SimpleOrchestrator("test").AsComposable();

        // Act - predicate returns true
        var filteredTrue = orchestrator.Where(s => s.Contains("test"));
        var resultTrue = await filteredTrue.ExecuteAsync("input");

        // Act - predicate returns false
        var filteredFalse = orchestrator.Where(s => s.Contains("missing"));
        var resultFalse = await filteredFalse.ExecuteAsync("input");

        // Assert
        resultTrue.Success.Should().BeTrue();
        resultTrue.Output.Should().Be("input_test");

        resultFalse.Success.Should().BeTrue();
        resultFalse.Output.Should().BeNull();
    }

    [Fact]
    public async Task ComplexComposition_ShouldChainMultipleOperations()
    {
        // Arrange
        var first = new SimpleOrchestrator("step1").AsComposable();
        var second = new SimpleOrchestrator("step2").AsComposable();
        
        // Act - complex composition: orchestrate -> map -> orchestrate -> tap
        string? tappedValue = null;
        var composed = first
            .Then(second)
            .Map(s => s.ToUpperInvariant())
            .Then(CompositeOrchestrator<string, string>.FromFunc(
                "final",
                (s, _) => Task.FromResult($"final_{s}")))
            .Tap(s => tappedValue = s);

        var result = await composed.ExecuteAsync("start");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("final_START_STEP1_STEP2");
        tappedValue.Should().Be("final_START_STEP1_STEP2");
    }

    [Fact]
    public void OrchestratorComposer_Parallel_WithNoOrchestrators_ShouldThrow()
    {
        // Act
        Action act = () => OrchestratorComposer.Parallel<string, string>();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*at least one orchestrator*");
    }

    [Fact]
    public async Task CompositeOrchestrator_PreservesMetrics()
    {
        // Arrange
        var orchestrator = new SimpleOrchestrator("test").AsComposable();

        // Act
        await orchestrator.ExecuteAsync("test1");
        await orchestrator.ExecuteAsync("test2");
        var metrics = orchestrator.Metrics;

        // Assert
        metrics.TotalExecutions.Should().Be(2);
        metrics.SuccessfulExecutions.Should().Be(2);
    }
}
