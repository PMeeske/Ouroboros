// <copyright file="WhisperSpeechToTextService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ouroboros.Providers.SpeechToText;

/// <summary>
/// OpenAI Whisper API-based speech-to-text service.
/// Supports transcription and translation of audio files.
/// </summary>
public sealed class WhisperSpeechToTextService : ISpeechToTextService, IDisposable
{
    private readonly HttpClient httpClient;
    private readonly string apiKey;
    private readonly string endpoint;
    private readonly string model;
    private readonly bool ownsClient;

    /// <summary>
    /// Supported audio formats for Whisper API.
    /// </summary>
    private static readonly string[] SupportedAudioFormats =
    [
        ".flac", ".m4a", ".mp3", ".mp4", ".mpeg", ".mpga", ".oga", ".ogg", ".wav", ".webm",
    ];

    /// <inheritdoc/>
    public string ProviderName => "OpenAI Whisper";

    /// <inheritdoc/>
    public IReadOnlyList<string> SupportedFormats => SupportedAudioFormats;

    /// <inheritdoc/>
    public long MaxFileSizeBytes => 25 * 1024 * 1024; // 25 MB limit

    /// <summary>
    /// Initializes a new instance of the <see cref="WhisperSpeechToTextService"/> class.
    /// </summary>
    /// <param name="apiKey">OpenAI API key.</param>
    /// <param name="endpoint">API endpoint (defaults to OpenAI).</param>
    /// <param name="model">Whisper model to use (defaults to whisper-1).</param>
    /// <param name="httpClient">Optional HTTP client.</param>
    public WhisperSpeechToTextService(
        string apiKey,
        string? endpoint = null,
        string model = "whisper-1",
        HttpClient? httpClient = null)
    {
        this.apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        this.endpoint = endpoint ?? "https://api.openai.com/v1";
        this.model = model;
        this.ownsClient = httpClient == null;
        this.httpClient = httpClient ?? new HttpClient();
    }

    /// <inheritdoc/>
    public async Task<Result<TranscriptionResult, string>> TranscribeFileAsync(
        string filePath,
        TranscriptionOptions? options = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            return Result<TranscriptionResult, string>.Failure($"File not found: {filePath}");
        }

        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        if (!SupportedAudioFormats.Contains(extension))
        {
            return Result<TranscriptionResult, string>.Failure(
                $"Unsupported audio format: {extension}. Supported formats: {string.Join(", ", SupportedAudioFormats)}");
        }

