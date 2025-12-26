// <copyright file="ToolAwareChatModel.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Providers;

/// <summary>
/// A chat model wrapper that can execute tools based on special tool invocation syntax in responses.
/// Uses monadic Result{T,E} for consistent error handling throughout the pipeline.
/// </summary>
/// <param name="llm">The underlying chat completion model.</param>
/// <param name="registry">The tool registry for tool execution.</param>
public sealed class ToolAwareChatModel(IChatCompletionModel llm, ToolRegistry registry)
{
    /// <summary>
    /// Gets the underlying chat completion model.
    /// </summary>
    public IChatCompletionModel InnerModel => llm;

    /// <summary>
    /// Generates a response and executes any tools mentioned in the response.
    /// </summary>
    /// <param name="prompt">The input prompt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A tuple containing the final text and list of tool executions.</returns>
    public async Task<(string Text, List<ToolExecution> Tools)> GenerateWithToolsAsync(string prompt, CancellationToken ct = default)
    {
        string result = await llm.GenerateTextAsync(prompt, ct);
        List<ToolExecution> toolCalls = [];
        string modifiedResult = result;

        // Use regex to find all tool invocations
        var toolPattern = new System.Text.RegularExpressions.Regex(@"\[TOOL:([^\s]+)\s*([^\]]*)\]");
        var matches = toolPattern.Matches(result);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            string fullMatch = match.Value;
            string name = match.Groups[1].Value;
            string args = match.Groups[2].Value.Trim();

            // Validate tool name is not empty or just whitespace
            if (string.IsNullOrWhiteSpace(name))
            {
                string errorResult = "[TOOL-RESULT:?] error: empty tool name";
                modifiedResult = modifiedResult.Replace(fullMatch, errorResult);
                continue;
            }

            ITool? tool = registry.Get(name);
            if (tool is null)
            {
                string errorResult = $"[TOOL-RESULT:{name}] error: tool not found";
                modifiedResult = modifiedResult.Replace(fullMatch, errorResult);
                continue;
            }

            string output;
            try
            {
                Result<string, string> toolResult = await tool.InvokeAsync(args, ct);
                output = toolResult.Match(
                    success => success,
                    error => $"error: {error}");
            }
            catch (Exception ex)
            {
                output = $"error: {ex.Message}";
            }

            toolCalls.Add(new ToolExecution(name, args, output, DateTime.UtcNow));

            // Replace the tool invocation with the result in the text
            string replacement = $"[TOOL-RESULT:{name}] {output}";
            modifiedResult = modifiedResult.Replace(fullMatch, replacement);
        }

        return (modifiedResult, toolCalls);
    }

    /// <summary>
    /// Monadic version that returns Result{T,E} for better error handling.
    /// </summary>
    /// <param name="prompt">The input prompt.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A Result containing the response and tool executions, or an error.</returns>
    public async Task<Result<(string Text, List<ToolExecution> Tools), string>> GenerateWithToolsResultAsync(string prompt, CancellationToken ct = default)
    {
        try
        {
            (string text, List<ToolExecution> tools) = await this.GenerateWithToolsAsync(prompt, ct);
            return Result<(string, List<ToolExecution>), string>.Success((text, tools));
        }
        catch (Exception ex)
        {
            return Result<(string, List<ToolExecution>), string>.Failure($"Tool-aware generation failed: {ex.Message}");
        }
    }
}
