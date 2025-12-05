// <copyright file="SpeechToTextCliSteps.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using LangChainPipeline.Providers.SpeechToText;

namespace Ouroboros.Application;

/// <summary>
/// CLI Pipeline steps for speech-to-text operations.
/// Supports OpenAI Whisper API and local Whisper installations.
/// Note: Use semicolon (;) as separator inside quotes since pipe (|) is the DSL step separator.
/// </summary>
public static class SpeechToTextCliSteps
{
    /// <summary>
    /// Current speech-to-text service instance.
    /// </summary>
    private static ISpeechToTextService? currentService;

    /// <summary>
    /// Initialize Whisper for speech-to-text (offline-first).
    /// Usage: SttInit() - auto-detects local Whisper, falls back to OpenAI
    /// Usage: SttInit('local;model=small') - force local Whisper (default)
    /// Usage: SttInit('openai;apiKey=sk-xxx') - force OpenAI API
    /// </summary>
    [PipelineToken("SttInit", "WhisperInit", "InitStt")]
    public static Step<CliPipelineState, CliPipelineState> SttInit(string? args = null)
        => s =>
        {
            SttConfig config = ParseSttConfig(args);

            try
            {
                // Offline-first: try local Whisper unless explicitly requesting OpenAI
                if (config.Provider.ToLowerInvariant() == "openai")
                {
                    // Explicitly requested OpenAI
                    string apiKey = config.ApiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                        ?? throw new InvalidOperationException("API key required. Set OPENAI_API_KEY or pass apiKey=...");
                    currentService = new WhisperSpeechToTextService(apiKey, config.Endpoint, config.Model ?? "whisper-1");
                }
                else if (config.Provider.ToLowerInvariant() is "local" or "whisper" or "whisper.cpp" or "offline")
                {
                    // Explicitly requested local
                    currentService = new LocalWhisperService(config.WhisperPath, config.ModelPath, config.Model ?? "small");
                }
                else
                {
                    // Auto-detect: prefer local/offline, fallback to cloud
                    LocalWhisperService localService = new LocalWhisperService(config.WhisperPath, config.ModelPath, config.Model ?? "small");
                    if (localService.IsAvailableAsync().GetAwaiter().GetResult())
                    {
                        currentService = localService;
                        if (s.Trace)
                        {
                            Console.WriteLine("[stt] Using offline local Whisper");
                        }
                    }
                    else
                    {
                        // Fallback to OpenAI if available
                        string? apiKey = config.ApiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                        if (!string.IsNullOrEmpty(apiKey))
                        {
                            currentService = new WhisperSpeechToTextService(apiKey, config.Endpoint, config.Model ?? "whisper-1");
                            if (s.Trace)
                            {
                                Console.WriteLine("[stt] Local Whisper not found, using OpenAI API");
                            }
                        }
                        else
                        {
                            throw new InvalidOperationException(
                                "No speech-to-text available. Install Whisper locally or set OPENAI_API_KEY.");
                        }
                    }
                }

                if (s.Trace)
                {
                    Console.WriteLine($"[stt] Initialized {currentService.ProviderName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[stt] Failed to initialize: {ex.Message}");
            }

            return Task.FromResult(s);
        };

    /// <summary>
    /// Transcribe an audio file to text.
    /// Usage: Transcribe('path/to/audio.mp3')
    /// Usage: Transcribe('path/to/audio.mp3;language=en')
    /// The transcribed text is stored in s.Output and s.Context.
    /// </summary>
    [PipelineToken("Transcribe", "SttTranscribe", "AudioToText")]
    public static Step<CliPipelineState, CliPipelineState> Transcribe(string? args = null)
        => async s =>
        {
            var config = ParseTranscribeArgs(args);

            if (string.IsNullOrEmpty(config.FilePath))
            {
                Console.WriteLine("[stt] Error: File path required. Usage: Transcribe('audio.mp3')");
                return s;
            }

            if (currentService == null)
            {
                // Auto-initialize with offline-first approach
                currentService = await TryAutoInitializeAsync(s.Trace);
                if (currentService == null)
                {
                    Console.WriteLine("[stt] Error: Not initialized. Install Whisper locally or set OPENAI_API_KEY");
                    return s;
                }
            }

            Console.WriteLine($"[stt] Transcribing: {config.FilePath}");

            var options = new TranscriptionOptions(
                Language: config.Language,
                ResponseFormat: config.Format ?? "json",
                Temperature: config.Temperature,
                TimestampGranularity: config.Timestamps ? "segment" : null,
                Prompt: config.Prompt);

            var result = await currentService.TranscribeFileAsync(config.FilePath, options);

            result.Match(
                transcription =>
                {
                    s.Output = transcription.Text;
                    s.Context = transcription.Text;

                    // Always show detection info for visibility
                    Console.WriteLine();
                    Console.WriteLine($"[stt] ✓ Language detected: {transcription.Language ?? "unknown"}");
                    Console.WriteLine($"[stt] ✓ Words detected: {(string.IsNullOrWhiteSpace(transcription.Text) ? "(none)" : transcription.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length.ToString())}");
                    if (transcription.Duration.HasValue)
                    {
                        Console.WriteLine($"[stt] ✓ Duration: {transcription.Duration:F2}s");
                    }

                    Console.WriteLine();
                    Console.WriteLine("═══════════════════════════════════════════════════════");
                    Console.WriteLine($"📝 TRANSCRIPTION: {(string.IsNullOrWhiteSpace(transcription.Text) ? "[No speech detected]" : transcription.Text)}");
                    Console.WriteLine("═══════════════════════════════════════════════════════");
                    Console.WriteLine();

                    // If timestamps requested, print segments
                    if (config.Timestamps && transcription.Segments != null)
                    {
                        Console.WriteLine("[stt] Segments:");
                        foreach (var segment in transcription.Segments)
                        {
                            Console.WriteLine($"  [{segment.Start:F2}-{segment.End:F2}] {segment.Text}");
                        }
                    }
                },
                error =>
                {
                    Console.WriteLine($"[stt] Error: {error}");
                    s.Output = $"Transcription failed: {error}";
                });

            return s;
        };

    /// <summary>
    /// Translate audio to English.
    /// Usage: SttTranslate('path/to/foreign_audio.mp3')
    /// Translates non-English audio to English text.
    /// </summary>
    [PipelineToken("SttTranslate", "TranslateAudio")]
    public static Step<CliPipelineState, CliPipelineState> SttTranslate(string? args = null)
        => async s =>
        {
            var config = ParseTranscribeArgs(args);

            if (string.IsNullOrEmpty(config.FilePath))
            {
                Console.WriteLine("[stt] Error: File path required. Usage: SttTranslate('audio.mp3')");
                return s;
            }

            if (currentService == null)
            {
                currentService = await TryAutoInitializeAsync(s.Trace);
                if (currentService == null)
                {
                    Console.WriteLine("[stt] Error: Not initialized. Install Whisper locally or set OPENAI_API_KEY");
                    return s;
                }
            }

            Console.WriteLine($"[stt] Translating to English: {config.FilePath}");

            var options = new TranscriptionOptions(
                ResponseFormat: config.Format ?? "json",
                Temperature: config.Temperature,
                Prompt: config.Prompt);

            var result = await currentService.TranslateToEnglishAsync(config.FilePath, options);

            result.Match(
                transcription =>
                {
                    s.Output = transcription.Text;
                    s.Context = transcription.Text;
                    Console.WriteLine($"\n{transcription.Text}\n");
                },
                error =>
                {
                    Console.WriteLine($"[stt] Error: {error}");
                    s.Output = $"Translation failed: {error}";
                });

            return s;
        };

    /// <summary>
    /// Transcribe audio and use the result as a query for the LLM.
    /// Usage: SttAsk('audio.mp3') - transcribes and sends to LLM
    /// Combines speech-to-text with LLM processing.
    /// </summary>
    [PipelineToken("SttAsk", "VoiceAsk")]
    public static Step<CliPipelineState, CliPipelineState> SttAsk(string? args = null)
        => async s =>
        {
            var config = ParseTranscribeArgs(args);

            if (string.IsNullOrEmpty(config.FilePath))
            {
                Console.WriteLine("[stt] Error: File path required. Usage: SttAsk('audio.mp3')");
                return s;
            }

            // First transcribe (offline-first)
            if (currentService == null)
            {
                currentService = await TryAutoInitializeAsync(s.Trace);
                if (currentService == null)
                {
                    Console.WriteLine("[stt] Error: Not initialized. Install Whisper locally or set OPENAI_API_KEY");
                    return s;
                }
            }

            Console.WriteLine($"[stt] Processing voice query: {config.FilePath}");

            var options = new TranscriptionOptions(
                Language: config.Language,
                ResponseFormat: "json");

            var transcribeResult = await currentService.TranscribeFileAsync(config.FilePath, options);

            string? query = null;
            transcribeResult.Match(
                t => query = t.Text,
                error => Console.WriteLine($"[stt] Transcription error: {error}"));

            if (string.IsNullOrEmpty(query))
            {
                return s;
            }

            Console.WriteLine($"[stt] Transcribed query: {query}");

            // Set as query and call LLM
            s.Query = query;

            try
            {
                var response = await s.Llm.InnerModel.GenerateTextAsync(query);
                s.Output = response;
                Console.WriteLine($"\n[LLM Response]\n{response}\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[stt] LLM error: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// Check speech-to-text service availability.
    /// Usage: SttStatus()
    /// </summary>
    [PipelineToken("SttStatus")]
    public static Step<CliPipelineState, CliPipelineState> SttStatus(string? args = null)
        => async s =>
        {
            if (currentService == null)
            {
                Console.WriteLine("[stt] Not initialized. Use SttInit() to configure.");
                return s;
            }

            Console.WriteLine($"[stt] Provider: {currentService.ProviderName}");
            Console.WriteLine($"[stt] Supported formats: {string.Join(", ", currentService.SupportedFormats)}");
            Console.WriteLine($"[stt] Max file size: {currentService.MaxFileSizeBytes / (1024 * 1024)} MB");

            var available = await currentService.IsAvailableAsync();
            Console.WriteLine($"[stt] Available: {(available ? "Yes" : "No")}");

            // Check microphone availability
            bool micAvailable = MicrophoneRecorder.IsRecordingAvailable();
            Console.WriteLine($"[stt] Microphone recording: {(micAvailable ? "Available" : "Not available (install ffmpeg)")}");

            return s;
        };

    /// <summary>
    /// Record audio from microphone for a specified duration.
    /// Usage: Record('5') - records for 5 seconds
    /// Usage: Record('10;output=my_recording.wav')
    /// The recorded file path is stored in s.Output.
    /// </summary>
    [PipelineToken("Record", "RecordMic", "MicRecord")]
    public static Step<CliPipelineState, CliPipelineState> Record(string? args = null)
        => async s =>
        {
            RecordConfig config = ParseRecordArgs(args);

            if (config.Duration <= 0)
            {
                Console.WriteLine("[stt] Error: Duration required. Usage: Record('5') for 5 seconds");
                return s;
            }

            if (!MicrophoneRecorder.IsRecordingAvailable())
            {
                Console.WriteLine("[stt] Error: No audio recorder available. Please install ffmpeg.");
                return s;
            }

            Console.WriteLine($"[stt] 🎤 Recording for {config.Duration} seconds...");

            Result<string, string> result = await MicrophoneRecorder.RecordAsync(
                config.Duration,
                config.OutputPath,
                config.Format);

            result.Match(
                path =>
                {
                    Console.WriteLine($"[stt] ✓ Recorded to: {path}");
                    s.Output = path;
                    s.Context = path;
                },
                error => Console.WriteLine($"[stt] Error: {error}"));

            return s;
        };

    /// <summary>
    /// Record audio from microphone until Enter is pressed.
    /// Usage: Listen() - starts recording, press Enter to stop
    /// Usage: Listen('output=recording.wav')
    /// The recorded file path is stored in s.Output.
    /// </summary>
    [PipelineToken("Listen", "ListenMic", "StartRecording")]
    public static Step<CliPipelineState, CliPipelineState> Listen(string? args = null)
        => async s =>
        {
            RecordConfig config = ParseRecordArgs(args);

            if (!MicrophoneRecorder.IsRecordingAvailable())
            {
                Console.WriteLine("[stt] Error: No audio recorder available. Please install ffmpeg.");
                return s;
            }

            Result<string, string> result = await MicrophoneRecorder.RecordUntilKeyPressAsync(
                config.OutputPath,
                config.Format,
                config.MaxDuration);

            result.Match(
                path =>
                {
                    Console.WriteLine($"[stt] ✓ Recorded to: {path}");
                    s.Output = path;
                    s.Context = path;
                },
                error => Console.WriteLine($"[stt] Error: {error}"));

            return s;
        };

    /// <summary>
    /// Record from microphone and transcribe immediately.
    /// Usage: Dictate('5') - records for 5 seconds and transcribes
    /// Usage: Dictate() - records until Enter, then transcribes
    /// The transcription is stored in s.Output.
    /// </summary>
    [PipelineToken("Dictate", "VoiceInput", "SpeakToText")]
    public static Step<CliPipelineState, CliPipelineState> Dictate(string? args = null)
        => async s =>
        {
            RecordConfig config = ParseRecordArgs(args);

            if (!MicrophoneRecorder.IsRecordingAvailable())
            {
                Console.WriteLine("[stt] Error: No audio recorder available. Please install ffmpeg.");
                return s;
            }

            // Initialize STT if needed (offline-first)
            if (currentService == null)
            {
                currentService = await TryAutoInitializeAsync(s.Trace);
                if (currentService == null)
                {
                    Console.WriteLine("[stt] Error: Not initialized. Install Whisper locally or set OPENAI_API_KEY");
                    return s;
                }
            }

            // Record
            Result<string, string> recordResult;
            if (config.Duration > 0)
            {
                Console.WriteLine($"[stt] 🎤 Recording for {config.Duration} seconds...");
                recordResult = await MicrophoneRecorder.RecordAsync(config.Duration, null, "wav");
            }
            else
            {
                recordResult = await MicrophoneRecorder.RecordUntilKeyPressAsync(null, "wav");
            }

            string? audioPath = null;
            recordResult.Match(
                path => audioPath = path,
                error => Console.WriteLine($"[stt] Recording error: {error}"));

            if (string.IsNullOrEmpty(audioPath))
            {
                return s;
            }

            // Transcribe
            Console.WriteLine("[stt] Transcribing...");

            TranscriptionOptions options = new TranscriptionOptions(
                Language: config.Language,
                ResponseFormat: "json");

            Result<TranscriptionResult, string> transcribeResult = await currentService.TranscribeFileAsync(audioPath, options);

            transcribeResult.Match(
                transcription =>
                {
                    s.Output = transcription.Text;
                    s.Context = transcription.Text;

                    // Show detected language and text
                    Console.WriteLine();
                    Console.WriteLine($"[stt] ✓ Language detected: {transcription.Language ?? "unknown"}");
                    Console.WriteLine($"[stt] ✓ Words detected: {(string.IsNullOrWhiteSpace(transcription.Text) ? "(none - silence or no speech)" : transcription.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length.ToString())}");
                    Console.WriteLine();
                    Console.WriteLine("═══════════════════════════════════════════════════════");
                    Console.WriteLine($"📝 TRANSCRIPTION: {(string.IsNullOrWhiteSpace(transcription.Text) ? "[No speech detected]" : transcription.Text)}");
                    Console.WriteLine("═══════════════════════════════════════════════════════");
                    Console.WriteLine();

                    // Show segments if available
                    if (transcription.Segments?.Count > 0)
                    {
                        Console.WriteLine("[stt] Segments:");
                        foreach (var seg in transcription.Segments)
                        {
                            Console.WriteLine($"  [{seg.Start:F1}s - {seg.End:F1}s] \"{seg.Text}\"");
                        }
                        Console.WriteLine();
                    }
                },
                error => Console.WriteLine($"[stt] Transcription error: {error}"));

            // Clean up temp file
            try
            {
                if (File.Exists(audioPath) && audioPath.Contains(Path.GetTempPath()))
                {
                    File.Delete(audioPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }

            return s;
        };

    /// <summary>
    /// Record from microphone, transcribe, and ask the LLM.
    /// Usage: VoiceChat('5') - records 5 seconds, transcribes, sends to LLM
    /// Usage: VoiceChat() - records until Enter, transcribes, sends to LLM
    /// Combines voice input with LLM processing.
    /// </summary>
    [PipelineToken("VoiceChat", "TalkToAi", "VoiceQuery")]
    public static Step<CliPipelineState, CliPipelineState> VoiceChat(string? args = null)
        => async s =>
        {
            RecordConfig config = ParseRecordArgs(args);

            if (!MicrophoneRecorder.IsRecordingAvailable())
            {
                Console.WriteLine("[stt] Error: No audio recorder available. Please install ffmpeg.");
                return s;
            }

            // Initialize STT if needed (offline-first)
            if (currentService == null)
            {
                currentService = await TryAutoInitializeAsync(s.Trace);
                if (currentService == null)
                {
                    Console.WriteLine("[stt] Error: Not initialized. Install Whisper locally or set OPENAI_API_KEY");
                    return s;
                }
            }

            // Record
            Result<string, string> recordResult;
            if (config.Duration > 0)
            {
                Console.WriteLine($"[stt] 🎤 Recording for {config.Duration} seconds...");
                recordResult = await MicrophoneRecorder.RecordAsync(config.Duration, null, "wav");
            }
            else
            {
                recordResult = await MicrophoneRecorder.RecordUntilKeyPressAsync(null, "wav");
            }

            string? audioPath = null;
            recordResult.Match(
                path => audioPath = path,
                error => Console.WriteLine($"[stt] Recording error: {error}"));

            if (string.IsNullOrEmpty(audioPath))
            {
                return s;
            }

            // Transcribe
            Console.WriteLine("[stt] Transcribing...");

            TranscriptionOptions options = new TranscriptionOptions(Language: config.Language);
            Result<TranscriptionResult, string> transcribeResult = await currentService.TranscribeFileAsync(audioPath, options);

            string? query = null;
            transcribeResult.Match(
                t =>
                {
                    query = t.Text;
                    Console.WriteLine();
                    Console.WriteLine($"[stt] ✓ Language: {t.Language ?? "auto"}");
                    Console.WriteLine($"[stt] ✓ Words detected: {(string.IsNullOrWhiteSpace(t.Text) ? "(none - silence)" : t.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length.ToString())}");
                    Console.WriteLine();
                    Console.WriteLine("═══════════════════════════════════════════════════════");
                    Console.WriteLine($"📝 YOU SAID: {(string.IsNullOrWhiteSpace(t.Text) ? "[No speech detected]" : t.Text)}");
                    Console.WriteLine("═══════════════════════════════════════════════════════");
                    Console.WriteLine();
                },
                error => Console.WriteLine($"[stt] Transcription error: {error}"));

            // Clean up temp file
            try
            {
                if (File.Exists(audioPath) && audioPath.Contains(Path.GetTempPath()))
                {
                    File.Delete(audioPath);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }

            if (string.IsNullOrEmpty(query))
            {
                return s;
            }

            // Ask LLM
            s.Query = query;

            try
            {
                string response = await s.Llm.InnerModel.GenerateTextAsync(query);
                s.Output = response;
                Console.WriteLine($"\n[LLM] {response}\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[stt] LLM error: {ex.Message}");
            }

            return s;
        };

    /// <summary>
    /// List available audio recording devices.
    /// Usage: MicDevices()
    /// </summary>
    [PipelineToken("MicDevices", "ListMics", "AudioDevices")]
    public static Step<CliPipelineState, CliPipelineState> MicDevices(string? args = null)
        => async s =>
        {
            Console.WriteLine("[stt] Audio recording devices:");
            string devices = await MicrophoneRecorder.GetDeviceInfoAsync();
            Console.WriteLine(devices);
            return s;
        };

    #region Helpers

    /// <summary>
    /// Auto-initialize STT service with offline-first approach.
    /// Tries local Whisper first, falls back to OpenAI if available.
    /// </summary>
    private static async Task<ISpeechToTextService?> TryAutoInitializeAsync(bool trace)
    {
        // Try local Whisper first (offline-first)
        LocalWhisperService localService = new LocalWhisperService();
        if (await localService.IsAvailableAsync())
        {
            if (trace)
            {
                Console.WriteLine("[stt] Auto-initialized local Whisper (offline)");
            }

            return localService;
        }

        // Fallback to OpenAI if API key is available
        string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrEmpty(apiKey))
        {
            if (trace)
            {
                Console.WriteLine("[stt] Local Whisper not found, using OpenAI API");
            }

            return new WhisperSpeechToTextService(apiKey);
        }

        return null;
    }

    #endregion

    #region Argument Parsing

    private sealed class SttConfig
    {
        public string Provider { get; set; } = "auto";

        public string? ApiKey { get; set; }

        public string? Endpoint { get; set; }

        public string? Model { get; set; }

        public string? WhisperPath { get; set; }

        public string? ModelPath { get; set; }
    }

    private sealed class TranscribeConfig
    {
        public string? FilePath { get; set; }
        public string? Language { get; set; }
        public string? Format { get; set; }
        public double? Temperature { get; set; }
        public bool Timestamps { get; set; }
        public string? Prompt { get; set; }
    }

    private sealed class RecordConfig
    {
        public int Duration { get; set; }

        public string? OutputPath { get; set; }

        public string Format { get; set; } = "wav";

        public int MaxDuration { get; set; } = 300;

        public string? Language { get; set; }
    }

    private static SttConfig ParseSttConfig(string? args)
    {
        var config = new SttConfig();
        if (string.IsNullOrWhiteSpace(args)) return config;

        // Remove surrounding quotes
        args = args.Trim().Trim('\'', '"');

        foreach (var part in args.Split(';'))
        {
            var trimmed = part.Trim();
            var eqIndex = trimmed.IndexOf('=');

            if (eqIndex > 0)
            {
                var key = trimmed[..eqIndex].Trim().ToLowerInvariant();
                var value = trimmed[(eqIndex + 1)..].Trim();

                switch (key)
                {
                    case "apikey" or "api_key" or "key":
                        config.ApiKey = value;
                        break;
                    case "endpoint" or "url":
                        config.Endpoint = value;
                        break;
                    case "model":
                        config.Model = value;
                        break;
                    case "whisperpath" or "path":
                        config.WhisperPath = value;
                        break;
                    case "modelpath":
                        config.ModelPath = value;
                        break;
                }
            }
            else if (!string.IsNullOrEmpty(trimmed))
            {
                // First non-key=value is provider
                config.Provider = trimmed;
            }
        }

        return config;
    }

    private static TranscribeConfig ParseTranscribeArgs(string? args)
    {
        var config = new TranscribeConfig();
        if (string.IsNullOrWhiteSpace(args)) return config;

        args = args.Trim().Trim('\'', '"');

        foreach (var part in args.Split(';'))
        {
            var trimmed = part.Trim();
            var eqIndex = trimmed.IndexOf('=');

            if (eqIndex > 0)
            {
                var key = trimmed[..eqIndex].Trim().ToLowerInvariant();
                var value = trimmed[(eqIndex + 1)..].Trim();

                switch (key)
                {
                    case "language" or "lang":
                        config.Language = value;
                        break;
                    case "format":
                        config.Format = value;
                        break;
                    case "temperature" or "temp":
                        if (double.TryParse(value, out var temp))
                            config.Temperature = temp;
                        break;
                    case "timestamps":
                        config.Timestamps = value.ToLowerInvariant() is "true" or "1" or "yes";
                        break;
                    case "prompt":
                        config.Prompt = value;
                        break;
                    case "file" or "path":
                        config.FilePath = value;
                        break;
                }
            }
            else if (!string.IsNullOrEmpty(trimmed) && config.FilePath == null)
            {
                // First non-key=value is file path
                config.FilePath = trimmed;
            }
        }

        return config;
    }

    private static RecordConfig ParseRecordArgs(string? args)
    {
        RecordConfig config = new RecordConfig();
        if (string.IsNullOrWhiteSpace(args))
        {
            return config;
        }

        args = args.Trim().Trim('\'', '"');

        foreach (string part in args.Split(';'))
        {
            string trimmed = part.Trim();
            int eqIndex = trimmed.IndexOf('=');

            if (eqIndex > 0)
            {
                string key = trimmed[..eqIndex].Trim().ToLowerInvariant();
                string value = trimmed[(eqIndex + 1)..].Trim();

                switch (key)
                {
                    case "duration" or "time" or "seconds":
                        if (int.TryParse(value, out int dur))
                        {
                            config.Duration = dur;
                        }

                        break;
                    case "output" or "out" or "path" or "file":
                        config.OutputPath = value;
                        break;
                    case "format":
                        config.Format = value;
                        break;
                    case "max" or "maxduration":
                        if (int.TryParse(value, out int maxDur))
                        {
                            config.MaxDuration = maxDur;
                        }

                        break;
                    case "language" or "lang":
                        config.Language = value;
                        break;
                }
            }
            else if (!string.IsNullOrEmpty(trimmed) && int.TryParse(trimmed, out int duration))
            {
                // First non-key=value number is duration
                config.Duration = duration;
            }
        }

        return config;
    }

    #endregion
}

