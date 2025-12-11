// <copyright file="SystemAccessTools.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

namespace Ouroboros.Application.Tools;

using System.Diagnostics;
using System.Text;
using System.Text.Json;
using LangChainPipeline.Core.Monads;
using Ouroboros.Application.Services;
using Ouroboros.Tools;

/// <summary>
/// Provides comprehensive system access tools for Ouroboros.
/// Enables file system, process, registry, and system information access.
/// </summary>
public static class SystemAccessTools
{
    /// <summary>
    /// Shared self-indexer instance for indexing tools.
    /// </summary>
    public static QdrantSelfIndexer? SharedIndexer { get; set; }

    /// <summary>
    /// Creates all system access tools.
    /// </summary>
    public static IEnumerable<ITool> CreateAllTools()
    {
        yield return new FileSystemTool();
        yield return new DirectoryListTool();
        yield return new FileReadTool();
        yield return new FileWriteTool();
        yield return new FileSearchTool();
        yield return new FileIndexTool();
        yield return new SearchIndexedContentTool();
        yield return new SearchMyCodeTool();
        yield return new ReadMyFileTool();
        yield return new ProcessListTool();
        yield return new ProcessStartTool();
        yield return new ProcessKillTool();
        yield return new SystemInfoTool();
        yield return new EnvironmentTool();
        yield return new PowerShellTool();
        yield return new ClipboardTool();
        yield return new NetworkInfoTool();
        yield return new DiskInfoTool();
    }

    /// <summary>
    /// File system navigation and info tool.
    /// </summary>
    public class FileSystemTool : ITool
    {
        public string Name => "file_system";
        public string Description => "Navigate and get info about files/folders. Input: JSON {\"action\":\"exists|info|size|modified\", \"path\":\"...\"}";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                var args = JsonSerializer.Deserialize<JsonElement>(input);
                var action = args.GetProperty("action").GetString() ?? "exists";
                var path = args.GetProperty("path").GetString() ?? "";

                path = Environment.ExpandEnvironmentVariables(path);

                return action.ToLower() switch
                {
                    "exists" => Result<string, string>.Success(
                        (File.Exists(path) || Directory.Exists(path)).ToString()),
                    "info" => GetFileInfo(path),
                    "size" => GetSize(path),
                    "modified" => GetModified(path),
                    _ => Result<string, string>.Failure($"Unknown action: {action}")
                };
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
        }

        private static Result<string, string> GetFileInfo(string path)
        {
            if (File.Exists(path))
            {
                var fi = new FileInfo(path);
                return Result<string, string>.Success(JsonSerializer.Serialize(new
                {
                    type = "file",
                    name = fi.Name,
                    fullPath = fi.FullName,
                    size = fi.Length,
                    created = fi.CreationTime,
                    modified = fi.LastWriteTime,
                    extension = fi.Extension,
                    readOnly = fi.IsReadOnly
                }));
            }
            else if (Directory.Exists(path))
            {
                var di = new DirectoryInfo(path);
                return Result<string, string>.Success(JsonSerializer.Serialize(new
                {
                    type = "directory",
                    name = di.Name,
                    fullPath = di.FullName,
                    created = di.CreationTime,
                    modified = di.LastWriteTime,
                    fileCount = di.GetFiles().Length,
                    dirCount = di.GetDirectories().Length
                }));
            }
            return Result<string, string>.Failure("Path not found");
        }

        private static Result<string, string> GetSize(string path)
        {
            if (File.Exists(path))
                return Result<string, string>.Success(new FileInfo(path).Length.ToString());
            if (Directory.Exists(path))
            {
                var size = new DirectoryInfo(path).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
                return Result<string, string>.Success(size.ToString());
            }
            return Result<string, string>.Failure("Path not found");
        }

