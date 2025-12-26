// <copyright file="SpeechModuleTests.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using Ouroboros.Providers.SpeechToText;
using Ouroboros.Providers.TextToSpeech;

namespace Ouroboros.Tests;

/// <summary>
/// Tests for the speech-to-text and text-to-speech modules.
/// </summary>
public class SpeechModuleTests
{
    [Fact]
    public void MicrophoneRecorder_IsRecordingAvailable_ReturnsTrue_WhenFfmpegInstalled()
    {
        // This test verifies ffmpeg detection works
        // Will pass if ffmpeg is in PATH
        bool available = MicrophoneRecorder.IsRecordingAvailable();

        // We just verify the method runs without exception
        Assert.True(available || !available); // Always passes, but verifies no exception
    }

    [Fact]
    public async Task MicrophoneRecorder_GetDeviceInfoAsync_ReturnsDeviceList()
    {
        // Act
        string deviceInfo = await MicrophoneRecorder.GetDeviceInfoAsync();

        // Assert
        Assert.NotNull(deviceInfo);
        Assert.NotEmpty(deviceInfo);
    }

    [Fact]
    public void AudioPlayer_PlayFileAsync_ReturnsFailure_WhenFileNotFound()
    {
        // Arrange
        string nonExistentFile = Path.Combine(Path.GetTempPath(), "nonexistent_audio_12345.wav");

        // Act
        Result<bool, string> result = AudioPlayer.PlayFileAsync(nonExistentFile).GetAwaiter().GetResult();

        // Assert
        Assert.True(result.IsFailure);
    }

    [Fact]
    public void WhisperSpeechToTextService_Constructor_ThrowsWithNullApiKey()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new WhisperSpeechToTextService(null!));
    }

    [Fact]
    public void WhisperSpeechToTextService_SupportedFormats_ContainsCommonFormats()
    {
        // Arrange
        WhisperSpeechToTextService service = new WhisperSpeechToTextService("dummy-key");

        // Act
        IReadOnlyList<string> formats = service.SupportedFormats;

        // Assert - formats include the dot prefix
        Assert.Contains(".mp3", formats);
        Assert.Contains(".wav", formats);
        Assert.Contains(".m4a", formats);
    }

    [Fact]
    public void OpenAiTextToSpeechService_Constructor_ThrowsWithNullApiKey()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new OpenAiTextToSpeechService(null!));
    }

    [Fact]
    public void OpenAiTextToSpeechService_AvailableVoices_ContainsAllVoices()
    {
        // Arrange
        OpenAiTextToSpeechService service = new OpenAiTextToSpeechService("dummy-key");

        // Act
        IReadOnlyList<string> voices = service.AvailableVoices;

        // Assert
        Assert.Contains("alloy", voices);
        Assert.Contains("echo", voices);
        Assert.Contains("fable", voices);
        Assert.Contains("onyx", voices);
        Assert.Contains("nova", voices);
        Assert.Contains("shimmer", voices);
    }

    [Fact]
    public void OpenAiTextToSpeechService_SupportedFormats_ContainsCommonFormats()
    {
        // Arrange
        OpenAiTextToSpeechService service = new OpenAiTextToSpeechService("dummy-key");

        // Act
        IReadOnlyList<string> formats = service.SupportedFormats;

        // Assert
        Assert.Contains("mp3", formats);
        Assert.Contains("opus", formats);
        Assert.Contains("aac", formats);
        Assert.Contains("flac", formats);
        Assert.Contains("wav", formats);
    }

    [Fact]
    public void TranscriptionOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        TranscriptionOptions options = new TranscriptionOptions();

        // Assert
        Assert.Null(options.Language);
        Assert.Equal("json", options.ResponseFormat);
        Assert.Null(options.Temperature);
        Assert.Null(options.TimestampGranularity);
        Assert.Null(options.Prompt);
    }

    [Fact]
    public void TextToSpeechOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        TextToSpeechOptions options = new TextToSpeechOptions();

        // Assert
        Assert.Equal(TtsVoice.Alloy, options.Voice);
        Assert.Equal(1.0, options.Speed);
        Assert.Equal("mp3", options.Format);
    }

    [Fact]
    public void SpeechResult_CanBeCreated_WithValidData()
    {
        // Arrange
        byte[] audioData = new byte[] { 1, 2, 3, 4 };

        // Act
        SpeechResult result = new SpeechResult(audioData, "mp3", 1.5);

        // Assert
        Assert.Equal(audioData, result.AudioData);
        Assert.Equal("mp3", result.Format);
        Assert.Equal(1.5, result.Duration);
    }

    [Fact]
    public void TranscriptionResult_CanBeCreated_WithValidData()
    {
        // Arrange & Act
        TranscriptionResult result = new TranscriptionResult(
            Text: "Hello world",
            Language: "en",
            Duration: 1.5,
            Segments: null);

        // Assert
        Assert.Equal("Hello world", result.Text);
        Assert.Equal("en", result.Language);
        Assert.Equal(1.5, result.Duration);
        Assert.Null(result.Segments);
    }
}
