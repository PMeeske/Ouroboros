// <copyright file="HealthEndpointsTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Ouroboros.Tests.Infrastructure.Utilities;
using Ouroboros.Tests.WebApi.Fixtures;
using Xunit;

namespace Ouroboros.Tests.WebApi.Controllers;

/// <summary>
/// Integration tests for health check endpoints (/health, /ready).
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("Category", TestCategories.WebApi)]
public class HealthEndpointsTests : IClassFixture<WebApiTestFixture>
{
    private readonly HttpClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="HealthEndpointsTests"/> class.
    /// </summary>
    public HealthEndpointsTests(WebApiTestFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task Health_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, "health endpoint should always return 200 OK when service is running");
    }

    [Fact]
    public async Task Health_ReturnsHealthyStatus()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Healthy", "health check should report healthy status");
    }

    [Fact]
    public async Task Ready_ReturnsOk()
    {
        // Act
        var response = await _client.GetAsync("/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, "ready endpoint should return 200 OK when service is ready");
    }

    [Fact]
    public async Task Ready_ReturnsHealthyStatus()
    {
        // Act
        var response = await _client.GetAsync("/ready");

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Healthy", "readiness check should report healthy status");
    }

    [Fact]
    public async Task Health_ResponseTime_IsFast()
    {
        // Arrange
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        stopwatch.Stop();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "health check should respond quickly");
    }

    [Fact]
    public async Task Ready_ResponseTime_IsFast()
    {
        // Arrange
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Act
        var response = await _client.GetAsync("/ready");

        // Assert
        stopwatch.Stop();
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(1000, "readiness check should respond quickly");
    }

    [Fact]
    public async Task Root_ReturnsServiceInfo()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("Ouroboros", "root endpoint should return service information");
        content.Should().Contain("version", "root endpoint should include version information");
    }

    [Fact]
    public async Task Root_ReturnsEnvironmentInfo()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("environment", "root endpoint should include environment information");
    }

    [Fact]
    public async Task Root_ReturnsEndpointList()
    {
        // Act
        var response = await _client.GetAsync("/");

        // Assert
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Contain("/api/ask", "root endpoint should list available endpoints");
        content.Should().Contain("/api/pipeline", "root endpoint should list available endpoints");
        content.Should().Contain("/api/self/state", "root endpoint should list self-model endpoints");
    }

    [Theory]
    [InlineData("/health")]
    [InlineData("/ready")]
    public async Task HealthEndpoints_SupportMultipleConcurrentRequests(string endpoint)
    {
        // Arrange
        var tasks = Enumerable.Range(0, 10)
            .Select(_ => _client.GetAsync(endpoint))
            .ToArray();

        // Act
        var responses = await Task.WhenAll(tasks);

        // Assert
        responses.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));
    }

    [Fact]
    public async Task Health_CanBeCalledRepeatedly()
    {
        // Act & Assert - Call health endpoint 5 times
        for (int i = 0; i < 5; i++)
        {
            var response = await _client.GetAsync("/health");
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"health check #{i + 1} should succeed");
        }
    }
}
