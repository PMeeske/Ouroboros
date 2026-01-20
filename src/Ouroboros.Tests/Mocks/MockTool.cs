// <copyright file="MockTool.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Mocks;

using Ouroboros.Core.Monads;
using Ouroboros.Tools;

/// <summary>
/// Mock implementation of ITool for testing purposes.
/// </summary>
public class MockTool : ITool
{
    private readonly Func<string, CancellationToken, Task<Result<string, string>>> invokeFunc;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockTool"/> class.
    /// </summary>
    /// <param name="name">The tool name.</param>
    /// <param name="description">The tool description.</param>
    /// <param name="jsonSchema">Optional JSON schema.</param>
    public MockTool(string name, string description = "Mock tool", string? jsonSchema = null)
    {
        Name = name;
        Description = description;
        JsonSchema = jsonSchema;
        this.invokeFunc = (input, ct) => Task.FromResult(Result<string, string>.Success($"Result: {input}"));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MockTool"/> class with custom invoke behavior.
    /// </summary>
    /// <param name="name">The tool name.</param>
    /// <param name="description">The tool description.</param>
    /// <param name="invokeFunc">Custom invoke function.</param>
    public MockTool(string name, string description, Func<string, Result<string, string>> invokeFunc)
    {
        Name = name;
        Description = description;
        this.invokeFunc = (input, ct) => Task.FromResult(invokeFunc(input));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MockTool"/> class with async invoke behavior.
    /// </summary>
    /// <param name="name">The tool name.</param>
    /// <param name="description">The tool description.</param>
    /// <param name="invokeFunc">Async custom invoke function.</param>
    public MockTool(string name, string description, Func<string, CancellationToken, Task<Result<string, string>>> invokeFunc)
    {
        Name = name;
        Description = description;
        this.invokeFunc = invokeFunc;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public string? JsonSchema { get; }

    /// <summary>
    /// Gets the number of times InvokeAsync was called.
    /// </summary>
    public int InvokeCount { get; private set; }

    /// <summary>
    /// Gets the last input passed to InvokeAsync.
    /// </summary>
    public string? LastInput { get; private set; }

    /// <inheritdoc />
    public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        InvokeCount++;
        LastInput = input;
        return this.invokeFunc(input, ct);
    }
}

/// <summary>
/// Mock tool that always succeeds with a fixed result.
/// </summary>
public class SuccessMockTool : ITool
{
    private readonly string result;

    /// <summary>
    /// Initializes a new instance of the <see cref="SuccessMockTool"/> class.
    /// </summary>
    /// <param name="name">The tool name.</param>
    /// <param name="result">The result to return.</param>
    public SuccessMockTool(string name, string result)
    {
        Name = name;
        this.result = result;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string Description => $"Always returns: {result}";

    /// <inheritdoc />
    public string? JsonSchema => null;

    /// <inheritdoc />
    public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        return Task.FromResult(Result<string, string>.Success(result));
    }
}

/// <summary>
/// Mock tool that always fails with a fixed error.
/// </summary>
public class FailingMockTool : ITool
{
    private readonly string error;

    /// <summary>
    /// Initializes a new instance of the <see cref="FailingMockTool"/> class.
    /// </summary>
    /// <param name="name">The tool name.</param>
    /// <param name="error">The error message to return.</param>
    public FailingMockTool(string name, string error)
    {
        Name = name;
        this.error = error;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string Description => $"Always fails with: {error}";

    /// <inheritdoc />
    public string? JsonSchema => null;

    /// <inheritdoc />
    public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        return Task.FromResult(Result<string, string>.Failure(error));
    }
}

/// <summary>
/// Mock tool that throws an exception.
/// </summary>
public class ThrowingMockTool : ITool
{
    private readonly Exception exception;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThrowingMockTool"/> class.
    /// </summary>
    /// <param name="name">The tool name.</param>
    /// <param name="exception">The exception to throw.</param>
    public ThrowingMockTool(string name, Exception? exception = null)
    {
        Name = name;
        this.exception = exception ?? new InvalidOperationException("Tool error");
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string Description => "A tool that throws";

    /// <inheritdoc />
    public string? JsonSchema => null;

    /// <inheritdoc />
    public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        throw exception;
    }
}

/// <summary>
/// Mock tool with a delay for testing async behavior.
/// </summary>
public class DelayedMockTool : ITool
{
    private readonly string result;
    private readonly TimeSpan delay;

    /// <summary>
    /// Initializes a new instance of the <see cref="DelayedMockTool"/> class.
    /// </summary>
    /// <param name="name">The tool name.</param>
    /// <param name="result">The result to return after delay.</param>
    /// <param name="delay">The delay duration.</param>
    public DelayedMockTool(string name, string result, TimeSpan delay)
    {
        Name = name;
        this.result = result;
        this.delay = delay;
    }

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string Description => "A delayed tool";

    /// <inheritdoc />
    public string? JsonSchema => null;

    /// <inheritdoc />
    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        await Task.Delay(delay, ct);
        return Result<string, string>.Success(result);
    }
}
