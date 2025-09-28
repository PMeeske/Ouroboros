using LangChain.Providers;
using LangChain.Providers.Ollama;
using LangChainPipeline.Tools;

namespace LangChainPipeline.Providers;

/// <summary>
/// A chat model wrapper that can execute tools based on special tool invocation syntax in responses.
/// Uses monadic Result<T,E> for consistent error handling throughout the pipeline.
/// </summary>
public sealed class ToolAwareChatModel(OllamaChatModel llm, ToolRegistry registry)
{
    /// <summary>
    /// Generates a response and executes any tools mentioned in the response.
    /// </summary>
    /// <param name="prompt">The input prompt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple containing the final text and list of tool executions.</returns>
    public async Task<(string Text, List<ToolExecution> Tools)> GenerateWithToolsAsync(string prompt, CancellationToken ct = default)
    {
        ChatResponse chatResponse = await llm.GenerateAsync(prompt);
        List<ToolExecution> toolCalls = new List<ToolExecution>();
        string result = chatResponse.LastMessageContent;
        
        // Use the sophisticated DSL parser for tool calls
        var parsedToolCalls = ToolCallParser.ParseToolCalls(result);
        
        foreach (var toolCall in parsedToolCalls)
        {
            ITool? tool = registry.Get(toolCall.Name);
            if (tool is null)
            {
                result += $"\n[TOOL-RESULT:{toolCall.Name}] error: tool not found";
                continue;
            }

            string output;
            try 
            { 
                var toolResult = await tool.InvokeAsync(toolCall.Arguments, ct);
                output = toolResult.Match(
                    success => success,
                    error => $"error: {error}"
                );
            }
            catch (Exception ex) 
            { 
                output = $"error: {ex.Message}"; 
            }

            toolCalls.Add(new ToolExecution(toolCall.Name, toolCall.Arguments, output, DateTime.UtcNow));
            result += $"\n[TOOL-RESULT:{toolCall.Name}] {output}";
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
