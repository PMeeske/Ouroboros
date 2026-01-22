using Ouroboros.Providers;
using Ouroboros.Providers.SpeechToText;
using Ouroboros.Providers.TextToSpeech;

namespace Ouroboros.Easy;

/// <summary>
/// Voice-enabled pipeline builder with speech-to-text and text-to-speech capabilities.
/// Supports multiple languages including German.
/// </summary>
public sealed class VoicePipeline
{
    private readonly Pipeline _basePipeline;
    private ISpeechToTextService? _sttService;
    private ITextToSpeechService? _ttsService;
    private string _language = "en"; // Default to English
    private string? _voiceName;
    private bool _enableVoiceInput = false;
    private bool _enableVoiceOutput = false;

    private VoicePipeline(Pipeline basePipeline)
    {
        _basePipeline = basePipeline;
    }

    /// <summary>
    /// Creates a new voice-enabled pipeline builder.
    /// </summary>
    /// <returns>A new VoicePipeline builder for fluent configuration.</returns>
    public static VoicePipeline Create()
    {
        return new VoicePipeline(Pipeline.Create());
    }

    /// <summary>
    /// Sets the topic using voice input (speech-to-text).
    /// </summary>
    /// <param name="audioFilePath">Path to the audio file to transcribe.</param>
    /// <returns>The voice pipeline builder for method chaining.</returns>
    public async Task<VoicePipeline> AboutFromVoiceAsync(string audioFilePath)
    {
        if (_sttService == null)
        {
            throw new InvalidOperationException("Speech-to-text service must be configured using WithSpeechToText()");
        }

        TranscriptionOptions options = new TranscriptionOptions(Language: _language);
        Result<TranscriptionResult, string> result = await _sttService.TranscribeFileAsync(audioFilePath, options);
        
        if (result.IsSuccess)
        {
            _basePipeline.About(result.Value.Text);
        }
        else
        {
            throw new InvalidOperationException($"Transcription failed: {result.Error}");
        }
        
        return this;
    }

    /// <summary>
    /// Sets the topic or question for the pipeline to process.
    /// </summary>
    /// <param name="topic">The topic, question, or prompt to process.</param>
    /// <returns>The voice pipeline builder for method chaining.</returns>
    public VoicePipeline About(string topic)
    {
        _basePipeline.About(topic);
        return this;
    }

    /// <summary>
    /// Enables the draft stage.
    /// </summary>
    public VoicePipeline Draft()
    {
        _basePipeline.Draft();
        return this;
    }

    /// <summary>
    /// Enables the critique stage.
    /// </summary>
    public VoicePipeline Critique()
    {
        _basePipeline.Critique();
        return this;
    }

    /// <summary>
    /// Enables the improve stage.
    /// </summary>
    public VoicePipeline Improve()
    {
        _basePipeline.Improve();
        return this;
    }

    /// <summary>
    /// Enables the summarize stage.
    /// </summary>
    public VoicePipeline Summarize()
    {
        _basePipeline.Summarize();
        return this;
    }

    /// <summary>
    /// Sets the language model to use.
    /// </summary>
    /// <param name="modelName">The name of the model (e.g., "llama3", "mistral", "phi3").</param>
    public VoicePipeline WithModel(string modelName)
    {
        _basePipeline.WithModel(modelName);
        return this;
    }

    /// <summary>
    /// Sets the temperature for the language model.
    /// </summary>
    /// <param name="temperature">Temperature value between 0.0 and 1.0.</param>
    public VoicePipeline WithTemperature(double temperature)
    {
        _basePipeline.WithTemperature(temperature);
        return this;
    }

    /// <summary>
    /// Sets the language for voice input/output.
    /// </summary>
    /// <param name="languageCode">ISO 639-1 language code (e.g., "en" for English, "de" for German, "fr" for French).</param>
    public VoicePipeline WithLanguage(string languageCode)
    {
        _language = languageCode ?? throw new ArgumentNullException(nameof(languageCode));
        return this;
    }

    /// <summary>
    /// Sets the voice for text-to-speech output.
    /// </summary>
    /// <param name="voiceName">Voice name (e.g., "alloy", "echo", "fable", "onyx", "nova", "shimmer" for OpenAI, or "de-DE-KatjaNeural" for German Cortana-like voice).</param>
    public VoicePipeline WithVoice(string voiceName)
    {
        _voiceName = voiceName ?? throw new ArgumentNullException(nameof(voiceName));
        return this;
    }

    /// <summary>
    /// Configures speech-to-text service.
    /// </summary>
    /// <param name="sttService">The speech-to-text service to use (e.g., WhisperSpeechToTextService, LocalWhisperService).</param>
    public VoicePipeline WithSpeechToText(ISpeechToTextService sttService)
    {
        _sttService = sttService ?? throw new ArgumentNullException(nameof(sttService));
        _enableVoiceInput = true;
        return this;
    }

