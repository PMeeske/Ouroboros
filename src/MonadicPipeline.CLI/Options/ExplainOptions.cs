using CommandLine;

namespace LangChainPipeline.Options;

[Verb("explain", HelpText = "Explain how a DSL is resolved.")]
sealed class ExplainOptions
{
    [Option('d', "dsl", Required = true, HelpText = "Pipeline DSL string.")]
    public string Dsl { get; set; } = string.Empty;
}