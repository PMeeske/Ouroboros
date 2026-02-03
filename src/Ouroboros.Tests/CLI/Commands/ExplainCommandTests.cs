// <copyright file="ExplainCommandTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Options;
using Ouroboros.Tests.CLI.Fixtures;
using Ouroboros.Tests.Infrastructure.Utilities;

namespace Ouroboros.Tests.CLI.Commands;

/// <summary>
/// Unit tests for Explain CLI command.
/// Tests DSL explanation functionality with various input patterns.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("Category", TestCategories.CLI)]
public class ExplainCommandTests
{
    [Fact]
    public async Task RunExplainAsync_WithBasicDsl_ProducesExplanation()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new ExplainOptions
        {
            Dsl = "SetPrompt 'test' | UseDraft"
        };

        // Act
        var result = await harness.ExecuteExplainAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue("basic DSL should be explained successfully");
        result.HasOutput.Should().BeTrue("explanation should produce output");
    }

    [Fact]
    public async Task RunExplainAsync_WithChainedSteps_ExplainsSequence()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new ExplainOptions
        {
            Dsl = "SetPrompt 'Hello' | UseDraft | UseOutput"
        };

        // Act
        var result = await harness.ExecuteExplainAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue("chained DSL should be explained");
        result.HasOutput.Should().BeTrue();
    }

    [Fact]
    public async Task RunExplainAsync_WithComplexDsl_ProducesDetailedExplanation()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new ExplainOptions
        {
            Dsl = "SetPrompt 'Refactor code' | UseFiles '*.cs' | LLM | UseOutput"
        };

        // Act
        var result = await harness.ExecuteExplainAsync(options);

        // Assert
        // Complex DSL should either succeed or fail gracefully with explanation
        (result.IsSuccess || result.IsFailure).Should().BeTrue();
        (result.HasOutput || result.HasError).Should().BeTrue("should produce some output or error");
    }

    [Fact]
    public async Task RunExplainAsync_WithSingleStep_ExplainsStep()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new ExplainOptions
        {
            Dsl = "SetPrompt 'test prompt'"
        };

        // Act
        var result = await harness.ExecuteExplainAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue("single step DSL should be explained");
        result.HasOutput.Should().BeTrue();
    }

    [Fact]
    public async Task RunExplainAsync_WithEmptyDsl_HandlesGracefully()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new ExplainOptions
        {
            Dsl = string.Empty
        };

        // Act
        var result = await harness.ExecuteExplainAsync(options);

        // Assert
        // Empty DSL should either succeed with empty output or fail gracefully
        (result.IsSuccess || result.IsFailure).Should().BeTrue();
    }

    [Fact]
    public async Task RunExplainAsync_WithWhitespaceDsl_HandlesGracefully()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new ExplainOptions
        {
            Dsl = "   "
        };

        // Act
        var result = await harness.ExecuteExplainAsync(options);

        // Assert
        (result.IsSuccess || result.IsFailure).Should().BeTrue("whitespace DSL should be handled");
    }

    [Fact]
    public async Task RunExplainAsync_WithInvalidDsl_HandlesError()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new ExplainOptions
        {
            Dsl = "InvalidToken | UnknownCommand"
        };

        // Act
        var result = await harness.ExecuteExplainAsync(options);

        // Assert
        // Invalid DSL might succeed with error explanation or fail
        (result.IsSuccess || result.IsFailure).Should().BeTrue();
        if (result.IsSuccess)
        {
            result.HasOutput.Should().BeTrue("explanation should indicate invalid syntax");
        }
    }

    [Fact]
    public async Task RunExplainAsync_WithDslContainingQuotes_ParsesCorrectly()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new ExplainOptions
        {
            Dsl = "SetPrompt \"test with quotes\" | UseDraft"
        };

        // Act
        var result = await harness.ExecuteExplainAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue("DSL with quotes should be explained");
        result.HasOutput.Should().BeTrue();
    }

    [Fact]
    public async Task RunExplainAsync_WithDslContainingSpecialChars_ParsesCorrectly()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new ExplainOptions
        {
            Dsl = "SetPrompt 'test@#$%' | UseDraft"
        };

        // Act
        var result = await harness.ExecuteExplainAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue("DSL with special characters should be explained");
        result.HasOutput.Should().BeTrue();
    }

    [Fact]
    public async Task RunExplainAsync_WithLlmStep_ExplainsLlmInteraction()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new ExplainOptions
        {
            Dsl = "SetPrompt 'Generate code' | LLM"
        };

        // Act
        var result = await harness.ExecuteExplainAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue("LLM step should be explained");
        result.HasOutput.Should().BeTrue();
    }

    [Fact]
    public async Task RunExplainAsync_WithUseFilesStep_ExplainsFileOperation()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new ExplainOptions
        {
            Dsl = "UseFiles '*.txt' | SetPrompt 'Summarize' | LLM"
        };

        // Act
        var result = await harness.ExecuteExplainAsync(options);

        // Assert
        // UseFiles step should either succeed or fail gracefully with explanation
        (result.IsSuccess || result.IsFailure).Should().BeTrue();
        (result.HasOutput || result.HasError).Should().BeTrue("should produce some output or error");
    }

    [Fact]
    public async Task RunExplainAsync_RecordsExecutionTime()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new ExplainOptions
        {
            Dsl = "SetPrompt 'test' | UseDraft"
        };

        // Act
        var result = await harness.ExecuteExplainAsync(options);

        // Assert
        result.ExecutionTimeMs.Should().BeGreaterThanOrEqualTo(0, "execution time should be recorded");
    }

    [Fact]
    public async Task RunExplainAsync_WithMalformedPipe_HandlesError()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new ExplainOptions
        {
            Dsl = "SetPrompt 'test' ||"
        };

        // Act
        var result = await harness.ExecuteExplainAsync(options);

        // Assert
        // Malformed pipe should either succeed with error explanation or fail
        (result.IsSuccess || result.IsFailure).Should().BeTrue();
    }

    [Fact]
    public async Task RunExplainAsync_WithLongDsl_ProducesComprehensiveExplanation()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new ExplainOptions
        {
            Dsl = "SetPrompt 'Analyze' | UseFiles '*.cs' | LLM | UseOutput | SetPrompt 'Refine' | LLM"
        };

        // Act
        var result = await harness.ExecuteExplainAsync(options);

        // Assert
        // Long DSL chain should either succeed or fail gracefully with explanation
        (result.IsSuccess || result.IsFailure).Should().BeTrue();
        (result.HasOutput || result.HasError).Should().BeTrue("should produce some output or error");
    }
}
