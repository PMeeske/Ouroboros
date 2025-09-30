using CommandLine;

namespace LangChainPipeline.Options;

[Verb("test", HelpText = "Run end-to-end tests.")]
sealed class TestOptions
{
    [Option('s', "suite", Required = false, HelpText = "Test suite to run: cli|vector|memory|conversation|all", Default = "all")]
    public string Suite { get; set; } = "all";
}
