// <copyright file="TestOptions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Options;

using CommandLine;

[Verb("test", HelpText = "Run integration tests.")]
internal sealed class TestOptions
{
    [Option("integration", Required = false, HelpText = "Run only integration tests", Default = false)]
    public bool IntegrationOnly { get; set; }

    [Option("all", Required = false, HelpText = "Run all tests including integration", Default = false)]
    public bool All { get; set; }

    [Option("cli", Required = false, HelpText = "Run CLI end-to-end tests", Default = false)]
    public bool CliOnly { get; set; }
}
