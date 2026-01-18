// <copyright file="OrchestratorContextTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.UnitTests.Agent;

using FluentAssertions;
using Ouroboros.Agent;
using Xunit;

/// <summary>
/// Tests for OrchestratorContext, OrchestratorConfig, RetryConfig, OrchestratorMetrics, and OrchestratorResult.
/// </summary>
[Trait("Category", "Unit")]
public class OrchestratorContextTests
{
    #region OrchestratorContext Tests

    [Fact]
    public void Create_GeneratesUniqueOperationId()
    {
        // Act
        var context1 = OrchestratorContext.Create();
        var context2 = OrchestratorContext.Create();

        // Assert
        context1.OperationId.Should().NotBeNullOrEmpty();
        context2.OperationId.Should().NotBeNullOrEmpty();
        context1.OperationId.Should().NotBe(context2.OperationId);
    }

    [Fact]
    public void Create_WithMetadata_IncludesMetadata()
    {
        // Arrange
        var metadata = new Dictionary<string, object>
        {
            ["key1"] = "value1",
            ["key2"] = 42
        };

        // Act
        var context = OrchestratorContext.Create(metadata);

        // Assert
        context.Metadata.Should().ContainKey("key1");
        context.Metadata.Should().ContainKey("key2");
        context.Metadata["key1"].Should().Be("value1");
        context.Metadata["key2"].Should().Be(42);
    }

    [Fact]
    public void Create_WithNullMetadata_CreatesEmptyDictionary()
    {
        // Act
        var context = OrchestratorContext.Create(null);

        // Assert
        context.Metadata.Should().NotBeNull();
        context.Metadata.Should().BeEmpty();
    }

    [Fact]
    public void Create_WithCancellationToken_IncludesToken()
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        // Act
        var context = OrchestratorContext.Create(ct: cts.Token);

        // Assert
        context.CancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public void GetMetadata_ExistingKey_ReturnsValue()
    {
        // Arrange
        var metadata = new Dictionary<string, object> { ["name"] = "test" };
        var context = OrchestratorContext.Create(metadata);

        // Act
        var value = context.GetMetadata<string>("name");

        // Assert
        value.Should().Be("test");
    }

    [Fact]
    public void GetMetadata_MissingKey_ReturnsDefault()
    {
        // Arrange
        var context = OrchestratorContext.Create();

        // Act
        var value = context.GetMetadata<string>("missing", "default");

        // Assert
        value.Should().Be("default");
    }

    [Fact]
    public void GetMetadata_WrongType_ReturnsDefault()
    {
        // Arrange
        var metadata = new Dictionary<string, object> { ["number"] = "not a number" };
        var context = OrchestratorContext.Create(metadata);

        // Act
        var value = context.GetMetadata<int>("number", 42);

        // Assert
        value.Should().Be(42);
    }

    [Fact]
    public void WithMetadata_AddsNewEntry()
    {
        // Arrange
        var context = OrchestratorContext.Create();

        // Act
        var newContext = context.WithMetadata("newKey", "newValue");

        // Assert
        newContext.Metadata.Should().ContainKey("newKey");
        newContext.Metadata["newKey"].Should().Be("newValue");
        context.Metadata.Should().NotContainKey("newKey"); // Original unchanged
    }

    [Fact]
    public void WithMetadata_UpdatesExistingEntry()
    {
        // Arrange
        var metadata = new Dictionary<string, object> { ["key"] = "oldValue" };
        var context = OrchestratorContext.Create(metadata);

        // Act
        var newContext = context.WithMetadata("key", "newValue");

        // Assert
        newContext.Metadata["key"].Should().Be("newValue");
        context.Metadata["key"].Should().Be("oldValue"); // Original unchanged
    }

    #endregion

    #region OrchestratorConfig Tests

    [Fact]
    public void Default_CreatesDefaultConfig()
    {
        // Act
        var config = OrchestratorConfig.Default();

        // Assert
        config.EnableTracing.Should().BeTrue();
        config.EnableMetrics.Should().BeTrue();
        config.EnableSafetyChecks.Should().BeTrue();
        config.ExecutionTimeout.Should().BeNull();
        config.RetryConfig.Should().BeNull();
        config.CustomSettings.Should().BeEmpty();
    }

    [Fact]
    public void GetSetting_ExistingKey_ReturnsValue()
    {
        // Arrange
        var config = new OrchestratorConfig
        {
            CustomSettings = new Dictionary<string, object> { ["maxRetries"] = 5 }
        };

        // Act
        var value = config.GetSetting<int>("maxRetries");

        // Assert
        value.Should().Be(5);
    }

    [Fact]
    public void GetSetting_MissingKey_ReturnsDefault()
    {
        // Arrange
        var config = OrchestratorConfig.Default();

        // Act
        var value = config.GetSetting<int>("missing", 10);

        // Assert
        value.Should().Be(10);
    }

