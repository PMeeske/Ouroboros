using CommandLine;

namespace LangChainPipeline.Options;

[Verb("ask", HelpText = "Ask the LLM. Use --rag to enable minimal RAG.")]
sealed class AskOptions
{
    [Option('r', "rag", Required = false, HelpText = "Enable minimal RAG context.")]
    public bool Rag { get; set; }

    [Option('q', "question", Required = true, HelpText = "Question text.")]
    public string Question { get; set; } = string.Empty;

    [Option("model", Required = false, HelpText = "Ollama chat model name", Default = "deepseek-coder:33b")]
    public string Model { get; set; } = "deepseek-coder:33b";

    [Option("embed", Required = false, HelpText = "Ollama embedding model name", Default = "nomic-embed-text")]
    public string Embed { get; set; } = "nomic-embed-text";

    [Option('k', "topk", Required = false, HelpText = "Number of context documents (RAG mode)", Default = 3)]
    public int K { get; set; } = 3;

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

    [Option("debug", Required = false, HelpText = "Enable verbose debug logging", Default = false)]
    public bool Debug { get; set; }

    [Option("agent", Required = false, HelpText = "Enable iterative agent loop with tool execution", Default = false)]
    public bool Agent { get; set; }

    [Option("agent-mode", Required = false, HelpText = "Agent implementation: simple|lc|react", Default = "lc")]
    public string AgentMode { get; set; } = "lc";

    [Option("agent-max-steps", Required = false, HelpText = "Max iterations for agent loop", Default = 6)]
    public int AgentMaxSteps { get; set; } = 6;

    [Option("strict-model", Required = false, HelpText = "Fail instead of falling back when remote model invalid", Default = false)]
    public bool StrictModel { get; set; }

    [Option("json-tools", Required = false, HelpText = "Force JSON tool call format {\"tool\":...,\"args\":{}}", Default = false)]
    public bool JsonTools { get; set; }

    [Option("endpoint", Required = false, HelpText = "Remote endpoint URL (overrides CHAT_ENDPOINT env var)")]
    public string? Endpoint { get; set; }

    [Option("api-key", Required = false, HelpText = "API key for remote endpoint (overrides CHAT_API_KEY env var)")]
    public string? ApiKey { get; set; }

    [Option("endpoint-type", Required = false, HelpText = "Endpoint type: auto|openai|ollama-cloud (overrides CHAT_ENDPOINT_TYPE env var)")]
    public string? EndpointType { get; set; }
}