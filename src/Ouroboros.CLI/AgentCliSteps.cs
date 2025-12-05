// <copyright file="AgentCliSteps.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace LangChainPipeline.CLI;

/// <summary>
/// Autonomous agent CLI steps - behaves like GitHub Copilot.
/// The agent can plan, use tools, execute actions, and iterate until task completion.
/// </summary>
public static class AgentCliSteps
{
    /// <summary>
    /// Autonomous agent that plans and executes multi-step tasks.
    /// Usage: AutoAgent('Fix the bug in UserService.cs')
    /// Usage: AutoAgent('Add logging to all controllers;maxIter=10')
    /// </summary>
    [PipelineToken("AutoAgent", "Agent", "CopilotAgent")]
    public static Step<CliPipelineState, CliPipelineState> AutoAgent(string? args = null)
        => async s =>
        {
            var config = ParseAgentConfig(args);
            string task = config.Task ?? s.Query;

            if (string.IsNullOrWhiteSpace(task))
            {
                Console.WriteLine("[agent] No task provided");
                return s;
            }

            Console.WriteLine($"\n{'=',-60}");
            Console.WriteLine($"[AutoAgent] Task: {task}");
            Console.WriteLine($"[AutoAgent] Max iterations: {config.MaxIterations}");
            Console.WriteLine($"{'=',-60}\n");

            // Build agent tools
            var agentTools = BuildAgentTools(s);
            var toolDescriptions = BuildToolDescriptions(agentTools);

            var conversationHistory = new List<AgentMessage>();
            var executedActions = new List<string>();
            bool taskComplete = false;

            for (int iteration = 1; iteration <= config.MaxIterations && !taskComplete; iteration++)
            {
                Console.WriteLine($"\n[AutoAgent] === Iteration {iteration}/{config.MaxIterations} ===");

                // Build the agent prompt
                var agentPrompt = BuildAgentPrompt(task, toolDescriptions, conversationHistory, executedActions);

                if (s.Trace) Console.WriteLine($"[AutoAgent] Prompt length: {agentPrompt.Length} chars");

                // Get agent's next action
                string agentResponse;
                try
                {
                    Console.WriteLine("[AutoAgent] Calling LLM...");
                    agentResponse = await s.Llm.InnerModel.GenerateTextAsync(agentPrompt);
                    Console.WriteLine($"[AutoAgent] LLM responded: {agentResponse.Length} chars");
                    if (s.Trace) Console.WriteLine($"[AutoAgent] Response: {TruncateForDisplay(agentResponse, 300)}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AutoAgent] LLM error: {ex.Message}");
                    if (s.Trace) Console.WriteLine($"[AutoAgent] Stack: {ex.StackTrace}");
                    break;
                }

                conversationHistory.Add(new AgentMessage("assistant", agentResponse));

                // Parse the agent's response
                var action = ParseAgentAction(agentResponse);

                if (action.Type == AgentActionType.Complete)
                {
                    Console.WriteLine($"\n[AutoAgent] ✓ Task completed!");
                    Console.WriteLine($"[AutoAgent] Summary: {action.Summary}");
                    taskComplete = true;
                    s.Output = action.Summary ?? "Task completed successfully.";
                }
                else if (action.Type == AgentActionType.Think)
                {
                    Console.WriteLine($"[AutoAgent] Thinking: {action.Thought}");
                }
                else if (action.Type == AgentActionType.UseTool)
                {
                    Console.WriteLine($"[AutoAgent] Using tool: {action.ToolName}");
                    if (s.Trace) Console.WriteLine($"[AutoAgent] Args: {action.ToolArgs}");

                    // Execute the tool
                    var toolResult = await ExecuteToolAsync(agentTools, action.ToolName!, action.ToolArgs ?? string.Empty, s);
                    
                    executedActions.Add($"[{action.ToolName}] {TruncateForDisplay(action.ToolArgs, 50)} -> {TruncateForDisplay(toolResult, 100)}");
                    
                    // Add tool result to conversation
                    conversationHistory.Add(new AgentMessage("tool", $"[{action.ToolName}]: {toolResult}"));
                    
                    Console.WriteLine($"[AutoAgent] Result: {TruncateForDisplay(toolResult, 200)}");
                }
                else
                {
                    Console.WriteLine($"[AutoAgent] Unknown action, asking for clarification...");
                    conversationHistory.Add(new AgentMessage("system", "Please use one of the available tools or mark the task as complete."));
                }
            }

            if (!taskComplete)
            {
                Console.WriteLine($"\n[AutoAgent] Max iterations reached ({config.MaxIterations})");
                s.Output = $"Task incomplete after {config.MaxIterations} iterations. Actions taken: {executedActions.Count}";
            }

            // Build final summary
            s.Context = string.Join("\n", executedActions);
            s.Query = task;

            return s;
        };