    [Fact]
    public void GetSetting_WrongType_ReturnsDefault()
    {
        // Arrange
        var config = new OrchestratorConfig
        {
            CustomSettings = new Dictionary<string, object> { ["value"] = "string" }
        };

        // Act
        var value = config.GetSetting<int>("value", 42);

        // Assert
        value.Should().Be(42);
    }

    #endregion

    #region RetryConfig Tests

    [Fact]
    public void Default_CreatesDefaultRetryConfig()
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
    public void Constructor_SetsAllProperties()
    {
        // Act
        var config = new RetryConfig(
            MaxRetries: 5,
            InitialDelay: TimeSpan.FromSeconds(1),
            MaxDelay: TimeSpan.FromMinutes(1),
            BackoffMultiplier: 1.5);

        // Assert
        config.MaxRetries.Should().Be(5);
        config.InitialDelay.Should().Be(TimeSpan.FromSeconds(1));
        config.MaxDelay.Should().Be(TimeSpan.FromMinutes(1));
        config.BackoffMultiplier.Should().Be(1.5);
    }

    #endregion

    #region OrchestratorMetrics Tests

    [Fact]
    public void Initial_CreatesEmptyMetrics()
    {
        // Act
        var metrics = OrchestratorMetrics.Initial("TestOrchestrator");

        // Assert
        metrics.OrchestratorName.Should().Be("TestOrchestrator");
        metrics.TotalExecutions.Should().Be(0);
        metrics.SuccessfulExecutions.Should().Be(0);
        metrics.FailedExecutions.Should().Be(0);
        metrics.AverageLatencyMs.Should().Be(0.0);
        metrics.SuccessRate.Should().Be(0.0);
        metrics.CustomMetrics.Should().BeEmpty();
    }

