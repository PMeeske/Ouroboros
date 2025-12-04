// <copyright file="TextToSpeechCliSteps.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using LangChainPipeline.Providers.TextToSpeech;
using MonadicPipeline.CLI;

namespace LangChainPipeline.CLI;

/// <summary>
/// CLI Pipeline steps for text-to-speech operations.
/// Supports OpenAI TTS API and other text-to-speech providers.
/// Note: Use semicolon (;) as separator inside quotes since pipe (|) is the DSL step separator.
/// </summary>
public static class TextToSpeechCliSteps
{
    /// <summary>
    /// Current text-to-speech service instance.
    /// </summary>
    private static ITextToSpeechService? currentService;

    /// <summary>
    /// Initialize TTS service.
    /// Usage: TtsInit('openai;apiKey=sk-xxx;model=tts-1')
    /// Usage: TtsInit() - uses OpenAI with OPENAI_API_KEY env var
    /// Usage: TtsInit('openai;model=tts-1-hd') - use HD model
    /// </summary>
    [PipelineToken("TtsInit", "InitTts", "SpeakInit")]
    public static Step<CliPipelineState, CliPipelineState> TtsInit(string? args = null)
        => s =>
        {
            TtsConfig config = ParseTtsConfig(args);

            try
            {
                string apiKey = config.ApiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                    ?? throw new InvalidOperationException("API key required. Set OPENAI_API_KEY or pass apiKey=...");

                currentService = new OpenAiTextToSpeechService(
                    apiKey,
                    config.Endpoint,
                    config.Model ?? "tts-1");

                if (s.Trace)
                {
                    Console.WriteLine($"[tts] Initialized {currentService.ProviderName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[tts] Failed to initialize: {ex.Message}");
            }

            return Task.FromResult(s);
        };

    /// <summary>
    /// Convert text to speech and save to file.
    /// Usage: Speak('Hello world;output=greeting.mp3')
    /// Usage: Speak('Hello world;output=greeting.mp3;voice=nova;speed=1.2')
    /// The text can also come from s.Output if no text specified.
    /// </summary>
    [PipelineToken("Speak", "TtsSpeak", "TextToSpeech")]
    public static Step<CliPipelineState, CliPipelineState> Speak(string? args = null)
        => async s =>
        {
            SpeakConfig config = ParseSpeakArgs(args);

            // Get text from args or from pipeline output
            string? text = config.Text ?? s.Output;
            if (string.IsNullOrWhiteSpace(text))
            {
                Console.WriteLine("[tts] Error: No text to speak. Provide text or use after a step that sets Output.");
                return s;
            }

            if (string.IsNullOrEmpty(config.OutputPath))
            {
                Console.WriteLine("[tts] Error: Output path required. Usage: Speak('text;output=file.mp3')");
                return s;
            }

            if (currentService == null)
            {
                // Auto-initialize with OpenAI if API key is available
                string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                if (!string.IsNullOrEmpty(apiKey))
                {
                    currentService = new OpenAiTextToSpeechService(apiKey);
                    if (s.Trace)
                    {
                        Console.WriteLine("[tts] Auto-initialized OpenAI TTS");
                    }
                }
                else
                {
                    Console.WriteLine("[tts] Error: Not initialized. Use TtsInit() first or set OPENAI_API_KEY");
                    return s;
                }
            }

            Console.WriteLine($"[tts] Synthesizing: {TruncateText(text, 50)}");
            Console.WriteLine($"[tts] Output: {config.OutputPath}");
            Console.WriteLine($"[tts] Voice: {config.Voice}, Speed: {config.Speed:F1}x");

            TtsVoice voice = ParseVoice(config.Voice);
            TextToSpeechOptions options = new TextToSpeechOptions(
                Voice: voice,
                Speed: config.Speed,
                Format: GetFormatFromPath(config.OutputPath));

            Result<string, string> result = await currentService.SynthesizeToFileAsync(
                text,
                config.OutputPath,
                options);

            result.Match(
                path =>
                {
                    Console.WriteLine($"[tts] ✓ Audio saved to: {path}");
                    s.Output = path;
                },
                error =>
                {
                    Console.WriteLine($"[tts] Error: {error}");
                });

            return s;
        };

    /// <summary>
    /// Speak the LLM response - combines Ask with text-to-speech.
    /// Usage: SpeakAnswer('What is the weather?;output=answer.mp3')
    /// </summary>
    [PipelineToken("SpeakAnswer", "TtsAnswer")]
    public static Step<CliPipelineState, CliPipelineState> SpeakAnswer(string? args = null)
        => async s =>
        {
            SpeakConfig config = ParseSpeakArgs(args);

            if (string.IsNullOrEmpty(config.Text))
            {
                Console.WriteLine("[tts] Error: Question required. Usage: SpeakAnswer('question;output=answer.mp3')");
                return s;
            }

            if (string.IsNullOrEmpty(config.OutputPath))
            {
                Console.WriteLine("[tts] Error: Output path required.");
                return s;
            }

            // Initialize TTS if needed
            if (currentService == null)
            {
                string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                if (!string.IsNullOrEmpty(apiKey))
                {
                    currentService = new OpenAiTextToSpeechService(apiKey);
                }
                else
                {
                    Console.WriteLine("[tts] Error: Not initialized.");
                    return s;
                }
            }

            // Ask the LLM
            Console.WriteLine($"[tts] Question: {config.Text}");

            string response;
            try
            {
                response = await s.Llm.InnerModel.GenerateTextAsync(config.Text);
                Console.WriteLine($"[LLM] {TruncateText(response, 200)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[tts] LLM error: {ex.Message}");
                return s;
            }

            // Speak the response
            TtsVoice voice = ParseVoice(config.Voice);
            TextToSpeechOptions options = new TextToSpeechOptions(
                Voice: voice,
                Speed: config.Speed,
                Format: GetFormatFromPath(config.OutputPath));

            Result<string, string> result = await currentService.SynthesizeToFileAsync(
                response,
                config.OutputPath,
                options);

            result.Match(
                path =>
                {
                    Console.WriteLine($"[tts] ✓ Audio saved to: {path}");
                    s.Output = response;
                    s.Context = path;
                },
                error => Console.WriteLine($"[tts] Error: {error}"));

            return s;
        };

    /// <summary>
    /// List available voices.
    /// Usage: TtsVoices()
    /// </summary>
    [PipelineToken("TtsVoices", "ListVoices")]
    public static Step<CliPipelineState, CliPipelineState> TtsVoices(string? args = null)
        => s =>
        {
            Console.WriteLine("[tts] Available voices:");
            Console.WriteLine("  • alloy   - Neutral, balanced voice");
            Console.WriteLine("  • echo    - Warm, conversational voice");
            Console.WriteLine("  • fable   - Expressive, narrative voice");
            Console.WriteLine("  • onyx    - Deep, authoritative voice");
            Console.WriteLine("  • nova    - Friendly, upbeat voice");
            Console.WriteLine("  • shimmer - Soft, gentle voice");

            return Task.FromResult(s);
        };

    /// <summary>
    /// Speak text directly through speakers (no file output).
    /// Usage: Say('Hello world')
    /// Usage: Say('Hello world;voice=nova;speed=1.2')
    /// Usage: Say() - speaks the current pipeline Output
    /// </summary>
    [PipelineToken("Say", "SayIt", "SayText", "PlaySpeech")]
    public static Step<CliPipelineState, CliPipelineState> Say(string? args = null)
        => async s =>
        {
            SpeakConfig config = ParseSpeakArgs(args);

            // Get text from args or from pipeline output
            string? text = config.Text ?? s.Output;
            if (string.IsNullOrWhiteSpace(text))
            {
                Console.WriteLine("[tts] Error: No text to speak. Provide text or use after a step that sets Output.");
                return s;
            }

            if (currentService == null)
            {
                // Auto-initialize with OpenAI if API key is available
                string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                if (!string.IsNullOrEmpty(apiKey))
                {
                    currentService = new OpenAiTextToSpeechService(apiKey);
                    if (s.Trace)
                    {
                        Console.WriteLine("[tts] Auto-initialized OpenAI TTS");
                    }
                }
                else
                {
                    Console.WriteLine("[tts] Error: Not initialized. Use TtsInit() first or set OPENAI_API_KEY");
                    return s;
                }
            }

            Console.WriteLine($"[tts] Speaking: {TruncateText(text, 50)}");
            Console.WriteLine($"[tts] Voice: {config.Voice ?? "alloy"}, Speed: {config.Speed:F1}x");

            TtsVoice voice = ParseVoice(config.Voice);
            TextToSpeechOptions options = new TextToSpeechOptions(
                Voice: voice,
                Speed: config.Speed,
                Format: "mp3");

            Result<SpeechResult, string> synthesisResult = await currentService.SynthesizeAsync(text, options);

            await synthesisResult.Match(
                async speech =>
                {
                    Console.WriteLine("[tts] 🔊 Playing audio...");
                    Result<bool, string> playResult = await AudioPlayer.PlayAsync(speech);
                    playResult.Match(
                        _ => Console.WriteLine("[tts] ✓ Playback complete"),
                        error => Console.WriteLine($"[tts] Playback error: {error}"));
                },
                error =>
                {
                    Console.WriteLine($"[tts] Error: {error}");
                    return Task.CompletedTask;
                });

            return s;
        };

    /// <summary>
    /// Ask a question and speak the answer directly (no file).
    /// Usage: AskAndSay('What is the weather?')
    /// Usage: AskAndSay('Tell me a joke;voice=fable')
    /// </summary>
    [PipelineToken("AskAndSay", "SayAnswer", "VoiceAnswer")]
    public static Step<CliPipelineState, CliPipelineState> AskAndSay(string? args = null)
        => async s =>
        {
            SpeakConfig config = ParseSpeakArgs(args);

            if (string.IsNullOrEmpty(config.Text))
            {
                Console.WriteLine("[tts] Error: Question required. Usage: AskAndSay('your question')");
                return s;
            }

            // Initialize TTS if needed
            if (currentService == null)
            {
                string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
                if (!string.IsNullOrEmpty(apiKey))
                {
                    currentService = new OpenAiTextToSpeechService(apiKey);
                }
                else
                {
                    Console.WriteLine("[tts] Error: Not initialized.");
                    return s;
                }
            }

            // Ask the LLM
            Console.WriteLine($"[tts] Question: {config.Text}");

            string response;
            try
            {
                response = await s.Llm.InnerModel.GenerateTextAsync(config.Text);
                Console.WriteLine($"[LLM] {TruncateText(response, 200)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[tts] LLM error: {ex.Message}");
                return s;
            }

            // Speak the response directly
            TtsVoice voice = ParseVoice(config.Voice);
            TextToSpeechOptions options = new TextToSpeechOptions(
                Voice: voice,
                Speed: config.Speed,
                Format: "mp3");

            Result<SpeechResult, string> synthesisResult = await currentService.SynthesizeAsync(response, options);

            await synthesisResult.Match(
                async speech =>
                {
                    Console.WriteLine("[tts] 🔊 Playing response...");
                    Result<bool, string> playResult = await AudioPlayer.PlayAsync(speech);
                    playResult.Match(
                        _ =>
                        {
                            Console.WriteLine("[tts] ✓ Playback complete");
                            s.Output = response;
                        },
                        error => Console.WriteLine($"[tts] Playback error: {error}"));
                },
                error =>
                {
                    Console.WriteLine($"[tts] Error: {error}");
                    return Task.CompletedTask;
                });

            return s;
        };

    /// <summary>
    /// Check TTS service status.
    /// Usage: TtsStatus()
    /// </summary>
    [PipelineToken("TtsStatus")]
    public static Step<CliPipelineState, CliPipelineState> TtsStatus(string? args = null)
        => async s =>
        {
            if (currentService == null)
            {
                Console.WriteLine("[tts] Not initialized. Use TtsInit() to configure.");
                return s;
            }

            Console.WriteLine($"[tts] Provider: {currentService.ProviderName}");
            Console.WriteLine($"[tts] Voices: {string.Join(", ", currentService.AvailableVoices)}");
            Console.WriteLine($"[tts] Formats: {string.Join(", ", currentService.SupportedFormats)}");
            Console.WriteLine($"[tts] Max length: {currentService.MaxInputLength} characters");

            bool available = await currentService.IsAvailableAsync();
            Console.WriteLine($"[tts] Available: {(available ? "Yes" : "No")}");

            return s;
        };

    #region Helpers

    private static TtsVoice ParseVoice(string? voiceName)
    {
        if (string.IsNullOrEmpty(voiceName))
        {
            return TtsVoice.Alloy;
        }

        return voiceName.ToLowerInvariant() switch
        {
            "alloy" => TtsVoice.Alloy,
            "echo" => TtsVoice.Echo,
            "fable" => TtsVoice.Fable,
            "onyx" => TtsVoice.Onyx,
            "nova" => TtsVoice.Nova,
            "shimmer" => TtsVoice.Shimmer,
            _ => TtsVoice.Alloy,
        };
    }

    private static string GetFormatFromPath(string path)
    {
        string ext = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return ext switch
        {
            "mp3" or "opus" or "aac" or "flac" or "wav" or "pcm" => ext,
            _ => "mp3",
        };
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (text.Length <= maxLength)
        {
            return text;
        }

        return text[..maxLength] + "...";
    }

    #endregion

    #region Argument Parsing

    private sealed class TtsConfig
    {
        public string? ApiKey { get; set; }

        public string? Endpoint { get; set; }

        public string? Model { get; set; }
    }

    private sealed class SpeakConfig
    {
        public string? Text { get; set; }

        public string? OutputPath { get; set; }

        public string? Voice { get; set; }

        public double Speed { get; set; } = 1.0;
    }

    private static TtsConfig ParseTtsConfig(string? args)
    {
        TtsConfig config = new TtsConfig();
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
                    case "apikey" or "api_key" or "key":
                        config.ApiKey = value;
                        break;
                    case "endpoint" or "url":
                        config.Endpoint = value;
                        break;
                    case "model":
                        config.Model = value;
                        break;
                }
            }
        }

        return config;
    }

    private static SpeakConfig ParseSpeakArgs(string? args)
    {
        SpeakConfig config = new SpeakConfig();
        if (string.IsNullOrWhiteSpace(args))
        {
            return config;
        }

        args = args.Trim().Trim('\'', '"');
        List<string> textParts = new List<string>();

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
                    case "output" or "out" or "path" or "file":
                        config.OutputPath = value;
                        break;
                    case "voice":
                        config.Voice = value;
                        break;
                    case "speed":
                        if (double.TryParse(value, out double speed))
                        {
                            config.Speed = speed;
                        }

                        break;
                    case "text":
                        config.Text = value;
                        break;
                }
            }
            else if (!string.IsNullOrEmpty(trimmed))
            {
                // Accumulate non-key=value parts as text
                textParts.Add(trimmed);
            }
        }

        // Join text parts if text wasn't explicitly set
        if (string.IsNullOrEmpty(config.Text) && textParts.Count > 0)
        {
            config.Text = string.Join("; ", textParts);
        }

        return config;
    }

    #endregion
}
