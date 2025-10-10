namespace LangChainPipeline.Domain;

public sealed record ToolExecution(
    string ToolName,
    string Arguments,
    string Output,
    DateTime Timestamp
);
