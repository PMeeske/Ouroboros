#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using CommandLine;

namespace LangChainPipeline.Options;

[Verb("pipeline", HelpText = "Run a pipeline DSL.")]
public sealed class PipelineOptions
{
    [Option('d', "dsl", Required = true, HelpText = "Pipeline DSL string.")]
    public string Dsl { get; set; } = string.Empty;

    [Option("model", Required = false, HelpText = "Ollama chat model name", Default = "deepseek-coder:33b")]
    public string Model { get; set; } = "deepseek-coder:33b";

    [Option("embed", Required = false, HelpText = "Ollama embedding model name", Default = "nomic-embed-text")]
    public string Embed { get; set; } = "nomic-embed-text";

    [Option("source", Required = false, HelpText = "Ingestion/source folder path", Default = ".")]
    public string Source { get; set; } = ".";

    [Option('k', "topk", Required = false, HelpText = "Similarity retrieval k", Default = 8)]
    public int K { get; set; } = 8;

    [Option('t', "trace", Required = false, HelpText = "Enable live trace output", Default = false)]
    public bool Trace { get; set; } = false;

    [Option("debug", Required = false, HelpText = "Enable verbose debug logging", Default = false)]
    public bool Debug { get; set; } = false;

    [Option("temperature", Required = false, HelpText = "Sampling temperature (remote models)", Default = 0.7)]
    public double Temperature { get; set; } = 0.7;

    [Option("max-tokens", Required = false, HelpText = "Max tokens for completion (remote models)", Default = 512)]
    public int MaxTokens { get; set; } = 512;

    [Option("timeout-seconds", Required = false, HelpText = "HTTP timeout for remote model", Default = 60)]
    public int TimeoutSeconds { get; set; } = 60;

    [Option("stream", Required = false, HelpText = "Stream output (simulated for now)", Default = false)]
    public bool Stream { get; set; }

    // Multi-model router options
    [Option("router", Required = false, HelpText = "Enable multi-model routing: off|auto", Default = "off")]
    public string Router { get; set; } = "off";

    [Option("coder-model", Required = false, HelpText = "Model for code/refactor prompts.")]
    public string? CoderModel { get; set; }

    [Option("summarize-model", Required = false, HelpText = "Model for long / summarization prompts.")]
    public string? SummarizeModel { get; set; }

    [Option("reason-model", Required = false, HelpText = "Model for strategic reasoning prompts.")]
    public string? ReasonModel { get; set; }

    [Option("general-model", Required = false, HelpText = "Fallback general model (overrides --model).")]
    public string? GeneralModel { get; set; }

    [Option("agent", Required = false, HelpText = "Enable iterative agent loop with tool execution", Default = false)]
    public bool Agent { get; set; }

    [Option("agent-mode", Required = false, HelpText = "Agent implementation: simple|lc|react|self-critique", Default = "lc")]
    public string AgentMode { get; set; } = "lc";

    [Option("agent-max-steps", Required = false, HelpText = "Max iterations for agent loop", Default = 6)]
    public int AgentMaxSteps { get; set; } = 6;

    [Option("critique-iterations", Required = false, HelpText = "Number of critique-improve cycles for self-critique mode (max 5)", Default = 1)]
    public int CritiqueIterations { get; set; } = 1;

    [Option("strict-model", Required = false, HelpText = "Fail instead of falling back when remote model invalid", Default = false)]
    public bool StrictModel { get; set; }

    [Option("json-tools", Required = false, HelpText = "Force JSON tool call format {\"tool\":...,\"args\":{}}", Default = false)]
    public bool JsonTools { get; set; }

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
