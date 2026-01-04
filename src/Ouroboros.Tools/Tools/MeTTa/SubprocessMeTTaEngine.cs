// <copyright file="SubprocessMeTTaEngine.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tools.MeTTa;

using System.Diagnostics;
using System.Text;

/// <summary>
/// Subprocess-based MeTTa engine implementation that communicates with metta-stdlib
/// through standard input/output.
/// </summary>
public sealed class SubprocessMeTTaEngine : IMeTTaEngine
{
    private readonly Process? process;
    private readonly StreamWriter? stdin;
    private readonly StreamReader? stdout;
    private readonly StreamReader? stderr;
    private readonly SemaphoreSlim @lock = new(1, 1);
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubprocessMeTTaEngine"/> class.
    /// Creates a new subprocess-based MeTTa engine.
    /// </summary>
    /// <param name="mettaExecutablePath">Path to the MeTTa executable (defaults to 'metta' in PATH).</param>
    public SubprocessMeTTaEngine(string? mettaExecutablePath = null)
    {
        // If path is provided, use it (legacy/local override).
        // If not, default to docker execution using a Python REPL wrapper
        // because the 'metta' binary in the container may not support --repl or interactive mode correctly via pipe.
        bool useDocker = mettaExecutablePath == null;
        string fileName = mettaExecutablePath ?? "docker";
        
        // Python script to act as a REPL for MeTTa with motto and ollama_agent support
        string pythonScript = 
            "import sys\n" +
            "import os\n" +
            "from hyperon import MeTTa\n" +
            "sys.stderr.write('MeTTa REPL Started\\n')\n" +
            "sys.stderr.flush()\n" +
            "m = MeTTa()\n" +
            "m.run('!(import! &self motto)')\n" +
            "m.run('!(import! &self ollama_agent)')\n" +
            "while True:\n" +
            "    try:\n" +
            "        line = sys.stdin.readline()\n" +
            "        if not line: break\n" +
            "        line = line.strip()\n" +
            "        if not line: continue\n" +
            "        if not line.startswith('!'): line = '!' + line\n" +
            "        result = m.run(line)\n" +
            "        print(result)\n" +
            "        sys.stdout.flush()\n" +
            "    except Exception as e:\n" +
            "        print(f'Error: {e}')\n" +
            "        sys.stdout.flush()";

        // Escape double quotes for the command line argument
        string escapedScript = pythonScript.Replace("\"", "\\\"");

        string currentDir = Directory.GetCurrentDirectory();
        
        // Pass environment variables to Docker container
        StringBuilder envArgs = new StringBuilder();
        string[] envVarsToPass = { "OPENAI_API_KEY", "OPENAI_API_BASE", "OPENAI_API_MODEL" };
        foreach (var envVar in envVarsToPass)
        {
            string? value = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrEmpty(value))
            {
                // If running on Windows, we might need to be careful with quoting, but -e VAR=VAL usually works.
                // Better to use -e VAR (pass-through) if the value is in the host env.
                // Docker supports -e VAR to take from host.
                envArgs.Append($" -e {envVar}");
            }
        }

        string arguments = useDocker 
            ? $"run --rm -i --add-host=host.docker.internal:host-gateway {envArgs} my-metta-agent python3 -c \"{escapedScript}\"" 
            : "--repl";

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            this.process = Process.Start(startInfo);

