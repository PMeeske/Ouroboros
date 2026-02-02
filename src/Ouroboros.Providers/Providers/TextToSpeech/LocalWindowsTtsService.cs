// <copyright file="LocalWindowsTtsService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace Ouroboros.Providers.TextToSpeech;

/// <summary>
/// Local text-to-speech service using Windows SAPI (no API key required).
/// Works offline using Windows built-in speech synthesis.
/// Enhanced with SSML support for natural prosody and expression.
/// </summary>
public sealed partial class LocalWindowsTtsService : ITextToSpeechService
{
    private readonly string _voiceName;
    private readonly int _rate;
    private readonly int _volume;
    private readonly bool _useEnhancedProsody;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalWindowsTtsService"/> class.
    /// </summary>
    /// <param name="voiceName">Optional voice name (e.g., "Microsoft David", "Microsoft Zira").</param>
    /// <param name="rate">Speech rate from -10 (slow) to 10 (fast), default -1 for natural pacing.</param>
    /// <param name="volume">Volume from 0 to 100, default 100.</param>
    /// <param name="useEnhancedProsody">Whether to use SSML for enhanced prosody and expression.</param>
    public LocalWindowsTtsService(string? voiceName = null, int rate = -1, int volume = 100, bool useEnhancedProsody = true)
    {
        _voiceName = voiceName ?? string.Empty;
        _rate = Math.Clamp(rate, -10, 10);
        _volume = Math.Clamp(volume, 0, 100);
        _useEnhancedProsody = useEnhancedProsody;
    }

    /// <inheritdoc/>
    public string ProviderName => "Windows SAPI (Local)";

    /// <inheritdoc/>
    public IReadOnlyList<string> AvailableVoices => new[]
    {
        "Microsoft David",
        "Microsoft Zira",
        "Microsoft Mark",
        "Microsoft Eva",
    };

    /// <inheritdoc/>
    public IReadOnlyList<string> SupportedFormats => new[] { "wav" };

    /// <inheritdoc/>
    public int MaxInputLength => 32000;

    /// <summary>
    /// Checks if Windows TTS is available on this system.
    /// </summary>
    /// <returns>True if available on Windows.</returns>
    public static bool IsAvailable() => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    /// <inheritdoc/>
    public Task<bool> IsAvailableAsync(CancellationToken ct = default)
        => Task.FromResult(IsAvailable());

