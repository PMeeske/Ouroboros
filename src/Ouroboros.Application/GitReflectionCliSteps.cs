// <copyright file="GitReflectionCliSteps.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Text;
using Ouroboros.Application.Tools;
using Ouroboros.Domain.SelfModification;

namespace Ouroboros.Application;

/// <summary>
/// CLI Pipeline steps for Git-based code reflection and self-modification.
/// Enables Ouroboros to analyze, understand, and modify its own source code through the pipeline DSL.
/// </summary>
public static class GitReflectionCliSteps
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

    // ═══════════════════════════════════════════════════════════════════════════
    // CODE REFLECTION STEPS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get an overview of the Ouroboros codebase.
    /// Usage: CodebaseOverview()
    /// </summary>
    [PipelineToken("CodebaseOverview", "MyCode")]
    public static Step<CliPipelineState, CliPipelineState> CodebaseOverview(string? args = null)
        => async s =>
        {
            try
            {
                GitReflectionService service = GetService();
                string overview = await service.GetCodebaseOverviewAsync();

                Console.WriteLine(overview);
                s.Output = overview;
                s.Context = "Codebase overview generated";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[git] Failed to get codebase overview: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Analyze a specific source file for reflection.
    /// Usage: AnalyzeFile('src/Ouroboros.Application/CliSteps.cs')
    /// </summary>
    [PipelineToken("AnalyzeFile", "ReflectFile")]
    public static Step<CliPipelineState, CliPipelineState> AnalyzeFile(string? args = null)
        => async s =>
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Console.WriteLine("[git] Usage: AnalyzeFile('path/to/file.cs')");
                return s;
            }

            try
            {
                GitReflectionService service = GetService();
                CodeAnalysis analysis = await service.AnalyzeFileAsync(args.Trim());

                StringBuilder sb = new();
                sb.AppendLine($"📄 Analysis: {analysis.FilePath}");
                sb.AppendLine($"   Lines: {analysis.TotalLines} total, {analysis.CodeLines} code, {analysis.CommentLines} comments");
                sb.AppendLine($"   Classes: {string.Join(", ", analysis.Classes)}");
                sb.AppendLine($"   Methods: {analysis.Methods.Count}");

                if (analysis.Todos.Count > 0)
                {
                    sb.AppendLine($"   TODOs: {analysis.Todos.Count}");
                    foreach (string todo in analysis.Todos.Take(3))
                    {
                        sb.AppendLine($"      - {todo}");
                    }
                }

                if (analysis.PotentialIssues.Count > 0)
                {
                    sb.AppendLine($"   ⚠️ Issues: {analysis.PotentialIssues.Count}");
                    foreach (string issue in analysis.PotentialIssues)
                    {
                        sb.AppendLine($"      - {issue}");
                    }
                }

                Console.WriteLine(sb.ToString());
                s.Output = sb.ToString();
                s.Context = $"Analyzed {analysis.FilePath}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[git] Analysis failed: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Search the codebase for a pattern.
    /// Usage: SearchCode('pattern')
    /// Usage: SearchCode('pattern;regex') - for regex search
    /// </summary>
    [PipelineToken("SearchCode", "FindCode")]
    public static Step<CliPipelineState, CliPipelineState> SearchCode(string? args = null)
        => async s =>
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Console.WriteLine("[git] Usage: SearchCode('pattern') or SearchCode('pattern;regex')");
                return s;
            }

            try
            {
                string[] parts = args.Split(';');
                string pattern = parts[0].Trim();
                bool isRegex = parts.Length > 1 && parts[1].Trim().ToLowerInvariant() == "regex";

                GitReflectionService service = GetService();
                IReadOnlyList<(string File, int Line, string Content)> results = await service.SearchCodeAsync(pattern, isRegex);

                StringBuilder sb = new();
                sb.AppendLine($"🔍 Found {results.Count} matches for: {pattern}");

                foreach ((string file, int line, string content) in results.Take(15))
                {
                    sb.AppendLine($"   {file}:{line} - {content.Truncate(60)}");
                }

                if (results.Count > 15)
                {
                    sb.AppendLine($"   ... and {results.Count - 15} more");
                }

                Console.WriteLine(sb.ToString());
                s.Output = sb.ToString();
                s.Context = $"Found {results.Count} matches for '{pattern}'";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[git] Search failed: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// List source files matching a filter.
    /// Usage: ListFiles() - list all files
    /// Usage: ListFiles('Application') - filter by path
    /// </summary>
    [PipelineToken("ListFiles", "MyFiles")]
    public static Step<CliPipelineState, CliPipelineState> ListFiles(string? args = null)
        => async s =>
        {
            try
            {
                GitReflectionService service = GetService();
                string? filter = string.IsNullOrWhiteSpace(args) ? null : args.Trim();
                IReadOnlyList<RepoFileInfo> files = await service.ListSourceFilesAsync(filter);

                StringBuilder sb = new();
                sb.AppendLine($"📁 Source Files{(filter != null ? $" matching '{filter}'" : "")} ({files.Count})");

                foreach (RepoFileInfo file in files.Take(30))
                {
                    sb.AppendLine($"   {file.RelativePath} ({file.LineCount} lines)");
                }

                if (files.Count > 30)
                {
                    sb.AppendLine($"   ... and {files.Count - 30} more");
                }

                Console.WriteLine(sb.ToString());
                s.Output = sb.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[git] Failed to list files: {ex.Message}");
            }

            return s;
        };

    // ═══════════════════════════════════════════════════════════════════════════
    // GIT OPERATION STEPS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Get Git status.
    /// Usage: GitStatus()
    /// </summary>
    [PipelineToken("GitStatus", "Status")]
    public static Step<CliPipelineState, CliPipelineState> GitStatus(string? args = null)
        => async s =>
        {
            try
            {
                GitReflectionService service = GetService();
                string status = await service.GetStatusAsync();
                string branch = await service.GetCurrentBranchAsync();

                Console.WriteLine($"🌿 Branch: {branch}");
                Console.WriteLine(status);

                s.Output = $"Branch: {branch}\n{status}";
                s.Context = $"On branch {branch}";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[git] Status failed: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Create a new branch for self-modification.
    /// Usage: GitBranch('feature-name')
    /// </summary>
    [PipelineToken("GitBranch", "CreateBranch")]
    public static Step<CliPipelineState, CliPipelineState> GitBranch(string? args = null)
        => async s =>
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Console.WriteLine("[git] Usage: GitBranch('branch-name')");
                return s;
            }

            try
            {
                GitReflectionService service = GetService();
                GitOperationResult result = await service.CreateBranchAsync(args.Trim());

                if (result.Success)
                {
                    Console.WriteLine($"✅ {result.Message}");
                    s.Context = $"On branch {result.BranchName}";
                }
                else
                {
                    Console.WriteLine($"❌ {result.Message}");
                }

                s.Output = result.Message;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[git] Branch creation failed: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Commit staged changes.
    /// Usage: GitCommit('commit message')
    /// </summary>
    [PipelineToken("GitCommit", "Commit")]
    public static Step<CliPipelineState, CliPipelineState> GitCommit(string? args = null)
        => async s =>
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Console.WriteLine("[git] Usage: GitCommit('commit message')");
                return s;
            }

            try
            {
                GitReflectionService service = GetService();
                GitOperationResult result = await service.CommitAsync(args.Trim());

                if (result.Success)
                {
                    Console.WriteLine($"✅ {result.Message}");
                    Console.WriteLine($"   Commit: {result.CommitHash}");
                }
                else
                {
                    Console.WriteLine($"❌ {result.Message}");
                }

                s.Output = result.Message;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[git] Commit failed: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Get recent commits.
    /// Usage: GitLog()
    /// Usage: GitLog('20') - get 20 commits
    /// </summary>
    [PipelineToken("GitLog", "RecentCommits")]
    public static Step<CliPipelineState, CliPipelineState> GitLog(string? args = null)
        => async s =>
        {
            try
            {
                int count = 10;
                if (!string.IsNullOrWhiteSpace(args) && int.TryParse(args.Trim(), out int parsed))
                {
                    count = Math.Min(50, Math.Max(1, parsed));
                }

                GitReflectionService service = GetService();
                IReadOnlyList<(string Hash, string Message, DateTime Date)> commits = await service.GetRecentCommitsAsync(count);

                StringBuilder sb = new();
                sb.AppendLine($"📜 Recent Commits ({commits.Count}):");

                foreach ((string hash, string message, DateTime date) in commits)
                {
                    sb.AppendLine($"   {hash} - {message} ({date:yyyy-MM-dd})");
                }

                Console.WriteLine(sb.ToString());
                s.Output = sb.ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[git] Log failed: {ex.Message}");
            }

            return s;
        };

    // ═══════════════════════════════════════════════════════════════════════════
    // SELF-MODIFICATION STEPS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Propose a code change for review.
    /// Usage: ProposeChange('file;description;old_code;new_code')
    /// </summary>
    [PipelineToken("ProposeChange", "Propose")]
    public static Step<CliPipelineState, CliPipelineState> ProposeChange(string? args = null)
        => s =>
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Console.WriteLine("[git] Usage: ProposeChange('file;description;old_code;new_code')");
                Console.WriteLine("[git] Or set s.Context with proposal details and use ProposeFromContext()");
                return Task.FromResult(s);
            }

            try
            {
                string[] parts = args.Split(';');
                if (parts.Length < 4)
                {
                    Console.WriteLine("[git] Need at least 4 parts: file;description;old_code;new_code");
                    return Task.FromResult(s);
                }

                GitReflectionService service = GetService();
                CodeChangeProposal proposal = service.ProposeChange(
                    parts[0].Trim(),
                    parts[1].Trim(),
                    "Self-improvement via pipeline",
                    parts[2].Trim(),
                    parts[3].Trim(),
                    ChangeCategory.Refactoring,
                    RiskLevel.Medium);

                Console.WriteLine($"📝 Proposal created: {proposal.Id}");
                Console.WriteLine($"   File: {proposal.FilePath}");
                Console.WriteLine($"   Risk: {proposal.Risk}");
                Console.WriteLine($"   Use ApproveChange('{proposal.Id}') to approve");

                s.Context = proposal.Id;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[git] Proposal failed: {ex.Message}");
            }

            return Task.FromResult(s);
        };

    /// <summary>
    /// Approve a pending change proposal.
    /// Usage: ApproveChange('proposal_id')
    /// </summary>
    [PipelineToken("ApproveChange", "Approve")]
    public static Step<CliPipelineState, CliPipelineState> ApproveChange(string? args = null)
        => s =>
        {
            string proposalId = args?.Trim() ?? s.Context;
            if (string.IsNullOrWhiteSpace(proposalId))
            {
                Console.WriteLine("[git] Usage: ApproveChange('proposal_id')");
                return Task.FromResult(s);
            }

            try
            {
                GitReflectionService service = GetService();
                bool success = service.ApproveProposal(proposalId);

                if (success)
                {
                    Console.WriteLine($"✅ Proposal {proposalId} approved");
                    Console.WriteLine($"   Use ApplyChange('{proposalId}') to apply");
                }
                else
                {
                    Console.WriteLine($"❌ Proposal {proposalId} not found");
                }

                s.Context = proposalId;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[git] Approval failed: {ex.Message}");
            }

            return Task.FromResult(s);
        };

    /// <summary>
    /// Apply an approved change.
    /// Usage: ApplyChange('proposal_id')
    /// Usage: ApplyChange('proposal_id;commit') - auto-commit after applying
    /// </summary>
    [PipelineToken("ApplyChange", "Apply")]
    public static Step<CliPipelineState, CliPipelineState> ApplyChange(string? args = null)
        => async s =>
        {
            string proposalId = args?.Split(';')[0].Trim() ?? s.Context;
            bool autoCommit = args?.Contains("commit") == true;

            if (string.IsNullOrWhiteSpace(proposalId))
            {
                Console.WriteLine("[git] Usage: ApplyChange('proposal_id') or ApplyChange('proposal_id;commit')");
                return s;
            }

            try
            {
                GitReflectionService service = GetService();
                GitOperationResult result = await service.ApplyProposalAsync(proposalId, autoCommit);

                if (result.Success)
                {
                    Console.WriteLine($"✅ {result.Message}");
                    Console.WriteLine("⚠️  Run `dotnet build` to verify changes");
                }
                else
                {
                    Console.WriteLine($"❌ {result.Message}");
                }

                s.Output = result.Message;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[git] Apply failed: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// View all change proposals and their status.
    /// Usage: ViewProposals()
    /// </summary>
    [PipelineToken("ViewProposals", "Proposals")]
    public static Step<CliPipelineState, CliPipelineState> ViewProposals(string? args = null)
        => s =>
        {
            GitReflectionService service = GetService();
            string summary = service.GetModificationSummary();

            Console.WriteLine(summary);
            s.Output = summary;

            return Task.FromResult(s);
        };

    /// <summary>
    /// Complete self-modification workflow: analyze, propose, approve, apply.
    /// Usage: SelfModify('file;description;old_code;new_code')
    /// </summary>
    [PipelineToken("SelfModify", "ModifyMyself")]
    public static Step<CliPipelineState, CliPipelineState> SelfModify(string? args = null)
        => async s =>
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Console.WriteLine("[git] Usage: SelfModify('file;description;old_code;new_code')");
                return s;
            }

            try
            {
                string[] parts = args.Split(';');
                if (parts.Length < 4)
                {
                    Console.WriteLine("[git] Need at least 4 parts: file;description;old_code;new_code");
                    return s;
                }

                GitReflectionService service = GetService();
                GitOperationResult result = await service.SelfModifyAsync(
                    parts[0].Trim(),
                    parts[1].Trim(),
                    "Self-improvement via pipeline DSL",
                    parts[2].Trim(),
                    parts[3].Trim(),
                    ChangeCategory.Refactoring,
                    autoApprove: true);

                if (result.Success)
                {
                    Console.WriteLine("🧬 Self-Modification Complete");
                    Console.WriteLine($"   {result.Message}");
                    Console.WriteLine("⚠️  Run `dotnet build` to verify changes");
                }
                else
                {
                    Console.WriteLine($"❌ {result.Message}");
                }

                s.Output = result.Message;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[git] Self-modification failed: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Reflect on a file and suggest improvements.
    /// Usage: ReflectOnFile('path/to/file.cs')
    /// </summary>
    [PipelineToken("ReflectOnFile", "CodeReflect")]
    public static Step<CliPipelineState, CliPipelineState> ReflectOnFile(string? args = null)
        => async s =>
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                Console.WriteLine("[git] Usage: ReflectOnFile('path/to/file.cs')");
                return s;
            }

            try
            {
                GitReflectionService service = GetService();
                CodeAnalysis analysis = await service.AnalyzeFileAsync(args.Trim());

                Console.WriteLine($"\n🔍 Self-Reflection: {analysis.FilePath}");
                Console.WriteLine($"════════════════════════════════════════════");

                // Summary
                Console.WriteLine($"📊 Metrics:");
                Console.WriteLine($"   Classes: {analysis.Classes.Count}");
                Console.WriteLine($"   Methods: {analysis.Methods.Count}");
                Console.WriteLine($"   Lines: {analysis.TotalLines} ({analysis.CommentRatio:P0} comments)");

                // Issues
                if (analysis.PotentialIssues.Count > 0)
                {
                    Console.WriteLine($"\n⚠️  Issues ({analysis.PotentialIssues.Count}):");
                    foreach (string issue in analysis.PotentialIssues)
                    {
                        Console.WriteLine($"   - {issue}");
                    }
                }

                // TODOs
                if (analysis.Todos.Count > 0)
                {
                    Console.WriteLine($"\n📝 TODOs ({analysis.Todos.Count}):");
                    foreach (string todo in analysis.Todos.Take(5))
                    {
                        Console.WriteLine($"   - {todo}");
                    }
                }

                // Suggestions
                Console.WriteLine($"\n💡 Improvement Suggestions:");
                if (analysis.CommentRatio < 0.1)
                {
                    Console.WriteLine("   - Add more documentation (comment ratio < 10%)");
                }
                if (analysis.Methods.Count > 20)
                {
                    Console.WriteLine($"   - Consider splitting file ({analysis.Methods.Count} methods is large)");
                }
                if (analysis.TotalLines > 500)
                {
                    Console.WriteLine($"   - File is large ({analysis.TotalLines} lines), consider refactoring");
                }

                Console.WriteLine($"\nUse SelfModify() to apply improvements");

                s.Output = $"Reflected on {analysis.FilePath}";
                s.Context = args.Trim();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[git] Reflection failed: {ex.Message}");
            }

            return s;
        };
}
