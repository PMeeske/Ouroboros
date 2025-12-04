// <copyright file="LocalWhisperService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LangChainPipeline.Providers.SpeechToText;

/// <summary>
/// Local Whisper-based speech-to-text service using whisper.cpp or faster-whisper.
/// Falls back to using the system's installed whisper command-line tool.
/// </summary>
public sealed class LocalWhisperService : ISpeechToTextService
{
    private readonly string whisperPath;
    private readonly string modelPath;
    private readonly string modelSize;

    /// <summary>
    /// Supported audio formats for local Whisper.
    /// </summary>
    private static readonly string[] SupportedAudioFormats =
    [
        ".wav", ".mp3", ".m4a", ".flac", ".ogg", ".opus",
    ];

    /// <inheritdoc/>
    public string ProviderName => "Local Whisper";

    /// <inheritdoc/>
    public IReadOnlyList<string> SupportedFormats => SupportedAudioFormats;

    /// <inheritdoc/>
    public long MaxFileSizeBytes => 500 * 1024 * 1024; // 500 MB for local processing

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalWhisperService"/> class.
    /// </summary>
    /// <param name="whisperPath">Path to whisper executable (e.g., whisper.cpp main or faster-whisper-cli).</param>
    /// <param name="modelPath">Path to the Whisper model file.</param>
    /// <param name="modelSize">Model size: tiny, base, small, medium, large.</param>
    public LocalWhisperService(
        string? whisperPath = null,
        string? modelPath = null,
        string modelSize = "small")
    {
        this.whisperPath = whisperPath ?? FindWhisperExecutable();
        this.modelPath = modelPath ?? string.Empty;
        this.modelSize = modelSize;
    }

    /// <inheritdoc/>
    public async Task<Result<TranscriptionResult, string>> TranscribeFileAsync(
        string filePath,
        TranscriptionOptions? options = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            return Result<TranscriptionResult, string>.Failure($"File not found: {filePath}");
        }

        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (!SupportedAudioFormats.Contains(extension))
        {
            return Result<TranscriptionResult, string>.Failure(
                $"Unsupported audio format: {extension}. Supported: {string.Join(", ", SupportedAudioFormats)}");
        }

        try
        {
            return await this.RunWhisperAsync(filePath, options, ct);
        }
        catch (Exception ex)
        {
            return Result<TranscriptionResult, string>.Failure($"Transcription failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<TranscriptionResult, string>> TranscribeStreamAsync(
        Stream audioStream,
        string fileName,
        TranscriptionOptions? options = null,
        CancellationToken ct = default)
    {
        // Save stream to temp file
        string tempPath = Path.Combine(Path.GetTempPath(), $"whisper_{Guid.NewGuid()}{Path.GetExtension(fileName)}");
        try
        {
            await using FileStream fileStream = File.Create(tempPath);
            await audioStream.CopyToAsync(fileStream, ct);
            await fileStream.FlushAsync(ct);
            fileStream.Close();

            return await this.TranscribeFileAsync(tempPath, options, ct);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                try
                {
                    File.Delete(tempPath);
                }
                catch
                {
                    // ignore cleanup errors
                }
            }
        }
    }

    /// <inheritdoc/>
    public async Task<Result<TranscriptionResult, string>> TranscribeBytesAsync(
        byte[] audioData,
        string fileName,
        TranscriptionOptions? options = null,
        CancellationToken ct = default)
    {
        await using MemoryStream stream = new MemoryStream(audioData);
        return await this.TranscribeStreamAsync(stream, fileName, options, ct);
    }

    /// <inheritdoc/>
    public async Task<Result<TranscriptionResult, string>> TranslateToEnglishAsync(
        string filePath,
        TranscriptionOptions? options = null,
        CancellationToken ct = default)
    {
        options ??= new TranscriptionOptions();

        // Add translation flag via prompt or special handling
        TranscriptionOptions translationOptions = options with { Prompt = "--translate " + (options.Prompt ?? string.Empty) };
        return await this.TranscribeFileAsync(filePath, translationOptions, ct);
    }

    /// <inheritdoc/>
    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            // First check for Python whisper (most common)
            if (IsPythonWhisperAvailable())
            {
                return Task.FromResult(true);
            }

            // Check if whisper executable exists
            if (File.Exists(this.whisperPath) || CanFindInPath(this.whisperPath))
            {
                // Optionally check if model exists
                if (!string.IsNullOrEmpty(this.modelPath) && !File.Exists(this.modelPath))
                {
                    return Task.FromResult(false);
                }

                return Task.FromResult(true);
            }