    /// <summary>
    /// Configures text-to-speech service.
    /// </summary>
    /// <param name="ttsService">The text-to-speech service to use (e.g., OpenAiTextToSpeechService, AzureNeuralTtsService, LocalWindowsTtsService).</param>
    public VoicePipeline WithTextToSpeech(ITextToSpeechService ttsService)
    {
        _ttsService = ttsService ?? throw new ArgumentNullException(nameof(ttsService));
        _enableVoiceOutput = true;
        return this;
    }

    /// <summary>
    /// Configures German voice using Azure Neural TTS with Cortana-like voice.
    /// </summary>
    /// <param name="subscriptionKey">Azure subscription key.</param>
    /// <param name="region">Azure region (e.g., "westeurope").</param>
    public VoicePipeline WithGermanVoice(string subscriptionKey, string region = "westeurope")
    {
        _language = "de";
        _voiceName = "de-DE-KatjaNeural"; // German female neural voice (Cortana-like)
        _ttsService = new AzureNeuralTtsService(subscriptionKey, region);
        _enableVoiceOutput = true;
        return this;
    }

    /// <summary>
    /// Configures default Whisper-based speech-to-text.
    /// </summary>
    /// <param name="apiKey">OpenAI API key.</param>
    public VoicePipeline WithWhisperSpeechToText(string apiKey)
    {
        _sttService = new WhisperSpeechToTextService(apiKey);
        _enableVoiceInput = true;
        return this;
    }

    /// <summary>
    /// Executes the pipeline and optionally generates voice output.
    /// </summary>
    /// <param name="outputAudioPath">Optional path to save the voice output. If null, no audio is generated.</param>
    /// <returns>A result containing the pipeline output and optional audio file path.</returns>
    public async Task<VoicePipelineResult> RunAsync(string? outputAudioPath = null)
    {
        // Execute the base pipeline
        PipelineResult result = await _basePipeline.RunAsync();

        if (!result.IsSuccess)
        {
            return VoicePipelineResult.Failure(result.Error!);
        }

        string? audioPath = null;

        // Generate voice output if enabled
        if (_enableVoiceOutput && _ttsService != null && !string.IsNullOrEmpty(outputAudioPath))
        {
            try
            {
                TextToSpeechOptions options = new TextToSpeechOptions(
                    Voice: TtsVoice.Alloy,  // Default voice, could be customized based on _voiceName
                    Format: "mp3"
                );
                
                Result<string, string> ttsResult = await _ttsService.SynthesizeToFileAsync(
                    result.Output!,
                    outputAudioPath,
                    options
                );
                
                if (ttsResult.IsSuccess)
                {
                    audioPath = ttsResult.Value;
                }
                else
                {
                    return VoicePipelineResult.Success(result.Output!, null, $"Voice generation failed: {ttsResult.Error}");
                }
            }
            catch (Exception ex)
            {
                // Voice generation failed, but text output succeeded
                return VoicePipelineResult.Success(result.Output!, null, $"Voice generation failed: {ex.Message}");
            }
        }

        return VoicePipelineResult.Success(result.Output!, audioPath);
    }

    /// <summary>
    /// Gets the underlying DSL representation.
    /// </summary>
    public string ToDSL()
    {
        string baseDsl = _basePipeline.ToDSL();
        return $@"{baseDsl}
Voice Configuration:
  Language: {_language}
  Voice: {_voiceName ?? "default"}
  Voice Input: {(_enableVoiceInput ? "enabled" : "disabled")}
  Voice Output: {(_enableVoiceOutput ? "enabled" : "disabled")}
  STT Service: {(_sttService?.GetType().Name ?? "none")}
  TTS Service: {(_ttsService?.GetType().Name ?? "none")}";
    }
}

/// <summary>
/// Represents the result of a voice-enabled pipeline execution.
/// </summary>
public sealed class VoicePipelineResult
{
    /// <summary>
    /// Gets whether the pipeline execution was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the text output from the pipeline, or null if execution failed.
    /// </summary>
    public string? TextOutput { get; }

    /// <summary>
    /// Gets the path to the generated audio file, or null if voice output was not requested or failed.
    /// </summary>
    public string? AudioPath { get; }

    /// <summary>
    /// Gets the error message if execution failed, or null if successful.
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// Gets a warning message if the main execution succeeded but voice generation failed.
    /// </summary>
    public string? Warning { get; }

    private VoicePipelineResult(bool isSuccess, string? textOutput, string? audioPath, string? error, string? warning = null)
    {
        IsSuccess = isSuccess;
        TextOutput = textOutput;
        AudioPath = audioPath;
        Error = error;
        Warning = warning;
    }

    internal static VoicePipelineResult Success(string textOutput, string? audioPath, string? warning = null)
    {
        return new VoicePipelineResult(true, textOutput, audioPath, null, warning);
    }

    internal static VoicePipelineResult Failure(string error)
    {
        return new VoicePipelineResult(false, null, null, error);
    }
}
