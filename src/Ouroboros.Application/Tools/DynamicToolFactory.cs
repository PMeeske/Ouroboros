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
using Ouroboros.Application.Mcp;
using Ouroboros.Application.Tools.CaptchaResolver;
using Ouroboros.Tools;

/// <summary>
/// Factory for dynamically generating, compiling, and registering tools at runtime.
/// Uses Roslyn for compilation and LLM for code generation.
/// </summary>
public class DynamicToolFactory
{
    private readonly ToolAwareChatModel _llm;
    private readonly PlaywrightMcpTool? _playwrightMcpTool;
    private readonly CaptchaResolverChain _captchaResolver;
    private readonly AssemblyLoadContext _loadContext;
    private readonly List<MetadataReference> _references;
    private readonly string _storagePath;
    private int _toolCounter;

    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicToolFactory"/> class.
    /// </summary>
    /// <param name="llm">The LLM for generating tool code.</param>
    /// <param name="storagePath">Optional path to store generated tools.</param>
    /// <param name="playwrightMcpTool">Optional Playwright tool for browser automation.</param>
    public DynamicToolFactory(ToolAwareChatModel llm, string? storagePath = null, PlaywrightMcpTool? playwrightMcpTool = null)
    {
        _llm = llm ?? throw new ArgumentNullException(nameof(llm));
        _playwrightMcpTool = playwrightMcpTool;
        _loadContext = new AssemblyLoadContext("DynamicTools", isCollectible: true);
        _storagePath = storagePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".ouroboros",
            "dynamic_tools");

        Directory.CreateDirectory(_storagePath);

        // Build reference assemblies for compilation
        _references = BuildReferences();

        // Initialize CAPTCHA resolver chain with available strategies
        // Use semantic decorator to enhance detection and provide intelligent guidance
        var visionResolver = new VisionCaptchaResolver(_playwrightMcpTool);
        var alternativeResolver = new AlternativeSearchResolver();

