using System.Text;
using System.Text.RegularExpressions;

namespace LangChainPipeline.Tools;

/// <summary>
/// A sophisticated parser for tool invocation syntax that handles various argument formats.
/// Follows functional programming principles with Result monad for error handling.
/// </summary>
public static class ToolCallParser
{
    /// <summary>
    /// Represents a parsed tool call with name and arguments.
    /// </summary>
    public sealed record ToolCall(string Name, string Arguments);

    /// <summary>
    /// Parses tool calls from text, extracting [TOOL:name args] patterns.
    /// Supports various argument formats including JSON, expressions, and plain text.
    /// </summary>
    /// <param name="text">The text containing tool calls</param>
    /// <returns>A list of successfully parsed tool calls</returns>
    public static List<ToolCall> ParseToolCalls(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new List<ToolCall>();

        var toolCalls = new List<ToolCall>();
        var lines = text.Split('\n');

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();
            if (!line.StartsWith("[TOOL:", StringComparison.Ordinal) || !line.EndsWith(']'))
                continue;

            var parseResult = ParseSingleToolCall(line);
            parseResult.Match(
                success => toolCalls.Add(success),
                error => { /* Skip invalid tool calls silently to maintain compatibility */ }
            );
        }

        return toolCalls;
    }

    /// <summary>
    /// Parses a single tool call line with advanced argument parsing.
    /// </summary>
    /// <param name="line">A single line containing [TOOL:name args]</param>
    /// <returns>Result with parsed ToolCall or error message</returns>
    public static Result<ToolCall, string> ParseSingleToolCall(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return Result<ToolCall, string>.Failure("Tool call line cannot be empty");

        if (!line.StartsWith("[TOOL:", StringComparison.Ordinal) || !line.EndsWith(']'))
            return Result<ToolCall, string>.Failure($"Invalid tool call format: {line}");

        try
        {
            // Extract content between [TOOL: and ]
            string content = line[6..^1].Trim(); // Remove "[TOOL:" and "]"
            
            var (name, args) = ExtractNameAndArguments(content);
            
            if (string.IsNullOrWhiteSpace(name))
                return Result<ToolCall, string>.Failure("Tool name cannot be empty");

            return Result<ToolCall, string>.Success(new ToolCall(name, args));
        }
        catch (Exception ex)
        {
            return Result<ToolCall, string>.Failure($"Tool call parsing failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Extracts tool name and arguments from the content inside [TOOL: ... ].
    /// Handles various argument formats intelligently.
    /// </summary>
    /// <param name="content">Content after "TOOL:" and before closing "]"</param>
    /// <returns>Tuple of (name, arguments)</returns>
    private static (string Name, string Arguments) ExtractNameAndArguments(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return (string.Empty, string.Empty);

        // Find the first space that's not inside brackets, braces, or quotes
        int nameEndIndex = FindNameEndIndex(content);
        
        if (nameEndIndex == -1)
        {
            // No arguments, just the tool name
            return (content.Trim(), string.Empty);
        }

        string name = content[..nameEndIndex].Trim();
        string arguments = content[(nameEndIndex + 1)..].Trim();

        return (name, arguments);
    }

    /// <summary>
    /// Finds where the tool name ends by looking for the first space
    /// that's not inside JSON objects, arrays, or quoted strings.
    /// </summary>
    /// <param name="content">The content to analyze</param>
    /// <returns>Index where tool name ends, or -1 if no arguments</returns>
    private static int FindNameEndIndex(string content)
    {
        int braceDepth = 0;
        int bracketDepth = 0;
        int parenDepth = 0;
        bool inQuotes = false;
        bool escaped = false;

        for (int i = 0; i < content.Length; i++)
        {
            char c = content[i];

            if (escaped)
            {
                escaped = false;
                continue;
            }

            switch (c)
            {
                case '\\' when inQuotes:
                    escaped = true;
                    break;
                case '"':
                    inQuotes = !inQuotes;
                    break;
                case '{' when !inQuotes:
                    braceDepth++;
                    break;
                case '}' when !inQuotes:
                    braceDepth--;
                    break;
                case '[' when !inQuotes:
                    bracketDepth++;
                    break;
                case ']' when !inQuotes:
                    bracketDepth--;
                    break;
                case '(' when !inQuotes:
                    parenDepth++;
                    break;
                case ')' when !inQuotes:
                    parenDepth--;
                    break;
                case ' ' when !inQuotes && braceDepth == 0 && bracketDepth == 0 && parenDepth == 0:
                    return i;
            }
        }

        return -1; // No space found outside of nested structures
    }

    /// <summary>
    /// Validates that a JSON string is properly formatted.
    /// Used for tools that expect JSON arguments.
    /// </summary>
    /// <param name="json">The JSON string to validate</param>
    /// <returns>Result indicating success or failure with error message</returns>
    public static Result<string, string> ValidateJsonArguments(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Result<string, string>.Success(string.Empty);

        try
        {
            System.Text.Json.JsonDocument.Parse(json);
            return Result<string, string>.Success(json);
        }
        catch (System.Text.Json.JsonException ex)
        {
            return Result<string, string>.Failure($"Invalid JSON arguments: {ex.Message}");
        }
    }

    /// <summary>
    /// Determines if the arguments look like JSON (start with { or [).
    /// </summary>
    /// <param name="arguments">The argument string to check</param>
    /// <returns>True if arguments appear to be JSON</returns>
    public static bool IsJsonArguments(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
            return false;

        string trimmed = arguments.Trim();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }

    /// <summary>
    /// Determines if the arguments look like a mathematical expression.
    /// </summary>
    /// <param name="arguments">The argument string to check</param>
    /// <returns>True if arguments appear to be a math expression</returns>
    public static bool IsMathExpression(string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
            return false;

        // Simple heuristic: contains math operators and/or parentheses
        return Regex.IsMatch(arguments, @"[+\-*/()\d]");
    }
}