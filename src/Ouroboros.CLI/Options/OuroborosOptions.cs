#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using CommandLine;

namespace Ouroboros.Options;

/// <summary>
/// Options for the unified Ouroboros agent mode.
/// This is the main entry point that integrates ALL capabilities:
/// - Voice interaction (TTS/STT)
/// - Skill-based learning with Qdrant persistence
/// - MeTTa symbolic reasoning
/// - Dynamic tool creation (web search, URL fetch, calculator, Playwright)
/// - Personality engine with affective states
/// - Self-improvement and curiosity (AutonomousMind)
/// - Pipeline DSL execution
/// - Multi-model orchestration
/// </summary>
[Verb("ouroboros", isDefault: true, HelpText = "Run the unified Ouroboros AI agent with full capabilities. Just run 'ouroboros' for maximum experience.")]
public sealed class OuroborosOptions
{
    // ═══════════════════════════════════════════════════════════════════════════
    // VOICE & INTERACTION
    // ═══════════════════════════════════════════════════════════════════════════

    [Option('v', "voice", Required = false, HelpText = "Enable voice mode (speak & listen)", Default = true)]
    public bool Voice { get; set; } = true;

    [Option("text-only", Required = false, HelpText = "Disable voice, use text input/output only", Default = false)]
    public bool TextOnly { get; set; }

    [Option("voice-only", Required = false, HelpText = "Voice-only mode (no text output)", Default = false)]
    public bool VoiceOnly { get; set; }

    [Option("local-tts", Required = false, HelpText = "Prefer local TTS (Windows SAPI) over Azure", Default = false)]
    public bool LocalTts { get; set; }

    [Option("azure-tts", Required = false, HelpText = "Use Azure Text-to-Speech (default when available)", Default = true)]
    public bool AzureTts { get; set; } = true;

    [Option("azure-speech-key", Required = false, HelpText = "Azure Speech API key (or set AZURE_SPEECH_KEY env var)")]
    public string? AzureSpeechKey { get; set; }

    [Option("azure-speech-region", Required = false, HelpText = "Azure Speech region (default: eastus)", Default = "eastus")]
    public string AzureSpeechRegion { get; set; } = "eastus";

    [Option("tts-voice", Required = false, HelpText = "Azure TTS voice name (e.g. en-US-AvaMultilingualNeural)", Default = "en-US-AvaMultilingualNeural")]
    public string TtsVoice { get; set; } = "en-US-AvaMultilingualNeural";

    [Option("voice-channel", Required = false, HelpText = "Enable parallel voice side channel for persona-specific audio", Default = false)]
    public bool VoiceChannel { get; set; }

    [Option("listen", Required = false, HelpText = "Enable voice input (speech-to-text) on startup", Default = false)]
    public bool Listen { get; set; }

    [Option("persona", Required = false, HelpText = "Persona: Ouroboros, Aria, Echo, Sage, Atlas", Default = "Ouroboros")]
    public string Persona { get; set; } = "Ouroboros";

    // ═══════════════════════════════════════════════════════════════════════════
    // LLM & MODEL CONFIGURATION
    // ═══════════════════════════════════════════════════════════════════════════

    [Option('m', "model", Required = false, HelpText = "LLM model name", Default = "deepseek-v3.1:671b-cloud")]
    public string Model { get; set; } = "deepseek-v3.1:671b-cloud";

    [Option('c', "culture", Required = false, HelpText = "Target culture for the response (e.g. en-US, fr-FR, es).")]
    public string? Culture { get; set; }

    [Option("endpoint", Required = false, HelpText = "LLM endpoint URL", Default = "http://localhost:11434")]
    public string Endpoint { get; set; } = "http://localhost:11434";

    [Option("api-key", Required = false, HelpText = "API key for remote endpoint")]
    public string? ApiKey { get; set; }

