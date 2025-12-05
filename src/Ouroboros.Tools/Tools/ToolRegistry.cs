// <copyright file="ToolRegistry.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tools;

using System.Collections.Immutable;
using System.Text.Json;

/// <summary>
/// A registry for managing and organizing tools that can be invoked within the pipeline system.
/// Enhanced with functional programming patterns and immutable operations.
/// </summary>
public sealed class ToolRegistry
{
    private readonly ImmutableDictionary<string, ITool> tools;

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolRegistry"/> class.
    /// Creates a new empty ToolRegistry.
    /// </summary>
    public ToolRegistry()
        : this(ImmutableDictionary<string, ITool>.Empty.WithComparers(StringComparer.OrdinalIgnoreCase))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolRegistry"/> class.
    /// Internal constructor for creating registries with existing tools.
    /// </summary>
    private ToolRegistry(ImmutableDictionary<string, ITool> tools)
    {
        this.tools = tools;
    }

    /// <summary>
    /// Registers a tool in the registry, returning a new registry instance.
    /// Follows functional programming principles by returning a new immutable instance.
    /// </summary>
    /// <param name="tool">The tool to register.</param>
    /// <returns>A new ToolRegistry instance with the tool registered.</returns>
    /// <exception cref="ArgumentNullException">Thrown when tool is null.</exception>
    public ToolRegistry WithTool(ITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        return new ToolRegistry(this.tools.SetItem(tool.Name, tool));
    }

    /// <summary>
    /// Gets a tool by its name using Option monad for safe null handling.
    /// </summary>
    /// <param name="name">The name of the tool.</param>
    /// <returns>An Option containing the tool if found, or None if not found.</returns>
    public Option<ITool> GetTool(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return this.tools.TryGetValue(name, out ITool? tool) ? Option<ITool>.Some(tool) : Option<ITool>.None();
    }

    /// <summary>
    /// Legacy method for getting tools. Use GetTool() for Option-based safety.
    /// </summary>
    /// <param name="name">The name of the tool.</param>
    /// <returns>The tool if found, otherwise null.</returns>
    public ITool? Get(string name) => this.tools.TryGetValue(name, out ITool? tool) ? tool : null;

    /// <summary>
    /// Gets all registered tools as an immutable collection.
    /// </summary>
    public IEnumerable<ITool> All => this.tools.Values;

    /// <summary>
    /// Gets the number of registered tools.
    /// </summary>
    public int Count => this.tools.Count;

    /// <summary>
    /// Checks if a tool with the specified name is registered.
    /// </summary>
    /// <param name="name">The tool name to check.</param>
    /// <returns>True if the tool is registered, false otherwise.</returns>
    public bool Contains(string name) => this.tools.ContainsKey(name);

