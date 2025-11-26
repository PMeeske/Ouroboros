namespace LangChainPipeline.CLI.Configuration;

public record DivideAndConquerRagConfig
{
    public int RetrievalCount { get; init; } = 24;
    public int GroupSize { get; init; } = 6;
    public string Separator { get; init; } = "\n---\n";
    public string? CustomTemplate { get; init; }
    public string? FinalTemplate { get; init; }
    public bool StreamPartials { get; init; } = false;
}

public record DecomposeAndAggregateRagConfig
{
    public int SubQuestions { get; init; } = 4;
    public int DocsPerSubQuestion { get; init; } = 6;
    public int InitialRetrievalCount { get; init; } = 24;
    public string Separator { get; init; } = "\n---\n";
    public bool StreamOutputs { get; init; } = false;
    public string? DecomposeTemplate { get; init; }
    public string? SubQuestionTemplate { get; init; }
    public string? FinalTemplate { get; init; }
}
