// <copyright file="DelegateToolTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests;

using FluentAssertions;
using Ouroboros.Core.Monads;
using Ouroboros.Tools;
using Xunit;

/// <summary>
/// Tests for the DelegateTool implementation.
/// </summary>
[Trait("Category", "Unit")]
public class DelegateToolTests
{
    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange & Act
        var tool = new DelegateTool(
            "test",
            "description",
            (input, ct) => Task.FromResult(Result<string, string>.Success("result")));

        // Assert
        tool.Name.Should().Be("test");
        tool.Description.Should().Be("description");
        tool.JsonSchema.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithSchema_StoresSchema()
    {
        // Arrange & Act
        var tool = new DelegateTool(
            "test",
            "description",
            (input, ct) => Task.FromResult(Result<string, string>.Success("result")),
            "{\"type\": \"object\"}");

        // Assert
        tool.JsonSchema.Should().Be("{\"type\": \"object\"}");
    }

    [Fact]
    public void Constructor_WithNullName_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DelegateTool(
                null!,
                "description",
                (input, ct) => Task.FromResult(Result<string, string>.Success("result"))));
    }

    [Fact]
    public void Constructor_WithNullDescription_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DelegateTool(
                "test",
                null!,
                (input, ct) => Task.FromResult(Result<string, string>.Success("result"))));
    }

    [Fact]
    public void Constructor_WithNullExecutor_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new DelegateTool(
                "test",
                "description",
                (Func<string, CancellationToken, Task<Result<string, string>>>)null!));
    }

    [Fact]
    public async Task InvokeAsync_ExecutesDelegate()
    {
        // Arrange
        var tool = new DelegateTool(
            "test",
            "description",
            (input, ct) => Task.FromResult(Result<string, string>.Success($"processed: {input}")));

        // Act
        var result = await tool.InvokeAsync("input");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("processed: input");
    }

    [Fact]
    public async Task InvokeAsync_WithFailure_ReturnsFailure()
    {
        // Arrange
        var tool = new DelegateTool(
            "test",
            "description",
            (input, ct) => Task.FromResult(Result<string, string>.Failure("error occurred")));

        // Act
        var result = await tool.InvokeAsync("input");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("error occurred");
    }

    [Fact]
    public async Task InvokeAsync_PassesCancellationToken()
    {
        // Arrange
        CancellationToken receivedToken = default;
        var tool = new DelegateTool(
            "test",
            "description",
            (input, ct) =>
            {
                receivedToken = ct;
                return Task.FromResult(Result<string, string>.Success("result"));
            });
        using var cts = new CancellationTokenSource();

        // Act
        await tool.InvokeAsync("input", cts.Token);

        // Assert
        receivedToken.Should().Be(cts.Token);
    }

    [Fact]
    public void Constructor_WithAsyncFunc_CreatesInstance()
    {
        // Arrange & Act
        var tool = new DelegateTool(
            "test",
            "description",
            (Func<string, Task<string>>)(input => Task.FromResult($"result: {input}")));

        // Assert
        tool.Name.Should().Be("test");
        tool.Description.Should().Be("description");
    }

    [Fact]
    public async Task InvokeAsync_WithAsyncFunc_ReturnsSuccess()
    {
        // Arrange
        var tool = new DelegateTool(
            "test",
            "description",
            (Func<string, Task<string>>)(input => Task.FromResult($"processed: {input}")));

        // Act
        var result = await tool.InvokeAsync("data");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("processed: data");
    }

    [Fact]
    public async Task InvokeAsync_WithAsyncFuncThrowingException_ReturnsFailure()
    {
        // Arrange
        var tool = new DelegateTool(
            "test",
            "description",
            (Func<string, Task<string>>)(input =>
            {
                throw new InvalidOperationException("test error");
            }));

        // Act
        var result = await tool.InvokeAsync("input");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("test error");
    }

    [Fact]
    public void Constructor_WithSyncFunc_CreatesInstance()
    {
        // Arrange & Act
        var tool = new DelegateTool(
            "test",
            "description",
            (input) => $"result: {input}");

        // Assert
        tool.Name.Should().Be("test");
        tool.Description.Should().Be("description");
    }

    [Fact]
    public async Task InvokeAsync_WithSyncFunc_ReturnsSuccess()
    {
        // Arrange
        var tool = new DelegateTool(
            "test",
            "description",
            (input) => $"processed: {input}");

        // Act
        var result = await tool.InvokeAsync("data");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be("processed: data");
    }

    [Fact]
    public async Task InvokeAsync_WithSyncFuncThrowingException_ReturnsFailure()
    {
        // Arrange
        var tool = new DelegateTool(
            "test",
            "description",
            (Func<string, string>)(input => throw new InvalidOperationException("sync error")));

        // Act
        var result = await tool.InvokeAsync("input");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("sync error");
    }

    [Fact]
    public void FromJson_CreatesToolWithSchema()
    {
        // Arrange
        var tool = DelegateTool.FromJson<TestArgs>(
            "test",
            "description",
            args => Task.FromResult($"Value: {args.Value}"));

        // Act & Assert
        tool.Name.Should().Be("test");
        tool.Description.Should().Be("description");
        tool.JsonSchema.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task FromJson_WithValidJson_ExecutesFunction()
    {
        // Arrange
        var tool = DelegateTool.FromJson<TestArgs>(
            "test",
            "description",
            args => Task.FromResult($"Received: {args.Value}"));

        // Act
        var result = await tool.InvokeAsync("{\"value\": \"test\"}");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Contain("Received: test");
    }

    [Fact]
    public async Task FromJson_WithInvalidJson_ReturnsFailure()
    {
        // Arrange
        var tool = DelegateTool.FromJson<TestArgs>(
            "test",
            "description",
            args => Task.FromResult("result"));

        // Act
        var result = await tool.InvokeAsync("invalid json");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("JSON parse failed");
    }

    [Fact]
    public async Task FromJson_WhenFunctionThrows_ReturnsFailure()
    {
        // Arrange
        var tool = DelegateTool.FromJson<TestArgs>(
            "test",
            "description",
            args => throw new InvalidOperationException("function error"));

        // Act
        var result = await tool.InvokeAsync("{\"value\": \"test\"}");

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("JSON parse failed");
    }

    // Test helper class
    private class TestArgs
    {
        public string Value { get; set; } = string.Empty;
    }
}
