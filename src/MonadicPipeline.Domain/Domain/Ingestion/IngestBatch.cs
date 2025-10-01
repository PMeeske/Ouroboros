namespace LangChainPipeline.Domain.Events;

public sealed record IngestBatch(
    Guid Id,
    string Source,
    IReadOnlyList<string> Ids,
    DateTime Timestamp
) : PipelineEvent(Id, "Ingest", Timestamp);
