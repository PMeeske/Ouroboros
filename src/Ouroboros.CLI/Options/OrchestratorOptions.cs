#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using CommandLine;

namespace LangChainPipeline.Options;

[Verb("orchestrator", HelpText = "Run smart model orchestrator with intelligent model and tool selection.")]
public sealed class OrchestratorOptions
{
    [Option('g', "goal", Required = true, HelpText = "Goal or task for the orchestrator to accomplish.")]
    public string Goal { get; set; } = string.Empty;

    [Option("model", Required = false, HelpText = "Primary Ollama chat model name", Default = "llama3")]
    public string Model { get; set; } = "llama3";

    [Option("coder-model", Required = false, HelpText = "Model for code/refactor tasks.", Default = "codellama")]
    public string? CoderModel { get; set; }

    [Option("reason-model", Required = false, HelpText = "Model for strategic reasoning tasks.")]
    public string? ReasonModel { get; set; }

    [Option("embed", Required = false, HelpText = "Ollama embedding model name", Default = "nomic-embed-text")]
    public string Embed { get; set; } = "nomic-embed-text";

    [Option("temperature", Required = false, HelpText = "Sampling temperature", Default = 0.7)]
    public double Temperature { get; set; } = 0.7;

    [Option("max-tokens", Required = false, HelpText = "Max tokens for completion", Default = 512)]
    public int MaxTokens { get; set; } = 512;

    [Option("timeout-seconds", Required = false, HelpText = "HTTP timeout for model", Default = 60)]
    public int TimeoutSeconds { get; set; } = 60;

    [Option("metrics", Required = false, HelpText = "Display performance metrics", Default = true)]
    public bool ShowMetrics { get; set; } = true;

    [Option("debug", Required = false, HelpText = "Enable verbose debug logging", Default = false)]
    public bool Debug { get; set; }

    [Option("endpoint", Required = false, HelpText = "Remote endpoint URL (overrides CHAT_ENDPOINT env var)")]
    public string? Endpoint { get; set; }

    [Option("api-key", Required = false, HelpText = "API key for remote endpoint (overrides CHAT_API_KEY env var)")]
    public string? ApiKey { get; set; }

    [Option("endpoint-type", Required = false, HelpText = "Endpoint type: auto|openai|ollama-cloud|litellm|github-models (overrides CHAT_ENDPOINT_TYPE env var)")]
    public string? EndpointType { get; set; }

    // Voice mode options
    [Option('v', "voice", Required = false, HelpText = "Enable voice mode (speak & listen)", Default = false)]
    public bool Voice { get; set; }

    [Option("persona", Required = false, HelpText = "Persona for voice mode: Ouroboros, Aria, Echo, Sage, Atlas", Default = "Ouroboros")]
    public string Persona { get; set; } = "Ouroboros";

    [Option("voice-only", Required = false, HelpText = "Voice-only mode (no text output)", Default = false)]
    public bool VoiceOnly { get; set; }

    [Option("local-tts", Required = false, HelpText = "Prefer local TTS (Windows SAPI) over cloud", Default = true)]
    public bool LocalTts { get; set; } = true;

    [Option("voice-loop", Required = false, HelpText = "Continue voice conversation after command", Default = false)]
    public bool VoiceLoop { get; set; }
}
