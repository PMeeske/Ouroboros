// <copyright file="SelfModelEndpointsTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Ouroboros.Tests.Infrastructure.Assertions;
using Ouroboros.Tests.Infrastructure.Utilities;
using Ouroboros.Tests.WebApi.Fixtures;
using Ouroboros.WebApi.Models;
using Xunit;

namespace Ouroboros.Tests.WebApi.Controllers;

/// <summary>
/// Integration tests for the self-model endpoints (/api/self/*).
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("Category", TestCategories.WebApi)]
public class SelfModelEndpointsTests : IClassFixture<WebApiTestFixture>
{
    private readonly HttpClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="SelfModelEndpointsTests"/> class.
    /// </summary>
    public SelfModelEndpointsTests(WebApiTestFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    #region /api/self/state Tests

    [Fact]
    public async Task GetState_ReturnsOkWithAgentState()
    {
        // Act
        var response = await _client.GetAsync("/api/self/state");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<SelfStateResponse>>();
        result.Should().BeSuccessful();
        result!.Data.Should().NotBeNull();
        result.Data!.AgentId.Should().NotBeEmpty();
        result.Data.Name.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetState_ReturnsCapabilityCount()
    {
        // Act
        var response = await _client.GetAsync("/api/self/state");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<SelfStateResponse>>();
        result!.Data!.CapabilityCount.Should().BeGreaterThan(0, "agent should have capabilities");
    }

    [Fact]
    public async Task GetState_ReturnsResources()
    {
        // Act
        var response = await _client.GetAsync("/api/self/state");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<SelfStateResponse>>();
        result!.Data!.Resources.Should().NotBeNull();
        result.Data.Resources.Should().ContainKey("CPU");
        result.Data.Resources.Should().ContainKey("Memory");
    }

    [Fact]
    public async Task GetState_ReturnsCommitments()
    {
        // Act
        var response = await _client.GetAsync("/api/self/state");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<SelfStateResponse>>();
        result!.Data!.Commitments.Should().NotBeNull();
    }

    [Fact]
    public async Task GetState_ReturnsPerformanceMetrics()
    {
        // Act
        var response = await _client.GetAsync("/api/self/state");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<SelfStateResponse>>();
        result!.Data!.Performance.Should().NotBeNull();
        result.Data.Performance.OverallSuccessRate.Should().BeInRange(0, 1);
        result.Data.Performance.TotalTasks.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetState_IncludesTimestamp()
    {
        // Act
        var response = await _client.GetAsync("/api/self/state");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<SelfStateResponse>>();
        result!.Data!.StateTimestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(5));
    }

    #endregion

    #region /api/self/forecast Tests

    [Fact]
    public async Task GetForecasts_ReturnsOkWithForecasts()
    {
        // Act
        var response = await _client.GetAsync("/api/self/forecast");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<SelfForecastResponse>>();
        result.Should().BeSuccessful();
        result!.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task GetForecasts_ReturnsPendingForecasts()
    {
        // Act
        var response = await _client.GetAsync("/api/self/forecast");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<SelfForecastResponse>>();
        result!.Data!.PendingForecasts.Should().NotBeNull();
    }

    [Fact]
    public async Task GetForecasts_ReturnsCalibrationMetrics()
    {
        // Act
        var response = await _client.GetAsync("/api/self/forecast");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<SelfForecastResponse>>();
        result!.Data!.Calibration.Should().NotBeNull();
        result.Data.Calibration.AverageConfidence.Should().BeInRange(0, 1);
        result.Data.Calibration.AverageAccuracy.Should().BeInRange(0, 1);
    }

    [Fact]
    public async Task GetForecasts_ReturnsRecentAnomalies()
    {
        // Act
        var response = await _client.GetAsync("/api/self/forecast");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<SelfForecastResponse>>();
        result!.Data!.RecentAnomalies.Should().NotBeNull();
    }

    #endregion

    #region /api/self/commitments Tests

    [Fact]
    public async Task GetCommitments_ReturnsOkWithCommitments()
    {
        // Act
        var response = await _client.GetAsync("/api/self/commitments");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<CommitmentDto>>>();
        result.Should().BeSuccessful();
        result!.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task GetCommitments_ReturnsCommitmentDetails()
    {
        // Act
        var response = await _client.GetAsync("/api/self/commitments");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<CommitmentDto>>>();

        if (result!.Data!.Count > 0)
        {
            var commitment = result.Data[0];
            commitment.Id.Should().NotBeEmpty();
            commitment.Description.Should().NotBeNullOrEmpty();
            commitment.Status.Should().NotBeNullOrEmpty();
            commitment.ProgressPercent.Should().BeInRange(0, 100);
        }
    }

    #endregion

    #region /api/self/explain Tests

    [Fact]
    public async Task Explain_WithValidRequest_ReturnsOkWithExplanation()
    {
        // Arrange
        var request = new SelfExplainRequest
        {
            IncludeContext = true,
            MaxDepth = 10
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/self/explain", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<SelfExplainResponse>>();
        result.Should().BeSuccessful();
        result!.Data.Should().NotBeNull();
    }

    [Fact]
    public async Task Explain_ReturnsNarrative()
    {
        // Arrange
        var request = new SelfExplainRequest { IncludeContext = true };

        // Act
        var response = await _client.PostAsJsonAsync("/api/self/explain", request);

        // Assert
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<SelfExplainResponse>>();
        result!.Data!.Narrative.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Explain_ReturnsDagSummary()
    {
        // Arrange
        var request = new SelfExplainRequest { IncludeContext = true };

        // Act
        var response = await _client.PostAsJsonAsync("/api/self/explain", request);

        // Assert
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<SelfExplainResponse>>();
        result!.Data!.DagSummary.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Explain_ReturnsKeyEvents()
    {
        // Arrange
        var request = new SelfExplainRequest { IncludeContext = true, MaxDepth = 5 };

        // Act
        var response = await _client.PostAsJsonAsync("/api/self/explain", request);

        // Assert
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<SelfExplainResponse>>();
        result!.Data!.KeyEvents.Should().NotBeNull();
    }

    #endregion

    #region Response Time Tests

    [Fact]
    public async Task GetState_Response_IncludesExecutionTime()
    {
        // Act
        var response = await _client.GetAsync("/api/self/state");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<SelfStateResponse>>();
        result.Should().HaveExecutionTimeLessThan(5000, "state endpoint should be fast");
    }

    [Fact]
    public async Task GetForecasts_Response_IncludesExecutionTime()
    {
        // Act
        var response = await _client.GetAsync("/api/self/forecast");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<SelfForecastResponse>>();
        result.Should().HaveExecutionTimeLessThan(5000, "forecast endpoint should be fast");
    }

    [Fact]
    public async Task GetCommitments_Response_IncludesExecutionTime()
    {
        // Act
        var response = await _client.GetAsync("/api/self/commitments");

        // Assert
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<List<CommitmentDto>>>();
        result.Should().HaveExecutionTimeLessThan(5000, "commitments endpoint should be fast");
    }

    #endregion
}
