#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
using CommandLine;

namespace Ouroboros.Options;

[Verb("benchmark", HelpText = "Run benchmark suite to evaluate Ouroboros capabilities.")]
public sealed class BenchmarkOptions
{
    [Option("arc", Required = false, HelpText = "Run ARC-AGI-2 abstract reasoning benchmark", Default = false)]
    public bool ARC { get; set; }

    [Option("mmlu", Required = false, HelpText = "Run MMLU (Massive Multitask Language Understanding) benchmark", Default = false)]
    public bool MMLU { get; set; }

    [Option("continual", Required = false, HelpText = "Run continual learning benchmark", Default = false)]
    public bool Continual { get; set; }

    [Option("cognitive", Required = false, HelpText = "Run cognitive dimension benchmarks", Default = false)]
    public bool Cognitive { get; set; }

    [Option("full", Required = false, HelpText = "Run comprehensive evaluation across all benchmarks", Default = false)]
    public bool Full { get; set; }

    [Option("all", Required = false, HelpText = "Run all benchmarks sequentially", Default = false)]
    public bool All { get; set; }

    [Option("task-count", Required = false, HelpText = "Number of tasks for ARC benchmark", Default = 100)]
    public int TaskCount { get; set; }

    [Option("subjects", Required = false, HelpText = "Comma-separated list of subjects for MMLU (e.g., mathematics,physics)", Default = "mathematics,physics,computer_science,history")]
    public string Subjects { get; set; } = "mathematics,physics,computer_science,history";

    [Option("dimension", Required = false, HelpText = "Cognitive dimension to test (Reasoning, Planning, Learning, Memory, Generalization, Creativity, SocialIntelligence)", Default = "Reasoning")]
    public string Dimension { get; set; } = "Reasoning";

    [Option("output", Required = false, HelpText = "Output file path for benchmark results (JSON format)")]
    public string? OutputFile { get; set; }
}