        FileInfo fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > this.MaxFileSizeBytes)
        {
            return Result<TranscriptionResult, string>.Failure(
                $"File too large: {fileInfo.Length / (1024 * 1024):F2} MB. Maximum size: {this.MaxFileSizeBytes / (1024 * 1024)} MB");
        }

        try
        {
            await using FileStream stream = File.OpenRead(filePath);
            return await this.TranscribeStreamAsync(stream, Path.GetFileName(filePath), options, ct);
        }
        catch (Exception ex)
        {
            return Result<TranscriptionResult, string>.Failure($"Failed to read file: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<TranscriptionResult, string>> TranscribeStreamAsync(
        Stream audioStream,
        string fileName,
        TranscriptionOptions? options = null,
        CancellationToken ct = default)
    {
        return await this.SendTranscriptionRequestAsync(
            $"{this.endpoint}/audio/transcriptions",
            audioStream,
            fileName,
            options,
            ct);
    }

    /// <inheritdoc/>
    public async Task<Result<TranscriptionResult, string>> TranscribeBytesAsync(
        byte[] audioData,
        string fileName,
        TranscriptionOptions? options = null,
        CancellationToken ct = default)
    {
        await using MemoryStream stream = new MemoryStream(audioData);
        return await this.TranscribeStreamAsync(stream, fileName, options, ct);
    }

    /// <inheritdoc/>
    public async Task<Result<TranscriptionResult, string>> TranslateToEnglishAsync(
        string filePath,
        TranscriptionOptions? options = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            return Result<TranscriptionResult, string>.Failure($"File not found: {filePath}");
        }

        try
        {
            await using FileStream stream = File.OpenRead(filePath);
            return await this.SendTranscriptionRequestAsync(
                $"{this.endpoint}/audio/translations",
                stream,
                Path.GetFileName(filePath),
                options,
                ct);
        }
        catch (Exception ex)
        {
            return Result<TranscriptionResult, string>.Failure($"Failed to read file: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(this.apiKey))
        {
            return false;
        }

        try
        {
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, $"{this.endpoint}/models");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.apiKey);

            HttpResponseMessage response = await this.httpClient.SendAsync(request, ct);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Disposes the HTTP client if owned.
    /// </summary>
    public void Dispose()
    {
        if (this.ownsClient)
        {
            this.httpClient.Dispose();
        }
    }

    private static Result<TranscriptionResult, string> ParseVerboseJsonResponse(string json)
    {
        try
        {
            WhisperVerboseResponse? response = JsonSerializer.Deserialize<WhisperVerboseResponse>(json);
            if (response == null)
            {
                return Result<TranscriptionResult, string>.Failure("Failed to parse Whisper response");
            }

            List<TranscriptionSegment>? segments = response.Segments?
                .Select(s => new TranscriptionSegment(
                    s.Text ?? string.Empty,
                    s.Start,
                    s.End,
                    null))
                .ToList();

            return Result<TranscriptionResult, string>.Success(
                new TranscriptionResult(
                    response.Text ?? string.Empty,
                    response.Language,
                    response.Duration,
                    segments));
        }
        catch (JsonException ex)
        {
            return Result<TranscriptionResult, string>.Failure($"Failed to parse response: {ex.Message}");
        }
    }

    private static string GetMimeType(string extension)
    {
        return extension.ToLowerInvariant() switch
        {
            ".mp3" => "audio/mpeg",
            ".mp4" or ".m4a" => "audio/mp4",
            ".wav" => "audio/wav",
            ".webm" => "audio/webm",
            ".ogg" or ".oga" => "audio/ogg",
            ".flac" => "audio/flac",
            ".mpeg" or ".mpga" => "audio/mpeg",
            _ => "application/octet-stream",
        };
    }

    private async Task<Result<TranscriptionResult, string>> SendTranscriptionRequestAsync(
        string url,
        Stream audioStream,
        string fileName,
        TranscriptionOptions? options,
        CancellationToken ct)
    {
        try
        {
            using MultipartFormDataContent content = new MultipartFormDataContent();

            // Add the audio file
            StreamContent streamContent = new StreamContent(audioStream);
            string mimeType = GetMimeType(Path.GetExtension(fileName));
            streamContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
            content.Add(streamContent, "file", fileName);

            // Add model
            content.Add(new StringContent(this.model), "model");

            // Add optional parameters
            options ??= new TranscriptionOptions();

            if (!string.IsNullOrEmpty(options.Language))
            {
                content.Add(new StringContent(options.Language), "language");
            }

            if (!string.IsNullOrEmpty(options.Prompt))
            {
                content.Add(new StringContent(options.Prompt), "prompt");
            }

            if (options.Temperature.HasValue)
            {
                content.Add(new StringContent(options.Temperature.Value.ToString("F2")), "temperature");
            }

            // Request verbose_json for rich response
            string responseFormat = options.ResponseFormat == "text" ? "text" : "verbose_json";
            content.Add(new StringContent(responseFormat), "response_format");

            if (!string.IsNullOrEmpty(options.TimestampGranularity))
            {
                content.Add(new StringContent(options.TimestampGranularity), "timestamp_granularities[]");
            }

            // Send request
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.apiKey);
            request.Content = content;

            HttpResponseMessage response = await this.httpClient.SendAsync(request, ct);
            string responseText = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                return Result<TranscriptionResult, string>.Failure(
                    $"Whisper API error ({response.StatusCode}): {responseText}");
            }

            // Parse response
            if (responseFormat == "text")
            {
                return Result<TranscriptionResult, string>.Success(
                    new TranscriptionResult(responseText.Trim()));
            }

            return ParseVerboseJsonResponse(responseText);
        }
        catch (Exception ex)
        {
            return Result<TranscriptionResult, string>.Failure($"Transcription failed: {ex.Message}");
        }
    }

    private sealed class WhisperVerboseResponse
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("language")]
        public string? Language { get; set; }

        [JsonPropertyName("duration")]
        public double? Duration { get; set; }

        [JsonPropertyName("segments")]
        public List<WhisperSegment>? Segments { get; set; }
    }

    private sealed class WhisperSegment
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("start")]
        public double Start { get; set; }

        [JsonPropertyName("end")]
        public double End { get; set; }

        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }
}