    /// <summary>
    /// Lists available Windows voices.
    /// </summary>
    /// <returns>List of voice names.</returns>
    public static async Task<Result<List<string>, string>> ListVoicesAsync()
    {
        if (!IsAvailable())
        {
            return Result<List<string>, string>.Failure("Windows TTS only available on Windows");
        }

        try
        {
            string script = @"
Add-Type -AssemblyName System.Speech
$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
$synth.GetInstalledVoices() | ForEach-Object { $_.VoiceInfo.Name }
";
            var result = await RunPowerShellAsync(script);
            return result.Match(
                output => Result<List<string>, string>.Success(
                    output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()),
                error => Result<List<string>, string>.Failure(error));
        }
        catch (Exception ex)
        {
            return Result<List<string>, string>.Failure(ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<Result<SpeechResult, string>> SynthesizeAsync(
        string text,
        TextToSpeechOptions? options = null,
        CancellationToken ct = default)
    {
        if (!IsAvailable())
        {
            return Result<SpeechResult, string>.Failure("Windows TTS only available on Windows");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return Result<SpeechResult, string>.Failure("Text cannot be empty");
        }

        try
        {
            // Map TtsVoice to rate adjustment for personality
            int rateAdjust = options?.Voice switch
            {
                TtsVoice.Nova => 1,      // Slightly faster, upbeat
                TtsVoice.Echo => -1,     // Slightly slower, warm
                TtsVoice.Onyx => -2,     // Slower, authoritative
                TtsVoice.Fable => 0,     // Normal, expressive
                TtsVoice.Shimmer => -1,  // Slower, gentle
                _ => 0
            };

            int effectiveRate = Math.Clamp(_rate + rateAdjust, -10, 10);
            string tempFile = Path.Combine(Path.GetTempPath(), $"tts_{Guid.NewGuid()}.wav");

            string voiceSelection = string.IsNullOrEmpty(_voiceName)
                ? string.Empty
                : $"$synth.SelectVoice('{_voiceName}')";

            // Use SSML for enhanced prosody if enabled
            string speechContent;
            string speakMethod;
            string script;

            if (_useEnhancedProsody)
            {
                string ssml = BuildEnhancedSsml(text, effectiveRate);
                // Use PowerShell here-string (@' ... '@) for SSML to avoid escaping issues
                // Here-strings preserve content literally without interpretation
                script = $@"
Add-Type -AssemblyName System.Speech
$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
{voiceSelection}
$synth.Rate = {effectiveRate}
$synth.Volume = {_volume}
$synth.SetOutputToWaveFile('{tempFile}')
$ssmlContent = @'
{ssml}
'@
$synth.SpeakSsml($ssmlContent)
$synth.SetOutputToNull()
$synth.Dispose()
Write-Output 'OK'
";
            }
            else
            {
                // Escape text for PowerShell single-quoted string
                speechContent = text
                    .Replace("'", "''")
                    .Replace("`", "``")
                    .Replace("$", "`$")
                    .Replace("\"", "`\"");
                speakMethod = "Speak";

                script = $@"
Add-Type -AssemblyName System.Speech
$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
{voiceSelection}
$synth.Rate = {effectiveRate}
$synth.Volume = {_volume}
$synth.SetOutputToWaveFile('{tempFile}')
$synth.{speakMethod}('{speechContent}')
$synth.SetOutputToNull()
$synth.Dispose()
Write-Output 'OK'
";
            }

            var runResult = await RunPowerShellAsync(script, ct);

            if (runResult.IsSuccess)
            {
                if (!File.Exists(tempFile))
                {
                    return Result<SpeechResult, string>.Failure("TTS failed to create audio file");
                }

                byte[] audioData = await File.ReadAllBytesAsync(tempFile, ct);

                // Clean up temp file
                try { File.Delete(tempFile); } catch { }

                return Result<SpeechResult, string>.Success(new SpeechResult(audioData, "wav"));
            }
            else
            {
                return Result<SpeechResult, string>.Failure(runResult.Error);
            }
        }
        catch (Exception ex)
        {
            return Result<SpeechResult, string>.Failure($"TTS error: {ex.Message}");
        }
    }

    /// <summary>
    /// Builds SSML with enhanced prosody for more natural speech.
    /// </summary>
    private string BuildEnhancedSsml(string text, int rate)
    {
        // Convert rate (-10 to 10) to SSML prosody rate (x-slow to x-fast)
        string prosodyRate = rate switch
        {
            <= -6 => "x-slow",
            <= -3 => "slow",
            <= 3 => "medium",
            <= 6 => "fast",
            _ => "x-fast"
        };

        // Add natural pauses and emphasis
        string enhancedText = AddNaturalProsody(text);

        return $@"<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='en-US'>
<prosody rate='{prosodyRate}' pitch='+0%' volume='loud'>
{enhancedText}
</prosody>
</speak>";
    }

    /// <summary>
    /// Adds natural prosody markers to text for more expressive speech.
    /// </summary>
    private string AddNaturalProsody(string text)
    {
        // Escape XML special characters first
        text = text
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;"); // Also escape apostrophes for XML/PowerShell safety

        // Add slight pauses after punctuation for more natural rhythm
        text = PeriodPauseRegex().Replace(text, ". <break time='400ms'/>");
        text = CommaPauseRegex().Replace(text, ", <break time='200ms'/>");
        text = SemicolonPauseRegex().Replace(text, "; <break time='300ms'/>");
        text = ColonPauseRegex().Replace(text, ": <break time='250ms'/>");

        // Add emphasis to words in ALL CAPS (but not single letters)
        text = AllCapsRegex().Replace(text, "<emphasis level='strong'>$1</emphasis>");

        // Add slight emphasis to quoted text
        text = QuotedTextRegex().Replace(text, "<emphasis level='moderate'>$1</emphasis>");

        // Handle ellipsis with longer pause
        text = text.Replace("...", "<break time='600ms'/>");

        // Handle exclamations with emphasis
        text = ExclamationRegex().Replace(text, "<emphasis level='strong'>$1</emphasis>!");

        return text;
    }

    [GeneratedRegex(@"\.(?=\s|$)")]
    private static partial Regex PeriodPauseRegex();

    [GeneratedRegex(@",(?=\s)")]
    private static partial Regex CommaPauseRegex();

    [GeneratedRegex(@";(?=\s)")]
    private static partial Regex SemicolonPauseRegex();

    [GeneratedRegex(@":(?=\s)")]
    private static partial Regex ColonPauseRegex();

    [GeneratedRegex(@"\b([A-Z]{2,})\b")]
    private static partial Regex AllCapsRegex();

    [GeneratedRegex(@"[""']([^""']+)[""']")]
    private static partial Regex QuotedTextRegex();

    [GeneratedRegex(@"(\w+)!")]
    private static partial Regex ExclamationRegex();

    /// <inheritdoc/>
    public async Task<Result<string, string>> SynthesizeToFileAsync(
        string text,
        string outputPath,
        TextToSpeechOptions? options = null,
        CancellationToken ct = default)
    {
        var result = await SynthesizeAsync(text, options, ct);

        if (result.IsSuccess)
        {
            try
            {
                string directory = Path.GetDirectoryName(outputPath) ?? ".";
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllBytesAsync(outputPath, result.Value.AudioData, ct);
                return Result<string, string>.Success(outputPath);
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to save audio: {ex.Message}");
            }
        }
        else
        {
            return Result<string, string>.Failure(result.Error);
        }
    }

    /// <inheritdoc/>
    public async Task<Result<string, string>> SynthesizeToStreamAsync(
        string text,
        Stream outputStream,
        TextToSpeechOptions? options = null,
        CancellationToken ct = default)
    {
        var result = await SynthesizeAsync(text, options, ct);

        if (result.IsSuccess)
        {
            try
            {
                await outputStream.WriteAsync(result.Value.AudioData, ct);
                return Result<string, string>.Success("wav");
            }
            catch (Exception ex)
            {
                return Result<string, string>.Failure($"Failed to write to stream: {ex.Message}");
            }
        }
        else
        {
            return Result<string, string>.Failure(result.Error);
        }
    }

    /// <summary>
    /// Speaks text directly to the default audio output.
    /// </summary>
    /// <param name="text">Text to speak.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task SpeakAsync(string text, CancellationToken ct = default)
    {
        var result = await SpeakDirectAsync(text, ct);
        if (!result.IsSuccess)
        {
            Console.WriteLine($"  [!] Local TTS Error: {result.Error}");
        }
    }

    /// <summary>
    /// Speaks text directly without returning audio data (fire and forget).
    /// </summary>
    /// <param name="text">Text to speak.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    public async Task<Result<bool, string>> SpeakDirectAsync(string text, CancellationToken ct = default)
    {
        return await SpeakWithToneAsync(text, _rate, _volume, ct);
    }

    /// <summary>
    /// Speaks text with specified voice tone settings.
    /// </summary>
    /// <param name="text">Text to speak.</param>
    /// <param name="rate">Speech rate from -10 (slow) to 10 (fast).</param>
    /// <param name="volume">Volume from 0 to 100.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Result indicating success or failure.</returns>
    public async Task<Result<bool, string>> SpeakWithToneAsync(
        string text,
        int rate,
        int volume,
        CancellationToken ct = default)
    {
        if (!IsAvailable())
        {
            return Result<bool, string>.Failure("Windows TTS only available on Windows");
        }

        try
        {
            // Clamp values to valid ranges
            rate = Math.Clamp(rate, -10, 10);
            volume = Math.Clamp(volume, 0, 100);

            // Use PowerShell here-string (@' ... '@) to avoid escaping issues with apostrophes
            // Here-strings preserve content literally without interpretation
            string script = $@"
$synth = New-Object -ComObject SAPI.SpVoice
$voices = $synth.GetVoices()
foreach ($v in $voices) {{
    if ($v.GetDescription() -like '*English*') {{
        $synth.Voice = $v
        break
    }}
}}
$synth.Rate = {rate}
$synth.Volume = {volume}
$speechText = @'
{text}
'@
$synth.Speak($speechText)
";

            var result = await RunPowerShellAsync(script, ct);
            return result.Match(
                _ => Result<bool, string>.Success(true),
                error => Result<bool, string>.Failure(error));
        }
        catch (Exception ex)
        {
            return Result<bool, string>.Failure($"TTS error: {ex.Message}");
        }
    }

    private static async Task<Result<string, string>> RunPowerShellAsync(
        string script,
        CancellationToken ct = default)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -Command \"{script.Replace("\"", "\\\"")}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return Result<string, string>.Failure("Failed to start PowerShell");
            }

            string output = await process.StandardOutput.ReadToEndAsync(ct);
            string error = await process.StandardError.ReadToEndAsync(ct);

            await process.WaitForExitAsync(ct);

            if (process.ExitCode != 0 && !string.IsNullOrWhiteSpace(error))
            {
                return Result<string, string>.Failure(error.Trim());
            }

            return Result<string, string>.Success(output.Trim());
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure(ex.Message);
        }
    }
}
