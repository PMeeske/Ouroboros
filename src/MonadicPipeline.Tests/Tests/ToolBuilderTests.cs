// <copyright file="ToolBuilderTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Tests;

using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using LangChainPipeline.Core.Monads;
using LangChainPipeline.Tools;
using Xunit;

/// <summary>
/// Tests for the ToolBuilder helper methods that compose tools.
/// </summary>
public class ToolBuilderTests
{
    [Fact]
    public async Task Chain_ComposesToolsSequentially()
    {
        // Arrange
        var tool = ToolBuilder.Chain(
            "pipeline",
            "Runs tools sequentially",
            new DelegateTool("uppercase", "Upper", value => value.ToUpperInvariant()),
            new DelegateTool("exclaim", "Exclaim", value => value + "!")
        );

        // Act
        Result<string, string> result = await tool.InvokeAsync("monad");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("MONAD!");
    }

    [Fact]
    public async Task Chain_StopsWhenToolFails()
    {
        // Arrange
        int secondInvocations = 0;
        ITool tool = ToolBuilder.Chain(
            "stopper",
            "Stops on failure",
            new DelegateTool("first", "First", (_, __) => Task.FromResult(Result<string, string>.Failure("fail"))),
            new DelegateTool("second", "Second", async (value, ct) =>
            {
                _ = ct;
                secondInvocations++;
                return Result<string, string>.Success(value);
            })
        );

        // Act
        Result<string, string> result = await tool.InvokeAsync("input");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("fail");
        secondInvocations.Should().Be(0);
    }

    [Fact]
    public async Task Chain_ReturnsCancellationWhenTokenCancelled()
    {
        // Arrange
        ITool tool = ToolBuilder.Chain(
            "cancel",
            "Handles cancellation",
            new DelegateTool("noop", "Noop", value => value)
        );

        using CancellationTokenSource cts = new CancellationTokenSource();
        cts.Cancel();

        // Act
        Result<string, string> result = await tool.InvokeAsync("anything", cts.Token);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Operation cancelled");
    }

    [Fact]
    public async Task FirstSuccess_ReturnsFirstSuccessfulResult()
    {
        // Arrange
        ITool tool = ToolBuilder.FirstSuccess(
            "first-success",
            "Uses first success",
            new DelegateTool("fail", "Fail", (_, __) => Task.FromResult(Result<string, string>.Failure("nope"))),
            new DelegateTool("ok", "Ok", value => value + "-ok"),
            new DelegateTool("skip", "Skip", value => value + "-skip")
        );

        // Act
        Result<string, string> result = await tool.InvokeAsync("input");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("input-ok");
    }

    [Fact]
    public async Task FirstSuccess_ReturnsFailureWhenAllFail()
    {
        // Arrange
        ITool tool = ToolBuilder.FirstSuccess(
            "all-fail",
            "All fail",
            new DelegateTool("one", "One", (_, __) => Task.FromResult(Result<string, string>.Failure("first"))),
            new DelegateTool("two", "Two", (_, __) => Task.FromResult(Result<string, string>.Failure("second")))
        );

        // Act
        Result<string, string> result = await tool.InvokeAsync("input");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("All tools failed");
    }

    [Fact]
    public async Task Conditional_SelectsToolBasedOnInput()
    {
        // Arrange
        ITool tool = ToolBuilder.Conditional(
            "conditional",
            "Selects tool",
            value => value switch
            {
                "upper" => new DelegateTool("upper", "Upper", s => s.ToUpperInvariant()),
                "lower" => new DelegateTool("lower", "Lower", s => s.ToLowerInvariant()),
                _ => new DelegateTool("noop", "Noop", s => s)
            });

        // Act
        Result<string, string> upper = await tool.InvokeAsync("upper");
        Result<string, string> lower = await tool.InvokeAsync("lower");

        // Assert
        upper.Value.Should().Be("UPPER");
        lower.Value.Should().Be("lower");
    }

    [Fact]
    public async Task Conditional_ReturnsFailureWhenSelectorThrows()
    {
        // Arrange
        ITool tool = ToolBuilder.Conditional(
            "conditional",
            "Selector throws",
            _ => throw new InvalidOperationException("boom"));

        // Act
        Result<string, string> result = await tool.InvokeAsync("anything");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Tool selection failed");
        result.Error.Should().Contain("boom");
    }
}
