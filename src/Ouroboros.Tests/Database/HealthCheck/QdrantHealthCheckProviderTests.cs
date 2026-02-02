// <copyright file="QdrantHealthCheckProviderTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Database.HealthCheck;

using System.Net;
using FluentAssertions;
using Ouroboros.Core.Infrastructure.HealthCheck;
using Xunit;

/// <summary>
/// Unit tests for QdrantHealthCheckProvider.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "Database")]
public sealed class QdrantHealthCheckProviderTests : IDisposable
{
    private QdrantHealthCheckProvider? _provider;

    public void Dispose()
    {
        _provider?.Dispose();
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidEndpoint_SetsComponentName()
    {
        // Arrange & Act
        _provider = new QdrantHealthCheckProvider("http://localhost:6334");

        // Assert
        _provider.ComponentName.Should().Be("Qdrant");
    }

    [Fact]
    public void Constructor_WithNullEndpoint_ThrowsArgumentNullException()
    {
        // Act & Assert
        var action = () => new QdrantHealthCheckProvider(null!);
        action.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(30)]
    public void Constructor_WithCustomTimeout_AcceptsValue(int timeoutSeconds)
    {
        // Act - should not throw
        _provider = new QdrantHealthCheckProvider("http://localhost:6334", timeoutSeconds);

        // Assert
        _provider.Should().NotBeNull();
    }

    #endregion

    #region CheckHealthAsync Tests

    [Fact]
    public async Task CheckHealthAsync_WithUnreachableEndpoint_ReturnsUnhealthy()
    {
        // Arrange - use a port that won't be listening
        _provider = new QdrantHealthCheckProvider("http://localhost:59999", timeoutSeconds: 1);

        // Act
        var result = await _provider.CheckHealthAsync();

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.ComponentName.Should().Be("Qdrant");
        result.ResponseTime.Should().BeGreaterThan(-1);
        result.Details.Should().ContainKey("endpoint");
    }

    [Fact]
    public async Task CheckHealthAsync_WithCancellation_ReturnsUnhealthy()
    {
        // Arrange
        _provider = new QdrantHealthCheckProvider("http://localhost:6334", timeoutSeconds: 30);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        var result = await _provider.CheckHealthAsync(cts.Token);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CheckHealthAsync_ResultHasTimestamp()
    {
        // Arrange
        _provider = new QdrantHealthCheckProvider("http://localhost:59999", timeoutSeconds: 1);
        var beforeCheck = DateTime.UtcNow.AddSeconds(-1);

        // Act
        var result = await _provider.CheckHealthAsync();

        var afterCheck = DateTime.UtcNow.AddSeconds(1);

        // Assert
        result.Timestamp.Should().BeAfter(beforeCheck);
        result.Timestamp.Should().BeBefore(afterCheck);
    }

    [Fact]
    public async Task CheckHealthAsync_IncludesEndpointInDetails()
    {
        // Arrange
        var endpoint = "http://test-qdrant:6334";
        _provider = new QdrantHealthCheckProvider(endpoint, timeoutSeconds: 1);

        // Act
        var result = await _provider.CheckHealthAsync();

        // Assert
        result.Details.Should().ContainKey("endpoint");
        result.Details["endpoint"].Should().Be(endpoint);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        // Arrange
        _provider = new QdrantHealthCheckProvider("http://localhost:6334");

        // Act & Assert - should not throw
        _provider.Dispose();
        var action = () => _provider.Dispose();
        action.Should().NotThrow();
    }

    #endregion
}

/// <summary>
/// Tests for HealthCheckResult static factory methods.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "Database")]
public sealed class HealthCheckResultTests
{
    [Fact]
    public void Healthy_CreatesHealthyResult()
    {
        // Arrange
        var componentName = "TestComponent";
        var responseTime = 50L;
        var details = new Dictionary<string, object> { { "key", "value" } };

        // Act
        var result = HealthCheckResult.Healthy(componentName, responseTime, details);

        // Assert
        result.Status.Should().Be(HealthStatus.Healthy);
        result.ComponentName.Should().Be(componentName);
        result.ResponseTime.Should().Be(responseTime);
        result.Details.Should().ContainKey("key");
        result.Error.Should().BeNull();
    }

    [Fact]
    public void Degraded_CreatesDegradedResult()
    {
        // Arrange
        var componentName = "TestComponent";
        var responseTime = 2500L;
        var warning = "Slow response";

        // Act
        var result = HealthCheckResult.Degraded(componentName, responseTime, null, warning);

        // Assert
        result.Status.Should().Be(HealthStatus.Degraded);
        result.ComponentName.Should().Be(componentName);
        result.ResponseTime.Should().Be(responseTime);
        result.Error.Should().Be(warning);
    }

    [Fact]
    public void Unhealthy_CreatesUnhealthyResult()
    {
        // Arrange
        var componentName = "TestComponent";
        var responseTime = 5000L;
        var error = "Connection refused";

        // Act
        var result = HealthCheckResult.Unhealthy(componentName, responseTime, error);

        // Assert
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.ComponentName.Should().Be(componentName);
        result.ResponseTime.Should().Be(responseTime);
        result.Error.Should().Be(error);
    }

    [Fact]
    public void Constructor_WithNullDetails_CreatesEmptyDictionary()
    {
        // Act
        var result = new HealthCheckResult("Test", HealthStatus.Healthy, 100, null);

        // Assert
        result.Details.Should().NotBeNull();
        result.Details.Should().BeEmpty();
    }

    [Theory]
    [InlineData(HealthStatus.Healthy)]
    [InlineData(HealthStatus.Degraded)]
    [InlineData(HealthStatus.Unhealthy)]
    public void Constructor_WithDifferentStatuses_SetsCorrectly(HealthStatus status)
    {
        // Act
        var result = new HealthCheckResult("Test", status, 100);

        // Assert
        result.Status.Should().Be(status);
    }
}

/// <summary>
/// Tests for HealthStatus enum.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "Database")]
public sealed class HealthStatusTests
{
    [Fact]
    public void HealthStatus_HasExpectedValues()
    {
        // Assert
        Enum.GetValues<HealthStatus>().Should().HaveCount(3);
        Enum.IsDefined(HealthStatus.Healthy).Should().BeTrue();
        Enum.IsDefined(HealthStatus.Degraded).Should().BeTrue();
        Enum.IsDefined(HealthStatus.Unhealthy).Should().BeTrue();
    }

    [Fact]
    public void HealthStatus_ValuesAreOrdered()
    {
        // Assert - Healthy should be best (0), Unhealthy worst (2)
        ((int)HealthStatus.Healthy).Should().BeLessThan((int)HealthStatus.Degraded);
        ((int)HealthStatus.Degraded).Should().BeLessThan((int)HealthStatus.Unhealthy);
    }
}
