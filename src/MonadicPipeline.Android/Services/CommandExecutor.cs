using System.Diagnostics;
using System.Text;

namespace MonadicPipeline.Android.Services;

/// <summary>
/// Executes native Android shell commands
/// </summary>
public class CommandExecutor
{
    private readonly bool _requiresRoot;

    /// <summary>
    /// Initializes a new instance of the <see cref="CommandExecutor"/> class.
    /// </summary>
    /// <param name="requiresRoot">Whether commands require root access</param>
    public CommandExecutor(bool requiresRoot = false)
    {
        _requiresRoot = requiresRoot;
    }

    /// <summary>
    /// Execute a shell command and return the output
    /// </summary>
    /// <param name="command">The command to execute</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Command execution result</returns>
    public async Task<CommandResult> ExecuteAsync(string command, CancellationToken cancellationToken = default)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = _requiresRoot ? "su" : "sh",
                Arguments = _requiresRoot ? $"-c \"{command}\"" : $"-c \"{command}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processInfo };
            var output = new StringBuilder();
            var error = new StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    output.AppendLine(e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    error.AppendLine(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);

            return new CommandResult
            {
                ExitCode = process.ExitCode,
                Output = output.ToString(),
                Error = error.ToString(),
                Success = process.ExitCode == 0
            };
        }
        catch (Exception ex)
        {
            return new CommandResult
            {
                ExitCode = -1,
                Output = string.Empty,
                Error = $"Failed to execute command: {ex.Message}",
                Success = false
            };
        }
    }

    /// <summary>
    /// Execute a command with streaming output
    /// </summary>
    /// <param name="command">The command to execute</param>
    /// <param name="outputHandler">Handler for output lines</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Command execution result</returns>
    public async Task<CommandResult> ExecuteStreamingAsync(
        string command,
        Action<string> outputHandler,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = _requiresRoot ? "su" : "sh",
                Arguments = _requiresRoot ? $"-c \"{command}\"" : $"-c \"{command}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processInfo };
            var output = new StringBuilder();
            var error = new StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    output.AppendLine(e.Data);
                    outputHandler?.Invoke(e.Data);
                }
            };

            process.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    error.AppendLine(e.Data);
                    outputHandler?.Invoke($"ERROR: {e.Data}");
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            await process.WaitForExitAsync(cancellationToken);

            return new CommandResult
            {
                ExitCode = process.ExitCode,
                Output = output.ToString(),
                Error = error.ToString(),
                Success = process.ExitCode == 0
            };
        }
        catch (Exception ex)
        {
            return new CommandResult
            {
                ExitCode = -1,
                Output = string.Empty,
                Error = $"Failed to execute command: {ex.Message}",
                Success = false
            };
        }
    }

    /// <summary>
    /// Validate a command before execution
    /// </summary>
    /// <param name="command">The command to validate</param>
    /// <returns>Validation result</returns>
    public ValidationResult ValidateCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return new ValidationResult
            {
                IsValid = false,
                Message = "Command cannot be empty"
            };
        }

        // Check for potentially dangerous commands
        var dangerousCommands = new[] { "rm -rf /", "dd if=/dev/zero", "mkfs", "format" };
        foreach (var dangerous in dangerousCommands)
        {
            if (command.Contains(dangerous, StringComparison.OrdinalIgnoreCase))
            {
                return new ValidationResult
                {
                    IsValid = false,
                    Message = $"Potentially dangerous command detected: {dangerous}"
                };
            }
        }

        return new ValidationResult
        {
            IsValid = true,
            Message = "Command is valid"
        };
    }
}

/// <summary>
/// Result of command execution
/// </summary>
public class CommandResult
{
    /// <summary>
    /// Gets or sets the exit code
    /// </summary>
    public int ExitCode { get; set; }

    /// <summary>
    /// Gets or sets the standard output
    /// </summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the standard error
    /// </summary>
    public string Error { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets a value indicating whether the command succeeded
    /// </summary>
    public bool Success { get; set; } 
}

/// <summary>
/// Result of command validation
/// </summary>
public class ValidationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether the command is valid
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the validation message
    /// </summary>
    public string Message { get; set; } = string.Empty;
}
