// <copyright file="AskEndpointTests.cs" company="PlaceholderCompany">
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
/// Integration tests for the /api/ask endpoint.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("Category", TestCategories.WebApi)]
public class AskEndpointTests : IClassFixture<WebApiTestFixture>
{
    private readonly HttpClient _client;

    /// <summary>
    /// Initializes a new instance of the <see cref="AskEndpointTests"/> class.
    /// </summary>
    public AskEndpointTests(WebApiTestFixture fixture)
    {
        _client = fixture.CreateClient();
    }

    [Fact]
    public async Task Ask_WithValidRequest_ReturnsOkWithAnswer()
    {
        // Arrange
        var request = new AskRequestBuilder()
            .WithQuestion("What is artificial intelligence?")
            .WithModel("llama3")
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/ask", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<AskResponse>>();
        result.Should().BeSuccessful();
        result!.Data.Should().NotBeNull();
        result.Data!.Answer.Should().NotBeNullOrEmpty();
        result.Data.Model.Should().Be("llama3");
    }

    [Fact]
    public async Task Ask_WithRagEnabled_ReturnsRagPrefixedAnswer()
    {
        // Arrange
        var request = new AskRequestBuilder()
            .WithQuestion("Explain event sourcing")
            .WithRag()
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/ask", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<AskResponse>>();
        result.Should()
            .BeSuccessful()
            .And.HaveData();

        result!.Data!.Answer.Should().Contain("[RAG]", "RAG mode should be indicated");
    }

    [Fact]
    public async Task Ask_WithAgentMode_ExecutesSuccessfully()
    {
        // Arrange
        var request = new AskRequestBuilder()
            .WithQuestion("Calculate fibonacci sequence")
            .WithAgent()
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/ask", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<AskResponse>>();
        result.Should().BeSuccessful();
    }

    [Fact]
    public async Task Ask_WithCustomTemperature_ExecutesSuccessfully()
    {
        // Arrange
        var request = new AskRequestBuilder()
            .WithQuestion("Generate creative text")
            .WithTemperature(0.9f)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/ask", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<AskResponse>>();
        result.Should().BeSuccessful();
    }

    [Fact]
    public async Task Ask_WithMaxTokens_ExecutesSuccessfully()
    {
        // Arrange
        var request = new AskRequestBuilder()
            .WithQuestion("Write a short story")
            .WithMaxTokens(500)
            .Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/ask", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<AskResponse>>();
        result.Should().BeSuccessful();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task Ask_WithEmptyQuestion_ReturnsBadRequest(string? question)
    {
        // Arrange
        var request = new { Question = question };

        // Act
        var response = await _client.PostAsJsonAsync("/api/ask", request);

        // Assert - Should fail validation
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Ask_WithGeneratedTestData_ExecutesSuccessfully()
    {
        // Arrange
        var request = TestDataGenerator.GenerateAskRequest();

        // Act
        var response = await _client.PostAsJsonAsync("/api/ask", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<ApiResponse<AskResponse>>();
        result.Should().BeSuccessful();
    }

    [Fact]
    public async Task Ask_Response_IncludesExecutionTime()
    {
        // Arrange
        var request = new AskRequestBuilder().Build();

        // Act
        var response = await _client.PostAsJsonAsync("/api/ask", request);

        // Assert
        var result = await response.Content.ReadFromJsonAsync<ApiResponse<AskResponse>>();
        result!.ExecutionTimeMs.Should().HaveValue();
        result.Should().HaveExecutionTimeLessThan(30000, "API should respond within 30 seconds");
    }

    [Fact]
    public async Task Ask_WithDifferentModels_ExecutesSuccessfully()
    {
        // Arrange & Act & Assert for multiple models
        var models = new[] { "llama3", "deepseek-coder:33b", "mistral:7b" };

        foreach (var model in models)
        {
            var request = new AskRequestBuilder()
                .WithQuestion("Hello world")
                .WithModel(model)
                .Build();

            var response = await _client.PostAsJsonAsync("/api/ask", request);

            response.StatusCode.Should().Be(HttpStatusCode.OK, $"model {model} should work");
        }
    }
}
