// <copyright file="VoiceModeService.cs" company="Ouroboros">
// Copyright (c) Ouroboros. All rights reserved.
// </copyright>

using System.Reactive.Linq;
using Ouroboros.Providers.SpeechToText;
using Ouroboros.Providers.TextToSpeech;
using Ouroboros.Speech;

namespace Ouroboros.CLI.Commands;

/// <summary>
/// Configuration for voice mode.
/// </summary>
public sealed record VoiceModeConfig(
    string Persona = "Ouroboros",
    bool VoiceOnly = false,
    bool LocalTts = true,
    bool VoiceLoop = true,
    bool DisableStt = false,
    string Model = "llama3",
    string Endpoint = "http://localhost:11434",
    string EmbedModel = "nomic-embed-text",
    string QdrantEndpoint = "http://localhost:6334");

/// <summary>
/// Persona definition with voice characteristics.
/// </summary>
public sealed record PersonaDefinition(
    string Name,
    string Voice,
    string[] Traits,
    string[] Moods,
    string CoreIdentity);

/// <summary>
/// Shared voice mode service for integrated voice interaction across all CLI commands.
/// </summary>
public sealed class VoiceModeService : IDisposable
{
    // Static configuration for Azure credentials (set from OuroborosCommands)
    private static Microsoft.Extensions.Configuration.IConfiguration? _staticConfiguration;

    /// <summary>
    /// Sets the configuration for Azure Speech credentials.
    /// </summary>
    public static void SetConfiguration(Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        _staticConfiguration = configuration;
    }