    /// <summary>
    /// Exports the schemas of all registered tools as JSON with Result-based error handling.
    /// </summary>
    /// <returns>A Result containing the JSON schema string or an error message.</returns>
    public Result<string> SafeExportSchemas()
    {
        try
        {
            var schemas = this.tools.Values.Select(t => new
            {
                name = t.Name,
                description = t.Description,
                parameters = string.IsNullOrEmpty(t.JsonSchema) ? null : JsonSerializer.Deserialize<object>(t.JsonSchema!),
            });

            string json = ToolJson.Serialize(schemas);
            return Result<string>.Success(json);
        }
        catch (Exception ex)
        {
            return Result<string>.Failure($"Schema export failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Legacy method for exporting schemas. Use SafeExportSchemas() for Result-based error handling.
    /// </summary>
    /// <returns>A JSON string containing all tool schemas.</returns>
    public string ExportSchemas()
    {
        return this.SafeExportSchemas().GetValueOrDefault("[]"); // Return empty array on failure
    }

    /// <summary>
    /// Registers a delegate function as a tool (synchronous), returning a new registry.
    /// </summary>
    /// <param name="name">The name of the tool.</param>
    /// <param name="description">A description of what the tool does.</param>
    /// <param name="function">The function to execute.</param>
    /// <returns>A new ToolRegistry with the function registered as a tool.</returns>
    public ToolRegistry WithFunction(string name, string description, Func<string, string> function)
        => this.WithTool(new DelegateTool(name, description, function));

    /// <summary>
    /// Registers a delegate function as a tool (asynchronous), returning a new registry.
    /// </summary>
    /// <param name="name">The name of the tool.</param>
    /// <param name="description">A description of what the tool does.</param>
    /// <param name="function">The async function to execute.</param>
    /// <returns>A new ToolRegistry with the function registered as a tool.</returns>
    public ToolRegistry WithFunction(string name, string description, Func<string, Task<string>> function)
        => this.WithTool(new DelegateTool(name, description, function));

    /// <summary>
    /// Registers a strongly-typed delegate function as a tool, returning a new registry.
    /// </summary>
    /// <typeparam name="T">The input type for the tool (must be JSON deserializable).</typeparam>
    /// <param name="name">The name of the tool.</param>
    /// <param name="description">A description of what the tool does.</param>
    /// <param name="function">The typed async function to execute.</param>
    /// <returns>A new ToolRegistry with the function registered as a tool.</returns>
    public ToolRegistry WithFunction<T>(string name, string description, Func<T, Task<string>> function)
        => this.WithTool(DelegateTool.FromJson(name, description, function));

    /// <summary>
    /// Creates a new ToolRegistry with multiple tools registered at once.
    /// </summary>
    /// <param name="tools">The tools to register.</param>
    /// <returns>A new ToolRegistry with all tools registered.</returns>
    public ToolRegistry WithTools(params ITool[] tools)
        => this.WithTools(tools.AsEnumerable());

    /// <summary>
    /// Creates a new ToolRegistry with multiple tools registered at once.
    /// </summary>
    /// <param name="tools">The tools to register.</param>
    /// <returns>A new ToolRegistry with all tools registered.</returns>
    public ToolRegistry WithTools(IEnumerable<ITool> tools)
    {
        ToolRegistry newRegistry = this;
        foreach (ITool tool in tools)
        {
            newRegistry = newRegistry.WithTool(tool);
        }

        return newRegistry;
    }

    /// <summary>
    /// Removes a tool from the registry, returning a new instance.
    /// </summary>
    /// <param name="name">The name of the tool to remove.</param>
    /// <returns>A new ToolRegistry without the specified tool.</returns>
    public ToolRegistry WithoutTool(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return new ToolRegistry(this.tools.Remove(name));
    }


    // Legacy mutable registration methods for backward compatibility.
    // These will be removed in future versions.

    /// <summary>
    /// Legacy method for registering delegate tools.
    /// </summary>
    [Obsolete("Use WithFunction() for immutable updates.")]
    public void Register(string name, string description, Func<string, string> function)
    {
        throw new InvalidOperationException("Use WithFunction() for immutable updates.");
    }

    /// <summary>
    /// Legacy method for registering async delegate tools.
    /// </summary>
    [Obsolete("Use WithFunction() for immutable updates.")]
    public void Register(string name, string description, Func<string, Task<string>> function)
    {
        throw new InvalidOperationException("Use WithFunction() for immutable updates.");
    }

    /// <summary>
    /// Legacy method for registering typed delegate tools.
    /// </summary>
    [Obsolete("Use WithFunction<T>() for immutable updates.")]
    public void Register<T>(string name, string description, Func<T, Task<string>> function)
    {
        throw new InvalidOperationException("Use WithFunction<T>() for immutable updates.");
    }


    /// <summary>
    /// Creates a default ToolRegistry with common tools pre-registered.
    /// </summary>
    /// <returns>A ToolRegistry with basic tools like math.</returns>
    public static ToolRegistry CreateDefault()
    {
        return new ToolRegistry()
            .WithTool(new MathTool());
    }
}
