// <copyright file="DelegateTool.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Tools;

/// <summary>
/// A tool implementation that wraps delegate functions for easy tool creation.
/// </summary>
public sealed class DelegateTool : ITool
{
    private readonly Func<string, CancellationToken, Task<Result<string, string>>> executor;

    /// <inheritdoc />
    public string Name { get; }

    /// <inheritdoc />
    public string Description { get; }

    /// <inheritdoc />
    public string? JsonSchema { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegateTool"/> class.
    /// </summary>
    /// <param name="name">The name of the tool.</param>
    /// <param name="description">The description of the tool.</param>
    /// <param name="executor">The executor function.</param>
    /// <param name="schema">Optional JSON schema for the tool's input.</param>
    public DelegateTool(string name, string description, Func<string, CancellationToken, Task<Result<string, string>>> executor, string? schema = null)
    {
        this.Name = name ?? throw new ArgumentNullException(nameof(name));
        this.Description = description ?? throw new ArgumentNullException(nameof(description));
        this.executor = executor ?? throw new ArgumentNullException(nameof(executor));
        this.JsonSchema = schema;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegateTool"/> class with an async function.
    /// </summary>
    /// <param name="name">The name of the tool.</param>
    /// <param name="description">The description of the tool.</param>
    /// <param name="executor">The async executor function.</param>
    public DelegateTool(string name, string description, Func<string, Task<string>> executor)
        : this(name, description, async (s, _) =>
        {
            try
            {
                string result = await executor(s);
                return Result<string, string>.Success(result);
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
        })
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DelegateTool"/> class with a synchronous function.
    /// </summary>
    /// <param name="name">The name of the tool.</param>
    /// <param name="description">The description of the tool.</param>
    /// <param name="executor">The synchronous executor function.</param>
    public DelegateTool(string name, string description, Func<string, string> executor)
        : this(name, description, (s, _) =>
        {
            try
            {
                string result = executor(s);
                return Task.FromResult(Result<string, string>.Success(result));
            }
            catch (Exception ex)
            {
                return Task.FromResult(Result<string, string>.Failure(ex.Message));
            }
        })
    {
    }

    /// <summary>
    /// Creates a delegate tool from a strongly-typed JSON function.
    /// </summary>
    /// <typeparam name="T">The input type for the tool.</typeparam>
    /// <param name="name">The name of the tool.</param>
    /// <param name="description">The description of the tool.</param>
    /// <param name="function">The typed function to execute.</param>
    /// <returns>A new delegate tool.</returns>
    public static DelegateTool FromJson<T>(string name, string description, Func<T, Task<string>> function)
    {
        string schema = SchemaGenerator.GenerateSchema(typeof(T));
        return new DelegateTool(name, description, async (raw, _) =>
        {
            try
            {
                T args = ToolJson.Deserialize<T>(raw);
                string result = await function(args);
                return Result<string, string>.Success(result);
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"JSON parse failed: {ex.Message}");
            }
        }, schema);
    }

    /// <inheritdoc />
    public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        => this.executor(input, ct);
}
