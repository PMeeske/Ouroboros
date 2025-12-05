// <copyright file="TextToSpeechTool.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Text.Json;
using LangChainPipeline.Providers.TextToSpeech;

namespace LangChainPipeline.Providers;

/// <summary>
/// Tool for synthesizing speech from text using text-to-speech services.
/// Can be used by AI agents to generate audio output.
/// </summary>
public sealed class TextToSpeechTool : ITool
{
    private readonly ITextToSpeechService service;

    /// <inheritdoc />
    public string Name => "text_to_speech";

    /// <inheritdoc />
    public string Description => "Convert text to speech audio. Saves the audio to a file and returns the file path. Supports mp3, wav, opus, flac formats.";

    /// <inheritdoc />
    public string? JsonSchema => """
        {
            "type": "object",
            "properties": {
                "text": {
                    "type": "string",
                    "description": "The text to convert to speech"
                },
                "output_path": {
                    "type": "string",
                    "description": "Path to save the audio file (e.g., 'output.mp3')"
                },
                "voice": {
                    "type": "string",
                    "description": "Voice to use: 'alloy', 'echo', 'fable', 'onyx', 'nova', 'shimmer'. Default: 'alloy'"
                },
                "speed": {
                    "type": "number",
                    "description": "Speech speed (0.25 to 4.0). Default: 1.0"
                }
            },
            "required": ["text", "output_path"]
        }
        """;

    /// <summary>
    /// Initializes a new instance of the <see cref="TextToSpeechTool"/> class.
    /// </summary>
    /// <param name="service">The text-to-speech service to use.</param>
    public TextToSpeechTool(ITextToSpeechService service)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <summary>
    /// Creates a TextToSpeechTool with OpenAI TTS API.
    /// </summary>
    /// <param name="apiKey">OpenAI API key.</param>
    /// <param name="model">TTS model: "tts-1" (faster) or "tts-1-hd" (higher quality).</param>
    /// <returns>A new TextToSpeechTool instance.</returns>
    public static TextToSpeechTool CreateWithOpenAi(string apiKey, string model = "tts-1")
    {
        return new TextToSpeechTool(new OpenAiTextToSpeechService(apiKey, model: model));
    }

    /// <inheritdoc />
    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            SpeakArgs args = ParseInput(input);

            if (string.IsNullOrEmpty(args.Text))
            {
                return Result<string, string>.Failure("text is required");
            }

            if (string.IsNullOrEmpty(args.OutputPath))
            {
                return Result<string, string>.Failure("output_path is required");
            }

            TtsVoice voice = ParseVoice(args.Voice);
            TextToSpeechOptions options = new TextToSpeechOptions(
                Voice: voice,
                Speed: args.Speed ?? 1.0);

            Result<string, string> result = await this.service.SynthesizeToFileAsync(
                args.Text,
                args.OutputPath,
                options,
                ct);

            return result.Match(
                path => Result<string, string>.Success($"Audio saved to: {path}"),
                error => Result<string, string>.Failure(error));
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Speech synthesis failed: {ex.Message}");
        }
    }

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

    private static SpeakArgs ParseInput(string input)
    {
        SpeakArgs args = new SpeakArgs();

        try
        {
            using JsonDocument doc = JsonDocument.Parse(input);
            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("text", out JsonElement textEl))
            {
                args.Text = textEl.GetString();
            }

            if (root.TryGetProperty("output_path", out JsonElement pathEl))
            {
                args.OutputPath = pathEl.GetString();
            }
            else if (root.TryGetProperty("outputPath", out JsonElement pathEl2))
            {
                args.OutputPath = pathEl2.GetString();
            }
            else if (root.TryGetProperty("path", out JsonElement pathEl3))
            {
                args.OutputPath = pathEl3.GetString();
            }

            if (root.TryGetProperty("voice", out JsonElement voiceEl))
            {
                args.Voice = voiceEl.GetString();
            }

            if (root.TryGetProperty("speed", out JsonElement speedEl))
            {
                args.Speed = speedEl.GetDouble();
            }
        }
        catch
        {
            // Plain text - can't parse without output path
            args.Text = input.Trim();
        }

        return args;
    }

    private sealed class SpeakArgs
    {
        public string? Text { get; set; }

        public string? OutputPath { get; set; }

        public string? Voice { get; set; }

        public double? Speed { get; set; }
    }
}

/// <summary>
/// Extension methods for registering text-to-speech tools.
/// </summary>
public static class TextToSpeechToolExtensions
{
    /// <summary>
    /// Adds text-to-speech tool to the registry using OpenAI TTS.
    /// </summary>
    /// <param name="registry">The tool registry.</param>
    /// <param name="apiKey">OpenAI API key. If null, uses OPENAI_API_KEY environment variable.</param>
    /// <param name="model">TTS model: "tts-1" or "tts-1-hd".</param>
    /// <returns>A new registry with the text-to-speech tool added.</returns>
    public static ToolRegistry WithTextToSpeech(this ToolRegistry registry, string? apiKey = null, string model = "tts-1")
    {
        string? key = apiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrEmpty(key))
        {
            throw new InvalidOperationException("OpenAI API key required. Set OPENAI_API_KEY or pass apiKey parameter.");
        }

        return registry.WithTool(TextToSpeechTool.CreateWithOpenAi(key, model));
    }

    /// <summary>
    /// Adds text-to-speech tool to the registry with a custom service.
    /// </summary>
    /// <param name="registry">The tool registry.</param>
    /// <param name="service">The text-to-speech service to use.</param>
    /// <returns>A new registry with the text-to-speech tool added.</returns>
    public static ToolRegistry WithTextToSpeech(this ToolRegistry registry, ITextToSpeechService service)
    {
        return registry.WithTool(new TextToSpeechTool(service));
    }

    /// <summary>
    /// Adds both speech-to-text and text-to-speech tools for bidirectional audio support.
    /// </summary>
    /// <param name="registry">The tool registry.</param>
    /// <param name="apiKey">OpenAI API key. If null, uses OPENAI_API_KEY environment variable.</param>
    /// <returns>A new registry with both audio tools added.</returns>
    public static ToolRegistry WithBidirectionalSpeech(this ToolRegistry registry, string? apiKey = null)
    {
        return registry
            .WithSpeechToText(apiKey)
            .WithTextToSpeech(apiKey);
    }
}
