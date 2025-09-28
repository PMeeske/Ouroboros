using CommandLine;

namespace LangChainPipeline.Options;

[Verb("pipeline", HelpText = "Run a pipeline DSL.")]
sealed class PipelineOptions
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
}