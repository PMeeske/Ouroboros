// <copyright file="MeTTaOptions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Options;

using CommandLine;

[Verb("metta", HelpText = "Run MeTTa orchestrator v3.0 with symbolic reasoning capabilities.")]
internal sealed class MeTTaOptions
{
    [Option('g', "goal", Required = true, HelpText = "Goal or task for the MeTTa orchestrator to plan and execute.")]
    public string Goal { get; set; } = string.Empty;

    [Option("model", Required = false, HelpText = "Ollama chat model name", Default = "llama3")]
    public string Model { get; set; } = "llama3";

    [Option("embed", Required = false, HelpText = "Ollama embedding model name", Default = "nomic-embed-text")]
    public string Embed { get; set; } = "nomic-embed-text";

    [Option("temperature", Required = false, HelpText = "Sampling temperature", Default = 0.7)]
    public double Temperature { get; set; } = 0.7;

    [Option("max-tokens", Required = false, HelpText = "Max tokens for completion", Default = 512)]
    public int MaxTokens { get; set; } = 512;

    [Option("timeout-seconds", Required = false, HelpText = "HTTP timeout for model", Default = 60)]
    public int TimeoutSeconds { get; set; } = 60;

    [Option("plan-only", Required = false, HelpText = "Only generate plan without execution", Default = false)]
    public bool PlanOnly { get; set; }

    [Option("metrics", Required = false, HelpText = "Display performance metrics", Default = true)]
    public bool ShowMetrics { get; set; } = true;

    [Option("debug", Required = false, HelpText = "Enable verbose debug logging", Default = false)]
    public bool Debug { get; set; }

    [Option("endpoint", Required = false, HelpText = "Remote endpoint URL (overrides CHAT_ENDPOINT env var)")]
    public string? Endpoint { get; set; }

    [Option("api-key", Required = false, HelpText = "API key for remote endpoint (overrides CHAT_API_KEY env var)")]
    public string? ApiKey { get; set; }

    [Option("endpoint-type", Required = false, HelpText = "Endpoint type: auto|openai|ollama-cloud (overrides CHAT_ENDPOINT_TYPE env var)")]
    public string? EndpointType { get; set; }
}
