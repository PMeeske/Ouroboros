using System.Collections.Generic;
using System.Threading.Tasks;

namespace Ouroboros.Tools
{
    /// <summary>
    /// Information about a tool.
    /// </summary>
    public class ToolInfo
    {
        public string Name { get; }
        public string Description { get; }
        public object InputSchema { get; }

        public ToolInfo(string name, string description, object inputSchema)
        {
            Name = name;
            Description = description;
            InputSchema = inputSchema;
        }
    }

    /// <summary>
    /// Result of tool execution.
    /// </summary>
    public class ToolExecutionResult
    {
        public bool Success { get; }
        public string Result { get; }

        public ToolExecutionResult(bool success, string result)
        {
            Success = success;
            Result = result;
        }
    }

    /// <summary>
    /// Mock MCP server for testing.
    /// </summary>
    public class MockMcpServer
    {
        public List<ToolInfo> ListTools() => new List<ToolInfo>
        {
            new ToolInfo("dsl_suggestion", "Suggest next DSL steps", new { }),
            new ToolInfo("code_analysis", "Analyze C# code", new { })
        };

        public async Task<ToolExecutionResult> ExecuteTool(string toolName, object parameters)
        {
            await Task.Delay(10);
            return new ToolExecutionResult(true, $"Executed {toolName} with result");
        }
    }
}
