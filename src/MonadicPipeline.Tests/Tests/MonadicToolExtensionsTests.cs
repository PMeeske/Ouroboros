// <copyright file="MonadicToolExtensionsTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Tests;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using LangChainPipeline.Core.Kleisli;
using LangChainPipeline.Core.Monads;
using LangChainPipeline.Core.Steps;
using LangChainPipeline.Tools;
using Xunit;

/// <summary>
/// Tests for monadic helpers that adapt tools into pipeline steps.
/// </summary>
public class MonadicToolExtensionsTests
{
    [Fact]
    public async Task ToStep_InvokesUnderlyingTool()
    {
        // Arrange
        Result<string, string>? capturedResult = null;
        var tool = new DelegateTool("echo", "Echo tool", async (string value, CancellationToken ct) =>
            {
                _ = ct;
                var result = Result<string, string>.Success($"[{value}]");
                capturedResult = result;
                return result;
            });

        // Act
        Step<string, Result<string, string>> step = tool.ToStep();
        Result<string, string> outcome = await step("hello");

        // Assert
        capturedResult.Should().NotBeNull();
        outcome.Should().BeEquivalentTo(capturedResult);
        outcome.IsSuccess.Should().BeTrue();
        outcome.Value.Should().Be("[hello]");
    }

    [Fact]
    public async Task ToKleisli_ProducesEquivalentDelegate()
    {
        // Arrange
        var tool = new DelegateTool("upper", "Uppercase", value => value.ToUpperInvariant());

        // Act
        KleisliResult<string, string, string> kleisli = tool.ToKleisli();
        Result<string, string> outcome = await kleisli("test");

        // Assert
        outcome.IsSuccess.Should().BeTrue();
        outcome.Value.Should().Be("TEST");
    }

    [Fact]
    public async Task Then_ChainsOnSuccess()
    {
        // Arrange
        var first = new DelegateTool("first", "First", value => $"{value}-one");
        var second = new DelegateTool("second", "Second", value => $"{value}-two");

        // Act
        Step<string, Result<string, string>> step = first.Then(second);
        Result<string, string> result = await step("start");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("start-one-two");
    }

    [Fact]
    public async Task Then_ShortCircuitsOnFailure()
    {
        // Arrange
        var first = new DelegateTool("fail", "Failure", (string _, CancellationToken __) => Task.FromResult(Result<string, string>.Failure("boom")));
        int secondInvocations = 0;
        var second = new DelegateTool("second", "Second", (string value, CancellationToken ct) =>
            {
                _ = ct;
                secondInvocations++;
                return Task.FromResult(Result<string, string>.Success(value + "-two"));
            });

        // Act
        Step<string, Result<string, string>> step = first.Then(second);
        Result<string, string> result = await step("start");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("boom");
        secondInvocations.Should().Be(0);
    }

    [Fact]
    public async Task OrElse_UsesFallbackOnFailure()
    {
        // Arrange
        var failing = new DelegateTool("fail", "Failure", (_, _) => Task.FromResult(Result<string, string>.Failure("nope")));
        var fallback = new DelegateTool("fallback", "Fallback", value => $"{value}-ok");

        // Act
        Step<string, Result<string, string>> step = failing.OrElse(fallback);
        Result<string, string> result = await step("input");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("input-ok");
    }

    [Fact]
    public async Task OrElse_SkipsFallbackOnSuccess()
    {
        // Arrange
        int primaryInvocations = 0;
        int fallbackInvocations = 0;
        var primary = new DelegateTool("primary", "Primary", value =>
            {
                primaryInvocations++;
                return value;
            });
        var fallback = new DelegateTool("fallback", "Fallback", value =>
            {
                fallbackInvocations++;
                return "should-not-run";
            });

        // Act
        Step<string, Result<string, string>> step = primary.OrElse(fallback);
        Result<string, string> result = await step("input");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("input");
        primaryInvocations.Should().Be(1);
        fallbackInvocations.Should().Be(0);
    }

    [Fact]
    public async Task Map_TransformsSuccessfulResult()
    {
        // Arrange
        var tool = new DelegateTool("length", "Length", value => value);

        // Act
        Step<string, Result<int, string>> step = tool.Map(value => value.Length);
        Result<int, string> result = await step("monad");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(5);
    }

    [Fact]
    public async Task ToContextual_EmitsLogEntries()
    {
        // Arrange
        var tool = new DelegateTool("logger", "Logger", value => $"{value}!");

        // Act
        ContextualStep<string, Result<string, string>, IDictionary<string, object>> contextual = tool.ToContextual<IDictionary<string, object>>();
        (Result<string, string> result, List<string> logs) = await contextual("ping", new Dictionary<string, object>());

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("ping!");
        logs.Should().ContainSingle().Which.Should().Be("Tool 'logger' executed");
    }

    [Fact]
    public async Task ToContextual_UsesCustomLogMessage()
    {
        // Arrange
        var tool = new DelegateTool("logger", "Logger", value => value);

        // Act
        ContextualStep<string, Result<string, string>, string> contextual = tool.ToContextual<string>("custom log");
        (Result<string, string> result, List<string> logs) = await contextual("data", "context");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("data");
        logs.Should().ContainSingle().Which.Should().Be("custom log");
    }
}