            return Task.FromResult(false);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private static string FindWhisperExecutable()
    {
        // First check for Python whisper via our wrapper script
        string scriptPath = FindWhisperPythonScript();
        if (!string.IsNullOrEmpty(scriptPath) && IsPythonWhisperAvailable())
        {
            return "python"; // Use python to run script
        }

        // Common whisper executable names
        string[] candidates =
        [
            "whisper",
            "whisper.exe",
            "whisper-cpp",
            "main", // whisper.cpp default
            "faster-whisper",
            "faster-whisper.exe",
        ];

        foreach (string candidate in candidates)
        {
            if (CanFindInPath(candidate))
            {
                return candidate;
            }
        }

        // Check if Python whisper is available
        if (IsPythonWhisperAvailable())
        {
            return "python";
        }

        // Return default, will fail gracefully if not found
        return "whisper";
    }

    private static string? FindWhisperPythonScript()
    {
        // Look for the whisper_transcribe.py script in common locations
        string[] scriptLocations =
        [
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts", "whisper_transcribe.py"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "scripts", "whisper_transcribe.py"),
            Path.Combine(Environment.CurrentDirectory, "scripts", "whisper_transcribe.py"),
        ];

        foreach (string path in scriptLocations)
        {
            string fullPath = Path.GetFullPath(path);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }

        return null;
    }

    private static bool IsPythonWhisperAvailable()
    {
        try
        {
            // Check if Python is available and whisper is installed
            string python = FindPythonExecutable();
            if (string.IsNullOrEmpty(python))
            {
                return false;
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = python,
                Arguments = "-c \"import whisper; print('ok')\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using Process? process = Process.Start(startInfo);
            if (process == null)
            {
                return false;
            }

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(5000);

            return process.ExitCode == 0 && output.Contains("ok");
        }
        catch
        {
            return false;
        }
    }

    private static string FindPythonExecutable()
    {
        // Check common Python locations
        string[] pythonCandidates = ["python", "python3", "py"];

        foreach (string candidate in pythonCandidates)
        {
            if (CanFindInPath(candidate))
            {
                return candidate;
            }
        }

        // Check Windows-specific Python installation paths
        if (OperatingSystem.IsWindows())
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] windowsPaths =
            [
                Path.Combine(localAppData, "Programs", "Python", "Python312", "python.exe"),
                Path.Combine(localAppData, "Programs", "Python", "Python311", "python.exe"),
                Path.Combine(localAppData, "Programs", "Python", "Python310", "python.exe"),
            ];

            foreach (string path in windowsPaths)
            {
                if (File.Exists(path))
                {
                    return path;
                }
            }
        }

