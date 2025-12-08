// <copyright file="OrchestratorBaseTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

namespace LangChainPipeline.Tests;

using LangChainPipeline.Agent;
using LangChainPipeline.Agent.MetaAI;
using Xunit;
using FluentAssertions;

/// <summary>
/// Comprehensive unit tests for OrchestratorBase and IOrchestrator interface.
/// Tests unified orchestrator patterns, metrics, tracing, and error handling.
/// </summary>
public sealed class OrchestratorBaseTests
{
    /// <summary>
    /// Test orchestrator implementation for testing base functionality.
    /// </summary>
    private sealed class TestOrchestrator : OrchestratorBase<string, string>
    {
        private readonly Func<string, OrchestratorContext, Task<string>>? _executeFunc;
        private readonly bool _shouldFail;

        public TestOrchestrator(
            OrchestratorConfig? config = null,
            ISafetyGuard? safetyGuard = null,
            Func<string, OrchestratorContext, Task<string>>? executeFunc = null,
            bool shouldFail = false)
            : base("test_orchestrator", config ?? OrchestratorConfig.Default(), safetyGuard)
        {
            _executeFunc = executeFunc;
            _shouldFail = shouldFail;
        }

        protected override async Task<string> ExecuteCoreAsync(string input, OrchestratorContext context)
        {
            if (_shouldFail)
            {
                throw new InvalidOperationException("Orchestrator configured to fail");
            }

            if (_executeFunc != null)
            {
                return await _executeFunc(input, context);
            }

            return $"Processed: {input}";
        }

        protected override Result<bool, string> ValidateInput(string input, OrchestratorContext context)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return Result<bool, string>.Failure("Input cannot be empty");
            }

