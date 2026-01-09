// <copyright file="GitReflectionTools.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using Ouroboros.Domain.SelfModification;

namespace Ouroboros.Application.Tools;

/// <summary>
/// Tools for Git-based code reflection and self-modification.
/// These tools enable Ouroboros to analyze, understand, and modify its own source code.
/// </summary>
public static class GitReflectionTools
{
    private static GitReflectionService? _service;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets or initializes the Git reflection service.
    /// </summary>
    private static GitReflectionService GetService()
    {
        if (_service == null)
        {
            lock (_lock)
            {
                _service ??= new GitReflectionService();
            }
        }
        return _service;
    }

    /// <summary>
    /// Gets all Git reflection tools as a collection.
    /// Use this to add them to a ToolRegistry using WithTool().
    /// </summary>
    public static IEnumerable<ITool> GetAllTools()
    {
        yield return new GetCodebaseOverviewTool();
        yield return new AnalyzeFileTool();
        yield return new SearchCodeTool();
        yield return new ListSourceFilesTool();
        yield return new GitStatusTool();
        yield return new GitBranchTool();
        yield return new GitCommitTool();
        yield return new ProposeChangeTool();
        yield return new ApproveChangeTool();
        yield return new ApplyChangeTool();
        yield return new SelfModifyTool();
        yield return new GetModificationLogTool();
        yield return new ReflectOnCodeTool();
    }

