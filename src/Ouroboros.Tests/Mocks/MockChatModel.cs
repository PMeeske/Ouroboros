// <copyright file="MockChatModel.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.Mocks;

using Ouroboros.Providers;

/// <summary>
/// Mock implementation of IChatCompletionModel for testing purposes.
/// </summary>
public class MockChatModel : IChatCompletionModel
{
    private readonly string response;
    private readonly bool shouldCheckCancellation;
    private readonly Func<string, string>? responseFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockChatModel"/> class with a fixed response.
    /// </summary>
    /// <param name="response">The response to return.</param>
    /// <param name="shouldCheckCancellation">Whether to check cancellation token.</param>
    public MockChatModel(string response, bool shouldCheckCancellation = false)
    {
        this.response = response;
        this.shouldCheckCancellation = shouldCheckCancellation;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MockChatModel"/> class with a response factory.
    /// </summary>
    /// <param name="responseFactory">Factory function to generate responses based on prompts.</param>
    public MockChatModel(Func<string, string> responseFactory)
    {
        this.responseFactory = responseFactory;
        this.response = string.Empty;
    }

    /// <summary>
    /// Gets the number of times GenerateTextAsync was called.
    /// </summary>
    public int CallCount { get; private set; }

    /// <summary>
    /// Gets the last prompt passed to GenerateTextAsync.
    /// </summary>
    public string? LastPrompt { get; private set; }

    /// <summary>
    /// Generates text using the mock configuration.
    /// </summary>
    /// <param name="prompt">The prompt to respond to.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The configured response.</returns>
    public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        CallCount++;
        LastPrompt = prompt;

        if (this.shouldCheckCancellation)
        {
            ct.ThrowIfCancellationRequested();
        }

        var result = this.responseFactory != null ? this.responseFactory(prompt) : this.response;
        return Task.FromResult(result);
    }
}

/// <summary>
/// Mock implementation of IChatCompletionModel that throws exceptions.
/// </summary>
public class ThrowingMockChatModel : IChatCompletionModel
{
    private readonly Exception exception;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThrowingMockChatModel"/> class.
    /// </summary>
    /// <param name="exception">The exception to throw.</param>
    public ThrowingMockChatModel(Exception exception)
    {
        this.exception = exception;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ThrowingMockChatModel"/> class with default exception.
    /// </summary>
    public ThrowingMockChatModel()
        : this(new InvalidOperationException("Mock model error"))
    {
    }

    /// <summary>
    /// Always throws the configured exception.
    /// </summary>
    public Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        throw this.exception;
    }
}

/// <summary>
/// Mock implementation that returns responses with simulated delay.
/// </summary>
public class DelayedMockChatModel : IChatCompletionModel
{
    private readonly string response;
    private readonly TimeSpan delay;

    /// <summary>
    /// Initializes a new instance of the <see cref="DelayedMockChatModel"/> class.
    /// </summary>
    /// <param name="response">The response to return.</param>
    /// <param name="delay">The delay before returning.</param>
    public DelayedMockChatModel(string response, TimeSpan delay)
    {
        this.response = response;
        this.delay = delay;
    }

    /// <summary>
    /// Generates text after a simulated delay.
    /// </summary>
    public async Task<string> GenerateTextAsync(string prompt, CancellationToken ct = default)
    {
        await Task.Delay(this.delay, ct);
        return this.response;
    }
}