        return string.Empty;
    }

    private static bool CanFindInPath(string executable)
    {
        try
        {
            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            string[] paths = pathEnv.Split(Path.PathSeparator);

            foreach (string path in paths)
            {
                string fullPath = Path.Combine(path, executable);
                if (File.Exists(fullPath))
                {
                    return true;
                }

                // On Windows, also check with .exe extension
                if (OperatingSystem.IsWindows() && File.Exists(fullPath + ".exe"))
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static Result<TranscriptionResult, string> ParseWhisperOutput(string output, string format)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return Result<TranscriptionResult, string>.Failure("Whisper produced no output");
        }

        if (format == "json" || format == "verbose_json")
        {
            try
            {
                // Try to parse JSON output
                int jsonStart = output.IndexOf('{');
                int jsonEnd = output.LastIndexOf('}');
                if (jsonStart >= 0 && jsonEnd > jsonStart)
                {
                    string json = output[jsonStart..(jsonEnd + 1)];
                    WhisperJsonOutput? result = JsonSerializer.Deserialize<WhisperJsonOutput>(json);
                    if (result != null)
                    {
                        List<TranscriptionSegment>? segments = result.Segments?
                            .Select(s => new TranscriptionSegment(
                                s.Text ?? string.Empty,
                                s.Start,
                                s.End,
                                s.Confidence))
                            .ToList();

                        return Result<TranscriptionResult, string>.Success(
                            new TranscriptionResult(
                                result.Text ?? string.Empty,
                                result.Language,
                                null,
                                segments));
                    }
                }
            }
            catch
            {
                // Fall through to plain text parsing
            }
        }

        // Plain text output
        return Result<TranscriptionResult, string>.Success(
            new TranscriptionResult(output.Trim()));
    }

    private async Task<Result<TranscriptionResult, string>> RunWhisperAsync(
        string filePath,
        TranscriptionOptions? options,
        CancellationToken ct)
    {
        options ??= new TranscriptionOptions();

        // Prefer Python whisper if available
        if (IsPythonWhisperAvailable())
        {
            return await this.RunPythonWhisperAsync(filePath, options, ct);
        }

        // Fall back to native whisper CLI
        string args = this.BuildWhisperArguments(filePath, options);

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = this.whisperPath,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using Process process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();

            Task<string> outputTask = process.StandardOutput.ReadToEndAsync(ct);
            Task<string> errorTask = process.StandardError.ReadToEndAsync(ct);

            await process.WaitForExitAsync(ct);

            string output = await outputTask;
            string error = await errorTask;

            if (process.ExitCode != 0)
            {
                return Result<TranscriptionResult, string>.Failure(
                    $"Whisper exited with code {process.ExitCode}: {error}");
            }

            return ParseWhisperOutput(output, options.ResponseFormat);
        }
        catch (Exception ex)
        {
            return Result<TranscriptionResult, string>.Failure($"Failed to run Whisper: {ex.Message}");
        }
    }

    private async Task<Result<TranscriptionResult, string>> RunPythonWhisperAsync(
        string filePath,
        TranscriptionOptions options,
        CancellationToken ct)
    {
        string python = FindPythonExecutable();
        string? scriptPath = FindWhisperPythonScript();

        List<string> args = new List<string>();

        if (!string.IsNullOrEmpty(scriptPath))
        {
            // Use our wrapper script
            args.Add($"\"{scriptPath}\"");
            args.Add($"\"{filePath}\"");
            args.Add($"--model {this.modelSize}");
            args.Add("--output json");

            if (!string.IsNullOrEmpty(options.Language))
            {
                args.Add($"--language {options.Language}");
            }

            if (options.Prompt?.Contains("--translate") == true)
            {
                args.Add("--task translate");
            }
        }
        else
        {
            // Use inline Python command
            string task = options.Prompt?.Contains("--translate") == true ? "translate" : "transcribe";
            string langArg = string.IsNullOrEmpty(options.Language) ? "" : $", language='{options.Language}'";

            string pythonCode = $@"
import json
import whisper
model = whisper.load_model('{this.modelSize}')
result = model.transcribe(r'{filePath.Replace("'", @"\'")}''{langArg}, task='{task}', verbose=False)
output = {{
    'text': result['text'].strip(),
    'language': result.get('language', 'unknown'),
    'segments': [
        {{'start': s['start'], 'end': s['end'], 'text': s['text'].strip()}}
        for s in result.get('segments', [])
    ]
}}
print(json.dumps(output, ensure_ascii=False))
";
            args.Add("-c");
            args.Add($"\"{pythonCode.Replace("\"", "\\\"").Replace("\n", " ")}\"");
        }

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = python,
            Arguments = string.Join(" ", args),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using Process process = new Process { StartInfo = startInfo };

        try
        {
            process.Start();

            Task<string> outputTask = process.StandardOutput.ReadToEndAsync(ct);
            Task<string> errorTask = process.StandardError.ReadToEndAsync(ct);

            await process.WaitForExitAsync(ct);

            string output = await outputTask;
            string error = await errorTask;

            // Python whisper prints warnings to stderr, ignore them if we got output
            if (string.IsNullOrWhiteSpace(output) && process.ExitCode != 0)
            {
                return Result<TranscriptionResult, string>.Failure(
                    $"Python Whisper failed: {error}");
            }

            return ParseWhisperOutput(output, "json");
        }
        catch (Exception ex)
        {
            return Result<TranscriptionResult, string>.Failure($"Failed to run Python Whisper: {ex.Message}");
        }
    }

    private string BuildWhisperArguments(string filePath, TranscriptionOptions options)
    {
        List<string> args = new List<string>();

        // Add input file
        args.Add($"\"{filePath}\"");

        // Add model path or size
        if (!string.IsNullOrEmpty(this.modelPath))
        {
            args.Add($"--model \"{this.modelPath}\"");
        }
        else
        {
            args.Add($"--model {this.modelSize}");
        }

        // Add language
        if (!string.IsNullOrEmpty(options.Language))
        {
            args.Add($"--language {options.Language}");
        }

        // Add output format
        if (options.ResponseFormat == "json" || options.ResponseFormat == "verbose_json")
        {
            args.Add("--output_format json");
        }

        // Check for translation flag in prompt
        if (options.Prompt?.Contains("--translate") == true)
        {
            args.Add("--task translate");
        }

        // Add actual prompt (without special flags)
        string? cleanPrompt = options.Prompt?.Replace("--translate", string.Empty).Trim();
        if (!string.IsNullOrEmpty(cleanPrompt))
        {
            args.Add($"--initial_prompt \"{cleanPrompt}\"");
        }

        return string.Join(" ", args);
    }

    private sealed class WhisperJsonOutput
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("language")]
        public string? Language { get; set; }

        [JsonPropertyName("segments")]
        public List<WhisperSegmentOutput>? Segments { get; set; }
    }

    private sealed class WhisperSegmentOutput
    {
        [JsonPropertyName("start")]
        public double Start { get; set; }

        [JsonPropertyName("end")]
        public double End { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("confidence")]
        public double? Confidence { get; set; }
    }
}
