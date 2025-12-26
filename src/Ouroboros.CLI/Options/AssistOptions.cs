#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using CommandLine;

namespace Ouroboros.Options;

/// <summary>
/// Options for the DSL assistant command (GitHub Copilot-like behavior).
/// NOTE: For the full Ouroboros experience, use the default 'ouroboros' command instead.
/// This command is maintained for backward compatibility with DSL-mode workflows.
/// </summary>
[Verb("assist", HelpText = "[DEPRECATED: Use 'ouroboros' instead] Legacy DSL assistant mode. Use --dsl-mode for DSL-specific features.")]
public class AssistOptions : BaseModelOptions, IVoiceOptions
{
    // Voice mode options
    [Option('v', "voice", Required = false, Default = false, HelpText = "Enable voice input/output (speak & listen).")]
    public bool Voice { get; set; }

    [Option("dsl-mode", Required = false, Default = false, HelpText = "Run DSL assistant instead of Ouroboros persona.")]
    public bool DslMode { get; set; }

    [Option("persona", Required = false, Default = "Ouroboros", HelpText = "Persona name for voice mode.")]
    public string Persona { get; set; } = "Ouroboros";

    [Option("embed-model", Required = false, Default = "nomic-embed-text", HelpText = "Embedding model for voice mode.")]
    public string EmbedModel { get; set; } = "nomic-embed-text";

    [Option("qdrant", Required = false, Default = "http://localhost:6334", HelpText = "Qdrant endpoint for skills.")]
    public string QdrantEndpoint { get; set; } = "http://localhost:6334";

    // Explicit interface for Endpoint
    string IVoiceOptions.Endpoint { get => Endpoint ?? "http://localhost:11434"; set => Endpoint = value; }

    [Option('m', "mode", Required = false, Default = "suggest", HelpText = "Assistant mode: suggest, complete, validate, explain, build")]
    public string Mode { get; set; } = "suggest";

    [Option('d', "dsl", Required = false, HelpText = "DSL string to analyze or complete")]
    public string? Dsl { get; set; }

    [Option('g', "goal", Required = false, HelpText = "High-level goal for interactive DSL building")]
    public string? Goal { get; set; }

    [Option('p', "partial", Required = false, HelpText = "Partial token to complete")]
    public string? PartialToken { get; set; }

    [Option('n', "max-suggestions", Required = false, Default = 5, HelpText = "Maximum number of suggestions to generate")]
    public int MaxSuggestions { get; set; } = 5;

    [Option('i', "interactive", Required = false, Default = false, HelpText = "Start interactive assistant mode")]
    public bool Interactive { get; set; }

    [Option("stream", Required = false, Default = false, HelpText = "Stream responses as they are generated")]
    public bool Stream { get; set; }

    // Additional voice mode options
    [Option("voice-only", Required = false, HelpText = "Voice-only mode (no text output)", Default = false)]
    public bool VoiceOnly { get; set; }

    [Option("local-tts", Required = false, HelpText = "Prefer local TTS (Windows SAPI) over cloud", Default = true)]
    public bool LocalTts { get; set; } = true;

    [Option("voice-loop", Required = false, HelpText = "Continue voice conversation after command", Default = false)]
    public bool VoiceLoop { get; set; }
}

/// <summary>
/// Base class for options that use AI models.
/// </summary>
public abstract class BaseModelOptions
{
    [Option("model", Required = false, Default = "ministral-3:latest", HelpText = "Model to use")]
    public string Model { get; set; } = "ministral-3:latest";

    [Option("embed", Required = false, Default = "nomic-embed-text", HelpText = "Embedding model")]
    public string Embed { get; set; } = "nomic-embed-text";

    [Option("temperature", Required = false, Default = 0.7, HelpText = "Temperature for generation")]
    public double Temperature { get; set; } = 0.7;

    [Option("max-tokens", Required = false, Default = 2000, HelpText = "Maximum tokens to generate")]
    public int MaxTokens { get; set; } = 2000;

    [Option("timeout", Required = false, Default = 120, HelpText = "Timeout in seconds")]
    public int TimeoutSeconds { get; set; } = 120;

    [Option("debug", Required = false, Default = false, HelpText = "Enable debug output")]
    public bool Debug { get; set; }

    [Option("endpoint", Required = false, HelpText = "Remote endpoint URL (e.g., https://api.ollama.com)")]
    public string? Endpoint { get; set; }

    [Option("api-key", Required = false, HelpText = "API key for remote endpoint")]
    public string? ApiKey { get; set; }

    [Option("endpoint-type", Required = false, Default = "auto", HelpText = "Endpoint type: auto, ollama-cloud, openai, litellm, github-models")]
    public string EndpointType { get; set; } = "auto";
}
