// <copyright file="CliTestHarness.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Diagnostics;
using Ouroboros.CLI.Commands;
using Ouroboros.Options;

namespace Ouroboros.Tests.CLI.Fixtures;

/// <summary>
/// Test harness for executing CLI commands with captured I/O.
/// </summary>
public class CliTestHarness : IDisposable
{
    private readonly MockConsole _console;

    /// <summary>
    /// Initializes a new instance of the <see cref="CliTestHarness"/> class.
    /// </summary>
    /// <param name="simulatedInput">Optional simulated user input.</param>
    public CliTestHarness(string? simulatedInput = null)
    {
        _console = new MockConsole(simulatedInput);
    }

    /// <summary>
    /// Gets the captured console output.
    /// </summary>
    public string Output => _console.Output;

    /// <summary>
    /// Gets the captured console error.
    /// </summary>
    public string Error => _console.Error;

    /// <summary>
    /// Executes an Ask command with the given options.
    /// </summary>
    public async Task<CliResult> ExecuteAskAsync(AskOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        _console.Redirect();

        try
        {
            await AskCommands.RunAskAsync(options);
            stopwatch.Stop();

            return new CliResult
            {
                Output = _console.Output,
                Error = _console.Error,
                ExitCode = 0,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new CliResult
            {
                Output = _console.Output,
                Error = _console.Error + ex.Message,
                ExitCode = 1,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        finally
        {
            _console.Restore();
        }
    }

    /// <summary>
    /// Executes a Pipeline command with the given options.
    /// </summary>
    public async Task<CliResult> ExecutePipelineAsync(PipelineOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        _console.Redirect();

        try
        {
            await PipelineCommands.RunPipelineAsync(options);
            stopwatch.Stop();

            return new CliResult
            {
                Output = _console.Output,
                Error = _console.Error,
                ExitCode = 0,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return new CliResult
            {
                Output = _console.Output,
                Error = _console.Error + ex.Message,
                ExitCode = 1,
                ExecutionTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        finally
        {
            _console.Restore();
        }
    }

    /// <summary>
    /// Clears all captured output.
    /// </summary>
    public void Clear()
    {
        _console.Clear();
    }

    /// <summary>
    /// Disposes the test harness.
    /// </summary>
    public void Dispose()
    {
        _console.Dispose();
        GC.SuppressFinalize(this);
    }
}
