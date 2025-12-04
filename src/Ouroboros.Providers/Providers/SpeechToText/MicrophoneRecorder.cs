// <copyright file="MicrophoneRecorder.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Diagnostics;

namespace LangChainPipeline.Providers.SpeechToText;

/// <summary>
/// Cross-platform microphone recorder using system tools.
/// Uses ffmpeg/sox on all platforms for recording from microphone.
/// </summary>
public static class MicrophoneRecorder
{
    /// <summary>
    /// Records audio from the microphone for a specified duration.
    /// </summary>
    /// <param name="durationSeconds">Duration to record in seconds.</param>
    /// <param name="outputPath">Optional output path. If null, creates a temp file.</param>
    /// <param name="format">Audio format (wav, mp3). Default is wav.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Path to the recorded audio file.</returns>
    public static async Task<Result<string, string>> RecordAsync(
        int durationSeconds,
        string? outputPath = null,
        string format = "wav",
        CancellationToken ct = default)
    {
        string filePath = outputPath ?? Path.Combine(Path.GetTempPath(), $"mic_recording_{Guid.NewGuid()}.{format}");

        // Ensure directory exists
        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        ProcessStartInfo? startInfo = GetRecorderStartInfo(filePath, durationSeconds, format);
        if (startInfo == null)
        {
            return Result<string, string>.Failure(
                "No suitable audio recorder found. Please install ffmpeg or sox.");
        }

        try
        {
            using Process? process = Process.Start(startInfo);
            if (process == null)
            {
                return Result<string, string>.Failure("Failed to start audio recorder");
            }

            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0)
            {
                string error = await process.StandardError.ReadToEndAsync(ct);
                return Result<string, string>.Failure($"Recording failed: {error}");
            }

            if (!File.Exists(filePath))
            {
                return Result<string, string>.Failure("Recording failed: output file not created");
            }

            return Result<string, string>.Success(filePath);
        }
        catch (OperationCanceledException)
        {
            return Result<string, string>.Failure("Recording cancelled");
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Recording failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Records audio from the microphone until Enter is pressed.
    /// </summary>
    /// <param name="outputPath">Optional output path. If null, creates a temp file.</param>
    /// <param name="format">Audio format (wav, mp3). Default is wav.</param>
    /// <param name="maxDurationSeconds">Maximum recording duration (safety limit). Default 300 seconds.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Path to the recorded audio file.</returns>
    public static async Task<Result<string, string>> RecordUntilKeyPressAsync(
        string? outputPath = null,
        string format = "wav",
        int maxDurationSeconds = 300,
        CancellationToken ct = default)
    {
        string filePath = outputPath ?? Path.Combine(Path.GetTempPath(), $"mic_recording_{Guid.NewGuid()}.{format}");

        // Ensure directory exists
        string? dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        ProcessStartInfo? startInfo = GetRecorderStartInfo(filePath, maxDurationSeconds, format);
        if (startInfo == null)
        {
            return Result<string, string>.Failure(
                "No suitable audio recorder found. Please install ffmpeg or sox.");
        }

        try
        {
            using Process? process = Process.Start(startInfo);
            if (process == null)
            {
                return Result<string, string>.Failure("Failed to start audio recorder");
            }

            // Wait for key press or cancellation
            Console.WriteLine("🎤 Recording... Press Enter to stop.");

            Task keyTask = Task.Run(() =>
            {
                Console.ReadLine();
            }, ct);

            Task processTask = process.WaitForExitAsync(ct);

            // Wait for either key press or process exit
            await Task.WhenAny(keyTask, processTask);

            // If process is still running, kill it
            if (!process.HasExited)
            {
                process.Kill();
                await process.WaitForExitAsync(ct);
            }

            // Give a moment for file to be finalized
            await Task.Delay(100, ct);

            if (!File.Exists(filePath))
            {
                return Result<string, string>.Failure("Recording failed: output file not created");
            }

            return Result<string, string>.Success(filePath);
        }
        catch (OperationCanceledException)
        {
            return Result<string, string>.Failure("Recording cancelled");
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Recording failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Records audio and returns the audio data directly.
    /// </summary>
    /// <param name="durationSeconds">Duration to record in seconds.</param>
    /// <param name="format">Audio format. Default is wav.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The recorded audio data.</returns>
    public static async Task<Result<byte[], string>> RecordToMemoryAsync(
        int durationSeconds,
        string format = "wav",
        CancellationToken ct = default)
    {
        Result<string, string> recordResult = await RecordAsync(durationSeconds, null, format, ct);

        return await recordResult.Match<Task<Result<byte[], string>>>(
            async path =>
            {
                try
                {
                    byte[] data = await File.ReadAllBytesAsync(path, ct);

                    // Clean up temp file
                    try
                    {
                        File.Delete(path);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }

                    return Result<byte[], string>.Success(data);
                }
                catch (Exception ex)
                {
                    return Result<byte[], string>.Failure($"Failed to read recording: {ex.Message}");
                }
            },
            error => Task.FromResult(Result<byte[], string>.Failure(error)));
    }

    /// <summary>
    /// Checks if audio recording is available on this system.
    /// </summary>
    /// <returns>True if recording is possible.</returns>
    public static bool IsRecordingAvailable()
    {
        if (OperatingSystem.IsWindows())
        {
            return IsCommandAvailable("ffmpeg") || IsCommandAvailable("sox");
        }
        else if (OperatingSystem.IsMacOS())
        {
            return IsCommandAvailable("ffmpeg") || IsCommandAvailable("sox") || IsCommandAvailable("rec");
        }
        else if (OperatingSystem.IsLinux())
        {
            return IsCommandAvailable("ffmpeg") || IsCommandAvailable("arecord") || IsCommandAvailable("sox");
        }

        return false;
    }

    /// <summary>
    /// Gets information about available recording devices.
    /// </summary>
    /// <returns>Device information string.</returns>
    public static async Task<string> GetDeviceInfoAsync()
    {
        if (OperatingSystem.IsWindows())
        {
            return await RunCommandAsync("ffmpeg", "-list_devices true -f dshow -i dummy 2>&1");
        }
        else if (OperatingSystem.IsMacOS())
        {
            return await RunCommandAsync("ffmpeg", "-f avfoundation -list_devices true -i \"\" 2>&1");
        }
        else if (OperatingSystem.IsLinux())
        {
            return await RunCommandAsync("arecord", "-l");
        }

        return "Unable to list devices on this platform";
    }

    private static ProcessStartInfo? GetRecorderStartInfo(string outputPath, int durationSeconds, string format)
    {
        if (OperatingSystem.IsWindows())
        {
            // Use ffmpeg with DirectShow on Windows
            if (IsCommandAvailable("ffmpeg"))
            {
                // Get the default/active audio input device
                string? audioDevice = GetDefaultWindowsAudioDevice();
                if (string.IsNullOrEmpty(audioDevice))
                {
                    audioDevice = "Microphone"; // Fallback
                }

                return new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-f dshow -i audio=\"{audioDevice}\" -t {durationSeconds} -y \"{outputPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };
            }

            // Fallback to PowerShell with Windows.Media.Capture
            return new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -Command \"" +
                    $"Add-Type -AssemblyName System.Speech; " +
                    $"$recognizer = New-Object System.Speech.Recognition.SpeechRecognitionEngine; " +
                    $"$recognizer.SetInputToDefaultAudioDevice(); " +
                    $"Start-Sleep -Seconds {durationSeconds}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
            };
        }
        else if (OperatingSystem.IsMacOS())
        {
            // Use ffmpeg with AVFoundation on macOS
            if (IsCommandAvailable("ffmpeg"))
            {
                return new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"-f avfoundation -i \":0\" -t {durationSeconds} -y \"{outputPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };
            }

            // Fallback to sox/rec
            if (IsCommandAvailable("rec"))
            {
                return new ProcessStartInfo
                {
                    FileName = "rec",
                    Arguments = $"\"{outputPath}\" trim 0 {durationSeconds}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                };
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            // Use ffmpeg with ALSA/PulseAudio on Linux
            if (IsCommandAvailable("ffmpeg"))
            {
                // Try PulseAudio first, fall back to ALSA
                string input = IsCommandAvailable("pactl") ? "-f pulse -i default" : "-f alsa -i default";
                return new ProcessStartInfo
                {
                    FileName = "ffmpeg",
                    Arguments = $"{input} -t {durationSeconds} -y \"{outputPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                };
            }

            // Fallback to arecord
            if (IsCommandAvailable("arecord"))
            {
                return new ProcessStartInfo
                {
                    FileName = "arecord",
                    Arguments = $"-d {durationSeconds} -f cd \"{outputPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the default/active Windows audio input device.
    /// Prioritizes devices with "Microphone" in name, falls back to first audio device.
    /// </summary>
    private static string? GetDefaultWindowsAudioDevice()
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = "-list_devices true -f dshow -i dummy",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using Process? process = Process.Start(startInfo);
            if (process == null)
            {
                return null;
            }

            string error = process.StandardError.ReadToEnd();
            process.WaitForExit(5000);

            // Parse output to find audio devices
            // Format: [dshow @ ...] "Device Name" (audio)
            List<string> audioDevices = new List<string>();
            foreach (string line in error.Split('\n'))
            {
                if (line.Contains("(audio)"))
                {
                    int start = line.IndexOf('"');
                    int end = line.LastIndexOf('"');
                    if (start >= 0 && end > start)
                    {
                        audioDevices.Add(line.Substring(start + 1, end - start - 1));
                    }
                }
            }

            if (audioDevices.Count == 0)
            {
                return null;
            }

            // Prioritize device with "Microphone" or "Mikrofon" in name (active/primary mic)
            string? preferredDevice = audioDevices.FirstOrDefault(d =>
                d.Contains("Microphone", StringComparison.OrdinalIgnoreCase) ||
                d.Contains("Mikrofon", StringComparison.OrdinalIgnoreCase));

            return preferredDevice ?? audioDevices[0];
        }
        catch
        {
            // Ignore errors
        }

        return null;
    }

    private static bool IsCommandAvailable(string command)
    {
        try
        {
            string whichCommand = OperatingSystem.IsWindows() ? "where" : "which";
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = whichCommand,
                Arguments = command,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            };

            using Process? process = Process.Start(startInfo);
            process?.WaitForExit(1000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static async Task<string> RunCommandAsync(string command, string args)
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using Process? process = Process.Start(startInfo);
            if (process == null)
            {
                return "Failed to start process";
            }

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            return string.IsNullOrEmpty(output) ? error : output;
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }
}
