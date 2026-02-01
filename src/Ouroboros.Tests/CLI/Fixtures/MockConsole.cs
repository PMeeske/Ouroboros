// <copyright file="MockConsole.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Tests.CLI.Fixtures;

/// <summary>
/// Mock console for capturing and simulating console I/O in tests.
/// </summary>
public class MockConsole : IDisposable
{
    private readonly StringWriter _outputWriter;
    private readonly StringWriter _errorWriter;
    private readonly StringReader _inputReader;
    private readonly TextWriter _originalOut;
    private readonly TextWriter _originalError;
    private readonly TextReader _originalIn;

    /// <summary>
    /// Initializes a new instance of the <see cref="MockConsole"/> class.
    /// </summary>
    /// <param name="simulatedInput">Optional simulated user input.</param>
    public MockConsole(string? simulatedInput = null)
    {
        _outputWriter = new StringWriter();
        _errorWriter = new StringWriter();
        _inputReader = new StringReader(simulatedInput ?? string.Empty);

        _originalOut = Console.Out;
        _originalError = Console.Error;
        _originalIn = Console.In;
    }

    /// <summary>
    /// Gets the captured standard output.
    /// </summary>
    public string Output => _outputWriter.ToString();

    /// <summary>
    /// Gets the captured standard error.
    /// </summary>
    public string Error => _errorWriter.ToString();

    /// <summary>
    /// Redirects console I/O to the mock console.
    /// </summary>
    public void Redirect()
    {
        Console.SetOut(_outputWriter);
        Console.SetError(_errorWriter);
        Console.SetIn(_inputReader);
    }

    /// <summary>
    /// Restores the original console I/O.
    /// </summary>
    public void Restore()
    {
        Console.SetOut(_originalOut);
        Console.SetError(_originalError);
        Console.SetIn(_originalIn);
    }

    /// <summary>
    /// Clears all captured output and error.
    /// </summary>
    public void Clear()
    {
        _outputWriter.GetStringBuilder().Clear();
        _errorWriter.GetStringBuilder().Clear();
    }

    /// <summary>
    /// Disposes the mock console and restores original I/O.
    /// </summary>
    public void Dispose()
    {
        Restore();
        _outputWriter.Dispose();
        _errorWriter.Dispose();
        _inputReader.Dispose();
        GC.SuppressFinalize(this);
    }
}
