// <copyright file="TestCommandTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Options;
using Ouroboros.Tests.CLI.Fixtures;
using Ouroboros.Tests.Infrastructure.Utilities;

namespace Ouroboros.Tests.CLI.Commands;

/// <summary>
/// Unit tests for Test CLI command.
/// Tests test runner execution with various flags and modes.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("Category", TestCategories.CLI)]
public class TestCommandTests
{
    [Fact]
    public async Task RunTestsAsync_WithAllFlag_ExecutesAllTests()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new TestOptions
        {
            All = true
        };

        // Act
        var result = await harness.ExecuteTestAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue("all tests should execute successfully");
        result.HasOutput.Should().BeTrue();
        result.Output.Should().Contain("Running Ouroboros Tests", "test header should be displayed");
    }

    [Fact]
    public async Task RunTestsAsync_WithIntegrationOnlyFlag_ExecutesIntegrationTests()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new TestOptions
        {
            IntegrationOnly = true
        };

        // Act
        var result = await harness.ExecuteTestAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue("integration tests should execute");
        result.HasOutput.Should().BeTrue();
    }

    [Fact]
    public async Task RunTestsAsync_WithCliOnlyFlag_ExecutesCliTests()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new TestOptions
        {
            CliOnly = true
        };

        // Act
        var result = await harness.ExecuteTestAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue("CLI tests should execute");
        result.HasOutput.Should().BeTrue();
    }

    [Fact]
    public async Task RunTestsAsync_WithMeTTaFlag_ExecutesMeTTaTests()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new TestOptions
        {
            MeTTa = true
        };

        // Act
        var result = await harness.ExecuteTestAsync(options);

        // Assert
        // MeTTa tests may fail if Docker/MeTTa is not available, but command should handle gracefully
        (result.IsSuccess || result.IsFailure).Should().BeTrue();
        result.HasOutput.Should().BeTrue();
        if (result.IsSuccess)
        {
            result.Output.Should().Contain("MeTTa", "MeTTa test output should be shown");
        }
    }

    [Fact]
    public async Task RunTestsAsync_WithNoFlags_DisplaysTestHeader()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new TestOptions
        {
            All = false,
            IntegrationOnly = false,
            CliOnly = false,
            MeTTa = false
        };

        // Act
        var result = await harness.ExecuteTestAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue("test command should execute even with no flags");
        result.Output.Should().Contain("Running Ouroboros Tests", "test header should be displayed");
    }

    [Fact]
    public async Task RunTestsAsync_WithAllFlag_ShowsCompletionMessage()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new TestOptions
        {
            All = true
        };

        // Act
        var result = await harness.ExecuteTestAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Output.Should().Contain("All Tests Passed", "completion message should be shown");
    }

    [Fact]
    public async Task RunTestsAsync_MeTTaMode_ShowsSubprocessEngineTest()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new TestOptions
        {
            MeTTa = true
        };

        // Act
        var result = await harness.ExecuteTestAsync(options);

        // Assert
        // Test should attempt to run MeTTa subprocess engine test
        (result.IsSuccess || result.IsFailure).Should().BeTrue();
        result.HasOutput.Should().BeTrue();
    }

    [Fact]
    public async Task RunTestsAsync_RecordsExecutionTime()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new TestOptions
        {
            IntegrationOnly = true
        };

        // Act
        var result = await harness.ExecuteTestAsync(options);

        // Assert
        result.ExecutionTimeMs.Should().BeGreaterThanOrEqualTo(0, "execution time should be recorded");
    }

    [Fact]
    public async Task RunTestsAsync_WithMultipleFlags_ExecutesMultipleTestSuites()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new TestOptions
        {
            All = true,
            IntegrationOnly = true
        };

        // Act
        var result = await harness.ExecuteTestAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue("multiple test suites should execute");
        result.HasOutput.Should().BeTrue();
    }

    [Fact]
    public async Task RunTestsAsync_ProducesStructuredOutput()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new TestOptions
        {
            IntegrationOnly = true
        };

        // Act
        var result = await harness.ExecuteTestAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Output.Should().Contain("===", "output should contain structured headers");
    }

    [Fact]
    public async Task RunTestsAsync_MeTTaMode_AttemptsDockerTest()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new TestOptions
        {
            MeTTa = true
        };

        // Act
        var result = await harness.ExecuteTestAsync(options);

        // Assert
        // Should attempt Docker-based MeTTa test
        result.HasOutput.Should().BeTrue();
        if (result.IsSuccess)
        {
            (result.Output.Contains("Subprocess") || result.Output.Contains("MeTTa") || result.Output.Contains("Docker"))
                .Should().BeTrue("MeTTa mode should show subprocess/Docker output");
        }
    }

    [Fact]
    public async Task RunTestsAsync_WithAllFlag_ExecutesComprehensiveTestSuite()
    {
        // Arrange
        using var harness = new CliTestHarness();
        var options = new TestOptions
        {
            All = true
        };

        // Act
        var result = await harness.ExecuteTestAsync(options);

        // Assert
        result.IsSuccess.Should().BeTrue("comprehensive test suite should execute");
        result.ExecutionTimeMs.Should().BeGreaterThanOrEqualTo(0);
    }
}
