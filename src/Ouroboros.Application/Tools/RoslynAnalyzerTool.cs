// <copyright file="RoslynAnalyzerTool.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

using System.Text.Json;
using Ouroboros.Core.Monads;
using Ouroboros.Tools;
using CodeGenResult = Ouroboros.Application.CodeGeneration.CodeAnalysisResult;

/// <summary>
/// Provides Roslyn-based code analysis, generation, and refactoring tools for LLM agents.
/// </summary>
public static class RoslynAnalyzerTools
{
    private static readonly Ouroboros.Application.CodeGeneration.RoslynCodeTool _codeTool = new();

    /// <summary>
    /// Creates all Roslyn analyzer tools.
    /// </summary>
    public static IEnumerable<ITool> CreateAllTools()
    {
        yield return new AnalyzeCodeTool();
        yield return new CreateClassTool();
        yield return new AddMethodTool();
        yield return new RenameSymbolTool();
        yield return new ExtractMethodTool();
        yield return new GetCodeStructureTool();
        yield return new FormatCodeTool();
    }

    /// <summary>
    /// Tool for analyzing C# code using Roslyn.
    /// </summary>
    public class AnalyzeCodeTool : ITool
    {
        public string Name => "analyze_csharp_code";
        public string Description => "Analyze C# code for errors, structure, and quality. Input: JSON {\"code\":\"...\", \"runAnalyzers\":true}";
        public string? JsonSchema => """
        {
            "type": "object",
            "properties": {
                "code": { "type": "string", "description": "C# code to analyze" },
                "runAnalyzers": { "type": "boolean", "description": "Run custom analyzers (default: true)" }
            },
            "required": ["code"]
        }
        """;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                JsonElement args = JsonSerializer.Deserialize<JsonElement>(input);
                string code = args.GetProperty("code").GetString() ?? "";
                bool runAnalyzers = args.TryGetProperty("runAnalyzers", out JsonElement ra) && ra.GetBoolean();

                Result<CodeGenResult, string> result = await _codeTool.AnalyzeCodeAsync(code, runAnalyzers: runAnalyzers);