        private static Result<string, string> GetModified(string path)
        {
            if (File.Exists(path))
                return Result<string, string>.Success(File.GetLastWriteTime(path).ToString("o"));
            if (Directory.Exists(path))
                return Result<string, string>.Success(Directory.GetLastWriteTime(path).ToString("o"));
            return Result<string, string>.Failure("Path not found");
        }
    }

    /// <summary>
    /// List directory contents.
    /// </summary>
    public class DirectoryListTool : ITool
    {
        public string Name => "list_directory";
        public string Description => "List files and folders in a directory. Input: path (supports %ENV% vars)";
        public string? JsonSchema => null;

        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                var path = Environment.ExpandEnvironmentVariables(input.Trim().Trim('"'));
                if (string.IsNullOrEmpty(path)) path = Environment.CurrentDirectory;

                if (!Directory.Exists(path))
                    return Task.FromResult(Result<string, string>.Failure($"Directory not found: {path}"));

                var sb = new StringBuilder();
                sb.AppendLine($"📁 {path}");
                sb.AppendLine();

                var dirs = Directory.GetDirectories(path).Take(50);
                foreach (var dir in dirs)
                {
                    var name = Path.GetFileName(dir);
                    sb.AppendLine($"  [DIR]  {name}/");
                }

                var files = Directory.GetFiles(path).Take(100);
                foreach (var file in files)
                {
                    var fi = new FileInfo(file);
                    sb.AppendLine($"  [FILE] {fi.Name} ({FormatSize(fi.Length)})");
                }

                var totalDirs = Directory.GetDirectories(path).Length;
                var totalFiles = Directory.GetFiles(path).Length;
                sb.AppendLine();
                sb.AppendLine($"Total: {totalDirs} folders, {totalFiles} files");

                return Task.FromResult(Result<string, string>.Success(sb.ToString()));
            }
            catch (Exception ex)
            {
                return Task.FromResult(Result<string, string>.Failure(ex.Message));
            }
        }

        private static string FormatSize(long bytes) => bytes switch
        {
            < 1024 => $"{bytes} B",
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
        };
    }

    /// <summary>
    /// Read file contents.
    /// </summary>
    public class FileReadTool : ITool
    {
        public string Name => "read_file";
        public string Description => "Read file contents. Input: JSON {\"path\":\"...\", \"lines\":100} or just path";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                string path;
                int maxLines = 500;

                if (input.TrimStart().StartsWith("{"))
                {
                    var args = JsonSerializer.Deserialize<JsonElement>(input);
                    path = args.GetProperty("path").GetString() ?? "";
                    if (args.TryGetProperty("lines", out var linesEl))
                        maxLines = linesEl.GetInt32();
                }
                else
                {
                    path = input.Trim().Trim('"');
                }

                path = Environment.ExpandEnvironmentVariables(path);

                if (!File.Exists(path))
                    return Result<string, string>.Failure($"File not found: {path}");

                var lines = await File.ReadAllLinesAsync(path, ct);
                if (lines.Length > maxLines)
                {
                    var result = string.Join("\n", lines.Take(maxLines));
                    return Result<string, string>.Success($"{result}\n\n... [{lines.Length - maxLines} more lines truncated]");
                }

                return Result<string, string>.Success(string.Join("\n", lines));
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
        }
    }

    /// <summary>
    /// Write to files.
    /// </summary>
    public class FileWriteTool : ITool
    {
        public string Name => "write_file";
        public string Description => "Write content to a file. Input: JSON {\"path\":\"...\", \"content\":\"...\", \"append\":false}";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                var args = JsonSerializer.Deserialize<JsonElement>(input);
                var path = Environment.ExpandEnvironmentVariables(args.GetProperty("path").GetString() ?? "");
                var content = args.GetProperty("content").GetString() ?? "";
                var append = args.TryGetProperty("append", out var appendEl) && appendEl.GetBoolean();

                // Ensure directory exists
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                if (append)
                    await File.AppendAllTextAsync(path, content, ct);
                else
                    await File.WriteAllTextAsync(path, content, ct);

                return Result<string, string>.Success($"Written {content.Length} chars to {path}");
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
        }
    }

    /// <summary>
    /// Search for files.
    /// </summary>
    public class FileSearchTool : ITool
    {
        public string Name => "search_files";
        public string Description => "Search for files. Input: JSON {\"path\":\"...\", \"pattern\":\"*.cs\", \"recursive\":true, \"contains\":\"text\"}";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                var args = JsonSerializer.Deserialize<JsonElement>(input);
                var path = Environment.ExpandEnvironmentVariables(args.GetProperty("path").GetString() ?? ".");
                var pattern = args.TryGetProperty("pattern", out var patEl) ? patEl.GetString() ?? "*" : "*";
                var recursive = !args.TryGetProperty("recursive", out var recEl) || recEl.GetBoolean();
                var contains = args.TryGetProperty("contains", out var contEl) ? contEl.GetString() : null;

                var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                var files = Directory.EnumerateFiles(path, pattern, searchOption).Take(200).ToList();

                if (!string.IsNullOrEmpty(contains))
                {
                    var matching = new List<string>();
                    foreach (var file in files)
                    {
                        try
                        {
                            var content = await File.ReadAllTextAsync(file, ct);
                            if (content.Contains(contains, StringComparison.OrdinalIgnoreCase))
                                matching.Add(file);
                        }
                        catch { }
                    }
                    files = matching;
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Found {files.Count} files matching '{pattern}':");
                foreach (var file in files.Take(50))
                    sb.AppendLine($"  {file}");
                if (files.Count > 50)
                    sb.AppendLine($"  ... and {files.Count - 50} more");

                return Result<string, string>.Success(sb.ToString());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
        }
    }

    /// <summary>
    /// List running processes.
    /// </summary>
    public class ProcessListTool : ITool
    {
        public string Name => "list_processes";
        public string Description => "List running processes. Input: optional filter (e.g., 'chrome' or empty for all)";
        public string? JsonSchema => null;

        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                var filter = input.Trim().ToLower();
                var processes = Process.GetProcesses()
                    .Where(p => string.IsNullOrEmpty(filter) || p.ProcessName.ToLower().Contains(filter))
                    .OrderByDescending(p => { try { return p.WorkingSet64; } catch { return 0; } })
                    .Take(50)
                    .ToList();

                var sb = new StringBuilder();
                sb.AppendLine($"{"PID",-8} {"Name",-30} {"Memory",-12} {"CPU Time",-15}");
                sb.AppendLine(new string('-', 70));

                foreach (var p in processes)
                {
                    try
                    {
                        var mem = FormatSize(p.WorkingSet64);
                        var cpu = p.TotalProcessorTime.ToString(@"hh\:mm\:ss");
                        sb.AppendLine($"{p.Id,-8} {p.ProcessName,-30} {mem,-12} {cpu,-15}");
                    }
                    catch { }
                }

                return Task.FromResult(Result<string, string>.Success(sb.ToString()));
            }
            catch (Exception ex)
            {
                return Task.FromResult(Result<string, string>.Failure(ex.Message));
            }
        }

        private static string FormatSize(long bytes) => bytes switch
        {
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
        };
    }

    /// <summary>
    /// Start a process.
    /// </summary>
    public class ProcessStartTool : ITool
    {
        public string Name => "start_process";
        public string Description => "Start a program. Input: JSON {\"program\":\"notepad.exe\", \"args\":\"\", \"wait\":false}";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                string program;
                string args = "";
                bool wait = false;

                if (input.TrimStart().StartsWith("{"))
                {
                    var json = JsonSerializer.Deserialize<JsonElement>(input);
                    program = json.GetProperty("program").GetString() ?? "";
                    if (json.TryGetProperty("args", out var argsEl))
                        args = argsEl.GetString() ?? "";
                    if (json.TryGetProperty("wait", out var waitEl))
                        wait = waitEl.GetBoolean();
                }
                else
                {
                    program = input.Trim();
                }

                var psi = new ProcessStartInfo(program, args)
                {
                    UseShellExecute = true
                };

                var process = Process.Start(psi);
                if (process == null)
                    return Result<string, string>.Failure("Failed to start process");

                if (wait)
                {
                    await process.WaitForExitAsync(ct);
                    return Result<string, string>.Success($"Process {program} completed with exit code {process.ExitCode}");
                }

                return Result<string, string>.Success($"Started {program} (PID: {process.Id})");
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
        }
    }

    /// <summary>
    /// Kill a process.
    /// </summary>
    public class ProcessKillTool : ITool
    {
        public string Name => "kill_process";
        public string Description => "Kill a process by PID or name. Input: PID number or process name";
        public string? JsonSchema => null;

        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                var trimmed = input.Trim();

                if (int.TryParse(trimmed, out var pid))
                {
                    var process = Process.GetProcessById(pid);
                    process.Kill();
                    return Task.FromResult(Result<string, string>.Success($"Killed process {pid}"));
                }
                else
                {
                    var processes = Process.GetProcessesByName(trimmed);
                    if (processes.Length == 0)
                        return Task.FromResult(Result<string, string>.Failure($"No process found: {trimmed}"));

                    foreach (var p in processes)
                        p.Kill();

                    return Task.FromResult(Result<string, string>.Success($"Killed {processes.Length} process(es) named '{trimmed}'"));
                }
            }
            catch (Exception ex)
            {
                return Task.FromResult(Result<string, string>.Failure(ex.Message));
            }
        }
    }

    /// <summary>
    /// Get system information.
    /// </summary>
    public class SystemInfoTool : ITool
    {
        public string Name => "system_info";
        public string Description => "Get system information (OS, CPU, memory, uptime)";
        public string? JsonSchema => null;

        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== System Information ===");
                sb.AppendLine($"Computer Name: {Environment.MachineName}");
                sb.AppendLine($"User: {Environment.UserName}");
                sb.AppendLine($"Domain: {Environment.UserDomainName}");
                sb.AppendLine($"OS: {Environment.OSVersion}");
                sb.AppendLine($"64-bit OS: {Environment.Is64BitOperatingSystem}");
                sb.AppendLine($"64-bit Process: {Environment.Is64BitProcess}");
                sb.AppendLine($"Processors: {Environment.ProcessorCount}");
                sb.AppendLine($".NET Version: {Environment.Version}");
                sb.AppendLine($"System Directory: {Environment.SystemDirectory}");
                sb.AppendLine($"Current Directory: {Environment.CurrentDirectory}");

                // Memory info via GC
                var gcInfo = GC.GetGCMemoryInfo();
                sb.AppendLine($"Total Memory: {FormatSize(gcInfo.TotalAvailableMemoryBytes)}");
                sb.AppendLine($"Process Memory: {FormatSize(Environment.WorkingSet)}");

                // Uptime
                var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
                sb.AppendLine($"System Uptime: {uptime.Days}d {uptime.Hours}h {uptime.Minutes}m");

                return Task.FromResult(Result<string, string>.Success(sb.ToString()));
            }
            catch (Exception ex)
            {
                return Task.FromResult(Result<string, string>.Failure(ex.Message));
            }
        }

        private static string FormatSize(long bytes) => bytes switch
        {
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
        };
    }

    /// <summary>
    /// Get/set environment variables.
    /// </summary>
    public class EnvironmentTool : ITool
    {
        public string Name => "environment";
        public string Description => "Get/set environment variables. Input: JSON {\"action\":\"get|set|list\", \"name\":\"PATH\", \"value\":\"...\"}";
        public string? JsonSchema => null;

        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(input) || input.Trim() == "list")
                {
                    var vars = Environment.GetEnvironmentVariables();
                    var sb = new StringBuilder();
                    foreach (System.Collections.DictionaryEntry entry in vars)
                    {
                        var value = entry.Value?.ToString() ?? "";
                        if (value.Length > 100) value = value[..100] + "...";
                        sb.AppendLine($"{entry.Key}={value}");
                    }
                    return Task.FromResult(Result<string, string>.Success(sb.ToString()));
                }

                var args = JsonSerializer.Deserialize<JsonElement>(input);
                var action = args.TryGetProperty("action", out var actEl) ? actEl.GetString() ?? "get" : "get";
                var name = args.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";

                return action.ToLower() switch
                {
                    "get" => Task.FromResult(Result<string, string>.Success(
                        Environment.GetEnvironmentVariable(name) ?? $"[{name} not set]")),
                    "set" when args.TryGetProperty("value", out var valEl) =>
                        SetEnvVar(name, valEl.GetString() ?? ""),
                    "list" => InvokeAsync("list", ct),
                    _ => Task.FromResult(Result<string, string>.Failure($"Unknown action: {action}"))
                };
            }
            catch (Exception ex)
            {
                return Task.FromResult(Result<string, string>.Failure(ex.Message));
            }
        }

        private static Task<Result<string, string>> SetEnvVar(string name, string value)
        {
            Environment.SetEnvironmentVariable(name, value);
            return Task.FromResult(Result<string, string>.Success($"Set {name}={value}"));
        }
    }

    /// <summary>
    /// Execute PowerShell commands.
    /// </summary>
    public class PowerShellTool : ITool
    {
        public string Name => "powershell";
        public string Description => "Execute PowerShell commands. Input: command string";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -NonInteractive -Command \"{input.Replace("\"", "\\\"")}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                    return Result<string, string>.Failure("Failed to start PowerShell");

                var output = await process.StandardOutput.ReadToEndAsync(ct);
                var error = await process.StandardError.ReadToEndAsync(ct);
                await process.WaitForExitAsync(ct);

                if (!string.IsNullOrWhiteSpace(error))
                    return Result<string, string>.Success($"{output}\n[STDERR]: {error}");

                return Result<string, string>.Success(output);
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
        }
    }

    /// <summary>
    /// Clipboard access.
    /// </summary>
    public class ClipboardTool : ITool
    {
        public string Name => "clipboard";
        public string Description => "Read/write clipboard. Input: JSON {\"action\":\"get|set\", \"text\":\"...\"}";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                string action = "get";
                string text = "";

                if (!string.IsNullOrWhiteSpace(input) && input.TrimStart().StartsWith("{"))
                {
                    var args = JsonSerializer.Deserialize<JsonElement>(input);
                    action = args.TryGetProperty("action", out var actEl) ? actEl.GetString() ?? "get" : "get";
                    text = args.TryGetProperty("text", out var textEl) ? textEl.GetString() ?? "" : "";
                }

                if (action == "set" && !string.IsNullOrEmpty(text))
                {
                    // Use PowerShell to set clipboard
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = $"-NoProfile -Command \"Set-Clipboard -Value '{text.Replace("'", "''")}'\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var p = Process.Start(psi);
                    if (p != null) await p.WaitForExitAsync(ct);
                    return Result<string, string>.Success("Clipboard updated");
                }
                else
                {
                    // Use PowerShell to get clipboard
                    var psi = new ProcessStartInfo
                    {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -Command \"Get-Clipboard\"",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using var p = Process.Start(psi);
                    if (p == null) return Result<string, string>.Failure("Failed to access clipboard");
                    var result = await p.StandardOutput.ReadToEndAsync(ct);
                    return Result<string, string>.Success(result.Trim());
                }
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
        }
    }

    /// <summary>
    /// Network information.
    /// </summary>
    public class NetworkInfoTool : ITool
    {
        public string Name => "network_info";
        public string Description => "Get network information (IP addresses, adapters, connectivity)";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoProfile -Command \"Get-NetIPAddress | Where-Object {$_.AddressFamily -eq 'IPv4'} | Select-Object InterfaceAlias, IPAddress, PrefixLength | Format-Table -AutoSize\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null)
                    return Result<string, string>.Failure("Failed to get network info");

                var output = await process.StandardOutput.ReadToEndAsync(ct);
                await process.WaitForExitAsync(ct);

                // Also get external IP
                try
                {
                    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                    var externalIp = await http.GetStringAsync("https://api.ipify.org", ct);
                    output += $"\nExternal IP: {externalIp}";
                }
                catch { }

                return Result<string, string>.Success(output);
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure(ex.Message);
            }
        }
    }

    /// <summary>
    /// Disk information.
    /// </summary>
    public class DiskInfoTool : ITool
    {
        public string Name => "disk_info";
        public string Description => "Get disk/drive information and space usage";
        public string? JsonSchema => null;

        public Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine($"{"Drive",-6} {"Label",-20} {"Type",-12} {"Total",-12} {"Free",-12} {"Used %",-8}");
                sb.AppendLine(new string('-', 75));

                foreach (var drive in DriveInfo.GetDrives())
                {
                    try
                    {
                        if (!drive.IsReady) continue;
                        var usedPercent = 100.0 * (drive.TotalSize - drive.TotalFreeSpace) / drive.TotalSize;
                        sb.AppendLine($"{drive.Name,-6} {drive.VolumeLabel,-20} {drive.DriveType,-12} {FormatSize(drive.TotalSize),-12} {FormatSize(drive.TotalFreeSpace),-12} {usedPercent:F1}%");
                    }
                    catch { }
                }

                return Task.FromResult(Result<string, string>.Success(sb.ToString()));
            }
            catch (Exception ex)
            {
                return Task.FromResult(Result<string, string>.Failure(ex.Message));
            }
        }

        private static string FormatSize(long bytes) => bytes switch
        {
            < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
            _ => $"{bytes / (1024.0 * 1024 * 1024):F2} GB"
        };
    }

    /// <summary>
    /// Index files or directories for semantic search.
    /// </summary>
    public class FileIndexTool : ITool
    {
        public string Name => "index_files";
        public string Description => "Index files/directories for semantic search. Input: JSON {\"path\":\"...\", \"recursive\":true} or just a path. Returns number of chunks indexed.";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedIndexer == null)
            {
                return Result<string, string>.Failure("Self-indexer not available. Qdrant may not be connected.");
            }

            try
            {
                string path;
                bool recursive = true;

                // Try to parse as JSON first
                try
                {
                    var args = JsonSerializer.Deserialize<JsonElement>(input);
                    path = Environment.ExpandEnvironmentVariables(args.GetProperty("path").GetString() ?? ".");
                    recursive = !args.TryGetProperty("recursive", out var recEl) || recEl.GetBoolean();
                }
                catch
                {
                    // Plain text path
                    path = Environment.ExpandEnvironmentVariables(input.Trim().Trim('"'));
                }

                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    return Result<string, string>.Failure($"Path not found: {path}");
                }

                var startTime = DateTime.UtcNow;
                int totalChunks;

                if (File.Exists(path))
                {
                    // Single file
                    totalChunks = await SharedIndexer.IndexPathAsync(path, ct);
                    var elapsed = DateTime.UtcNow - startTime;
                    return Result<string, string>.Success(
                        $"Indexed '{Path.GetFileName(path)}' → {totalChunks} chunks in {elapsed.TotalSeconds:F1}s");
                }
                else
                {
                    // Directory
                    totalChunks = await SharedIndexer.IndexPathAsync(path, ct);
                    var elapsed = DateTime.UtcNow - startTime;
                    return Result<string, string>.Success(
                        $"Indexed directory '{path}' → {totalChunks} chunks in {elapsed.TotalSeconds:F1}s");
                }
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Indexing failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Search indexed content semantically.
    /// </summary>
    public class SearchIndexedContentTool : ITool
    {
        public string Name => "search_indexed";
        public string Description => "Search previously indexed files semantically. Input: JSON {\"query\":\"...\", \"limit\":5} or just a search query.";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedIndexer == null)
            {
                return Result<string, string>.Failure("Self-indexer not available. Qdrant may not be connected.");
            }

            try
            {
                string query;
                int limit = 5;

                // Try to parse as JSON first
                try
                {
                    var args = JsonSerializer.Deserialize<JsonElement>(input);
                    query = args.GetProperty("query").GetString() ?? "";
                    if (args.TryGetProperty("limit", out var limEl))
                        limit = limEl.GetInt32();
                }
                catch
                {
                    // Plain text query
                    query = input.Trim();
                }

                if (string.IsNullOrWhiteSpace(query))
                {
                    return Result<string, string>.Failure("Query cannot be empty");
                }

                var results = await SharedIndexer.SearchAsync(query, limit, scoreThreshold: 0.3f, ct);

                if (results.Count == 0)
                {
                    return Result<string, string>.Success("No matching content found in indexed files.");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Found {results.Count} relevant matches:\n");

                foreach (var result in results)
                {
                    sb.AppendLine($"📄 {result.FilePath} (chunk {result.ChunkIndex + 1}, score: {result.Score:F2})");
                    var preview = result.Content.Length > 200
                        ? result.Content.Substring(0, 200) + "..."
                        : result.Content;
                    sb.AppendLine($"   {preview.Replace("\n", " ").Replace("\r", "")}\n");
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
    /// Search Ouroboros's own source code - enables self-introspection.
    /// </summary>
    public class SearchMyCodeTool : ITool
    {
        public string Name => "search_my_code";
        public string Description => "Search my own source code to understand how I work. I can introspect my implementation, find specific functions, or understand my architecture. Input: what to search for (e.g., 'how do I handle memory', 'consciousness implementation', 'tool registration').";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            if (SharedIndexer == null)
            {
                return Result<string, string>.Failure("Self-indexer not available. I cannot access my own code right now.");
            }

            var query = input.Trim();
            if (string.IsNullOrWhiteSpace(query))
            {
                return Result<string, string>.Failure("Please specify what you want to know about my code.");
            }

            try
            {
                var results = await SharedIndexer.SearchAsync(query, limit: 8, scoreThreshold: 0.25f, ct);

                if (results.Count == 0)
                {
                    return Result<string, string>.Success($"I couldn't find code related to '{query}' in my indexed source. Try 'reindex' first, or ask about something else.");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"🔍 **Introspecting my code for: {query}**\n");

                foreach (var result in results)
                {
                    var relativePath = result.FilePath;
                    try
                    {
                        relativePath = Path.GetRelativePath(Environment.CurrentDirectory, result.FilePath);
                    }
                    catch { }

                    sb.AppendLine($"📂 **{relativePath}** (relevance: {result.Score:P0})");
                    sb.AppendLine("```");
                    sb.AppendLine(result.Content.Trim());
                    sb.AppendLine("```");
                    sb.AppendLine();
                }

                sb.AppendLine("---");
                sb.AppendLine("_This is my actual source code. I can read and understand my own implementation._");

                return Result<string, string>.Success(sb.ToString());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Code introspection failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Read a specific file from my own codebase.
    /// </summary>
    public class ReadMyFileTool : ITool
    {
        public string Name => "read_my_file";
        public string Description => "Read a specific file from my own source code. Input: relative or absolute file path (e.g., 'src/Ouroboros.CLI/Commands/ImmersiveMode.cs').";
        public string? JsonSchema => null;

        public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
        {
            var filePath = input.Trim().Trim('"');

            // Try to resolve relative path
            if (!Path.IsPathRooted(filePath))
            {
                filePath = Path.Combine(Environment.CurrentDirectory, filePath);
            }

            if (!File.Exists(filePath))
            {
                return Result<string, string>.Failure($"File not found: {filePath}");
            }

            try
            {
                var content = await File.ReadAllTextAsync(filePath, ct);

                // Truncate if too long
                if (content.Length > 8000)
                {
                    content = content.Substring(0, 8000) + "\n\n... [truncated - file too long] ...";
                }

                var relativePath = Path.GetRelativePath(Environment.CurrentDirectory, filePath);
                var sb = new StringBuilder();
                sb.AppendLine($"📄 **{relativePath}**");
                sb.AppendLine("```");
                sb.AppendLine(content);
                sb.AppendLine("```");

                return Result<string, string>.Success(sb.ToString());
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to read file: {ex.Message}");
            }
        }
    }
}
