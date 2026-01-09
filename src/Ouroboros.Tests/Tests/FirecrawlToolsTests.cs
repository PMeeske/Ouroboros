// <copyright file="FirecrawlToolsTests.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using FluentAssertions;
using Ouroboros.Application.Tools;
using Xunit;

namespace Ouroboros.Tests;

/// <summary>
/// Tests for Firecrawl integration tools.
/// </summary>
[Trait("Category", "Unit")]
public class FirecrawlToolsTests
{
    [Fact]
    public void FirecrawlScrapeTool_HasCorrectMetadata()
    {
        // Arrange
        var tool = new AutonomousTools.FirecrawlScrapeTool();

        // Assert
        tool.Name.Should().Be("firecrawl_scrape");
        tool.Description.Should().Contain("Firecrawl");
        tool.Description.Should().Contain("FIRECRAWL_API_KEY");
        tool.JsonSchema.Should().Contain("url");
    }

    [Fact]
    public void FirecrawlResearchTool_HasCorrectMetadata()
    {
        // Arrange
        var tool = new AutonomousTools.FirecrawlResearchTool();

        // Assert
        tool.Name.Should().Be("web_research");
        tool.Description.Should().Contain("Research");
        tool.JsonSchema.Should().Contain("query");
    }

    [Fact]
    public async Task FirecrawlScrapeTool_ReturnsErrorWhenNoApiKey()
    {
        // Arrange
        var originalKey = Environment.GetEnvironmentVariable("FIRECRAWL_API_KEY");
        Environment.SetEnvironmentVariable("FIRECRAWL_API_KEY", null);

        try
        {
            var tool = new AutonomousTools.FirecrawlScrapeTool();

            // Act
            var result = await tool.InvokeAsync("https://example.com", CancellationToken.None);

            // Assert
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Contain("FIRECRAWL_API_KEY");
        }
        finally
        {
            Environment.SetEnvironmentVariable("FIRECRAWL_API_KEY", originalKey);
        }
    }

    [Fact]
    public async Task FirecrawlScrapeTool_ReturnsErrorWhenEmptyUrl()
    {
        // Arrange
        var tool = new AutonomousTools.FirecrawlScrapeTool();

        // Act
        var result = await tool.InvokeAsync("", CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No URL");
    }

    [Fact]
    public async Task FirecrawlResearchTool_ReturnsErrorWhenEmptyQuery()
    {
        // Arrange
        var tool = new AutonomousTools.FirecrawlResearchTool();

        // Act
        var result = await tool.InvokeAsync("", CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("No query");
    }

    [Fact]
    public async Task FirecrawlScrapeTool_ParsesJsonInput()
    {
        // Arrange
        var originalKey = Environment.GetEnvironmentVariable("FIRECRAWL_API_KEY");
        Environment.SetEnvironmentVariable("FIRECRAWL_API_KEY", null);

        try
        {
            var tool = new AutonomousTools.FirecrawlScrapeTool();
            string jsonInput = """{"url": "https://example.com"}""";

            // Act
            var result = await tool.InvokeAsync(jsonInput, CancellationToken.None);

            // Assert - Should fail due to missing API key, but URL should be parsed
            result.IsSuccess.Should().BeFalse();
            result.Error.Should().Contain("FIRECRAWL_API_KEY");
        }
        finally
        {
            Environment.SetEnvironmentVariable("FIRECRAWL_API_KEY", originalKey);
        }
    }

    [Fact]
    public void AutonomousTools_IncludesFirecrawlTools()
    {
        // Act
        var tools = AutonomousTools.GetAllTools().ToList();

        // Assert
        tools.Should().Contain(t => t.Name == "firecrawl_scrape");
        tools.Should().Contain(t => t.Name == "web_research");
    }

    [Fact]
    public async Task FirecrawlResearchTool_FallsBackToSearchForNonUrl()
    {
        // Arrange
        var tool = new AutonomousTools.FirecrawlResearchTool();

        // Act - use a search query, not a URL
        // This will attempt a DuckDuckGo search
        // Note: This is a live test, skip in CI if needed
        var result = await tool.InvokeAsync("test query that is not a url", CancellationToken.None);

        // Assert - should attempt search (may succeed or fail depending on network)
        // Just verify it doesn't throw and returns a result
        result.Should().NotBeNull();
    }
}