        _captchaResolver = new CaptchaResolverChain()
            .AddStrategy(new SemanticCaptchaResolverDecorator(visionResolver, _llm))
            .AddStrategy(new SemanticCaptchaResolverDecorator(alternativeResolver, _llm, useSemanticDetection: false))
            .AddStrategy(visionResolver)  // Fallback without semantic analysis
            .AddStrategy(alternativeResolver);
    }

    /// <summary>
    /// Fixes malformed URLs that LLMs sometimes generate (e.g., "https: example.com path" → "https://example.com/path").
    /// </summary>
    private static string FixMalformedUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return url;

        url = url.Trim();

        // Fix "https: " → "https://" and "http: " → "http://"
        url = Regex.Replace(url, @"^(https?): +", "$1://");

        // If URL still doesn't have proper scheme, check for common patterns
        if (!url.StartsWith("http://") && !url.StartsWith("https://"))
        {
            // Check if it looks like a domain (e.g., "www.example.com" or "example.com")
            if (Regex.IsMatch(url, @"^[\w\-]+(\.[\w\-]+)+"))
            {
                url = "https://" + url;
            }
        }

        // Try to parse and fix path components with spaces
        if (Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed))
        {
            // URL is valid, return as-is
            return url;
        }

        // If parsing failed, try to fix spaces in path
        // Pattern: "https://domain path1 path2" → "https://domain/path1/path2"
        var match = Regex.Match(url, @"^(https?://[\w\.\-]+)([\s/].*)$");
        if (match.Success)
        {
            string domain = match.Groups[1].Value;
            string path = match.Groups[2].Value.Trim();

            // Replace spaces with / in path, normalize multiple slashes
            path = Regex.Replace(path, @"\s+", "/");
            path = Regex.Replace(path, @"/+", "/");

            if (!path.StartsWith("/"))
                path = "/" + path;

            url = domain + path;
        }

        return url;
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

    // Random instance for human-like timing simulation
    private static readonly Random _humanRng = new();

    /// <summary>
    /// Simulates human-like delay to avoid bot detection.
    /// </summary>
    private static async Task SimulateHumanDelayAsync(int minMs = 500, int maxMs = 2000)
    {
        int delay = _humanRng.Next(minMs, maxMs);
        await Task.Delay(delay);
    }

    /// <summary>
    /// Gets a randomized User-Agent string to simulate different browsers.
    /// </summary>
    private static string GetRandomUserAgent()
    {
        var userAgents = new[]
        {
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/119.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:121.0) Gecko/20100101 Firefox/121.0",
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.2 Safari/605.1.15",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36 Edg/120.0.0.0"
        };
        return userAgents[_humanRng.Next(userAgents.Length)];
    }

    /// <summary>
    /// Configures HttpClient with realistic browser-like headers.
    /// </summary>
    private static void ConfigureHumanLikeHeaders(HttpClient http)
    {
        http.DefaultRequestHeaders.Clear();
        http.DefaultRequestHeaders.Add("User-Agent", GetRandomUserAgent());
        http.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
        http.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.9,de;q=0.8");
        http.DefaultRequestHeaders.Add("Accept-Encoding", "gzip, deflate, br");
        http.DefaultRequestHeaders.Add("DNT", "1");
        http.DefaultRequestHeaders.Add("Connection", "keep-alive");
        http.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
        http.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
        http.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
        http.DefaultRequestHeaders.Add("Sec-Fetch-Site", "none");
        http.DefaultRequestHeaders.Add("Sec-Fetch-User", "?1");
        http.DefaultRequestHeaders.Add("Cache-Control", "max-age=0");
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

                // Configure realistic browser-like headers
                ConfigureHumanLikeHeaders(http);
                http.Timeout = TimeSpan.FromSeconds(20);

                // Initial human-like delay before first request (simulates typing/thinking)
                await SimulateHumanDelayAsync(300, 800);

                // Try multiple search endpoints for DuckDuckGo
                var searchUrls = searchProvider.ToLowerInvariant() switch
                {
                    "google" => new[] { $"https://www.google.com/search?q={Uri.EscapeDataString(query)}" },
                    "bing" => new[] { $"https://www.bing.com/search?q={Uri.EscapeDataString(query)}" },
                    _ => new[]
                    {
                        $"https://lite.duckduckgo.com/lite/?q={Uri.EscapeDataString(query)}",  // Lite version (less strict)
                        $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}", // HTML version
                        $"https://duckduckgo.com/?q={Uri.EscapeDataString(query)}&t=h_&ia=web" // Main site
                    }
                };

                foreach (var searchUrl in searchUrls)
                {
                    try
                    {
                        // Add referer for subsequent requests (simulates clicking through)
                        if (searchUrl != searchUrls[0])
                        {
                            http.DefaultRequestHeaders.Remove("Referer");
                            http.DefaultRequestHeaders.Add("Referer", searchUrls[0]);

                            // Human-like delay between retry attempts
                            await SimulateHumanDelayAsync(1000, 3000);
                        }

                        var response = await http.GetAsync(searchUrl);

                        if (!response.IsSuccessStatusCode)
                        {
                            // Try next URL if this one fails
                            continue;
                        }

                        string html = await response.Content.ReadAsStringAsync();

                        // Check for CAPTCHA before extracting results
                        var captchaCheck = _captchaResolver.DetectCaptcha(html, searchUrl);
                        if (captchaCheck.IsCaptcha)
                        {
                            // CAPTCHA detected - try to resolve it
                            var resolution = await _captchaResolver.ResolveAsync(searchUrl, html);
                            if (resolution.Success && !string.IsNullOrWhiteSpace(resolution.ResolvedContent))
                            {
                                return resolution.ResolvedContent;
                            }

                            // Resolution failed - continue to next URL
                            continue;
                        }

                        // Extract text snippets from results
                        var results = ExtractSearchResults(html, searchProvider);
                        if (results.Count > 0)
                        {
                            return string.Join("\n\n", results.Take(5));
                        }
                    }
                    catch (HttpRequestException)
                    {
                        // Try next URL
                        continue;
                    }
                    catch (TaskCanceledException)
                    {
                        // Timeout, try next
                        continue;
                    }
                }

                // All primary URLs failed - use CAPTCHA resolver's alternative search strategy
                var fallbackUrl = $"https://duckduckgo.com/?q={Uri.EscapeDataString(query)}";
                var alternativeResult = await _captchaResolver.ResolveAsync(fallbackUrl, "Primary search failed");
                if (alternativeResult.Success && !string.IsNullOrWhiteSpace(alternativeResult.ResolvedContent))
                {
                    return alternativeResult.ResolvedContent;
                }

                // If CAPTCHA resolver also failed, try browser automation as last resort
                if (_playwrightMcpTool != null)
                {
                    try
                    {
                        // First, navigate to the page. This ensures the page is loaded before we screenshot.
                        var searchUrl = $"https://duckduckgo.com/?q={Uri.EscapeDataString(query)}&t=h_&ia=web";
                        var navArgs = new Dictionary<string, object>
                        {
                            { "action", "navigate" },
                            { "url", searchUrl }
                        };
                        var navJson = JsonSerializer.Serialize(navArgs);
                        await _playwrightMcpTool.InvokeAsync(navJson, CancellationToken.None); // We don't need the result, just the action

                        // Now, take a screenshot. We'll get the analysis in the next step.
                        var screenshotArgs = new Dictionary<string, object>
                        {
                            { "action", "screenshot" }
                        };
                        var screenshotJson = JsonSerializer.Serialize(screenshotArgs);
                        await _playwrightMcpTool.InvokeAsync(screenshotJson, CancellationToken.None);

                        // Get the clean vision analysis using the new internal method.
                        var visionResult = await _playwrightMcpTool.GetVisionAnalysisForLastScreenshotAsync(CancellationToken.None);

                        if (visionResult.IsSuccess)
                        {
                            var analysis = visionResult.Match(
                                success => success,
                                failure => string.Empty);

                            if (!string.IsNullOrWhiteSpace(analysis))
                            {
                                return $"Visually extracted results:\n{analysis}";
                            }
                        }
                        else
                        {
                            var error = visionResult.Match(
                                success => string.Empty,
                                failure => failure);
                            return $"Search failed. Vision analysis returned an error: {error}";
                        }
                    }
                    catch (Exception ex)
                    {
                        // Playwright tool also failed, fall through to the generic error.
                        return $"Search failed after multiple retries. Vision-based search failed with: {ex.Message}";
                    }
                }

                return "Search failed: All search providers returned errors or blocked the request. Try using the Playwright browser tool to search manually.";
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
                // Simulate human typing/thinking delay
                await SimulateHumanDelayAsync(200, 600);

                using var http = new HttpClient();
                ConfigureHumanLikeHeaders(http);
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
            async (input) =>
            {
                string url = input?.Trim() ?? string.Empty;

                // Handle JSON input from orchestrator (e.g., {"url":"...","__sandboxed__":true})
                if (url.StartsWith("{") || url.StartsWith("'"))
                {
                    try
                    {
                        // Try to parse as JSON and extract 'url' field
                        string normalized = url.Replace("'", "\""); // Handle single quotes
                        using var doc = System.Text.Json.JsonDocument.Parse(normalized);
                        if (doc.RootElement.TryGetProperty("url", out var urlProp))
                        {
                            url = urlProp.GetString() ?? string.Empty;
                        }
                    }
                    catch
                    {
                        // Not valid JSON, continue with original input
                    }
                }

                // Validate URL is not empty
                if (string.IsNullOrWhiteSpace(url))
                {
                    return "Fetch failed: URL is required";
                }

                // Fix malformed URLs from LLM (e.g., "https: example.com path" → "https://example.com/path")
                url = FixMalformedUrl(url);

                // Detect placeholder descriptions that LLMs sometimes generate instead of actual URLs
                string lower = url.ToLowerInvariant().Trim();
                if (lower.StartsWith("url of") ||
                    lower.StartsWith("the ") ||
                    lower.Contains(" of the ") ||
                    lower.Contains("from step") ||
                    lower.Contains("e.g.,") ||
                    lower.Contains("placeholder") ||
                    lower.Contains("result from"))
                {
                    return $"Fetch failed: The URL appears to be a placeholder description, not an actual URL. Got: '{url}'. Please provide a real URL like 'https://example.com'.";
                }

                // Validate URL is absolute
                if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? parsedUri))
                {
                    return $"Fetch failed: Invalid URL format. URL must be absolute (e.g., https://example.com). Got: {url}";
                }

                // Only allow http/https schemes
                if (parsedUri.Scheme != "http" && parsedUri.Scheme != "https")
                {
                    return $"Fetch failed: Only http and https URLs are supported. Got: {parsedUri.Scheme}";
                }

                // Simulate human-like delay before fetch
                await SimulateHumanDelayAsync(200, 500);

                using HttpClient http = new HttpClient();
                ConfigureHumanLikeHeaders(http);
                http.Timeout = TimeSpan.FromSeconds(30);

                try
                {
                    string content = await http.GetStringAsync(parsedUri);

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

        // Try multiple patterns for each provider (they change HTML frequently)
        var patterns = provider.ToLowerInvariant() switch
        {
            "google" => new[]
            {
                @"<span[^>]*>([^<]{50,})</span>",
                @"<div[^>]*class=""[^""]*BNeawe[^""]*""[^>]*>([^<]{30,})</div>",
                @"<div[^>]*data-sncf=""[^""]*""[^>]*>([^<]{30,})</div>"
            },
            "bing" => new[]
            {
                @"<p[^>]*>([^<]{50,})</p>",
                @"<li[^>]*class=""b_algo""[^>]*>.*?<p>([^<]{30,})</p>",
                @"<span[^>]*class=""algoSlug_icon""[^>]*>([^<]{30,})</span>"
            },
            "brave" => new[]
            {
                @"<p[^>]*class=""[^""]*snippet[^""]*""[^>]*>([^<]{30,})</p>",
                @"<div[^>]*class=""[^""]*snippet[^""]*""[^>]*>([^<]{30,})</div>",
                @"<span[^>]*class=""[^""]*description[^""]*""[^>]*>([^<]{30,})</span>",
                @"data-testid=""result-item""[^>]*>.*?<p[^>]*>([^<]{30,})</p>"
            },
            _ => new[] // DuckDuckGo (including lite version) - try multiple selectors
            {
                @"<a[^>]*class=""result__snippet""[^>]*>([^<]+)</a>",
                @"<td[^>]*class=""result__snippet""[^>]*>([^<]+)</td>",
                @"class=""result__snippet""[^>]*>([^<]{20,})<",
                @"<a[^>]*class=""[^""]*result[^""]*""[^>]*>([^<]{30,})</a>",
                @"<div[^>]*class=""[^""]*snippet[^""]*""[^>]*>([^<]{30,})</div>",
                @"<span[^>]*class=""[^""]*snippet[^""]*""[^>]*>([^<]{30,})</span>",
                // Lite version patterns
                @"<td>([^<]{40,})</td>",
                @"<tr[^>]*>.*?<td[^>]*>([^<]{30,})</td>"
            }
        };

        foreach (var pattern in patterns)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(html, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            foreach (System.Text.RegularExpressions.Match m in matches)
            {
                // Try all capture groups
                for (int i = 1; i <= m.Groups.Count - 1; i++)
                {
                    if (m.Groups[i].Success)
                    {
                        string text = System.Net.WebUtility.HtmlDecode(m.Groups[i].Value).Trim();
                        // Filter out noise
                        if (text.Length > 25 &&
                            !results.Contains(text) &&
                            !text.StartsWith("http") &&
                            !text.Contains("javascript:") &&
                            !text.All(c => char.IsDigit(c) || char.IsWhiteSpace(c)))
                        {
                            results.Add(text);
                        }
                    }
                }
            }

            // If we found results with this pattern, don't need to try more
            if (results.Count >= 3) break;
        }

        // Fallback: extract any reasonably sized text blocks if no results found
        if (results.Count == 0)
        {
            var fallbackPattern = @">([^<]{40,200})<";
            var fallbackMatches = System.Text.RegularExpressions.Regex.Matches(html, fallbackPattern);
            foreach (System.Text.RegularExpressions.Match m in fallbackMatches)
            {
                string text = System.Net.WebUtility.HtmlDecode(m.Groups[1].Value).Trim();
                if (text.Length > 40 &&
                    !results.Contains(text) &&
                    !text.StartsWith("http") &&
                    !text.Contains("{") &&  // Skip JSON/CSS
                    !text.Contains("function") &&  // Skip JS
                    !text.Contains("var ") && // Skip JS vars
                    !text.Contains("window.") && // Skip JS window objects
                    !text.Contains(";") && // Skip code-like lines
                    results.Count < 10)
                {
                    // Check for high density of symbols (likely code or garbage)
                    int symbols = text.Count(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c) && c != '.' && c != ',');
                    if (symbols < text.Length * 0.2) // Less than 20% symbols
                    {
                        results.Add(text);
                    }
                }
            }
        }

        return results;
    }
}
