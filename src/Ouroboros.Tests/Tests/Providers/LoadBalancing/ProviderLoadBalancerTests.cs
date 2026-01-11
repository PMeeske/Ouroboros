// <copyright file="ProviderLoadBalancerTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using FluentAssertions;
using Ouroboros.Providers.LoadBalancing;
using Xunit;

namespace Ouroboros.Tests.Providers.LoadBalancing;

/// <summary>
/// Comprehensive unit tests for the ProviderLoadBalancer class.
/// Tests all rotation strategies, health tracking, rate limiting, and circuit breaker functionality.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ProviderLoadBalancerTests
{
    /// <summary>
    /// Test that load balancer can be created with different strategies.
    /// </summary>
    [Fact]
    public void Constructor_WithStrategy_ShouldSetStrategy()
    {
        // Arrange & Act
        var lb1 = new ProviderLoadBalancer<IChatCompletionModel>(ProviderSelectionStrategies.RoundRobin);
        var lb2 = new ProviderLoadBalancer<IChatCompletionModel>(ProviderSelectionStrategies.WeightedRandom);
        var lb3 = new ProviderLoadBalancer<IChatCompletionModel>(ProviderSelectionStrategies.LeastLatency);
        var lb4 = new ProviderLoadBalancer<IChatCompletionModel>(ProviderSelectionStrategies.AdaptiveHealth);

        // Assert
        lb1.Strategy.Name.Should().Be("RoundRobin");
        lb1.ProviderCount.Should().Be(0);
        lb1.HealthyProviderCount.Should().Be(0);

        lb2.Strategy.Name.Should().Be("WeightedRandom");
        lb3.Strategy.Name.Should().Be("LeastLatency");
        lb4.Strategy.Name.Should().Be("AdaptiveHealth");
    }

    /// <summary>
    /// Test that providers can be registered successfully.
    /// </summary>
    [Fact]
    public void RegisterProvider_WithValidProvider_ShouldRegister()
    {
        // Arrange
        var loadBalancer = new ProviderLoadBalancer<IChatCompletionModel>();
        var provider = new MockChatModel("test-provider");

        // Act
        loadBalancer.RegisterProvider("provider-1", provider);

        // Assert
        loadBalancer.ProviderCount.Should().Be(1);
        loadBalancer.HealthyProviderCount.Should().Be(1);
    }

    /// <summary>
    /// Test that registering multiple providers works correctly.
    /// </summary>
    [Fact]
    public void RegisterProvider_WithMultipleProviders_ShouldRegisterAll()
    {
        // Arrange
        var loadBalancer = new ProviderLoadBalancer<IChatCompletionModel>();

        // Act
        loadBalancer.RegisterProvider("provider-1", new MockChatModel("p1"));
        loadBalancer.RegisterProvider("provider-2", new MockChatModel("p2"));
        loadBalancer.RegisterProvider("provider-3", new MockChatModel("p3"));

        // Assert
        loadBalancer.ProviderCount.Should().Be(3);
        loadBalancer.HealthyProviderCount.Should().Be(3);
    }

    /// <summary>
    /// Test that registering provider with null throws ArgumentNullException.
    /// </summary>
    [Fact]
    public void RegisterProvider_WithNullProvider_ShouldThrow()
    {
        // Arrange
        var loadBalancer = new ProviderLoadBalancer<IChatCompletionModel>();

        // Act
        Action act = () => loadBalancer.RegisterProvider("provider-1", null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    /// <summary>
    /// Test that registering provider with empty ID throws ArgumentException.
    /// </summary>
    [Fact]
    public void RegisterProvider_WithEmptyId_ShouldThrow()
    {
        // Arrange
        var loadBalancer = new ProviderLoadBalancer<IChatCompletionModel>();
        var provider = new MockChatModel("test");

        // Act
        Action act = () => loadBalancer.RegisterProvider("", provider);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Test that selecting provider fails when no providers are registered.
    /// </summary>
    [Fact]
    public async Task SelectProviderAsync_WithNoProviders_ShouldFail()
    {
        // Arrange
        var loadBalancer = new ProviderLoadBalancer<IChatCompletionModel>();

        // Act
        var result = await loadBalancer.SelectProviderAsync();

        // Assert
        result.IsFailure.Should().BeTrue();
        string errorMsg = result.Match(_ => string.Empty, error => error);
        errorMsg.Should().Contain("No providers registered");
    }

    /// <summary>
    /// Test RoundRobin strategy distributes load evenly.
    /// </summary>
    [Fact]
    public async Task SelectProviderAsync_WithRoundRobin_ShouldDistributeEvenly()
    {
        // Arrange
        var loadBalancer = new ProviderLoadBalancer<IChatCompletionModel>(ProviderSelectionStrategies.RoundRobin);
        loadBalancer.RegisterProvider("p1", new MockChatModel("p1"));
        loadBalancer.RegisterProvider("p2", new MockChatModel("p2"));
        loadBalancer.RegisterProvider("p3", new MockChatModel("p3"));

        // Act - Select 6 times to verify round-robin
        var selections = new List<string>();
        for (int i = 0; i < 6; i++)
        {
            var result = await loadBalancer.SelectProviderAsync();
            result.Match(
                selection => selections.Add(selection.ProviderId),
                _ => { });
        }

        // Assert
        selections.Should().HaveCount(6);
        // Should cycle through p1, p2, p3, p1, p2, p3
        selections[0].Should().Be(selections[3]);
        selections[1].Should().Be(selections[4]);
        selections[2].Should().Be(selections[5]);
    }

    /// <summary>
    /// Test LeastLatency strategy selects provider with lowest latency.
    /// </summary>
    [Fact]
    public async Task SelectProviderAsync_WithLeastLatency_ShouldSelectFastest()
    {
        // Arrange
        var loadBalancer = new ProviderLoadBalancer<IChatCompletionModel>(ProviderSelectionStrategies.LeastLatency);
        loadBalancer.RegisterProvider("slow", new MockChatModel("slow"));
        loadBalancer.RegisterProvider("fast", new MockChatModel("fast"));
        loadBalancer.RegisterProvider("medium", new MockChatModel("medium"));

        // Record metrics
        loadBalancer.RecordExecution("slow", 1000, true);
        loadBalancer.RecordExecution("fast", 100, true);
        loadBalancer.RecordExecution("medium", 500, true);

        // Act
        var result = await loadBalancer.SelectProviderAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Match(
            selection => selection.ProviderId.Should().Be("fast"),
            _ => { });
    }

    /// <summary>
    /// Test AdaptiveHealth strategy selects provider with best health score.
    /// </summary>
    [Fact]
    public async Task SelectProviderAsync_WithAdaptiveHealth_ShouldSelectHealthiest()
    {
        // Arrange
        var loadBalancer = new ProviderLoadBalancer<IChatCompletionModel>(ProviderSelectionStrategies.AdaptiveHealth);
        loadBalancer.RegisterProvider("unreliable", new MockChatModel("unreliable"));
        loadBalancer.RegisterProvider("reliable", new MockChatModel("reliable"));

        // Record metrics - unreliable has failures
        loadBalancer.RecordExecution("unreliable", 100, false);
        loadBalancer.RecordExecution("unreliable", 100, true);
        loadBalancer.RecordExecution("unreliable", 100, false);

        // Record metrics - reliable is consistent
        loadBalancer.RecordExecution("reliable", 200, true);
        loadBalancer.RecordExecution("reliable", 200, true);
        loadBalancer.RecordExecution("reliable", 200, true);

        // Act
        var result = await loadBalancer.SelectProviderAsync();

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Match(
            selection => selection.ProviderId.Should().Be("reliable"),
            _ => { });
    }

    /// <summary>
    /// Test that recording rate limit marks provider with cooldown.
    /// </summary>
    [Fact]
    public void RecordExecution_WithRateLimit_ShouldApplyCooldown()
    {
        // Arrange
        var loadBalancer = new ProviderLoadBalancer<IChatCompletionModel>();
        loadBalancer.RegisterProvider("provider-1", new MockChatModel("p1"));

        // Act
        loadBalancer.RecordExecution("provider-1", 100, false, wasRateLimited: true);

        // Assert
        var health = loadBalancer.GetHealthStatus();
        health["provider-1"].IsInCooldown.Should().BeTrue();
        health["provider-1"].CooldownUntil.Should().NotBeNull();
        health["provider-1"].CooldownUntil!.Value.Should().BeAfter(DateTime.UtcNow);
    }

    /// <summary>
    /// Test that consecutive failures trigger circuit breaker.
    /// </summary>
    [Fact]
    public void RecordExecution_WithConsecutiveFailures_ShouldMarkUnhealthy()
    {
        // Arrange
        var loadBalancer = new ProviderLoadBalancer<IChatCompletionModel>();
        loadBalancer.RegisterProvider("provider-1", new MockChatModel("p1"));

        // Act - Record 3 consecutive failures
        loadBalancer.RecordExecution("provider-1", 100, false);
        loadBalancer.RecordExecution("provider-1", 100, false);
        loadBalancer.RecordExecution("provider-1", 100, false);

        // Assert
        var health = loadBalancer.GetHealthStatus();
        health["provider-1"].IsHealthy.Should().BeFalse();
        health["provider-1"].ConsecutiveFailures.Should().Be(3);
    }

    /// <summary>
    /// Test that success resets consecutive failures counter.
    /// </summary>
    [Fact]
    public void RecordExecution_WithSuccessAfterFailures_ShouldResetCounter()
    {
        // Arrange
        var loadBalancer = new ProviderLoadBalancer<IChatCompletionModel>();
        loadBalancer.RegisterProvider("provider-1", new MockChatModel("p1"));

        // Act - Record failures then success
        loadBalancer.RecordExecution("provider-1", 100, false);
        loadBalancer.RecordExecution("provider-1", 100, false);
        loadBalancer.RecordExecution("provider-1", 100, true);

        // Assert
        var health = loadBalancer.GetHealthStatus();
        health["provider-1"].ConsecutiveFailures.Should().Be(0);
        health["provider-1"].IsHealthy.Should().BeTrue();
    }

    /// <summary>
    /// Test that manually marking provider unhealthy works.
    /// </summary>
    [Fact]
    public void MarkProviderUnhealthy_ShouldSetUnhealthy()
    {
        // Arrange
        var loadBalancer = new ProviderLoadBalancer<IChatCompletionModel>();
        loadBalancer.RegisterProvider("provider-1", new MockChatModel("p1"));

        // Act
        loadBalancer.MarkProviderUnhealthy("provider-1");

        // Assert
        var health = loadBalancer.GetHealthStatus();
        health["provider-1"].IsHealthy.Should().BeFalse();
        loadBalancer.HealthyProviderCount.Should().Be(0);
    }

    /// <summary>
    /// Test that manually marking provider healthy works.
    /// </summary>
    [Fact]
    public void MarkProviderHealthy_AfterUnhealthy_ShouldSetHealthy()
    {
        // Arrange
        var loadBalancer = new ProviderLoadBalancer<IChatCompletionModel>();
        loadBalancer.RegisterProvider("provider-1", new MockChatModel("p1"));
        loadBalancer.MarkProviderUnhealthy("provider-1");

        // Act
        loadBalancer.MarkProviderHealthy("provider-1");

        // Assert
        var health = loadBalancer.GetHealthStatus();
        health["provider-1"].IsHealthy.Should().BeTrue();
        health["provider-1"].ConsecutiveFailures.Should().Be(0);
        loadBalancer.HealthyProviderCount.Should().Be(1);
    }

    /// <summary>
    /// Test that health score calculation works correctly.
    /// </summary>
    [Fact]
    public void ProviderHealthStatus_HealthScore_ShouldCalculateCorrectly()
    {
        // Arrange
        var healthyStatus = new ProviderHealthStatus(
            ProviderId: "test",
            IsHealthy: true,
            SuccessRate: 0.9,
            AverageLatencyMs: 500,
            ConsecutiveFailures: 0,
            LastFailureTime: null,
            CooldownUntil: null,
            TotalRequests: 10,
            SuccessfulRequests: 9,
            LastChecked: DateTime.UtcNow);

        // Act
        double healthScore = healthyStatus.HealthScore;

        // Assert
        healthScore.Should().BeGreaterThan(0.5);
        healthScore.Should().BeLessThanOrEqualTo(1.0);
    }

    /// <summary>
    /// Test that unhealthy provider has zero health score.
    /// </summary>
    [Fact]
    public void ProviderHealthStatus_HealthScore_WithUnhealthy_ShouldBeZero()
    {
        // Arrange
        var unhealthyStatus = new ProviderHealthStatus(
            ProviderId: "test",
            IsHealthy: false,
            SuccessRate: 0.5,
            AverageLatencyMs: 500,
            ConsecutiveFailures: 5,
            LastFailureTime: DateTime.UtcNow,
            CooldownUntil: null,
            TotalRequests: 10,
            SuccessfulRequests: 5,
            LastChecked: DateTime.UtcNow);

        // Act
        double healthScore = unhealthyStatus.HealthScore;

        // Assert
        healthScore.Should().Be(0.0);
    }

    /// <summary>
    /// Test that provider in cooldown has zero health score.
    /// </summary>
    [Fact]
    public void ProviderHealthStatus_HealthScore_WithCooldown_ShouldBeZero()
    {
        // Arrange
        var cooldownStatus = new ProviderHealthStatus(
            ProviderId: "test",
            IsHealthy: true,
            SuccessRate: 1.0,
            AverageLatencyMs: 100,
            ConsecutiveFailures: 0,
            LastFailureTime: null,
            CooldownUntil: DateTime.UtcNow.AddMinutes(1),
            TotalRequests: 10,
            SuccessfulRequests: 10,
            LastChecked: DateTime.UtcNow);

        // Act
        double healthScore = cooldownStatus.HealthScore;

        // Assert
        healthScore.Should().Be(0.0);
        cooldownStatus.IsInCooldown.Should().BeTrue();
    }

    /// <summary>
    /// Test that success rate is tracked correctly.
    /// </summary>
    [Fact]
    public void RecordExecution_ShouldTrackSuccessRate()
    {
        // Arrange
        var loadBalancer = new ProviderLoadBalancer<IChatCompletionModel>();
        loadBalancer.RegisterProvider("provider-1", new MockChatModel("p1"));

        // Act - Record 7 successes and 3 failures
        for (int i = 0; i < 7; i++)
            loadBalancer.RecordExecution("provider-1", 100, true);
        for (int i = 0; i < 3; i++)
            loadBalancer.RecordExecution("provider-1", 100, false);

        // Assert
        var health = loadBalancer.GetHealthStatus();
        health["provider-1"].TotalRequests.Should().Be(10);
        health["provider-1"].SuccessfulRequests.Should().Be(7);
        health["provider-1"].SuccessRate.Should().BeApproximately(0.7, 0.01);
    }

    /// <summary>
    /// Test that unregistering provider works.
    /// </summary>
    [Fact]
    public void UnregisterProvider_ShouldRemoveProvider()
    {
        // Arrange
        var loadBalancer = new ProviderLoadBalancer<IChatCompletionModel>();
        loadBalancer.RegisterProvider("provider-1", new MockChatModel("p1"));
        loadBalancer.RegisterProvider("provider-2", new MockChatModel("p2"));

        // Act
        bool removed = loadBalancer.UnregisterProvider("provider-1");

        // Assert
        removed.Should().BeTrue();
        loadBalancer.ProviderCount.Should().Be(1);
    }
}
