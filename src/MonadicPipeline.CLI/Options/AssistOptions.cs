#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using CommandLine;

namespace LangChainPipeline.Options;

/// <summary>
/// Options for the DSL assistant command (GitHub Copilot-like behavior).
/// </summary>
[Verb("assist", HelpText = "AI-powered DSL assistant with GitHub Copilot-like suggestions")]
public class AssistOptions : BaseModelOptions
{
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
}

/// <summary>
/// Base class for options that use AI models.
/// </summary>
public abstract class BaseModelOptions
{
    [Option("model", Required = false, Default = "llama3", HelpText = "Model to use")]
    public string Model { get; set; } = "llama3";

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

    [Option("endpoint-type", Required = false, Default = "auto", HelpText = "Endpoint type: auto, ollama-cloud, openai, litellm")]
    public string EndpointType { get; set; } = "auto";
}