    #region Agent Tools

    private static Dictionary<string, Func<string, CliPipelineState, Task<string>>> BuildAgentTools(CliPipelineState state)
    {
        return new Dictionary<string, Func<string, CliPipelineState, Task<string>>>(StringComparer.OrdinalIgnoreCase)
        {
            ["read_file"] = async (args, s) =>
            {
                var path = ParseToolArg(args, "path") ?? args.Trim().Trim('"', '\'');
                if (!File.Exists(path))
                    return $"Error: File not found: {path}";
                
                try
                {
                    var content = await File.ReadAllTextAsync(path);
                    if (content.Length > 10000)
                        content = content[..10000] + $"\n\n... [truncated, {content.Length} total chars]";
                    return content;
                }
                catch (Exception ex)
                {
                    return $"Error reading file: {ex.Message}";
                }
            },

            ["write_file"] = async (args, s) =>
            {
                var path = ParseToolArg(args, "path");
                var content = ParseToolArg(args, "content");
                
                if (string.IsNullOrEmpty(path) || content == null)
                    return "Error: Required args: path, content";
                
                try
                {
                    var dir = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                        Directory.CreateDirectory(dir);
                    
                    await File.WriteAllTextAsync(path, content);
                    return $"Successfully wrote {content.Length} chars to {path}";
                }
                catch (Exception ex)
                {
                    return $"Error writing file: {ex.Message}";
                }
            },

            ["edit_file"] = async (args, s) =>
            {
                var path = ParseToolArg(args, "path");
                var oldText = ParseToolArg(args, "old");
                var newText = ParseToolArg(args, "new");
                
                if (string.IsNullOrEmpty(path) || oldText == null || newText == null)
                    return "Error: Required args: path, old, new";
                
                if (!File.Exists(path))
                    return $"Error: File not found: {path}";
                
                try
                {
                    var content = await File.ReadAllTextAsync(path);
                    if (!content.Contains(oldText))
                        return $"Error: Old text not found in file. Make sure to include enough context.";
                    
                    var newContent = content.Replace(oldText, newText);
                    await File.WriteAllTextAsync(path, newContent);
                    return $"Successfully edited {path}";
                }
                catch (Exception ex)
                {
                    return $"Error editing file: {ex.Message}";
                }
            },

            ["list_dir"] = async (args, s) =>
            {
                var path = ParseToolArg(args, "path") ?? args.Trim().Trim('"', '\'');
                if (string.IsNullOrEmpty(path)) path = ".";
                
                if (!Directory.Exists(path))
                    return $"Error: Directory not found: {path}";
                
                try
                {
                    var entries = new List<string>();
                    foreach (var dir in Directory.GetDirectories(path).Take(50))
                        entries.Add(Path.GetFileName(dir) + "/");
                    foreach (var file in Directory.GetFiles(path).Take(100))
                        entries.Add(Path.GetFileName(file));
                    
                    return string.Join("\n", entries);
                }
                catch (Exception ex)
                {
                    return $"Error listing directory: {ex.Message}";
                }
            },

            ["search_files"] = async (args, s) =>
            {
                var query = ParseToolArg(args, "query") ?? args.Trim().Trim('"', '\'');
                var path = ParseToolArg(args, "path") ?? ".";
                var pattern = ParseToolArg(args, "pattern") ?? "*.cs";
                
                if (string.IsNullOrEmpty(query))
                    return "Error: Required arg: query";
                
                try
                {
                    var results = new List<string>();
                    foreach (var file in Directory.GetFiles(path, pattern, SearchOption.AllDirectories).Take(100))
                    {
                        var content = await File.ReadAllTextAsync(file);
                        if (content.Contains(query, StringComparison.OrdinalIgnoreCase))
                        {
                            var lines = content.Split('\n');
                            for (int i = 0; i < lines.Length; i++)
                            {
                                if (lines[i].Contains(query, StringComparison.OrdinalIgnoreCase))
                                {
                                    results.Add($"{file}:{i + 1}: {lines[i].Trim()}");
                                    if (results.Count >= 20) break;
                                }
                            }
                        }

                        if (results.Count >= 20) break;
                    }
                    
                    return results.Count > 0 
                        ? string.Join("\n", results) 
                        : "No matches found";
                }
                catch (Exception ex)
                {
                    return $"Error searching: {ex.Message}";
                }
            },

            ["run_command"] = async (args, s) =>
            {
                var command = ParseToolArg(args, "command") ?? args.Trim().Trim('"', '\'');
                
                if (string.IsNullOrEmpty(command))
                    return "Error: Required arg: command";
                
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -Command \"{command.Replace("\"", "\\\"")}\"",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    
                    using var process = Process.Start(psi);
                    if (process == null) return "Error: Failed to start process";
                    
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();
                    
                    var result = new StringBuilder();
                    if (!string.IsNullOrEmpty(output)) result.AppendLine(output);
                    if (!string.IsNullOrEmpty(error)) result.AppendLine($"STDERR: {error}");
                    result.AppendLine($"Exit code: {process.ExitCode}");
                    
                    var text = result.ToString();
                    if (text.Length > 5000) text = text[..5000] + "\n... [truncated]";
                    return text;
                }
                catch (Exception ex)
                {
                    return $"Error running command: {ex.Message}";
                }
            },

            ["vector_search"] = async (args, s) =>
            {
                var query = ParseToolArg(args, "query") ?? args.Trim().Trim('"', '\'');
                
                if (string.IsNullOrEmpty(query))
                    return "Error: Required arg: query";
                
                if (s.VectorStore == null && s.Branch.Store == null)
                    return "Error: No vector store available. Use UseQdrant first.";
                
                var store = s.VectorStore ?? s.Branch.Store;
                
                try
                {
                    var embedding = await s.Embed.CreateEmbeddingsAsync(query);
                    var results = await store.GetSimilarDocumentsAsync(embedding, 5);
                    
                    if (results.Count == 0) return "No similar documents found";
                    
                    var sb = new StringBuilder();
                    int i = 0;
                    foreach (var doc in results)
                    {
                        i++;
                        sb.AppendLine($"[{i}] {TruncateForDisplay(doc.PageContent, 500)}");
                        sb.AppendLine("---");
                    }

                    return sb.ToString();
                }
                catch (Exception ex)
                {
                    return $"Error searching vectors: {ex.Message}";
                }
            },

            ["think"] = (args, s) =>
            {
                // No-op, just for agent to organize thoughts
                return Task.FromResult($"Thought recorded: {args}");
            },

            ["ask_user"] = (args, s) =>
            {
                // In a real implementation, this would prompt the user
                Console.WriteLine($"\n[AutoAgent] Question for user: {args}");
                Console.Write("[AutoAgent] Your response (or press Enter to skip): ");
                var response = Console.ReadLine();
                return Task.FromResult(string.IsNullOrEmpty(response) ? "User skipped the question" : response);
            }
        };
    }

    private static string BuildToolDescriptions(Dictionary<string, Func<string, CliPipelineState, Task<string>>> tools)
    {
        return """
            ## Available Tools

            Use tools by responding with JSON in this format:
            {"tool": "tool_name", "args": {"arg1": "value1", ...}}

            ### read_file
            Read the contents of a file.
            Args: {"path": "path/to/file.cs"}

            ### write_file
            Create or overwrite a file with new content.
            Args: {"path": "path/to/file.cs", "content": "file contents here"}

            ### edit_file
            Replace specific text in a file. Include enough context to uniquely identify the location.
            Args: {"path": "path/to/file.cs", "old": "text to replace", "new": "replacement text"}

            ### list_dir
            List contents of a directory.
            Args: {"path": "path/to/directory"}

            ### search_files
            Search for text across files.
            Args: {"query": "search text", "path": ".", "pattern": "*.cs"}

            ### run_command
            Execute a PowerShell command.
            Args: {"command": "dotnet build"}

            ### vector_search
            Search the vector store for similar documents (requires UseQdrant).
            Args: {"query": "semantic search query"}

            ### think
            Record your thoughts/planning (no external action).
            Args: {"thought": "I need to first..."}

            ### ask_user
            Ask the user a clarifying question.
            Args: {"question": "What file should I modify?"}

            ## Completing the Task

            When the task is complete, respond with:
            {"complete": true, "summary": "Description of what was accomplished"}
            """;
    }

    #endregion

    #region Agent Prompt Building

    private static string BuildAgentPrompt(
        string task,
        string toolDescriptions,
        List<AgentMessage> history,
        List<string> executedActions)
    {
        var sb = new StringBuilder();

        sb.AppendLine("""
            You are an autonomous AI agent, similar to GitHub Copilot. You can read files, write code, 
            run commands, and complete programming tasks independently.

            ## Your Behavior
            1. ALWAYS respond with valid JSON (a tool call or completion)
            2. Think step-by-step about what needs to be done
            3. Use tools to gather information before making changes
            4. When editing files, include enough context to uniquely identify the location
            5. Verify your changes by reading files or running tests
            6. IMPORTANT: Once you have gathered enough information, mark the task as complete!

            ## Important Rules
            - Never guess file contents - always read first
            - Make small, incremental changes
            - If you encounter an error, analyze it and try a different approach
            - If stuck, ask the user for clarification
            - DO NOT read the same file multiple times - analyze what you already have
            - When you have the answer, complete immediately with a detailed summary
            """);

        sb.AppendLine(toolDescriptions);

        sb.AppendLine($"\n## Current Task\n{task}");

        if (executedActions.Count > 0)
        {
            sb.AppendLine("\n## Actions Taken So Far");
            foreach (var action in executedActions.TakeLast(10))
            {
                sb.AppendLine($"- {action}");
            }
        }

        if (history.Count > 0)
        {
            sb.AppendLine("\n## Conversation History");
            foreach (var msg in history.TakeLast(10))
            {
                // Tool results need more context, assistant messages can be shorter
                var maxLen = msg.Role == "tool" ? 2000 : 500;
                sb.AppendLine($"[{msg.Role}]: {TruncateForDisplay(msg.Content, maxLen)}");
            }
        }

        sb.AppendLine("\n## Your Next Action (respond with JSON only):");

        return sb.ToString();
    }

    #endregion

    #region Action Parsing

    private static AgentAction ParseAgentAction(string response)
    {
        try
        {
            // Try to extract JSON from response
            var jsonStart = response.IndexOf('{');
            var jsonEnd = response.LastIndexOf('}');

            if (jsonStart < 0 || jsonEnd <= jsonStart)
            {
                return new AgentAction { Type = AgentActionType.Think, Thought = response };
            }

            var json = response[jsonStart..(jsonEnd + 1)];
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Check for completion
            if (root.TryGetProperty("complete", out var complete) && complete.GetBoolean())
            {
                return new AgentAction
                {
                    Type = AgentActionType.Complete,
                    Summary = root.TryGetProperty("summary", out var summary) ? summary.GetString() : "Task completed"
                };
            }

            // Check for tool use
            if (root.TryGetProperty("tool", out var tool))
            {
                var toolName = tool.GetString();
                string? toolArgs = null;

                if (root.TryGetProperty("args", out var args))
                {
                    toolArgs = args.ValueKind == JsonValueKind.String
                        ? args.GetString()
                        : args.GetRawText();
                }

                return new AgentAction
                {
                    Type = AgentActionType.UseTool,
                    ToolName = toolName,
                    ToolArgs = toolArgs
                };
            }

            // Check for think
            if (root.TryGetProperty("thought", out var thought))
            {
                return new AgentAction { Type = AgentActionType.Think, Thought = thought.GetString() };
            }

            return new AgentAction { Type = AgentActionType.Unknown };
        }
        catch
        {
            // If JSON parsing fails, treat as a thought
            return new AgentAction { Type = AgentActionType.Think, Thought = response };
        }
    }

    private static async Task<string> ExecuteToolAsync(
        Dictionary<string, Func<string, CliPipelineState, Task<string>>> tools,
        string toolName,
        string args,
        CliPipelineState state)
    {
        if (!tools.TryGetValue(toolName, out var tool))
        {
            return $"Error: Unknown tool '{toolName}'. Available tools: {string.Join(", ", tools.Keys)}";
        }

        try
        {
            return await tool(args, state);
        }
        catch (Exception ex)
        {
            return $"Error executing {toolName}: {ex.Message}";
        }
    }

    #endregion

    #region Helper Methods

    private static string? ParseToolArg(string json, string argName)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty(argName, out var prop))
            {
                return prop.ValueKind == JsonValueKind.String
                    ? prop.GetString()
                    : prop.GetRawText();
            }
        }
        catch
        {
            // Not valid JSON, return null
        }

        return null;
    }

    private static string TruncateForDisplay(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        text = text.Replace("\r\n", " ").Replace("\n", " ");
        return text.Length <= maxLength ? text : text[..maxLength] + "...";
    }

    private static AutoAgentConfig ParseAgentConfig(string? args)
    {
        var config = new AutoAgentConfig();

        if (string.IsNullOrWhiteSpace(args)) return config;

        // Remove quotes if present
        if (args.StartsWith("'") && args.EndsWith("'")) args = args[1..^1];
        if (args.StartsWith("\"") && args.EndsWith("\"")) args = args[1..^1];

        // Parse semicolon-separated args
        foreach (var part in args.Split(';'))
        {
            var trimmed = part.Trim();
            if (trimmed.StartsWith("maxIter=", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(trimmed[8..], out var max))
                    config.MaxIterations = max;
            }
            else if (!trimmed.Contains('='))
            {
                config.Task = trimmed;
            }
        }

        return config;
    }

    #endregion

    #region Types

    private sealed class AutoAgentConfig
    {
        public string? Task { get; set; }
        public int MaxIterations { get; set; } = 15;
    }

    private sealed class AgentMessage
    {
        public string Role { get; }
        public string Content { get; }

        public AgentMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }
    }

    private enum AgentActionType
    {
        Unknown,
        Think,
        UseTool,
        Complete
    }

    private sealed class AgentAction
    {
        public AgentActionType Type { get; set; }
        public string? ToolName { get; set; }
        public string? ToolArgs { get; set; }
        public string? Thought { get; set; }
        public string? Summary { get; set; }
    }

    #endregion
}
