#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using CommandLine;

namespace LangChainPipeline.Options;

[Verb("skills", HelpText = "Manage research-powered skills and DSL tokens.")]
sealed class SkillsOptions
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
}