// <copyright file="ISpeechToTextService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace Ouroboros.Providers.SpeechToText;

/// <summary>
/// Represents the result of a speech-to-text transcription.
/// </summary>
/// <param name="Text">The transcribed text.</param>
/// <param name="Language">The detected or specified language.</param>
/// <param name="Duration">Duration of the audio in seconds.</param>
/// <param name="Segments">Optional word/segment-level timestamps.</param>
public sealed record TranscriptionResult(
    string Text,
    string? Language = null,
    double? Duration = null,
    IReadOnlyList<TranscriptionSegment>? Segments = null);

/// <summary>
/// Represents a segment of transcribed audio with timing information.
/// </summary>
/// <param name="Text">The text of this segment.</param>
/// <param name="Start">Start time in seconds.</param>
/// <param name="End">End time in seconds.</param>
/// <param name="Confidence">Optional confidence score (0-1).</param>
public sealed record TranscriptionSegment(
    string Text,
    double Start,
    double End,
    double? Confidence = null);

/// <summary>
/// Configuration options for speech-to-text transcription.
/// </summary>
/// <param name="Language">Optional language hint (ISO 639-1 code, e.g., "en", "de", "fr").</param>
/// <param name="ResponseFormat">Response format: "text", "json", "verbose_json", "srt", "vtt".</param>
/// <param name="Temperature">Sampling temperature (0-1). Lower = more deterministic.</param>
/// <param name="TimestampGranularity">Granularity for timestamps: "word", "segment", or null.</param>
/// <param name="Prompt">Optional prompt to guide the transcription style.</param>
public sealed record TranscriptionOptions(
    string? Language = null,
    string ResponseFormat = "json",
    double? Temperature = null,
    string? TimestampGranularity = null,
    string? Prompt = null);

/// <summary>
/// Defines the contract for speech-to-text transcription services.
/// Supports various audio formats and providers (OpenAI Whisper, Azure, local Whisper, etc.).
/// </summary>
public interface ISpeechToTextService
{
    /// <summary>
    /// Gets the name of the speech-to-text provider.
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    /// Gets the supported audio formats.
    /// </summary>
    IReadOnlyList<string> SupportedFormats { get; }

    /// <summary>
    /// Gets the maximum audio file size in bytes.
    /// </summary>
    long MaxFileSizeBytes { get; }

    /// <summary>
    /// Transcribes audio from a file path.
    /// </summary>
    /// <param name="filePath">Path to the audio file.</param>
    /// <param name="options">Optional transcription options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The transcription result.</returns>
    Task<Result<TranscriptionResult, string>> TranscribeFileAsync(
        string filePath,
        TranscriptionOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Transcribes audio from a stream.
    /// </summary>
    /// <param name="audioStream">The audio stream.</param>
    /// <param name="fileName">The file name (used to determine format).</param>
    /// <param name="options">Optional transcription options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The transcription result.</returns>
    Task<Result<TranscriptionResult, string>> TranscribeStreamAsync(
        Stream audioStream,
        string fileName,
        TranscriptionOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Transcribes audio from a byte array.
    /// </summary>
    /// <param name="audioData">The audio data bytes.</param>
    /// <param name="fileName">The file name (used to determine format).</param>
    /// <param name="options">Optional transcription options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The transcription result.</returns>
    Task<Result<TranscriptionResult, string>> TranscribeBytesAsync(
        byte[] audioData,
        string fileName,
        TranscriptionOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Translates audio to English (if supported by the provider).
    /// </summary>
    /// <param name="filePath">Path to the audio file.</param>
    /// <param name="options">Optional transcription options.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The translation result in English.</returns>
    Task<Result<TranscriptionResult, string>> TranslateToEnglishAsync(
        string filePath,
        TranscriptionOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Checks if the service is available and properly configured.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True if the service is available.</returns>
    Task<bool> IsAvailableAsync(CancellationToken ct = default);
}
