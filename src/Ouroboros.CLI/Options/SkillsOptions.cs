#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using CommandLine;

namespace Ouroboros.Options;

[Verb("skills", HelpText = "Manage research-powered skills and DSL tokens. Use --voice for voice mode.")]
public sealed class SkillsOptions : IVoiceOptions
{
    [Option('l', "list", HelpText = "List all registered skills.")]
    public bool List { get; set; }

    [Option('t', "tokens", HelpText = "List auto-generated DSL tokens from skills.")]
    public bool Tokens { get; set; }

    [Option('f', "fetch", HelpText = "Fetch research papers and extract skills from a query.")]
    public string? Fetch { get; set; }

    [Option('s', "suggest", HelpText = "Suggest skills for a given goal.")]
    public string? Suggest { get; set; }

    [Option('r', "run", HelpText = "Run a DSL pipeline with skill tokens.")]
    public string? Run { get; set; }

    [Option('i', "interactive", HelpText = "Start interactive skills REPL mode.")]
    public bool Interactive { get; set; }

    [Option('v', "voice", HelpText = "Enable voice persona mode (speak & listen).")]
    public bool Voice { get; set; }

    [Option("persona", Default = "Ouroboros", HelpText = "Persona name for voice mode (Ouroboros, Aria, Nova, Echo, Sage).")]
    public string Persona { get; set; } = "Ouroboros";

    [Option('m', "model", Default = "deepseek-v3.1:671b-cloud", HelpText = "LLM model for natural language understanding (e.g., deepseek-v3.1:671b-cloud, llama3.2).")]
    public string Model { get; set; } = "deepseek-v3.1:671b-cloud";

    [Option("endpoint", Default = "http://localhost:11434", HelpText = "LLM endpoint URL (Ollama, OpenAI-compatible).")]
    public string Endpoint { get; set; } = "http://localhost:11434";

    // Qdrant storage options (skills are stored in Qdrant by default)
    [Option("qdrant", Default = "http://localhost:6334", HelpText = "Qdrant connection string for skill storage.")]
    public string QdrantEndpoint { get; set; } = "http://localhost:6334";

    [Option("qdrant-collection", Default = "ouroboros_skills", HelpText = "Qdrant collection name for skills.")]
    public string QdrantCollection { get; set; } = "ouroboros_skills";

    [Option("use-json", Default = false, HelpText = "Use JSON file storage instead of Qdrant (fallback mode).")]
    public bool UseJsonStorage { get; set; } = false;

    [Option("embed-model", Default = "nomic-embed-text", HelpText = "Embedding model for semantic skill search.")]
    public string EmbedModel { get; set; } = "nomic-embed-text";

    // Additional voice mode options
    [Option("voice-only", Required = false, HelpText = "Voice-only mode (no text output)", Default = false)]
    public bool VoiceOnly { get; set; }

    [Option("local-tts", Required = false, HelpText = "Prefer local TTS (Windows SAPI) over cloud", Default = true)]
    public bool LocalTts { get; set; } = true;

    [Option("voice-loop", Required = false, HelpText = "Continue voice conversation after command", Default = false)]
    public bool VoiceLoop { get; set; }
}
