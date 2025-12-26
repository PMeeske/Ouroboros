// <copyright file="OpenAiTextToSpeechService.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Ouroboros.Providers.TextToSpeech;

/// <summary>
/// OpenAI TTS API-based text-to-speech service.
/// Supports high-quality speech synthesis with multiple voices.
/// </summary>
public sealed class OpenAiTextToSpeechService : ITextToSpeechService, IDisposable
{
    private readonly HttpClient httpClient;
    private readonly string apiKey;
    private readonly string endpoint;
    private readonly string defaultModel;
    private readonly bool ownsClient;

    private static readonly string[] Voices = ["alloy", "echo", "fable", "onyx", "nova", "shimmer"];
    private static readonly string[] Formats = ["mp3", "opus", "aac", "flac", "wav", "pcm"];

    /// <inheritdoc/>
    public string ProviderName => "OpenAI TTS";

    /// <inheritdoc/>
    public IReadOnlyList<string> AvailableVoices => Voices;

    /// <inheritdoc/>
    public IReadOnlyList<string> SupportedFormats => Formats;

    /// <inheritdoc/>
    public int MaxInputLength => 4096;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAiTextToSpeechService"/> class.
    /// </summary>
    /// <param name="apiKey">OpenAI API key.</param>
    /// <param name="endpoint">API endpoint (defaults to OpenAI).</param>
    /// <param name="model">TTS model to use (defaults to tts-1).</param>
    /// <param name="httpClient">Optional HTTP client.</param>
    public OpenAiTextToSpeechService(
        string apiKey,
        string? endpoint = null,
        string model = "tts-1",
        HttpClient? httpClient = null)
    {
        this.apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        this.endpoint = endpoint ?? "https://api.openai.com/v1";
        this.defaultModel = model;
        this.ownsClient = httpClient == null;
        this.httpClient = httpClient ?? new HttpClient();
    }

    /// <inheritdoc/>
    public async Task<Result<SpeechResult, string>> SynthesizeAsync(
        string text,
        TextToSpeechOptions? options = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Result<SpeechResult, string>.Failure("Text cannot be empty");
        }

        if (text.Length > this.MaxInputLength)
        {
            return Result<SpeechResult, string>.Failure(
                $"Text too long: {text.Length} characters. Maximum: {this.MaxInputLength}");
        }

        options ??= new TextToSpeechOptions();

        try
        {
            string voiceName = options.Voice.ToString().ToLowerInvariant();
            string format = options.Format.ToLowerInvariant();
            string model = options.Model ?? this.defaultModel;

            // Validate speed
            double speed = Math.Clamp(options.Speed, 0.25, 4.0);

            object requestBody = new
            {
                model = model,
                input = text,
                voice = voiceName,
                response_format = format,
                speed = speed,
            };

            string jsonBody = JsonSerializer.Serialize(requestBody);
            using StringContent content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, $"{this.endpoint}/audio/speech");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", this.apiKey);
            request.Content = content;

            HttpResponseMessage response = await this.httpClient.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                string errorText = await response.Content.ReadAsStringAsync(ct);
                return Result<SpeechResult, string>.Failure(
                    $"TTS API error ({response.StatusCode}): {errorText}");
            }

            byte[] audioData = await response.Content.ReadAsByteArrayAsync(ct);

            return Result<SpeechResult, string>.Success(
                new SpeechResult(audioData, format));
        }
        catch (Exception ex)
        {
            return Result<SpeechResult, string>.Failure($"Speech synthesis failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public async Task<Result<string, string>> SynthesizeToFileAsync(
        string text,
        string outputPath,
        TextToSpeechOptions? options = null,
        CancellationToken ct = default)
    {
        // Infer format from file extension if not specified
        if (options == null || string.IsNullOrEmpty(options.Format))
        {
            string extension = Path.GetExtension(outputPath).TrimStart('.').ToLowerInvariant();
            if (Formats.Contains(extension))
            {
                options = (options ?? new TextToSpeechOptions()) with { Format = extension };
            }
        }

        Result<SpeechResult, string> result = await this.SynthesizeAsync(text, options, ct);

        return result.Match(
            speech =>
            {
                try
                {
                    string? directory = Path.GetDirectoryName(outputPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.WriteAllBytes(outputPath, speech.AudioData);
                    return Result<string, string>.Success(outputPath);
                }
                catch (Exception ex)
                {
                    return Result<string, string>.Failure($"Failed to save audio file: {ex.Message}");
                }
            },
            error => Result<string, string>.Failure(error));
    }

    /// <inheritdoc/>
    public async Task<Result<string, string>> SynthesizeToStreamAsync(
        string text,
        Stream outputStream,
        TextToSpeechOptions? options = null,
        CancellationToken ct = default)
    {
        Result<SpeechResult, string> result = await this.SynthesizeAsync(text, options, ct);

        return result.Match(
            speech =>
            {
                try
                {
                    outputStream.Write(speech.AudioData, 0, speech.AudioData.Length);
                    return Result<string, string>.Success(speech.Format);
                }
                catch (Exception ex)
                {
                    return Result<string, string>.Failure($"Failed to write audio stream: {ex.Message}");
                }
            },
            error => Result<string, string>.Failure(error));
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
}
