// <copyright file="ExplainOptions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LangChainPipeline.Options;

using CommandLine;

[Verb("explain", HelpText = "Explain how a DSL is resolved.")]
internal sealed class ExplainOptions
{
    [Option('d', "dsl", Required = true, HelpText = "Pipeline DSL string.")]
    public string Dsl { get; set; } = string.Empty;
}
