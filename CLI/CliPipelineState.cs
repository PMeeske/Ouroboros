using LangChainPipeline.Providers;

namespace LangChainPipeline.CLI;

public sealed class CliPipelineState
{
    public required PipelineBranch Branch { get; set; }
    public required ToolAwareChatModel Llm { get; set; }
    public required ToolRegistry Tools { get; set; }
    // Generalized embedding model (was OllamaEmbeddingModel)
    public required IEmbeddingModel Embed { get; set; }

    public string Topic { get; set; } = string.Empty;
    public string Query { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public int RetrievalK { get; set; } = 8;
    public bool Trace { get; set; } = false;

    // Extended chain state (for new DSL style retrieval + template + llm steps)
    public List<string> Retrieved { get; } = new();
    public string Context { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
}