// <copyright file="AudioPlayer.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Diagnostics;

namespace Ouroboros.Providers.TextToSpeech;

/// <summary>
/// Cross-platform audio player for playing audio data directly.
/// Uses system audio players (Windows Media Player, afplay on macOS, mpv/ffplay on Linux).
/// </summary>
public static class AudioPlayer
{
    /// <summary>
    /// Plays audio data directly without requiring a permanent file.
    /// </summary>
    /// <param name="audioData">The audio data to play.</param>
    /// <param name="format">The audio format (mp3, wav, etc.).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result indicating success or failure.</returns>
    public static async Task<Result<bool, string>> PlayAsync(
        byte[] audioData,
        string format = "mp3",
        CancellationToken ct = default)
    {
        // Create a temporary file for playback
        string tempFile = Path.Combine(Path.GetTempPath(), $"tts_playback_{Guid.NewGuid()}.{format}");

        try
        {
            await File.WriteAllBytesAsync(tempFile, audioData, ct);
            return await PlayFileAsync(tempFile, ct);
        }
        finally
        {
            // Clean up temp file after a delay to ensure playback started
            _ = Task.Run(async () =>
            {
                await Task.Delay(30000, CancellationToken.None); // Wait 30 seconds
                try
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            });
        }
    }

    /// <summary>
    /// Plays an audio file using the system's default audio player.
    /// </summary>
    /// <param name="filePath">Path to the audio file.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result indicating success or failure.</returns>
    public static async Task<Result<bool, string>> PlayFileAsync(
        string filePath,
        CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            return Result<bool, string>.Failure($"File not found: {filePath}");
        }

        try
        {
            ProcessStartInfo? startInfo = GetPlayerStartInfo(filePath);
            if (startInfo == null)
            {
                return Result<bool, string>.Failure("No suitable audio player found on this system");
            }

            using Process? process = Process.Start(startInfo);
            if (process == null)
            {
                return Result<bool, string>.Failure("Failed to start audio player");
            }

            await process.WaitForExitAsync(ct);
            return Result<bool, string>.Success(true);
        }
        catch (OperationCanceledException)
        {
            return Result<bool, string>.Failure("Playback cancelled");
        }
        catch (Exception ex)
        {
            return Result<bool, string>.Failure($"Playback failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Plays audio data and waits for playback to complete.
    /// </summary>
    /// <param name="speechResult">The speech result to play.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result indicating success or failure.</returns>
    public static Task<Result<bool, string>> PlayAsync(
        SpeechResult speechResult,
        CancellationToken ct = default)
    {
        return PlayAsync(speechResult.AudioData, speechResult.Format, ct);
    }

    private static ProcessStartInfo? GetPlayerStartInfo(string filePath)
    {
        if (OperatingSystem.IsWindows())
        {
            // Use PowerShell with System.Media.SoundPlayer for WAV (fast, synchronous)
            // For other formats, use Windows Media Player
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext == ".wav")
            {
                // SoundPlayer is much faster for WAV files
                return new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -Command \"Add-Type -AssemblyName System.Windows.Forms; $p = New-Object System.Media.SoundPlayer('{filePath.Replace("'", "''")}'); $p.PlaySync(); $p.Dispose()\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
            }
            else
            {
                // For MP3 and other formats, use a simpler approach
                return new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -Command \"Add-Type -AssemblyName presentationCore; $p = New-Object System.Windows.Media.MediaPlayer; $p.Open([Uri]::new('{filePath.Replace("'", "''")}')); $p.Play(); while($p.NaturalDuration.HasTimeSpan -eq $false){{Start-Sleep -Milliseconds 100}}; Start-Sleep -Milliseconds ([int]$p.NaturalDuration.TimeSpan.TotalMilliseconds + 100); $p.Close()\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            // Use afplay on macOS
            return new ProcessStartInfo
            {
                FileName = "afplay",
                Arguments = $"\"{filePath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
            };
        }
        else if (OperatingSystem.IsLinux())
        {
            // Try mpv, then ffplay, then aplay for wav
            string[] players = ["mpv", "ffplay", "paplay", "aplay"];

            foreach (string player in players)
            {
                if (IsCommandAvailable(player))
                {
                    string args = player switch
                    {
                        "mpv" => $"--no-video \"{filePath}\"",
                        "ffplay" => $"-nodisp -autoexit \"{filePath}\"",
                        "paplay" or "aplay" => $"\"{filePath}\"",
                        _ => $"\"{filePath}\"",
                    };

                    return new ProcessStartInfo
                    {
                        FileName = player,
                        Arguments = args,
                        UseShellExecute = false,
                        CreateNoWindow = true,
                    };
                }
            }

            return null;
        }

        return null;
    }

    private static bool IsCommandAvailable(string command)
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "which",
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
}

/// <summary>
/// Extension methods for direct audio playback.
/// </summary>
public static class TextToSpeechPlaybackExtensions
{
    /// <summary>
    /// Synthesizes speech and plays it directly through the speakers.
    /// </summary>
    /// <param name="service">The TTS service.</param>
    /// <param name="text">The text to speak.</param>
    /// <param name="options">Optional TTS options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A result indicating success or failure.</returns>
    public static async Task<Result<bool, string>> SpeakAsync(
        this ITextToSpeechService service,
        string text,
        TextToSpeechOptions? options = null,
        CancellationToken ct = default)
    {
        Result<SpeechResult, string> synthesisResult = await service.SynthesizeAsync(text, options, ct);

        return synthesisResult.Match(
            speech => AudioPlayer.PlayAsync(speech, ct).GetAwaiter().GetResult(),
            error => Result<bool, string>.Failure(error));
    }
}