            if (this.process != null)
            {
                this.stdin = this.process.StandardInput;
                this.stdout = this.process.StandardOutput;
                this.stderr = this.process.StandardError;
            }
        }
        catch (Exception ex)
        {
            // If MeTTa executable is not found, we continue with null process
            // Methods will return appropriate errors when called
            Console.WriteLine($"Warning: Could not start MeTTa subprocess: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<Result<string, string>> ExecuteQueryAsync(string query, CancellationToken ct = default)
    {
        if (this.process == null || this.stdin == null || this.stdout == null)
        {
            return Result<string, string>.Failure("MeTTa engine is not initialized. Ensure metta executable is in PATH.");
        }

        await this.@lock.WaitAsync(ct);
        try
        {
            // Send query to MeTTa
            await this.stdin.WriteLineAsync(query.AsMemory(), ct);
            await this.stdin.FlushAsync();

            // Read response with timeout
            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            string? response = await this.stdout.ReadLineAsync();

            if (string.IsNullOrEmpty(response))
            {
                return Result<string, string>.Failure("No response from MeTTa engine");
            }

            return Result<string, string>.Success(response);
        }
        catch (OperationCanceledException)
        {
            return Result<string, string>.Failure("Query execution timed out");
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Query execution failed: {ex.Message}");
        }
        finally
        {
            this.@lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<Result<Unit, string>> AddFactAsync(string fact, CancellationToken ct = default)
    {
        if (this.process == null || this.stdin == null)
        {
            return Result<Unit, string>.Failure("MeTTa engine is not initialized");
        }

        await this.@lock.WaitAsync(ct);
        try
        {
            // Add fact using MeTTa assertion syntax
            string command = $"!(add-atom &self {fact})";
            await this.stdin.WriteLineAsync(command.AsMemory(), ct);
            await this.stdin.FlushAsync();

            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Failed to add fact: {ex.Message}");
        }
        finally
        {
            this.@lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<Result<string, string>> ApplyRuleAsync(string rule, CancellationToken ct = default)
    {
        if (this.process == null || this.stdin == null || this.stdout == null)
        {
            return Result<string, string>.Failure("MeTTa engine is not initialized");
        }

        await this.@lock.WaitAsync(ct);
        try
        {
            // Apply rule and get result
            await this.stdin.WriteLineAsync(rule.AsMemory(), ct);
            await this.stdin.FlushAsync();

            using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            string? response = await this.stdout.ReadLineAsync();

            return !string.IsNullOrEmpty(response)
                ? Result<string, string>.Success(response)
                : Result<string, string>.Failure("No response from rule application");
        }
        catch (OperationCanceledException)
        {
            return Result<string, string>.Failure("Rule application timed out");
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Rule application failed: {ex.Message}");
        }
        finally
        {
            this.@lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool, string>> VerifyPlanAsync(string plan, CancellationToken ct = default)
    {
        // Use MeTTa query to verify plan
        string query = $"!(match &self (verify-plan {plan}) $result)";
        Result<string, string> result = await this.ExecuteQueryAsync(query, ct);

        return result.Match(
            success => success.Contains("True") || success.Contains("true")
                ? Result<bool, string>.Success(true)
                : Result<bool, string>.Success(false),
            error => Result<bool, string>.Failure(error));
    }

    /// <inheritdoc />
    public async Task<Result<Unit, string>> ResetAsync(CancellationToken ct = default)
    {
        if (this.process == null || this.stdin == null)
        {
            return Result<Unit, string>.Failure("MeTTa engine is not initialized");
        }

        await this.@lock.WaitAsync(ct);
        try
        {
            // Clear the space
            string command = "!(clear-space &self)";
            await this.stdin.WriteLineAsync(command.AsMemory(), ct);
            await this.stdin.FlushAsync();

            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Failed to reset: {ex.Message}");
        }
        finally
        {
            this.@lock.Release();
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Uses a timeout on semaphore acquisition to prevent deadlocks during disposal.
    /// This is necessary because the interface is IDisposable (synchronous), not IAsyncDisposable.
    /// </remarks>
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        // Use timeout instead of blocking Wait() to prevent potential deadlocks
        // If we can't acquire the lock in 5 seconds, proceed anyway as we're disposing
        if (!this.@lock.Wait(TimeSpan.FromSeconds(5)))
        {
            // Log warning if needed - couldn't acquire lock but continuing disposal
        }

        try
        {
            this.stdin?.Close();
            this.stdout?.Close();
            this.stderr?.Close();

            if (this.process != null && !this.process.HasExited)
            {
                this.process.Kill();
                this.process.WaitForExit(1000);
            }

            this.process?.Dispose();
            this.disposed = true;
        }
        finally
        {
            this.@lock.Release();
            this.@lock.Dispose();
        }
    }
}