    [Option("endpoint-type", Required = false, HelpText = "Endpoint type: auto|openai|ollama-cloud|litellm|github-models")]
    public string? EndpointType { get; set; }

    [Option("temperature", Required = false, HelpText = "Sampling temperature", Default = 0.7)]
    public double Temperature { get; set; } = 0.7;

    [Option("max-tokens", Required = false, HelpText = "Max tokens for completion", Default = 2048)]
    public int MaxTokens { get; set; } = 2048;

    [Option("timeout", Required = false, HelpText = "Request timeout in seconds", Default = 120)]
    public int TimeoutSeconds { get; set; } = 120;

    // ═══════════════════════════════════════════════════════════════════════════
    // EMBEDDINGS & MEMORY
    // ═══════════════════════════════════════════════════════════════════════════

    [Option("embed-model", Required = false, HelpText = "Embedding model name", Default = "nomic-embed-text")]
    public string EmbedModel { get; set; } = "nomic-embed-text";

    [Option("embed-endpoint", Required = false, HelpText = "Embedding endpoint (defaults to local Ollama)", Default = "http://localhost:11434")]
    public string EmbedEndpoint { get; set; } = "http://localhost:11434";

    [Option("qdrant", Required = false, HelpText = "Qdrant endpoint for persistent memory", Default = "http://localhost:6334")]
    public string QdrantEndpoint { get; set; } = "http://localhost:6334";

    // ═══════════════════════════════════════════════════════════════════════════
    // FEATURE TOGGLES (All enabled by default for max experience)
    // ═══════════════════════════════════════════════════════════════════════════

    [Option("no-skills", Required = false, HelpText = "Disable skill learning subsystem", Default = false)]
    public bool NoSkills { get; set; }

    [Option("no-metta", Required = false, HelpText = "Disable MeTTa symbolic reasoning", Default = false)]
    public bool NoMeTTa { get; set; }

    [Option("no-tools", Required = false, HelpText = "Disable dynamic tools (web search, etc.)", Default = false)]
    public bool NoTools { get; set; }

    [Option("no-personality", Required = false, HelpText = "Disable personality engine & affect", Default = false)]
    public bool NoPersonality { get; set; }

    [Option("no-mind", Required = false, HelpText = "Disable autonomous mind (inner thoughts)", Default = false)]
    public bool NoMind { get; set; }

    [Option("no-browser", Required = false, HelpText = "Disable Playwright browser automation", Default = false)]
    public bool NoBrowser { get; set; }

    // ═══════════════════════════════════════════════════════════════════════════
    // AUTONOMOUS/PUSH MODE (Ouroboros proposes actions for approval)
    // ═══════════════════════════════════════════════════════════════════════════

    [Option("push", Required = false, HelpText = "Enable push mode - Ouroboros proposes actions for your approval (defaults to text-only)", Default = false)]
    public bool Push { get; set; }

    [Option("push-voice", Required = false, HelpText = "Enable voice in push mode (by default push mode is text-only)", Default = false)]
    public bool PushVoice { get; set; }

    [Option("yolo", Required = false, HelpText = "YOLO mode - full autonomous operation, auto-approve ALL actions (use with caution!)", Default = false)]
    public bool Yolo { get; set; }

    [Option("auto-approve", Required = false, HelpText = "Auto-approve intention categories: safe,memory,analysis (comma-separated)", Default = "")]
    public string AutoApprove { get; set; } = "";

    [Option("intention-interval", Required = false, HelpText = "Seconds between autonomous intention proposals", Default = 45)]
    public int IntentionInterval { get; set; } = 45;

    [Option("discovery-interval", Required = false, HelpText = "Seconds between autonomous topic discovery", Default = 90)]
    public int DiscoveryInterval { get; set; } = 90;

    // ═══════════════════════════════════════════════════════════════════════════
    // GOVERNANCE & SELF-MODIFICATION
    // ═══════════════════════════════════════════════════════════════════════════

