using CommandLine;

namespace LangChainPipeline.Options;

[Verb("test", HelpText = "Run integration tests.")]
sealed class TestOptions
{
    [Option("integration", Required = false, HelpText = "Run only integration tests", Default = false)]
    public bool IntegrationOnly { get; set; }

    [Option("all", Required = false, HelpText = "Run all tests including integration", Default = false)]
    public bool All { get; set; }

    [Option("cli", Required = false, HelpText = "Run CLI end-to-end tests", Default = false)]
    public bool CliOnly { get; set; }
}
