// <copyright file="GitReflectionService.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using SysEnv = System.Environment;

namespace Ouroboros.Domain.SelfModification;

/// <summary>
/// Represents a proposed code change for self-modification.
/// </summary>
public sealed record CodeChangeProposal(
    string Id,
    string FilePath,
    string Description,
    string Rationale,
    string OldCode,
    string NewCode,
    ChangeCategory Category,
    RiskLevel Risk,
    DateTime ProposedAt,
    string? ReviewComment = null,
    ProposalStatus Status = ProposalStatus.Pending);

/// <summary>
/// Category of code change.
/// </summary>
public enum ChangeCategory
{
    /// <summary>Bug fix.</summary>
    BugFix,

    /// <summary>Performance improvement.</summary>
    Performance,

    /// <summary>Code refactoring.</summary>
    Refactoring,

    /// <summary>New feature.</summary>
    Feature,

    /// <summary>Documentation update.</summary>
    Documentation,

    /// <summary>Test improvement.</summary>
    Testing,

    /// <summary>Security enhancement.</summary>
    Security,
}

/// <summary>
/// Risk level of a proposed change.
/// </summary>
public enum RiskLevel
{
    /// <summary>Low risk - documentation, comments, formatting.</summary>
    Low,

    /// <summary>Medium risk - refactoring, minor logic changes.</summary>
    Medium,

    /// <summary>High risk - core logic, public API changes.</summary>
    High,

    /// <summary>Critical - security, data handling changes.</summary>
    Critical,
}

/// <summary>
/// Status of a change proposal.
/// </summary>
public enum ProposalStatus
{
    /// <summary>Awaiting review.</summary>
    Pending,

    /// <summary>Approved for implementation.</summary>
    Approved,

    /// <summary>Rejected.</summary>
    Rejected,

    /// <summary>Successfully applied.</summary>
    Applied,

    /// <summary>Failed to apply.</summary>
    Failed,
}

/// <summary>
/// Result of a Git operation.
/// </summary>
public sealed record GitOperationResult(
    bool Success,
    string Message,
    string? CommitHash = null,
    string? BranchName = null,
    IReadOnlyList<string>? AffectedFiles = null);

/// <summary>
/// Information about a file in the repository.
/// </summary>
public sealed record RepoFileInfo(
    string RelativePath,
    string FullPath,
    long SizeBytes,
    DateTime LastModified,
    int LineCount,
    string Language);

/// <summary>
/// Analysis of code for reflection purposes.
/// </summary>
public sealed record CodeAnalysis(
    string FilePath,
    IReadOnlyList<string> Classes,
    IReadOnlyList<string> Methods,
    IReadOnlyList<string> Usings,
    int TotalLines,
    int CodeLines,
    int CommentLines,
    double CommentRatio,
    IReadOnlyList<string> Todos,
    IReadOnlyList<string> PotentialIssues);

/// <summary>
/// Provides Git-based self-reflection and code modification capabilities.
/// Enables Ouroboros to analyze, understand, and modify its own source code.
/// </summary>
public sealed class GitReflectionService : IDisposable
{
    private readonly string _repoRoot;
    private readonly List<CodeChangeProposal> _proposals = new();
    private readonly SemaphoreSlim _gitLock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Known safe directories for self-modification.
    /// </summary>
    public static readonly IReadOnlyList<string> SafeModificationPaths = new[]
    {
        "src/Ouroboros.Application",
        "src/Ouroboros.Domain",
        "src/Ouroboros.Agent",
        "src/Ouroboros.Tools",
        "docs",
        "examples",
    };