            return base.ValidateInput(input, context);
        }

        protected override Task<Dictionary<string, object>> GetCustomHealthAsync(CancellationToken ct)
        {
            return Task.FromResult(new Dictionary<string, object>
            {
                ["custom_property"] = "test_value"
            });
        }
    }

    [Fact]
    public void OrchestratorContext_Create_ShouldGenerateOperationId()
    {
        // Act
        var context = OrchestratorContext.Create();

        // Assert
        context.OperationId.Should().NotBeNullOrEmpty();
        context.Metadata.Should().NotBeNull();
        context.CancellationToken.Should().Be(CancellationToken.None);
    }

    [Fact]
    public void OrchestratorContext_WithMetadata_ShouldAddMetadata()
    {
        // Arrange
        var context = OrchestratorContext.Create();

        // Act
        var newContext = context.WithMetadata("key1", "value1");

        // Assert
        newContext.GetMetadata<string>("key1").Should().Be("value1");
        context.Metadata.Should().NotContainKey("key1"); // Original unchanged
    }

    [Fact]
    public void OrchestratorContext_GetMetadata_ShouldReturnDefaultForMissingKey()
    {
        // Arrange
        var context = OrchestratorContext.Create();

        // Act
        var value = context.GetMetadata("nonexistent", "default");

        // Assert
        value.Should().Be("default");
    }

    [Fact]
    public void OrchestratorConfig_Default_ShouldHaveExpectedDefaults()
    {
        // Act
        var config = OrchestratorConfig.Default();

        // Assert
        config.EnableTracing.Should().BeTrue();
        config.EnableMetrics.Should().BeTrue();
        config.EnableSafetyChecks.Should().BeTrue();
        config.ExecutionTimeout.Should().BeNull();
        config.CustomSettings.Should().NotBeNull();
    }

    [Fact]
    public void OrchestratorConfig_GetSetting_ShouldReturnDefaultForMissingSetting()
    {
        // Arrange
        var config = OrchestratorConfig.Default();

        // Act
        var value = config.GetSetting("nonexistent", 42);

        // Assert
        value.Should().Be(42);
    }

    [Fact]
    public void RetryConfig_Default_ShouldHaveExpectedValues()
    {
        // Act
        var config = RetryConfig.Default();

        // Assert
        config.MaxRetries.Should().Be(3);
        config.InitialDelay.Should().Be(TimeSpan.FromMilliseconds(100));
        config.MaxDelay.Should().Be(TimeSpan.FromSeconds(10));
        config.BackoffMultiplier.Should().Be(2.0);
    }

    [Fact]
    public void OrchestratorMetrics_Initial_ShouldStartWithZeros()
    {
        // Act
        var metrics = OrchestratorMetrics.Initial("test");

        // Assert
        metrics.OrchestratorName.Should().Be("test");
        metrics.TotalExecutions.Should().Be(0);
        metrics.SuccessfulExecutions.Should().Be(0);
        metrics.FailedExecutions.Should().Be(0);
        metrics.AverageLatencyMs.Should().Be(0.0);
        metrics.SuccessRate.Should().Be(0.0);
    }

    [Fact]
    public void OrchestratorMetrics_RecordExecution_ShouldUpdateMetrics()
    {
        // Arrange
        var metrics = OrchestratorMetrics.Initial("test");

        // Act
        var updated = metrics.RecordExecution(100.0, success: true);

        // Assert
        updated.TotalExecutions.Should().Be(1);
        updated.SuccessfulExecutions.Should().Be(1);
        updated.FailedExecutions.Should().Be(0);
        updated.AverageLatencyMs.Should().Be(100.0);
        updated.SuccessRate.Should().Be(1.0);
    }

    [Fact]
    public void OrchestratorMetrics_MultipleExecutions_ShouldCalculateCorrectAverages()
    {
        // Arrange
        var metrics = OrchestratorMetrics.Initial("test");

        // Act
        var updated = metrics
            .RecordExecution(100.0, success: true)
            .RecordExecution(200.0, success: true)
            .RecordExecution(150.0, success: false);

        // Assert
        updated.TotalExecutions.Should().Be(3);
        updated.SuccessfulExecutions.Should().Be(2);
        updated.FailedExecutions.Should().Be(1);
        updated.AverageLatencyMs.Should().BeApproximately(150.0, 0.1);
        updated.SuccessRate.Should().BeApproximately(0.666, 0.01);
    }

    [Fact]
    public void OrchestratorMetrics_WithCustomMetric_ShouldAddMetric()
    {
        // Arrange
        var metrics = OrchestratorMetrics.Initial("test");

        // Act
        var updated = metrics.WithCustomMetric("custom_score", 0.95);

        // Assert
        updated.GetCustomMetric("custom_score").Should().Be(0.95);
    }

    [Fact]
    public void OrchestratorResult_Ok_ShouldCreateSuccessResult()
    {
        // Arrange
        var metrics = OrchestratorMetrics.Initial("test");
        var executionTime = TimeSpan.FromMilliseconds(100);

        // Act
        var result = OrchestratorResult<string>.Ok("output", metrics, executionTime);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("output");
        result.ErrorMessage.Should().BeNull();
        result.ExecutionTime.Should().Be(executionTime);
    }

    [Fact]
    public void OrchestratorResult_Failure_ShouldCreateFailureResult()
    {
        // Arrange
        var metrics = OrchestratorMetrics.Initial("test");
        var executionTime = TimeSpan.FromMilliseconds(100);

        // Act
        var result = OrchestratorResult<string>.Failure("error", metrics, executionTime);

        // Assert
        result.Success.Should().BeFalse();
        result.Output.Should().BeNull();
        result.ErrorMessage.Should().Be("error");
        result.ExecutionTime.Should().Be(executionTime);
    }

    [Fact]
    public void OrchestratorResult_ToResult_ShouldConvertSuccessfully()
    {
        // Arrange
        var metrics = OrchestratorMetrics.Initial("test");
        var result = OrchestratorResult<string>.Ok("output", metrics, TimeSpan.Zero);

        // Act
        var converted = result.ToResult();

        // Assert
        converted.IsSuccess.Should().BeTrue();
        converted.Match(
            value => value.Should().Be("output"),
            error => Assert.Fail("Should not be error"));
    }

    [Fact]
    public async Task ExecuteAsync_WithValidInput_ShouldReturnSuccess()
    {
        // Arrange
        var orchestrator = new TestOrchestrator();

        // Act
        var result = await orchestrator.ExecuteAsync("test input");

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be("Processed: test input");
        result.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task ExecuteAsync_WithInvalidInput_ShouldReturnFailure()
    {
        // Arrange
        var orchestrator = new TestOrchestrator();

        // Act
        var result = await orchestrator.ExecuteAsync("");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Input cannot be empty");
    }

    [Fact]
    public async Task ExecuteAsync_WhenCoreThrows_ShouldReturnFailure()
    {
        // Arrange
        var orchestrator = new TestOrchestrator(shouldFail: true);

        // Act
        var result = await orchestrator.ExecuteAsync("test input");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Execution failed");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldUpdateMetrics()
    {
        // Arrange
        var orchestrator = new TestOrchestrator();

        // Act
        await orchestrator.ExecuteAsync("test input");
        var metrics = orchestrator.Metrics;

        // Assert
        metrics.TotalExecutions.Should().Be(1);
        metrics.SuccessfulExecutions.Should().Be(1);
        metrics.SuccessRate.Should().Be(1.0);
    }

    [Fact]
    public async Task ExecuteAsync_MultipleExecutions_ShouldTrackAllMetrics()
    {
        // Arrange
        var orchestrator = new TestOrchestrator();

        // Act
        await orchestrator.ExecuteAsync("input1");
        await orchestrator.ExecuteAsync("input2");
        await orchestrator.ExecuteAsync("");  // This will fail validation
        var metrics = orchestrator.Metrics;

        // Assert
        metrics.TotalExecutions.Should().Be(3);
        metrics.SuccessfulExecutions.Should().Be(2);
        metrics.FailedExecutions.Should().Be(1);
    }

    [Fact]
    public async Task ExecuteAsync_WithTimeout_ShouldRespectTimeout()
    {
        // Arrange
        var config = new OrchestratorConfig
        {
            ExecutionTimeout = TimeSpan.FromMilliseconds(50)
        };
        var orchestrator = new TestOrchestrator(
            config: config,
            executeFunc: async (input, ctx) =>
            {
                await Task.Delay(200, ctx.CancellationToken);
                return "done";
            });

        // Act
        var result = await orchestrator.ExecuteAsync("test input");

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("cancelled");
    }

    [Fact]
    public async Task ExecuteAsync_WithContext_ShouldUseProvidedContext()
    {
        // Arrange
        var context = OrchestratorContext.Create()
            .WithMetadata("custom_key", "custom_value");
        
        string? capturedMetadata = null;
        var orchestrator = new TestOrchestrator(
            executeFunc: (input, ctx) =>
            {
                capturedMetadata = ctx.GetMetadata<string>("custom_key");
                return Task.FromResult("done");
            });

        // Act
        await orchestrator.ExecuteAsync("test", context);

        // Assert
        capturedMetadata.Should().Be("custom_value");
    }

    [Fact]
    public void ValidateReadiness_Default_ShouldReturnSuccess()
    {
        // Arrange
        var orchestrator = new TestOrchestrator();

        // Act
        var result = orchestrator.ValidateReadiness();

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task GetHealthAsync_ShouldReturnHealthInfo()
    {
        // Arrange
        var orchestrator = new TestOrchestrator();
        await orchestrator.ExecuteAsync("test");

        // Act
        var health = await orchestrator.GetHealthAsync();

        // Assert
        health.Should().ContainKey("orchestrator_name");
        health.Should().ContainKey("status");
        health.Should().ContainKey("total_executions");
        health.Should().ContainKey("success_rate");
        health.Should().ContainKey("custom_property");
        health["custom_property"].Should().Be("test_value");
    }

    [Fact]
    public async Task ExecuteAsync_WithMetricsDisabled_ShouldNotUpdateMetrics()
    {
        // Arrange
        var config = new OrchestratorConfig { EnableMetrics = false };
        var orchestrator = new TestOrchestrator(config: config);

        // Act
        await orchestrator.ExecuteAsync("test");
        var metrics = orchestrator.Metrics;

        // Assert
        metrics.TotalExecutions.Should().Be(0);
    }

    [Fact]
    public void Configuration_ShouldReturnProvidedConfig()
    {
        // Arrange
        var config = new OrchestratorConfig { EnableTracing = false };
        var orchestrator = new TestOrchestrator(config: config);

        // Act
        var retrievedConfig = orchestrator.Configuration;

        // Assert
        retrievedConfig.EnableTracing.Should().BeFalse();
    }
}
