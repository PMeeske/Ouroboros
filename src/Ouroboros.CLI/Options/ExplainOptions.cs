#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using CommandLine;

namespace LangChainPipeline.Options;

[Verb("explain", HelpText = "Explain how a DSL is resolved.")]
public sealed class ExplainOptions
{
    [Option('d', "dsl", Required = true, HelpText = "Pipeline DSL string.")]
    public string Dsl { get; set; } = string.Empty;
}
