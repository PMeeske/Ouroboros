// <copyright file="SubprocessMeTTaEngine.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Tools.MeTTa;

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
        string execPath = mettaExecutablePath ?? "metta";

        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = execPath,
                Arguments = "--repl",
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
            cts.CancelAfter(TimeSpan.FromSeconds(10));

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
    public void Dispose()
    {
        if (this.disposed)
        {
            return;
        }

        this.@lock.Wait();
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
