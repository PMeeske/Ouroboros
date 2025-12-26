#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using CommandLine;

namespace Ouroboros.Options;

[Verb("test", HelpText = "Run integration tests.")]
public sealed class TestOptions
{
    [Option("integration", Required = false, HelpText = "Run only integration tests", Default = false)]
    public bool IntegrationOnly { get; set; }

    [Option("all", Required = false, HelpText = "Run all tests including integration", Default = false)]
    public bool All { get; set; }

    [Option("cli", Required = false, HelpText = "Run CLI end-to-end tests", Default = false)]
    public bool CliOnly { get; set; }

    [Option("metta", Required = false, HelpText = "Run MeTTa integration tests", Default = false)]
    public bool MeTTa { get; set; }
}
