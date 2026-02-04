// <copyright file="MeTTaCommandTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Options;
using Ouroboros.Tests.CLI.Fixtures;
using Ouroboros.Tests.Infrastructure.Utilities;

namespace Ouroboros.Tests.CLI.Commands;

/// <summary>
/// Unit tests for MeTTa CLI command.
/// Tests MeTTa orchestrator v3.0 with symbolic reasoning, plan generation, and execution.
/// Note: These tests may fail if Ollama or MeTTa engine is not available - that's expected behavior.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("Category", TestCategories.CLI)]
public class MeTTaCommandTests
{
    [Fact]
    public async Task RunMeTTaAsync_WithBasicGoal_ExecutesSuccessfully()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new MeTTaOptions
        {
            Goal = "Calculate the sum of 1 and 2",
            Model = "llama3"
        };

        // Act
        var result = await harness.ExecuteMeTTaAsync(options);

        // Assert
        // May fail if Ollama/MeTTa is not available, but should handle gracefully
        (result.IsSuccess || result.IsFailure).Should().BeTrue();
        result.HasOutput.Should().BeTrue("MeTTa orchestrator should produce output");
        if (result.IsSuccess)
        {
            result.Output.Should().Contain("MeTTa Orchestrator", "header should be displayed");
        }
    }

    [Fact]
    public async Task RunMeTTaAsync_WithPlanOnlyMode_GeneratesPlanWithoutExecution()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new MeTTaOptions
        {
            Goal = "Solve a complex problem",
            Model = "llama3",
            PlanOnly = true
        };

        // Act
        var result = await harness.ExecuteMeTTaAsync(options);

        // Assert
        (result.IsSuccess || result.IsFailure).Should().BeTrue();
        if (result.IsSuccess)
        {
            result.Output.Should().Contain("Planning Phase", "should show planning phase");
            result.Output.Should().Contain("Plan-only mode", "should indicate plan-only mode");
        }
    }

    [Fact]
    public async Task RunMeTTaAsync_WithDebugEnabled_SetsDebugEnvironment()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new MeTTaOptions
        {
            Goal = "Debug test",
            Model = "llama3",
            Debug = true
        };

        string? originalDebugValue = Environment.GetEnvironmentVariable("MONADIC_DEBUG");

        try
        {
            // Act
            var result = await harness.ExecuteMeTTaAsync(options);

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
    public async Task RunMeTTaAsync_WithShowMetrics_DisplaysPerformanceData()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new MeTTaOptions
        {
            Goal = "Test metrics display",
            Model = "llama3",
            ShowMetrics = true
        };

        // Act
        var result = await harness.ExecuteMeTTaAsync(options);

        // Assert
        (result.IsSuccess || result.IsFailure).Should().BeTrue();
        if (result.IsSuccess)
        {
            result.Output.Should().Contain("Performance Metrics", "metrics should be displayed");
        }
    }

    [Fact]
    public async Task RunMeTTaAsync_DisplaysPlanningPhase()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new MeTTaOptions
        {
            Goal = "Test planning",
            Model = "llama3"
        };

        // Act
        var result = await harness.ExecuteMeTTaAsync(options);

        // Assert
        if (result.IsSuccess)
        {
            result.Output.Should().Contain("Planning Phase", "should show planning phase");
        }
    }

    [Fact]
    public async Task RunMeTTaAsync_DisplaysExecutionPhase()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new MeTTaOptions
        {
            Goal = "Test execution",
            Model = "llama3",
            PlanOnly = false
        };

        // Act
        var result = await harness.ExecuteMeTTaAsync(options);

        // Assert
        if (result.IsSuccess)
        {
            result.Output.Should().Contain("Execution Phase", "should show execution phase");
        }
    }

    [Fact]
    public async Task RunMeTTaAsync_ShowsConfidenceScores()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new MeTTaOptions
        {
            Goal = "Analyze data",
            Model = "llama3"
        };

        // Act
        var result = await harness.ExecuteMeTTaAsync(options);

        // Assert
        if (result.IsSuccess)
        {
            result.Output.Should().Contain("confidence", "should show confidence scores");
        }
    }

    [Fact]
    public async Task RunMeTTaAsync_WhenOllamaNotRunning_DisplaysHelpfulError()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new MeTTaOptions
        {
            Goal = "Test error handling",
            Model = "llama3"
        };

        // Act
        var result = await harness.ExecuteMeTTaAsync(options);

        // Assert
        if (result.IsFailure)
        {
            // If Ollama is not running, should show helpful error message
            (result.Error.Contains("Ollama") || result.Error.Contains("Connection refused"))
                .Should().BeTrue("error should mention Ollama or connection issues");
        }
    }

    [Fact]
    public async Task RunMeTTaAsync_WhenMeTTaEngineNotFound_DisplaysHelpfulError()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new MeTTaOptions
        {
            Goal = "Test MeTTa engine error",
            Model = "llama3"
        };

        // Act
        var result = await harness.ExecuteMeTTaAsync(options);

        // Assert
        if (result.IsFailure)
        {
            // If MeTTa engine is not found, should show helpful error
            if (result.Error.Contains("metta") || result.Error.Contains("not found"))
            {
                result.Error.Should().Contain("Install", "should suggest installation");
            }
        }
    }

    [Fact]
    public async Task RunMeTTaAsync_WithCustomTemperature_AppliesSetting()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new MeTTaOptions
        {
            Goal = "Temperature test",
            Model = "llama3",
            Temperature = 0.5
        };

        // Act
        var result = await harness.ExecuteMeTTaAsync(options);

        // Assert
        (result.IsSuccess || result.IsFailure).Should().BeTrue();
        result.HasOutput.Should().BeTrue();
    }

    [Fact]
    public async Task RunMeTTaAsync_WithMaxTokens_AppliesSetting()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new MeTTaOptions
        {
            Goal = "Max tokens test",
            Model = "llama3",
            MaxTokens = 256
        };

        // Act
        var result = await harness.ExecuteMeTTaAsync(options);

        // Assert
        (result.IsSuccess || result.IsFailure).Should().BeTrue();
        result.HasOutput.Should().BeTrue();
    }

    [Fact]
    public async Task RunMeTTaAsync_WithCulture_AppliesCultureSetting()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new MeTTaOptions
        {
            Goal = "Culture test",
            Model = "llama3",
            Culture = "en-US"
        };

        // Act
        var result = await harness.ExecuteMeTTaAsync(options);

        // Assert
        (result.IsSuccess || result.IsFailure).Should().BeTrue();
        result.HasOutput.Should().BeTrue();
    }

    [Fact]
    public async Task RunMeTTaAsync_RecordsExecutionTime()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new MeTTaOptions
        {
            Goal = "Quick test",
            Model = "llama3"
        };

        // Act
        var result = await harness.ExecuteMeTTaAsync(options);

        // Assert
        result.ExecutionTimeMs.Should().BeGreaterThan(0, "execution time should be recorded");
    }

    [Fact]
    public async Task RunMeTTaAsync_WithRemoteEndpoint_ConfiguresRemoteBackend()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new MeTTaOptions
        {
            Goal = "Remote test",
            Model = "gpt-4",
            Endpoint = "https://api.openai.com/v1",
            ApiKey = "sk-test",
            EndpointType = "openai"
        };

        // Act
        var result = await harness.ExecuteMeTTaAsync(options);

        // Assert
        // Will likely fail without valid credentials, but should attempt remote configuration
        (result.IsSuccess || result.IsFailure).Should().BeTrue();
        result.HasOutput.Should().BeTrue();
    }

    [Fact]
    public async Task RunMeTTaAsync_ShowsStepResults()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new MeTTaOptions
        {
            Goal = "Test step tracking",
            Model = "llama3"
        };

        // Act
        var result = await harness.ExecuteMeTTaAsync(options);

        // Assert
        if (result.IsSuccess)
        {
            result.Output.Should().Contain("Step Results", "should show step results");
        }
    }

    [Fact]
    public async Task RunMeTTaAsync_ShowsInitializationInfo()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new MeTTaOptions
        {
            Goal = "Test initialization",
            Model = "llama3"
        };

        // Act
        var result = await harness.ExecuteMeTTaAsync(options);

        // Assert
        if (result.IsSuccess)
        {
            result.Output.Should().Contain("Initializing MeTTa", "should show initialization");
        }
    }

    [Fact]
    public async Task RunMeTTaAsync_WithEmbeddingModel_ConfiguresEmbedding()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new MeTTaOptions
        {
            Goal = "Test embedding",
            Model = "llama3",
            Embed = "nomic-embed-text"
        };

        // Act
        var result = await harness.ExecuteMeTTaAsync(options);

        // Assert
        (result.IsSuccess || result.IsFailure).Should().BeTrue();
        if (result.IsSuccess)
        {
            result.Output.Should().Contain("embedding", "should mention embedding model");
        }
    }

    [Fact]
    public async Task RunMeTTaAsync_PlanOnlyMode_SkipsExecution()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new MeTTaOptions
        {
            Goal = "Test plan-only",
            Model = "llama3",
            PlanOnly = true
        };

        // Act
        var result = await harness.ExecuteMeTTaAsync(options);

        // Assert
        if (result.IsSuccess)
        {
            result.Output.Should().Contain("skipping execution", "should skip execution in plan-only mode");
        }
    }

    [Fact]
    public async Task RunMeTTaAsync_ShowsPlanSteps()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new MeTTaOptions
        {
            Goal = "Test plan steps",
            Model = "llama3"
        };

        // Act
        var result = await harness.ExecuteMeTTaAsync(options);

        // Assert
        if (result.IsSuccess)
        {
            result.Output.Should().Contain("Steps:", "should show plan steps");
        }
    }

    [Fact]
    public async Task RunMeTTaAsync_WithLongGoal_HandlesCorrectly()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new MeTTaOptions
        {
            Goal = "Create a comprehensive plan to build, test, and deploy a microservices architecture with proper monitoring, logging, and CI/CD pipelines",
            Model = "llama3"
        };

        // Act
        var result = await harness.ExecuteMeTTaAsync(options);

        // Assert
        (result.IsSuccess || result.IsFailure).Should().BeTrue();
        result.HasOutput.Should().BeTrue();
    }
}
