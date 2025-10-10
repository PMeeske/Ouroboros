using System.Diagnostics;
using System.Text;

namespace LangChainPipeline.Tools.MeTTa;

/// <summary>
/// Subprocess-based MeTTa engine implementation that communicates with metta-stdlib
/// through standard input/output.
/// </summary>
public sealed class SubprocessMeTTaEngine : IMeTTaEngine
{
    private readonly Process? _process;
    private readonly StreamWriter? _stdin;
    private readonly StreamReader? _stdout;
    private readonly StreamReader? _stderr;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _disposed;

    /// <summary>
    /// Creates a new subprocess-based MeTTa engine.
    /// </summary>
    /// <param name="mettaExecutablePath">Path to the MeTTa executable (defaults to 'metta' in PATH).</param>
    public SubprocessMeTTaEngine(string? mettaExecutablePath = null)
    {
        var execPath = mettaExecutablePath ?? "metta";

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = execPath,
                Arguments = "--repl",
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            _process = Process.Start(startInfo);

            if (_process != null)
            {
                _stdin = _process.StandardInput;
                _stdout = _process.StandardOutput;
                _stderr = _process.StandardError;
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
        if (_process == null || _stdin == null || _stdout == null)
        {
            return Result<string, string>.Failure("MeTTa engine is not initialized. Ensure metta executable is in PATH.");
        }

        await _lock.WaitAsync(ct);
        try
        {
            // Send query to MeTTa
            await _stdin.WriteLineAsync(query.AsMemory(), ct);
            await _stdin.FlushAsync();

            // Read response with timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var response = await _stdout.ReadLineAsync();

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
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<Result<Unit, string>> AddFactAsync(string fact, CancellationToken ct = default)
    {
        if (_process == null || _stdin == null)
        {
            return Result<Unit, string>.Failure("MeTTa engine is not initialized");
        }

        await _lock.WaitAsync(ct);
        try
        {
            // Add fact using MeTTa assertion syntax
            var command = $"!(add-atom &self {fact})";
            await _stdin.WriteLineAsync(command.AsMemory(), ct);
            await _stdin.FlushAsync();

            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Failed to add fact: {ex.Message}");
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<Result<string, string>> ApplyRuleAsync(string rule, CancellationToken ct = default)
    {
        if (_process == null || _stdin == null || _stdout == null)
        {
            return Result<string, string>.Failure("MeTTa engine is not initialized");
        }

        await _lock.WaitAsync(ct);
        try
        {
            // Apply rule and get result
            await _stdin.WriteLineAsync(rule.AsMemory(), ct);
            await _stdin.FlushAsync();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(10));

            var response = await _stdout.ReadLineAsync();

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
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public async Task<Result<bool, string>> VerifyPlanAsync(string plan, CancellationToken ct = default)
    {
        // Use MeTTa query to verify plan
        var query = $"!(match &self (verify-plan {plan}) $result)";
        var result = await ExecuteQueryAsync(query, ct);

        return result.Match(
            success => success.Contains("True") || success.Contains("true")
                ? Result<bool, string>.Success(true)
                : Result<bool, string>.Success(false),
            error => Result<bool, string>.Failure(error)
        );
    }

    /// <inheritdoc />
    public async Task<Result<Unit, string>> ResetAsync(CancellationToken ct = default)
    {
        if (_process == null || _stdin == null)
        {
            return Result<Unit, string>.Failure("MeTTa engine is not initialized");
        }

        await _lock.WaitAsync(ct);
        try
        {
            // Clear the space
            var command = "!(clear-space &self)";
            await _stdin.WriteLineAsync(command.AsMemory(), ct);
            await _stdin.FlushAsync();

            return Result<Unit, string>.Success(Unit.Value);
        }
        catch (Exception ex)
        {
            return Result<Unit, string>.Failure($"Failed to reset: {ex.Message}");
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed) return;

        _lock.Wait();
        try
        {
            _stdin?.Close();
            _stdout?.Close();
            _stderr?.Close();

            if (_process != null && !_process.HasExited)
            {
                _process.Kill();
                _process.WaitForExit(1000);
            }

            _process?.Dispose();
            _disposed = true;
        }
        finally
        {
            _lock.Release();
            _lock.Dispose();
        }
    }
}
