// <copyright file="DynamicToolFactory.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LangChainPipeline.Providers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Ouroboros.Tools;

/// <summary>
/// Factory for dynamically generating, compiling, and registering tools at runtime.
/// Uses Roslyn for compilation and LLM for code generation.
/// </summary>
public class DynamicToolFactory
{
    private readonly ToolAwareChatModel _llm;
    private readonly AssemblyLoadContext _loadContext;
    private readonly List<MetadataReference> _references;
    private readonly string _storagePath;
    private int _toolCounter;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicToolFactory"/> class.
    /// </summary>
    /// <param name="llm">The LLM for generating tool code.</param>
    /// <param name="storagePath">Optional path to store generated tools.</param>
    public DynamicToolFactory(ToolAwareChatModel llm, string? storagePath = null)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _loadContext = new AssemblyLoadContext("DynamicTools", isCollectible: true);
        _storagePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".ouroboros",
            "dynamic_tools");

        Directory.CreateDirectory(_storagePath);

        // Build reference assemblies for compilation
        _references = BuildReferences();
    }

    /// <summary>
    /// Gets the list of dynamically created tools.
    /// </summary>
    public List<(string Name, string Description, ITool Tool)> CreatedTools { get; } = new();

    /// <summary>
    /// Generates a new tool based on a natural language description.
    /// </summary>
    /// <param name="toolName">The desired name for the tool (e.g., "google_search").</param>
    /// <param name="description">Natural language description of what the tool should do.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result containing the created tool or an error.</returns>
    public async Task<Result<ITool, string>> CreateToolAsync(
        string toolName,
        string description,
        CancellationToken ct = default)
    {
        try
        {
            // Sanitize tool name
            string safeName = SanitizeToolName(toolName);
            string className = $"{ToPascalCase(safeName)}Tool";

            // Generate tool code using LLM
            string codeResult = await GenerateToolCodeAsync(className, safeName, description, ct);
            if (string.IsNullOrWhiteSpace(codeResult))
            {
                return Result<ITool, string>.Failure("LLM failed to generate tool code");
            }

            // Extract code from markdown if present
            string code = ExtractCode(codeResult);

            // Ensure required using statements are present
            code = EnsureRequiredUsings(code);

            // Compile the tool
            var compileResult = CompileTool(code, className);
            if (!compileResult.IsSuccess)
            {
                // Try to fix compilation errors with LLM
                string fixPrompt = $@"The following C# code has compilation errors. Fix them and return ONLY the corrected code.
IMPORTANT: Make sure to include 'using LangChainPipeline.Core.Monads;' for the Result type.

ERRORS:
{compileResult.Error}

CODE:
```csharp
{code}
```";
                string fixedCode = await _llm.InnerModel.GenerateTextAsync(fixPrompt, ct);
                fixedCode = ExtractCode(fixedCode);
                fixedCode = EnsureRequiredUsings(fixedCode); // Ensure usings are present
                compileResult = CompileTool(fixedCode, className);

                if (!compileResult.IsSuccess)
                {
                    return Result<ITool, string>.Failure($"Compilation failed: {compileResult.Error}");
                }

                code = fixedCode;
            }

            // Save the generated code for inspection
            string codePath = Path.Combine(_storagePath, $"{className}.cs");
            await File.WriteAllTextAsync(codePath, code, ct);

            // Create instance of the tool
            if (compileResult.Value is not ITool tool)
            {
                return Result<ITool, string>.Failure("Compiled type does not implement ITool");
            }

            CreatedTools.Add((safeName, description, tool));
            _toolCounter++;

            return Result<ITool, string>.Success(tool);
        }
        catch (Exception ex)
        {
            return Result<ITool, string>.Failure($"Tool creation failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a simple delegate-based tool without full code generation.
    /// Faster but less flexible than full code generation.
    /// </summary>
    /// <param name="toolName">The name for the tool.</param>
    /// <param name="description">Description of what the tool does.</param>
    /// <param name="implementation">The implementation function.</param>
    /// <returns>The created tool.</returns>
    public ITool CreateSimpleTool(
        string toolName,
        string description,
        Func<string, Task<string>> implementation)
    {
        var tool = new DelegateTool(toolName, description, implementation);
        CreatedTools.Add((toolName, description, tool));
        return tool;
    }

    /// <summary>
    /// Creates a web search tool that searches the internet.
    /// </summary>
    /// <param name="searchProvider">The search provider (google, bing, duckduckgo).</param>
    /// <returns>A web search tool.</returns>
    public ITool CreateWebSearchTool(string searchProvider = "duckduckgo")
    {
        var tool = CreateSimpleTool(
            $"{searchProvider}_search",
            $"Search the web using {searchProvider} and return results",
            async (query) =>
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

                string searchUrl = searchProvider.ToLowerInvariant() switch
                {
                    "google" => $"https://www.google.com/search?q={Uri.EscapeDataString(query)}",
                    "bing" => $"https://www.bing.com/search?q={Uri.EscapeDataString(query)}",
                    _ => $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}"
                };

                try
                {
                    string html = await http.GetStringAsync(searchUrl);

                    // Extract text snippets from results
                    var results = ExtractSearchResults(html, searchProvider);
                    return results.Count > 0
                        ? string.Join("\n\n", results.Take(5))
                        : "No results found";
                }
                catch (Exception ex)
                {
                    return $"Search failed: {ex.Message}";
                }
            });

        return tool;
    }

    /// <summary>
    /// Creates a Google Search tool that uses SerpAPI (if key available) or DuckDuckGo fallback.
    /// </summary>
    /// <returns>A Google search tool.</returns>
    public ITool CreateGoogleSearchTool()
    {
        return CreateSimpleTool(
            "google_search",
            "Search the web using Google and return results with titles, URLs, and snippets",
            async (query) =>
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                http.Timeout = TimeSpan.FromSeconds(30);

                string? serpApiKey = Environment.GetEnvironmentVariable("SERPAPI_KEY");
                var results = new List<string>();

                try
                {
                    if (!string.IsNullOrEmpty(serpApiKey))
                    {
                        // Use SerpAPI for reliable Google results
                        string url = $"https://serpapi.com/search.json?q={Uri.EscapeDataString(query)}&api_key={serpApiKey}&num=10";
                        string json = await http.GetStringAsync(url);
                        using var doc = JsonDocument.Parse(json);

                        if (doc.RootElement.TryGetProperty("organic_results", out var organicEl))
                        {
                            foreach (var result in organicEl.EnumerateArray().Take(10))
                            {
                                string title = result.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                                string link = result.TryGetProperty("link", out var l) ? l.GetString() ?? "" : "";
                                string snippet = result.TryGetProperty("snippet", out var s) ? s.GetString() ?? "" : "";
                                results.Add($"🔍 {title}\n   URL: {link}\n   {snippet}");
                            }
                        }
                    }
                    else
                    {
                        // Fallback to DuckDuckGo HTML
                        string url = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}";
                        string html = await http.GetStringAsync(url);
                        results = ExtractSearchResults(html, "duckduckgo");
                    }

                    return results.Count > 0
                        ? string.Join("\n\n", results.Take(8))
                        : "No search results found.";
                }
                catch (Exception ex)
                {
                    return $"Google search failed: {ex.Message}";
                }
            });
    }

    /// <summary>
    /// Creates a URL fetcher tool.
    /// </summary>
    /// <returns>A URL fetcher tool.</returns>
    public ITool CreateUrlFetchTool()
    {
        return CreateSimpleTool(
            "fetch_url",
            "Fetch content from a URL and return the text",
            async (url) =>
            {
                using var http = new HttpClient();
                http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0");
                http.Timeout = TimeSpan.FromSeconds(30);

                try
                {
                    string content = await http.GetStringAsync(url);

                    // Basic HTML to text conversion
                    content = System.Text.RegularExpressions.Regex.Replace(content, @"<script[^>]*>[\s\S]*?</script>", "");
                    content = System.Text.RegularExpressions.Regex.Replace(content, @"<style[^>]*>[\s\S]*?</style>", "");
                    content = System.Text.RegularExpressions.Regex.Replace(content, @"<[^>]+>", " ");
                    content = System.Text.RegularExpressions.Regex.Replace(content, @"\s+", " ");
                    content = System.Net.WebUtility.HtmlDecode(content);

                    // Truncate if too long
                    return content.Length > 5000 ? content[..5000] + "..." : content;
                }
                catch (Exception ex)
                {
                    return $"Fetch failed: {ex.Message}";
                }
            });
    }

    /// <summary>
    /// Creates a calculator tool.
    /// </summary>
    /// <returns>A calculator tool.</returns>
    public ITool CreateCalculatorTool()
    {
        return CreateSimpleTool(
            "calculator",
            "Evaluate mathematical expressions",
            (expression) =>
            {
                try
                {
                    // Simple expression evaluator using DataTable
                    var dt = new System.Data.DataTable();
                    var result = dt.Compute(expression, null);
                    return Task.FromResult(result?.ToString() ?? "undefined");
                }
                catch (Exception ex)
                {
                    return Task.FromResult($"Calculation error: {ex.Message}");
                }
            });
    }

    private async Task<string> GenerateToolCodeAsync(
        string className,
        string toolName,
        string description,
        CancellationToken ct)
    {
        string prompt = $@"Generate a C# class that implements the ITool interface for the following tool:

Tool Name: {toolName}
Description: {description}

Requirements:
1. Class name: {className}
2. Implement the ITool interface with these members:
   - string Name {{ get; }} => ""{toolName}""
   - string Description {{ get; }} => ""{description}""
   - string? JsonSchema {{ get; }} => null or a valid JSON schema for args
   - Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct)

