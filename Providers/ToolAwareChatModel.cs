using LangChainPipeline.Tools;

namespace LangChainPipeline.Providers;

/// <summary>
/// A chat model wrapper that can execute tools based on special tool invocation syntax in responses.
/// Uses monadic Result<T,E> for consistent error handling throughout the pipeline.
/// </summary>
public sealed class ToolAwareChatModel(IChatCompletionModel llm, ToolRegistry registry)
{
    /// <summary>
    /// Generates a response and executes any tools mentioned in the response.
    /// </summary>
    /// <param name="prompt">The input prompt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple containing the final text and list of tool executions.</returns>
    public async Task<(string Text, List<ToolExecution> Tools)> GenerateWithToolsAsync(string prompt, CancellationToken ct = default)
    {
    string result = await llm.GenerateTextAsync(prompt, ct);
    List<ToolExecution> toolCalls = new List<ToolExecution>();
        
        foreach (string rawLine in result.Split('\n'))
        {
            string line = rawLine.Trim();
            if (!line.StartsWith("[TOOL:", StringComparison.Ordinal)) 
                continue;

            // Parse tool invocation: [TOOL:name args]
            string inside = line.Trim('[', ']')[5..].Trim(); // Remove "[TOOL:" prefix
            string[] split = inside.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            string name = split[0];
            string args = split.Length > 1 ? split[1] : string.Empty;

            ITool? tool = registry.Get(name);
            if (tool is null)
            {
                result += $"\n[TOOL-RESULT:{name}] error: tool not found";
                continue;
            }

            string output;
            try 
            { 
                var toolResult = await tool.InvokeAsync(args, ct);
                output = toolResult.Match(
                    success => success,
                    error => $"error: {error}"
                );
            }
            catch (Exception ex) 
            { 
                output = $"error: {ex.Message}"; 
            }

            toolCalls.Add(new ToolExecution(name, args, output, DateTime.UtcNow));
            result += $"\n[TOOL-RESULT:{name}] {output}";
        }

        return (result, toolCalls);
    }

    /// <summary>
    /// Monadic version that returns Result<T,E> for better error handling.
    /// </summary>
    /// <param name="prompt">The input prompt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the response and tool executions, or an error.</returns>
    public async Task<Result<(string Text, List<ToolExecution> Tools), string>> GenerateWithToolsResultAsync(string prompt, CancellationToken ct = default)
    {
        try
        {
            var (text, tools) = await GenerateWithToolsAsync(prompt, ct);
            return Result<(string, List<ToolExecution>), string>.Success((text, tools));
        }
        catch (Exception ex)
        {
            return Result<(string, List<ToolExecution>), string>.Failure($"Tool-aware generation failed: {ex.Message}");
        }
    }
}
