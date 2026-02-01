// <copyright file="PipelineEndpointTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Ouroboros.Tests.Infrastructure.Assertions;
using Ouroboros.Tests.Infrastructure.Builders;
using Ouroboros.Tests.Infrastructure.Utilities;
using Ouroboros.Tests.WebApi.Fixtures;
using Ouroboros.WebApi.Models;
using Xunit;

namespace Ouroboros.Tests.WebApi.Controllers;

/// <summary>
/// Integration tests for the /api/pipeline endpoint.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("Category", TestCategories.WebApi)]
public class PipelineEndpointTests : IClassFixture<WebApiTestFixture>
{
    private readonly HttpClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="PipelineEndpointTests"/> class.
    /// </summary>
    public PipelineEndpointTests(WebApiTestFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task Pipeline_WithValidDsl_ReturnsOkWithResult()
    {
        // Arrange
        var request = new PipelineRequestBuilder()
            .WithDsl("SetTopic('AI') | UseDraft")
            .WithModel("llama3")
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/pipeline", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PipelineResponse>>();
        result.Should().BeSuccessful();
        result!.Data.Should().NotBeNull();
        result.Data!.Result.Should().NotBeNullOrEmpty();
        result.Data.FinalState.Should().Be("Completed");
    }

    [Fact]
    public async Task Pipeline_WithDebugMode_ReturnsDebugPrefixedResult()
    {
        // Arrange
        var request = new PipelineRequestBuilder()
            .WithDsl("SetTopic('Testing') | UseDraft")
            .WithDebug()
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/pipeline", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PipelineResponse>>();
        result.Should()
            .BeSuccessful()
            .And.HaveData();

        result!.Data!.Result.Should().Contain("[DEBUG]", "debug mode should be indicated");
    }

    [Fact]
    public async Task Pipeline_WithComplexDsl_ExecutesSuccessfully()
    {
        // Arrange
        var request = new PipelineRequestBuilder()
            .WithDsl("SetTopic('Functional Programming') | UseDraft | UseCritique | UseImprove")
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/pipeline", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PipelineResponse>>();
        result.Should().BeSuccessful();
    }

    [Fact]
    public async Task Pipeline_WithCustomTemperature_ExecutesSuccessfully()
    {
        // Arrange
        var request = new PipelineRequestBuilder()
            .WithDsl("SetTopic('Machine Learning') | UseDraft")
            .WithTemperature(0.8f)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/pipeline", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PipelineResponse>>();
        result.Should().BeSuccessful();
    }

    [Fact]
    public async Task Pipeline_WithMaxTokens_ExecutesSuccessfully()
    {
        // Arrange
        var request = new PipelineRequestBuilder()
            .WithDsl("SetTopic('Software Architecture') | UseDraft")
            .WithMaxTokens(1000)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/pipeline", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PipelineResponse>>();
        result.Should().BeSuccessful();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Pipeline_WithEmptyDsl_ReturnsBadRequest(string? dsl)
    {
        // Arrange
        var request = new { Dsl = dsl };

        // Act
        var response = await _client.PostAsJsonAsync("/api/pipeline", request);

        // Assert - Should fail validation
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Pipeline_WithGeneratedTestData_ExecutesSuccessfully()
    {
        // Arrange
        var request = TestDataGenerator.GeneratePipelineRequest();

        // Act
        var response = await _client.PostAsJsonAsync("/api/pipeline", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PipelineResponse>>();
        result.Should().BeSuccessful();
    }

    [Fact]
    public async Task Pipeline_Response_IncludesExecutionTime()
    {
        // Arrange
        var request = new PipelineRequestBuilder().Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/pipeline", request);

        // Assert
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PipelineResponse>>();
        result!.ExecutionTimeMs.Should().HaveValue();
        result.Should().HaveExecutionTimeLessThan(30000, "API should respond within 30 seconds");
    }

    [Theory]
    [InlineData("SetTopic('AI') | UseDraft")]
    [InlineData("SetTopic('DevOps') | UseDraft | UseCritique")]
    [InlineData("SetTopic('Security') | UseDraft | UseRefine")]
    public async Task Pipeline_WithVariousDslExpressions_ExecutesSuccessfully(string dsl)
    {
        // Arrange
        var request = new PipelineRequestBuilder()
            .WithDsl(dsl)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/pipeline", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, $"DSL '{dsl}' should execute successfully");

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PipelineResponse>>();
        result.Should().BeSuccessful();
    }

    [Fact]
    public async Task Pipeline_WithRemoteEndpoint_ExecutesSuccessfully()
    {
        // Arrange
        var request = new PipelineRequestBuilder()
            .WithDsl("SetTopic('Cloud Computing') | UseDraft")
            .WithEndpoint("https://api.example.com", "test-api-key")
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/pipeline", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<PipelineResponse>>();
        result.Should().BeSuccessful();
    }
}
