// <copyright file="SpeechToTextTool.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Text.Json;
using LangChainPipeline.Providers.SpeechToText;

namespace LangChainPipeline.Providers;

/// <summary>
/// Tool for transcribing audio files to text using speech-to-text services.
/// Can be used by AI agents to process audio input.
/// </summary>
public sealed class SpeechToTextTool : ITool
{
    private readonly ISpeechToTextService service;

    /// <inheritdoc />
    public string Name => "transcribe_audio";

    /// <inheritdoc />
    public string Description => "Transcribe an audio file to text. Supports mp3, wav, m4a, flac, ogg formats. Returns the transcribed text.";

    /// <inheritdoc />
    public string? JsonSchema => """
        {
            "type": "object",
            "properties": {
                "file_path": {
                    "type": "string",
                    "description": "Path to the audio file to transcribe"
                },
                "language": {
                    "type": "string",
                    "description": "Optional ISO 639-1 language code (e.g., 'en', 'de', 'fr'). If not provided, language is auto-detected."
                },
                "translate_to_english": {
                    "type": "boolean",
                    "description": "If true, translates non-English audio to English instead of transcribing in the original language"
                }
            },
            "required": ["file_path"]
        }
        """;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpeechToTextTool"/> class.
    /// </summary>
    /// <param name="service">The speech-to-text service to use.</param>
    public SpeechToTextTool(ISpeechToTextService service)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
    }

    /// <summary>
    /// Creates a SpeechToTextTool with OpenAI Whisper API.
    /// </summary>
    /// <param name="apiKey">OpenAI API key.</param>
    /// <returns>A new SpeechToTextTool instance.</returns>
    public static SpeechToTextTool CreateWithWhisper(string apiKey)
    {
        return new SpeechToTextTool(new WhisperSpeechToTextService(apiKey));
    }

    /// <summary>
    /// Creates a SpeechToTextTool with local Whisper.
    /// </summary>
    /// <param name="modelSize">Model size: tiny, base, small, medium, large.</param>
    /// <returns>A new SpeechToTextTool instance.</returns>
    public static SpeechToTextTool CreateWithLocalWhisper(string modelSize = "small")
    {
        return new SpeechToTextTool(new LocalWhisperService(modelSize: modelSize));
    }

    /// <inheritdoc />
    public async Task<Result<string, string>> InvokeAsync(string input, CancellationToken ct = default)
    {
        try
        {
            TranscribeArgs args = ParseInput(input);

            if (string.IsNullOrEmpty(args.FilePath))
            {
                return Result<string, string>.Failure("file_path is required");
            }

            if (!File.Exists(args.FilePath))
            {
                return Result<string, string>.Failure($"File not found: {args.FilePath}");
            }

            TranscriptionOptions options = new TranscriptionOptions(Language: args.Language);

            Result<TranscriptionResult, string> result;

            if (args.TranslateToEnglish)
            {
                result = await this.service.TranslateToEnglishAsync(args.FilePath, options, ct);
            }
            else
            {
                result = await this.service.TranscribeFileAsync(args.FilePath, options, ct);
            }

            return result.Match(
                transcription => Result<string, string>.Success(transcription.Text),
                error => Result<string, string>.Failure(error));
        }
        catch (Exception ex)
        {
            return Result<string, string>.Failure($"Transcription failed: {ex.Message}");
        }
    }

    private static TranscribeArgs ParseInput(string input)
    {
        TranscribeArgs args = new TranscribeArgs();

        // Try JSON parse first
        try
        {
            using JsonDocument doc = JsonDocument.Parse(input);
            JsonElement root = doc.RootElement;

            if (root.TryGetProperty("file_path", out JsonElement fp))
            {
                args.FilePath = fp.GetString();
            }
            else if (root.TryGetProperty("filePath", out JsonElement fp2))
            {
                args.FilePath = fp2.GetString();
            }
            else if (root.TryGetProperty("path", out JsonElement p))
            {
                args.FilePath = p.GetString();
            }

            if (root.TryGetProperty("language", out JsonElement lang))
            {
                args.Language = lang.GetString();
            }

            if (root.TryGetProperty("translate_to_english", out JsonElement trans))
            {
                args.TranslateToEnglish = trans.GetBoolean();
            }
            else if (root.TryGetProperty("translate", out JsonElement trans2))
            {
                args.TranslateToEnglish = trans2.GetBoolean();
            }
        }
        catch
        {
            // Plain text - treat as file path
            args.FilePath = input.Trim().Trim('"', '\'');
        }

        return args;
    }

    private sealed class TranscribeArgs
    {
        public string? FilePath { get; set; }

        public string? Language { get; set; }

        public bool TranslateToEnglish { get; set; }
    }
}

/// <summary>
/// Extension methods for registering speech-to-text tools.
/// </summary>
public static class SpeechToTextToolExtensions
{
    /// <summary>
    /// Adds speech-to-text tool to the registry using OpenAI Whisper.
    /// </summary>
    /// <param name="registry">The tool registry.</param>
    /// <param name="apiKey">OpenAI API key. If null, uses OPENAI_API_KEY environment variable.</param>
    /// <returns>A new registry with the speech-to-text tool added.</returns>
    public static ToolRegistry WithSpeechToText(this ToolRegistry registry, string? apiKey = null)
    {
        string? key = apiKey ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrEmpty(key))
        {
            throw new InvalidOperationException("OpenAI API key required. Set OPENAI_API_KEY or pass apiKey parameter.");
        }

        return registry.WithTool(SpeechToTextTool.CreateWithWhisper(key));
    }

    /// <summary>
    /// Adds speech-to-text tool to the registry using local Whisper.
    /// </summary>
    /// <param name="registry">The tool registry.</param>
    /// <param name="modelSize">Whisper model size: tiny, base, small, medium, large.</param>
    /// <returns>A new registry with the local speech-to-text tool added.</returns>
    public static ToolRegistry WithLocalSpeechToText(this ToolRegistry registry, string modelSize = "small")
    {
        return registry.WithTool(SpeechToTextTool.CreateWithLocalWhisper(modelSize));
    }

    /// <summary>
    /// Adds speech-to-text tool to the registry with a custom service.
    /// </summary>
    /// <param name="registry">The tool registry.</param>
    /// <param name="service">The speech-to-text service to use.</param>
    /// <returns>A new registry with the speech-to-text tool added.</returns>
    public static ToolRegistry WithSpeechToText(this ToolRegistry registry, ISpeechToTextService service)
    {
        return registry.WithTool(new SpeechToTextTool(service));
    }
}
