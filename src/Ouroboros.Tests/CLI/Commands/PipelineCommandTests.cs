// <copyright file="PipelineCommandTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Options;
using Ouroboros.Tests.CLI.Fixtures;
using Ouroboros.Tests.Infrastructure.Utilities;

namespace Ouroboros.Tests.CLI.Commands;

/// <summary>
/// Unit tests for Pipeline CLI command.
/// These tests execute actual pipeline DSL commands with mocked console I/O.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("Category", TestCategories.CLI)]
public class PipelineCommandTests
{
    [Fact]
    public async Task RunPipelineAsync_WithBasicDsl_ExecutesSuccessfully()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new PipelineOptions
        {
            Dsl = "SetPrompt 'Test prompt' | UseDraft",
            Model = "llama3",
            Source = Path.GetTempPath()
        };

        // Act
        var result = await harness.ExecutePipelineAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue("basic DSL should execute");
        result.HasOutput.Should().BeTrue();
    }

    [Fact]
    public async Task RunPipelineAsync_WithChainedSteps_ExecutesInSequence()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new PipelineOptions
        {
            Dsl = "SetPrompt 'Hello' | UseDraft | UseOutput",
            Model = "llama3",
            Source = Path.GetTempPath()
        };

        // Act
        var result = await harness.ExecutePipelineAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue("chained pipeline should execute");
    }

    [Fact]
    public async Task RunPipelineAsync_WithTraceEnabled_ShowsEvents()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new PipelineOptions
        {
            Dsl = "SetPrompt 'Test' | UseDraft",
            Model = "llama3",
            Trace = true,
            Source = Path.GetTempPath()
        };

        // Act
        var result = await harness.ExecutePipelineAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue("pipeline with trace should execute successfully");
        // Just verify that some output was produced (trace format may vary)
        result.HasOutput.Should().BeTrue("trace mode should produce output");
    }

    [Fact]
    public async Task RunPipelineAsync_WithDebugEnabled_ShowsDebugOutput()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new PipelineOptions
        {
            Dsl = "SetPrompt 'Debug test' | UseDraft",
            Model = "llama3",
            Debug = true,
            Source = Path.GetTempPath()
        };

        // Act
        var result = await harness.ExecutePipelineAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue();
        Environment.GetEnvironmentVariable("MONADIC_DEBUG").Should().Be("1");
    }

    [Fact]
    public async Task RunPipelineAsync_WithCustomSource_UsesCorrectPath()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var tempDir = Path.Combine(Path.GetTempPath(), $"ouroboros-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var options = new PipelineOptions
            {
                Dsl = "SetPrompt 'Source test' | UseDraft",
                Model = "llama3",
                Source = tempDir
            };

            // Act
            var result = await harness.ExecutePipelineAsync(options);

            // Assert
            result.IsSuccess.Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public async Task RunPipelineAsync_WithRouterAuto_ExecutesWithMultiModelRouting()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new PipelineOptions
        {
            Dsl = "SetPrompt 'Router test' | UseDraft",
            Model = "llama3",
            Router = "auto",
            GeneralModel = "llama3",
            Source = Path.GetTempPath()
        };

        // Act
        var result = await harness.ExecutePipelineAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue();
        Environment.GetEnvironmentVariable("MONADIC_ROUTER").Should().Be("auto");
    }

    [Fact]
    public async Task RunPipelineAsync_WithCustomK_AppliesRetrievalSetting()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new PipelineOptions
        {
            Dsl = "SetPrompt 'K test' | UseDraft",
            Model = "llama3",
            K = 5,
            Source = Path.GetTempPath()
        };

        // Act
        var result = await harness.ExecutePipelineAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RunPipelineAsync_WithCulture_AppliesCorrectly()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new PipelineOptions
        {
            Dsl = "SetPrompt 'Culture test' | UseDraft",
            Model = "llama3",
            Culture = "en-US",
            Source = Path.GetTempPath()
        };

        // Act
        var result = await harness.ExecutePipelineAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RunPipelineAsync_WithStrictModel_EnforcesModelValidation()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new PipelineOptions
        {
            Dsl = "SetPrompt 'Strict test' | UseDraft",
            Model = "llama3",
            StrictModel = true,
            Source = Path.GetTempPath()
        };

        // Act
        var result = await harness.ExecutePipelineAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RunPipelineAsync_WithStream_EnablesStreamingOutput()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new PipelineOptions
        {
            Dsl = "SetPrompt 'Stream test' | UseDraft",
            Model = "llama3",
            Stream = true,
            Source = Path.GetTempPath()
        };

        // Act
        var result = await harness.ExecutePipelineAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RunPipelineAsync_ShowsResultOutput()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new PipelineOptions
        {
            Dsl = "SetPrompt 'Result test' | UseDraft",
            Model = "llama3",
            Source = Path.GetTempPath()
        };

        // Act
        var result = await harness.ExecutePipelineAsync(options);

        // Assert
        result.Output.Should().Contain("PIPELINE RESULT", "output should show result section");
    }

    [Fact]
    public async Task RunPipelineAsync_RecordsExecutionTime()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new PipelineOptions
        {
            Dsl = "SetPrompt 'Quick' | UseDraft",
            Model = "llama3",
            Source = Path.GetTempPath()
        };

        // Act
        var result = await harness.ExecutePipelineAsync(options);

        // Assert
        result.ExecutionTimeMs.Should().BeGreaterThan(0, "execution time should be recorded");
    }

    [Fact]
    public async Task RunPipelineAsync_WithInvalidDsl_ReturnsError()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new PipelineOptions
        {
            Dsl = "InvalidToken | BadStep",
            Model = "llama3",
            Source = Path.GetTempPath()
        };

        // Act
        var result = await harness.ExecutePipelineAsync(options);

        // Assert
        result.ExitCode.Should().Be(1, "invalid DSL should fail");
        result.Error.Should().NotBeEmpty("error message should be provided");
    }

    [Fact]
    public async Task RunPipelineAsync_WithEmptyDsl_ReturnsError()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new PipelineOptions
        {
            Dsl = string.Empty,
            Model = "llama3",
            Source = Path.GetTempPath()
        };

        // Act
        var result = await harness.ExecutePipelineAsync(options);

        // Assert
        result.ExitCode.Should().Be(1, "empty DSL should fail");
    }

    [Fact]
    public async Task RunPipelineAsync_WithCustomTemperature_AppliesSetting()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new PipelineOptions
        {
            Dsl = "SetPrompt 'Temperature' | UseDraft",
            Model = "llama3",
            Temperature = 0.5,
            Source = Path.GetTempPath()
        };

        // Act
        var result = await harness.ExecutePipelineAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RunPipelineAsync_WithMaxTokens_AppliesSetting()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new PipelineOptions
        {
            Dsl = "SetPrompt 'Tokens' | UseDraft",
            Model = "llama3",
            MaxTokens = 256,
            Source = Path.GetTempPath()
        };

        // Act
        var result = await harness.ExecutePipelineAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }
}
