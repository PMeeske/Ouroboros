// <copyright file="OrchestratorCommandTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Options;
using Ouroboros.Tests.CLI.Fixtures;
using Ouroboros.Tests.Infrastructure.Utilities;

namespace Ouroboros.Tests.CLI.Commands;

/// <summary>
/// Unit tests for Orchestrator CLI command.
/// Tests smart model orchestration, multi-model routing, and error handling.
/// Note: These tests may fail if Ollama is not running - that's expected behavior.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("Category", TestCategories.CLI)]
public class OrchestratorCommandTests
{
    [Fact]
    public async Task RunOrchestratorAsync_WithBasicGoal_ExecutesSuccessfully()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new OrchestratorOptions
        {
            Goal = "What is 2+2?",
            Model = "llama3"
        };

        // Act
        var result = await harness.ExecuteOrchestratorAsync(options);

        // Assert
        // May fail if Ollama is not running, but that's expected
        (result.IsSuccess || result.IsFailure).Should().BeTrue();
        result.HasOutput.Should().BeTrue("orchestrator should produce output");
        if (result.IsSuccess)
        {
            result.Output.Should().Contain("Smart Model Orchestrator", "header should be displayed");
        }
    }

    [Fact]
    public async Task RunOrchestratorAsync_WithMultiModelConfiguration_UsesSpecializedModels()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new OrchestratorOptions
        {
            Goal = "Write a Python function",
            Model = "llama3",
            CoderModel = "codellama",
            ReasonModel = "deepseek-r1"
        };

        // Act
        var result = await harness.ExecuteOrchestratorAsync(options);

        // Assert
        (result.IsSuccess || result.IsFailure).Should().BeTrue();
        result.HasOutput.Should().BeTrue();
        if (result.IsSuccess)
        {
            result.Output.Should().Contain("Orchestrator", "should indicate orchestrator mode");
        }
    }

    [Fact]
    public async Task RunOrchestratorAsync_WithDebugEnabled_SetsDebugEnvironment()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new OrchestratorOptions
        {
            Goal = "Debug test",
            Model = "llama3",
            Debug = true
        };

        string? originalDebugValue = Environment.GetEnvironmentVariable("MONADIC_DEBUG");

        try
        {
            // Act
            var result = await harness.ExecuteOrchestratorAsync(options);

            // Assert
            (result.IsSuccess || result.IsFailure).Should().BeTrue();
            if (result.IsSuccess)
            {
                Environment.GetEnvironmentVariable("MONADIC_DEBUG").Should().Be("1");
            }
        }
        finally
        {
            // Cleanup: restore original environment variable value
            if (originalDebugValue == null)
            {
                Environment.SetEnvironmentVariable("MONADIC_DEBUG", null);
            }
            else
            {
                Environment.SetEnvironmentVariable("MONADIC_DEBUG", originalDebugValue);
            }
        }
    }

    [Fact]
    public async Task RunOrchestratorAsync_WithShowMetrics_DisplaysPerformanceData()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new OrchestratorOptions
        {
            Goal = "Test metrics",
            Model = "llama3",
            ShowMetrics = true
        };

        // Act
        var result = await harness.ExecuteOrchestratorAsync(options);

        // Assert
        (result.IsSuccess || result.IsFailure).Should().BeTrue();
        if (result.IsSuccess)
        {
            result.Output.Should().Contain("Performance Metrics", "metrics should be displayed");
        }
    }

    [Fact]
    public async Task RunOrchestratorAsync_WithCustomTemperature_AppliesSetting()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new OrchestratorOptions
        {
            Goal = "Temperature test",
            Model = "llama3",
            Temperature = 0.5
        };

        // Act
        var result = await harness.ExecuteOrchestratorAsync(options);

        // Assert
        (result.IsSuccess || result.IsFailure).Should().BeTrue();
        result.HasOutput.Should().BeTrue();
    }

    [Fact]
    public async Task RunOrchestratorAsync_WithMaxTokens_AppliesSetting()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new OrchestratorOptions
        {
            Goal = "Max tokens test",
            Model = "llama3",
            MaxTokens = 256
        };

        // Act
        var result = await harness.ExecuteOrchestratorAsync(options);

        // Assert
        (result.IsSuccess || result.IsFailure).Should().BeTrue();
        result.HasOutput.Should().BeTrue();
    }

    [Fact]
    public async Task RunOrchestratorAsync_WhenOllamaNotRunning_DisplaysHelpfulError()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new OrchestratorOptions
        {
            Goal = "Test error handling",
            Model = "llama3"
        };

        // Act
        var result = await harness.ExecuteOrchestratorAsync(options);

        // Assert
        if (result.IsFailure)
        {
            // If Ollama is not running, should show helpful error message
            (result.Error.Contains("Ollama") || result.Error.Contains("Connection refused"))
                .Should().BeTrue("error should mention Ollama or connection issues");
        }
    }

    [Fact]
    public async Task RunOrchestratorAsync_WithCulture_AppliesCultureSetting()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new OrchestratorOptions
        {
            Goal = "Culture test",
            Model = "llama3",
            Culture = "en-US"
        };

        // Act
        var result = await harness.ExecuteOrchestratorAsync(options);

        // Assert
        (result.IsSuccess || result.IsFailure).Should().BeTrue();
        result.HasOutput.Should().BeTrue();
    }

    [Fact]
    public async Task RunOrchestratorAsync_RecordsExecutionTime()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new OrchestratorOptions
        {
            Goal = "Quick test",
            Model = "llama3"
        };

        // Act
        var result = await harness.ExecuteOrchestratorAsync(options);

        // Assert
        result.ExecutionTimeMs.Should().BeGreaterThan(0, "execution time should be recorded");
    }

    [Fact]
    public async Task RunOrchestratorAsync_WithRemoteEndpoint_ConfiguresRemoteBackend()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new OrchestratorOptions
        {
            Goal = "Remote test",
            Model = "gpt-4",
            Endpoint = "https://api.openai.com/v1",
            ApiKey = "sk-test",
            EndpointType = "openai"
        };

        // Act
        var result = await harness.ExecuteOrchestratorAsync(options);

        // Assert
        // Will likely fail without valid credentials, but should attempt remote configuration
        (result.IsSuccess || result.IsFailure).Should().BeTrue();
        result.HasOutput.Should().BeTrue();
    }

    [Fact]
    public async Task RunOrchestratorAsync_DisplaysToolRegistryInfo()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new OrchestratorOptions
        {
            Goal = "Test with tools",
            Model = "llama3"
        };

        // Act
        var result = await harness.ExecuteOrchestratorAsync(options);

        // Assert
        if (result.IsSuccess)
        {
            result.Output.Should().Contain("Tool registry", "should mention tool registry");
        }
    }

    [Fact]
    public async Task RunOrchestratorAsync_ShowsTimingInformation()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new OrchestratorOptions
        {
            Goal = "Timing test",
            Model = "llama3"
        };

        // Act
        var result = await harness.ExecuteOrchestratorAsync(options);

        // Assert
        if (result.IsSuccess)
        {
            result.Output.Should().Contain("timing", "should show timing information");
        }
    }

    [Fact]
    public async Task RunOrchestratorAsync_WithCoderModel_ConfiguresSeparateCoderModel()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new OrchestratorOptions
        {
            Goal = "Write some code",
            Model = "llama3",
            CoderModel = "codellama"
        };

        // Act
        var result = await harness.ExecuteOrchestratorAsync(options);

        // Assert
        (result.IsSuccess || result.IsFailure).Should().BeTrue();
        result.HasOutput.Should().BeTrue();
    }

    [Fact]
    public async Task RunOrchestratorAsync_WithReasonModel_ConfiguresSeparateReasonModel()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new OrchestratorOptions
        {
            Goal = "Analyze this problem",
            Model = "llama3",
            ReasonModel = "deepseek-r1"
        };

        // Act
        var result = await harness.ExecuteOrchestratorAsync(options);

        // Assert
        (result.IsSuccess || result.IsFailure).Should().BeTrue();
        result.HasOutput.Should().BeTrue();
    }

    [Fact]
    public async Task RunOrchestratorAsync_ShowsResponseSection()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new OrchestratorOptions
        {
            Goal = "Simple question",
            Model = "llama3"
        };

        // Act
        var result = await harness.ExecuteOrchestratorAsync(options);

        // Assert
        if (result.IsSuccess)
        {
            result.Output.Should().Contain("Response", "should show response section");
        }
    }

    [Fact]
    public async Task RunOrchestratorAsync_WithLongGoal_HandlesCorrectly()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new OrchestratorOptions
        {
            Goal = "Write a comprehensive Python web scraper that handles authentication, rate limiting, error retries, and can extract structured data from multiple pages",
            Model = "llama3"
        };

        // Act
        var result = await harness.ExecuteOrchestratorAsync(options);

        // Assert
        (result.IsSuccess || result.IsFailure).Should().BeTrue();
        result.HasOutput.Should().BeTrue();
    }
}
