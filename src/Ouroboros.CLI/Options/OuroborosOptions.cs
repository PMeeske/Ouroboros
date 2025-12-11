#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using CommandLine;

namespace LangChainPipeline.Options;

/// <summary>
/// Options for the unified Ouroboros agent mode.
/// This is the main entry point that integrates all capabilities.
/// </summary>
[Verb("ouroboros", isDefault: true, HelpText = "Run the unified Ouroboros AI agent with voice, skills, tools, and symbolic reasoning.")]
public sealed class OuroborosOptions
{
    [Option('v', "voice", Required = false, HelpText = "Enable voice mode (speak & listen)", Default = true)]
    public bool Voice { get; set; } = true;

    [Option("persona", Required = false, HelpText = "Persona: Ouroboros, Aria, Echo, Sage, Atlas", Default = "Ouroboros")]
    public string Persona { get; set; } = "Ouroboros";

    [Option("voice-only", Required = false, HelpText = "Voice-only mode (no text output)", Default = false)]
    public bool VoiceOnly { get; set; }

    [Option("local-tts", Required = false, HelpText = "Prefer local TTS (Windows SAPI) over cloud", Default = true)]
    public bool LocalTts { get; set; } = true;

    [Option('m', "model", Required = false, HelpText = "LLM model name", Default = "deepseek-v3.1:671b-cloud")]
    public string Model { get; set; } = "deepseek-v3.1:671b-cloud";

    [Option("endpoint", Required = false, HelpText = "LLM endpoint URL", Default = "https://api.ollama.com")]
    public string Endpoint { get; set; } = "https://api.ollama.com";

    [Option("embed-model", Required = false, HelpText = "Embedding model name", Default = "nomic-embed-text")]
    public string EmbedModel { get; set; } = "nomic-embed-text";

    [Option("embed-endpoint", Required = false, HelpText = "Embedding endpoint (defaults to local Ollama)", Default = "http://localhost:11434")]
    public string EmbedEndpoint { get; set; } = "http://localhost:11434";

    [Option("qdrant", Required = false, HelpText = "Qdrant endpoint for persistent memory", Default = "http://localhost:6334")]
    public string QdrantEndpoint { get; set; } = "http://localhost:6334";

    [Option("temperature", Required = false, HelpText = "Sampling temperature", Default = 0.7)]
    public double Temperature { get; set; } = 0.7;

    [Option("max-tokens", Required = false, HelpText = "Max tokens for completion", Default = 512)]
    public int MaxTokens { get; set; } = 512;

    [Option("debug", Required = false, HelpText = "Enable debug logging", Default = false)]
    public bool Debug { get; set; }

    [Option("api-key", Required = false, HelpText = "API key for remote endpoint")]
    public string? ApiKey { get; set; }

    [Option("endpoint-type", Required = false, HelpText = "Endpoint type: auto|openai|ollama-cloud|litellm|github-models")]
    public string? EndpointType { get; set; }

    [Option('g', "goal", Required = false, HelpText = "Optional initial goal to accomplish")]
    public string? Goal { get; set; }

    [Option('q', "question", Required = false, HelpText = "Optional initial question to answer")]
    public string? Question { get; set; }

    [Option("text-only", Required = false, HelpText = "Disable voice, use text input/output only", Default = false)]
    public bool TextOnly { get; set; }
}
