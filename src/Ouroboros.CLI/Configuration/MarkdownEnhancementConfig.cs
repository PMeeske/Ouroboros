namespace LangChainPipeline.CLI.Configuration;

public record MarkdownEnhancementConfig
{
    public required string FilePath { get; init; }
    public int Iterations { get; init; } = 1;
    public int ContextCount { get; init; } = 8;
    public bool CreateBackup { get; init; } = true;
    public string? Goal { get; init; }
}
