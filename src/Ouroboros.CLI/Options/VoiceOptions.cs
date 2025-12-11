#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
namespace LangChainPipeline.Options;

/// <summary>
/// Shared voice mode options that can be mixed into any command.
/// </summary>
public interface IVoiceOptions
{
    /// <summary>
    /// Gets or sets whether voice mode is enabled.
    /// </summary>
    bool Voice { get; set; }

    /// <summary>
    /// Gets or sets the persona for voice mode.
    /// </summary>
    string Persona { get; set; }

    /// <summary>
    /// Gets or sets the voice-only mode (no text output).
    /// </summary>
    bool VoiceOnly { get; set; }

    /// <summary>
    /// Gets or sets whether to use local TTS (Windows SAPI) vs cloud.
    /// </summary>
    bool LocalTts { get; set; }

    /// <summary>
    /// Gets or sets whether to continue in voice loop after initial command.
    /// </summary>
    bool VoiceLoop { get; set; }
}

/// <summary>
/// Voice options attribute group for CommandLineParser.
/// Apply these to any options class that supports voice mode.
/// </summary>
public static class VoiceOptionsDefaults
{
    public const string DefaultPersona = "Ouroboros";
    public const bool DefaultVoiceOnly = false;
    public const bool DefaultLocalTts = true;
    public const bool DefaultVoiceLoop = true;
}
