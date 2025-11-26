namespace LangChainPipeline.CLI.Configuration;

public record ModelSwitchConfig
{
    public string? ChatModel { get; init; }
    public string? EmbeddingModel { get; init; }
    public bool ForceRemote { get; init; } = false;
}
