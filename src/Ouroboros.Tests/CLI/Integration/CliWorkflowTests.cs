// <copyright file="CliWorkflowTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Options;
using Ouroboros.Tests.CLI.Fixtures;
using Ouroboros.Tests.Infrastructure.Utilities;

namespace Ouroboros.Tests.CLI.Integration;

/// <summary>
/// Integration tests for end-to-end CLI workflows.
/// Tests complete user scenarios spanning multiple commands.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("Category", TestCategories.CLI)]
public class CliWorkflowTests
{
    [Fact]
    public async Task AskWorkflow_BasicQuestion_CompletesSuccessfully()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new AskOptions
        {
            Question = "What is the capital of France?",
            Model = "llama3"
        };

        // Act
        var result = await harness.ExecuteAskAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.HasOutput.Should().BeTrue();
        (result.Output.Contains("timing") || result.Output.Contains("telemetry")).Should().BeTrue("output should include execution metrics");
    }

    [Fact]
    public async Task AskWorkflow_WithRag_RetrievesAndAnswers()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new AskOptions
        {
            Question = "Explain circuit breaker patterns",
            Model = "llama3",
            Rag = true,
            K = 3
        };

        // Act
        var result = await harness.ExecuteAskAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.HasOutput.Should().BeTrue();
    }

    [Fact]
    public async Task AskWorkflow_WithAgent_ExecutesToolCalls()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new AskOptions
        {
            Question = "Convert hello to uppercase",
            Model = "llama3",
            Agent = true,
            AgentMaxSteps = 3
        };

        // Act
        var result = await harness.ExecuteAskAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue();
        (result.Output.Contains("agent-") || result.Output.Contains("agentIters")).Should().BeTrue("output should indicate agent mode");
    }

    [Fact]
    public async Task PipelineWorkflow_SetPromptAndDraft_CompletesSuccessfully()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new PipelineOptions
        {
            Dsl = "SetPrompt 'Explain AI in one sentence' | UseDraft",
            Model = "llama3",
            Source = Path.GetTempPath()
        };

        // Act
        var result = await harness.ExecutePipelineAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Output.Should().Contain("PIPELINE RESULT");
    }

    [Fact]
    public async Task PipelineWorkflow_WithTrace_ShowsDetailedEvents()
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
        result.HasOutput.Should().BeTrue("trace mode should produce output");
    }

    [Fact]
    public async Task MultipleCommands_InSequence_ExecuteIndependently()
    {
        // Arrange
        using var harness1 = new CliTestHarness();
        using var harness2 = new CliTestHarness();

        var askOptions = new AskOptions
        {
            Question = "First question",
            Model = "llama3"
        };

        var pipelineOptions = new PipelineOptions
        {
            Dsl = "SetPrompt 'Second task' | UseDraft",
            Model = "llama3",
            Source = Path.GetTempPath()
        };

        // Act
        var askResult = await harness1.ExecuteAskAsync(askOptions);
        var pipelineResult = await harness2.ExecutePipelineAsync(pipelineOptions);

        // Assert
        askResult.IsSuccess.Should().BeTrue();
        pipelineResult.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task AskWorkflow_WithDebugMode_ShowsDebugInformation()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new AskOptions
        {
            Question = "Debug test",
            Model = "llama3",
            Debug = true
        };

        // Act
        var result = await harness.ExecuteAskAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue();
        Environment.GetEnvironmentVariable("MONADIC_DEBUG").Should().Be("1");
    }

    [Fact]
    public async Task PipelineWorkflow_WithDebugMode_ShowsDebugInformation()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new PipelineOptions
        {
            Dsl = "SetPrompt 'Debug' | UseDraft",
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
    public async Task AskWorkflow_ClearsOutputBetweenCalls()
    {
        // Arrange
        using var harness = new CliTestHarness();

        var options1 = new AskOptions { Question = "First", Model = "llama3" };
        var options2 = new AskOptions { Question = "Second", Model = "llama3" };

        // Act
        var result1 = await harness.ExecuteAskAsync(options1);
        harness.Clear();
        var result2 = await harness.ExecuteAskAsync(options2);

        // Assert
        result1.Output.Should().NotBeEmpty();
        result2.Output.Should().NotBeEmpty();
        result2.Output.Should().NotContain(result1.Output, "output should be cleared between calls");
    }

    [Fact]
    public async Task PipelineWorkflow_WithCustomSource_UsesCorrectPath()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), $"cli-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);

        try
        {
            using var harness = new CliTestHarness();
            var options = new PipelineOptions
            {
                Dsl = "SetPrompt 'Custom source' | UseDraft",
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
    public async Task AskWorkflow_WithAgentAndRag_CombinesFeatures()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new AskOptions
        {
            Question = "What are the benefits of CQRS?",
            Model = "llama3",
            Agent = true,
            Rag = true,
            AgentMaxSteps = 3,
            K = 2
        };

        // Act
        var result = await harness.ExecuteAskAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue();
        (result.Output.Contains("agent-") || result.Output.Contains("agentIters")).Should().BeTrue("output should indicate agent mode");
    }

    [Fact]
    public async Task PipelineWorkflow_WithRouter_UsesMultiModelRouting()
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
    public async Task AskWorkflow_RecordsPerformanceMetrics()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new AskOptions
        {
            Question = "Performance test",
            Model = "llama3"
        };

        // Act
        var result = await harness.ExecuteAskAsync(options);

        // Assert
        result.ExecutionTimeMs.Should().BeGreaterThan(0);
        result.Output.Should().MatchRegex(@"\[timing\] total=\d+ms");
    }

    [Fact]
    public async Task PipelineWorkflow_RecordsPerformanceMetrics()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new PipelineOptions
        {
            Dsl = "SetPrompt 'Perf' | UseDraft",
            Model = "llama3",
            Source = Path.GetTempPath()
        };

        // Act
        var result = await harness.ExecutePipelineAsync(options);

        // Assert
        result.ExecutionTimeMs.Should().BeGreaterThan(0);
    }
}
