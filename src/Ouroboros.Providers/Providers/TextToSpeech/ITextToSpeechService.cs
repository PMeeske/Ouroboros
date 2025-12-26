// <copyright file="ITextToSpeechService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Providers.TextToSpeech;

/// <summary>
/// Represents the result of a text-to-speech synthesis.
/// </summary>
/// <param name="AudioData">The synthesized audio data.</param>
/// <param name="Format">The audio format (e.g., "mp3", "wav", "opus").</param>
/// <param name="Duration">Optional duration in seconds.</param>
public sealed record SpeechResult(
    byte[] AudioData,
    string Format,
    double? Duration = null);

/// <summary>
/// Available voices for text-to-speech synthesis.
/// </summary>
public enum TtsVoice
{
    /// <summary>Alloy - neutral, balanced voice.</summary>
    Alloy,

    /// <summary>Echo - warm, conversational voice.</summary>
    Echo,

    /// <summary>Fable - expressive, narrative voice.</summary>
    Fable,

    /// <summary>Onyx - deep, authoritative voice.</summary>
    Onyx,

    /// <summary>Nova - friendly, upbeat voice.</summary>
    Nova,

    /// <summary>Shimmer - soft, gentle voice.</summary>
    Shimmer,
}

/// <summary>
/// Configuration options for text-to-speech synthesis.
/// </summary>
/// <param name="Voice">The voice to use for synthesis.</param>
/// <param name="Speed">Speech speed (0.25 to 4.0, default 1.0).</param>
/// <param name="Format">Output format: "mp3", "opus", "aac", "flac", "wav", "pcm".</param>
/// <param name="Model">TTS model to use (e.g., "tts-1", "tts-1-hd").</param>
public sealed record TextToSpeechOptions(
    TtsVoice Voice = TtsVoice.Alloy,
    double Speed = 1.0,
    string Format = "mp3",
    string? Model = null);

/// <summary>
/// Defines the contract for text-to-speech synthesis services.
/// Supports various providers (OpenAI TTS, Azure, local engines, etc.).
/// </summary>
public interface ITextToSpeechService
{
    /// <summary>
    /// Gets the name of the text-to-speech provider.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets the available voices.
    /// </summary>
    IReadOnlyList<string> AvailableVoices { get; }

    /// <summary>
    /// Gets the supported output formats.
    /// </summary>
    IReadOnlyList<string> SupportedFormats { get; }

    /// <summary>
    /// Gets the maximum input text length.
    /// </summary>
    int MaxInputLength { get; }

    /// <summary>
    /// Synthesizes speech from text and returns the audio data.
    /// </summary>
    /// <param name="text">The text to synthesize.</param>
    /// <param name="options">Optional synthesis options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The synthesized audio result.</returns>
    Task<Result<SpeechResult, string>> SynthesizeAsync(
        string text,
        TextToSpeechOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Synthesizes speech from text and saves to a file.
    /// </summary>
    /// <param name="text">The text to synthesize.</param>
    /// <param name="outputPath">Path to save the audio file.</param>
    /// <param name="options">Optional synthesis options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The path to the saved audio file.</returns>
    Task<Result<string, string>> SynthesizeToFileAsync(
        string text,
        string outputPath,
        TextToSpeechOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Synthesizes speech from text and writes to a stream.
    /// </summary>
    /// <param name="text">The text to synthesize.</param>
    /// <param name="outputStream">Stream to write the audio data.</param>
    /// <param name="options">Optional synthesis options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The audio format of the written data.</returns>
    Task<Result<string, string>> SynthesizeToStreamAsync(
        string text,
        Stream outputStream,
        TextToSpeechOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if the service is available and properly configured.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the service is available.</returns>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}