3. Use HttpClient for any web requests
4. Handle errors gracefully and return Result<string, string>.Failure() on error
5. Return Result<string, string>.Success() with the result string on success

The Result type is: Result<TSuccess, TError> with static methods:
- Result<string, string>.Success(string value)
- Result<string, string>.Failure(string error)

IMPORTANT: You MUST include these exact using statements at the top:

```csharp
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LangChainPipeline.Core.Monads;
using Ouroboros.Tools;

namespace Ouroboros.DynamicTools
{{
    public class {className} : ITool
    {{
        public string Name => ""{toolName}"";
        public string Description => ""{description}"";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {{
            try
            {{
                // Your implementation here
                return Result<string, string>.Success(""Result"");
            }}
            catch (Exception ex)
            {{
                return Result<string, string>.Failure(ex.Message);
            }}
        }}
    }}
}}
```

Generate ONLY the complete C# code, no explanations.";

        return await _llm.InnerModel.GenerateTextAsync(prompt, ct);
    }

    private Result<ITool?, string> CompileTool(string code, string className)
    {
        try
        {
            // Parse the code
            var syntaxTree = CSharpSyntaxTree.ParseText(code);

            // Create compilation
            var assemblyName = $"DynamicTool_{_toolCounter}_{Guid.NewGuid():N}";
            var compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { syntaxTree },
                _references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithOptimizationLevel(OptimizationLevel.Release)
                    .WithPlatform(Platform.AnyCpu));

            // Emit to memory stream
            using var ms = new MemoryStream();
            var emitResult = compilation.Emit(ms);

            if (!emitResult.Success)
            {
                var errors = emitResult.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .Select(d => d.GetMessage())
                    .ToList();
                return Result<ITool?, string>.Failure(string.Join("\n", errors));
            }

            // Load assembly and create instance
            ms.Seek(0, SeekOrigin.Begin);
            var assembly = _loadContext.LoadFromStream(ms);

            // Find the tool type
            var toolType = assembly.GetTypes()
                .FirstOrDefault(t => typeof(ITool).IsAssignableFrom(t) && !t.IsAbstract);

            if (toolType == null)
            {
                return Result<ITool?, string>.Failure($"No ITool implementation found in compiled code");
            }

            var tool = (ITool?)Activator.CreateInstance(toolType);
            return tool != null
                ? Result<ITool?, string>.Success(tool)
                : Result<ITool?, string>.Failure("Failed to create tool instance");
        }
        catch (Exception ex)
        {
            return Result<ITool?, string>.Failure($"Compilation error: {ex.Message}");
        }
    }

    private List<MetadataReference> BuildReferences()
    {
        var refs = new List<MetadataReference>();

        // Get runtime assemblies
        var assemblies = new[]
        {
            typeof(object).Assembly,                           // System.Private.CoreLib
            typeof(Console).Assembly,                          // System.Console
            typeof(HttpClient).Assembly,                       // System.Net.Http
            typeof(Uri).Assembly,                              // System.Private.Uri
            typeof(Task).Assembly,                             // System.Threading.Tasks
            typeof(Enumerable).Assembly,                       // System.Linq
            typeof(JsonSerializer).Assembly,                   // System.Text.Json
            typeof(ITool).Assembly,                            // Ouroboros.Tools
            typeof(Result<,>).Assembly,                        // Ouroboros.Core (Result type)
            Assembly.Load("System.Runtime"),
            Assembly.Load("System.Collections"),
            Assembly.Load("netstandard"),
        };

        foreach (var asm in assemblies)
        {
            if (!string.IsNullOrEmpty(asm.Location))
            {
                refs.Add(MetadataReference.CreateFromFile(asm.Location));
            }
        }

        // Add additional runtime references
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var additionalRefs = new[]
        {
            "System.Threading.dll",
            "System.Threading.Tasks.dll",
            "System.Net.Primitives.dll",
            "System.Runtime.Extensions.dll",
            "System.ComponentModel.Primitives.dll",
        };

        foreach (var refName in additionalRefs)
        {
            var refPath = Path.Combine(runtimeDir, refName);
            if (File.Exists(refPath))
            {
                refs.Add(MetadataReference.CreateFromFile(refPath));
            }
        }

        return refs;
    }

    private static string SanitizeToolName(string name)
    {
        // Convert to snake_case and remove invalid chars
        var sb = new StringBuilder();
        foreach (char c in name.ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c))
                sb.Append(c);
            else if (c == ' ' || c == '-' || c == '_')
                sb.Append('_');
        }
        return sb.ToString().Trim('_');
    }

    private static string ToPascalCase(string snakeCase)
    {
        return string.Join("", snakeCase.Split('_')
            .Where(s => s.Length > 0)
            .Select(s => char.ToUpperInvariant(s[0]) + s[1..]));
    }

    private static string ExtractCode(string response)
    {
        // Extract code from markdown code blocks if present
        var match = System.Text.RegularExpressions.Regex.Match(
            response,
            @"```(?:csharp|cs)?\s*([\s\S]*?)```",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        return match.Success ? match.Groups[1].Value.Trim() : response.Trim();
    }

    /// <summary>
    /// Ensures that required using statements are present in the generated code.
    /// </summary>
    private static string EnsureRequiredUsings(string code)
    {
        var requiredUsings = new[]
        {
            "using System;",
            "using System.Threading;",
            "using System.Threading.Tasks;",
            "using LangChainPipeline.Core.Monads;",
            "using Ouroboros.Tools;",
        };

        var sb = new StringBuilder();
        var existingUsings = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Extract existing using statements
        var lines = code.Split('\n');
        var usingLines = new List<string>();
        var codeStart = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            var trimmed = lines[i].Trim();
            if (trimmed.StartsWith("using ") && trimmed.EndsWith(";"))
            {
                existingUsings.Add(trimmed);
                usingLines.Add(lines[i]);
                codeStart = i + 1;
            }
            else if (!string.IsNullOrWhiteSpace(trimmed) && !trimmed.StartsWith("//"))
            {
                // Found non-using, non-empty line
                break;
            }
            else
            {
                codeStart = i + 1;
            }
        }

        // Add all existing usings
        foreach (var u in usingLines)
        {
            sb.AppendLine(u);
        }

        // Add missing required usings
        foreach (var required in requiredUsings)
        {
            if (!existingUsings.Contains(required))
            {
                sb.AppendLine(required);
            }
        }

        // Add the rest of the code
        if (usingLines.Count > 0)
        {
            sb.AppendLine();
        }

        for (int i = codeStart; i < lines.Length; i++)
        {
            sb.AppendLine(lines[i]);
        }

        return sb.ToString().TrimEnd();
    }

    private static List<string> ExtractSearchResults(string html, string provider)
    {
        var results = new List<string>();

        // Simple regex-based extraction (works for basic cases)
        var snippetPattern = provider.ToLowerInvariant() switch
        {
            "google" => @"<span[^>]*>([^<]{50,})</span>",
            "bing" => @"<p[^>]*>([^<]{50,})</p>",
            _ => @"<a[^>]*class=""result__snippet""[^>]*>([^<]+)</a>|<td[^>]*class=""result__snippet""[^>]*>([^<]+)</td>"
        };

        var matches = System.Text.RegularExpressions.Regex.Matches(html, snippetPattern);
        foreach (System.Text.RegularExpressions.Match m in matches)
        {
            string text = m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value;
            text = System.Net.WebUtility.HtmlDecode(text).Trim();
            if (text.Length > 30 && !results.Contains(text))
            {
                results.Add(text);
            }
        }

        return results;
    }
}
