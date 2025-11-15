#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using CommandLine;

namespace LangChainPipeline.Options;

[Verb("list", HelpText = "List available pipeline tokens.")]
sealed class ListTokensOptions;