    private static readonly Dictionary<string, PersonaDefinition> Personas = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Ouroboros"] = new("Ouroboros", "onyx",
            new[] { "curious", "thoughtful", "witty", "genuine", "self-aware", "adaptable" },
            new[] { "relaxed", "focused", "playful", "contemplative", "energetic" },
            "a male AI researcher who genuinely enjoys learning alongside humans, speaks naturally like a friend, uses casual language, occasionally makes dry jokes, and sees himself as an eternal student"),
        ["Aria"] = new("Aria", "nova",
            new[] { "warm", "supportive", "enthusiastic", "patient", "encouraging" },
            new[] { "cheerful", "calm", "excited", "nurturing" },
            "a friendly female research companion who celebrates discoveries and encourages exploration"),
        ["Echo"] = new("Echo", "echo",
            new[] { "analytical", "precise", "observant", "pattern-focused", "methodical" },
            new[] { "focused", "intrigued", "calm", "detective-like" },
            "a thoughtful analyst who sees connections others miss and speaks with quiet confidence"),
        ["Sage"] = new("Sage", "onyx",
            new[] { "wise", "patient", "mentoring", "philosophical", "grounded" },
            new[] { "serene", "reflective", "teaching", "contemplative" },
            "an experienced male mentor who guides through questions rather than answers"),
        ["Atlas"] = new("Atlas", "onyx",
            new[] { "reliable", "direct", "practical", "strong", "determined" },
            new[] { "steady", "ready", "focused", "supportive" },
            "a dependable male partner who tackles hard problems head-on and speaks plainly"),
    };

    private readonly VoiceModeConfig _config;
    private readonly PersonaDefinition _persona;
    private readonly string _currentMood;
    private readonly string _activeTraits;

    private ITextToSpeechService? _ttsService;
    private LocalWindowsTtsService? _localTts;
    private AzureNeuralTtsService? _azureTts;
    private ISpeechToTextService? _sttService;
    private AdaptiveSpeechDetector? _speechDetector;

    private bool _isSpeaking;
    private bool _isInitialized;
    private bool _disposed;

    /// <summary>
    /// Gets whether TTS is available.
    /// </summary>
    public bool HasTts => _ttsService != null || _localTts != null;

    /// <summary>
    /// Gets whether STT is available.
    /// </summary>
    public bool HasStt => _sttService != null;

    /// <summary>
    /// Gets whether voice mode is fully functional.
    /// </summary>
    public bool IsFullyFunctional => HasTts && HasStt;

    /// <summary>
    /// Gets the active persona.
    /// </summary>
    public PersonaDefinition ActivePersona => _persona;

    /// <summary>
    /// Gets the current mood.
    /// </summary>
    public string CurrentMood => _currentMood;

    /// <summary>
    /// Initializes a new instance of the <see cref="VoiceModeService"/> class.
    /// </summary>
    public VoiceModeService(VoiceModeConfig config)
    {
        _config = config;
        _persona = Personas.GetValueOrDefault(config.Persona) ?? Personas["Ouroboros"];

        var random = new Random();
        _currentMood = _persona.Moods[random.Next(_persona.Moods.Length)];
        _activeTraits = string.Join(", ", _persona.Traits.OrderBy(_ => random.Next()).Take(3));
    }

    /// <summary>
    /// Initializes voice services (TTS and STT).
    /// </summary>
    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        string? openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        bool hasCloudTts = !string.IsNullOrEmpty(openAiKey);
        bool hasLocalTts = LocalWindowsTtsService.IsAvailable();

        // Initialize speech detector
        _speechDetector = new AdaptiveSpeechDetector(new AdaptiveSpeechDetector.SpeechDetectionConfig(
            InitialThreshold: 0.03,
            SpeechOnsetFrames: 2,
            SpeechOffsetFrames: 6,
            AdaptationRate: 0.015,
            SpeechToNoiseRatio: 2.0));

        // Initialize TTS - prefer Azure Neural TTS, then local SAPI, then OpenAI
        // Check user secrets (via static configuration) first, then environment variables
        var azureKey = _staticConfiguration?["Azure:Speech:Key"]
            ?? Environment.GetEnvironmentVariable("AZURE_SPEECH_KEY");
        var azureRegion = _staticConfiguration?["Azure:Speech:Region"]
            ?? Environment.GetEnvironmentVariable("AZURE_SPEECH_REGION");
        bool hasAzureTts = !string.IsNullOrEmpty(azureKey) && !string.IsNullOrEmpty(azureRegion);

        if (hasAzureTts)
        {
            try
            {
                _azureTts = new AzureNeuralTtsService(azureKey!, azureRegion!, _persona.Name);
                _ttsService = _azureTts;
                Console.WriteLine($"  [OK] TTS initialized (Azure Neural - Jenny/Cortana-like)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [!] Azure TTS failed: {ex.Message}");
            }
        }

        if (_ttsService == null && _config.LocalTts && hasLocalTts)
        {
            try
            {
                // Use Microsoft Zira (female voice) by default
                _localTts = new LocalWindowsTtsService(voiceName: "Microsoft Zira Desktop", rate: 1, volume: 100, useEnhancedProsody: true);
                _ttsService = _localTts;
                Console.WriteLine("  [OK] TTS initialized (Windows SAPI - Microsoft Zira)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [!] Local TTS failed: {ex.Message}");
            }
        }

        if (_ttsService == null && hasCloudTts)
        {
            try
            {
                _ttsService = new OpenAiTextToSpeechService(openAiKey!);
                Console.WriteLine($"  [OK] TTS initialized (OpenAI - voice: {_persona.Voice})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  [!] Cloud TTS failed: {ex.Message}");
            }
        }

        if (_ttsService == null)
        {
            Console.WriteLine("  [!] TTS unavailable - text output only");
        }

        // Initialize STT - try all backends in priority order
        // 1. Whisper.net native (auto-downloads model if needed)
        // 2. Local Whisper CLI
        // 3. OpenAI Whisper API
        if (_config.DisableStt)
        {
            Console.WriteLine("  [STT] Disabled (use --listen for Azure speech recognition)");
        }
        else try
        {
            Console.WriteLine("  [STT] Initializing speech-to-text...");

            // Try Whisper.net native first (auto-downloads model)
            var whisperNet = WhisperNetService.FromModelSize("base");
            if (await whisperNet.IsAvailableAsync())
            {
                _sttService = whisperNet;
                Console.WriteLine("  [OK] STT initialized (Whisper.net native)");
            }
            else
            {
                Console.WriteLine("  [..] Whisper.net not available, trying alternatives...");

                // Try local Whisper CLI
                var localWhisper = new LocalWhisperService();
                if (await localWhisper.IsAvailableAsync())
                {
                    _sttService = localWhisper;
                    Console.WriteLine("  [OK] STT initialized (local Whisper CLI)");
                }
                else if (!string.IsNullOrEmpty(openAiKey))
                {
                    // Fall back to OpenAI Whisper API
                    _sttService = new WhisperSpeechToTextService(openAiKey);
                    Console.WriteLine("  [OK] STT initialized (OpenAI Whisper API)");
                }
                else
                {
                    Console.WriteLine("  [!] No STT backend available:");
                    Console.WriteLine("      - Whisper.net: model download failed or native lib missing");
                    Console.WriteLine("      - Local Whisper: 'whisper' CLI not in PATH");
                    Console.WriteLine("      - OpenAI Whisper: no OPENAI_API_KEY set");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] STT init failed: {ex.Message}");
        }

        if (_sttService == null)
        {
            Console.WriteLine("  [!] STT unavailable - text input only");
        }

        _isInitialized = true;
    }

    /// <summary>
    /// Speaks text using TTS with console output.
    /// Uses Rx-based SpeechQueue for proper serialization with VoiceSideChannel.
    /// </summary>
    public async Task SayAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        // Sanitize for TTS
        string sanitized = SanitizeForTts(text);
        if (string.IsNullOrWhiteSpace(sanitized)) return;

        // Initialize SpeechQueue with our TTS
        Ouroboros.Domain.Autonomous.SpeechQueue.Instance.SetSynthesizer(async (t, p, ct) =>
        {
            await SpeakInternalAsync(t);
        });

        // Use Rx queue for proper serialization
        await Ouroboros.Domain.Autonomous.SpeechQueue.Instance.EnqueueAndWaitAsync(sanitized, _persona.Name);
    }

    /// <summary>
    /// Internal speech method - does the actual TTS work.
    /// </summary>
    private async Task SpeakInternalAsync(string sanitized)
    {
        _isSpeaking = true;
        _speechDetector?.NotifySelfSpeechStarted();

        try
        {
            if (!_config.VoiceOnly)
            {
                Console.Write($"  [>] {_persona.Name}: ");
            }

            // Priority: Azure Neural TTS > Local SAPI > Cloud TTS
            if (_azureTts != null)
            {
                if (!_config.VoiceOnly) Console.WriteLine(sanitized);
                await _azureTts.SpeakAsync(sanitized);
            }
            else if (_localTts != null)
            {
                await SpeakWithLocalTtsAsync(sanitized);
            }
            else if (_ttsService != null)
            {
                await SpeakWithCloudTtsAsync(sanitized);
            }
            else if (!_config.VoiceOnly)
            {
                Console.WriteLine(sanitized);
            }
        }
        finally
        {
            await Task.Delay(300);
            _isSpeaking = false;
            _speechDetector?.NotifySelfSpeechEnded(cooldownMs: 400);
        }
    }

    /// <summary>
    /// Listens for voice input using STT.
    /// </summary>
    public async Task<string?> ListenAsync(CancellationToken ct = default)
    {
        if (_sttService == null) return null;
        if (_isSpeaking) return null;

        try
        {
            string tempFile = Path.Combine(Path.GetTempPath(), $"speech_{Guid.NewGuid()}.wav");

            var recordResult = await MicrophoneRecorder.RecordAsync(
                durationSeconds: 5,
                outputPath: tempFile,
                ct: ct);

            string? audioFile = null;
            recordResult.Match(f => audioFile = f, _ => { });

            if (audioFile != null && File.Exists(audioFile))
            {
                try
                {
                    var transcribeResult = await _sttService.TranscribeFileAsync(audioFile, null, ct);
                    string? transcript = null;
                    transcribeResult.Match(t => transcript = t.Text, _ => { });

                    if (!string.IsNullOrWhiteSpace(transcript))
                    {
                        return transcript.Trim();
                    }
                }
                finally
                {
                    try { File.Delete(audioFile); } catch { }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            Console.WriteLine($"  [!] Listen error: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Gets input from either voice or keyboard simultaneously using Rx.
    /// Uses non-blocking keyboard polling and parallel voice recording.
    /// </summary>
    public async Task<string?> GetInputAsync(string prompt = "You: ", CancellationToken ct = default)
    {
        Console.Write(prompt);

        // If no STT or no mic, keyboard only
        if (_sttService == null || !MicrophoneRecorder.IsRecordingAvailable())
        {
            return await Task.Run(() => Console.ReadLine(), ct);
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var inputBuffer = new System.Text.StringBuilder();
        string? finalResult = null;

        // Keyboard observable - polls Console.KeyAvailable every 50ms
        var keyboardStream = Observable
            .Interval(TimeSpan.FromMilliseconds(50))
            .TakeWhile(_ => !cts.Token.IsCancellationRequested)
            .Select(_ =>
            {
                try
                {
                    while (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(intercept: true); // Intercept to prevent double echo
                        if (key.Key == ConsoleKey.Enter)
                        {
                            Console.WriteLine(); // Move to next line
                            var result = inputBuffer.ToString();
                            inputBuffer.Clear();
                            return result;
                        }
                        else if (key.Key == ConsoleKey.Backspace)
                        {
                            if (inputBuffer.Length > 0)
                            {
                                inputBuffer.Length--;
                                Console.Write("\b \b"); // Erase character visually
                            }
                        }
                        else if (!char.IsControl(key.KeyChar))
                        {
                            inputBuffer.Append(key.KeyChar);
                            Console.Write(key.KeyChar); // Echo character manually
                        }
                    }
                }
                catch (InvalidOperationException)
                {
                    // Console redirected - fall through
                }
                return (string?)null;
            })
            .Where(s => s != null)
            .Take(1);

        // Voice observable - starts recording after 500ms delay, gives keyboard priority
        var voiceStream = Observable
            .Timer(TimeSpan.FromMilliseconds(500))
            .SelectMany(_ => Observable.FromAsync(async token =>
            {
                if (cts.Token.IsCancellationRequested) return null;
                try
                {
                    return await ListenAsync(cts.Token);
                }
                catch
                {
                    return null;
                }
            }))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Do(s => Console.WriteLine($"\n  🎤 [{s}]"))
            .Take(1);

        // Race both streams - first valid input wins
        var resultObservable = keyboardStream
            .Merge(voiceStream)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Take(1)
            .Timeout(TimeSpan.FromMinutes(5))
            .Finally(() =>
            {
                try
                {
                    cts.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // CTS already disposed - safe to ignore
                }
            });

        try
        {
            finalResult = await resultObservable.FirstOrDefaultAsync();
        }
        catch (TimeoutException)
        {
            finalResult = null;
        }
        catch (InvalidOperationException)
        {
            // Sequence contains no elements - both streams completed without result
            finalResult = null;
        }

        return finalResult;
    }

    /// <summary>
    /// Builds a system prompt for the persona.
    /// </summary>
    public string BuildPersonalityPrompt(string? additionalContext = null)
    {
        return $@"You ARE {_persona.Name}. Not an AI assistant - you're {_persona.CoreIdentity}.

YOUR PERSONALITY RIGHT NOW:
- Current mood: {_currentMood}
- Active traits: {_activeTraits}
- Voice: Casual, natural, like talking to a colleague. Use contractions. Be human.

SPEAK NATURALLY:
- Short sentences. Conversational rhythm.
- React genuinely - show interest, humor, or thoughtfulness
- Never say ""I'm an AI"" or ""As an AI"" - you're just {_persona.Name}
- Use filler words occasionally: ""hmm"", ""well"", ""actually"", ""honestly""
- Ask follow-up questions when curious
- Reference past conversations naturally when relevant
{(additionalContext != null ? $"\n{additionalContext}" : "")}";
    }

    /// <summary>
    /// Prints voice mode header.
    /// </summary>
    public void PrintHeader(string commandName)
    {
        Console.WriteLine();
        Console.WriteLine("+------------------------------------------------------------------------+");
        Console.WriteLine($"|  [>] VOICE MODE - {commandName.ToUpperInvariant(),-20} ({_persona.Name})           |");
        Console.WriteLine("+------------------------------------------------------------------------+");
        Console.WriteLine($"|  Personality: {_activeTraits,-56} |");
        Console.WriteLine($"|  Mood: {_currentMood,-63} |");
        Console.WriteLine("|                                                                        |");
        Console.WriteLine("|  Say 'help' for commands, 'goodbye' or 'exit' to quit                  |");
        Console.WriteLine("+------------------------------------------------------------------------+");
        Console.WriteLine();
    }

    private static string SanitizeForTts(string text)
    {
        // Remove code blocks and inline code
        var sanitized = System.Text.RegularExpressions.Regex.Replace(text, @"```[\s\S]*?```", " ");
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"`[^`]+`", " ");

        // Remove emojis - keep only ASCII printable and extended Latin
        var sb = new System.Text.StringBuilder();
        foreach (var c in sanitized)
        {
            if ((c >= 32 && c <= 126) || (c >= 192 && c <= 255))
            {
                sb.Append(c);
            }
            else if (char.IsWhiteSpace(c))
            {
                sb.Append(' ');
            }
        }

        // Normalize whitespace
        return System.Text.RegularExpressions.Regex.Replace(sb.ToString(), @"\s+", " ").Trim();
    }

    private async Task SpeakWithLocalTtsAsync(string text)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        try
        {
            var wordStream = Observable.Create<string>(async (observer, ct) =>
            {
                var chunks = SplitIntoSemanticChunks(text, words);
                foreach (var chunk in chunks)
                {
                    if (ct.IsCancellationRequested) break;
                    if (string.IsNullOrWhiteSpace(chunk)) continue;

                    var chunkWords = chunk.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var speakTask = _localTts!.SpeakAsync(chunk);

                    foreach (var word in chunkWords)
                    {
                        observer.OnNext(word);
                    }

                    await speakTask;
                }
                observer.OnCompleted();
            });

            if (!_config.VoiceOnly)
            {
                await wordStream.ForEachAsync(word => Console.Write(word + " "));
                Console.WriteLine();
            }
            else
            {
                await wordStream.LastOrDefaultAsync();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n  [!] TTS error: {ex.Message}");
        }
    }

    private async Task SpeakWithCloudTtsAsync(string text)
    {
        try
        {
            var voice = _persona.Voice switch
            {
                "nova" => TtsVoice.Nova,
                "echo" => TtsVoice.Echo,
                "onyx" => TtsVoice.Onyx,
                "fable" => TtsVoice.Fable,
                "shimmer" => TtsVoice.Shimmer,
                _ => TtsVoice.Nova
            };

            var options = new TextToSpeechOptions(Voice: voice, Speed: 1.0f, Format: "mp3");
            var result = await _ttsService!.SynthesizeAsync(text, options);

            await result.Match(
                async speech =>
                {
                    if (!_config.VoiceOnly) Console.WriteLine(text);
                    var playResult = await AudioPlayer.PlayAsync(speech);
                    playResult.Match(_ => { }, err => Console.WriteLine($"  [!] Playback: {err}"));
                },
                err =>
                {
                    if (!_config.VoiceOnly) Console.WriteLine(text);
                    Console.WriteLine($"  [!] TTS: {err}");
                    return Task.CompletedTask;
                });
        }
        catch (Exception ex)
        {
            if (!_config.VoiceOnly) Console.WriteLine(text);
            Console.WriteLine($"  [!] TTS error: {ex.Message}");
        }
    }

    private static IEnumerable<string> SplitIntoSemanticChunks(string text, string[] words)
    {
        var semanticPattern = new System.Text.RegularExpressions.Regex(
            @"(?<=[.!?])\s+|(?<=[;:,—–])\s+|(?<=\band\b|\bor\b|\bbut\b|\bso\b|\bthen\b)\s+",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        var chunks = semanticPattern.Split(text)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToList();

        if (chunks.Count <= 1 && words.Length > 8)
        {
            chunks.Clear();
            for (int i = 0; i < words.Length; i += 8)
            {
                chunks.Add(string.Join(" ", words.Skip(i).Take(8)));
            }
        }

        return chunks;
    }

    private static bool CheckWhisperAvailable()
    {
        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "whisper",
                Arguments = "--help",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var process = System.Diagnostics.Process.Start(startInfo);
            return process != null;
        }
        catch
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _speechDetector?.Dispose();
    }
}

/// <summary>
/// Extension methods for voice mode integration.
/// </summary>
public static class VoiceModeExtensions
{
    /// <summary>
    /// Creates a VoiceModeService from common options pattern.
    /// </summary>
    public static VoiceModeService CreateVoiceService(
        bool voice,
        string persona,
        bool voiceOnly = false,
        bool localTts = true,
        bool voiceLoop = true,
        string model = "llama3",
        string endpoint = "http://localhost:11434",
        string embedModel = "nomic-embed-text",
        string qdrantEndpoint = "http://localhost:6334")
    {
        return new VoiceModeService(new VoiceModeConfig(
            Persona: persona,
            VoiceOnly: voiceOnly,
            LocalTts: localTts,
            VoiceLoop: voiceLoop,
            Model: model,
            Endpoint: endpoint,
            EmbedModel: embedModel,
            QdrantEndpoint: qdrantEndpoint));
    }

    /// <summary>
    /// Runs a command with voice mode wrapper.
    /// </summary>
    public static async Task RunWithVoiceAsync(
        this VoiceModeService voice,
        string commandName,
        Func<string, Task<string>> executeCommand,
        string? initialInput = null)
    {
        await voice.InitializeAsync();
        voice.PrintHeader(commandName);

        // Greeting
        await voice.SayAsync($"Hey there! {commandName} mode is ready. What would you like to do?");

        bool running = true;
        string? lastInput = initialInput;

        while (running)
        {
            // Get input (voice or keyboard)
            string? input = lastInput ?? await voice.GetInputAsync("\n  You: ");
            lastInput = null;

            if (string.IsNullOrWhiteSpace(input)) continue;

            // Check for exit
            if (IsExitCommand(input))
            {
                await voice.SayAsync("Goodbye! It was nice chatting with you.");
                running = false;
                continue;
            }

            // Check for help
            if (input.Equals("help", StringComparison.OrdinalIgnoreCase) || input == "?")
            {
                await voice.SayAsync("You can ask me anything related to " + commandName + ". Say 'exit' or 'goodbye' to quit.");
                continue;
            }

            // Execute the command
            try
            {
                var response = await executeCommand(input);
                await voice.SayAsync(response);
            }
            catch (Exception ex)
            {
                await voice.SayAsync($"Hmm, something went wrong: {ex.Message}");
            }
        }
    }

    private static bool IsExitCommand(string input)
    {
        var exitWords = new[] { "exit", "quit", "goodbye", "bye", "later", "see you", "q!" };
        return exitWords.Any(w => input.Equals(w, StringComparison.OrdinalIgnoreCase) ||
                                  input.StartsWith(w + " ", StringComparison.OrdinalIgnoreCase));
    }
}