                return result.Match(
                    success =>
                    {
                        var response = new
                        {
                            isValid = success.IsValid,
                            diagnostics = success.Diagnostics,
                            classes = success.Classes,
                            methods = success.Methods,
                            usings = success.Usings,
                            analyzerResults = success.AnalyzerResults
                        };
                        return Result<string, string>.Success(JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }));
                    },
                    error => Result<string, string>.Failure(error)
                );
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to analyze code: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Tool for creating new C# classes using Roslyn.
    /// </summary>
    public class CreateClassTool : ITool
    {
        public string Name => "create_csharp_class";
        public string Description => "Generate a new C# class. Input: JSON {\"className\":\"...\", \"namespaceName\":\"...\", \"methods\":[...], \"properties\":[...], \"baseClass\":\"...\", \"interfaces\":[...]}";
        public string? JsonSchema => """
        {
            "type": "object",
            "properties": {
                "className": { "type": "string", "description": "Name of the class" },
                "namespaceName": { "type": "string", "description": "Namespace for the class" },
                "methods": { "type": "array", "items": { "type": "string" }, "description": "Method signatures" },
                "properties": { "type": "array", "items": { "type": "string" }, "description": "Property definitions" },
                "baseClass": { "type": "string", "description": "Base class to inherit from" },
                "interfaces": { "type": "array", "items": { "type": "string" }, "description": "Interfaces to implement" },
                "usings": { "type": "array", "items": { "type": "string" }, "description": "Using directives" }
            },
            "required": ["className", "namespaceName"]
        }
        """;

        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                JsonElement args = JsonSerializer.Deserialize<JsonElement>(input);
                string className = args.GetProperty("className").GetString() ?? "";
                string namespaceName = args.GetProperty("namespaceName").GetString() ?? "";

                List<string>? methods = args.TryGetProperty("methods", out JsonElement m)
                    ? m.EnumerateArray().Select(e => e.GetString() ?? "").ToList()
                    : null;

                List<string>? properties = args.TryGetProperty("properties", out JsonElement p)
                    ? p.EnumerateArray().Select(e => e.GetString() ?? "").ToList()
                    : null;

                string? baseClass = args.TryGetProperty("baseClass", out JsonElement bc)
                    ? bc.GetString()
                    : null;

                List<string>? interfaces = args.TryGetProperty("interfaces", out JsonElement i)
                    ? i.EnumerateArray().Select(e => e.GetString() ?? "").ToList()
                    : null;

                List<string>? usings = args.TryGetProperty("usings", out JsonElement u)
                    ? u.EnumerateArray().Select(e => e.GetString() ?? "").ToList()
                    : null;

                Result<string, string> result = _codeTool.CreateClass(
                    className, namespaceName, methods, properties, usings, baseClass, interfaces);

                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                return Task.FromResult(Result<string, string>.Failure($"Failed to create class: {ex.Message}"));
            }
        }
    }

    /// <summary>
    /// Tool for adding methods to existing classes.
    /// </summary>
    public class AddMethodTool : ITool
    {
        public string Name => "add_method_to_class";
        public string Description => "Add a method to an existing C# class. Input: JSON {\"code\":\"...\", \"className\":\"...\", \"methodSignature\":\"...\", \"methodBody\":\"...\"}";
        public string? JsonSchema => """
        {
            "type": "object",
            "properties": {
                "code": { "type": "string", "description": "Existing C# code" },
                "className": { "type": "string", "description": "Name of the class to modify" },
                "methodSignature": { "type": "string", "description": "Method signature (e.g., 'public async Task DoSomething()')" },
                "methodBody": { "type": "string", "description": "Method body (without braces)" }
            },
            "required": ["code", "className", "methodSignature"]
        }
        """;

        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                JsonElement args = JsonSerializer.Deserialize<JsonElement>(input);
                string code = args.GetProperty("code").GetString() ?? "";
                string className = args.GetProperty("className").GetString() ?? "";
                string methodSignature = args.GetProperty("methodSignature").GetString() ?? "";
                string? methodBody = args.TryGetProperty("methodBody", out JsonElement mb)
                    ? mb.GetString()
                    : null;

                Result<string, string> result = _codeTool.AddMethodToClass(code, className, methodSignature, methodBody);
                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                return Task.FromResult(Result<string, string>.Failure($"Failed to add method: {ex.Message}"));
            }
        }
    }

    /// <summary>
    /// Tool for renaming symbols throughout code.
    /// </summary>
    public class RenameSymbolTool : ITool
    {
        public string Name => "rename_csharp_symbol";
        public string Description => "Rename a symbol (class, method, variable) throughout C# code. Input: JSON {\"code\":\"...\", \"oldName\":\"...\", \"newName\":\"...\"}";
        public string? JsonSchema => """
        {
            "type": "object",
            "properties": {
                "code": { "type": "string", "description": "C# code containing the symbol" },
                "oldName": { "type": "string", "description": "Current name of the symbol" },
                "newName": { "type": "string", "description": "New name for the symbol" }
            },
            "required": ["code", "oldName", "newName"]
        }
        """;

        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                JsonElement args = JsonSerializer.Deserialize<JsonElement>(input);
                string code = args.GetProperty("code").GetString() ?? "";
                string oldName = args.GetProperty("oldName").GetString() ?? "";
                string newName = args.GetProperty("newName").GetString() ?? "";

                Result<string, string> result = _codeTool.RenameSymbol(code, oldName, newName);
                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                return Task.FromResult(Result<string, string>.Failure($"Failed to rename symbol: {ex.Message}"));
            }
        }
    }

    /// <summary>
    /// Tool for extracting code into new methods.
    /// </summary>
    public class ExtractMethodTool : ITool
    {
        public string Name => "extract_method";
        public string Description => "Extract code lines into a new method. Input: JSON {\"code\":\"...\", \"startLine\":1, \"endLine\":10, \"newMethodName\":\"...\"}";
        public string? JsonSchema => """
        {
            "type": "object",
            "properties": {
                "code": { "type": "string", "description": "C# code to refactor" },
                "startLine": { "type": "integer", "description": "Start line (1-based)" },
                "endLine": { "type": "integer", "description": "End line (1-based)" },
                "newMethodName": { "type": "string", "description": "Name for the extracted method" }
            },
            "required": ["code", "startLine", "endLine", "newMethodName"]
        }
        """;

        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                JsonElement args = JsonSerializer.Deserialize<JsonElement>(input);
                string code = args.GetProperty("code").GetString() ?? "";
                int startLine = args.GetProperty("startLine").GetInt32();
                int endLine = args.GetProperty("endLine").GetInt32();
                string newMethodName = args.GetProperty("newMethodName").GetString() ?? "";

                Result<string, string> result = _codeTool.ExtractMethod(code, startLine, endLine, newMethodName);
                return Task.FromResult(result);
            }
            catch (Exception ex)
            {
                return Task.FromResult(Result<string, string>.Failure($"Failed to extract method: {ex.Message}"));
            }
        }
    }

    /// <summary>
    /// Tool for getting code structure information.
    /// </summary>
    public class GetCodeStructureTool : ITool
    {
        public string Name => "get_code_structure";
        public string Description => "Get structural overview of C# code (classes, methods, properties). Input: JSON {\"code\":\"...\"}";
        public string? JsonSchema => """
        {
            "type": "object",
            "properties": {
                "code": { "type": "string", "description": "C# code to analyze" }
            },
            "required": ["code"]
        }
        """;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                JsonElement args = JsonSerializer.Deserialize<JsonElement>(input);
                string code = args.GetProperty("code").GetString() ?? "";

                Result<CodeGenResult, string> result = await _codeTool.AnalyzeCodeAsync(code, runAnalyzers: false);

                return result.Match(
                    success =>
                    {
                        var structure = new
                        {
                            classes = success.Classes,
                            methods = success.Methods,
                            usings = success.Usings,
                            isValid = success.IsValid,
                            errorCount = success.Diagnostics.Count(d => d.Contains("Error:"))
                        };
                        return Result<string, string>.Success(JsonSerializer.Serialize(structure, new JsonSerializerOptions { WriteIndented = true }));
                    },
                    error => Result<string, string>.Failure(error)
                );
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to get code structure: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Tool for formatting C# code.
    /// </summary>
    public class FormatCodeTool : ITool
    {
        public string Name => "format_csharp_code";
        public string Description => "Format C# code using Roslyn formatter. Input: JSON {\"code\":\"...\"}";
        public string? JsonSchema => """
        {
            "type": "object",
            "properties": {
                "code": { "type": "string", "description": "C# code to format" }
            },
            "required": ["code"]
        }
        """;

        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                JsonElement args = JsonSerializer.Deserialize<JsonElement>(input);
                string code = args.GetProperty("code").GetString() ?? "";

                // Use Roslyn's Formatter directly
                Microsoft.CodeAnalysis.SyntaxTree tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText(code);
                Microsoft.CodeAnalysis.SyntaxNode root = tree.GetRoot();

                using var workspace = new Microsoft.CodeAnalysis.AdhocWorkspace();
                Microsoft.CodeAnalysis.SyntaxNode formatted = Microsoft.CodeAnalysis.Formatting.Formatter.Format(root, workspace);

                return Task.FromResult(Result<string, string>.Success(formatted.ToFullString()));
            }
            catch (Exception ex)
            {
                return Task.FromResult(Result<string, string>.Failure($"Failed to format code: {ex.Message}"));
            }
        }
    }
}
