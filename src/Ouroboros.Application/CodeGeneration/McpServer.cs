#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using System.Text.Json;
using System.Text.Json.Serialization;
using Ouroboros.Application.GitHub;

namespace Ouroboros.Application.CodeGeneration;

/// <summary>
/// Model Context Protocol (MCP) server for code generation and analysis.
/// Provides a standard protocol interface for AI assistants to interact with code tools.
/// </summary>
public class McpServer
{
    private readonly RoslynCodeTool _codeTool;
    private readonly DslAssistant _dslAssistant;
    private readonly IGitHubMcpClient? _githubClient;

    public McpServer(RoslynCodeTool codeTool, DslAssistant dslAssistant, IGitHubMcpClient? githubClient = null)
    {
        _codeTool = codeTool;
        _dslAssistant = dslAssistant;
        _githubClient = githubClient;
    }

    /// <summary>
    /// Lists available tools/capabilities.
    /// </summary>
    public McpResponse ListTools()
    {
        List<McpTool> tools = new List<McpTool>
        {
            new McpTool
            {
                Name = "analyze_code",
                Description = "Analyze C# code using Roslyn and custom analyzers",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        code = new { type = "string", description = "C# code to analyze" },
                        runAnalyzers = new { type = "boolean", description = "Run custom analyzers" }
                    },
                    required = new[] { "code" }
                }
            },
            new McpTool
            {
                Name = "create_class",
                Description = "Generate a new C# class with specified structure",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        className = new { type = "string" },
                        namespaceName = new { type = "string" },
                        methods = new { type = "array", items = new { type = "string" } },
                        properties = new { type = "array", items = new { type = "string" } },
                        baseClass = new { type = "string" },
                        interfaces = new { type = "array", items = new { type = "string" } }
                    },
                    required = new[] { "className", "namespaceName" }
                }
            },
            new McpTool
            {
                Name = "add_method",
                Description = "Add a method to an existing class",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        code = new { type = "string" },
                        className = new { type = "string" },
                        methodSignature = new { type = "string" },
                        methodBody = new { type = "string" }
                    },
                    required = new[] { "code", "className", "methodSignature" }
                }
            },
            new McpTool
            {
                Name = "rename_symbol",
                Description = "Rename a symbol (class, method, variable) throughout the code",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        code = new { type = "string" },
                        oldName = new { type = "string" },
                        newName = new { type = "string" }
                    },
                    required = new[] { "code", "oldName", "newName" }
                }
            },
            new McpTool
            {
                Name = "extract_method",
                Description = "Extract code into a new method (refactoring)",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        code = new { type = "string" },
                        startLine = new { type = "integer" },
                        endLine = new { type = "integer" },
                        newMethodName = new { type = "string" }
                    },
                    required = new[] { "code", "startLine", "endLine", "newMethodName" }
                }
            },
            new McpTool
            {
                Name = "suggest_dsl_step",
                Description = "Suggest next DSL pipeline step based on context",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        currentDsl = new { type = "string", description = "Current DSL pipeline" },
                        maxSuggestions = new { type = "integer", description = "Max suggestions to return" }
                    },
                    required = new[] { "currentDsl" }
                }
            },
            new McpTool
            {
                Name = "complete_token",
                Description = "Complete partial DSL token",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        partialToken = new { type = "string" },
                        maxCompletions = new { type = "integer" }
                    },
                    required = new[] { "partialToken" }
                }
            },
            new McpTool
            {
                Name = "validate_dsl",
                Description = "Validate DSL syntax and suggest fixes",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        dsl = new { type = "string", description = "DSL to validate" }
                    },
                    required = new[] { "dsl" }
                }
            },
            new McpTool
            {
                Name = "explain_dsl",
                Description = "Explain what a DSL pipeline does in natural language",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        dsl = new { type = "string" }
                    },
                    required = new[] { "dsl" }
                }
            },
            new McpTool
            {
                Name = "build_dsl",
                Description = "Build a DSL pipeline from high-level goal",
                InputSchema = new
                {
                    type = "object",
                    properties = new
                    {
                        goal = new { type = "string", description = "High-level goal for the pipeline" }
                    },
                    required = new[] { "goal" }
                }
            }
        };

        // Add GitHub tools if client is available
        if (_githubClient != null)
        {
            tools.AddRange(new[]
            {
                new McpTool
                {
                    Name = "github_create_pr",
                    Description = "Create a pull request for self-modification. Requires ethics approval.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            title = new { type = "string", description = "PR title" },
                            description = new { type = "string", description = "PR description" },
                            sourceBranch = new { type = "string", description = "Source branch" },
                            targetBranch = new { type = "string", description = "Target branch (default: main)" }
                        },
                        required = new[] { "title", "description", "sourceBranch" }
                    }
                },
                new McpTool
                {
                    Name = "github_push_changes",
                    Description = "Push code changes to a branch. Requires ethics approval.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            branchName = new { type = "string", description = "Branch name" },
                            changes = new { type = "array", description = "File changes to push" },
                            commitMessage = new { type = "string", description = "Commit message" }
                        },
                        required = new[] { "branchName", "changes", "commitMessage" }
                    }
                },
                new McpTool
                {
                    Name = "github_create_issue",
                    Description = "Create an issue for improvement proposal.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            title = new { type = "string", description = "Issue title" },
                            description = new { type = "string", description = "Issue description" },
                            labels = new { type = "array", items = new { type = "string" }, description = "Issue labels" }
                        },
                        required = new[] { "title", "description" }
                    }
                },
                new McpTool
                {
                    Name = "github_read_file",
                    Description = "Read a file from the repository.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            path = new { type = "string", description = "File path" },
                            branch = new { type = "string", description = "Branch name (optional)" }
                        },
                        required = new[] { "path" }
                    }
                },
                new McpTool
                {
                    Name = "github_list_files",
                    Description = "List files in a directory.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            path = new { type = "string", description = "Directory path (default: root)" },
                            branch = new { type = "string", description = "Branch name (optional)" }
                        },
                        required = Array.Empty<string>()
                    }
                },
                new McpTool
                {
                    Name = "github_create_branch",
                    Description = "Create a new branch for modifications. Requires ethics approval.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            branchName = new { type = "string", description = "New branch name" },
                            baseBranch = new { type = "string", description = "Base branch (default: main)" }
                        },
                        required = new[] { "branchName" }
                    }
                },
                new McpTool
                {
                    Name = "github_search_code",
                    Description = "Search code in the repository.",
                    InputSchema = new
                    {
                        type = "object",
                        properties = new
                        {
                            query = new { type = "string", description = "Search query" },
                            path = new { type = "string", description = "Path filter (optional)" }
                        },
                        required = new[] { "query" }
                    }
                }
            });
        }

        return new McpResponse
        {
            Tools = tools
        };
    }

    /// <summary>
    /// Executes a tool with given parameters.
    /// </summary>
    public async Task<McpToolResult> ExecuteToolAsync(string toolName, Dictionary<string, object> parameters)
    {
        try
        {
            return toolName switch
            {
                "analyze_code" => await ExecuteAnalyzeCodeAsync(parameters),
                "create_class" => ExecuteCreateClass(parameters),
                "add_method" => ExecuteAddMethod(parameters),
                "rename_symbol" => ExecuteRenameSymbol(parameters),
                "extract_method" => ExecuteExtractMethod(parameters),
                "suggest_dsl_step" => await ExecuteSuggestDslStepAsync(parameters),
                "complete_token" => ExecuteCompleteToken(parameters),
                "validate_dsl" => await ExecuteValidateDslAsync(parameters),
                "explain_dsl" => await ExecuteExplainDslAsync(parameters),
                "build_dsl" => await ExecuteBuildDslAsync(parameters),
                "github_create_pr" => await ExecuteGitHubCreatePrAsync(parameters),
                "github_push_changes" => await ExecuteGitHubPushChangesAsync(parameters),
                "github_create_issue" => await ExecuteGitHubCreateIssueAsync(parameters),
                "github_read_file" => await ExecuteGitHubReadFileAsync(parameters),
                "github_list_files" => await ExecuteGitHubListFilesAsync(parameters),
                "github_create_branch" => await ExecuteGitHubCreateBranchAsync(parameters),
                "github_search_code" => await ExecuteGitHubSearchCodeAsync(parameters),
                _ => new McpToolResult
                {
                    Success = false,
                    Error = $"Unknown tool: {toolName}"
                }
            };
        }
        catch (Exception ex)
        {
            return new McpToolResult
            {
                Success = false,
                Error = $"Tool execution failed: {ex.Message}"
            };
        }
    }

    // Tool execution methods

    private async Task<McpToolResult> ExecuteAnalyzeCodeAsync(Dictionary<string, object> parameters)
    {
        string code = parameters["code"].ToString() ?? string.Empty;
        bool runAnalyzers = parameters.ContainsKey("runAnalyzers") && Convert.ToBoolean(parameters["runAnalyzers"]);

        Result<CodeAnalysisResult, string> result = await _codeTool.AnalyzeCodeAsync(code, runAnalyzers: runAnalyzers);

        return result.Match(
            success => new McpToolResult
            {
                Success = true,
                Data = new
                {
                    isValid = success.IsValid,
                    diagnostics = success.Diagnostics,
                    classes = success.Classes,
                    methods = success.Methods,
                    usings = success.Usings,
                    analyzerResults = success.AnalyzerResults
                }
            },
            error => new McpToolResult { Success = false, Error = error });
    }

    private McpToolResult ExecuteCreateClass(Dictionary<string, object> parameters)
    {
        string className = parameters["className"].ToString() ?? string.Empty;
        string namespaceName = parameters["namespaceName"].ToString() ?? string.Empty;
        List<string>? methods = ExtractStringList(parameters, "methods");
        List<string>? properties = ExtractStringList(parameters, "properties");
        List<string>? interfaces = ExtractStringList(parameters, "interfaces");
        string? baseClass = parameters.ContainsKey("baseClass") ? parameters["baseClass"]?.ToString() : null;

        Result<string, string> result = _codeTool.CreateClass(
            className,
            namespaceName,
            methods,
            properties,
            baseClass: baseClass,
            interfaces: interfaces);

        return result.Match(
            success => new McpToolResult { Success = true, Data = new { code = success } },
            error => new McpToolResult { Success = false, Error = error });
    }

    private McpToolResult ExecuteAddMethod(Dictionary<string, object> parameters)
    {
        string code = parameters["code"].ToString() ?? string.Empty;
        string className = parameters["className"].ToString() ?? string.Empty;
        string methodSignature = parameters["methodSignature"].ToString() ?? string.Empty;
        string? methodBody = parameters.ContainsKey("methodBody") ? parameters["methodBody"]?.ToString() : null;

        Result<string, string> result = _codeTool.AddMethodToClass(code, className, methodSignature, methodBody);

        return result.Match(
            success => new McpToolResult { Success = true, Data = new { code = success } },
            error => new McpToolResult { Success = false, Error = error });
    }

    private McpToolResult ExecuteRenameSymbol(Dictionary<string, object> parameters)
    {
        string code = parameters["code"].ToString() ?? string.Empty;
        string oldName = parameters["oldName"].ToString() ?? string.Empty;
        string newName = parameters["newName"].ToString() ?? string.Empty;

        Result<string, string> result = _codeTool.RenameSymbol(code, oldName, newName);

        return result.Match(
            success => new McpToolResult { Success = true, Data = new { code = success } },
            error => new McpToolResult { Success = false, Error = error });
    }

    private McpToolResult ExecuteExtractMethod(Dictionary<string, object> parameters)
    {
        string code = parameters["code"].ToString() ?? string.Empty;
        int startLine = Convert.ToInt32(parameters["startLine"]);
        int endLine = Convert.ToInt32(parameters["endLine"]);
        string newMethodName = parameters["newMethodName"].ToString() ?? string.Empty;

        Result<string, string> result = _codeTool.ExtractMethod(code, startLine, endLine, newMethodName);

        return result.Match(
            success => new McpToolResult { Success = true, Data = new { code = success } },
            error => new McpToolResult { Success = false, Error = error });
    }

    private async Task<McpToolResult> ExecuteSuggestDslStepAsync(Dictionary<string, object> parameters)
    {
        string currentDsl = parameters["currentDsl"].ToString() ?? string.Empty;
        int maxSuggestions = parameters.ContainsKey("maxSuggestions")
            ? Convert.ToInt32(parameters["maxSuggestions"])
            : 5;

        Result<List<DslSuggestion>, string> result = await _dslAssistant.SuggestNextStepAsync(
            currentDsl,
            maxSuggestions: maxSuggestions);

        return result.Match(
            success => new McpToolResult
            {
                Success = true,
                Data = new { suggestions = success }
            },
            error => new McpToolResult { Success = false, Error = error });
    }

    private McpToolResult ExecuteCompleteToken(Dictionary<string, object> parameters)
    {
        string partialToken = parameters["partialToken"].ToString() ?? string.Empty;
        int maxCompletions = parameters.ContainsKey("maxCompletions")
            ? Convert.ToInt32(parameters["maxCompletions"])
            : 10;

        Result<List<string>, string> result = _dslAssistant.CompleteToken(partialToken, maxCompletions);

        return result.Match(
            success => new McpToolResult { Success = true, Data = new { completions = success } },
            error => new McpToolResult { Success = false, Error = error });
    }

    private async Task<McpToolResult> ExecuteValidateDslAsync(Dictionary<string, object> parameters)
    {
        string dsl = parameters["dsl"].ToString() ?? string.Empty;

        Result<DslValidationResult, string> result = await _dslAssistant.ValidateAndFixAsync(dsl);

        return result.Match(
            success => new McpToolResult
            {
                Success = true,
                Data = new
                {
                    isValid = success.IsValid,
                    errors = success.Errors,
                    warnings = success.Warnings,
                    suggestions = success.Suggestions,
                    fixedDsl = success.FixedDsl
                }
            },
            error => new McpToolResult { Success = false, Error = error });
    }

    private async Task<McpToolResult> ExecuteExplainDslAsync(Dictionary<string, object> parameters)
    {
        string dsl = parameters["dsl"].ToString() ?? string.Empty;

        Result<string, string> result = await _dslAssistant.ExplainDslAsync(dsl);

        return result.Match(
            success => new McpToolResult { Success = true, Data = new { explanation = success } },
            error => new McpToolResult { Success = false, Error = error });
    }

    private async Task<McpToolResult> ExecuteBuildDslAsync(Dictionary<string, object> parameters)
    {
        string goal = parameters["goal"].ToString() ?? string.Empty;

        Result<string, string> result = await _dslAssistant.BuildDslInteractivelyAsync(goal);

        return result.Match(
            success => new McpToolResult { Success = true, Data = new { dsl = success } },
            error => new McpToolResult { Success = false, Error = error });
    }

    // GitHub tool execution methods

    private async Task<McpToolResult> ExecuteGitHubCreatePrAsync(Dictionary<string, object> parameters)
    {
        if (_githubClient == null)
        {
            return new McpToolResult { Success = false, Error = "GitHub client not configured" };
        }

        string title = parameters["title"].ToString() ?? string.Empty;
        string description = parameters["description"].ToString() ?? string.Empty;
        string sourceBranch = parameters["sourceBranch"].ToString() ?? string.Empty;
        string targetBranch = parameters.ContainsKey("targetBranch")
            ? parameters["targetBranch"]?.ToString() ?? "main"
            : "main";

        var result = await _githubClient.CreatePullRequestAsync(title, description, sourceBranch, targetBranch);

        return result.Match(
            success => new McpToolResult
            {
                Success = true,
                Data = new
                {
                    number = success.Number,
                    url = success.Url,
                    title = success.Title,
                    state = success.State
                }
            },
            error => new McpToolResult { Success = false, Error = error });
    }

    private async Task<McpToolResult> ExecuteGitHubPushChangesAsync(Dictionary<string, object> parameters)
    {
        if (_githubClient == null)
        {
            return new McpToolResult { Success = false, Error = "GitHub client not configured" };
        }

        string branchName = parameters["branchName"].ToString() ?? string.Empty;
        string commitMessage = parameters["commitMessage"].ToString() ?? string.Empty;

        // Parse file changes from parameters
        var changes = new List<FileChange>();
        if (parameters.ContainsKey("changes") && parameters["changes"] is JsonElement changesElement)
        {
            foreach (var changeElement in changesElement.EnumerateArray())
            {
                changes.Add(new FileChange
                {
                    Path = changeElement.GetProperty("path").GetString() ?? string.Empty,
                    Content = changeElement.GetProperty("content").GetString() ?? string.Empty,
                    ChangeType = Enum.Parse<FileChangeType>(
                        changeElement.GetProperty("changeType").GetString() ?? "Update",
                        ignoreCase: true)
                });
            }
        }

        var result = await _githubClient.PushChangesAsync(branchName, changes, commitMessage);

        return result.Match(
            success => new McpToolResult
            {
                Success = true,
                Data = new
                {
                    sha = success.Sha,
                    message = success.Message,
                    url = success.Url
                }
            },
            error => new McpToolResult { Success = false, Error = error });
    }

    private async Task<McpToolResult> ExecuteGitHubCreateIssueAsync(Dictionary<string, object> parameters)
    {
        if (_githubClient == null)
        {
            return new McpToolResult { Success = false, Error = "GitHub client not configured" };
        }

        string title = parameters["title"].ToString() ?? string.Empty;
        string description = parameters["description"].ToString() ?? string.Empty;
        List<string>? labels = ExtractStringList(parameters, "labels");

        var result = await _githubClient.CreateIssueAsync(title, description, labels);

        return result.Match(
            success => new McpToolResult
            {
                Success = true,
                Data = new
                {
                    number = success.Number,
                    url = success.Url,
                    title = success.Title
                }
            },
            error => new McpToolResult { Success = false, Error = error });
    }

    private async Task<McpToolResult> ExecuteGitHubReadFileAsync(Dictionary<string, object> parameters)
    {
        if (_githubClient == null)
        {
            return new McpToolResult { Success = false, Error = "GitHub client not configured" };
        }

        string path = parameters["path"].ToString() ?? string.Empty;
        string? branch = parameters.ContainsKey("branch") ? parameters["branch"]?.ToString() : null;

        var result = await _githubClient.ReadFileAsync(path, branch);

        return result.Match(
            success => new McpToolResult
            {
                Success = true,
                Data = new
                {
                    path = success.Path,
                    content = success.Content,
                    size = success.Size
                }
            },
            error => new McpToolResult { Success = false, Error = error });
    }

    private async Task<McpToolResult> ExecuteGitHubListFilesAsync(Dictionary<string, object> parameters)
    {
        if (_githubClient == null)
        {
            return new McpToolResult { Success = false, Error = "GitHub client not configured" };
        }

        string path = parameters.ContainsKey("path") ? parameters["path"]?.ToString() ?? string.Empty : string.Empty;
        string? branch = parameters.ContainsKey("branch") ? parameters["branch"]?.ToString() : null;

        var result = await _githubClient.ListFilesAsync(path, branch);

        return result.Match(
            success => new McpToolResult
            {
                Success = true,
                Data = new { files = success }
            },
            error => new McpToolResult { Success = false, Error = error });
    }

    private async Task<McpToolResult> ExecuteGitHubCreateBranchAsync(Dictionary<string, object> parameters)
    {
        if (_githubClient == null)
        {
            return new McpToolResult { Success = false, Error = "GitHub client not configured" };
        }

        string branchName = parameters["branchName"].ToString() ?? string.Empty;
        string baseBranch = parameters.ContainsKey("baseBranch")
            ? parameters["baseBranch"]?.ToString() ?? "main"
            : "main";

        var result = await _githubClient.CreateBranchAsync(branchName, baseBranch);

        return result.Match(
            success => new McpToolResult
            {
                Success = true,
                Data = new
                {
                    name = success.Name,
                    sha = success.Sha
                }
            },
            error => new McpToolResult { Success = false, Error = error });
    }

    private async Task<McpToolResult> ExecuteGitHubSearchCodeAsync(Dictionary<string, object> parameters)
    {
        if (_githubClient == null)
        {
            return new McpToolResult { Success = false, Error = "GitHub client not configured" };
        }

        string query = parameters["query"].ToString() ?? string.Empty;
        string? path = parameters.ContainsKey("path") ? parameters["path"]?.ToString() : null;

        var result = await _githubClient.SearchCodeAsync(query, path);

        return result.Match(
            success => new McpToolResult
            {
                Success = true,
                Data = new { results = success }
            },
            error => new McpToolResult { Success = false, Error = error });
    }

    // Helper methods

    private List<string>? ExtractStringList(Dictionary<string, object> parameters, string key)
    {
        if (!parameters.ContainsKey(key))
            return null;

        object? value = parameters[key];
        if (value == null)
            return null;

        if (value is JsonElement jsonElement && jsonElement.ValueKind == JsonValueKind.Array)
        {
            return jsonElement.EnumerateArray()
                .Select(e => e.GetString() ?? string.Empty)
                .ToList();
        }

        if (value is IEnumerable<object> enumerable)
        {
            return enumerable.Select(o => o?.ToString() ?? string.Empty).ToList();
        }

        return null;
    }
}

/// <summary>
/// MCP response with available tools.
/// </summary>
public class McpResponse
{
    [JsonPropertyName("tools")]
    public List<McpTool> Tools { get; set; } = new List<McpTool>();
}

/// <summary>
/// MCP tool definition.
/// </summary>
public class McpTool
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("inputSchema")]
    public object? InputSchema { get; set; }
}

/// <summary>
/// Result of MCP tool execution.
/// </summary>
public class McpToolResult
{
    [JsonPropertyName("success")]
    public bool Success { get; set; }

    [JsonPropertyName("data")]
    public object? Data { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

