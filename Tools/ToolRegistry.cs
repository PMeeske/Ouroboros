using System.Text.Json;

namespace LangChainPipeline.Tools;

/// <summary>
/// A registry for managing and organizing tools that can be invoked within the pipeline system.
/// </summary>
public sealed class ToolRegistry
{
    private readonly Dictionary<string, ITool> _tools = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Registers a tool in the registry.
    /// </summary>
    /// <param name="tool">The tool to register.</param>
    /// <exception cref="ArgumentNullException">Thrown when tool is null.</exception>
    public void Register(ITool tool)
    {
        ArgumentNullException.ThrowIfNull(tool);
        _tools[tool.Name] = tool;
    }

    /// <summary>
    /// Gets a tool by its name.
    /// </summary>
    /// <param name="name">The name of the tool.</param>
    /// <returns>The tool if found, otherwise null.</returns>
    public ITool? Get(string name) => _tools.TryGetValue(name, out ITool? tool) ? tool : null;

    /// <summary>
    /// Gets all registered tools.
    /// </summary>
    public IEnumerable<ITool> All => _tools.Values;

    /// <summary>
    /// Exports the schemas of all registered tools as JSON.
    /// </summary>
    /// <returns>A JSON string containing all tool schemas.</returns>
    public string ExportSchemas()
    {
        var schemas = _tools.Values.Select(t => new
        {
            name = t.Name,
            description = t.Description,
            parameters = string.IsNullOrEmpty(t.JsonSchema) ? null : JsonSerializer.Deserialize<object>(t.JsonSchema!)
        });
        
        return ToolJson.Serialize(schemas);
    }

    /// <summary>
    /// Registers a delegate function as a tool (synchronous).
    /// </summary>
    /// <param name="name">The name of the tool.</param>
    /// <param name="description">A description of what the tool does.</param>
    /// <param name="function">The function to execute.</param>
    public void Register(string name, string description, Func<string, string> function)
        => Register(new DelegateTool(name, description, function));

    /// <summary>
    /// Registers a delegate function as a tool (asynchronous).
    /// </summary>
    /// <param name="name">The name of the tool.</param>
    /// <param name="description">A description of what the tool does.</param>
    /// <param name="function">The async function to execute.</param>
    public void Register(string name, string description, Func<string, Task<string>> function)
        => Register(new DelegateTool(name, description, function));

    /// <summary>
    /// Registers a strongly-typed delegate function as a tool.
    /// </summary>
    /// <typeparam name="T">The input type for the tool (must be JSON deserializable).</typeparam>
    /// <param name="name">The name of the tool.</param>
    /// <param name="description">A description of what the tool does.</param>
    /// <param name="function">The typed async function to execute.</param>
    public void Register<T>(string name, string description, Func<T, Task<string>> function)
        => Register(DelegateTool.FromJson(name, description, function));
}