    [Option("enable-self-mod", Required = false, HelpText = "Enable self-modification for agent autonomy", Default = false)]
    public bool EnableSelfModification { get; set; }

    [Option("risk-level", Required = false, HelpText = "Minimum risk level for approval: Low|Medium|High|Critical", Default = "Medium")]
    public string RiskLevel { get; set; } = "Medium";

    [Option("auto-approve-low", Required = false, HelpText = "Auto-approve low-risk modifications", Default = true)]
    public bool AutoApproveLow { get; set; } = true;

    // ═══════════════════════════════════════════════════════════════════════════
    // INITIAL TASK (Optional)
    // ═══════════════════════════════════════════════════════════════════════════

    [Option('g', "goal", Required = false, HelpText = "Initial goal to accomplish (starts planning immediately)")]
    public string? Goal { get; set; }

    [Option('q', "question", Required = false, HelpText = "Initial question to answer")]
    public string? Question { get; set; }

    [Option('d', "dsl", Required = false, HelpText = "Pipeline DSL to execute immediately")]
    public string? Dsl { get; set; }

    // ═══════════════════════════════════════════════════════════════════════════
    // MULTI-MODEL ORCHESTRATION
    // ═══════════════════════════════════════════════════════════════════════════

    [Option("coder-model", Required = false, HelpText = "Model for code/refactor tasks")]
    public string? CoderModel { get; set; }

    [Option("reason-model", Required = false, HelpText = "Model for strategic reasoning")]
    public string? ReasonModel { get; set; }

    [Option("summarize-model", Required = false, HelpText = "Model for summarization")]
    public string? SummarizeModel { get; set; }

    // ═══════════════════════════════════════════════════════════════════════════
    // AGENT BEHAVIOR
    // ═══════════════════════════════════════════════════════════════════════════

    [Option("agent-max-steps", Required = false, HelpText = "Max steps for agent planning", Default = 10)]
    public int AgentMaxSteps { get; set; } = 10;

    [Option("thinking-interval", Required = false, HelpText = "Seconds between autonomous thoughts", Default = 30)]
    public int ThinkingInterval { get; set; } = 30;

    // ═══════════════════════════════════════════════════════════════════════════
    // PIPING & BATCH MODE
    // ═══════════════════════════════════════════════════════════════════════════

    [Option("pipe", Required = false, HelpText = "Enable pipe mode - read commands from stdin, output to stdout", Default = false)]
    public bool Pipe { get; set; }

    [Option("batch", Required = false, HelpText = "Batch file containing commands to execute (one per line)")]
    public string? BatchFile { get; set; }

    [Option("json-output", Required = false, HelpText = "Output responses as JSON for scripting", Default = false)]
    public bool JsonOutput { get; set; }

    [Option("no-greeting", Required = false, HelpText = "Skip greeting in non-interactive mode", Default = false)]
    public bool NoGreeting { get; set; }

    [Option("exit-on-error", Required = false, HelpText = "Exit immediately on command error in batch/pipe mode", Default = false)]
    public bool ExitOnError { get; set; }

    [Option('e', "exec", Required = false, HelpText = "Execute a single command and exit (supports | piping syntax)")]
    public string? Exec { get; set; }

    // ═══════════════════════════════════════════════════════════════════════════
    // DEBUG & OUTPUT
    // ═══════════════════════════════════════════════════════════════════════════

    [Option("debug", Required = false, HelpText = "Enable debug logging", Default = false)]
    public bool Debug { get; set; }

    [Option("trace", Required = false, HelpText = "Enable trace output for pipelines", Default = false)]
    public bool Trace { get; set; }

    [Option("metrics", Required = false, HelpText = "Show performance metrics", Default = false)]
    public bool ShowMetrics { get; set; }

    [Option("stream", Required = false, HelpText = "Stream responses as generated", Default = true)]
    public bool Stream { get; set; } = true;
}