    [Fact]
    public void RecordExecution_Success_UpdatesMetrics()
    {
        // Arrange
        var metrics = OrchestratorMetrics.Initial("Test");

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
    public void RecordExecution_Failure_UpdatesMetrics()
    {
        // Arrange
        var metrics = OrchestratorMetrics.Initial("Test");

        // Act
        var updated = metrics.RecordExecution(50.0, success: false);

        // Assert
        updated.TotalExecutions.Should().Be(1);
        updated.SuccessfulExecutions.Should().Be(0);
        updated.FailedExecutions.Should().Be(1);
        updated.AverageLatencyMs.Should().Be(50.0);
        updated.SuccessRate.Should().Be(0.0);
    }

    [Fact]
    public void RecordExecution_MultipleExecutions_CalculatesAverages()
    {
        // Arrange
        var metrics = OrchestratorMetrics.Initial("Test");

        // Act
        var updated = metrics
            .RecordExecution(100.0, success: true)
            .RecordExecution(200.0, success: true)
            .RecordExecution(300.0, success: false);

        // Assert
        updated.TotalExecutions.Should().Be(3);
        updated.SuccessfulExecutions.Should().Be(2);
        updated.FailedExecutions.Should().Be(1);
        updated.AverageLatencyMs.Should().Be(200.0); // (100 + 200 + 300) / 3
        updated.SuccessRate.Should().BeApproximately(0.666, 0.01);
    }

    [Fact]
    public void CalculatedSuccessRate_ReturnsCorrectValue()
    {
        // Arrange
        var metrics = OrchestratorMetrics.Initial("Test")
            .RecordExecution(100.0, true)
            .RecordExecution(100.0, true)
            .RecordExecution(100.0, false)
            .RecordExecution(100.0, true);

        // Assert
        metrics.CalculatedSuccessRate.Should().Be(0.75);
    }

    [Fact]
    public void CalculatedSuccessRate_NoExecutions_ReturnsZero()
    {
        // Arrange
        var metrics = OrchestratorMetrics.Initial("Test");

        // Assert
        metrics.CalculatedSuccessRate.Should().Be(0.0);
    }

    [Fact]
    public void GetCustomMetric_ExistingKey_ReturnsValue()
    {
        // Arrange
        var metrics = OrchestratorMetrics.Initial("Test")
            .WithCustomMetric("myMetric", 42.5);

        // Act
        var value = metrics.GetCustomMetric("myMetric");

        // Assert
        value.Should().Be(42.5);
    }

    [Fact]
    public void GetCustomMetric_MissingKey_ReturnsDefault()
    {
        // Arrange
        var metrics = OrchestratorMetrics.Initial("Test");

        // Act
        var value = metrics.GetCustomMetric("missing", 99.0);

        // Assert
        value.Should().Be(99.0);
    }

    [Fact]
    public void WithCustomMetric_AddsNewMetric()
    {
        // Arrange
        var metrics = OrchestratorMetrics.Initial("Test");

        // Act
        var updated = metrics.WithCustomMetric("newMetric", 123.0);

        // Assert
        updated.CustomMetrics.Should().ContainKey("newMetric");
        updated.CustomMetrics["newMetric"].Should().Be(123.0);
        metrics.CustomMetrics.Should().NotContainKey("newMetric"); // Original unchanged
    }

    [Fact]
    public void WithCustomMetric_UpdatesExistingMetric()
    {
        // Arrange
        var metrics = OrchestratorMetrics.Initial("Test")
            .WithCustomMetric("metric", 10.0);

        // Act
        var updated = metrics.WithCustomMetric("metric", 20.0);

        // Assert
        updated.CustomMetrics["metric"].Should().Be(20.0);
    }

    #endregion

    #region OrchestratorResult Tests

    [Fact]
    public void Ok_CreatesSuccessfulResult()
    {
        // Arrange
        var metrics = OrchestratorMetrics.Initial("Test");
        var executionTime = TimeSpan.FromMilliseconds(100);

        // Act
        var result = OrchestratorResult<int>.Ok(42, metrics, executionTime);

        // Assert
        result.Success.Should().BeTrue();
        result.Output.Should().Be(42);
        result.ErrorMessage.Should().BeNull();
        result.Metrics.Should().Be(metrics);
        result.ExecutionTime.Should().Be(executionTime);
    }

    [Fact]
    public void Ok_WithMetadata_IncludesMetadata()
    {
        // Arrange
        var metrics = OrchestratorMetrics.Initial("Test");
        var metadata = new Dictionary<string, object> { ["key"] = "value" };

        // Act
        var result = OrchestratorResult<int>.Ok(42, metrics, TimeSpan.Zero, metadata);

        // Assert
        result.Metadata.Should().ContainKey("key");
        result.Metadata["key"].Should().Be("value");
    }

    [Fact]
    public void Failure_CreatesFailedResult()
    {
        // Arrange
        var metrics = OrchestratorMetrics.Initial("Test");
        var executionTime = TimeSpan.FromMilliseconds(50);

        // Act
        var result = OrchestratorResult<int>.Failure("Error message", metrics, executionTime);

        // Assert
        result.Success.Should().BeFalse();
        result.Output.Should().Be(default(int));
        result.ErrorMessage.Should().Be("Error message");
        result.Metrics.Should().Be(metrics);
        result.ExecutionTime.Should().Be(executionTime);
    }

    [Fact]
    public void ToResult_OnSuccess_ReturnsResultSuccess()
    {
        // Arrange
        var orchestratorResult = OrchestratorResult<int>.Ok(
            42,
            OrchestratorMetrics.Initial("Test"),
            TimeSpan.Zero);

        // Act
        var result = orchestratorResult.ToResult();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(42);
    }

    [Fact]
    public void ToResult_OnFailure_ReturnsResultFailure()
    {
        // Arrange
        var orchestratorResult = OrchestratorResult<int>.Failure(
            "Something went wrong",
            OrchestratorMetrics.Initial("Test"),
            TimeSpan.Zero);

        // Act
        var result = orchestratorResult.ToResult();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Something went wrong");
    }

    [Fact]
    public void ToResult_OnFailureWithNullMessage_ReturnsDefaultError()
    {
        // Arrange
        var orchestratorResult = new OrchestratorResult<int>(
            default,
            Success: false,
            ErrorMessage: null,
            OrchestratorMetrics.Initial("Test"),
            TimeSpan.Zero,
            new Dictionary<string, object>());

        // Act
        var result = orchestratorResult.ToResult();

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Operation failed");
    }

    [Fact]
    public void OrchestratorResult_GetMetadata_ExistingKey_ReturnsValue()
    {
        // Arrange
        var metadata = new Dictionary<string, object> { ["count"] = 5 };
        var result = OrchestratorResult<int>.Ok(
            42,
            OrchestratorMetrics.Initial("Test"),
            TimeSpan.Zero,
            metadata);

        // Act
        var value = result.GetMetadata<int>("count");

        // Assert
        value.Should().Be(5);
    }

    [Fact]
    public void OrchestratorResult_GetMetadata_MissingKey_ReturnsDefault()
    {
        // Arrange
        var result = OrchestratorResult<int>.Ok(
            42,
            OrchestratorMetrics.Initial("Test"),
            TimeSpan.Zero);

        // Act
        var value = result.GetMetadata<string>("missing", "default");

        // Assert
        value.Should().Be("default");
    }

    #endregion

    #region ConfidenceRating Tests

    [Fact]
    public void ConfidenceRating_HasExpectedValues()
    {
        // Assert
        Enum.GetValues<ConfidenceRating>().Should().HaveCount(3);
        ((int)ConfidenceRating.Low).Should().Be(0);
        ((int)ConfidenceRating.Medium).Should().Be(1);
        ((int)ConfidenceRating.High).Should().Be(2);
    }

    #endregion
}