    /// <summary>
    /// File extensions allowed for modification.
    /// </summary>
    public static readonly IReadOnlyList<string> AllowedExtensions = new[]
    {
        ".cs", ".json", ".md", ".txt", ".yaml", ".yml", ".xml",
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="GitReflectionService"/> class.
    /// </summary>
    /// <param name="repoRoot">Root directory of the Git repository.</param>
    public GitReflectionService(string? repoRoot = null)
    {
        _repoRoot = repoRoot ?? FindRepoRoot() ?? SysEnv.CurrentDirectory;
    }

    /// <summary>
    /// Gets all pending change proposals.
    /// </summary>
    public IReadOnlyList<CodeChangeProposal> Proposals => _proposals.AsReadOnly();

    /// <summary>
    /// Finds the repository root by looking for .git folder.
    /// </summary>
    private static string? FindRepoRoot()
    {
        string? dir = SysEnv.CurrentDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
            {
                return dir;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }
        return null;
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CODE REFLECTION - Understanding Own Code
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lists all source files in the repository.
    /// </summary>
    public async Task<IReadOnlyList<RepoFileInfo>> ListSourceFilesAsync(
        string? filter = null,
        CancellationToken ct = default)
    {
        List<RepoFileInfo> files = new();
        string[] extensions = new[] { "*.cs", "*.json", "*.md", "*.yaml", "*.yml" };

        foreach (string ext in extensions)
        {
            foreach (string file in Directory.EnumerateFiles(_repoRoot, ext, SearchOption.AllDirectories))
            {
                if (file.Contains("bin") || file.Contains("obj") || file.Contains(".git"))
                    continue;

                if (filter != null && !file.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    continue;

                FileInfo fi = new(file);
                int lineCount = 0;
                try
                {
                    lineCount = (await File.ReadAllLinesAsync(file, ct)).Length;
                }
                catch
                {
                    // Ignore read errors
                }

                string language = Path.GetExtension(file).ToLowerInvariant() switch
                {
                    ".cs" => "C#",
                    ".json" => "JSON",
                    ".md" => "Markdown",
                    ".yaml" or ".yml" => "YAML",
                    _ => "Unknown",
                };

                files.Add(new RepoFileInfo(
                    Path.GetRelativePath(_repoRoot, file),
                    file,
                    fi.Length,
                    fi.LastWriteTimeUtc,
                    lineCount,
                    language));
            }
        }

        return files.OrderBy(f => f.RelativePath).ToList();
    }

    /// <summary>
    /// Analyzes a C# source file for reflection.
    /// </summary>
    public async Task<CodeAnalysis> AnalyzeFileAsync(string filePath, CancellationToken ct = default)
    {
        string fullPath = Path.IsPathRooted(filePath) ? filePath : Path.Combine(_repoRoot, filePath);

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        string content = await File.ReadAllTextAsync(fullPath, ct);
        string[] lines = content.Split('\n');

        // Extract classes
        List<string> classes = Regex.Matches(content, @"(?:public|internal|private|protected)\s+(?:sealed|abstract|static|partial)?\s*class\s+(\w+)")
            .Select(m => m.Groups[1].Value)
            .ToList();

        // Extract methods
        List<string> methods = Regex.Matches(content, @"(?:public|internal|private|protected)\s+(?:static|virtual|override|async)?\s*(?:\w+(?:<[\w,\s]+>)?)\s+(\w+)\s*\(")
            .Select(m => m.Groups[1].Value)
            .Where(m => !new[] { "if", "for", "while", "switch", "catch" }.Contains(m))
            .Distinct()
            .ToList();

        // Extract usings
        List<string> usings = Regex.Matches(content, @"using\s+([\w.]+);")
            .Select(m => m.Groups[1].Value)
            .ToList();

        // Count lines
        int commentLines = lines.Count(l => l.Trim().StartsWith("//") || l.Trim().StartsWith("/*") || l.Trim().StartsWith("*"));
        int codeLines = lines.Count(l => !string.IsNullOrWhiteSpace(l) && !l.Trim().StartsWith("//"));

        // Find TODOs
        List<string> todos = Regex.Matches(content, @"//\s*TODO:?\s*(.+)$", RegexOptions.Multiline)
            .Select(m => m.Groups[1].Value.Trim())
            .ToList();

        // Find potential issues
        List<string> issues = new();

        if (content.Contains("throw new NotImplementedException"))
            issues.Add("Contains NotImplementedException - incomplete implementation");
        if (Regex.IsMatch(content, @"catch\s*\(\s*Exception\s+\w+\s*\)\s*\{\s*\}"))
            issues.Add("Empty catch block - swallowing exceptions");
        if (content.Contains("// HACK"))
            issues.Add("Contains HACK comment");
        if (lines.Any(l => l.Length > 200))
            issues.Add("Contains very long lines (>200 chars)");
        if (methods.Count > 30)
            issues.Add($"Large file with {methods.Count} methods - consider splitting");

        return new CodeAnalysis(
            filePath,
            classes,
            methods,
            usings,
            lines.Length,
            codeLines,
            commentLines,
            commentLines / (double)Math.Max(1, lines.Length),
            todos,
            issues);
    }

    /// <summary>
    /// Searches code for a pattern across all files.
    /// </summary>
    public async Task<IReadOnlyList<(string File, int Line, string Content)>> SearchCodeAsync(
        string pattern,
        bool isRegex = false,
        CancellationToken ct = default)
    {
        List<(string, int, string)> results = new();
        IReadOnlyList<RepoFileInfo> files = await ListSourceFilesAsync(ct: ct);

        Regex? regex = isRegex ? new Regex(pattern, RegexOptions.IgnoreCase) : null;

        foreach (RepoFileInfo file in files)
        {
            if (file.Language != "C#") continue;

            try
            {
                string[] lines = await File.ReadAllLinesAsync(file.FullPath, ct);
                for (int i = 0; i < lines.Length; i++)
                {
                    bool match = regex != null
                        ? regex.IsMatch(lines[i])
                        : lines[i].Contains(pattern, StringComparison.OrdinalIgnoreCase);

                    if (match)
                    {
                        results.Add((file.RelativePath, i + 1, lines[i].Trim()));
                    }
                }
            }
            catch
            {
                // Skip files that can't be read
            }
        }

        return results;
    }

    /// <summary>
    /// Gets the structure overview of the codebase.
    /// </summary>
    public async Task<string> GetCodebaseOverviewAsync(CancellationToken ct = default)
    {
        IReadOnlyList<RepoFileInfo> files = await ListSourceFilesAsync(ct: ct);
        StringBuilder sb = new();

        sb.AppendLine("╔═══════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║               OUROBOROS CODEBASE OVERVIEW                     ║");
        sb.AppendLine("╠═══════════════════════════════════════════════════════════════╣");

        // Group by directory
        var byDir = files
            .GroupBy(f => Path.GetDirectoryName(f.RelativePath) ?? "root")
            .OrderBy(g => g.Key);

        int totalLines = 0;
        int totalFiles = 0;

        foreach (var group in byDir)
        {
            int dirLines = group.Sum(f => f.LineCount);
            totalLines += dirLines;
            totalFiles += group.Count();

            if (dirLines > 100) // Only show significant directories
            {
                sb.AppendLine($"║  📁 {group.Key,-40} {group.Count(),4} files {dirLines,6} lines ║");
            }
        }

        sb.AppendLine("╠═══════════════════════════════════════════════════════════════╣");
        sb.AppendLine($"║  TOTAL: {totalFiles,5} files, {totalLines,7} lines                       ║");
        sb.AppendLine("╚═══════════════════════════════════════════════════════════════╝");

        return sb.ToString();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // GIT OPERATIONS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Executes a Git command and returns the output.
    /// </summary>
    private async Task<(bool Success, string Output, string Error)> ExecuteGitAsync(
        string arguments,
        CancellationToken ct = default)
    {
        await _gitLock.WaitAsync(ct);
        try
        {
            ProcessStartInfo psi = new()
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = _repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using Process? process = Process.Start(psi);
            if (process == null)
            {
                return (false, "", "Failed to start git process");
            }

            string output = await process.StandardOutput.ReadToEndAsync(ct);
            string error = await process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);

            return (process.ExitCode == 0, output.Trim(), error.Trim());
        }
        finally
        {
            _gitLock.Release();
        }
    }

    /// <summary>
    /// Gets the current Git status.
    /// </summary>
    public async Task<string> GetStatusAsync(CancellationToken ct = default)
    {
        (bool success, string output, string error) = await ExecuteGitAsync("status --porcelain", ct);
        if (!success)
        {
            return $"Git status failed: {error}";
        }

        if (string.IsNullOrWhiteSpace(output))
        {
            return "Working tree clean - no uncommitted changes";
        }

        StringBuilder sb = new();
        sb.AppendLine("📊 Git Status:");
        foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string status = line[..2].Trim();
            string file = line[3..];
            string emoji = status switch
            {
                "M" => "📝",
                "A" => "➕",
                "D" => "🗑️",
                "??" => "❓",
                _ => "•",
            };
            sb.AppendLine($"  {emoji} {file}");
        }

        return sb.ToString();
    }

    /// <summary>
    /// Gets the current branch name.
    /// </summary>
    public async Task<string> GetCurrentBranchAsync(CancellationToken ct = default)
    {
        (bool success, string output, _) = await ExecuteGitAsync("branch --show-current", ct);
        return success ? output : "unknown";
    }

    /// <summary>
    /// Creates a new branch for self-modification.
    /// </summary>
    public async Task<GitOperationResult> CreateBranchAsync(
        string branchName,
        bool checkout = true,
        CancellationToken ct = default)
    {
        // Sanitize branch name
        string safeName = Regex.Replace(branchName, @"[^a-zA-Z0-9_-]", "-").ToLowerInvariant();
        string fullName = $"ouroboros/self-modify/{safeName}";

        string args = checkout ? $"checkout -b {fullName}" : $"branch {fullName}";
        (bool success, string output, string error) = await ExecuteGitAsync(args, ct);

        return new GitOperationResult(
            success,
            success ? $"Created branch: {fullName}" : $"Failed: {error}",
            BranchName: fullName);
    }

    /// <summary>
    /// Stages files for commit.
    /// </summary>
    public async Task<GitOperationResult> StageFilesAsync(
        IEnumerable<string> files,
        CancellationToken ct = default)
    {
        List<string> staged = new();
        foreach (string file in files)
        {
            (bool success, _, string error) = await ExecuteGitAsync($"add \"{file}\"", ct);
            if (success)
            {
                staged.Add(file);
            }
        }

        return new GitOperationResult(
            staged.Count > 0,
            $"Staged {staged.Count} files",
            AffectedFiles: staged);
    }

    /// <summary>
    /// Commits staged changes.
    /// </summary>
    public async Task<GitOperationResult> CommitAsync(
        string message,
        CancellationToken ct = default)
    {
        // Add Ouroboros signature to commit message
        string fullMessage = $"[Ouroboros Self-Modification] {message}";

        (bool success, string output, string error) = await ExecuteGitAsync($"commit -m \"{fullMessage}\"", ct);

        // Extract commit hash
        string? hash = null;
        if (success)
        {
            Match match = Regex.Match(output, @"\[[\w/]+\s+([a-f0-9]+)\]");
            if (match.Success)
            {
                hash = match.Groups[1].Value;
            }
        }

        return new GitOperationResult(
            success,
            success ? $"Committed: {hash}" : $"Commit failed: {error}",
            CommitHash: hash);
    }

    /// <summary>
    /// Gets recent commits.
    /// </summary>
    public async Task<IReadOnlyList<(string Hash, string Message, DateTime Date)>> GetRecentCommitsAsync(
        int count = 10,
        CancellationToken ct = default)
    {
        (bool success, string output, _) = await ExecuteGitAsync(
            $"log --oneline --date=iso -n {count} --format=\"%h|%s|%ai\"", ct);

        if (!success || string.IsNullOrWhiteSpace(output))
        {
            return Array.Empty<(string, string, DateTime)>();
        }

        List<(string, string, DateTime)> commits = new();
        foreach (string line in output.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = line.Split('|');
            if (parts.Length >= 3 && DateTime.TryParse(parts[2], out DateTime date))
            {
                commits.Add((parts[0], parts[1], date));
            }
        }

        return commits;
    }

    /// <summary>
    /// Gets the diff for a file.
    /// </summary>
    public async Task<string> GetFileDiffAsync(string filePath, CancellationToken ct = default)
    {
        (bool success, string output, _) = await ExecuteGitAsync($"diff \"{filePath}\"", ct);
        return success ? output : "No changes or file not tracked";
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CHANGE PROPOSALS
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Proposes a code change for review.
    /// </summary>
    public CodeChangeProposal ProposeChange(
        string filePath,
        string description,
        string rationale,
        string oldCode,
        string newCode,
        ChangeCategory category,
        RiskLevel risk)
    {
        CodeChangeProposal proposal = new(
            Id: Guid.NewGuid().ToString("N")[..8],
            FilePath: filePath,
            Description: description,
            Rationale: rationale,
            OldCode: oldCode,
            NewCode: newCode,
            Category: category,
            Risk: risk,
            ProposedAt: DateTime.UtcNow);

        _proposals.Add(proposal);
        return proposal;
    }

    /// <summary>
    /// Reviews and approves a change proposal.
    /// </summary>
    public bool ApproveProposal(string proposalId, string? comment = null)
    {
        int index = _proposals.FindIndex(p => p.Id == proposalId);
        if (index < 0) return false;

        _proposals[index] = _proposals[index] with
        {
            Status = ProposalStatus.Approved,
            ReviewComment = comment,
        };
        return true;
    }

    /// <summary>
    /// Rejects a change proposal.
    /// </summary>
    public bool RejectProposal(string proposalId, string reason)
    {
        int index = _proposals.FindIndex(p => p.Id == proposalId);
        if (index < 0) return false;

        _proposals[index] = _proposals[index] with
        {
            Status = ProposalStatus.Rejected,
            ReviewComment = reason,
        };
        return true;
    }

    /// <summary>
    /// Applies an approved change proposal.
    /// </summary>
    public async Task<GitOperationResult> ApplyProposalAsync(
        string proposalId,
        bool autoCommit = false,
        CancellationToken ct = default)
    {
        CodeChangeProposal? proposal = _proposals.FirstOrDefault(p => p.Id == proposalId);
        if (proposal == null)
        {
            return new GitOperationResult(false, $"Proposal {proposalId} not found");
        }

        if (proposal.Status != ProposalStatus.Approved)
        {
            return new GitOperationResult(false, $"Proposal {proposalId} is not approved (status: {proposal.Status})");
        }

        // Validate the file path is safe
        string relativePath = proposal.FilePath.Replace('\\', '/');
        bool isSafe = SafeModificationPaths.Any(p => relativePath.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        if (!isSafe && proposal.Risk >= RiskLevel.High)
        {
            return new GitOperationResult(false, $"Cannot modify {proposal.FilePath} - not in safe modification paths");
        }

        string fullPath = Path.IsPathRooted(proposal.FilePath)
            ? proposal.FilePath
            : Path.Combine(_repoRoot, proposal.FilePath);

        try
        {
            // Read current content
            string content = await File.ReadAllTextAsync(fullPath, ct);

            // Verify the old code exists
            if (!content.Contains(proposal.OldCode))
            {
                int idx = _proposals.FindIndex(p => p.Id == proposalId);
                if (idx >= 0)
                {
                    _proposals[idx] = _proposals[idx] with { Status = ProposalStatus.Failed };
                }
                return new GitOperationResult(false, "Old code not found in file - may have already been modified");
            }

            // Create backup
            string backupPath = fullPath + $".backup.{DateTime.Now:yyyyMMdd_HHmmss}";
            await File.WriteAllTextAsync(backupPath, content, ct);

            // Apply change
            string newContent = content.Replace(proposal.OldCode, proposal.NewCode);
            await File.WriteAllTextAsync(fullPath, newContent, ct);

            // Update proposal status
            int index = _proposals.FindIndex(p => p.Id == proposalId);
            if (index >= 0)
            {
                _proposals[index] = _proposals[index] with { Status = ProposalStatus.Applied };
            }

            // Auto-commit if requested
            if (autoCommit)
            {
                await StageFilesAsync(new[] { proposal.FilePath }, ct);
                await CommitAsync($"{proposal.Category}: {proposal.Description}", ct);
            }

            return new GitOperationResult(
                true,
                $"Applied change to {proposal.FilePath}",
                AffectedFiles: new[] { proposal.FilePath });
        }
        catch (Exception ex)
        {
            int index = _proposals.FindIndex(p => p.Id == proposalId);
            if (index >= 0)
            {
                _proposals[index] = _proposals[index] with { Status = ProposalStatus.Failed };
            }
            return new GitOperationResult(false, $"Failed to apply change: {ex.Message}");
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // SELF-MODIFICATION WORKFLOW
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Complete workflow: propose, review, apply, commit a change.
    /// </summary>
    public async Task<GitOperationResult> SelfModifyAsync(
        string filePath,
        string description,
        string rationale,
        string oldCode,
        string newCode,
        ChangeCategory category,
        bool autoApprove = false,
        CancellationToken ct = default)
    {
        // Assess risk
        RiskLevel risk = AssessRisk(filePath, oldCode, newCode);

        // Create proposal
        CodeChangeProposal proposal = ProposeChange(filePath, description, rationale, oldCode, newCode, category, risk);

        // Auto-approve only low-risk changes
        if (autoApprove && risk <= RiskLevel.Low)
        {
            ApproveProposal(proposal.Id, "Auto-approved (low risk)");
        }
        else if (autoApprove && risk == RiskLevel.Medium)
        {
            ApproveProposal(proposal.Id, "Auto-approved (medium risk - review recommended)");
        }
        else if (!autoApprove)
        {
            return new GitOperationResult(
                false,
                $"Proposal {proposal.Id} created (risk: {risk}). Call ApproveProposal to approve.");
        }
        else
        {
            return new GitOperationResult(
                false,
                $"High-risk change requires manual approval. Proposal ID: {proposal.Id}");
        }

        // Create branch for the change
        string branchName = $"{category.ToString().ToLowerInvariant()}-{proposal.Id}";
        GitOperationResult branchResult = await CreateBranchAsync(branchName, ct: ct);

        // Apply the change
        GitOperationResult applyResult = await ApplyProposalAsync(proposal.Id, autoCommit: true, ct: ct);

        return applyResult;
    }

    /// <summary>
    /// Assesses the risk level of a proposed change.
    /// </summary>
    private RiskLevel AssessRisk(string filePath, string oldCode, string newCode)
    {
        // Documentation changes are low risk
        if (filePath.EndsWith(".md") || filePath.EndsWith(".txt"))
        {
            return RiskLevel.Low;
        }

        // Check for high-risk patterns
        if (oldCode.Contains("private") && newCode.Contains("public"))
        {
            return RiskLevel.High; // Visibility change
        }

        if (Regex.IsMatch(oldCode, @"async\s+Task") && !Regex.IsMatch(newCode, @"async\s+Task"))
        {
            return RiskLevel.High; // Removing async
        }

        if (oldCode.Contains("try") && !newCode.Contains("try"))
        {
            return RiskLevel.High; // Removing error handling
        }

        // Security patterns
        if (newCode.Contains("unsafe") || newCode.Contains("Process.Start") ||
            newCode.Contains("File.Delete") || newCode.Contains("Directory.Delete"))
        {
            return RiskLevel.Critical;
        }

        // Size of change
        int sizeDiff = Math.Abs(newCode.Length - oldCode.Length);
        if (sizeDiff > 500)
        {
            return RiskLevel.Medium;
        }

        // Comments and whitespace only
        if (Regex.Replace(oldCode, @"[\s//*]+", "") == Regex.Replace(newCode, @"[\s//*]+", ""))
        {
            return RiskLevel.Low;
        }

        return RiskLevel.Medium;
    }

    /// <summary>
    /// Generates a summary of self-modification activity.
    /// </summary>
    public string GetModificationSummary()
    {
        StringBuilder sb = new();
        sb.AppendLine("╔═══════════════════════════════════════════════════════════════╗");
        sb.AppendLine("║           OUROBOROS SELF-MODIFICATION LOG                     ║");
        sb.AppendLine("╠═══════════════════════════════════════════════════════════════╣");

        IEnumerable<IGrouping<ProposalStatus, CodeChangeProposal>> byStatus = _proposals.GroupBy(p => p.Status);
        foreach (IGrouping<ProposalStatus, CodeChangeProposal> group in byStatus)
        {
            sb.AppendLine($"║  {group.Key,-15}: {group.Count(),5} proposals                         ║");
        }

        sb.AppendLine("╠═══════════════════════════════════════════════════════════════╣");

        foreach (CodeChangeProposal p in _proposals.OrderByDescending(p => p.ProposedAt).Take(5))
        {
            string status = p.Status switch
            {
                ProposalStatus.Applied => "✅",
                ProposalStatus.Approved => "🔵",
                ProposalStatus.Rejected => "❌",
                ProposalStatus.Failed => "⚠️",
                _ => "⏳",
            };
            sb.AppendLine($"║  {status} [{p.Id}] {p.Description.Truncate(40),-40}  ║");
        }

        sb.AppendLine("╚═══════════════════════════════════════════════════════════════╝");

        return sb.ToString();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!_disposed)
        {
            _gitLock.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// String extension methods.
/// </summary>
internal static class StringExtensions
{
    /// <summary>
    /// Truncates a string to the specified length.
    /// </summary>
    public static string Truncate(this string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";
    }
}
