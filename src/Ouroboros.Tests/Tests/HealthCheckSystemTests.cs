// <copyright file="HealthCheckSystemTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Ouroboros.Core.Infrastructure.HealthCheck;
using Xunit;

/// <summary>
/// Tests for the health check system.
/// </summary>
[Trait("Category", "Unit")]
public class HealthCheckSystemTests
{
    [Fact]
    public void HealthCheckResult_Healthy_CreatesCorrectStatus()
    {
        // Arrange & Act
        HealthCheckResult result = HealthCheckResult.Healthy("TestComponent", 100);

        // Assert
        result.ComponentName.Should().Be("TestComponent");
        result.Status.Should().Be(HealthStatus.Healthy);
        result.ResponseTime.Should().Be(100);
        result.Error.Should().BeNull();
    }

    [Fact]
    public void HealthCheckResult_Unhealthy_IncludesErrorMessage()
    {
        // Arrange & Act
        HealthCheckResult result = HealthCheckResult.Unhealthy(
            "TestComponent",
            200,
            "Service unavailable");

        // Assert
        result.ComponentName.Should().Be("TestComponent");
        result.Status.Should().Be(HealthStatus.Unhealthy);
        result.ResponseTime.Should().Be(200);
        result.Error.Should().Be("Service unavailable");
    }

    [Fact]
    public void HealthCheckResult_Degraded_IncludesWarning()
    {
        // Arrange & Act
        HealthCheckResult result = HealthCheckResult.Degraded(
            "TestComponent",
            300,
            warning: "Slow response");

        // Assert
        result.ComponentName.Should().Be("TestComponent");
        result.Status.Should().Be(HealthStatus.Degraded);
        result.ResponseTime.Should().Be(300);
        result.Error.Should().Be("Slow response");
    }

    [Fact]
    public void HealthCheckReport_AllHealthy_SetsOverallHealthy()
    {
        // Arrange
        List<HealthCheckResult> results = new List<HealthCheckResult>
        {
            HealthCheckResult.Healthy("Service1", 100),
            HealthCheckResult.Healthy("Service2", 150),
        };

        // Act
        HealthCheckReport report = new HealthCheckReport(results, 250);

        // Assert
        report.OverallStatus.Should().Be(HealthStatus.Healthy);
        report.IsHealthy.Should().BeTrue();
        report.IsReady.Should().BeTrue();
    }

    [Fact]
    public void HealthCheckReport_OneDegraded_SetsOverallDegraded()
    {
        // Arrange
        List<HealthCheckResult> results = new List<HealthCheckResult>
        {
            HealthCheckResult.Healthy("Service1", 100),
            HealthCheckResult.Degraded("Service2", 500, warning: "Slow"),
        };

        // Act
        HealthCheckReport report = new HealthCheckReport(results, 600);

        // Assert
        report.OverallStatus.Should().Be(HealthStatus.Degraded);
        report.IsHealthy.Should().BeFalse();
        report.IsReady.Should().BeTrue();
    }

    [Fact]
    public void HealthCheckReport_OneUnhealthy_SetsOverallUnhealthy()
    {
        // Arrange
        List<HealthCheckResult> results = new List<HealthCheckResult>
        {
            HealthCheckResult.Healthy("Service1", 100),
            HealthCheckResult.Unhealthy("Service2", 0, "Connection failed"),
        };

        // Act
        HealthCheckReport report = new HealthCheckReport(results, 100);

        // Assert
        report.OverallStatus.Should().Be(HealthStatus.Unhealthy);
        report.IsHealthy.Should().BeFalse();
        report.IsReady.Should().BeFalse();
    }

    [Fact]
    public async Task HealthCheckAggregator_RunsAllProviders()
    {
        // Arrange
        MockHealthCheckProvider provider1 = new MockHealthCheckProvider("Service1", HealthStatus.Healthy);
        MockHealthCheckProvider provider2 = new MockHealthCheckProvider("Service2", HealthStatus.Healthy);

        HealthCheckAggregator aggregator = new HealthCheckAggregator(new[] { provider1, provider2 });

        // Act
        HealthCheckReport report = await aggregator.CheckHealthAsync();

        // Assert
        report.Results.Should().HaveCount(2);
        report.Results.Should().Contain(r => r.ComponentName == "Service1");
        report.Results.Should().Contain(r => r.ComponentName == "Service2");
    }

    [Fact]
    public async Task HealthCheckAggregator_HandlesProviderException()
    {
        // Arrange
        MockHealthCheckProvider goodProvider = new MockHealthCheckProvider("GoodService", HealthStatus.Healthy);
        FailingHealthCheckProvider failingProvider = new FailingHealthCheckProvider("FailingService");

        HealthCheckAggregator aggregator = new HealthCheckAggregator(new IHealthCheckProvider[] { goodProvider, failingProvider });

        // Act
        HealthCheckReport report = await aggregator.CheckHealthAsync();

        // Assert
        report.Results.Should().HaveCount(2);
        report.Results.First(r => r.ComponentName == "FailingService").Status.Should().Be(HealthStatus.Unhealthy);
        report.Results.First(r => r.ComponentName == "FailingService").Error.Should().Contain("Health check failed");
    }

    [Fact]
    public void HealthCheckAggregator_RegisterProvider_AddsProvider()
    {
        // Arrange
        MockHealthCheckProvider provider = new MockHealthCheckProvider("Service", HealthStatus.Healthy);
        HealthCheckAggregator aggregator = new HealthCheckAggregator(Array.Empty<IHealthCheckProvider>());

        // Act
        aggregator.RegisterProvider(provider);
        HealthCheckReport report = aggregator.CheckHealthAsync().Result;

        // Assert
        report.Results.Should().HaveCount(1);
        report.Results.First().ComponentName.Should().Be("Service");
    }

    /// <summary>
    /// Mock health check provider for testing.
    /// </summary>
    private sealed class MockHealthCheckProvider : IHealthCheckProvider
    {
        private readonly HealthStatus status;

        public MockHealthCheckProvider(string componentName, HealthStatus status)
        {
            this.ComponentName = componentName;
            this.status = status;
        }

        public string ComponentName { get; }

        public Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new HealthCheckResult(
                this.ComponentName,
                this.status,
                100));
        }
    }

    /// <summary>
    /// Failing health check provider for testing exception handling.
    /// </summary>
    private sealed class FailingHealthCheckProvider : IHealthCheckProvider
    {
        public FailingHealthCheckProvider(string componentName)
        {
            this.ComponentName = componentName;
        }

        public string ComponentName { get; }

        public Task<HealthCheckResult> CheckHealthAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated health check failure");
        }
    }
}
