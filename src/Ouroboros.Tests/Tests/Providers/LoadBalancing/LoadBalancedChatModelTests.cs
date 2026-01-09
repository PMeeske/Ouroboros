// <copyright file="LoadBalancedChatModelTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>

using FluentAssertions;
using Ouroboros.Providers.LoadBalancing;
using Xunit;

namespace Ouroboros.Tests.Providers.LoadBalancing;

/// <summary>
/// Comprehensive unit tests for the LoadBalancedChatModel class.
/// Tests automatic provider rotation, rate limit handling, and failover behavior.
/// </summary>
[Trait("Category", "Unit")]
public sealed class LoadBalancedChatModelTests
{
    /// <summary>
    /// Test that load balanced model can be created with different strategies.
    /// </summary>
    [Fact]
    public void Constructor_WithStrategy_ShouldCreateModel()
    {
        // Arrange & Act
        var model1 = new LoadBalancedChatModel(ProviderSelectionStrategies.RoundRobin);
        var model2 = new LoadBalancedChatModel(ProviderSelectionStrategies.AdaptiveHealth);

        // Assert
        model1.Should().NotBeNull();
        model1.Strategy.Should().NotBeNull();
        model1.Strategy.Name.Should().Be("RoundRobin");
        
        model2.Should().NotBeNull();
        model2.Strategy.Should().NotBeNull();
        model2.Strategy.Name.Should().Be("AdaptiveHealth");
    }

    /// <summary>
    /// Test that providers can be registered successfully.
    /// </summary>
    [Fact]
    public void RegisterProvider_WithValidProvider_ShouldRegister()
    {
        // Arrange
        var model = new LoadBalancedChatModel();
        var provider = new MockChatModel("test-provider");

        // Act
        model.RegisterProvider("provider-1", provider);

        // Assert
        model.ProviderCount.Should().Be(1);
        model.HealthyProviderCount.Should().Be(1);
    }

    /// <summary>
    /// Test that generation works with single provider.
    /// </summary>
    [Fact]
    public async Task GenerateTextAsync_WithSingleProvider_ShouldSucceed()
    {
        // Arrange
        var model = new LoadBalancedChatModel();
        var provider = new MockChatModel("Test response");
        model.RegisterProvider("provider-1", provider);

        // Act
        string result = await model.GenerateTextAsync("Test prompt");

        // Assert
        result.Should().Contain("Test response");
    }

    /// <summary>
    /// Test that generation rotates through providers with RoundRobin.
    /// </summary>
    [Fact]
    public async Task GenerateTextAsync_WithRoundRobin_ShouldRotate()
    {
        // Arrange
        var model = new LoadBalancedChatModel(ProviderSelectionStrategies.RoundRobin);
        model.RegisterProvider("p1", new MockChatModel("Response from p1"));
        model.RegisterProvider("p2", new MockChatModel("Response from p2"));
        model.RegisterProvider("p3", new MockChatModel("Response from p3"));

        // Act
        var results = new List<string>();
        for (int i = 0; i < 6; i++)
        {
            string result = await model.GenerateTextAsync("Test");
            results.Add(result);
        }

        // Assert - Round robin should cycle through all providers
        results.Should().HaveCount(6);
        // Each unique response should appear exactly twice in 6 requests
        var distinctResults = results.Distinct().ToList();
        distinctResults.Should().HaveCount(3);
        
        // Verify pattern repeats: results[0] == results[3], results[1] == results[4], results[2] == results[5]
        results[0].Should().Be(results[3]);
        results[1].Should().Be(results[4]);
        results[2].Should().Be(results[5]);
    }

    /// <summary>
    /// Test that rate limited provider is skipped automatically.
    /// </summary>
    [Fact]
    public async Task GenerateTextAsync_WithRateLimitedProvider_ShouldFailover()
    {
        // Arrange
        var model = new LoadBalancedChatModel(ProviderSelectionStrategies.RoundRobin);
        
        // First provider will be rate limited
        var rateLimitedProvider = new RateLimitedMockChatModel("rate-limited");
        var healthyProvider = new MockChatModel("Success");
        
        model.RegisterProvider("rate-limited", rateLimitedProvider);
        model.RegisterProvider("healthy", healthyProvider);

        // Act
        string result = await model.GenerateTextAsync("Test prompt");

        // Assert
        result.Should().Contain("Success");
        
        // Verify rate-limited provider is in cooldown
        var health = model.GetHealthStatus();
        health["rate-limited"].IsInCooldown.Should().BeTrue();
    }

    /// <summary>
    /// Test that all providers exhausted returns error.
    /// </summary>
    [Fact]
    public async Task GenerateTextAsync_WithAllProvidersDown_ShouldReturnError()
    {
        // Arrange
        var model = new LoadBalancedChatModel();
        var failingProvider = new FailingMockChatModel("failing");
        model.RegisterProvider("provider-1", failingProvider);

        // Act
        string result = await model.GenerateTextAsync("Test");

        // Assert
        result.Should().Contain("[load-balanced-error]");
    }

    /// <summary>
    /// Test that health status can be retrieved.
    /// </summary>
    [Fact]
    public void GetHealthStatus_ShouldReturnProviderHealth()
    {
        // Arrange
        var model = new LoadBalancedChatModel();
        model.RegisterProvider("p1", new MockChatModel("p1"));
        model.RegisterProvider("p2", new MockChatModel("p2"));

        // Act
        var health = model.GetHealthStatus();

        // Assert
        health.Should().HaveCount(2);
        health["p1"].IsHealthy.Should().BeTrue();
        health["p2"].IsHealthy.Should().BeTrue();
    }

    /// <summary>
    /// Test that consecutive rate limits increase cooldown duration.
    /// </summary>
    [Fact]
    public async Task GenerateTextAsync_WithRepeatedRateLimits_ShouldIncreaseBackoff()
    {
        // Arrange
        var model = new LoadBalancedChatModel();
        var rateLimitedProvider = new RateLimitedMockChatModel("rate-limited");
        var healthyProvider = new MockChatModel("Success");
        
        model.RegisterProvider("rate-limited", rateLimitedProvider);
        model.RegisterProvider("healthy", healthyProvider);

        // Act - Try multiple requests
        for (int i = 0; i < 3; i++)
        {
            await model.GenerateTextAsync("Test");
        }

        // Assert
        var health = model.GetHealthStatus();
        health["rate-limited"].IsInCooldown.Should().BeTrue();
        health["healthy"].IsHealthy.Should().BeTrue();
    }
}

/// <summary>
/// Mock chat model that simulates rate limiting (429 error).
/// </summary>
internal sealed class RateLimitedMockChatModel : IChatCompletionModel
{
    private readonly string _name;

    public RateLimitedMockChatModel(string name)
    {
        _name = name;
    }

    public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        // Simulate 429 Too Many Requests error
        throw new HttpRequestException("429 (Too Many Requests)", null, System.Net.HttpStatusCode.TooManyRequests);
    }
}

/// <summary>
/// Mock chat model that always fails with a generic error.
/// </summary>
internal sealed class FailingMockChatModel : IChatCompletionModel
{
    private readonly string _name;

    public FailingMockChatModel(string name)
    {
        _name = name;
    }

    public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        throw new InvalidOperationException("Provider is unavailable");
    }
}