    /// <summary>
    /// Adds all Git reflection tools to an existing registry.
    /// </summary>
    public static ToolRegistry WithGitReflectionTools(this ToolRegistry registry)
    {
        foreach (ITool tool in GetAllTools())
        {
            registry = registry.WithTool(tool);
        }
        return registry;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CODE REFLECTION TOOLS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gets an overview of the entire codebase.
    /// </summary>
    public class GetCodebaseOverviewTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "get_codebase_overview";

        /// <inheritdoc/>
        public string Description => "Get a high-level overview of my own codebase including directory structure, file counts, and line counts. Use this to understand the overall architecture.";

        /// <inheritdoc/>
        public string? JsonSchema => null;

        /// <inheritdoc/>
        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                GitReflectionService service = GetService();
                string overview = await service.GetCodebaseOverviewAsync(ct);
                return Result<string, string>.Success(overview);
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to get codebase overview: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Analyzes a specific source file.
    /// </summary>
    public class AnalyzeFileTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "analyze_my_file";

        /// <inheritdoc/>
        public string Description => "Analyze one of my own source files. Returns classes, methods, usings, TODOs, and potential issues. Input: file path (relative or absolute).";

        /// <inheritdoc/>
        public string? JsonSchema => null;

        /// <inheritdoc/>
        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                GitReflectionService service = GetService();
                CodeAnalysis analysis = await service.AnalyzeFileAsync(input.Trim(), ct);

                StringBuilder sb = new();
                sb.AppendLine($"📄 **Analysis: {analysis.FilePath}**\n");
                sb.AppendLine($"**Lines:** {analysis.TotalLines} total, {analysis.CodeLines} code, {analysis.CommentLines} comments ({analysis.CommentRatio:P0})");

                if (analysis.Classes.Count > 0)
                {
                    sb.AppendLine($"\n**Classes ({analysis.Classes.Count}):** {string.Join(", ", analysis.Classes)}");
                }

                if (analysis.Methods.Count > 0)
                {
                    sb.AppendLine($"\n**Methods ({analysis.Methods.Count}):** {string.Join(", ", analysis.Methods.Take(15))}");
                    if (analysis.Methods.Count > 15)
                    {
                        sb.AppendLine($"  ... and {analysis.Methods.Count - 15} more");
                    }
                }

                if (analysis.Todos.Count > 0)
                {
                    sb.AppendLine($"\n**TODOs ({analysis.Todos.Count}):**");
                    foreach (string todo in analysis.Todos.Take(5))
                    {
                        sb.AppendLine($"  - {todo}");
                    }
                }

                if (analysis.PotentialIssues.Count > 0)
                {
                    sb.AppendLine($"\n**⚠️ Potential Issues ({analysis.PotentialIssues.Count}):**");
                    foreach (string issue in analysis.PotentialIssues)
                    {
                        sb.AppendLine($"  - {issue}");
                    }
                }

                return Result<string, string>.Success(sb.ToString());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Analysis failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Searches code across the codebase.
    /// </summary>
    public class SearchCodeTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "search_my_codebase";

        /// <inheritdoc/>
        public string Description => "Search my own codebase for a pattern. Input JSON: {\"query\": \"search pattern\", \"regex\": false}. Returns matching lines with file and line number.";

        /// <inheritdoc/>
        public string? JsonSchema => """{"type":"object","properties":{"query":{"type":"string"},"regex":{"type":"boolean"}},"required":["query"]}""";

        /// <inheritdoc/>
        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                string query;
                bool isRegex = false;

                try
                {
                    JsonElement args = JsonSerializer.Deserialize<JsonElement>(input);
                    query = args.GetProperty("query").GetString() ?? "";
                    if (args.TryGetProperty("regex", out JsonElement regexProp))
                    {
                        isRegex = regexProp.GetBoolean();
                    }
                }
                catch
                {
                    query = input.Trim();
                }

                GitReflectionService service = GetService();
                IReadOnlyList<(string File, int Line, string Content)> results = await service.SearchCodeAsync(query, isRegex, ct);

                if (results.Count == 0)
                {
                    return Result<string, string>.Success($"No matches found for: {query}");
                }

                StringBuilder sb = new();
                sb.AppendLine($"🔍 **Found {results.Count} matches for:** `{query}`\n");

                foreach ((string file, int line, string content) in results.Take(20))
                {
                    sb.AppendLine($"**{file}:{line}**");
                    sb.AppendLine($"  `{content.Truncate(100)}`");
                }

                if (results.Count > 20)
                {
                    sb.AppendLine($"\n... and {results.Count - 20} more matches");
                }

                return Result<string, string>.Success(sb.ToString());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Search failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Lists source files in the repository.
    /// </summary>
    public class ListSourceFilesTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "list_my_files";

        /// <inheritdoc/>
        public string Description => "List my source files. Optional input: filter pattern (e.g., 'Tools' to list only files with 'Tools' in the path).";

        /// <inheritdoc/>
        public string? JsonSchema => null;

        /// <inheritdoc/>
        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                GitReflectionService service = GetService();
                string? filter = string.IsNullOrWhiteSpace(input) ? null : input.Trim();
                IReadOnlyList<RepoFileInfo> files = await service.ListSourceFilesAsync(filter, ct);

                StringBuilder sb = new();
                sb.AppendLine($"📁 **Source Files{(filter != null ? $" matching '{filter}'" : "")}** ({files.Count} files)\n");

                foreach (RepoFileInfo file in files.Take(50))
                {
                    string sizeStr = file.SizeBytes > 10000 ? $"{file.SizeBytes / 1024}KB" : $"{file.SizeBytes}B";
                    sb.AppendLine($"  📄 {file.RelativePath} ({file.LineCount} lines, {sizeStr})");
                }

                if (files.Count > 50)
                {
                    sb.AppendLine($"\n... and {files.Count - 50} more files");
                }

                return Result<string, string>.Success(sb.ToString());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to list files: {ex.Message}");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // GIT OPERATION TOOLS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Gets the current Git status.
    /// </summary>
    public class GitStatusTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "git_status";

        /// <inheritdoc/>
        public string Description => "Get the current Git status showing modified, staged, and untracked files.";

        /// <inheritdoc/>
        public string? JsonSchema => null;

        /// <inheritdoc/>
        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                GitReflectionService service = GetService();
                string status = await service.GetStatusAsync(ct);
                string branch = await service.GetCurrentBranchAsync(ct);

                return Result<string, string>.Success($"**Branch:** `{branch}`\n\n{status}");
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Git status failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Creates a new Git branch.
    /// </summary>
    public class GitBranchTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "git_create_branch";

        /// <inheritdoc/>
        public string Description => "Create a new Git branch for self-modification. Input: branch name (will be prefixed with 'ouroboros/self-modify/').";

        /// <inheritdoc/>
        public string? JsonSchema => null;

        /// <inheritdoc/>
        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                GitReflectionService service = GetService();
                GitOperationResult result = await service.CreateBranchAsync(input.Trim(), ct: ct);

                return result.Success
                    ? Result<string, string>.Success($"✅ {result.Message}")
                    : Result<string, string>.Failure(result.Message);
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Branch creation failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Commits staged changes.
    /// </summary>
    public class GitCommitTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "git_commit";

        /// <inheritdoc/>
        public string Description => "Commit staged changes. Input: commit message. Note: Message will be prefixed with '[Ouroboros Self-Modification]'.";

        /// <inheritdoc/>
        public string? JsonSchema => null;

        /// <inheritdoc/>
        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                GitReflectionService service = GetService();
                GitOperationResult result = await service.CommitAsync(input.Trim(), ct);

                return result.Success
                    ? Result<string, string>.Success($"✅ {result.Message}\nCommit: `{result.CommitHash}`")
                    : Result<string, string>.Failure(result.Message);
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Commit failed: {ex.Message}");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SELF-MODIFICATION TOOLS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Proposes a code change.
    /// </summary>
    public class ProposeChangeTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "propose_code_change";

        /// <inheritdoc/>
        public string Description => """
            Propose a change to my own source code for review before applying.
            Input JSON: {
                "file": "relative/path/to/file.cs",
                "description": "what the change does",
                "rationale": "why this change is needed",
                "old_code": "exact code to replace",
                "new_code": "replacement code",
                "category": "BugFix|Performance|Refactoring|Feature|Documentation|Testing|Security"
            }
            Returns a proposal ID that can be approved and applied.
            """;

        /// <inheritdoc/>
        public string? JsonSchema => """{"type":"object","properties":{"file":{"type":"string"},"description":{"type":"string"},"rationale":{"type":"string"},"old_code":{"type":"string"},"new_code":{"type":"string"},"category":{"type":"string"}},"required":["file","description","rationale","old_code","new_code","category"]}""";

        /// <inheritdoc/>
        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                JsonElement args = JsonSerializer.Deserialize<JsonElement>(input);
                string file = args.GetProperty("file").GetString() ?? "";
                string description = args.GetProperty("description").GetString() ?? "";
                string rationale = args.GetProperty("rationale").GetString() ?? "";
                string oldCode = args.GetProperty("old_code").GetString() ?? "";
                string newCode = args.GetProperty("new_code").GetString() ?? "";
                string categoryStr = args.GetProperty("category").GetString() ?? "Refactoring";

                if (!Enum.TryParse<ChangeCategory>(categoryStr, true, out ChangeCategory category))
                {
                    category = ChangeCategory.Refactoring;
                }

                GitReflectionService service = GetService();
                CodeChangeProposal proposal = service.ProposeChange(file, description, rationale, oldCode, newCode, category, RiskLevel.Medium);

                StringBuilder sb = new();
                sb.AppendLine($"📝 **Change Proposal Created**");
                sb.AppendLine($"**ID:** `{proposal.Id}`");
                sb.AppendLine($"**File:** {proposal.FilePath}");
                sb.AppendLine($"**Category:** {proposal.Category}");
                sb.AppendLine($"**Risk:** {proposal.Risk}");
                sb.AppendLine($"**Description:** {proposal.Description}");
                sb.AppendLine($"\nTo apply: use `approve_code_change` with ID `{proposal.Id}`, then `apply_code_change`");

                return Result<string, string>.Success(sb.ToString());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to create proposal: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Approves a change proposal.
    /// </summary>
    public class ApproveChangeTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "approve_code_change";

        /// <inheritdoc/>
        public string Description => "Approve a pending code change proposal. Input JSON: {\"id\": \"proposal_id\", \"comment\": \"optional review comment\"}";

        /// <inheritdoc/>
        public string? JsonSchema => """{"type":"object","properties":{"id":{"type":"string"},"comment":{"type":"string"}},"required":["id"]}""";

        /// <inheritdoc/>
        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                JsonElement args = JsonSerializer.Deserialize<JsonElement>(input);
                string id = args.GetProperty("id").GetString() ?? "";
                string? comment = args.TryGetProperty("comment", out JsonElement commentProp) ? commentProp.GetString() : null;

                GitReflectionService service = GetService();
                bool success = service.ApproveProposal(id, comment);

                return success
                    ? Result<string, string>.Success($"✅ Proposal `{id}` approved. Use `apply_code_change` to apply it.")
                    : Result<string, string>.Failure($"Proposal `{id}` not found");
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Approval failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Applies an approved change.
    /// </summary>
    public class ApplyChangeTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "apply_code_change";

        /// <inheritdoc/>
        public string Description => "Apply an approved code change proposal. Input JSON: {\"id\": \"proposal_id\", \"auto_commit\": true}";

        /// <inheritdoc/>
        public string? JsonSchema => """{"type":"object","properties":{"id":{"type":"string"},"auto_commit":{"type":"boolean"}},"required":["id"]}""";

        /// <inheritdoc/>
        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                JsonElement args = JsonSerializer.Deserialize<JsonElement>(input);
                string id = args.GetProperty("id").GetString() ?? "";
                bool autoCommit = args.TryGetProperty("auto_commit", out JsonElement commitProp) && commitProp.GetBoolean();

                GitReflectionService service = GetService();
                GitOperationResult result = await service.ApplyProposalAsync(id, autoCommit, ct);

                return result.Success
                    ? Result<string, string>.Success($"✅ {result.Message}\n\n⚠️ Note: Run `dotnet build` to verify changes compile correctly.")
                    : Result<string, string>.Failure(result.Message);
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Apply failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Complete self-modification workflow.
    /// </summary>
    public class SelfModifyTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "self_modify";

        /// <inheritdoc/>
        public string Description => """
            Complete self-modification workflow: propose, approve (if low risk), and apply a code change.
            Input JSON: {
                "file": "relative/path/to/file.cs",
                "description": "what the change does",
                "rationale": "why this change improves me",
                "old_code": "exact code to replace",
                "new_code": "replacement code",
                "category": "BugFix|Performance|Refactoring|Feature|Documentation|Testing"
            }
            Low-risk changes are auto-approved. High-risk changes require manual approval.
            """;

        /// <inheritdoc/>
        public string? JsonSchema => """{"type":"object","properties":{"file":{"type":"string"},"description":{"type":"string"},"rationale":{"type":"string"},"old_code":{"type":"string"},"new_code":{"type":"string"},"category":{"type":"string"}},"required":["file","description","rationale","old_code","new_code"]}""";

        /// <inheritdoc/>
        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                JsonElement args = JsonSerializer.Deserialize<JsonElement>(input);
                string file = args.GetProperty("file").GetString() ?? "";
                string description = args.GetProperty("description").GetString() ?? "";
                string rationale = args.GetProperty("rationale").GetString() ?? "";
                string oldCode = args.GetProperty("old_code").GetString() ?? "";
                string newCode = args.GetProperty("new_code").GetString() ?? "";
                string categoryStr = args.TryGetProperty("category", out JsonElement catProp) ? catProp.GetString() ?? "Refactoring" : "Refactoring";

                if (!Enum.TryParse<ChangeCategory>(categoryStr, true, out ChangeCategory category))
                {
                    category = ChangeCategory.Refactoring;
                }

                GitReflectionService service = GetService();
                GitOperationResult result = await service.SelfModifyAsync(file, description, rationale, oldCode, newCode, category, autoApprove: true, ct);

                if (result.Success)
                {
                    StringBuilder sb = new();
                    sb.AppendLine("🧬 **Self-Modification Complete**");
                    sb.AppendLine($"**File:** {file}");
                    sb.AppendLine($"**Change:** {description}");
                    sb.AppendLine($"**Branch:** {result.BranchName ?? "current"}");
                    sb.AppendLine($"\n⚠️ **Important:** Run `dotnet build` to verify the changes compile correctly.");
                    return Result<string, string>.Success(sb.ToString());
                }
                else
                {
                    return Result<string, string>.Failure(result.Message);
                }
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Self-modification failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Gets the modification log.
    /// </summary>
    public class GetModificationLogTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "get_modification_log";

        /// <inheritdoc/>
        public string Description => "Get a summary of all self-modification proposals and their status.";

        /// <inheritdoc/>
        public string? JsonSchema => null;

        /// <inheritdoc/>
        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            GitReflectionService service = GetService();
            string log = service.GetModificationSummary();
            return Result<string, string>.Success(log);
        }
    }

    /// <summary>
    /// Reflects on own code for improvement opportunities.
    /// </summary>
    public class ReflectOnCodeTool : ITool
    {
        /// <inheritdoc/>
        public string Name => "reflect_on_my_code";

        /// <inheritdoc/>
        public string Description => "Analyze a file in my codebase and identify potential improvements. Input: file path. Returns analysis with TODOs, issues, and improvement suggestions.";

        /// <inheritdoc/>
        public string? JsonSchema => null;

        /// <inheritdoc/>
        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                GitReflectionService service = GetService();
                string filePath = input.Trim();

                // Get file analysis
                CodeAnalysis analysis = await service.AnalyzeFileAsync(filePath, ct);

                StringBuilder sb = new();
                sb.AppendLine($"🔍 **Self-Reflection: {analysis.FilePath}**\n");

                // Summary
                sb.AppendLine("## Summary");
                sb.AppendLine($"- **Classes:** {analysis.Classes.Count}");
                sb.AppendLine($"- **Methods:** {analysis.Methods.Count}");
                sb.AppendLine($"- **Lines:** {analysis.TotalLines} ({analysis.CodeLines} code, {analysis.CommentLines} comments)");
                sb.AppendLine($"- **Comment Ratio:** {analysis.CommentRatio:P0}");

                // Issues
                if (analysis.PotentialIssues.Count > 0)
                {
                    sb.AppendLine("\n## ⚠️ Issues Found");
                    foreach (string issue in analysis.PotentialIssues)
                    {
                        sb.AppendLine($"- {issue}");
                    }
                }

                // TODOs
                if (analysis.Todos.Count > 0)
                {
                    sb.AppendLine("\n## 📝 TODOs");
                    foreach (string todo in analysis.Todos)
                    {
                        sb.AppendLine($"- {todo}");
                    }
                }

                // Improvement suggestions
                sb.AppendLine("\n## 💡 Improvement Opportunities");

                if (analysis.CommentRatio < 0.1)
                {
                    sb.AppendLine("- **Documentation:** Comment ratio is low. Consider adding XML documentation.");
                }

                if (analysis.Methods.Count > 20)
                {
                    sb.AppendLine("- **Refactoring:** Large number of methods. Consider extracting related methods to separate classes.");
                }

                if (analysis.TotalLines > 500)
                {
                    sb.AppendLine("- **Size:** File is large. Consider splitting into multiple files by responsibility.");
                }

                if (analysis.PotentialIssues.Any(i => i.Contains("NotImplementedException")))
                {
                    sb.AppendLine("- **Completeness:** Contains unimplemented methods. Complete the implementation.");
                }

                sb.AppendLine($"\nUse `self_modify` to apply improvements to this file.");

                return Result<string, string>.Success(sb.ToString());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Reflection failed: {ex.Message}");
            }
        }
    }
}

/// <summary>
/// String extension methods for the Application namespace.
/// </summary>
internal static class StringExtensions
{
    /// <summary>
    /// Truncates a string to the specified maximum length.
    /// </summary>
    public static string Truncate(this string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";
    }
}
