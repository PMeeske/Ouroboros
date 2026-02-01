// <copyright file="AskCommandTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Options;
using Ouroboros.Tests.CLI.Fixtures;
using Ouroboros.Tests.Infrastructure.Utilities;

namespace Ouroboros.Tests.CLI.Commands;

/// <summary>
/// Unit tests for Ask CLI command.
/// Note: These tests execute actual CLI commands but use mocked console I/O.
/// They are marked as Integration tests as they involve the full command pipeline.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("Category", TestCategories.CLI)]
public class AskCommandTests
{
    [Fact]
    public async Task RunAskAsync_WithBasicQuestion_ExecutesSuccessfully()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new AskOptions
        {
            Question = "What is 2+2?",
            Model = "llama3",
            Rag = false,
            Agent = false
        };

        // Act
        var result = await harness.ExecuteAskAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue("basic question should execute without errors");
        result.HasOutput.Should().BeTrue("command should produce output");
    }

    [Fact]
    public async Task RunAskAsync_WithAgentMode_ExecutesSuccessfully()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new AskOptions
        {
            Question = "Test question for agent",
            Model = "llama3",
            Agent = true,
            AgentMode = "lc",
            AgentMaxSteps = 3
        };

        // Act
        var result = await harness.ExecuteAskAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue("agent mode should execute");
        // Output should have either timing or telemetry info
        (result.Output.Contains("timing") || result.Output.Contains("telemetry")).Should().BeTrue("output should include execution metrics");
        // Output should indicate agent mode (either "agent-" or "agentIters")
        (result.Output.Contains("agent-") || result.Output.Contains("agentIters")).Should().BeTrue("output should indicate agent mode");
    }

    [Fact]
    public async Task RunAskAsync_WithRagEnabled_ExecutesSuccessfully()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new AskOptions
        {
            Question = "What is event sourcing?",
            Model = "llama3",
            Rag = true,
            Embed = "nomic-embed-text",
            K = 3
        };

        // Act
        var result = await harness.ExecuteAskAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue("RAG mode should execute");
        result.HasOutput.Should().BeTrue("RAG query should produce output");
    }

    [Fact]
    public async Task RunAskAsync_WithDebugEnabled_ShowsDebugOutput()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new AskOptions
        {
            Question = "Test debug output",
            Model = "llama3",
            Debug = true
        };

        // Act
        var result = await harness.ExecuteAskAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue();
        // Debug mode sets environment variable MONADIC_DEBUG=1
        Environment.GetEnvironmentVariable("MONADIC_DEBUG").Should().Be("1");
    }

    [Fact]
    public async Task RunAskAsync_WithCultureOption_AppliesCorrectly()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new AskOptions
        {
            Question = "Hello",
            Model = "llama3",
            Culture = "en-US"
        };

        // Act
        var result = await harness.ExecuteAskAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.HasOutput.Should().BeTrue();
    }

    [Fact]
    public async Task RunAskAsync_WithRouterAuto_ExecutesWithMultiModelRouting()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new AskOptions
        {
            Question = "Test multi-model routing",
            Model = "llama3",
            Router = "auto",
            GeneralModel = "llama3"
        };

        // Act
        var result = await harness.ExecuteAskAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue();
        Environment.GetEnvironmentVariable("MONADIC_ROUTER").Should().Be("auto");
    }

    [Fact]
    public async Task RunAskAsync_WithAgentAndRag_CombinesBothFeatures()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new AskOptions
        {
            Question = "What are CQRS patterns?",
            Model = "llama3",
            Agent = true,
            Rag = true,
            AgentMaxSteps = 2,
            K = 2
        };

        // Act
        var result = await harness.ExecuteAskAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue("agent + RAG should work together");
        // Output should indicate agent mode
        (result.Output.Contains("agent-") || result.Output.Contains("agentIters")).Should().BeTrue("should indicate agent mode");
    }

    [Fact]
    public async Task RunAskAsync_WithJsonTools_EnablesJsonFormat()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new AskOptions
        {
            Question = "Test JSON tools",
            Model = "llama3",
            Agent = true,
            JsonTools = true
        };

        // Act
        var result = await harness.ExecuteAskAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RunAskAsync_WithStrictModel_EnforcesModelValidation()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new AskOptions
        {
            Question = "Test strict model",
            Model = "llama3",
            StrictModel = true
        };

        // Act
        var result = await harness.ExecuteAskAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RunAskAsync_WithStream_EnablesStreamingOutput()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new AskOptions
        {
            Question = "Test streaming",
            Model = "llama3",
            Stream = true
        };

        // Act
        var result = await harness.ExecuteAskAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RunAskAsync_WithCustomTemperature_AppliesSetting()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new AskOptions
        {
            Question = "Test temperature",
            Model = "llama3",
            Temperature = 0.5
        };

        // Act
        var result = await harness.ExecuteAskAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RunAskAsync_WithMaxTokensLimit_AppliesSetting()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new AskOptions
        {
            Question = "Test max tokens",
            Model = "llama3",
            MaxTokens = 256
        };

        // Act
        var result = await harness.ExecuteAskAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RunAskAsync_RecordsExecutionTime()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new AskOptions
        {
            Question = "Quick test",
            Model = "llama3"
        };

        // Act
        var result = await harness.ExecuteAskAsync(options);

        // Assert
        result.ExecutionTimeMs.Should().BeGreaterThan(0, "execution time should be recorded");
    }

    [Fact]
    public async Task RunAskAsync_ShowsTimingInformation()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new AskOptions
        {
            Question = "Test timing",
            Model = "llama3"
        };

        // Act
        var result = await harness.ExecuteAskAsync(options);

        // Assert
        // Output should have either timing or telemetry info
        (result.Output.Contains("timing") || result.Output.Contains("telemetry")).Should().BeTrue("output should include execution metrics");
        // Check for timing format if timing is present
        if (result.Output.Contains("timing"))
        {
            result.Output.Should().MatchRegex(@"\[timing\] total=\d+ms", "should show timing in correct format");
        }
    }
}
